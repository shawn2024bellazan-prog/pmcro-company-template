namespace PMCR.O.Core;

public sealed record CommandExecutionContext(string OrganizationId, string ProjectId, string AgentId, string PolicyId);
public sealed record CommandExecutionResult<T>(string Status, string Code, string Summary, T? Payload, IReadOnlyList<string> EvidenceRefs, bool RequiresHumanAction = false, string? NextSeed = null);

public interface IPmcroCommandHandler<TPayload, TResult>
{
    string CommandType { get; }
    Task<CommandExecutionResult<TResult>> HandleAsync(PmcroCommand<TPayload> command, CancellationToken cancellationToken);
}

public interface ICommandAuthorizationGate<TPayload>
{
    Task<ApprovalDecision> AuthorizeAsync(PmcroCommand<TPayload> command, CancellationToken cancellationToken);
}

/// <summary>Application-wide router. All transports should normalize into this contract before execution.</summary>
public sealed class PmcroApplicationCommandRouter<TPayload, TResult>
{
    private readonly IReadOnlyDictionary<string, IPmcroCommandHandler<TPayload, TResult>> _handlers;
    private readonly ICommandAuthorizationGate<TPayload> _authorization;
    private readonly IEnterpriseTrailStore _trail;
    private readonly CommandExecutionContext _context;
    private readonly ICommandIdempotencyStore _idempotency;

    public PmcroApplicationCommandRouter(IEnumerable<IPmcroCommandHandler<TPayload, TResult>> handlers,
        ICommandAuthorizationGate<TPayload> authorization, IEnterpriseTrailStore trail, CommandExecutionContext context, ICommandIdempotencyStore? idempotency = null)
    {
        _handlers = handlers.ToDictionary(x => x.CommandType, StringComparer.Ordinal);
        _authorization = authorization;
        _trail = trail;
        _context = context;
        _idempotency = idempotency ?? new InMemoryCommandIdempotencyStore();
    }

