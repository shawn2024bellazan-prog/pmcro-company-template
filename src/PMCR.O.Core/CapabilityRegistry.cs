using System.Text.Json;

namespace PMCR.O.Core;

/// <summary>Lifecycle labels are evidence states, not permission grants.</summary>
public enum CapabilityStatus { Discovered, Available, Validated, Approved, Executable, Disabled }
public enum CapabilitySourceKind { GitRepository, GitHubMarketplace, VsCodeMarketplace, McpRegistry, PrivateRegistry, LocalCatalog, HttpCatalog, Unknown }
public enum CapabilityInterfaceKind { Skill, McpTool, McpResource, A2AAgent, OpenApiService, HttpService, Plugin, Script, Library, Workflow, Unknown }

public sealed record CapabilitySource(
    string SourceId,
    CapabilitySourceKind Kind,
    string DisplayName,
    string? BaseUri,
    bool RequiresAuthentication,
    string DiscoveryMode,
    string TrustLevel,
    string? Notes = null);

public sealed record CapabilityRegistration(
    string CapabilityId,
    string Name,
    string Version,
    CapabilityInterfaceKind InterfaceKind,
    string SourceId,
    CapabilityStatus Status,
    string? EntryPoint,
    string License,
    string Provenance,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> RequiredPermissions,
    string PolicyRef,
    string? ContentDigest = null,
    string? ValidatedBy = null,
    DateTimeOffset? ValidatedAt = null,
    string? Notes = null);

public sealed record CapabilityValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static CapabilityValidationResult Success() => new(true, []);
}

/// <summary>
/// Dependency-free validation for local capability inventory records. It deliberately does not
/// fetch, install, invoke, or approve remote code. Those operations require governed adapters.
/// </summary>
public static class CapabilityRegistryValidator
{
    private static readonly HashSet<string> AllowedLicenses = new(StringComparer.OrdinalIgnoreCase)
    { "MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC", "MPL-2.0", "GPL-2.0-only", "GPL-3.0-only", "LGPL-2.1-only", "LGPL-3.0-only", "Proprietary", "NOASSERTION", "Unknown" };

    public static CapabilityValidationResult ValidateSource(CapabilitySource source)
    {
        var errors = new List<string>();
        Required(source.SourceId, "sourceId", errors);
        Required(source.DisplayName, "displayName", errors);
        Required(source.DiscoveryMode, "discoveryMode", errors);
        Required(source.TrustLevel, "trustLevel", errors);
        if (source.BaseUri is not null && (!Uri.TryCreate(source.BaseUri, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http" or "file")))
            errors.Add("baseUri must be an absolute http, https, or file URI when supplied.");
        if (source.TrustLevel is not ("untrusted" or "review-required" or "trusted"))
            errors.Add("trustLevel must be untrusted, review-required, or trusted.");
        if (source.Kind == CapabilitySourceKind.Unknown && source.TrustLevel == "trusted")
            errors.Add("Unknown source kinds cannot be marked trusted.");
        return new(errors.Count == 0, errors);
    }

    public static CapabilityValidationResult ValidateCapability(CapabilityRegistration item, IReadOnlySet<string> knownSourceIds)
    {
        var errors = new List<string>();
        Required(item.CapabilityId, "capabilityId", errors);
        Required(item.Name, "name", errors);
        Required(item.Version, "version", errors);
        Required(item.SourceId, "sourceId", errors);
        Required(item.Provenance, "provenance", errors);
        Required(item.PolicyRef, "policyRef", errors);
        if (!knownSourceIds.Contains(item.SourceId)) errors.Add($"Unknown sourceId '{item.SourceId}'.");
        if (!AllowedLicenses.Contains(item.License)) errors.Add($"License '{item.License}' is not in the recognized license set; use Unknown or NOASSERTION when unresolved.");
        if (item.Dependencies.Any(string.IsNullOrWhiteSpace)) errors.Add("Dependencies must not contain blank entries.");
        if (item.RequiredPermissions.Any(string.IsNullOrWhiteSpace)) errors.Add("Required permissions must not contain blank entries.");
        if (item.Status is CapabilityStatus.Validated or CapabilityStatus.Approved or CapabilityStatus.Executable)
        {
            if (string.IsNullOrWhiteSpace(item.ContentDigest)) errors.Add("Validated or later capabilities require a contentDigest.");
            if (string.IsNullOrWhiteSpace(item.ValidatedBy) || item.ValidatedAt is null) errors.Add("Validated or later capabilities require validatedBy and validatedAt evidence.");
        }
        if (item.Status == CapabilityStatus.Executable && item.RequiredPermissions.Count == 0 && (item.InterfaceKind is CapabilityInterfaceKind.Script or CapabilityInterfaceKind.Plugin or CapabilityInterfaceKind.McpTool or CapabilityInterfaceKind.A2AAgent))
            errors.Add("Executable active capabilities must explicitly declare requiredPermissions; use an explicit least-privilege value when no extra permission is needed.");
        if (item.Status == CapabilityStatus.Disabled && string.IsNullOrWhiteSpace(item.Notes)) errors.Add("Disabled capabilities require a reason in notes.");
        return new(errors.Count == 0, errors);
    }

    public static IReadOnlyList<T> ParseJsonArray<T>(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Registry document must be a JSON array.");
        return JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }) ?? [];
    }

    private static void Required(string? value, string field, List<string> errors)
    { if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} is required."); }
}
