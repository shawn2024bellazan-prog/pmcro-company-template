using System.Text.Json;

namespace ProjectName.Contracts;

/// <summary>
/// Non-authoritative, candidate-only comparison of the two executive catalogs.
/// An office alias is an owner decision, never an inferred mapping.
/// </summary>
public sealed record CandidateExecutiveCatalogDifference(
    IReadOnlyList<string> OrganizationOnly,
    IReadOnlyList<string> MarketplaceOnly)
{
    public bool ExactMatch => OrganizationOnly.Count == 0 && MarketplaceOnly.Count == 0;
}

public static class CandidateExecutiveCatalogValidator
{
    public static CandidateExecutiveCatalogDifference Compare(
        ExecutiveOrganizationManifest organization,
        IReadOnlyList<string> marketplaceExecutiveIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(marketplaceExecutiveIdentifiers);
        if (organization.Offices.Count == 0 || marketplaceExecutiveIdentifiers.Count == 0)
            throw new InvalidDataException("candidate-executive-catalog-empty");
        var officeIds = organization.Offices.Select(o =>
            string.IsNullOrWhiteSpace(o.OfficeId)
                ? throw new InvalidDataException("candidate-office-id-required")
                : o.OfficeId.ToUpperInvariant()).ToArray();
        var marketIds = marketplaceExecutiveIdentifiers.Select(x =>
            string.IsNullOrWhiteSpace(x)
                ? throw new InvalidDataException("candidate-marketplace-id-required")
                : x.ToUpperInvariant()).ToArray();
        if (officeIds.Distinct(StringComparer.Ordinal).Count() != officeIds.Length ||
            marketIds.Distinct(StringComparer.Ordinal).Count() != marketIds.Length)
            throw new InvalidDataException("candidate-executive-catalog-duplicate");
        return new(
            officeIds.Except(marketIds, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            marketIds.Except(officeIds, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}

/// <summary>
/// Candidate-only UMP-to-wire adapter. Caller-supplied identity and authority fields
/// are *declarations*, never a verified grant or authenticated agent identity.
/// No phase transition, dispatch, registration, grant or activation occurs.
/// </summary>
public static class CandidateUmpMessageAdapter
{
    private static readonly string[] RequiredFields =
    [
        "identity", "authority", "objective", "evidence", "causal-chain",
        "constraints", "risk", "next-action"
    ];

    public static PmcroMessageEnvelope ToProposal(
        JsonElement umpPayload, string messageId, string trailId,
        string sourceAgent, string targetAgent, string cycleRef,
        string causationId, string correlationId, DateTimeOffset createdAt)
    {
        if (umpPayload.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("ump-object-required");
        foreach (var field in RequiredFields)
        {
            if (!umpPayload.TryGetProperty(field, out var value) ||
                value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()) ||
                value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0)
                throw new InvalidDataException("ump-field-required:" + field);
        }

        if (string.IsNullOrWhiteSpace(cycleRef) ||
            string.IsNullOrWhiteSpace(causationId) ||
            string.IsNullOrWhiteSpace(correlationId))
            throw new InvalidDataException("ump-lineage-required");

        // Never forward a payload that pretends to carry an authenticated grant.
        foreach (var prohibited in new[] { "grantsAuthority", "effectiveGrant", "issuerProof" })
            if (umpPayload.TryGetProperty(prohibited, out _))
                throw new InvalidDataException("ump-authority-field-prohibited:" + prohibited);

        var message = new PmcroMessageEnvelope(
            MessageId: messageId,
            Kind: PmcroMessageKinds.ExecutivePosition,
            TrailId: trailId,
            SourceAgent: sourceAgent,
            TargetAgent: targetAgent,
            Payload: umpPayload.Clone(),
            CreatedAt: createdAt,
            CycleRef: cycleRef,
            FrameRef: null,
            CausationId: causationId,
            CorrelationId: correlationId);
        message.EnsureValidForPersistence();
        return message;
    }
}