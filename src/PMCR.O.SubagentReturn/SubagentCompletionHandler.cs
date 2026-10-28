using PMCR.O.Continuation;
using PMCR.O.Core;
using System.Security.Cryptography;
using System.Text.Json;
using PMCR.O.Continuation.Plugin;

namespace PMCR.O.SubagentReturn;

public enum SubagentReturnStatus
{
    Completed,
    Failed,
    Interrupted,
    TimedOut,
    Cancelled,
    Deferred,
    Rejected,
    Duplicate,
    ExecutionUncertain
}

public sealed record SubagentReturnReceipt(
    SubagentReturnStatus Status,
    string ReasonCode,
    string CorrelationId,
    string SubagentRunId,
    SubagentFailureAttribution Attribution,
    string? TrailFrameId,
    ContinuationReceipt? Continuation);

/// <summary>Result of an independent checker proof established from governed evidence, never from the child's message.</summary>
public sealed record CheckerAttestation(
    bool Verified, string CheckerAgentId, string? EvidenceFrameId = null,
    string? EvidenceDigest = null, string? ResultDigest = null, string? Signature = null);

/// <summary>
/// Host-owned verifier. A Trail frame match alone cannot authenticate a checker or its verdict.
/// Production implementations must validate immutable provenance and signed result-bound evidence.
/// The unconfigured default denies all proofs.
/// </summary>
public interface IAuthenticatedCheckerEvidenceVerifier
{
    Task<bool> VerifyAsync(
        CheckerAttestation attestation, SubagentReturnEnvelope returned,
        TrustedSubagentInvocation invocation, EnterpriseTrailFrame frame,
        CancellationToken cancellationToken);
}

public sealed class UnconfiguredCheckerEvidenceVerifier : IAuthenticatedCheckerEvidenceVerifier
{
    public Task<bool> VerifyAsync(CheckerAttestation attestation, SubagentReturnEnvelope returned,
        TrustedSubagentInvocation invocation, EnterpriseTrailFrame frame,
        CancellationToken cancellationToken) => Task.FromResult(false);
}

public static class SubagentResultDigest
{
    public static string Compute(CycleResultEnvelope result) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(result))).ToLowerInvariant();
}

/// <summary>
/// Implementations must verify a checker identity independent of the maker (the subagent)
/// from authoritative evidence such as Trail Frames and the host's agent registry.
/// </summary>
public interface ISubagentCheckerProof
{
    Task<CheckerAttestation?> VerifyAsync(
        SubagentReturnEnvelope returned,
        TrustedSubagentInvocation invocation,
        CancellationToken cancellationToken);
}

/// <summary>Default: no checker proof is configured, so no completed return is ever accepted.</summary>
public sealed class UnconfiguredSubagentCheckerProof : ISubagentCheckerProof
{
    public Task<CheckerAttestation?> VerifyAsync(
        SubagentReturnEnvelope returned,
        TrustedSubagentInvocation invocation,
        CancellationToken cancellationToken) => Task.FromResult<CheckerAttestation?>(null);
}

/// <summary>
/// Caller-side completion of a delegated subagent run. Requires an authenticated
/// I AM context and either a correlated terminal result or a caller-owned failure frame,
/// and routes any proposed handoff (next seed) through <see cref="CallerContinuationHandler"/>.
/// Every branch fails closed: an unconfigured, disabled, unverified, duplicate or
/// unavailable dependency never results in acceptance or dispatch.
/// </summary>
public sealed class SubagentCompletionHandler
{
    private readonly PluginManifest _manifest;
    private readonly IReadOnlyList<string> _manifestErrors;
    private readonly ISeedClaimStore _claims;
    private readonly SqliteSubagentReturnStateStore? _durableStates;
    private readonly DurableContinuationCoordinator? _durableCoordinator;
    private readonly TrustedTrailHeadVerifier? _trustedTrailHead;
    private readonly ISubagentCheckerProof _checker;
    private readonly IAuthenticatedCheckerEvidenceVerifier _proofVerifier;
    private readonly CallerContinuationHandler _continuation;
    private readonly TrailFrameIntegration _trail;
    private readonly CallerFailureFrameFactory _frames;

