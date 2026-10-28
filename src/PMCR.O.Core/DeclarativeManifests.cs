using System.Text.Json;
using System.Text.RegularExpressions;

namespace PMCR.O.Core;

public static partial class ManifestKinds
{
    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    { "AgentTemplate", "WorkflowTemplate", "TrailProduct", "EnterpriseProfile", "CapabilityPack" };

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]*$")]
    public static partial Regex NamePattern();
}

/// <summary>Generic envelope shared by declarative PMCR-O resources. Kind-specific spec validation is a second stage.</summary>
public sealed record DeclarativeManifest(
    string ApiVersion,
    string Kind,
    ManifestMetadata Metadata,
    JsonElement Spec,
    string? Extends = null,
    IReadOnlyList<string>? PolicyRefs = null,
    JsonElement? Parameters = null);

public sealed record ManifestMetadata(
    string Name,
    string Version,
    string? Description = null,
    string? Owner = null,
    IReadOnlyDictionary<string, string>? Labels = null);

public sealed record ManifestValidationResult(bool IsValid, IReadOnlyList<string> Errors);

public static class DeclarativeManifestValidator
{
    public static ManifestValidationResult Validate(DeclarativeManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var errors = new List<string>();
        Required(manifest.ApiVersion, "apiVersion", errors);
        if (!ManifestKinds.Supported.Contains(manifest.Kind)) errors.Add($"Unsupported manifest kind '{manifest.Kind}'.");
        Required(manifest.Metadata.Name, "metadata.name", errors);
        if (!ManifestKinds.NamePattern().IsMatch(manifest.Metadata.Name ?? string.Empty))
            errors.Add("metadata.name must use lowercase letters, digits, dots, and hyphens, and start with a letter or digit.");
        Required(manifest.Metadata.Version, "metadata.version", errors);
        if (manifest.Spec.ValueKind != JsonValueKind.Object) errors.Add("spec must be a JSON object.");
        if (manifest.PolicyRefs is not null)
        {
            if (manifest.PolicyRefs.Any(string.IsNullOrWhiteSpace)) errors.Add("policyRefs cannot contain blank references.");
            if (manifest.PolicyRefs.Distinct(StringComparer.Ordinal).Count() != manifest.PolicyRefs.Count) errors.Add("policyRefs must be unique.");
        }
        if (manifest.Extends is not null && string.IsNullOrWhiteSpace(manifest.Extends)) errors.Add("extends must be omitted or a non-empty versioned template reference.");
        return new(errors.Count == 0, errors);
    }

    /// <summary>Parses the common JSON envelope. YAML parsing belongs in a host adapter; no YAML parser is bundled in core.</summary>
    public static (DeclarativeManifest? Manifest, ManifestValidationResult Result) ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (null, new(false, ["Manifest JSON must not be empty."]));
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (null, new(false, ["Manifest root must be a JSON object."]));
            var errors = new List<string>();
            CheckUnknownProperties(root, ["apiVersion", "kind", "metadata", "spec", "extends", "policyRefs", "parameters"], "manifest", errors);
            var apiVersion = ReadString(root, "apiVersion", errors);
            var kind = ReadString(root, "kind", errors);
            if (!root.TryGetProperty("metadata", out var metadataElement) || metadataElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add("metadata must be an object.");
                return (null, new(false, errors));
            }
            CheckUnknownProperties(metadataElement, ["name", "version", "description", "owner", "labels"], "metadata", errors);
            var name = ReadString(metadataElement, "name", errors, "metadata.name");
            var version = ReadString(metadataElement, "version", errors, "metadata.version");
            var description = OptionalString(metadataElement, "description", errors, "metadata.description");
            var owner = OptionalString(metadataElement, "owner", errors, "metadata.owner");
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            if (metadataElement.TryGetProperty("labels", out var labelsElement))
            {
                if (labelsElement.ValueKind != JsonValueKind.Object) errors.Add("metadata.labels must be an object of strings.");
                else foreach (var property in labelsElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.String) errors.Add($"metadata.labels.{property.Name} must be a string.");
                    else labels[property.Name] = property.Value.GetString()!;
                }
            }
            if (!root.TryGetProperty("spec", out _)) errors.Add("spec is required.");
            var specCopy = root.TryGetProperty("spec", out var specElement) ? specElement.Clone() : default;
            var extends = OptionalString(root, "extends", errors, "extends");
            var policies = new List<string>();
            if (root.TryGetProperty("policyRefs", out var policyElement))
            {
                if (policyElement.ValueKind != JsonValueKind.Array) errors.Add("policyRefs must be an array of strings.");
                else foreach (var item in policyElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) errors.Add("policyRefs entries must be strings.");
                    else policies.Add(item.GetString()!);
                }
            }
            JsonElement? parameters = null;
            if (root.TryGetProperty("parameters", out var parametersElement))
            {
                if (parametersElement.ValueKind != JsonValueKind.Object) errors.Add("parameters must be an object.");
                else parameters = parametersElement.Clone();
            }
            if (errors.Count > 0) return (null, new(false, errors));
            var manifest = new DeclarativeManifest(apiVersion!, kind!, new ManifestMetadata(name!, version!, description, owner, labels), specCopy, extends, policies, parameters);
            var validation = Validate(manifest);
            return validation.IsValid ? (manifest, validation) : (null, validation);
        }
        catch (JsonException ex)
        {
            return (null, new(false, [$"Invalid manifest JSON: {ex.Message}"]));
        }
    }

    private static void CheckUnknownProperties(JsonElement parent, IReadOnlyCollection<string> allowed, string scope, List<string> errors)
    {
        foreach (var property in parent.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                errors.Add($"Unknown property '{scope}.{property.Name}'.");
    }

    private static string? ReadString(JsonElement parent, string property, List<string> errors, string? displayName = null)
    {
        displayName ??= property;
        if (!parent.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        { errors.Add($"{displayName} is required and must be a non-empty string."); return null; }
        return element.GetString();
    }

    private static string? OptionalString(JsonElement parent, string property, List<string> errors, string displayName)
    {
        if (!parent.TryGetProperty(property, out var element)) return null;
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        { errors.Add($"{displayName} must be omitted or a non-empty string."); return null; }
        return element.GetString();
    }

    private static void Required(string? value, string name, List<string> errors)
    { if (string.IsNullOrWhiteSpace(value)) errors.Add($"{name} is required."); }
}
