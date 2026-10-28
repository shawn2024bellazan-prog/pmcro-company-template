namespace PMCR.O.Continuation;

public static class PmcrSchemas
{
    public const string CycleResult = "pmcro.cycle-result.v1";
    public const string NextSeed = "pmcro.next-seed.v1";
}

public sealed record CycleRouting(string From, string ReplyTo, string Target, string ReturnRoute);

public sealed record CorrelationContext(
    string IntentId,
    string RunId,
    string CorrelationId,
    string CausationId,
    string ParentFrameId);

public sealed record CycleEvidence(IReadOnlyList<string> TrailFrameIds, string CheckerVerdict);

public sealed record CycleOutcome(string Phase, string Outcome, CycleEvidence Evidence);

public sealed record NextSeedCommand(string Target, string Route, string Action);

public sealed record SeedIntent(string Original, string Refined);

public sealed record SeedGovernance(string ApprovalStatus, bool InheritedAuthority);

public sealed record SeedExecution(
    string IdempotencyKey,
    int RecursionDepth,
    int MaxDepth,
    int RetryLimit);

public sealed record NextSeedEnvelope(
    string Schema,
    string SeedId,
    string SourceFrameId,
    NextSeedCommand Command,
    SeedIntent Intent,
    SeedGovernance Governance,
    SeedExecution Execution,
    string State);

public sealed record CycleResultEnvelope(
    string Schema,
    string MessageType,
    string Status,
    CycleRouting Routing,
    CorrelationContext Correlation,
    CycleOutcome Result,
    NextSeedEnvelope? NextSeed,
    bool ResponseRequired,
    string ResponseReplyTo,
    string ExpectedResponseMessage);

// Trusted context supplied by the caller's runtime, never reconstructed from the child reply.
public sealed record CallerContext(
    string AgentId,
    string ExpectedChildAgent,
    string IntentId,
    string RunId,
    string CorrelationId,
    string CommandId,
    string ParentFrameId,
    string ExpectedReturnRoute,
    int RecursionDepth,
    int MaxDepth,
    int MaxRetries);

public enum ContinuationStatus
{
    NoContinuation,
    Dispatched,
    Deferred,
    Rejected,
    Duplicate,
    ExecutionUncertain
}

public enum AuthorizationDisposition
{
    Approved,
    Deferred,
    Denied
}

public sealed record AuthorizationDecision(
    AuthorizationDisposition Disposition,
    string ReasonCode);

public sealed record ContinuationReceipt(
    ContinuationStatus Status,
    string ReasonCode,
    string CorrelationId,
    string? SeedId);

public sealed record ContinuationTrailRecord(
    DateTimeOffset RecordedAt,
    string CallerAgent,
    string IntentId,
    string RunId,
    string CorrelationId,
    string CausationId,
    string? SeedId,
    ContinuationStatus Status,
    string ReasonCode);

// Implementations must validate evidence against an authoritative store, not the child's verdict alone.
public interface IContinuationEvidenceVerifier
{
    Task<bool> VerifyAsync(CycleResultEnvelope result, CallerContext caller, CancellationToken cancellationToken);
}

// Implementations evaluate current permissions, policy, tenant and capability; no child-supplied grant is trusted.
public interface IContinuationPolicy
{
    Task<AuthorizationDecision> EvaluateAsync(NextSeedEnvelope seed, CallerContext caller, CancellationToken cancellationToken);
}

public interface ISeedClaimStore
{
    Task<bool> TryClaimAsync(string claimKey, CancellationToken cancellationToken);
}

public interface INextSeedDispatcher
{
    Task DispatchAsync(NextSeedEnvelope seed, CallerContext caller, CancellationToken cancellationToken);
}

public interface IContinuationTrailWriter
{
    Task WriteAsync(ContinuationTrailRecord record, CancellationToken cancellationToken);
}
