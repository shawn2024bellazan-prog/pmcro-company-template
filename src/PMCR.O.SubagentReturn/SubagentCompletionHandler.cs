using PMCR.O.Continuation;
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
public sealed record CheckerAttestation(bool Verified, string CheckerAgentId);

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
    private readonly ISubagentCheckerProof _checker;
    private readonly CallerContinuationHandler _continuation;
    private readonly TrailFrameIntegration _trail;
    private readonly CallerFailureFrameFactory _frames;

    public SubagentCompletionHandler(
        PluginManifest manifest,
        ISeedClaimStore claims,
        ISubagentCheckerProof checker,
        CallerContinuationHandler continuation,
        TrailFrameIntegration trail,
        CallerFailureFrameFactory frames)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _claims = claims ?? throw new ArgumentNullException(nameof(claims));
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
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
        if (invocation.HostObservedState is not null)
        {
            var hostStatus = FromLifecycle(invocation.HostObservedState);
            if (hostStatus is null || hostStatus == SubagentReturnStatus.Completed)
                return await ConcludeAsync(invocation, SubagentReturnStatus.Rejected, "invalid_host_observation",
                    SubagentFailureAttribution.Host);

            var claimedByHost = await ClaimOrConcludeAsync(invocation, cancellationToken);
            if (claimedByHost is not null)
                return claimedByHost;
            return await ConcludeAsync(invocation, hostStatus.Value, "host_observed_" + invocation.HostObservedState,
                SubagentFailureAttribution.Host);
        }

        var validation = SubagentReturnValidator.Validate(returned, invocation);
        if (returned is null || !validation.IdentityVerified)
            // An unauthenticated return never consumes the claim, so it cannot block the genuine child.
            return await ConcludeAsync(invocation, SubagentReturnStatus.Rejected, validation.ReasonCode,
                validation.Attribution);

        var duplicate = await ClaimOrConcludeAsync(invocation, cancellationToken);
        if (duplicate is not null)
            return duplicate;

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
