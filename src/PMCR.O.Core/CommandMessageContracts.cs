namespace PMCR.O.Core;

/// <summary>Protocol-neutral command envelope. Transport adapters map this contract to A2A, MCP, HTTP/OpenAPI, or internal workflows.</summary>
public sealed record PmcroCommand<TPayload>(
    string SchemaVersion,
    string CommandId,
    string IntentId,
    string RunId,
    string CorrelationId,
    string? CausationId,
    string ActorId,
    string CommandType,
    DateTimeOffset IssuedAt,
    string IdempotencyKey,
    TPayload Payload,
    IReadOnlyDictionary<string, string> Context);

/// <summary>Protocol-neutral command response; rejected and failed commands use the same correlated envelope.</summary>
public sealed record PmcroMessage<TPayload>(
    string SchemaVersion,
    string MessageId,
    string CommandId,
    string IntentId,
    string RunId,
    string CorrelationId,
    string? CausationId,
    string Status,
    string Code,
    string Summary,
    DateTimeOffset Timestamp,
    TPayload? Payload,
    IReadOnlyList<string> EvidenceRefs,
    string? TrailFrameId,
    bool RequiresHumanAction);

public static class PmcroCommandValidator
{
    public static IReadOnlyList<string> Validate<T>(PmcroCommand<T> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new List<string>();
        Required(command.SchemaVersion, nameof(command.SchemaVersion), errors);
        Required(command.CommandId, nameof(command.CommandId), errors);
        Required(command.IntentId, nameof(command.IntentId), errors);
        Required(command.RunId, nameof(command.RunId), errors);
        Required(command.CorrelationId, nameof(command.CorrelationId), errors);
        Required(command.ActorId, nameof(command.ActorId), errors);
        Required(command.CommandType, nameof(command.CommandType), errors);
        Required(command.IdempotencyKey, nameof(command.IdempotencyKey), errors);
        if (command.IssuedAt == default) errors.Add("IssuedAt is required.");
        if (command.CausationId == command.CommandId && command.CausationId is not null) errors.Add("A command cannot directly cause itself.");
        if (command.Context.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value is null)) errors.Add("Context keys and values must be non-null and keys nonblank.");
        return errors;
    }

    private static void Required(string? value, string field, List<string> errors)
    { if (string.IsNullOrWhiteSpace(value)) errors.Add($"{field} is required."); }
}
