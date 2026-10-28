using PMCR.O.Continuation;

var cases = new (string Name, Func<Task> Run)[]
{
    ("authorized continuation dispatches and trails", SuccessfulDispatch),
    ("wrong return address rejects", WrongReturnAddress),
    ("correlation mismatch rejects", CorrelationMismatch),
    ("source frame missing rejects", MissingFrame),
    ("independent evidence check defers", UnverifiedEvidence),
    ("checker failure defers", CheckerNotPassed),
    ("policy denial rejects", PolicyDenied),
    ("unauthorized inherited authority rejects", InheritedAuthority),
    ("recursion bound rejects", RecursionBound),
    ("duplicate seed dispatches once", DuplicateSeed),
    ("dispatcher failure remains uncertain and claimed", UncertainExecution),
    ("trail failure stops dispatch", TrailFailure),
    ("completed cycle without seed returns cleanly", NoNextSeed)
};

var failures = 0;
foreach (var (name, run) in cases)
{
    try
    {
        await run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {error.GetType().Name}: {error.Message}");
    }
}

Console.WriteLine($"{cases.Length - failures}/{cases.Length} contract checks passed");
return failures == 0 ? 0 : 1;

static (CycleResultEnvelope Message, CallerContext Caller) ValidCase()
{
    var caller = new CallerContext(
        "@orchestrator", "@reflector", "intent-001", "run-001", "corr-001",
        "command-001", "frame-001", "/pmcro:run", 0, 5, 2);
    var seed = new NextSeedEnvelope(
        PmcrSchemas.NextSeed,
        "seed-002", "frame-002",
        new NextSeedCommand("@planner", "/pmcro:plan", "implement_next_increment"),
        new SeedIntent("Activate PMCR-O", "Integrate governed command routing"),
        new SeedGovernance("pending_policy_evaluation", false),
        new SeedExecution("seed-002", 1, 5, 2),
        "proposed");
    var message = new CycleResultEnvelope(
        PmcrSchemas.CycleResult, "cycle.result", "completed",
        new CycleRouting("@reflector", "@orchestrator", "@orchestrator", "/pmcro:run"),
        new CorrelationContext("intent-001", "run-001", "corr-001", "command-001", "frame-001"),
        new CycleOutcome("reflect", "completed", new CycleEvidence(new[] { "frame-001", "frame-002" }, "passed")),
        seed, true, "@reflector", "next_seed.acceptance");
    return (message, caller);
}

static TestRig Rig() => new();

static void Require(bool condition, string reason)
{
    if (!condition) throw new InvalidOperationException(reason);
}

static async Task SuccessfulDispatch()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    var response = await rig.Handler.HandleAsync(message, caller);
    Require(response.Status == ContinuationStatus.Dispatched, "should dispatch");
    Require(rig.Dispatcher.Calls == 1, "one dispatch required");
    Require(rig.Trail.Records.Count == 2, "claim and outcome trails required");
    Require(rig.Trail.Records.All(r => r.CorrelationId == caller.CorrelationId), "correlation must be preserved");
}

static async Task WrongReturnAddress()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    var changed = message with { Routing = message.Routing with { ReplyTo = "@other" } };
    var result = await rig.Handler.HandleAsync(changed, caller);
    Require(result.Status == ContinuationStatus.Rejected && result.ReasonCode == "invalid_return_route", "must reject route");
    Require(rig.Dispatcher.Calls == 0, "no dispatch permitted");
}

static async Task CorrelationMismatch()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    var changed = message with { Correlation = message.Correlation with { CorrelationId = "spoofed" } };
    var result = await rig.Handler.HandleAsync(changed, caller);
    Require(result.ReasonCode == "correlation_mismatch", "must reject lineage mismatch");
    Require(rig.Dispatcher.Calls == 0, "no dispatch permitted");
}

static async Task MissingFrame()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    var changed = message with { Result = message.Result with { Evidence = new CycleEvidence(new[] { "frame-001" }, "passed") } };
    var result = await rig.Handler.HandleAsync(changed, caller);
    Require(result.ReasonCode == "missing_evidence_reference", "must reject missing source frame");
}

static async Task UnverifiedEvidence()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    rig.Evidence.Verified = false;
    var result = await rig.Handler.HandleAsync(message, caller);
    Require(result.Status == ContinuationStatus.Deferred && result.ReasonCode == "evidence_unverified", "must defer");
    Require(rig.Policy.Calls == 0 && rig.Dispatcher.Calls == 0, "must not authorize or dispatch");
}

static async Task CheckerNotPassed()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    var changed = message with { Result = message.Result with { Evidence = message.Result.Evidence with { CheckerVerdict = "pending" } } };
    var result = await rig.Handler.HandleAsync(changed, caller);
    Require(result.ReasonCode == "checker_not_passed", "must defer until checked");
    Require(rig.Evidence.Calls == 0, "claimed pending check is not sufficient");
}

