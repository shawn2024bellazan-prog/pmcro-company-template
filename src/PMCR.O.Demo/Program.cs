using PMCR.O.Core;
var trails = new JsonlTrailStore(Path.Combine(Environment.CurrentDirectory,".pmcro","trails"));
var runner = new CycleRunner(new Planner(),new Maker(),new Checker(),new Reflector(),trails,3);
var results = await runner.RunAsync(new SeedIntent("demo","Demonstrate one safe, in-memory PMCR-O cycle."));
foreach(var r in results) Console.WriteLine($"Cycle {r.CycleNumber}: {r.Verdict.ToString().ToUpperInvariant()} — {r.Reason}");
Console.WriteLine($"Trail directory: {Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,".pmcro","trails"))}");
sealed class Planner:IPlanner { public Task<ExecutionPlan> PlanAsync(CycleContext c,CancellationToken t)=>Task.FromResult(new ExecutionPlan(c.Seed.Objective,[new PlanStep("demo","Return deterministic in-memory evidence",ActionRisk.LocalValidation)],["One successful result with evidence"])); }
sealed class Maker:IMaker { public Task<MakeResult> MakeAsync(CycleContext c,ExecutionPlan p,CancellationToken t)=>Task.FromResult(new MakeResult([new StepResult("demo",true,"No external mutation performed",["Deterministic demo handler completed"])])); }
sealed class Checker:IChecker { public Task<CheckResult> CheckAsync(CycleContext c,ExecutionPlan p,MakeResult m,CancellationToken t){var ok=m.Steps.Count==p.Steps.Count&&m.Steps.All(s=>s.Succeeded&&s.Evidence.Count>0);return Task.FromResult(new CheckResult(ok,ok?1:0,ok?1:0,true,[],m.Steps.SelectMany(s=>s.Evidence).ToArray()));} }
sealed class Reflector:IReflector { public Task<Reflection> ReflectAsync(CycleContext c,CheckResult x,CancellationToken t)=>Task.FromResult(new Reflection(x.Passed?CycleVerdict.Accept:CycleVerdict.Loop,x.Passed?"Demo acceptance criteria satisfied":"Evidence missing",x.Passed?[]:["Ensure every step returns evidence"],x.Passed)); }
