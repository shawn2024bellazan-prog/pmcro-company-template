using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var path = Environment.GetEnvironmentVariable("PMCRO_MAF_AGENT_DEFINITION")
    ?? Path.GetFullPath("proof/maf/agents/pmcro-native-echo.definition.json");
var definition = AgentDefinition.Load(path);
AIAgent agent = new PmcroNativeEchoAgent(definition);

const string input = "seed-maf-proof-001";
const string streamingInput = "seed-maf-stream-002";
var session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync(input, session);
string output = response.Messages.FirstOrDefault()?.Text ?? "";

List<string> chunks = [];
await foreach (AgentResponseUpdate item in agent.RunStreamingAsync(streamingInput, session))
    chunks.Add(string.Concat(item.Contents.OfType<TextContent>().Select(c => c.Text)));
var streamText = string.Concat(chunks);

var expected = $"{definition.Prefix}|{definition.AgentId}|{input}";
var streamExpected = $"{definition.Prefix}|{definition.AgentId}|{streamingInput}";
bool verified = output == expected && streamText == streamExpected && chunks.Count > 0
    && response.AgentId == agent.Id && agent.Id == definition.AgentId;
Console.WriteLine(JsonSerializer.Serialize(new {
    framework = "Microsoft.Agents.AI.Abstractions",
    agent_type = agent.GetType().FullName,
    loaded_agent_id = agent.Id,
    loaded_agent_name = agent.Name,
    definition_sha256 = definition.DefinitionSha256,
    source_skill_sha256 = definition.SourceSkillSha256,
    run_input = input,
    run_response = output,
    run_response_agent_id = response.AgentId,
    streaming_input = streamingInput,
    streaming_response = streamText,
    streaming_chunks = chunks.Count,
    verified
}, new JsonSerializerOptions { WriteIndented = true }));
return verified ? 0 : 3;

public sealed record AgentDefinition(string AgentId, string Name, string Prefix, string SourceSkill,
    string DefinitionSha256, string SourceSkillSha256)
{
    public static AgentDefinition Load(string file)
    {
        var bytes = File.ReadAllBytes(file);
        using var j = JsonDocument.Parse(bytes);
        var obj = j.RootElement;
        static string Field(JsonElement obj, string key)
            => obj.GetProperty(key).GetString() ?? throw new InvalidDataException(key);
        if (Field(obj, "handler_class") != nameof(PmcroNativeEchoAgent))
            throw new InvalidDataException("Wrong handler_class in MAF definition");
        var source = Field(obj, "source_skill");
        if (Path.IsPathRooted(source) || source.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe source skill");
        var skillBytes = File.ReadAllBytes(Path.GetFullPath(source));
        static string Hash(byte[] input) => Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        return new AgentDefinition(Field(obj, "agent_id"), Field(obj, "name"),
            Field(obj, "response_prefix"), source, Hash(bytes), Hash(skillBytes));
    }
}

public sealed class PmcroNativeEchoAgent : AIAgent
{
    private readonly AgentDefinition _definition;
    public PmcroNativeEchoAgent(AgentDefinition definition) => _definition = definition;
    protected override string? IdCore => _definition.AgentId;
    public override string? Name => _definition.Name;
    public override string? Description => "Deterministic native MAF AIAgent proof";

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        => new(new LocalPmcroSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { session_kind = "local-proof-only" }));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new LocalPmcroSession());

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var input = messages.LastOrDefault()?.Text ?? "";
        var text = Format(input);
        Audit("RunCoreAsync", input, text);
        return Task.FromResult(new AgentResponse
        {
            AgentId = this.Id,
            ResponseId = Guid.NewGuid().ToString("N"),
            Messages = [new ChatMessage(ChatRole.Assistant, text)]
        });
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = messages.LastOrDefault()?.Text ?? "";
        var text = Format(input);
        Audit("RunCoreStreamingAsync", input, text);
        yield return new AgentResponseUpdate
        {
            AgentId = this.Id,
            AuthorName = this.Name,
            Role = ChatRole.Assistant,
            Contents = [new TextContent(text)],
            MessageId = Guid.NewGuid().ToString("N"),
            ResponseId = Guid.NewGuid().ToString("N")
        };
        await Task.CompletedTask;
    }

    private string Format(string input) => $"{_definition.Prefix}|{_definition.AgentId}|{input}";

    private void Audit(string operation, string input, string output)
    {
        var path = Environment.GetEnvironmentVariable("PMCRO_MAF_INVOCATIONS")
            ?? throw new InvalidOperationException("Missing PMCRO_MAF_INVOCATIONS");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.AppendAllText(path, JsonSerializer.Serialize(new
        {
            operation,
            agent_id = this.Id,
            agent_type = this.GetType().Name,
            definition_sha256 = _definition.DefinitionSha256,
            source_skill_sha256 = _definition.SourceSkillSha256,
            source_skill = _definition.SourceSkill,
            input,
            output
        }) + "\n");
    }

    private sealed class LocalPmcroSession : AgentSession { }
}
