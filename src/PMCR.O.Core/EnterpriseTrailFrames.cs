namespace PMCR.O.Core;

/// <summary>Canonical cross-product evidence envelope. Sensitive payloads belong in protected artifacts, not inline trail data.</summary>
public sealed record EnterpriseTrailFrame(
    string SchemaVersion,
    string OrganizationId,
    string ProjectId,
    string IntentId,
    string RunId,
    string AgentId,
    string CorrelationId,
    string FrameId,
    long Sequence,
    string? ParentFrameId,
    IReadOnlyList<string> CausalFrameIds,
    DateTimeOffset Timestamp,
    string OriginalSeed,
    string RefinedObjective,
    IReadOnlyList<string> Constraints,
    string? NextSeed,
    string Phase,
    string Capability,
    string Action,
    string Status,
    IReadOnlyList<string> EvidenceRefs,
    string PolicyId,
    string AuthorizationDecision,
    string? ApprovalRef,
    string CheckerVerdict,
    string Reflection,
    IReadOnlyList<string> Corrections,
    bool ObjectiveComplete,
    string? ProposedLesson,
    string LearningStatus,
    double? Confidence,
    string Provenance,
    bool ReuseEligible,
    string? PayloadDigest = null);

public sealed record TrailFrameValidationResult(bool IsValid, IReadOnlyList<string> Errors);

public static class EnterpriseTrailFrameValidator
{
    private static readonly string[] LearningStatuses = ["observation", "proposed", "validated", "approved", "rejected"];
    private static readonly string[] AuthorizationDecisions = ["allow", "require_approval", "deny", "not_applicable"];

    public static TrailFrameValidationResult Validate(EnterpriseTrailFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var errors = new List<string>();
        Required(frame.SchemaVersion, nameof(frame.SchemaVersion), errors);
        Required(frame.OrganizationId, nameof(frame.OrganizationId), errors);
        Required(frame.ProjectId, nameof(frame.ProjectId), errors);
        Required(frame.IntentId, nameof(frame.IntentId), errors);
        Required(frame.RunId, nameof(frame.RunId), errors);
        Required(frame.AgentId, nameof(frame.AgentId), errors);
        Required(frame.CorrelationId, nameof(frame.CorrelationId), errors);
        Required(frame.FrameId, nameof(frame.FrameId), errors);
        Required(frame.OriginalSeed, nameof(frame.OriginalSeed), errors);
        Required(frame.RefinedObjective, nameof(frame.RefinedObjective), errors);
        Required(frame.Phase, nameof(frame.Phase), errors);
        Required(frame.Capability, nameof(frame.Capability), errors);
        Required(frame.Action, nameof(frame.Action), errors);
        Required(frame.Status, nameof(frame.Status), errors);
        Required(frame.PolicyId, nameof(frame.PolicyId), errors);
        Required(frame.AuthorizationDecision, nameof(frame.AuthorizationDecision), errors);
        Required(frame.CheckerVerdict, nameof(frame.CheckerVerdict), errors);
        Required(frame.LearningStatus, nameof(frame.LearningStatus), errors);
        Required(frame.Provenance, nameof(frame.Provenance), errors);
        if (frame.Sequence < 1) errors.Add("Sequence must be >= 1.");
        if (frame.Timestamp == default) errors.Add("Timestamp is required.");
        if (!LearningStatuses.Contains(frame.LearningStatus, StringComparer.Ordinal)) errors.Add("Unknown learning status.");
        if (!AuthorizationDecisions.Contains(frame.AuthorizationDecision, StringComparer.Ordinal)) errors.Add("Unknown authorization decision.");
        if (frame.Confidence is < 0 or > 1) errors.Add("Confidence must be between 0 and 1.");
        if (frame.ReuseEligible && frame.LearningStatus is not ("validated" or "approved")) errors.Add("Only validated or approved lessons may be reuse-eligible.");
        if (frame.AuthorizationDecision == "require_approval" && string.IsNullOrWhiteSpace(frame.ApprovalRef)) errors.Add("Approval-required actions must reference an approval record or pending approval token.");
        if (frame.EvidenceRefs.Any(string.IsNullOrWhiteSpace)) errors.Add("Evidence references cannot be blank.");
        if (frame.CausalFrameIds.Any(string.IsNullOrWhiteSpace)) errors.Add("Causal frame IDs cannot be blank.");
        return new(errors.Count == 0, errors);
    }

    private static void Required(string? value, string name, List<string> errors)
    { if (string.IsNullOrWhiteSpace(value)) errors.Add($"{name} is required."); }
}

/// <summary>Guardrails for a child run that changes the system running its parent run.</summary>
public sealed record RecursiveCycleContext(
    string ParentRunId,
    string ChildRunId,
    string SelfTargetId,
    int Depth,
    int MaxDepth,
    int RemainingCycleBudget,
    IReadOnlyList<string> AncestorTargetIds,
    string BeforeSnapshotRef,
    bool HasRollbackPlan,
    bool IndependentCheckerAssigned,
    bool ChangesSecurityOrPermissions,
    bool ChangesProduction,
    bool HasExplicitApproval);

public sealed record RecursiveCycleDecision(bool Allowed, string Reason, bool RequiresApproval);

public static class RecursiveCycleGuard
{
    public static RecursiveCycleDecision Evaluate(RecursiveCycleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.ParentRunId) || string.IsNullOrWhiteSpace(context.ChildRunId) || string.IsNullOrWhiteSpace(context.SelfTargetId))
            return new(false, "Parent run, child run, and self-target identity are required.", false);
        if (context.ParentRunId == context.ChildRunId) return new(false, "A recursive child cannot reuse its parent run ID.", false);
        if (context.Depth < 1 || context.MaxDepth < 1 || context.Depth > context.MaxDepth) return new(false, "Maximum recursion depth reached or invalid.", false);
        if (context.RemainingCycleBudget < 1) return new(false, "Recursive cycle budget exhausted.", false);
        if (context.AncestorTargetIds.Contains(context.SelfTargetId, StringComparer.Ordinal)) return new(false, "Self-target cycle detected; stop to prevent recursion/oscillation.", false);
        if (string.IsNullOrWhiteSpace(context.BeforeSnapshotRef) || !context.HasRollbackPlan) return new(false, "Self-modification requires a before-snapshot reference and rollback plan.", false);
        if (!context.IndependentCheckerAssigned) return new(false, "Self-modification requires an independent checker.", false);
        var approvalNeeded = context.ChangesSecurityOrPermissions || context.ChangesProduction;
        if (approvalNeeded && !context.HasExplicitApproval) return new(false, "Security, permission, or production changes require explicit approval.", true);
        return new(true, "Within recursive budget and required safeguards.", approvalNeeded);
    }
}
