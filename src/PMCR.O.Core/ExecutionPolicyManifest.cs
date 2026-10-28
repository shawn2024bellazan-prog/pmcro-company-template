namespace PMCR.O.Core;

/// <summary>Declarative per-capability execution limits. Runtime adapters must enforce these values; manifest presence alone is not enforcement.</summary>
public sealed record ExecutionPolicyManifest(
    string PolicyId,
    string CapabilityId,
    int TimeoutSeconds,
    int MaxAttempts,
    int InitialBackoffMilliseconds,
    int MaxBackoffSeconds,
    int MaxEvidenceLength,
    bool RequireReconciliationForUnknownNonIdempotentOutcome,
    bool AllowAutomaticRetryForIdempotentOperations,
    IReadOnlyList<string> RedactFieldNames);

public static class ExecutionPolicyManifestValidator
{
    public static IReadOnlyList<string> Validate(ExecutionPolicyManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(manifest.PolicyId)) errors.Add("PolicyId is required.");
        if (string.IsNullOrWhiteSpace(manifest.CapabilityId)) errors.Add("CapabilityId is required.");
        if (manifest.TimeoutSeconds is < 1 or > 86400) errors.Add("TimeoutSeconds must be between 1 and 86400.");
        if (manifest.MaxAttempts is < 1 or > 20) errors.Add("MaxAttempts must be between 1 and 20.");
        if (manifest.InitialBackoffMilliseconds is < 0 or > 60000) errors.Add("InitialBackoffMilliseconds must be between 0 and 60000.");
        if (manifest.MaxBackoffSeconds is < 1 or > 3600) errors.Add("MaxBackoffSeconds must be between 1 and 3600.");
        if (manifest.MaxEvidenceLength is < 32 or > 16384) errors.Add("MaxEvidenceLength must be between 32 and 16384.");
        if (manifest.InitialBackoffMilliseconds > manifest.MaxBackoffSeconds * 1000L) errors.Add("Initial backoff cannot exceed maximum backoff.");
        if (!manifest.RequireReconciliationForUnknownNonIdempotentOutcome) errors.Add("Unknown non-idempotent outcomes must require reconciliation before retry.");
        if (manifest.RedactFieldNames is null || manifest.RedactFieldNames.Count == 0) errors.Add("At least one redacted field name is required.");
        else if (manifest.RedactFieldNames.Any(string.IsNullOrWhiteSpace)) errors.Add("Redacted field names cannot be blank.");
        return errors;
    }
}
