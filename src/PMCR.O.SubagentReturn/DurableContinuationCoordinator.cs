using PMCR.O.Continuation;

namespace PMCR.O.SubagentReturn;

public enum DurableContinuationDecision
{
    PolicyDeferred, ReadyForReservation, ResultConflict, ExternalIdRequired,
    ReservedRequiresReconciliation, AlreadyReserved, AdmissionConflict,
    AuthorizationUnavailable, InvalidAuthorization, AuthorizedIntentRecorded, PolicyVersionChanged
}

/// <summary>
/// A caller-owned, fail-closed durable continuation coordinator. No external
/// dispatch is performed here. ReserveAsync records an intent and requires
/// external reconciliation before any replay. Production integration MUST
/// atomically persist policy evidence and reservation on an authoritative
/// store before a side-effecting dispatcher is installed.
/// </summary>
public interface ITrustedCallerAuthorizationSource
{
    Task<DurableAuthorizationSnapshot?> AuthorizeAsync(
        CycleResultEnvelope result, CallerContext caller, CheckerAttestation checker, CancellationToken ct);
}

public interface IAuthenticatedCallerAuthorizationVerifier
{
    Task<bool> VerifyAsync(
        DurableAuthorizationSnapshot snapshot, CycleResultEnvelope result,
        CallerContext caller, CheckerAttestation checker, CancellationToken ct);
}

public sealed class UnconfiguredCallerAuthorizationSource : ITrustedCallerAuthorizationSource
{
    public Task<DurableAuthorizationSnapshot?> AuthorizeAsync(
        CycleResultEnvelope result, CallerContext caller, CheckerAttestation checker, CancellationToken ct) =>
        Task.FromResult<DurableAuthorizationSnapshot?>(null);
}

public sealed class UnconfiguredCallerAuthorizationVerifier : IAuthenticatedCallerAuthorizationVerifier
{
    public Task<bool> VerifyAsync(DurableAuthorizationSnapshot snapshot, CycleResultEnvelope result,
        CallerContext caller, CheckerAttestation checker, CancellationToken ct) => Task.FromResult(false);
}

public sealed class DurableContinuationCoordinator
{
    private readonly SqliteContinuationOutbox _outbox;
    private readonly DurableCallerPolicyPreflight _policy;
    private readonly ITrustedCallerAuthorizationSource _authority;
    private readonly IAuthenticatedCallerAuthorizationVerifier _verifier;
    public DurableContinuationCoordinator(SqliteContinuationOutbox outbox,
        ITrustedCallerAuthorizationSource? authority = null,
        IAuthenticatedCallerAuthorizationVerifier? verifier = null)
    {
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _policy = new DurableCallerPolicyPreflight(outbox);
        _authority = authority ?? new UnconfiguredCallerAuthorizationSource();
        _verifier = verifier ?? new UnconfiguredCallerAuthorizationVerifier();
    }


    /// <summary>Only independently host-authenticated caller policy may provide
    /// authorization. Child-supplied PASS, approval, or policy values never suffice.</summary>
    public async Task<DurableContinuationDecision> PrepareVerifiedAsync(
        CycleResultEnvelope result, CallerContext caller, CheckerAttestation checker,
        CancellationToken ct = default)
    {
        if (result.NextSeed is null || CallerContinuationHandler.Validate(result,caller) is not null)
            return DurableContinuationDecision.InvalidAuthorization;
        if (!checker.Verified || string.IsNullOrWhiteSpace(checker.EvidenceFrameId) ||
            string.IsNullOrWhiteSpace(checker.EvidenceDigest))
            return DurableContinuationDecision.InvalidAuthorization;
        DurableAuthorizationSnapshot? a;
        try { a = await _authority.AuthorizeAsync(result,caller,checker,ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return DurableContinuationDecision.AuthorizationUnavailable; }
        if (a is null) return DurableContinuationDecision.AuthorizationUnavailable;
        var digest=SubagentResultDigest.Compute(result);
        if (a.ResultDigest!=digest || a.SeedId!=result.NextSeed.SeedId ||
            a.CheckerFrameId!=checker.EvidenceFrameId ||
            a.CheckerEvidenceDigest!=checker.EvidenceDigest ||
            a.CheckerAgentId!=checker.CheckerAgentId ||
            a.CallerAgentId!=caller.AgentId || a.RunId!=caller.RunId ||
            a.CorrelationId!=caller.CorrelationId)
            return DurableContinuationDecision.InvalidAuthorization;
        bool valid;
        try { valid=await _verifier.VerifyAsync(a,result,caller,checker,ct); }
        catch (OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch (Exception) { return DurableContinuationDecision.AuthorizationUnavailable; }
        if (!valid) return DurableContinuationDecision.InvalidAuthorization;
        var admitted=await _outbox.AdmitAuthorizedAsync(a,ct);
        if (admitted is not null) return DurableContinuationDecision.AuthorizedIntentRecorded;
        var existing=await _outbox.ReadAsync(a.IdempotencyKey,ct);
        return existing is not null &&
            (existing.ResultDigest!=digest || existing.SeedId!=a.SeedId)
            ? DurableContinuationDecision.ResultConflict : DurableContinuationDecision.PolicyVersionChanged;
    }

    /// <summary>Reserves a previously host-authorized intent, but never dispatches.
    /// The host must mirror policy epochs transactionally before any reservation.</summary>
    public async Task<DurableContinuationDecision> ReserveVerifiedAsync(
        string key, string expectedPolicyVersion, string checkerFrameId,
        string externalOperationId, CancellationToken ct=default)
    {
        if (string.IsNullOrWhiteSpace(externalOperationId))
            return DurableContinuationDecision.ExternalIdRequired;
        var row=await _outbox.ReadAsync(key,ct);
        if (row is null) return DurableContinuationDecision.AdmissionConflict;
        if (row.Status!=DurableOutboxStatus.Deferred)
            return DurableContinuationDecision.AlreadyReserved;
        var reservation=await _outbox.ReserveAuthorizedAsync(key,row.Version,expectedPolicyVersion,
            checkerFrameId,externalOperationId,ct);
        return reservation is null ? DurableContinuationDecision.PolicyVersionChanged
            : DurableContinuationDecision.ReservedRequiresReconciliation;
    }

    // Legacy diagnostic preflight path is not authorized for external dispatch.
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
