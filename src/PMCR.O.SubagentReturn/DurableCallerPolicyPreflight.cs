namespace PMCR.O.SubagentReturn;

/// <summary>
/// Caller-owned preflight for the persistent outbox. This component does not
/// execute dispatch; production must supply a policy and evidence evaluation
/// from a trusted caller, and must revalidate it at reservation time.
/// Never treat the child's checker or approval claims as authorization.
/// </summary>
public sealed class DurableCallerPolicyPreflight
{
    private readonly SqliteContinuationOutbox _outbox;
    public DurableCallerPolicyPreflight(SqliteContinuationOutbox outbox) =>
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));

    public async Task<DurableOutboxRecord?> EvaluateAndAdmitAsync(
        string idempotencyKey, string resultDigest, string seedId,
        Func<Task<bool>> trustedCallerApproved, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trustedCallerApproved);
        bool approved;
        try { approved = await trustedCallerApproved(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
        if (!approved) return null;
        var existing = await _outbox.ReadAsync(idempotencyKey, ct);
        if (existing is not null)
            return existing.ResultDigest == resultDigest && existing.SeedId == seedId &&
                existing.Status == DurableOutboxStatus.Deferred ? existing : null;
        var admitted = await _outbox.AdmitAsync(idempotencyKey, resultDigest, seedId, ct);
        if (admitted is not null) return admitted;
        existing = await _outbox.ReadAsync(idempotencyKey, ct);
        return existing is not null && existing.ResultDigest == resultDigest &&
            existing.SeedId == seedId && existing.Status == DurableOutboxStatus.Deferred ? existing : null;
    }
}
