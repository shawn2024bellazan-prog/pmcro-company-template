using PMCR.O.Continuation;
using PMCR.O.Continuation.Integration;
using PMCR.O.Continuation.Plugin;
using PMCR.O.Core;

var tests = new (string Name, Func<Task> Run)[]
{
    ("disabled plugin blocks native application routing", DisabledByDefault),
    ("trusted caller mismatch fails before plugin", TrustedCallerMismatch),
    ("application authorization blocks continuation", OuterAuthorizationDenied),
    ("missing independent checker proof defers", CheckerProofMissing),
    ("correlated governed continuation dispatches", AuthorizedDispatch),
    ("native idempotency blocks repeated returns", NativeIdempotency),
    ("continuation Trail cannot forge checker PASS", TrailHonesty),
    ("unknown dispatch outcome does not auto retry", UncertainOutcome),
    ("missing source frame fails evidence verification", MissingActualFrame)
};

var failures = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine("PASS " + name); }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine("FAIL " + name + ": " + ex.GetType().Name + ": " + ex.Message);
    }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} integration contract checks passed");
return failures == 0 ? 0 : 1;

static async Task DisabledByDefault()
{
    var rig = await IntegrationRig.CreateAsync();
    var reply = await rig.Router().DispatchAsync(rig.Command());
    Require(reply.Status == "deferred" && reply.Code == "CONTINUATION_DEFERRED", "disabled plugin did not defer");
    Require(rig.Dispatcher.Calls == 0 && rig.Proof.Calls == 0, "disabled plugin consulted checker or dispatched");
}

static async Task TrustedCallerMismatch()
{
    var rig = await IntegrationRig.CreateAsync();
    rig.Resolver.Binding = rig.Resolver.Binding! with { Grant = rig.Grant(true) };
    var response = await rig.Router().DispatchAsync(rig.Command() with { CorrelationId = "forged-correlation" });
    Require(response.Code == "CALLER_BINDING_MISMATCH", "untrusted correlation bypassed binding");
    Require(rig.Dispatcher.Calls == 0 && rig.Proof.Calls == 0, "mismatched return reached plugin");
}

static async Task OuterAuthorizationDenied()
{
    var rig = await IntegrationRig.CreateAsync();
    rig.Gate.Disposition = ApprovalDisposition.RequireHumanApproval;
    rig.Resolver.Binding = rig.Resolver.Binding! with { Grant = rig.Grant(true) };
    var response = await rig.Router().DispatchAsync(rig.Command());
    Require(response.Status == "waiting_for_approval" && response.RequiresHumanAction, "outer gate did not require approval");
    Require(rig.Resolver.Calls == 0 && rig.Dispatcher.Calls == 0, "blocked command reached continuation");
}

static async Task CheckerProofMissing()
{
    var rig = await IntegrationRig.CreateAsync();
    rig.Resolver.Binding = rig.Resolver.Binding! with { Grant = rig.Grant(true) };
    rig.Proof.Allowed = false;
    var response = await rig.Router().DispatchAsync(rig.Command());
    Require(response.Payload?.ReasonCode == "evidence_unverified", "unverified checker evidence was not deferred");
    Require(rig.Proof.Calls == 1 && rig.Policy.Calls == 0 && rig.Dispatcher.Calls == 0,
        "checker failure reached policy or dispatcher");
}

static async Task AuthorizedDispatch()
{
    var rig = await IntegrationRig.CreateAsync();
    rig.Resolver.Binding = rig.Resolver.Binding! with { Grant = rig.Grant(true) };
    var response = await rig.Router().DispatchAsync(rig.Command());
    Require(response.Status == "completed" && response.Payload?.Status == ContinuationStatus.Dispatched,
        "governed continuation did not complete");
    Require(response.CorrelationId == rig.Caller.CorrelationId && response.TrailFrameId is not null,
        "native result lost correlation or Trail Frame");
    Require(rig.Proof.Calls == 1 && rig.Policy.Calls == 1 && rig.Dispatcher.Calls == 1,
        "continuation gates not traversed exactly once");
}

static async Task NativeIdempotency()
{
    var rig = await IntegrationRig.CreateAsync();
    rig.Resolver.Binding = rig.Resolver.Binding! with { Grant = rig.Grant(true) };
    var router = rig.Router();
    var command = rig.Command();
    var first = await router.DispatchAsync(command);
    var duplicate = await router.DispatchAsync(command with { CommandId = "cmd-return-duplicate" });
    Require(first.Status == "completed" && duplicate.Code == "DUPLICATE_COMMAND_COMPLETED",
        "native idempotency did not block duplicate");
    Require(rig.Dispatcher.Calls == 1, "duplicate continuation dispatched twice");
}

