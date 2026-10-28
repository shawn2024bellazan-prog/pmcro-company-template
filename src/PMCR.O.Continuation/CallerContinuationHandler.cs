namespace PMCR.O.Continuation;

/// <summary>
/// Processes a child's proposed next seed on behalf of the original calling agent.
/// A child message cannot approve its own continuation or bypass independent evidence checks.
/// </summary>
public sealed class CallerContinuationHandler
{
    private readonly IContinuationEvidenceVerifier _evidence;
    private readonly IContinuationPolicy _policy;
    private readonly ISeedClaimStore _claims;
    private readonly INextSeedDispatcher _dispatcher;
    private readonly IContinuationTrailWriter _trail;

    public CallerContinuationHandler(
        IContinuationEvidenceVerifier evidence,
        IContinuationPolicy policy,
        ISeedClaimStore claims,
        INextSeedDispatcher dispatcher,
        IContinuationTrailWriter trail)
    {
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _claims = claims ?? throw new ArgumentNullException(nameof(claims));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _trail = trail ?? throw new ArgumentNullException(nameof(trail));
    }

    public async Task<ContinuationReceipt> HandleAsync(
        CycleResultEnvelope message,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(caller);

        var error = Validate(message, caller);
        if (error is not null)
            return await RecordAsync(message, caller, ContinuationStatus.Rejected, error);

        var seed = message.NextSeed;
        if (seed is null)
            return await RecordAsync(message, caller, ContinuationStatus.NoContinuation, "no_next_seed");

        if (!EqualsOrdinal(message.Result.Evidence.CheckerVerdict, "passed"))
            return await RecordAsync(message, caller, ContinuationStatus.Deferred, "checker_not_passed");

        bool verified;
        try
        {
            verified = await _evidence.VerifyAsync(message, caller, cancellationToken);
        }
        catch (Exception)
        {
            return await RecordAsync(message, caller, ContinuationStatus.Deferred, "evidence_unavailable");
        }

        if (!verified)
            return await RecordAsync(message, caller, ContinuationStatus.Deferred, "evidence_unverified");

        AuthorizationDecision authorization;
        try
        {
            authorization = await _policy.EvaluateAsync(seed, caller, cancellationToken);
        }
        catch (Exception)
        {
            return await RecordAsync(message, caller, ContinuationStatus.Deferred, "policy_unavailable");
        }

        if (authorization is null)
            return await RecordAsync(message, caller, ContinuationStatus.Deferred, "policy_unavailable");

        if (authorization.Disposition == AuthorizationDisposition.Denied)
            return await RecordAsync(message, caller, ContinuationStatus.Rejected, "policy_denied");
        if (authorization.Disposition != AuthorizationDisposition.Approved)
            return await RecordAsync(message, caller, ContinuationStatus.Deferred, "policy_deferred");

        bool claimed;
        try
        {
            claimed = await _claims.TryClaimAsync(ClaimKey(seed, caller), cancellationToken);
        }
        catch (Exception)
        {
            return await RecordAsync(message, caller, ContinuationStatus.Deferred, "claim_store_unavailable");
        }

        if (!claimed)
            return await RecordAsync(message, caller, ContinuationStatus.Duplicate, "seed_already_claimed");

        // A durable trail write is required before any side-effecting dispatch.
        // A failed write aborts dispatch; the claim deliberately remains held for reconciliation.
        await RecordAsync(message, caller, ContinuationStatus.Deferred, "dispatch_claimed");

        try
        {
            await _dispatcher.DispatchAsync(seed, caller, cancellationToken);
        }
        catch (Exception)
        {
            // The dispatcher may have partially executed. Never release the claim or auto-retry.
            return await RecordAsync(message, caller, ContinuationStatus.ExecutionUncertain, "dispatch_outcome_uncertain");
        }

        return await RecordAsync(message, caller, ContinuationStatus.Dispatched, "continuation_dispatched");
    }

    private async Task<ContinuationReceipt> RecordAsync(
        CycleResultEnvelope message,
        CallerContext caller,
        ContinuationStatus status,
        string code)
    {
        var record = new ContinuationTrailRecord(
            DateTimeOffset.UtcNow,
            caller.AgentId,
            caller.IntentId,
            caller.RunId,
            caller.CorrelationId,
            caller.CommandId,
            message.NextSeed?.SeedId,
            status,
            code);

        // Use a separate bounded trail implementation in production; cancellation cannot silently erase evidence.
        await _trail.WriteAsync(record, CancellationToken.None);
        return new ContinuationReceipt(status, code, caller.CorrelationId, message.NextSeed?.SeedId);
    }

