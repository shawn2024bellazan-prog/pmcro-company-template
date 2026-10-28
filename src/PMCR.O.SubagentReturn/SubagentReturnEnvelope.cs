using System.Text.Json;
using System.Text.Json.Serialization;
using PMCR.O.Continuation;

namespace PMCR.O.SubagentReturn;

public static class SubagentReturnSchemas
{
    public const string SubagentReturn = "pmcro.subagent-return.v1";
    public const string MessageType = "subagent.return";
    public const string ReturnCommandType = "pmcro.subagent.return";
    public const string ArtifactId = "pmcro.subagent-return";
}

/// <summary>
/// Terminal lifecycle states of a delegated subagent run. The vocabulary is
/// informed by VS Code's SubagentStart/SubagentStop lifecycle events, but a
/// PMCR-O state is only a claim until the caller validates it. It never grants authority.
/// </summary>
public static class SubagentLifecycleStates
{
    public const string Completed = "completed";
    public const string Interrupted = "interrupted";
    public const string TimedOut = "timed_out";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> Terminal =
        [Completed, Interrupted, TimedOut, Failed, Cancelled];

    public static bool IsTerminal(string? state) =>
        state is not null && Terminal.Contains(state, StringComparer.Ordinal);
}

/// <summary>
/// The subagent's self-declared "I AM" identity and lineage. It is an untrusted
/// assertion: the caller compares every field with its own trusted invocation
/// record and never reconstructs the invocation from this value.
/// </summary>
public sealed record IAmContext(
    [property: JsonPropertyName("agent_id")] string AgentId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("intent_id")] string IntentId,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("correlation_id")] string CorrelationId,
    [property: JsonPropertyName("causation_id")] string CausationId,
    [property: JsonPropertyName("parent_frame_id")] string ParentFrameId,
    [property: JsonPropertyName("subagent_run_id")] string SubagentRunId);

/// <summary>
/// Terminal return of a delegated subagent to its calling agent
/// (pmcro.subagent-return.v1). A completed return wraps the existing
/// <see cref="CycleResultEnvelope"/>; any proposed next seed inside it stays
/// a handoff proposal (never inherited authority) and is routed through
/// <see cref="CallerContinuationHandler"/>.
/// </summary>
public sealed record SubagentReturnEnvelope(
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("message_type")] string MessageType,
    [property: JsonPropertyName("i_context")] IAmContext? IContext,
    [property: JsonPropertyName("lifecycle_state")] string LifecycleState,
    [property: JsonPropertyName("stop_reason")] string? StopReason,
    [property: JsonPropertyName("reply_to")] string ReplyTo,
    [property: JsonPropertyName("return_route")] string ReturnRoute,
    [property: JsonPropertyName("final_result")] CycleResultEnvelope? FinalResult);

public sealed record SubagentReturnParseResult(SubagentReturnEnvelope? Envelope, string? ReasonCode);

/// <summary>Parses untrusted JSON into an envelope, mapping structural problems to stable reason codes.</summary>
public static class SubagentReturnParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static SubagentReturnParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new(null, "malformed_envelope");

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new(null, "malformed_envelope");

            if (!root.TryGetProperty("i_context", out var context) ||
                context.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return new(null, "missing_i_context");
            if (context.ValueKind != JsonValueKind.Object)
                return new(null, "malformed_i_context");

            try
            {
                if (context.Deserialize<IAmContext>(Options) is null)
                    return new(null, "malformed_i_context");
            }
            catch (JsonException)
            {
                return new(null, "malformed_i_context");
            }

            var envelope = root.Deserialize<SubagentReturnEnvelope>(Options);
            return envelope is null ? new(null, "malformed_envelope") : new(envelope, null);
        }
        catch (JsonException)
        {
            return new(null, "malformed_envelope");
        }
    }
}