static async Task TrailHonesty()
{
    var rig = await IntegrationRig.CreateAsync();
    await rig.Router().DispatchAsync(rig.Command());
    var frames = await rig.Trails.ReadRunAsync(rig.Caller.RunId);
    var decisions = frames.Where(frame => frame.Phase == "continuation-decision").ToArray();
    Require(decisions.Length == 1 && decisions[0].CheckerVerdict == "NOT_CHECKED",
        "integration fabricated a checker PASS");
    Require(decisions[0].CorrelationId == rig.Caller.CorrelationId &&
        decisions[0].EvidenceRefs.Any(x => x.Contains("plugin_disabled")),
        "correlated plugin decision was not persisted");
}

static async Task UncertainOutcome()
{
    var rig = await IntegrationRig.CreateAsync();
    rig.Resolver.Binding = rig.Resolver.Binding! with { Grant = rig.Grant(true) };
    rig.Dispatcher.Fail = true;
    var router = rig.Router();
    var first = await router.DispatchAsync(rig.Command());
    var second = await router.DispatchAsync(rig.Command() with { CommandId = "cmd-retry-unsafe" });
    Require(first.Code == "CONTINUATION_OUTCOME_UNCERTAIN" &&
        second.Code == "DUPLICATE_COMMAND_COMPLETED" && rig.Dispatcher.Calls == 1,
        "uncertain side effects were automatically retried");
}

static async Task MissingActualFrame()
{
    var rig = await IntegrationRig.CreateAsync(includeSource: false);
    rig.Resolver.Binding = rig.Resolver.Binding! with { Grant = rig.Grant(true) };
    var reply = await rig.Router().DispatchAsync(rig.Command());
    Require(reply.Payload?.ReasonCode == "evidence_unverified" && rig.Proof.Calls == 0,
        "missing Trail evidence was treated as verified");
}