static async Task PolicyDenied()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    rig.Policy.Disposition = AuthorizationDisposition.Denied;
    var result = await rig.Handler.HandleAsync(message, caller);
    Require(result.Status == ContinuationStatus.Rejected && result.ReasonCode == "policy_denied", "must reject");
    Require(rig.Dispatcher.Calls == 0, "must not dispatch denied command");
}

static async Task InheritedAuthority()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    var changed = message with { NextSeed = message.NextSeed! with { Governance = new SeedGovernance("approved", true) } };
    var result = await rig.Handler.HandleAsync(changed, caller);
    Require(result.ReasonCode == "untrusted_authority", "child cannot grant authority");
}

static async Task RecursionBound()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    var changed = message with { NextSeed = message.NextSeed! with { Execution = message.NextSeed!.Execution with { RecursionDepth = 6 } } };
    var result = await rig.Handler.HandleAsync(changed, caller);
    Require(result.ReasonCode == "execution_bounds_exceeded", "must enforce recursion budget");
}

static async Task DuplicateSeed()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    var first = await rig.Handler.HandleAsync(message, caller);
    var second = await rig.Handler.HandleAsync(message, caller);
    Require(first.Status == ContinuationStatus.Dispatched && second.Status == ContinuationStatus.Duplicate, "must deduplicate");
    Require(rig.Dispatcher.Calls == 1, "dispatcher called more than once");
}

static async Task UncertainExecution()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    rig.Dispatcher.Fail = true;
    var first = await rig.Handler.HandleAsync(message, caller);
    var second = await rig.Handler.HandleAsync(message, caller);
    Require(first.Status == ContinuationStatus.ExecutionUncertain, "must report uncertainty");
    Require(second.Status == ContinuationStatus.Duplicate, "uncertain claim must be retained");
    Require(rig.Dispatcher.Calls == 1, "no automatic retry of uncertain side effects");
}

static async Task TrailFailure()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    rig.Trail.Fail = true;
    try
    {
        await rig.Handler.HandleAsync(message, caller);
        throw new InvalidOperationException("expected trail failure");
    }
    catch (TrailUnavailableException)
    {
        Require(rig.Dispatcher.Calls == 0, "trail failure must prevent dispatch");
    }
}

static async Task NoNextSeed()
{
    var (message, caller) = ValidCase();
    var rig = Rig();
    var result = await rig.Handler.HandleAsync(message with { NextSeed = null }, caller);
    Require(result.Status == ContinuationStatus.NoContinuation, "no seed should finish cleanly");
    Require(rig.Dispatcher.Calls == 0, "no dispatch permitted");
}

sealed class TestRig
{
    public FakeEvidence Evidence { get; } = new();
    public FakePolicy Policy { get; } = new();
    public InMemorySeedClaimStore Claims { get; } = new();
    public FakeDispatcher Dispatcher { get; } = new();
    public FakeTrail Trail { get; } = new();

    public CallerContinuationHandler Handler => new(Evidence, Policy, Claims, Dispatcher, Trail);
}

sealed class FakeEvidence : IContinuationEvidenceVerifier
{
    public bool Verified { get; set; } = true;
    public int Calls { get; private set; }
    public Task<bool> VerifyAsync(CycleResultEnvelope result, CallerContext caller, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Verified);
    }
}

sealed class FakePolicy : IContinuationPolicy
{
    public AuthorizationDisposition Disposition { get; set; } = AuthorizationDisposition.Approved;
    public int Calls { get; private set; }
    public Task<AuthorizationDecision> EvaluateAsync(NextSeedEnvelope seed, CallerContext caller, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new AuthorizationDecision(Disposition, "test_policy"));
    }
}

sealed class FakeDispatcher : INextSeedDispatcher
{
    public int Calls { get; private set; }
    public bool Fail { get; set; }
    public Task DispatchAsync(NextSeedEnvelope seed, CallerContext caller, CancellationToken cancellationToken)
    {
        Calls++;
        if (Fail) throw new InvalidOperationException("simulated uncertain dispatch");
        return Task.CompletedTask;
    }
}

sealed class TrailUnavailableException : Exception { }

sealed class FakeTrail : IContinuationTrailWriter
{
    public List<ContinuationTrailRecord> Records { get; } = new();
    public bool Fail { get; set; }
    public Task WriteAsync(ContinuationTrailRecord record, CancellationToken cancellationToken)
    {
        if (Fail) throw new TrailUnavailableException();
        Records.Add(record);
        return Task.CompletedTask;
    }
}