    public static string? Validate(CycleResultEnvelope message, CallerContext caller)
    {
        if (!EqualsOrdinal(message.Schema, PmcrSchemas.CycleResult) ||
            !EqualsOrdinal(message.MessageType, "cycle.result"))
            return "invalid_schema";

        if (message.Routing is null || message.Correlation is null ||
            message.Result is null || message.Result.Evidence is null)
            return "invalid_message";

        if (!EqualsOrdinal(message.Routing.From, caller.ExpectedChildAgent) ||
            !EqualsOrdinal(message.Routing.ReplyTo, caller.AgentId) ||
            !EqualsOrdinal(message.Routing.Target, caller.AgentId) ||
            !EqualsOrdinal(message.Routing.ReturnRoute, caller.ExpectedReturnRoute) ||
            !message.ResponseRequired ||
            !EqualsOrdinal(message.ResponseReplyTo, caller.ExpectedChildAgent) ||
            !EqualsOrdinal(message.ExpectedResponseMessage, "next_seed.acceptance"))
            return "invalid_return_route";

        var ids = message.Correlation;
        if (!EqualsOrdinal(ids.IntentId, caller.IntentId) ||
            !EqualsOrdinal(ids.RunId, caller.RunId) ||
            !EqualsOrdinal(ids.CorrelationId, caller.CorrelationId) ||
            !EqualsOrdinal(ids.CausationId, caller.CommandId) ||
            !EqualsOrdinal(ids.ParentFrameId, caller.ParentFrameId) ||
            string.IsNullOrWhiteSpace(ids.ParentFrameId))
            return "correlation_mismatch";

        if (!EqualsOrdinal(message.Status, "completed") ||
            !EqualsOrdinal(message.Result.Phase, "reflect") ||
            !EqualsOrdinal(message.Result.Outcome, "completed"))
            return "cycle_not_completed";

        var seed = message.NextSeed;
        if (seed is null)
            return null;

        if (!EqualsOrdinal(seed.Schema, PmcrSchemas.NextSeed) ||
            !EqualsOrdinal(seed.State, "proposed") ||
            string.IsNullOrWhiteSpace(seed.SeedId) ||
            string.IsNullOrWhiteSpace(seed.SourceFrameId) ||
            seed.Command is null || seed.Intent is null ||
            seed.Governance is null || seed.Execution is null)
            return "invalid_next_seed";

        if (message.Result.Evidence.TrailFrameIds is null ||
            !message.Result.Evidence.TrailFrameIds.Contains(seed.SourceFrameId, StringComparer.Ordinal) ||
            !message.Result.Evidence.TrailFrameIds.Contains(caller.ParentFrameId, StringComparer.Ordinal))
            return "missing_evidence_reference";

        if (!seed.Command.Target.StartsWith('@') ||
            !seed.Command.Route.StartsWith('/') ||
            string.IsNullOrWhiteSpace(seed.Command.Action) ||
            string.IsNullOrWhiteSpace(seed.Intent.Original) ||
            string.IsNullOrWhiteSpace(seed.Intent.Refined))
            return "invalid_next_command";

        if (seed.Governance.InheritedAuthority ||
            !EqualsOrdinal(seed.Governance.ApprovalStatus, "pending_policy_evaluation"))
            return "untrusted_authority";

        if (caller.RecursionDepth < 0 || caller.MaxDepth <= 0 ||
            caller.RecursionDepth >= caller.MaxDepth ||
            seed.Execution.RecursionDepth != caller.RecursionDepth + 1 ||
            seed.Execution.MaxDepth <= 0 ||
            seed.Execution.MaxDepth > caller.MaxDepth ||
            seed.Execution.RecursionDepth > seed.Execution.MaxDepth ||
            seed.Execution.RetryLimit < 0 || seed.Execution.RetryLimit > caller.MaxRetries ||
            string.IsNullOrWhiteSpace(seed.Execution.IdempotencyKey))
            return "execution_bounds_exceeded";

        return null;
    }

    private static bool EqualsOrdinal(string? a, string b) =>
        string.Equals(a, b, StringComparison.Ordinal);

    private static string ClaimKey(NextSeedEnvelope seed, CallerContext caller)
    {
        // Length-prefix each part so user-controlled separators cannot create key collisions.
        var values = new[] { caller.AgentId, caller.IntentId, caller.RunId, seed.SeedId, seed.Execution.IdempotencyKey };
        return string.Concat(values.Select(value => $"{value.Length}:{value}"));
    }
}
