namespace PMCR.O.Continuation.Plugin;

// The host must construct this grant from its authenticated registry/policy state.
// Never deserialize it directly from a child agent's message or a marketplace listing.
public sealed record PluginActivationGrant(
    string ArtifactId,
    string Version,
    string PolicyReference,
    string GrantedBy,
    bool Registered,
    bool Enabled);

/// <summary>
/// Declarative plugin boundary for caller-returned continuation commands.
/// It is deliberately not a live marketplace connector or a privileged plugin loader.
/// </summary>
public sealed class ContinuationPlugin
{
    private readonly PluginManifest _manifest;
    private readonly CallerContinuationHandler _handler;
    private readonly IContinuationTrailWriter _trail;

    public ContinuationPlugin(
        PluginManifest manifest,
        CallerContinuationHandler handler,
        IContinuationTrailWriter trail)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _trail = trail ?? throw new ArgumentNullException(nameof(trail));
    }

    public IReadOnlyList<PluginRoute> DeclaredRoutes => _manifest.Routes;

    public async Task<ContinuationReceipt> InvokeAsync(
        string route,
        CycleResultEnvelope message,
        CallerContext caller,
        PluginActivationGrant grant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(grant);

        if (_manifest.Validate().Count > 0)
            return await DenyAsync(message, caller, ContinuationStatus.Rejected, "invalid_plugin_manifest");
        if (string.IsNullOrWhiteSpace(route) ||
            !_manifest.Routes.Any(candidate => string.Equals(candidate.Path, route, StringComparison.Ordinal)))
            return await DenyAsync(message, caller, ContinuationStatus.Rejected, "unregistered_plugin_route");
        if (!grant.Registered)
            return await DenyAsync(message, caller, ContinuationStatus.Rejected, "plugin_not_registered");
        if (!grant.Enabled)
            return await DenyAsync(message, caller, ContinuationStatus.Deferred, "plugin_disabled");
        if (!string.Equals(grant.ArtifactId, _manifest.ArtifactId, StringComparison.Ordinal) ||
            !string.Equals(grant.Version, _manifest.Version, StringComparison.Ordinal) ||
            !string.Equals(grant.PolicyReference, _manifest.Governance.PolicyRef, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(grant.GrantedBy))
            return await DenyAsync(message, caller, ContinuationStatus.Rejected, "invalid_plugin_grant");

        // Host activation is not execution permission: the caller handler still
        // requires independent evidence and per-command policy evaluation.
        return await _handler.HandleAsync(message, caller, cancellationToken);
    }

    private async Task<ContinuationReceipt> DenyAsync(
        CycleResultEnvelope message,
        CallerContext caller,
        ContinuationStatus status,
        string reasonCode)
    {
        await _trail.WriteAsync(new ContinuationTrailRecord(
            DateTimeOffset.UtcNow,
            caller.AgentId,
            caller.IntentId,
            caller.RunId,
            caller.CorrelationId,
            caller.CommandId,
            message.NextSeed?.SeedId,
            status,
            reasonCode), CancellationToken.None);
        return new ContinuationReceipt(status, reasonCode, caller.CorrelationId, message.NextSeed?.SeedId);
    }
}
