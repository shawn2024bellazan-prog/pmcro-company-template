using A2A;
using A2A.AspNetCore;
using System.Security.Cryptography;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var definitionPath = Environment.GetEnvironmentVariable("PMCRO_A2A_AGENT_DEFINITION")
    ?? Path.GetFullPath("proof/a2a/agents/pmcro-echo.definition.json");
var agent = AgentDefinition.Load(definitionPath);
builder.Services.AddSingleton(agent);
var url = Environment.GetEnvironmentVariable("PMCRO_A2A_URL")
    ?? "http://127.0.0.1:5077";
builder.Services.AddA2AAgent<PmcroEchoWorker>(PmcroEchoWorker.GetAgentCard(agent, url + "/a2a/pmcro-echo"));

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new
{
    status = "ready",
    definition_id = agent.AgentId,
    definition_sha256 = agent.DefinitionSha256,
    source_skill_sha256 = agent.SourceSkillSha256
}));
app.MapA2A("/a2a/pmcro-echo");
app.MapWellKnownAgentCard(app.Services.GetRequiredService<AgentCard>());
app.Run();

public sealed record AgentDefinition(
    string AgentId, string Name, string SkillId, string ResponsePrefix,
    string SourceSkill, string DefinitionSha256, string SourceSkillSha256)
{
    public static AgentDefinition Load(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        static string Field(JsonElement obj, string field) =>
            obj.GetProperty(field).GetString() ?? throw new InvalidDataException("Null field: " + field);
        var sourceSkill = Field(root, "source_skill");
        if (Path.IsPathRooted(sourceSkill) || sourceSkill.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Expected a repo-relative source skill");
        var skillPath = Path.GetFullPath(sourceSkill);
        var skillBytes = File.ReadAllBytes(skillPath);
        var handlerClass = Field(root, "handler_class");
        if (handlerClass != nameof(PmcroEchoWorker))
            throw new InvalidDataException("Agent definition does not bind the expected worker handler");
        return new AgentDefinition(
            Field(root, "agent_id"), Field(root, "name"), Field(root, "skill_id"),
            Field(root, "response_prefix"), sourceSkill,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(skillBytes)).ToLowerInvariant());
    }
}

public sealed class PmcroEchoWorker : IAgentHandler
{
    private readonly AgentDefinition _definition;
    public PmcroEchoWorker(AgentDefinition definition) => _definition = definition;

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var updater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
        await updater.SubmitAsync(cancellationToken: cancellationToken);
        await updater.StartWorkAsync(cancellationToken: cancellationToken);
        var input = context.UserText;
        var output = $"{_definition.ResponsePrefix}|{_definition.AgentId}|{input}";
        await updater.AddArtifactAsync([Part.FromText(output)],
            name: "pmcro-echo-artifact", cancellationToken: cancellationToken);
        await updater.CompleteAsync(cancellationToken: cancellationToken);

        var auditPath = Environment.GetEnvironmentVariable("PMCRO_A2A_INVOCATIONS");
        if (!string.IsNullOrWhiteSpace(auditPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(auditPath))!);
            var row = JsonSerializer.Serialize(new
            {
                event_type = "handler-executed",
                handler = nameof(PmcroEchoWorker),
                agent_id = _definition.AgentId,
                definition_sha256 = _definition.DefinitionSha256,
                source_skill = _definition.SourceSkill,
                source_skill_sha256 = _definition.SourceSkillSha256,
                task_id = context.TaskId,
                context_id = context.ContextId,
                input,
                output,
                result = "completed"
            });
            await File.AppendAllTextAsync(auditPath, row + "\n", cancellationToken);
        }
    }

    public static AgentCard GetAgentCard(AgentDefinition def, string endpoint) => new()
    {
        Name = def.Name,
        Description = "Deterministic PMCR-O worker loaded from authored definition: " + def.AgentId,
        Version = "1.0.0",
        SupportedInterfaces = [new AgentInterface { Url = endpoint, ProtocolBinding = "JSONRPC", ProtocolVersion = "1.0" }],
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain"],
        Capabilities = new AgentCapabilities { Streaming = true, PushNotifications = false },
        Skills = [new AgentSkill {
            Id = def.SkillId, Name = def.Name,
            Description = "Return deterministic task artifact and record actual handler call.",
            Tags = ["pmcro", "worker", "deterministic-proof"]
        }]
    };
}