    public SubagentCompletionHandler(
        PluginManifest manifest,
        ISeedClaimStore claims,
        ISubagentCheckerProof checker,
        CallerContinuationHandler continuation,
        TrailFrameIntegration trail,
        CallerFailureFrameFactory frames,
        IAuthenticatedCheckerEvidenceVerifier? proofVerifier = null,
        SqliteSubagentReturnStateStore? durableStates = null,
        DurableContinuationCoordinator? durableCoordinator = null,
        TrustedTrailHeadVerifier? trustedTrailHead = null)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _claims = claims ?? throw new ArgumentNullException(nameof(claims));
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
        _proofVerifier = proofVerifier ?? new UnconfiguredCheckerEvidenceVerifier();
        _durableStates = durableStates;
        _durableCoordinator = durableCoordinator;
        _trustedTrailHead = trustedTrailHead;
        _continuation = continuation ?? throw new ArgumentNullException(nameof(continuation));
        _trail = trail ?? throw new ArgumentNullException(nameof(trail));
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));
        _manifestErrors = SubagentReturnManifestValidator.Validate(manifest);
    }

    public IReadOnlyList<PluginRoute> DeclaredRoutes => _manifest.Routes;

    public async Task<SubagentReturnReceipt> HandleAsync(
        SubagentReturnEnvelope? returned,
        TrustedSubagentInvocation invocation,
        PluginActivationGrant grant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(invocation.Caller);
        ArgumentNullException.ThrowIfNull(grant);

        // Without a complete trusted binding there is no run to attribute a Trail Frame to.
        if (!SubagentReturnValidator.HasCompleteTrustedContext(invocation))
            return new SubagentReturnReceipt(
                SubagentReturnStatus.Rejected, "trusted_context_invalid",
                invocation.Caller.CorrelationId ?? "", invocation.SubagentRunId ?? "",
                SubagentFailureAttribution.Host, null, null);

        var gate = EvaluateGate(invocation.Route, grant);
        if (gate is not null)
            return await ConcludeAsync(invocation, gate.Value.Status, gate.Value.Reason, SubagentFailureAttribution.Host);

        // The caller's own parent frame must really exist in its Trail; otherwise there is no evidence to return into.
        if (!await _trail.ParentFrameExistsAsync(invocation.Caller, cancellationToken))
            return await ConcludeAsync(invocation, SubagentReturnStatus.Rejected, "parent_frame_missing",
                SubagentFailureAttribution.Caller);

        // The host's own lifecycle observation outranks anything the child claims, including a late success.
        if (invocation.HostObservedState == "pending")
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "subagent_pending", SubagentFailureAttribution.Host);
        if (invocation.HostObservedState is not null)
        {
            var hostStatus = invocation.HostObservedState == "terminal_missing"
                ? SubagentReturnStatus.Failed : FromLifecycle(invocation.HostObservedState);
            if (hostStatus is null || hostStatus == SubagentReturnStatus.Completed)
                return await ConcludeAsync(invocation, SubagentReturnStatus.Rejected, "invalid_host_observation",
                    SubagentFailureAttribution.Host);

            var claimedByHost = await ClaimOrConcludeAsync(invocation, cancellationToken);
            if (claimedByHost is not null)
                return claimedByHost;
            return await ConcludeAsync(invocation, hostStatus.Value, "host_observed_" + invocation.HostObservedState,
                SubagentFailureAttribution.Host);
        }

        if (returned is null)
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "no_authenticated_terminal_observation", SubagentFailureAttribution.Host);

        var validation = SubagentReturnValidator.Validate(returned, invocation);
        if (returned is null || !validation.IdentityVerified)
            // An unauthenticated return never consumes the claim, so it cannot block the genuine child.
            return await ConcludeAsync(invocation, SubagentReturnStatus.Rejected, validation.ReasonCode,
                validation.Attribution);

        // Non-completed or malformed terminal outcomes close their own run immediately.
        // Completed returns are claimed only after independent proof has been verified.
        if (!validation.IsValid || returned.LifecycleState != SubagentLifecycleStates.Completed)
        {
            var terminalClaim = await ClaimOrConcludeAsync(invocation, cancellationToken);
            if (terminalClaim is not null) return terminalClaim;
        }

        if (!validation.IsValid)
            return await ConcludeAsync(
                invocation,
                validation.ReasonCode == "missing_final_result" ? SubagentReturnStatus.Failed : SubagentReturnStatus.Rejected,
                validation.ReasonCode, validation.Attribution);

        var lifecycleStatus = FromLifecycle(returned.LifecycleState);
        if (lifecycleStatus is null)
            return await ConcludeAsync(invocation, SubagentReturnStatus.Rejected, "invalid_lifecycle_state",
                SubagentFailureAttribution.Child);
        if (lifecycleStatus != SubagentReturnStatus.Completed)
            return await ConcludeAsync(invocation, lifecycleStatus.Value, "subagent_" + returned.LifecycleState,
                SubagentFailureAttribution.Child);

        return await CompleteAsync(returned, invocation, cancellationToken);
    }

    private async Task<SubagentReturnReceipt> CompleteAsync(
        SubagentReturnEnvelope returned,
        TrustedSubagentInvocation invocation,
        CancellationToken cancellationToken)
    {
        var final = returned.FinalResult;
        if (final is null)
            return await ConcludeAsync(invocation, SubagentReturnStatus.Failed, "missing_final_result",
                SubagentFailureAttribution.Child);

        if (!string.Equals(final.Result?.Evidence?.CheckerVerdict, "passed", StringComparison.Ordinal))
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "checker_not_passed",
                SubagentFailureAttribution.Child);

        CheckerAttestation? attestation;
        try
        {
            attestation = await _checker.VerifyAsync(returned, invocation, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "checker_unavailable",
                SubagentFailureAttribution.Host);
        }

        if (attestation is null || !attestation.Verified)
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "checker_unverified",
                SubagentFailureAttribution.Host);

        var maker = invocation.Caller.ExpectedChildAgent;
        if (string.IsNullOrWhiteSpace(attestation.CheckerAgentId) ||
            string.Equals(attestation.CheckerAgentId, maker, StringComparison.Ordinal) ||
            string.Equals(attestation.CheckerAgentId, returned.IContext?.AgentId, StringComparison.Ordinal))
            return await ConcludeAsync(invocation, SubagentReturnStatus.Rejected, "checker_not_independent",
                SubagentFailureAttribution.Host);

        if (string.IsNullOrWhiteSpace(attestation.EvidenceFrameId) ||
            string.IsNullOrWhiteSpace(attestation.EvidenceDigest))
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "checker_evidence_missing", SubagentFailureAttribution.Host);
        var checkerFrames = await _trail.ReadCorrelatedAsync(invocation.Caller, cancellationToken);
        var proofFrame = checkerFrames.SingleOrDefault(frame =>
            string.Equals(frame.FrameId, attestation.EvidenceFrameId, StringComparison.Ordinal) &&
            string.Equals(frame.AgentId, attestation.CheckerAgentId, StringComparison.Ordinal) &&
            string.Equals(frame.PayloadDigest, attestation.EvidenceDigest, StringComparison.Ordinal) &&
            string.Equals(frame.CheckerVerdict, "PASS", StringComparison.Ordinal));
        if (proofFrame is null ||
            !string.Equals(attestation.ResultDigest, SubagentResultDigest.Compute(final), StringComparison.Ordinal))
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "checker_evidence_unverified", SubagentFailureAttribution.Host);

        bool authenticated;
        try
        {
            authenticated = await _proofVerifier.VerifyAsync(attestation, returned, invocation, proofFrame, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "checker_proof_unavailable", SubagentFailureAttribution.Host);
        }
        if (!authenticated)
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "checker_proof_unverified", SubagentFailureAttribution.Host);

        // Durable mode is a strict, opt-in gate: external continuation dispatch must be
        // staged by a separate transactional outbox adapter. Calling the legacy handler
        // here would permit side effects without recording an atomic dispatch intent.
        if (_durableStates is not null)
        {
            var digest = SubagentResultDigest.Compute(final);
            var key = ClaimKey(invocation);
            var recorded = await _durableStates.ReadAsync(key, cancellationToken);
            if (recorded is null)
            {
                recorded = await _durableStates.TryTransitionAsync(key, null,
                    DurableReturnState.Received, digest, cancellationToken)
                    ?? await _durableStates.ReadAsync(key, cancellationToken);
            }
            if (recorded is null || recorded.ResultDigest != digest)
                return await ConcludeAsync(invocation, SubagentReturnStatus.Rejected,
                    "durable_result_digest_conflict", SubagentFailureAttribution.Host);
            if (recorded.State is DurableReturnState.Closed or DurableReturnState.Dispatched)
                return await ConcludeAsync(invocation, SubagentReturnStatus.Duplicate,
                    "durable_return_already_closed", SubagentFailureAttribution.None);
            if (recorded.State is DurableReturnState.Dispatching or DurableReturnState.ExecutionUncertain)
                return await ConcludeAsync(invocation, SubagentReturnStatus.ExecutionUncertain,
                    "external_dispatch_reconciliation_required", SubagentFailureAttribution.Host);
            if (recorded.State is DurableReturnState.Received or DurableReturnState.Deferred)
            {
                var moved = await _durableStates.TryTransitionAsync(key, recorded.Version,
                    DurableReturnState.Verified, digest, cancellationToken);
                if (moved is null)
                    return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred,
                        "durable_admission_concurrent", SubagentFailureAttribution.Host);
                recorded = moved;
            }
            if (final.NextSeed is not null)
            {
                // A caller-owned coordinator may atomically persist a verified, policy-
                // bound intent. This handler NEVER dispatches a seed in durable mode.
                // Reservation of an external operation requires a separate host action.
                var reason = "durable_dispatch_adapter_unconfigured";
                if (_durableCoordinator is not null)
                {
                    if (_trustedTrailHead is null)
                        reason = "trusted_trail_head_unconfigured";
                    else
                    {
                        bool integrity;
                        try
                        {
                            integrity = await _trustedTrailHead.VerifyEvidenceAsync(proofFrame, cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                        catch (Exception) { integrity = false; }
                        if (!integrity)
                            reason = "trusted_trail_head_unverified";
                        else
                        {
                            var decision = await _durableCoordinator.PrepareVerifiedAsync(
                                final, invocation.Caller, attestation, cancellationToken);
                            reason = decision switch
                            {
                                DurableContinuationDecision.AuthorizedIntentRecorded =>
                                    "durable_authorized_intent_recorded",
                                DurableContinuationDecision.AuthorizationUnavailable =>
                                    "durable_policy_or_authority_unavailable",
                                DurableContinuationDecision.PolicyVersionChanged =>
                                    "durable_policy_version_changed",
                                DurableContinuationDecision.ResultConflict =>
                                    "durable_authorization_result_conflict",
                                _ => "durable_authorization_rejected"
                            };
                        }
                    }
                }
                var deferred = await _durableStates.TryTransitionAsync(key, recorded.Version,
                    DurableReturnState.Deferred, digest, cancellationToken);
                if (deferred is null)
                    return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred,
                        "durable_admission_concurrent", SubagentFailureAttribution.Host);
                return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred,
                    reason, SubagentFailureAttribution.Host);
            }
            var closed = await _durableStates.TryTransitionAsync(key, recorded.Version,
                DurableReturnState.Closed, digest, cancellationToken);
            if (closed is null)
                return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred,
                    "durable_admission_concurrent", SubagentFailureAttribution.Host);
            return await ConcludeAsync(invocation, SubagentReturnStatus.Completed,
                "subagent_completed_no_continuation", SubagentFailureAttribution.None);
        }

        var executionClaim = await ClaimOrConcludeAsync(invocation, cancellationToken);
        if (executionClaim is not null) return executionClaim;

        // The handoff proposal (next seed) is evaluated by the caller's existing continuation
        // handler: lineage, evidence, current policy, budgets and atomic idempotency all apply.
        var continuation = await _continuation.HandleAsync(final, invocation.Caller, cancellationToken);
        IReadOnlyList<string> evidence =
        [
            "checker-agent:" + attestation.CheckerAgentId,
            "continuation-status:" + continuation.Status,
            "continuation-reason:" + continuation.ReasonCode
        ];

        switch (continuation.Status)
        {
            case ContinuationStatus.NoContinuation:
                return await ConcludeAsync(invocation, SubagentReturnStatus.Completed,
                    "subagent_completed_no_continuation", SubagentFailureAttribution.None, continuation, evidence);
            case ContinuationStatus.Dispatched:
                return await ConcludeAsync(invocation, SubagentReturnStatus.Completed,
                    "continuation_dispatched", SubagentFailureAttribution.None, continuation, evidence);
            case ContinuationStatus.Deferred:
                return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred,
                    continuation.ReasonCode, SubagentFailureAttribution.None, continuation, evidence);
            case ContinuationStatus.Duplicate:
                return await ConcludeAsync(invocation, SubagentReturnStatus.Duplicate,
                    continuation.ReasonCode, SubagentFailureAttribution.None, continuation, evidence);
            case ContinuationStatus.ExecutionUncertain:
                return await ConcludeAsync(invocation, SubagentReturnStatus.ExecutionUncertain,
                    continuation.ReasonCode, SubagentFailureAttribution.Host, continuation, evidence);
            default:
                // A denied policy is a caller decision, not a child failure; other rejections are child-proposal defects.
                var attribution = string.Equals(continuation.ReasonCode, "policy_denied", StringComparison.Ordinal)
                    ? SubagentFailureAttribution.None
                    : SubagentFailureAttribution.Child;
                return await ConcludeAsync(invocation, SubagentReturnStatus.Rejected,
                    continuation.ReasonCode, attribution, continuation, evidence);
        }
    }

    private (SubagentReturnStatus Status, string Reason)? EvaluateGate(string route, PluginActivationGrant grant)
    {
        if (_manifestErrors.Count > 0)
            return (SubagentReturnStatus.Rejected, "invalid_plugin_manifest");
        if (!_manifest.Routes.Any(candidate => string.Equals(candidate.Path, route, StringComparison.Ordinal)))
            return (SubagentReturnStatus.Rejected, "unregistered_plugin_route");
        if (!grant.Registered)
            return (SubagentReturnStatus.Rejected, "plugin_not_registered");
        if (!grant.Enabled)
            return (SubagentReturnStatus.Deferred, "plugin_disabled");
        if (!string.Equals(grant.ArtifactId, _manifest.ArtifactId, StringComparison.Ordinal) ||
            !string.Equals(grant.Version, _manifest.Version, StringComparison.Ordinal) ||
            !string.Equals(grant.PolicyReference, _manifest.Governance.PolicyRef, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(grant.GrantedBy))
            return (SubagentReturnStatus.Rejected, "invalid_plugin_grant");
        return null;
    }

    private async Task<SubagentReturnReceipt?> ClaimOrConcludeAsync(
        TrustedSubagentInvocation invocation,
        CancellationToken cancellationToken)
    {
        bool claimed;
        try
        {
            claimed = await _claims.TryClaimAsync(ClaimKey(invocation), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return await ConcludeAsync(invocation, SubagentReturnStatus.Deferred, "claim_store_unavailable",
                SubagentFailureAttribution.Host);
        }

        return claimed
            ? null
            : await ConcludeAsync(invocation, SubagentReturnStatus.Duplicate, "subagent_return_already_consumed",
                SubagentFailureAttribution.None);
    }

    private async Task<SubagentReturnReceipt> ConcludeAsync(
        TrustedSubagentInvocation invocation,
        SubagentReturnStatus status,
        string reasonCode,
        SubagentFailureAttribution attribution,
        ContinuationReceipt? continuation = null,
        IReadOnlyList<string>? additionalEvidence = null)
    {
        var evidence = new List<string>(additionalEvidence ?? []);
        var frame = IsFailure(status)
            ? _frames.CreateFailureFrame(invocation, StatusText(status), reasonCode, attribution, evidence)
            : _frames.CreateDecisionFrame(invocation, StatusText(status), reasonCode,
                attribution == SubagentFailureAttribution.None
                    ? evidence
                    : [.. evidence, "attribution:" + attribution.ToString().ToLowerInvariant()]);

        // A durable Trail write is required before any receipt is returned; a failed write propagates.
        var frameId = await _trail.AppendAsync(frame);
        return new SubagentReturnReceipt(
            status, reasonCode, invocation.Caller.CorrelationId, invocation.SubagentRunId,
            attribution, frameId, continuation);
    }

    private static bool IsFailure(SubagentReturnStatus status) => status is
        SubagentReturnStatus.Failed or SubagentReturnStatus.Interrupted or SubagentReturnStatus.TimedOut or
        SubagentReturnStatus.Cancelled or SubagentReturnStatus.Rejected or SubagentReturnStatus.ExecutionUncertain;

    private static string StatusText(SubagentReturnStatus status) => status switch
    {
        SubagentReturnStatus.Completed => "completed",
        SubagentReturnStatus.Failed => "failed",
        SubagentReturnStatus.Interrupted => "interrupted",
        SubagentReturnStatus.TimedOut => "timed_out",
        SubagentReturnStatus.Cancelled => "cancelled",
        SubagentReturnStatus.Deferred => "deferred",
        SubagentReturnStatus.Rejected => "rejected",
        SubagentReturnStatus.Duplicate => "duplicate",
        _ => "execution_uncertain"
    };

    private static SubagentReturnStatus? FromLifecycle(string state) => state switch
    {
        SubagentLifecycleStates.Completed => SubagentReturnStatus.Completed,
        SubagentLifecycleStates.Interrupted => SubagentReturnStatus.Interrupted,
        SubagentLifecycleStates.TimedOut => SubagentReturnStatus.TimedOut,
        SubagentLifecycleStates.Failed => SubagentReturnStatus.Failed,
        SubagentLifecycleStates.Cancelled => SubagentReturnStatus.Cancelled,
        _ => null
    };

    private static string ClaimKey(TrustedSubagentInvocation invocation)
    {
        // Length-prefix each part so separators cannot collide; the schema prefix namespaces these
        // claims away from seed claims even if a host shares one claim store.
        var caller = invocation.Caller;
        var values = new[]
        {
            SubagentReturnSchemas.SubagentReturn, caller.AgentId, caller.IntentId, caller.RunId, invocation.SubagentRunId
        };
        return string.Concat(values.Select(value => $"{value.Length}:{value}"));
    }
}
