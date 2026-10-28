using PMCR.O.Core;

namespace PMCR.O.SubagentReturn;

/// <summary>
/// Builds Trail Frames that the CALLER owns for every non-successful or
/// non-actionable subagent outcome. A child that never returned, returned late,
/// or returned something unusable cannot author its own failure record, so the
/// caller records the failure and attributes it, using reason codes only.
/// Child-supplied free text (for example a stop reason) is deliberately never copied.
/// </summary>
public sealed class CallerFailureFrameFactory
{
    public const string Capability = "pmcro.subagent-return";
    public const string Provenance = "pmcro.subagent-return.caller";

    private readonly CommandExecutionContext _context;
    private readonly Func<DateTimeOffset> _clock;

    public CallerFailureFrameFactory(CommandExecutionContext context, Func<DateTimeOffset>? clock = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>A failure/rejection frame: the caller records that this subagent run did not produce an actionable result.</summary>
    public EnterpriseTrailFrame CreateFailureFrame(
        TrustedSubagentInvocation invocation,
        string status,
        string reasonCode,
        SubagentFailureAttribution attribution,
        IReadOnlyList<string>? additionalEvidence = null) =>
        Build(invocation, "subagent-return-failure", "subagent-return.fail", status, reasonCode, attribution,
            additionalEvidence ?? []);

    /// <summary>A non-failure decision frame (completed, deferred, duplicate). It never asserts a checker PASS.</summary>
    public EnterpriseTrailFrame CreateDecisionFrame(
        TrustedSubagentInvocation invocation,
        string status,
        string reasonCode,
        IReadOnlyList<string> additionalEvidence) =>
        Build(invocation, "subagent-return-decision", "subagent-return.evaluate", status, reasonCode,
            SubagentFailureAttribution.None, additionalEvidence);

    private EnterpriseTrailFrame Build(
        TrustedSubagentInvocation invocation,
        string phase,
        string action,
        string status,
        string reasonCode,
        SubagentFailureAttribution attribution,
        IReadOnlyList<string> additionalEvidence)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(invocation.Caller);
        var caller = invocation.Caller;

        var evidence = new List<string>
        {
            "reason:" + reasonCode,
            "child-agent:" + caller.ExpectedChildAgent,
            "subagent-run-id:" + invocation.SubagentRunId,
            "causation-id:" + caller.CommandId,
            "parent-frame:" + caller.ParentFrameId
        };
        if (phase == "subagent-return-failure")
        {
            evidence.Insert(0, "failure-owner:caller");
            evidence.Insert(1, "failure-attribution:" + attribution.ToString().ToLowerInvariant());
        }
        evidence.AddRange(additionalEvidence);

        return new EnterpriseTrailFrame(
            SchemaVersion: "1.0",
            OrganizationId: _context.OrganizationId,
            ProjectId: _context.ProjectId,
            IntentId: caller.IntentId,
            RunId: caller.RunId,
            AgentId: caller.AgentId,
            CorrelationId: caller.CorrelationId,
            FrameId: "frame-" + Guid.NewGuid().ToString("N"),
            Sequence: 1,
            ParentFrameId: null,
            CausalFrameIds: [caller.ParentFrameId],
            Timestamp: _clock(),
            OriginalSeed: "subagent.return",
            RefinedObjective: "Evaluate a delegated subagent's terminal return under caller-owned governance",
            Constraints: ["subagent-failure-is-caller-owned", "child-return-confers-no-authority"],
            NextSeed: null,
            Phase: phase,
            Capability: Capability,
            Action: action,
            Status: status,
            EvidenceRefs: evidence,
            PolicyId: _context.PolicyId,
            AuthorizationDecision: "not_applicable",
            ApprovalRef: null,
            CheckerVerdict: "NOT_CHECKED",
            Reflection: reasonCode,
            Corrections: [],
            ObjectiveComplete: false,
            ProposedLesson: null,
            LearningStatus: "observation",
            Confidence: null,
            Provenance: Provenance,
            ReuseEligible: false,
            PayloadDigest: null);
    }
}
