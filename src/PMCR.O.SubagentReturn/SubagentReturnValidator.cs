using PMCR.O.Continuation;
using PMCR.O.Continuation.Plugin;

namespace PMCR.O.SubagentReturn;

/// <summary>Who a failure is attributed to. The caller always OWNS the resulting Trail Frame.</summary>
public enum SubagentFailureAttribution
{
    None,
    Child,
    Host,
    Caller
}

/// <summary>
/// Supplied by the authenticated host from its invocation ledger. A child return
/// must never construct or alter this record. <see cref="HostObservedState"/> is
/// the host's own lifecycle observation (for example a deadline it enforced) and
/// always outranks what the child claims.
/// </summary>
public sealed record TrustedSubagentInvocation(
    CallerContext Caller,
    string SubagentRunId,
    string Route,
    string? HostObservedState);

/// <param name="IsValid">The return is structurally valid for its claimed lifecycle state.</param>
/// <param name="IdentityVerified">The envelope's I AM context matched the trusted invocation, so the return may consume the single terminal-return claim.</param>
public sealed record SubagentValidationResult(
    bool IsValid,
    bool IdentityVerified,
    string ReasonCode,
    SubagentFailureAttribution Attribution)
{
    public static SubagentValidationResult Valid { get; } =
        new(true, true, "valid", SubagentFailureAttribution.None);

    internal static SubagentValidationResult Untrusted(string code, SubagentFailureAttribution attribution) =>
        new(false, false, code, attribution);

    internal static SubagentValidationResult Trusted(string code) =>
        new(false, true, code, SubagentFailureAttribution.Child);
}

/// <summary>
/// Fail-closed structural and lineage validation. Every comparison is ordinal and
/// against the caller's trusted record, never against another child-supplied field.
/// </summary>
public static class SubagentReturnValidator
{
    public static SubagentValidationResult Validate(
        SubagentReturnEnvelope? returned,
        TrustedSubagentInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(invocation.Caller);
        var caller = invocation.Caller;

        if (!HasCompleteTrustedContext(invocation))
            return SubagentValidationResult.Untrusted("trusted_context_invalid", SubagentFailureAttribution.Host);

        if (returned is null)
            return SubagentValidationResult.Untrusted("no_return", SubagentFailureAttribution.Child);

        if (!Eq(returned.Schema, SubagentReturnSchemas.SubagentReturn) ||
            !Eq(returned.MessageType, SubagentReturnSchemas.MessageType))
            return SubagentValidationResult.Untrusted("invalid_schema", SubagentFailureAttribution.Child);

        var who = returned.IContext;
        if (who is null)
            return SubagentValidationResult.Untrusted("missing_i_context", SubagentFailureAttribution.Child);
        if (Blank(who.AgentId) || Blank(who.Role) || Blank(who.IntentId) || Blank(who.RunId) ||
            Blank(who.CorrelationId) || Blank(who.CausationId) || Blank(who.ParentFrameId) ||
            Blank(who.SubagentRunId))
            return SubagentValidationResult.Untrusted("malformed_i_context", SubagentFailureAttribution.Child);

        if (!Eq(who.AgentId, caller.ExpectedChildAgent))
            return SubagentValidationResult.Untrusted("i_am_mismatch", SubagentFailureAttribution.Child);

        if (!Eq(who.IntentId, caller.IntentId) || !Eq(who.RunId, caller.RunId) ||
            !Eq(who.CorrelationId, caller.CorrelationId) || !Eq(who.CausationId, caller.CommandId) ||
            !Eq(who.ParentFrameId, caller.ParentFrameId) || !Eq(who.SubagentRunId, invocation.SubagentRunId))
            return SubagentValidationResult.Untrusted("correlation_mismatch", SubagentFailureAttribution.Child);

        if (!Eq(returned.ReplyTo, caller.AgentId) || !Eq(returned.ReturnRoute, caller.ExpectedReturnRoute))
            return SubagentValidationResult.Untrusted("invalid_return_route", SubagentFailureAttribution.Child);

        // From here the sender is authenticated against the trusted record; remaining
        // problems are terminal failures of this run, not unauthenticated noise.
        if (!SubagentLifecycleStates.IsTerminal(returned.LifecycleState))
            return SubagentValidationResult.Trusted("invalid_lifecycle_state");

        if (Eq(returned.LifecycleState, SubagentLifecycleStates.Completed))
        {
            var final = returned.FinalResult;
            if (final is null)
                return SubagentValidationResult.Trusted("missing_final_result");
            var ids = final.Correlation;
            if (ids is null ||
                !Eq(ids.IntentId, caller.IntentId) || !Eq(ids.RunId, caller.RunId) ||
                !Eq(ids.CorrelationId, caller.CorrelationId) || !Eq(ids.CausationId, caller.CommandId) ||
                !Eq(ids.ParentFrameId, caller.ParentFrameId))
                return SubagentValidationResult.Trusted("correlation_mismatch");
        }
        else if (returned.FinalResult is not null)
        {
            return SubagentValidationResult.Trusted("final_result_on_non_completed_return");
        }

        return SubagentValidationResult.Valid;
    }

