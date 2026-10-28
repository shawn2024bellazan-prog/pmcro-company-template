using System.Text.Json;
using ProjectName.Contracts;
using ProjectName.Core;
using ProjectName.Harness;
using ProjectName.Runtime.Abstractions;
using ProjectName.Runtime.Maf;
using Xunit;

namespace ProjectName.Runtime.Tests;

public sealed class CandidateCompanyIntegrationTests
{
    private static string Source(string relative)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 16 && root is not null; i++, root = root.Parent)
        {
            var candidate = Path.Combine(root.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("candidate-source-not-found:" + relative);
    }

    [Fact]
    public void Candidate_office_lists_report_exact_unresolved_differences_without_aliasing()
    {
        var company = ExecutiveOrganizationManifestLoader.Load(Source(
            ".agents/organizations/pmcro-autonomous-ai-agent-company/organization.candidate.json"));
        using var doc = JsonDocument.Parse(File.ReadAllText(Source("marketplaces/csuite-marketplace/manifest.json")));
        var marketplace = doc.RootElement.GetProperty("executives").EnumerateArray()
            .Select(x => x.GetString()!).ToArray();
        var difference = CandidateExecutiveCatalogValidator.Compare(company, marketplace);

        Assert.False(difference.ExactMatch);
        Assert.Equal(new[] { "CISO", "COS" }, difference.OrganizationOnly);
        Assert.Equal(new[] { "CHIEFOFSTAFF", "CIO", "CSO" }, difference.MarketplaceOnly);
        Assert.Equal(new[] { "ceo", "cto" }, company.DefaultOfficeIds);
        Assert.Empty(company.OwnerApprovalOfficeIds);
        Assert.False(company.GrantsAuthority);
    }

    [Fact]
    public void Catalog_validation_does_not_invent_aliases_or_accept_duplicates()
    {
        var company = ExecutiveOrganizationManifestLoader.Load(Source(
            ".agents/organizations/pmcro-autonomous-ai-agent-company/organization.candidate.json"));
        Assert.Throws<InvalidDataException>(() =>
            CandidateExecutiveCatalogValidator.Compare(company, ["CEO", "CEO"]));
        Assert.Throws<InvalidDataException>(() =>
            CandidateExecutiveCatalogValidator.Compare(company, Array.Empty<string>()));
    }

    private static JsonElement Ump() => JsonSerializer.SerializeToElement(new Dictionary<string, object>
    {
        ["identity"] = "I AM the Chief Technology Officer Agent.",
        ["authority"] = "candidate recommendations only",
        ["objective"] = "evaluate feasibility",
        ["evidence"] = new[] { "design:source-manifest" },
        ["causal-chain"] = new[] { "seed:1" },
        ["dependencies"] = new[] { "ceo position" },
        ["constraints"] = new[] { "Creation != Activation" },
        ["risk"] = new[] { "missing role grant" },
        ["decision"] = "recommend inspection only",
        ["next-action"] = "return to owning Trail"
    });

    [Fact]
    public void Ump_translation_preserves_fields_and_creates_non_authoritative_proposal()
    {
        var payload = Ump();
        var message = CandidateUmpMessageAdapter.ToProposal(
            payload, "message-001", "trail-1", "executive-cto", "pmcro-orchestrator",
            "cycle-1", "seed-1", "corr-1", DateTimeOffset.UtcNow);
        message.EnsureValidForPersistence();

        Assert.Equal("pmcro.messaging/v1", message.ContractRef);
        Assert.Equal(PmcroMessageKinds.ExecutivePosition, message.Kind);
        Assert.Equal("trail-1", message.TrailId);
        Assert.Equal("cycle-1", message.CycleRef);
        Assert.Equal("seed-1", message.CausationId);
        Assert.Equal("corr-1", message.CorrelationId);
        Assert.Null(message.Phase);
        Assert.Null(message.FrameRef);
        Assert.False(message.GrantsAuthority);
        Assert.Equal(JsonSerializer.Serialize(payload), JsonSerializer.Serialize(message.Payload));
        Assert.Equal("candidate recommendations only", message.Payload.GetProperty("authority").GetString());
    }

    [Fact]
    public void Ump_missing_required_field_or_causal_lineage_is_rejected()
    {
        using var missing = JsonDocument.Parse("""
        {"identity":"I AM the CTO Agent.","objective":"inspect"}
        """);
        Assert.Throws<InvalidDataException>(() =>
            CandidateUmpMessageAdapter.ToProposal(missing.RootElement,
                "m-2", "trail", "executive-cto", "orchestrator", "cycle", "seed", "corr", DateTimeOffset.UtcNow));
        Assert.Throws<InvalidDataException>(() =>
            CandidateUmpMessageAdapter.ToProposal(Ump(),
                "m-3", "trail", "executive-cto", "orchestrator", "cycle", "", "corr", DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("grantsAuthority")]
    [InlineData("effectiveGrant")]
    [InlineData("issuerProof")]
    public void Ump_must_not_smuggle_grant_fields(string prohibited)
    {
        var content = Ump();
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content.GetRawText())!;
        values[prohibited] = JsonSerializer.SerializeToElement(true);
        var forged = JsonSerializer.SerializeToElement(values);
        Assert.Throws<InvalidDataException>(() =>
            CandidateUmpMessageAdapter.ToProposal(forged,
                "m-f", "trail", "executive-cto", "orchestrator", "cycle", "seed", "corr", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Candidate_workflow_requires_orchestrator_boundaries_and_denies_authority_carry()
    {
        var projection = PmcroMafWorkflowCompiler.Compile();
        Assert.Contains("orchestrator-admit-plan", projection.Mermaid, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orchestrator-admit-check", projection.Mermaid, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, projection.MicroWorkflows.Count);
        Assert.Throws<UnauthorizedAccessException>(() =>
            PmcroRoleOwnership.RequireOwnedFrame("planner", "MAKE"));

        var admit = new PmcroAdmissionExecutor("guard", "maker", "MAKE");
        var framed = new PmcroWorkflowFrame("orchestrator", "ORCHESTRATE", "inspect", "cycle-guard", true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await admit.HandleAsync(framed, null!, CancellationToken.None));
    }

    [Fact]
    public async Task Candidate_role_worker_denies_by_default_without_starting_model()
    {
        var runtime = new CountingRuntime();
        var bus = new CountingBus();
        var handler = new GovernedRolePhaseHandler(runtime, new DenyAllRoleExecutionAdmissionProvider(), bus);
        var request = new PmcroRequest(
            "plan", "review candidate design", "pmcro-planner",
            TrailRef: "trail-test", SeedRef: "seed-test", CycleRef: "cycle-test",
            Role: "planner", Phase: "PLAN");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.HandleAsync(request, "planner:PLAN"));

        Assert.Equal(0, runtime.Count);
        Assert.Equal(0, bus.Count);
    }

    private sealed class CountingRuntime : IAgentRuntime
    {
        public int Count { get; private set; }
        public string FrameworkId => "maf";
        public Task<AgentRuntimeResult> RunAsync(AgentRuntimeRequest request, CancellationToken ct = default)
        {
            Count++;
            throw new InvalidOperationException("model-must-not-run");
        }
    }

    private sealed class CountingBus : IMessageBus
    {
        public int Count { get; private set; }
        public void Publish(PmcroMessageEnvelope message) => Count++;
        public PmcroMessageEnvelope? Peek(IReadOnlySet<string>? kinds = null, string? targetAgent = null) => null;
        public PmcroMessageEnvelope? Claim(string workerId, TimeSpan lease,
            IReadOnlySet<string>? kinds = null, string? targetAgent = null) => null;
        public void Acknowledge(string messageId) { }
        public IReadOnlyList<PmcroMessageEnvelope> Snapshot() => [];
    }
}