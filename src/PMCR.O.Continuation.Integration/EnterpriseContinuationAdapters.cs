using PMCR.O.Continuation;
using PMCR.O.Core;

namespace PMCR.O.Continuation.Integration;

/// <summary>
/// An external checker proof must be verified from independently governed
/// evidence, not inferred from a generic Trail Frame's PASS string.
/// </summary>
public interface IIndependentContinuationCheckerProof
{
    Task<bool> VerifyAsync(
        CycleResultEnvelope result,
        CallerContext caller,
        IReadOnlyList<EnterpriseTrailFrame> correlatedFrames,
        CancellationToken cancellationToken);
}

public sealed class UnconfiguredContinuationCheckerProof : IIndependentContinuationCheckerProof
{
    public Task<bool> VerifyAsync(
        CycleResultEnvelope result,
        CallerContext caller,
        IReadOnlyList<EnterpriseTrailFrame> correlatedFrames,
        CancellationToken cancellationToken) => Task.FromResult(false);
}

/// <summary>Correlates real source/parent frames before asking a separate checker.</summary>
public sealed class EnterpriseContinuationEvidenceVerifier : IContinuationEvidenceVerifier
{
    private readonly IEnterpriseTrailStore _store;
    private readonly IIndependentContinuationCheckerProof _checker;

    public EnterpriseContinuationEvidenceVerifier(
        IEnterpriseTrailStore store,
        IIndependentContinuationCheckerProof checker)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
    }

    public async Task<bool> VerifyAsync(
        CycleResultEnvelope result,
        CallerContext caller,
        CancellationToken cancellationToken)
    {
        if (result.NextSeed is null || result.Result?.Evidence?.TrailFrameIds is null)
            return false;

        var required = result.Result.Evidence.TrailFrameIds;
        if (required.Count < 2 || required.Any(string.IsNullOrWhiteSpace) ||
            required.Distinct(StringComparer.Ordinal).Count() != required.Count ||
            !required.Contains(caller.ParentFrameId, StringComparer.Ordinal) ||
            !required.Contains(result.NextSeed.SourceFrameId, StringComparer.Ordinal))
            return false;

        var frames = await _store.ReadRunAsync(caller.RunId, cancellationToken);
        var correlated = frames.Where(frame =>
            string.Equals(frame.IntentId, caller.IntentId, StringComparison.Ordinal) &&
            string.Equals(frame.RunId, caller.RunId, StringComparison.Ordinal) &&
            string.Equals(frame.CorrelationId, caller.CorrelationId, StringComparison.Ordinal) &&
            required.Contains(frame.FrameId, StringComparer.Ordinal)).ToArray();

        if (correlated.Length != required.Count ||
            correlated.Select(frame => frame.FrameId).Distinct(StringComparer.Ordinal).Count() != required.Count ||
            correlated.Any(frame => !EnterpriseTrailFrameValidator.Validate(frame).IsValid))
            return false;

        return await _checker.VerifyAsync(result, caller, correlated, cancellationToken);
    }
}

/// <summary>Writes a correlated observation, never a fabricated checker PASS.</summary>
public sealed class EnterpriseContinuationTrailWriter : IContinuationTrailWriter
{
    private readonly IEnterpriseTrailStore _store;
    private readonly CommandExecutionContext _context;

    public EnterpriseContinuationTrailWriter(
        IEnterpriseTrailStore store,
        CommandExecutionContext context)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task WriteAsync(ContinuationTrailRecord record, CancellationToken cancellationToken)
    {
        var frame = new EnterpriseTrailFrame(
            SchemaVersion: "1.0",
            OrganizationId: _context.OrganizationId,
            ProjectId: _context.ProjectId,
            IntentId: record.IntentId,
            RunId: record.RunId,
            AgentId: _context.AgentId,
            CorrelationId: record.CorrelationId,
            FrameId: "frame-" + Guid.NewGuid().ToString("N"),
            Sequence: 1,
            ParentFrameId: null,
            CausalFrameIds: [],
            Timestamp: record.RecordedAt,
            OriginalSeed: "continuation.return",
            RefinedObjective: "Evaluate caller-governed continuation",
            Constraints: ["next-seed-does-not-inherit-authority"],
            NextSeed: null,
            Phase: "continuation-decision",
            Capability: "pmcro.continuation",
            Action: "next-seed.evaluate",
            Status: record.Status.ToString().ToLowerInvariant(),
            EvidenceRefs: ["continuation:" + record.ReasonCode, "causation-id:" + record.CausationId,
                "seed-id:" + (record.SeedId ?? "none")],
            PolicyId: _context.PolicyId,
            AuthorizationDecision: "not_applicable",
            ApprovalRef: null,
            CheckerVerdict: "NOT_CHECKED",
            Reflection: record.ReasonCode,
            Corrections: [],
            ObjectiveComplete: false,
            ProposedLesson: null,
            LearningStatus: "observation",
            Confidence: null,
            Provenance: "pmcro.continuation.integration",
            ReuseEligible: false,
            PayloadDigest: null);
        return _store.AppendAsync(frame, cancellationToken);
    }
}
