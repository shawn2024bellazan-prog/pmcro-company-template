using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var evidenceDir = Environment.GetEnvironmentVariable("PMCRO_CYCLE_EVIDENCE_DIR")
    ?? Path.GetFullPath("evidence/cycle");
Directory.CreateDirectory(evidenceDir);
var trailPath = Path.Combine(evidenceDir, "cycle.jsonl");
var invocationPath = Path.Combine(evidenceDir, "phase-invocations.jsonl");
if (File.Exists(trailPath) || File.Exists(invocationPath))
    throw new IOException("Refusing to overwrite prior phase evidence");

var phases = new[]
{
    new PhaseBinding("orchestrate", "pmcro-orchestrator", "plugins/pmcro-orchestrator/skills/orchestrate/SKILL.md"),
    new PhaseBinding("plan", "pmcro-planner", "plugins/pmcro/skills/plan/SKILL.md"),
    new PhaseBinding("make", "pmcro-maker", "plugins/pmcro/skills/make/SKILL.md"),
    new PhaseBinding("check", "pmcro-checker", "plugins/pmcro/skills/check/SKILL.md"),
    new PhaseBinding("reflect", "pmcro-reflector", "plugins/pmcro/skills/reflect/SKILL.md")
};
var input = "seed-native-five-phase-001";
var previous = "GENESIS";
int sequence = 0;
foreach (var binding in phases)
{
    sequence++;
    AIAgent role = new PmcroPhaseAgent(binding, invocationPath);
    AgentResponse response = await role.RunAsync(input);
    var output = response.Messages.Single().Text ?? "";
    if (response.AgentId != binding.AgentId)
        throw new InvalidDataException("MAF response agent id mismatch for " + binding.Phase);
    var sourceHash = Sha(File.ReadAllBytes(binding.SourceSkill));
    var inputHash = Sha(Encoding.UTF8.GetBytes(input));
    var outputHash = Sha(Encoding.UTF8.GetBytes(output));
    var signed = string.Join("|", sequence.ToString(), binding.Phase, binding.AgentId,
        sourceHash, inputHash, outputHash, previous);
    var frameHash = Sha(Encoding.UTF8.GetBytes(signed));
    var frame = new
    {
        sequence, phase = binding.Phase, agent_id = role.Id, agent_type = role.GetType().Name,
        invocation = "AIAgent.RunAsync", source_skill = binding.SourceSkill,
        source_skill_sha256 = sourceHash, input, input_sha256 = inputHash,
        output, output_sha256 = outputHash, previous_frame_sha256 = previous,
        frame_sha256 = frameHash
    };
    File.AppendAllText(trailPath, JsonSerializer.Serialize(frame) + "\n");
    previous = frameHash;
    input = output;
}
using var finalDocument = JsonDocument.Parse(input);
var final = finalDocument.RootElement;
var passed = final.GetProperty("phase").GetString() == "reflect"
    && final.GetProperty("disposition").GetString() == "PASS";
Console.WriteLine(JsonSerializer.Serialize(new
{
    status = passed ? "PASS" : "FAIL",
    executed_phases = phases.Select(p => p.Phase).ToArray(),
    native_agent_invocations = sequence,
    trail_head_sha256 = previous,
    final_disposition = final.GetProperty("disposition").GetString(),
    llm_inference = "NOT_INVOKED",
    role_implementation = "deterministic AIAgent subclass for each phase"
}, new JsonSerializerOptions { WriteIndented = true }));
return passed ? 0 : 3;

static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

public sealed record PhaseBinding(string Phase, string AgentId, string SourceSkill);

