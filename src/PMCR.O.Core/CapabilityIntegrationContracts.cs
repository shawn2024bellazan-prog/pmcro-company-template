namespace PMCR.O.Core;

/// <summary>Normalized discovery result. Source-specific metadata stays in provenance.</summary>
public sealed record CapabilityDiscoveryQuery(string? Text, IReadOnlyList<CapabilityInterfaceKind> Kinds, int Limit = 50);
public sealed record CapabilityDiscoveryResult(string SourceId, IReadOnlyList<CapabilityRegistration> Items, string? ContinuationToken, IReadOnlyList<string> Warnings);
public sealed record CapabilityInvocationRequest(string CapabilityId, string ObjectiveId, string Actor, IReadOnlyDictionary<string,string> Inputs, ActionRisk Risk, string? RunId = null, string? IntentId = null, string? OriginalSeed = null, string? RefinedObjective = null);
public sealed record CapabilityInvocationResult(bool Succeeded, string Summary, IReadOnlyList<string> Evidence, string? ExternalOperationId = null);

/// <summary>Adapters must never install or invoke a capability without runtime policy evaluation.</summary>
public interface ICapabilitySourceAdapter
{
    string SourceId { get; }
    Task<CapabilityDiscoveryResult> DiscoverAsync(CapabilityDiscoveryQuery query, CancellationToken cancellationToken);
}

public interface ICapabilityPolicyGate
{
    Task<ApprovalDecision> EvaluateAsync(CapabilityRegistration capability, CapabilityInvocationRequest request, CancellationToken cancellationToken);
}

public interface ICapabilityInvoker
{
    CapabilityInterfaceKind InterfaceKind { get; }
    Task<CapabilityInvocationResult> InvokeAsync(CapabilityRegistration capability, CapabilityInvocationRequest request, CancellationToken cancellationToken);
}

/// <summary>Governed invocation wrapper: status, kind, policy and caller permission gate precede adapter execution.</summary>
public sealed class GovernedCapabilityExecutor
{
    private readonly ICapabilityPolicyGate _policyGate;
    private readonly IReadOnlyDictionary<CapabilityInterfaceKind, ICapabilityInvoker> _invokers;
    private readonly ICapabilityTrailRecorder? _trail;

    public GovernedCapabilityExecutor(ICapabilityPolicyGate policyGate, IEnumerable<ICapabilityInvoker> invokers, ICapabilityTrailRecorder? trail = null)
    {
        _policyGate = policyGate;
        _invokers = invokers.ToDictionary(x => x.InterfaceKind);
        _trail = trail;
    }

    public async Task<CapabilityInvocationResult> InvokeAsync(CapabilityRegistration capability, CapabilityInvocationRequest request, CancellationToken cancellationToken = default)
    {
        request = request with { RunId = string.IsNullOrWhiteSpace(request.RunId) ? "caprun-" + Guid.NewGuid().ToString("N") : request.RunId, IntentId = string.IsNullOrWhiteSpace(request.IntentId) ? request.ObjectiveId : request.IntentId };
        async Task<CapabilityInvocationResult> Finish(CapabilityInvocationResult result, string phase, string decision)
        {
            if (_trail is not null)
                await _trail.RecordAsync(capability, request, phase, result.Succeeded ? "succeeded" : "blocked-or-failed", decision, result.Evidence, decision == "require_approval" ? "pending:" + request.ObjectiveId : null, cancellationToken);
            return result;
        }

        if (capability.Status != CapabilityStatus.Executable)
            return await Finish(new(false, $"Capability status '{capability.Status}' is not executable.", ["governance:status-denied"]), "authorization", "deny");
        var validation = CapabilityRegistryValidator.ValidateCapability(capability, new HashSet<string>(StringComparer.Ordinal) { capability.SourceId });
        if (!validation.IsValid)
            return await Finish(new(false, "Capability registration failed validation.", validation.Errors.Select(x => "governance:invalid-registration:" + x).ToArray()), "validation", "deny");
        if (!string.Equals(capability.CapabilityId, request.CapabilityId, StringComparison.Ordinal))
            return await Finish(new(false, "Request capability ID does not match registration.", ["governance:identity-mismatch"]), "authorization", "deny");
        if (!_invokers.TryGetValue(capability.InterfaceKind, out var invoker))
            return await Finish(new(false, $"No invoker registered for interface '{capability.InterfaceKind}'.", ["governance:adapter-missing"]), "adapter-selection", "deny");

        var decision = await _policyGate.EvaluateAsync(capability, request, cancellationToken);
        if (decision.Disposition != ApprovalDisposition.Allow)
            return await Finish(new(false, $"Invocation blocked: {decision.Disposition}. {decision.Reason}", [$"governance:{decision.Disposition}", decision.Policy]), "policy", decision.Disposition == ApprovalDisposition.RequireHumanApproval ? "require_approval" : "deny");

        await RecordStartAsync();
        CapabilityInvocationResult result;
        try { result = await invoker.InvokeAsync(capability, request, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = new(false, "Capability invocation failed; exception details are intentionally not copied to the trail.", ["execution:exception", "error-type:" + ex.GetType().Name]);
        }
        return await Finish(result, "execution-result", "allow");

        async Task RecordStartAsync()
        {
            if (_trail is not null)
                await _trail.RecordAsync(capability, request, "execution-start", "started", "allow", ["policy:allow"], cancellationToken: cancellationToken);
        }
    }
}
