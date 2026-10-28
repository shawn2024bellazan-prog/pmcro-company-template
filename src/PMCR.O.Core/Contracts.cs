namespace PMCR.O.Core;

public enum CycleVerdict { Accept, Loop, Escalate }
public enum ActionRisk { ReadOnly, LocalValidation, RepositoryMutation, ExternalCodeExecution, PrivilegeChange, Destructive, SecretAccess, Unknown }
public enum ApprovalDisposition { Allow, RequireHumanApproval, Deny }

public sealed record SeedIntent(string Id, string Objective, string? ParentSeedId = null);
public sealed record PlanStep(string Id, string Description, ActionRisk Risk, string Capability = "tests.local");
public sealed record ExecutionPlan(string Objective, IReadOnlyList<PlanStep> Steps, IReadOnlyList<string> AcceptanceCriteria);
public sealed record StepResult(string StepId, bool Succeeded, string Summary, IReadOnlyList<string> Evidence);
public sealed record MakeResult(IReadOnlyList<StepResult> Steps);
public sealed record CheckResult(bool Passed, double Completeness, double Correctness, bool PolicyCompliant, IReadOnlyList<string> Findings, IReadOnlyList<string> Evidence);
public sealed record Reflection(CycleVerdict Verdict, string Reason, IReadOnlyList<string> EarnedConstraints, bool ObjectiveComplete);
public sealed record CycleContext(SeedIntent Seed, int CycleNumber, int MaxCycles, IReadOnlyList<string> EarnedConstraints);
public sealed record CycleOutcome(string CycleId, int CycleNumber, CycleVerdict Verdict, string Reason, bool ObjectiveComplete, IReadOnlyList<string> EarnedConstraints, CheckResult CheckResult);
public sealed record ActionRequest(string ObjectiveId, string Actor, string Capability, ActionRisk Risk, string Description);
public sealed record ApprovalDecision(ApprovalDisposition Disposition, string Policy, string Reason);
public sealed record TrailFrame(string FrameId, string CycleId, int Sequence, DateTimeOffset Timestamp, string FrameType, string Actor, string Summary, IReadOnlyDictionary<string,string> Data, string? CorrectsFrameId = null);

public interface IPlanner { Task<ExecutionPlan> PlanAsync(CycleContext context, CancellationToken ct); }
public interface IMaker { Task<MakeResult> MakeAsync(CycleContext context, ExecutionPlan plan, CancellationToken ct); }
public interface IChecker { Task<CheckResult> CheckAsync(CycleContext context, ExecutionPlan plan, MakeResult result, CancellationToken ct); }
public interface IReflector { Task<Reflection> ReflectAsync(CycleContext context, CheckResult check, CancellationToken ct); }
public interface IApprovalPolicy { Task<ApprovalDecision> EvaluateAsync(ActionRequest request, CancellationToken ct); }
public interface ITrailStore { Task AppendAsync(TrailFrame frame, CancellationToken ct); Task<IReadOnlyList<TrailFrame>> ReadCycleAsync(string cycleId, CancellationToken ct); }