    public async Task<PmcroMessage<TResult>> DispatchAsync(PmcroCommand<TPayload> command, CancellationToken cancellationToken = default)
    {
        var errors = PmcroCommandValidator.Validate(command);
        if (errors.Count > 0)
            return await RespondAsync(command, "rejected", "COMMAND_INVALID", "Command envelope failed validation.", default, errors, false, "command.validation.correct", "deny", cancellationToken);

        if (!_handlers.TryGetValue(command.CommandType, out var handler))
            return await RespondAsync(command, "rejected", "HANDLER_NOT_FOUND", "No handler is registered for this command type.", default, ["router:handler-not-found"], false, "capability.discovery.review", "deny", cancellationToken);

        ApprovalDecision decision;
        try { decision = await _authorization.AuthorizeAsync(command, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await RespondAsync(command, "failed", "AUTHORIZATION_UNAVAILABLE", "Authorization could not be completed; execution was not started.", default, ["authorization:unavailable", "error-type:" + ex.GetType().Name], true, "governance.authorization.review", "deny", cancellationToken);
        }
        if (decision.Disposition != ApprovalDisposition.Allow)
        {
            var requiresHuman = decision.Disposition == ApprovalDisposition.RequireHumanApproval;
            return await RespondAsync(command, requiresHuman ? "waiting_for_approval" : "rejected", "POLICY_BLOCKED", decision.Reason, default,
                [$"policy:{decision.Disposition}", decision.Policy], requiresHuman, requiresHuman ? "approval.request.review" : "governance.policy.review",
                requiresHuman ? "require_approval" : "deny", cancellationToken);
        }

        IdempotencyClaim claim;
        try { claim = await _idempotency.TryBeginAsync(command.ActorId, command.CommandType, command.IdempotencyKey, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await RespondAsync(command, "failed", "IDEMPOTENCY_UNAVAILABLE", "Duplicate-execution protection is unavailable; command execution was not started.", default,
                ["idempotency:unavailable", "error-type:" + ex.GetType().Name], false, "infrastructure.idempotency.review", "deny", cancellationToken);
        }
        if (claim.Status != IdempotencyClaimStatus.Acquired)
            return await RespondAsync(command, "rejected", claim.Status == IdempotencyClaimStatus.AlreadyCompleted ? "DUPLICATE_COMMAND_COMPLETED" : "DUPLICATE_COMMAND_IN_PROGRESS",
                claim.Status == IdempotencyClaimStatus.AlreadyCompleted ? "This actor, command type, and idempotency key already completed; no second execution occurred." : "An execution with this idempotency key is already in progress; no second execution occurred.",
                default, ["idempotency:" + claim.Status], false, "command.result.lookup", "deny", cancellationToken);

        try
        {
            await RecordAsync(command, "execution-start", "started", "allow", ["policy:" + decision.Policy, "idempotency:claimed"], null, cancellationToken);
            var result = await handler.HandleAsync(command, cancellationToken);
            // A handler cannot retroactively turn an already-running operation into an approval request.
            if (result.RequiresHumanAction || result.Status == "waiting_for_approval")
            {
                await _idempotency.ReleaseAsync(claim.ScopeKey, CancellationToken.None);
                var evidence = result.EvidenceRefs.Concat(["contract:handler-returned-approval-after-dispatch"]).ToArray();
                var violationFrameId = await RecordAsync(command, "execution-contract-violation", "failed", "deny", evidence, "governance.handler-contract.review", CancellationToken.None);
                return new("1.0", "msg-" + Guid.NewGuid().ToString("N"), command.CommandId, command.IntentId, command.RunId,
                    command.CorrelationId, command.CommandId, "failed", "HANDLER_APPROVAL_CONTRACT_VIOLATION", "A handler requested approval after dispatch began; result was blocked and requires handler correction.",
                    DateTimeOffset.UtcNow, default, evidence, violationFrameId, false);
            }
            await _idempotency.CompleteAsync(claim.ScopeKey, cancellationToken);
            var status = result.Status;
            var frameId = await RecordAsync(command, "execution-result", status, "allow", result.EvidenceRefs.Concat(["idempotency:completed"]).ToArray(), result.NextSeed, cancellationToken);
            return new("1.0", "msg-" + Guid.NewGuid().ToString("N"), command.CommandId, command.IntentId, command.RunId,
                command.CorrelationId, command.CommandId, status, result.Code, result.Summary, DateTimeOffset.UtcNow,
                result.Payload, result.EvidenceRefs, frameId, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _idempotency.ReleaseAsync(claim.ScopeKey, CancellationToken.None);
            await RecordAsync(command, "execution-cancelled", "cancelled", "allow", ["execution:cancelled", "idempotency:released"], null, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            // Conservatively mark failed execution as completed: retrying a non-idempotent side effect could duplicate it.
            await _idempotency.CompleteAsync(claim.ScopeKey, CancellationToken.None);
            var evidence = new[] { "execution:exception", "error-type:" + ex.GetType().Name, "idempotency:completed-after-failure" };
            var frameId = await RecordAsync(command, "execution-result", "failed", "allow", evidence, "command.failure.review-before-retry", CancellationToken.None);
            return new("1.0", "msg-" + Guid.NewGuid().ToString("N"), command.CommandId, command.IntentId, command.RunId,
                command.CorrelationId, command.CommandId, "failed", "HANDLER_FAILED", "Command handler failed; internal exception details are not exposed. A retry with the same key is blocked to prevent duplicate side effects.",
                DateTimeOffset.UtcNow, default, evidence, frameId, false);
        }
    }

    private async Task<PmcroMessage<TResult>> RespondAsync(PmcroCommand<TPayload> command, string status, string code, string summary,
        TResult? payload, IReadOnlyList<string> evidence, bool requiresHuman, string? nextSeed, string authorizationDecision, CancellationToken ct)
    {
        string? frameId = null;
        if (!string.IsNullOrWhiteSpace(command.RunId) && !string.IsNullOrWhiteSpace(command.IntentId) &&
            !string.IsNullOrWhiteSpace(command.CorrelationId) && !string.IsNullOrWhiteSpace(command.CommandType))
            frameId = await RecordAsync(command, "command-response", status, authorizationDecision, evidence, nextSeed, ct);
        return new("1.0", "msg-" + Guid.NewGuid().ToString("N"), command.CommandId ?? "invalid", command.IntentId ?? "unknown",
            command.RunId ?? "unknown", command.CorrelationId ?? "unknown", command.CausationId, status, code, summary,
            DateTimeOffset.UtcNow, payload, evidence, frameId, requiresHuman);
    }

    private async Task<string> RecordAsync(PmcroCommand<TPayload> command, string phase, string status, string authorizationDecision,
        IReadOnlyList<string> evidence, string? nextSeed, CancellationToken ct)
    {
        var frameId = "frame-" + Guid.NewGuid().ToString("N");
        var frame = new EnterpriseTrailFrame("1.0", _context.OrganizationId, _context.ProjectId, command.IntentId,
            command.RunId, _context.AgentId, command.CorrelationId, frameId, 1, null, [], DateTimeOffset.UtcNow,
            command.Context.TryGetValue("original_seed", out var original) ? original : command.CommandType,
            command.Context.TryGetValue("refined_objective", out var refined) ? refined : command.CommandType,
            [], nextSeed, phase, command.CommandType, command.CommandType, status, evidence, _context.PolicyId,
            authorizationDecision, authorizationDecision == "require_approval" ? "pending:" + command.CommandId : null,
            status is "completed" or "succeeded" or "started" ? "PASS" : "NOT_PASS", status, [], false, null, "observation", null,
            "application-command-router", false, null);
        await _trail.AppendAsync(frame, ct);
        return frameId;
    }
}
