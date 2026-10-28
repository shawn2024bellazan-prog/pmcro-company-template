namespace PMCR.O.SubagentReturn;

public enum DurableContinuationDecision
{
    PolicyDeferred, ReadyForReservation, ResultConflict, ExternalIdRequired,
    ReservedRequiresReconciliation, AlreadyReserved, AdmissionConflict
}

/// <summary>
/// A caller-owned, fail-closed durable continuation coordinator. No external
/// dispatch is performed here. ReserveAsync records an intent and requires
/// external reconciliation before any replay. Production integration MUST
/// atomically persist policy evidence and reservation on an authoritative
/// store before a side-effecting dispatcher is installed.
/// </summary>
public sealed class DurableContinuationCoordinator
{
    private readonly SqliteContinuationOutbox _outbox;
    private readonly DurableCallerPolicyPreflight _policy;
    public DurableContinuationCoordinator(SqliteContinuationOutbox outbox)
    {
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _policy = new DurableCallerPolicyPreflight(outbox);
    }

    public async Task<DurableContinuationDecision> PrepareAsync(
        string key, string digest, string seedId, Func<Task<bool>> trustedCallerPolicy,
        CancellationToken ct = default)
    {
        var record = await _policy.EvaluateAndAdmitAsync(key, digest, seedId, trustedCallerPolicy, ct);
        if (record is not null) return DurableContinuationDecision.ReadyForReservation;
        var existing = await _outbox.ReadAsync(key, ct);
        return existing is not null &&
            (existing.ResultDigest != digest || existing.SeedId != seedId)
            ? DurableContinuationDecision.ResultConflict : DurableContinuationDecision.PolicyDeferred;
    }

    public async Task<DurableContinuationDecision> ReserveAsync(
        string key, string externalOperationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalOperationId))
            return DurableContinuationDecision.ExternalIdRequired;
        var entry = await _outbox.ReadAsync(key, ct);
        if (entry is null) return DurableContinuationDecision.AdmissionConflict;
        if (entry.Status != DurableOutboxStatus.Deferred)
            return DurableContinuationDecision.AlreadyReserved;
        var reserved = await _outbox.ReserveAsync(key, entry.Version, ct);
        return reserved is null ? DurableContinuationDecision.AlreadyReserved
            : DurableContinuationDecision.ReservedRequiresReconciliation;
    }
}