public sealed class PmcroPhaseAgent : AIAgent
{
    private readonly PhaseBinding _phase;
    private readonly string _invocationPath;
    public PmcroPhaseAgent(PhaseBinding phase, string invocationPath)
    {
        _phase = phase;
        _invocationPath = invocationPath;
    }
    protected override string? IdCore => _phase.AgentId;
    public override string? Name => _phase.AgentId;
    public override string? Description => "Deterministic native PMCR-O phase processor";

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        => new(new LocalCycleSession());
    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { kind = "bounded-native-phase" }));
    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new LocalCycleSession());

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null,
        AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = messages.LastOrDefault()?.Text ?? "";
        var output = ExecutePhase(input);
        var sourceHash = Sha(File.ReadAllBytes(_phase.SourceSkill));
        File.AppendAllText(_invocationPath, JsonSerializer.Serialize(new
        {
            method = "RunCoreAsync", phase = _phase.Phase, agent_id = Id,
            agent_type = GetType().Name, source_skill = _phase.SourceSkill,
            source_skill_sha256 = sourceHash, input, output
        }) + "\n");
        return Task.FromResult(new AgentResponse
        {
            AgentId = Id, ResponseId = Guid.NewGuid().ToString("N"),
            Messages = [new ChatMessage(ChatRole.Assistant, output)]
        });
    }

    private string ExecutePhase(string input)
    {
        if (_phase.Phase == "orchestrate")
        {
            if (!input.StartsWith("seed-", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid input seed");
            return JsonSerializer.Serialize(new { phase = "orchestrate", seed = input,
                admitted = true, next_phase = "plan" });
        }
        using var document = JsonDocument.Parse(input);
        var previous = document.RootElement;
        string Required(string name) => previous.GetProperty(name).GetString()
            ?? throw new InvalidDataException("Missing " + name);
        var previousPhase = Required("phase");
        var seed = Required("seed");
        string Expect(string expected)
        {
            if (previousPhase != expected)
                throw new InvalidDataException($"Expected {expected} before {_phase.Phase}; received {previousPhase}");
            return seed;
        }
        switch (_phase.Phase)
        {
            case "plan":
                Expect("orchestrate");
                if (previous.GetProperty("admitted").GetBoolean() != true)
                    throw new InvalidDataException("Seed was not admitted");
                return JsonSerializer.Serialize(new { phase = "plan", seed, plan_status = "READY",
                    deliverable = "deterministic acceptance artifact", next_phase = "make" });
            case "make":
                Expect("plan");
                if (Required("plan_status") != "READY")
                    throw new InvalidDataException("Plan was not ready");
                var text = "PMCRO-CYCLE-ARTIFACT|" + seed;
                return JsonSerializer.Serialize(new { phase = "make", seed,
                    artifact = text, artifact_sha256 = Sha(Encoding.UTF8.GetBytes(text)),
                    next_phase = "check" });
            case "check":
                Expect("make");
                var artifact = Required("artifact");
                var expectedHash = Required("artifact_sha256");
                if (artifact != "PMCRO-CYCLE-ARTIFACT|" + seed
                    || Sha(Encoding.UTF8.GetBytes(artifact)) != expectedHash)
                    throw new InvalidDataException("Make artifact rejected");
                return JsonSerializer.Serialize(new { phase = "check", seed, artifact,
                    artifact_sha256 = expectedHash, check_status = "PASS", next_phase = "reflect" });
            case "reflect":
                Expect("check");
                if (Required("check_status") != "PASS")
                    throw new InvalidDataException("Checker did not approve artifact");
                return JsonSerializer.Serialize(new { phase = "reflect", seed,
                    artifact = Required("artifact"), disposition = "PASS",
                    proposed_next_seed = "Await Orchestrator authorization for next cycle",
                    next_phase = "STOP" });
            default:
                throw new InvalidDataException("Unsupported phase: " + _phase.Phase);
        }
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = messages.LastOrDefault()?.Text ?? "";
        var output = ExecutePhase(input);
        yield return new AgentResponseUpdate
        {
            AgentId = Id, Role = ChatRole.Assistant, Contents = [new TextContent(output)],
            MessageId = Guid.NewGuid().ToString("N"), ResponseId = Guid.NewGuid().ToString("N")
        };
        await Task.CompletedTask;
    }

    private static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    private sealed class LocalCycleSession : AgentSession { }
}