    /// <summary>Blank trusted values would match blank child values, so an incomplete binding is never usable.</summary>
    public static bool HasCompleteTrustedContext(TrustedSubagentInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var caller = invocation.Caller;
        return caller is not null &&
            !Blank(caller.AgentId) && !Blank(caller.ExpectedChildAgent) && !Blank(caller.IntentId) &&
            !Blank(caller.RunId) && !Blank(caller.CorrelationId) && !Blank(caller.CommandId) &&
            !Blank(caller.ParentFrameId) && !Blank(caller.ExpectedReturnRoute) &&
            !Blank(invocation.SubagentRunId) && !Blank(invocation.Route);
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static bool Eq(string? a, string b) => string.Equals(a, b, StringComparison.Ordinal);
}

/// <summary>
/// Structural governance checks for the <c>pmcro.subagent-return</c> plugin manifest. The existing
/// <see cref="PluginManifest.Validate"/> is pinned to the continuation identity, so this
/// validator applies the same safety invariants to this plugin's identity.
/// </summary>
public static class SubagentReturnManifestValidator
{
    public static IReadOnlyList<string> Validate(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var errors = new List<string>();

        if (manifest.Schema != "pmcro.plugin.v1" ||
            manifest.ArtifactId != SubagentReturnSchemas.ArtifactId ||
            manifest.ArtifactType != "plugin")
            errors.Add("invalid_plugin_identity");
        if (!System.Version.TryParse(manifest.Version, out _) || manifest.Version.Count(c => c == '.') != 2)
            errors.Add("invalid_plugin_version");
        if (manifest.Parent is null || manifest.Parent.MaxDepth <= 0 || manifest.Parent.Depth < 0 ||
            manifest.Parent.Depth > manifest.Parent.MaxDepth || string.IsNullOrWhiteSpace(manifest.Parent.RunId))
            errors.Add("invalid_parent_bounds");
        if (manifest.Governance is null || manifest.Governance.EnabledByDefault ||
            !manifest.Governance.ApprovalRequired || !manifest.Governance.TrailRequired ||
            string.IsNullOrWhiteSpace(manifest.Governance.PolicyRef))
            errors.Add("unsafe_plugin_governance");
        if (manifest.Execution is null || manifest.Execution.MaxDepth <= 0 ||
            manifest.Execution.RetryLimit < 0 || !manifest.Execution.IdempotencyRequired)
            errors.Add("invalid_plugin_execution");
        if (manifest.Contracts is null ||
            !manifest.Contracts.Contains(SubagentReturnSchemas.SubagentReturn, StringComparer.Ordinal) ||
            !manifest.Contracts.Contains(PmcrSchemas.CycleResult, StringComparer.Ordinal) ||
            !manifest.Contracts.Contains(PmcrSchemas.NextSeed, StringComparer.Ordinal))
            errors.Add("missing_contract_versions");

        if (manifest.Routes is null || manifest.Routes.Count == 0)
        {
            errors.Add("missing_plugin_routes");
        }
        else if (manifest.Routes.Any(route => route is null))
        {
            errors.Add("invalid_plugin_route");
        }
        else
        {
            if (manifest.Routes.GroupBy(route => route.Path, StringComparer.Ordinal).Any(group => group.Count() > 1))
                errors.Add("ambiguous_plugin_routes");
            if (manifest.Routes.Any(route =>
                    string.IsNullOrWhiteSpace(route.Path) || !route.Path.StartsWith('/') ||
                    string.IsNullOrWhiteSpace(route.Operation) ||
                    string.IsNullOrWhiteSpace(route.Target) || !route.Target.StartsWith('@') ||
                    !string.Equals(route.PolicyRef, manifest.Governance?.PolicyRef, StringComparison.Ordinal) ||
                    route.Permissions is null || route.Permissions.Count == 0 ||
                    route.Permissions.Any(string.IsNullOrWhiteSpace)))
                errors.Add("invalid_plugin_route");
        }

        if (manifest.Promotion is not ("described" or "composed" or "generated" or "validated" or "checked" or "staged" or "promoted"))
            errors.Add("invalid_plugin_lifecycle");
        return errors;
    }
}