static void Require(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

sealed class IntegrationRig
{
    public CallerContext Caller { get; } = new(
        "@orchestrator", "@reflector", "intent-integrate", "run-integrate", "corr-integrate",
        "cmd-original", "frame-parent", "/pmcro:run", 0, 5, 2);
    public JsonlEnterpriseTrailStore Trails { get; } = new(
        Path.Combine(Path.GetTempPath(), "pmcro-integration-" + Guid.NewGuid().ToString("N")));
    public FakeIndependentProof Proof { get; } = new();
    public FakeContinuationPolicy Policy { get; } = new();
    public FakeNextDispatcher Dispatcher { get; } = new();
    public FakeOuterGate Gate { get; } = new();
    public FakeTrustedResolver Resolver { get; } = new();
    public CommandExecutionContext HostContext { get; } = new("org-test", "project-test", "@orchestrator", "policy.continuation");

    public static async Task<IntegrationRig> CreateAsync(bool includeSource = true)
    {
        var rig = new IntegrationRig();
        await rig.Trails.AppendAsync(rig.Frame("frame-parent"));
        if (includeSource) await rig.Trails.AppendAsync(rig.Frame("frame-source"));
        rig.Resolver.Binding = new TrustedContinuationInvocation(rig.Caller, rig.Grant(false), "/pmcro:continue");
        return rig;
    }

    public PluginActivationGrant Grant(bool enabled) =>
        new("pmcro.continuation", "0.1.0", "pmcro.policy.continuation", "@host", true, enabled);

    public PmcroApplicationCommandRouter<CycleResultEnvelope, ContinuationReceipt> Router()
    {
        var writer = new EnterpriseContinuationTrailWriter(Trails, HostContext);
        var verifier = new EnterpriseContinuationEvidenceVerifier(Trails, Proof);
        var handler = new CallerContinuationHandler(verifier, Policy, new InMemorySeedClaimStore(), Dispatcher, writer);
        var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "plugin.json")));
        var plugin = new ContinuationPlugin(manifest, handler, writer);
        return ContinuationApplicationComposition.CreateRouter(plugin, Resolver, Gate, Trails, HostContext);
    }

    public PmcroCommand<CycleResultEnvelope> Command() => new(
        "1.0", "cmd-return-01", Caller.IntentId, Caller.RunId, Caller.CorrelationId,
        Caller.CommandId, "@reflector", ApplicationContinuationHandler.ReturnCommandType,
        DateTimeOffset.UtcNow, "native-return-key", Result(), new Dictionary<string, string>());

    private CycleResultEnvelope Result()
    {
        var seed = new NextSeedEnvelope(PmcrSchemas.NextSeed, "seed-return", "frame-source",
            new NextSeedCommand("@orchestrator", "/pmcro:plan", "next_increment"),
            new PMCR.O.Continuation.SeedIntent("Activate PMCR-O", "Use governed continuation"),
            new SeedGovernance("pending_policy_evaluation", false),
            new SeedExecution("seed-return", 1, 5, 2), "proposed");
        return new CycleResultEnvelope(PmcrSchemas.CycleResult, "cycle.result", "completed",
            new CycleRouting("@reflector", "@orchestrator", "@orchestrator", "/pmcro:run"),
            new CorrelationContext(Caller.IntentId, Caller.RunId, Caller.CorrelationId,
                Caller.CommandId, Caller.ParentFrameId),
            new CycleOutcome("reflect", "completed", new CycleEvidence(["frame-parent", "frame-source"], "passed")),
            seed, true, "@reflector", "next_seed.acceptance");
    }

    private EnterpriseTrailFrame Frame(string id) => new(
        SchemaVersion: "1.0", OrganizationId: "org-test", ProjectId: "project-test",
        IntentId: Caller.IntentId, RunId: Caller.RunId, AgentId: "@reflector",
        CorrelationId: Caller.CorrelationId, FrameId: id, Sequence: 1,
        ParentFrameId: null, CausalFrameIds: [], Timestamp: DateTimeOffset.UtcNow,
        OriginalSeed: "Activate PMCR-O", RefinedObjective: "Use governed continuation",
        Constraints: [], NextSeed: null, Phase: "reflect", Capability: "pmcro.reflect",
        Action: "cycle.reflect", Status: "completed", EvidenceRefs: ["fixture:source"],
        PolicyId: "policy.test", AuthorizationDecision: "not_applicable", ApprovalRef: null,
        CheckerVerdict: "NOT_CHECKED", Reflection: "Fixture: no independent approval implied",
        Corrections: [], ObjectiveComplete: false, ProposedLesson: null,
        LearningStatus: "observation", Confidence: null, Provenance: "test-fixture",
        ReuseEligible: false, PayloadDigest: null);
}

sealed class FakeIndependentProof : IIndependentContinuationCheckerProof
{
    public bool Allowed { get; set; } = true;
    public int Calls { get; private set; }
    public Task<bool> VerifyAsync(CycleResultEnvelope result, CallerContext caller,
        IReadOnlyList<EnterpriseTrailFrame> correlatedFrames, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Allowed);
    }
}

sealed class FakeContinuationPolicy : IContinuationPolicy
{
    public int Calls { get; private set; }
    public Task<PMCR.O.Continuation.AuthorizationDecision> EvaluateAsync(
        NextSeedEnvelope seed, CallerContext caller, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new PMCR.O.Continuation.AuthorizationDecision(
            PMCR.O.Continuation.AuthorizationDisposition.Approved, "test-only-approval"));
    }
}

sealed class FakeNextDispatcher : INextSeedDispatcher
{
    public int Calls { get; private set; }
    public bool Fail { get; set; }
    public Task DispatchAsync(NextSeedEnvelope seed, CallerContext caller, CancellationToken cancellationToken)
    {
        Calls++;
        if (Fail) throw new InvalidOperationException("test outcome uncertain");
        return Task.CompletedTask;
    }
}

sealed class FakeOuterGate : ICommandAuthorizationGate<CycleResultEnvelope>
{
    public ApprovalDisposition Disposition { get; set; } = ApprovalDisposition.Allow;
    public Task<ApprovalDecision> AuthorizeAsync(
        PmcroCommand<CycleResultEnvelope> command, CancellationToken cancellationToken) =>
        Task.FromResult(new ApprovalDecision(Disposition, "test-policy", "fixture"));
}

sealed class FakeTrustedResolver : ITrustedContinuationInvocationResolver
{
    public TrustedContinuationInvocation? Binding { get; set; }
    public int Calls { get; private set; }
    public Task<TrustedContinuationInvocation?> ResolveAsync(
        PmcroCommand<CycleResultEnvelope> command, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Binding);
    }
}
