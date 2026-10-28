namespace PMCR.O.Core;

/// <summary>Bounded controller. Orchestrator alone owns continuation and hard verdict guards.</summary>
public sealed class CycleRunner
{
    private readonly IPlanner _planner; private readonly IMaker _maker; private readonly IChecker _checker; private readonly IReflector _reflector; private readonly ITrailStore _trails; private readonly IApprovalPolicy _approvalPolicy; private readonly int _maxCycles;
    public CycleRunner(IPlanner planner, IMaker maker, IChecker checker, IReflector reflector, ITrailStore trails, int maxCycles = 3, IApprovalPolicy? approvalPolicy = null) {
        if (maxCycles < 1) throw new ArgumentOutOfRangeException(nameof(maxCycles));
        (_planner,_maker,_checker,_reflector,_trails,_maxCycles,_approvalPolicy) = (planner,maker,checker,reflector,trails,maxCycles,approvalPolicy ?? new ConservativeApprovalPolicy());
    }

    public async Task<IReadOnlyList<CycleOutcome>> RunAsync(SeedIntent seed, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(seed);
        if (string.IsNullOrWhiteSpace(seed.Id) || string.IsNullOrWhiteSpace(seed.Objective)) throw new ArgumentException("Seed ID and objective are required.");
        var outcomes = new List<CycleOutcome>(); var constraints = new List<string>();
        for (var n=1; n<=_maxCycles; n++) {
            ct.ThrowIfCancellationRequested(); var cycleId = $"{seed.Id}-cycle-{n:D3}";
            var context = new CycleContext(seed,n,_maxCycles,constraints.ToArray());
            await Record(cycleId,"cycle.started","orchestrator","Cycle started",new Dictionary<string,string>{{"objective",seed.Objective},{"cycle",n.ToString()}},ct);
            var plan = await _planner.PlanAsync(context,ct);
            await Record(cycleId,"plan.completed","planner","Plan returned",new Dictionary<string,string>{{"steps",plan.Steps.Count.ToString()}},ct);
            foreach (var step in plan.Steps)
            {
                var decision = await _approvalPolicy.EvaluateAsync(new ActionRequest(seed.Id,"maker",step.Capability,step.Risk,step.Description),ct);
                await Record(cycleId,"action.authorization","orchestrator",decision.Reason,new Dictionary<string,string>{{"stepId",step.Id},{"capability",step.Capability},{"risk",step.Risk.ToString()},{"disposition",decision.Disposition.ToString()},{"policy",decision.Policy}},ct);
                if (decision.Disposition != ApprovalDisposition.Allow)
                {
                    var blockedCheck = new CheckResult(false,0,0,false,[$"approval-disposition:{decision.Disposition}",decision.Reason],[]);
                    var blockedOutcome = new CycleOutcome(cycleId,n,CycleVerdict.Escalate,$"Execution blocked by approval policy: {decision.Reason}",false,constraints.ToArray(),blockedCheck);
                    outcomes.Add(blockedOutcome);
                    await Record(cycleId,"reflect.verdict","orchestrator",blockedOutcome.Reason,new Dictionary<string,string>{{"verdict","ESCALATE"},{"objectiveComplete","False"}},ct);
                    return outcomes;
                }
            }
            var made = await _maker.MakeAsync(context,plan,ct);
            await Record(cycleId,"make.completed","maker","Execution phase returned",new Dictionary<string,string>{{"steps",made.Steps.Count.ToString()},{"failed",made.Steps.Count(x=>!x.Succeeded).ToString()}},ct);
            var check = await _checker.CheckAsync(context,plan,made,ct);
            await Record(cycleId,"check.completed","checker","Independent verification returned",new Dictionary<string,string>{{"passed",check.Passed.ToString()},{"policyCompliant",check.PolicyCompliant.ToString()}},ct);
            var reflect = await _reflector.ReflectAsync(context,check,ct);
            var verdict=reflect.Verdict; var reason=reflect.Reason;
            if (!check.PolicyCompliant) { verdict=CycleVerdict.Escalate; reason="Policy compliance failed; orchestrator overrides reflector."; }
            else if (verdict==CycleVerdict.Accept && !check.Passed) { verdict=n<_maxCycles?CycleVerdict.Loop:CycleVerdict.Escalate; reason="Independent check failed; ACCEPT rejected."; }
            else if ((verdict==CycleVerdict.Loop || (verdict==CycleVerdict.Accept && !reflect.ObjectiveComplete)) && n==_maxCycles) { verdict=CycleVerdict.Escalate; reason="Cycle budget exhausted before objective completion; human review required."; }
            if (verdict==CycleVerdict.Loop) constraints.AddRange(reflect.EarnedConstraints.Where(x=>!string.IsNullOrWhiteSpace(x)));
            var outcome=new CycleOutcome(cycleId,n,verdict,reason,reflect.ObjectiveComplete && verdict==CycleVerdict.Accept, constraints.ToArray(),check); outcomes.Add(outcome);
            await Record(cycleId,"reflect.verdict","orchestrator",reason,new Dictionary<string,string>{{"verdict",verdict.ToString().ToUpperInvariant()},{"objectiveComplete",outcome.ObjectiveComplete.ToString()}},ct);
            if (verdict==CycleVerdict.Escalate || (verdict==CycleVerdict.Accept && outcome.ObjectiveComplete)) break;
            // ACCEPT with objective incomplete continues automatically; LOOP retries within budget.
        }
        return outcomes;
    }

    private Task Record(string cycle,string type,string actor,string summary,IReadOnlyDictionary<string,string> data,CancellationToken ct) =>
        _trails.AppendAsync(new TrailFrame(Guid.NewGuid().ToString("N"),cycle,0,DateTimeOffset.UtcNow,type,actor,summary,data),ct);
}
