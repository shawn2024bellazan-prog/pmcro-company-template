using System.Text.Json;

namespace PMCR.O.Continuation.Plugin;

// The public manifest is a discovery description, not a grant of execution authority.
public sealed class PluginManifest
{
    public string Schema { get; init; } = "";
    public string ArtifactId { get; init; } = "";
    public string ArtifactType { get; init; } = "";
    public string Version { get; init; } = "";
    public PluginParent Parent { get; init; } = new();
    public PluginGovernance Governance { get; init; } = new();
    public PluginExecution Execution { get; init; } = new();
    public List<PluginRoute> Routes { get; init; } = new();
    public List<string> Contracts { get; init; } = new();
    public string Promotion { get; init; } = "";

    public static PluginManifest Parse(string json) =>
        JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new JsonException("Empty plugin manifest");

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Schema != "pmcro.plugin.v1" || ArtifactId != "pmcro.continuation" || ArtifactType != "plugin")
            errors.Add("invalid_plugin_identity");
        if (!System.Version.TryParse(Version, out _) || Version.Count(c => c == '.') != 2)
            errors.Add("invalid_plugin_version");
        if (Parent is null || Parent.MaxDepth <= 0 || Parent.Depth < 0 ||
            Parent.Depth > Parent.MaxDepth || string.IsNullOrWhiteSpace(Parent.RunId))
            errors.Add("invalid_parent_bounds");
        if (Governance is null || Governance.EnabledByDefault || !Governance.ApprovalRequired ||
            !Governance.TrailRequired || string.IsNullOrWhiteSpace(Governance.PolicyRef))
            errors.Add("unsafe_plugin_governance");
        if (Execution is null || Execution.MaxDepth <= 0 || Execution.RetryLimit < 0 ||
            !Execution.IdempotencyRequired)
            errors.Add("invalid_plugin_execution");
        if (Contracts is null || !Contracts.Contains(PmcrSchemas.NextSeed, StringComparer.Ordinal) ||
            !Contracts.Contains(PmcrSchemas.CycleResult, StringComparer.Ordinal))
            errors.Add("missing_contract_versions");
        if (Routes is null || Routes.Count == 0)
        {
            errors.Add("missing_plugin_routes");
        }
        else
        {
            if (Routes.Any(route => route is null))
                errors.Add("invalid_plugin_route");
            else
            {
                if (Routes.GroupBy(route => route.Path, StringComparer.Ordinal).Any(group => group.Count() > 1))
                    errors.Add("ambiguous_plugin_routes");
                if (Routes.Any(route =>
                    string.IsNullOrWhiteSpace(route.Path) || !route.Path.StartsWith('/') ||
                    string.IsNullOrWhiteSpace(route.Operation) ||
                    string.IsNullOrWhiteSpace(route.Target) || !route.Target.StartsWith('@') ||
                    !string.Equals(route.PolicyRef, Governance?.PolicyRef, StringComparison.Ordinal) ||
                    route.Permissions is null || route.Permissions.Count == 0 ||
                    route.Permissions.Any(string.IsNullOrWhiteSpace)))
                    errors.Add("invalid_plugin_route");
            }
        }
        if (Promotion is not ("described" or "composed" or "generated" or "validated" or "checked" or "staged" or "promoted"))
            errors.Add("invalid_plugin_lifecycle");
        return errors;
    }
}

public sealed class PluginParent
{
    public string ArtifactId { get; init; } = "";
    public string RunId { get; init; } = "";
    public int Depth { get; init; }
    public int MaxDepth { get; init; }
}

public sealed class PluginGovernance
{
    public bool EnabledByDefault { get; init; }
    public bool ApprovalRequired { get; init; }
    public bool TrailRequired { get; init; }
    public string PolicyRef { get; init; } = "";
}

public sealed class PluginExecution
{
    public int MaxDepth { get; init; }
    public int RetryLimit { get; init; }
    public bool IdempotencyRequired { get; init; }
}

public sealed class PluginRoute
{
    public string Path { get; init; } = "";
    public string Operation { get; init; } = "";
    public string Target { get; init; } = "";
    public string PolicyRef { get; init; } = "";
    public List<string> Permissions { get; init; } = new();
}
