using System.Text.Json;
using PMCR.O.Continuation;
using PMCR.O.Continuation.Integration;
using PMCR.O.Continuation.Plugin;
using PMCR.O.Core;
using PMCR.O.SubagentReturn;

var tests = new (string Name, Func<Task> Run)[]
{
    ("successful return completes, dispatches once, and trails caller-owned", SuccessfulReturn),
    ("interrupted return records caller-owned failure and never continues", InterruptedReturn),
    ("host-observed timeout overrides a late child success", TimedOutReturn),
    ("completed return without final result fails with caller frame", MissingFinalResult),
    ("malformed or missing i_context is rejected without consuming the claim", MalformedIContext),
    ("i_context envelope round-trips and untrusted JSON maps to stable codes", EnvelopeParsing),
    ("correlation mismatch rejects and never dispatches", CorrelationMismatch),
    ("child claiming another identity is rejected", IAmMismatch),
    ("duplicate terminal return is consumed once", DuplicateReturn),
    ("caller failure frames are caller-owned, attributed, and redacted", CallerFailureAttribution),
    ("independent checker proof is required and must differ from the maker", IndependentChecker),
    ("continuation authorization stays caller-owned and fail-closed", ContinuationAuthorization),
    ("disabled-by-default plugin defers without side effects", DisabledByDefault),
    ("unsafe or foreign plugin manifest fails closed", UnsafeManifest),
    ("grant and route mismatches are rejected", GrantAndRouteMismatch),
    ("missing parent frame is attributed to the caller", ParentFrameMissing),
    ("incomplete trusted context is rejected without a trail write", TrustedContextInvalid),
    ("native command router enforces authorization, binding, and idempotency", RouterIntegration),
    ("canonical role spoofing denied", RoleSpoofing),
    ("canonical authority widening denied", AuthorityWidening),
    ("host terminal missing closes run", TerminalMissing),
    ("checker evidence forgery rejected", ForgedCheckerEvidence),
    ("checker outage recovers without losing terminal claim", CheckerOutageRecovery),
    ("outer and canonical role must agree", ConflictingRoles),
    ("result digest mismatches cannot dispatch", ResultDigestMismatch),
    ("host checker proof gate cannot be bypassed", UntrustedProofGate),
    ("durable state survives a new store instance", DurableStateRestart),
    ("durable compare and swap has one winner", DurableStateCas),
    ("HMAC checker proof binds identity and result", SignedCheckerProof),
    ("hash-linked SQLite trail detects tampering", HashTrailIntegrity),
    ("durable completion closes result without seed", DurableNoSeed),
    ("durable seed defers without external dispatch and can restart", DurableSeedDeferred),
    ("transactional outbox reservation does not replay", OutboxReservation),
    ("outbox uncertain requires external reconciliation", OutboxReconciliation),
    ("trusted Trail head anchor detects forgery", TrustedAnchorForgery),
    ("caller policy outage remains deferred and recoverable", DurablePolicyRecovery)
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
Console.WriteLine($"{tests.Length - failures}/{tests.Length} subagent-return contract checks passed");
return failures == 0 ? 0 : 1;

static async Task SuccessfulReturn()
{
    var rig = await Rig.CreateAsync();
    var receipt = await rig.Handler().HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.Completed && receipt.ReasonCode == "continuation_dispatched",
        "authorized return did not complete: " + receipt.ReasonCode);
    Require(rig.Checker.Calls == 1 && rig.Policy.Calls == 1 && rig.Dispatcher.Calls == 1,
        "checker, policy and dispatcher were not each consulted exactly once");
    Require(receipt.Continuation?.Status == ContinuationStatus.Dispatched, "continuation receipt missing");

    var frames = await rig.Trails.ReadRunAsync(rig.Caller.RunId);
    var mine = frames.Where(f => f.Provenance == CallerFailureFrameFactory.Provenance).ToArray();
    Require(mine.Length == 1 && mine[0].Phase == "subagent-return-decision" && mine[0].Status == "completed",
        "expected one completed decision frame");
    Require(mine[0].AgentId == rig.Caller.AgentId, "decision frame was not caller-owned");
    Require(mine[0].EvidenceRefs.Contains("checker-agent:@checker"), "independent checker identity not recorded");
    Require(mine.All(f => f.CheckerVerdict != "PASS"), "subagent-return fabricated a checker PASS in its own frames");
}

static async Task InterruptedReturn()
{
    var rig = await Rig.CreateAsync();
    var returned = rig.NonCompleted("interrupted") with { StopReason = "user pasted SECRET-TOKEN-123 here" };
    var receipt = await rig.Handler().HandleAsync(returned, rig.Invocation(), rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.Interrupted && receipt.Attribution == SubagentFailureAttribution.Child,
        "interrupted return was not recorded as an attributed interruption");
    Require(rig.Checker.Calls == 0 && rig.Dispatcher.Calls == 0, "an interrupted run was checked or continued");

    var frames = await rig.Trails.ReadRunAsync(rig.Caller.RunId);
    var failure = frames.Single(f => f.Phase == "subagent-return-failure");
    Require(failure.Status == "interrupted" && failure.EvidenceRefs.Contains("failure-owner:caller"),
        "failure frame missing status or caller ownership");
    Require(!JsonSerializer.Serialize(frames).Contains("SECRET-TOKEN-123"),
        "child-supplied free text leaked into the Trail");
}

static async Task TimedOutReturn()
{
    var rig = await Rig.CreateAsync();
    var handler = rig.Handler();

    // The child never returned: the host observed the deadline and supplied it as trusted state.
    var receipt = await handler.HandleAsync(null, rig.Invocation("timed_out"), rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.TimedOut && receipt.Attribution == SubagentFailureAttribution.Host &&
            receipt.ReasonCode == "host_observed_timed_out", "host-observed timeout was not recorded");

    // A late, otherwise valid success must not resurrect the run.
    var late = await handler.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(late.Status == SubagentReturnStatus.Duplicate, "late success after timeout was accepted");
    Require(rig.Dispatcher.Calls == 0, "timed-out run reached dispatcher");

    // Even in a single call, the host's observation outranks the child's claim.
    var rig2 = await Rig.CreateAsync();
    var combined = await rig2.Handler().HandleAsync(rig2.Completed(), rig2.Invocation("timed_out"), rig2.Grant());
    Require(combined.Status == SubagentReturnStatus.TimedOut && rig2.Dispatcher.Calls == 0,
        "child success overrode host-observed timeout");

    // A null return with no host observation is simply an unauthenticated non-return.
    var rig3 = await Rig.CreateAsync();
    var none = await rig3.Handler().HandleAsync(null, rig3.Invocation(), rig3.Grant());
    Require(none.Status == SubagentReturnStatus.Deferred && none.ReasonCode == "no_authenticated_terminal_observation", "nonterminal absence closed run");
}

static async Task MissingFinalResult()
{
    var rig = await Rig.CreateAsync();
    var returned = rig.Completed() with { FinalResult = null };
    var receipt = await rig.Handler().HandleAsync(returned, rig.Invocation(), rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.Failed && receipt.ReasonCode == "missing_final_result" &&
            receipt.Attribution == SubagentFailureAttribution.Child, "missing final result was not a failure");
    Require(rig.Checker.Calls == 0 && rig.Dispatcher.Calls == 0, "run without a final result progressed");
    var frames = await rig.Trails.ReadRunAsync(rig.Caller.RunId);
    Require(frames.Any(f => f.Phase == "subagent-return-failure" && f.EvidenceRefs.Contains("reason:missing_final_result")),
        "no caller-owned failure frame for the missing final result");
}

static async Task MalformedIContext()
{
    var rig = await Rig.CreateAsync();
    var handler = rig.Handler();
    var blank = rig.Completed() with { IContext = rig.IAm() with { AgentId = " " } };
    var rejected = await handler.HandleAsync(blank, rig.Invocation(), rig.Grant());
    Require(rejected.Status == SubagentReturnStatus.Rejected && rejected.ReasonCode == "malformed_i_context",
        "blank I AM field was accepted");

    var absent = rig.Completed() with { IContext = null };
    var missing = await handler.HandleAsync(absent, rig.Invocation(), rig.Grant());
    Require(missing.ReasonCode == "missing_i_context" && rig.Dispatcher.Calls == 0, "absent I AM context was accepted");

    // An unauthenticated return must not consume the claim and block the genuine child.
    var genuine = await handler.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(genuine.Status == SubagentReturnStatus.Completed, "forged returns blocked the genuine child");
}

static async Task EnvelopeParsing()
{
    Require(SubagentReturnParser.Parse("{\"i_context\":\"I am the maker\"}").ReasonCode == "malformed_i_context",
        "string i_context not rejected");
    Require(SubagentReturnParser.Parse("{\"i_context\":{\"agent_id\":5}}").ReasonCode == "malformed_i_context",
        "mistyped i_context field not rejected");
    Require(SubagentReturnParser.Parse("{\"lifecycle_state\":\"completed\"}").ReasonCode == "missing_i_context",
        "absent i_context not reported");
    Require(SubagentReturnParser.Parse("{\"i_context\":null}").ReasonCode == "missing_i_context", "null i_context not reported");
    Require(SubagentReturnParser.Parse("not json {").ReasonCode == "malformed_envelope", "invalid JSON not rejected");
    Require(SubagentReturnParser.Parse("[]").ReasonCode == "malformed_envelope", "non-object JSON not rejected");

    var rig = await Rig.CreateAsync();
    var original = rig.NonCompleted("failed");
    var json = JsonSerializer.Serialize(original);
    Require(json.Contains("\"i_context\"") && json.Contains("\"agent_id\":\"@maker\""), "wire names are not snake_case");
    var parsed = SubagentReturnParser.Parse(json);
    Require(parsed.ReasonCode is null && parsed.Envelope is not null &&
            parsed.Envelope.IContext?.AgentId == original.IContext?.AgentId &&
            parsed.Envelope.IContext?.Canonical?.Schema == "pmcro.i-context/v1" &&
            parsed.Envelope.IContext?.Canonical?.Role == original.IContext?.Canonical?.Role &&
            parsed.Envelope.IContext?.Canonical?.AuthorityCeilingRef == original.IContext?.Canonical?.AuthorityCeilingRef &&
            parsed.Envelope.IContext?.Canonical?.ObjectIRefs.SequenceEqual(original.IContext!.Canonical!.ObjectIRefs, StringComparer.Ordinal) == true &&
            parsed.Envelope.LifecycleState == "failed",
        "valid envelope did not round-trip");
}

static async Task CorrelationMismatch()
{
    var rig = await Rig.CreateAsync();
    var handler = rig.Handler();
    var forged = rig.Completed() with { IContext = rig.IAm() with { CorrelationId = "forged-correlation" } };
    var receipt = await handler.HandleAsync(forged, rig.Invocation(), rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.Rejected && receipt.ReasonCode == "correlation_mismatch",
        "forged correlation was accepted");
    Require(rig.Checker.Calls == 0 && rig.Dispatcher.Calls == 0, "forged correlation reached checker or dispatcher");

    var wrongRun = rig.Completed() with { IContext = rig.IAm() with { SubagentRunId = "someone-elses-run" } };
    Require((await handler.HandleAsync(wrongRun, rig.Invocation(), rig.Grant())).ReasonCode == "correlation_mismatch",
        "another subagent run's return was accepted");

    // The envelope identifies the right run, but the nested final result carries different lineage.
    var good = rig.Completed();
    var nestedWrong = good with
    {
        FinalResult = good.FinalResult! with
        {
            Correlation = good.FinalResult.Correlation with { CorrelationId = "different-lineage" }
        }
    };
    var nested = await handler.HandleAsync(nestedWrong, rig.Invocation(), rig.Grant());
    Require(nested.Status == SubagentReturnStatus.Rejected && nested.ReasonCode == "correlation_mismatch" &&
            rig.Dispatcher.Calls == 0, "mismatched nested lineage was accepted");

    var badRoute = rig.Completed() with { ReplyTo = "@someone-else" };
    var routeResult = await Rig.Fresh(rig).HandleAsync(badRoute, rig.Invocation(), rig.Grant());
    Require(routeResult.ReasonCode == "invalid_return_route", "wrong return route was accepted");
}

static async Task IAmMismatch()
{
    var rig = await Rig.CreateAsync();
    var intruder = rig.Completed() with { IContext = rig.IAm() with { AgentId = "@intruder" } };
    var receipt = await rig.Handler().HandleAsync(intruder, rig.Invocation(), rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.Rejected && receipt.ReasonCode == "i_am_mismatch" &&
            rig.Dispatcher.Calls == 0, "an impersonating child was accepted");
}

static async Task DuplicateReturn()
{
    var rig = await Rig.CreateAsync();
    var handler = rig.Handler();
    var first = await handler.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    var second = await handler.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(first.Status == SubagentReturnStatus.Completed, "first return did not complete");
    Require(second.Status == SubagentReturnStatus.Duplicate && second.ReasonCode == "subagent_return_already_consumed",
        "duplicate return was not detected");
    Require(rig.Dispatcher.Calls == 1 && rig.Policy.Calls == 1 && rig.Checker.Calls >= 1,
        "duplicate return caused a second evaluation or dispatch");
}

static async Task CallerFailureAttribution()
{
    var rig = await Rig.CreateAsync();
    var receipt = await rig.Handler().HandleAsync(
        rig.Completed() with { FinalResult = null }, rig.Invocation(), rig.Grant());
    var frames = await rig.Trails.ReadRunAsync(rig.Caller.RunId);
    var failure = frames.Single(f => f.Phase == "subagent-return-failure");

    Require(failure.AgentId == rig.Caller.AgentId && failure.AgentId != rig.Caller.ExpectedChildAgent,
        "failure frame was authored by the child instead of the caller");
    Require(EnterpriseTrailFrameValidator.Validate(failure).IsValid, "failure frame is not a valid Enterprise Trail Frame");
    foreach (var expected in new[]
             {
                 "failure-owner:caller", "failure-attribution:child", "child-agent:@maker",
                 "subagent-run-id:subrun-001", "reason:missing_final_result", "causation-id:cmd-sr"
             })
        Require(failure.EvidenceRefs.Contains(expected), "failure frame missing evidence " + expected);
    Require(failure.CheckerVerdict == "NOT_CHECKED" && failure.Provenance == CallerFailureFrameFactory.Provenance &&
            failure.CausalFrameIds.Contains("frame-parent") && failure.AuthorizationDecision == "not_applicable",
        "failure frame asserted evidence it does not have");
    Require(receipt.TrailFrameId == failure.FrameId, "receipt does not point at the caller's failure frame");

    // Host and caller attribution are distinct from child attribution.
    var hostRig = await Rig.CreateAsync();
    await hostRig.Handler().HandleAsync(null, hostRig.Invocation("timed_out"), hostRig.Grant());
    var hostFrame = (await hostRig.Trails.ReadRunAsync(hostRig.Caller.RunId)).Single(f => f.Phase == "subagent-return-failure");
    Require(hostFrame.EvidenceRefs.Contains("failure-attribution:host") && hostFrame.AgentId == hostRig.Caller.AgentId,
        "host-observed failure was mis-attributed");
}

static async Task IndependentChecker()
{
    var unverified = await Rig.CreateAsync();
    unverified.Checker.Attestation = null;
    var a = await unverified.Handler().HandleAsync(unverified.Completed(), unverified.Invocation(), unverified.Grant());
    Require(a.Status == SubagentReturnStatus.Deferred && a.ReasonCode == "checker_unverified" &&
            unverified.Dispatcher.Calls == 0, "unverified checker did not defer");

    var self = await Rig.CreateAsync();
    self.Checker.Attestation = new CheckerAttestation(true, "@maker");
    var b = await self.Handler().HandleAsync(self.Completed(), self.Invocation(), self.Grant());
    Require(b.Status == SubagentReturnStatus.Rejected && b.ReasonCode == "checker_not_independent" &&
            self.Dispatcher.Calls == 0, "maker was accepted as its own checker");

    var unnamed = await Rig.CreateAsync();
    unnamed.Checker.Attestation = new CheckerAttestation(true, " ");
    var c = await unnamed.Handler().HandleAsync(unnamed.Completed(), unnamed.Invocation(), unnamed.Grant());
    Require(c.ReasonCode == "checker_not_independent", "anonymous checker was accepted");

    var failing = await Rig.CreateAsync();
    failing.Checker.Throw = true;
    var d = await failing.Handler().HandleAsync(failing.Completed(), failing.Invocation(), failing.Grant());
    Require(d.Status == SubagentReturnStatus.Deferred && d.ReasonCode == "checker_unavailable" &&
            failing.Dispatcher.Calls == 0, "checker outage did not defer");

    var notPassed = await Rig.CreateAsync();
    var e = await notPassed.Handler().HandleAsync(notPassed.Completed(verdict: "failed"), notPassed.Invocation(), notPassed.Grant());
    Require(e.Status == SubagentReturnStatus.Deferred && e.ReasonCode == "checker_not_passed" &&
            notPassed.Checker.Calls == 0, "unpassed child checker verdict was trusted");

    var unconfigured = await Rig.CreateAsync();
    var handler = unconfigured.Handler(checker: new UnconfiguredSubagentCheckerProof());
    var f = await handler.HandleAsync(unconfigured.Completed(), unconfigured.Invocation(), unconfigured.Grant());
    Require(f.ReasonCode == "checker_unverified" && unconfigured.Dispatcher.Calls == 0,
        "default checker proof accepted a return");
}

static async Task ContinuationAuthorization()
{
    var denied = await Rig.CreateAsync();
    denied.Policy.Disposition = PMCR.O.Continuation.AuthorizationDisposition.Denied;
    var a = await denied.Handler().HandleAsync(denied.Completed(), denied.Invocation(), denied.Grant());
    Require(a.Status == SubagentReturnStatus.Rejected && a.ReasonCode == "policy_denied" &&
            a.Continuation?.Status == ContinuationStatus.Rejected && denied.Dispatcher.Calls == 0,
        "policy denial still dispatched");

    var deferred = await Rig.CreateAsync();
    deferred.Policy.Disposition = PMCR.O.Continuation.AuthorizationDisposition.Deferred;
    var b = await deferred.Handler().HandleAsync(deferred.Completed(), deferred.Invocation(), deferred.Grant());
    Require(b.Status == SubagentReturnStatus.Deferred && deferred.Dispatcher.Calls == 0, "deferred policy dispatched");

    var inherited = await Rig.CreateAsync();
    var c = await inherited.Handler().HandleAsync(
        inherited.Completed(governance: new SeedGovernance("approved", true)), inherited.Invocation(), inherited.Grant());
    Require(c.Status == SubagentReturnStatus.Rejected && c.ReasonCode == "untrusted_authority" &&
            inherited.Policy.Calls == 0 && inherited.Dispatcher.Calls == 0,
        "child self-approval was honored");

    var noEvidence = await Rig.CreateAsync();
    noEvidence.ContinuationProof.Allowed = false;
    var d = await noEvidence.Handler().HandleAsync(noEvidence.Completed(), noEvidence.Invocation(), noEvidence.Grant());
    Require(d.Status == SubagentReturnStatus.Deferred && d.ReasonCode == "evidence_unverified" &&
            noEvidence.Dispatcher.Calls == 0, "unverified continuation evidence dispatched");

    var noSeed = await Rig.CreateAsync();
    var e = await noSeed.Handler().HandleAsync(noSeed.Completed(withSeed: false), noSeed.Invocation(), noSeed.Grant());
    Require(e.Status == SubagentReturnStatus.Completed && e.ReasonCode == "subagent_completed_no_continuation" &&
            noSeed.Policy.Calls == 0 && noSeed.Dispatcher.Calls == 0, "a return without a handoff was dispatched");

    var uncertain = await Rig.CreateAsync();
    uncertain.Dispatcher.Fail = true;
    var handler = uncertain.Handler();
    var f = await handler.HandleAsync(uncertain.Completed(), uncertain.Invocation(), uncertain.Grant());
    var retry = await handler.HandleAsync(uncertain.Completed(), uncertain.Invocation(), uncertain.Grant());
    Require(f.Status == SubagentReturnStatus.ExecutionUncertain && retry.Status == SubagentReturnStatus.Duplicate &&
            uncertain.Dispatcher.Calls == 1, "an uncertain dispatch was retried");
}

static async Task DisabledByDefault()
{
    var rig = await Rig.CreateAsync();
    var manifest = Rig.LoadManifest();
    Require(!manifest.Governance.EnabledByDefault && manifest.Governance.ApprovalRequired &&
            manifest.Governance.TrailRequired && manifest.Promotion == "generated",
        "shipped manifest is not disabled by default and unpromoted");
    Require(SubagentReturnManifestValidator.Validate(manifest).Count == 0, "shipped manifest is invalid");

    var handler = rig.Handler();
    var receipt = await handler.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant(enabled: false));
    Require(receipt.Status == SubagentReturnStatus.Deferred && receipt.ReasonCode == "plugin_disabled",
        "disabled plugin did not defer");
    Require(rig.Checker.Calls == 0 && rig.Policy.Calls == 0 && rig.Dispatcher.Calls == 0, "disabled plugin had side effects");
    var frames = await rig.Trails.ReadRunAsync(rig.Caller.RunId);
    Require(frames.Any(f => f.Phase == "subagent-return-decision" && f.EvidenceRefs.Contains("reason:plugin_disabled")),
        "disabled invocation was not recorded");

    // Deferral did not consume the claim, so an enabled retry still works.
    var enabled = await handler.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(enabled.Status == SubagentReturnStatus.Completed, "plugin could not complete after being enabled");
}

static async Task UnsafeManifest()
{
    var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "plugin.json"));
    var enabledByDefault = PluginManifest.Parse(text.Replace("\"enabledByDefault\": false", "\"enabledByDefault\": true"));
    Require(SubagentReturnManifestValidator.Validate(enabledByDefault).Contains("unsafe_plugin_governance"),
        "enabled-by-default manifest passed validation");

    var rig = await Rig.CreateAsync();
    var receipt = await rig.Handler(manifest: enabledByDefault).HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.Rejected && receipt.ReasonCode == "invalid_plugin_manifest" &&
            rig.Dispatcher.Calls == 0, "unsafe manifest was usable");

    var foreign = PluginManifest.Parse(text.Replace("\"artifactId\": \"pmcro.subagent-return\"", "\"artifactId\": \"pmcro.continuation\""));
    Require(SubagentReturnManifestValidator.Validate(foreign).Contains("invalid_plugin_identity"),
        "another plugin's identity passed validation");

    var noContract = PluginManifest.Parse(text.Replace("\"pmcro.subagent-return.v1\", ", ""));
    Require(SubagentReturnManifestValidator.Validate(noContract).Contains("missing_contract_versions"),
        "missing contract version passed validation");

    var ambiguous = PluginManifest.Parse(text.Replace("\"routes\": [", "\"routes\": [{\"path\":\"/pmcro:subagent-return\",\"operation\":\"x\",\"target\":\"@orchestrator\",\"policyRef\":\"pmcro.policy.subagent-return\",\"permissions\":[\"p\"]},"));
    Require(SubagentReturnManifestValidator.Validate(ambiguous).Contains("ambiguous_plugin_routes"),
        "ambiguous routes passed validation");
}

static async Task GrantAndRouteMismatch()
{
    var rig = await Rig.CreateAsync();
    var handler = rig.Handler();
    var wrongPolicy = rig.Grant() with { PolicyReference = "pmcro.policy.other" };
    Require((await handler.HandleAsync(rig.Completed(), rig.Invocation(), wrongPolicy)).ReasonCode == "invalid_plugin_grant",
        "grant for another policy was accepted");
    var wrongArtifact = rig.Grant() with { ArtifactId = "pmcro.continuation" };
    Require((await handler.HandleAsync(rig.Completed(), rig.Invocation(), wrongArtifact)).ReasonCode == "invalid_plugin_grant",
        "grant for another artifact was accepted");
    var noGrantor = rig.Grant() with { GrantedBy = "" };
    Require((await handler.HandleAsync(rig.Completed(), rig.Invocation(), noGrantor)).ReasonCode == "invalid_plugin_grant",
        "anonymous grant was accepted");
    var unregistered = rig.Grant(registered: false);
    Require((await handler.HandleAsync(rig.Completed(), rig.Invocation(), unregistered)).ReasonCode == "plugin_not_registered",
        "unregistered plugin was accepted");
    var wrongRoute = rig.Invocation() with { Route = "/pmcro:continue" };
    Require((await handler.HandleAsync(rig.Completed(), wrongRoute, rig.Grant())).ReasonCode == "unregistered_plugin_route",
        "undeclared route was accepted");
    Require(rig.Dispatcher.Calls == 0 && rig.Checker.Calls == 0, "a rejected grant had side effects");
}

static async Task ParentFrameMissing()
{
    var rig = await Rig.CreateAsync(includeParent: false);
    var receipt = await rig.Handler().HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.Rejected && receipt.ReasonCode == "parent_frame_missing" &&
            receipt.Attribution == SubagentFailureAttribution.Caller, "missing parent frame was not attributed to the caller");
    var frames = await rig.Trails.ReadRunAsync(rig.Caller.RunId);
    Require(frames.Any(f => f.EvidenceRefs.Contains("failure-attribution:caller")), "caller attribution not trailed");
    Require(rig.Checker.Calls == 0 && rig.Dispatcher.Calls == 0, "return progressed without a parent frame");
}

static async Task TrustedContextInvalid()
{
    var rig = await Rig.CreateAsync();
    var before = (await rig.Trails.ReadRunAsync(rig.Caller.RunId)).Count;
    var blank = rig.Invocation() with { SubagentRunId = " " };
    var receipt = await rig.Handler().HandleAsync(rig.Completed(), blank, rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.Rejected && receipt.ReasonCode == "trusted_context_invalid" &&
            receipt.TrailFrameId is null, "incomplete trusted context was usable");
    Require((await rig.Trails.ReadRunAsync(rig.Caller.RunId)).Count == before, "an unattributable frame was written");

    var blankChild = rig.Invocation() with { Caller = rig.Caller with { ExpectedChildAgent = "" } };
    var blankReceipt = await rig.Handler().HandleAsync(rig.Completed(), blankChild, rig.Grant());
    Require(blankReceipt.ReasonCode == "trusted_context_invalid", "a blank expected child matched a blank I AM");
}

static async Task RouterIntegration()
{
    var rig = await Rig.CreateAsync();
    var router = rig.Router();
    var okReply = await router.DispatchAsync(rig.Command());
    Require(okReply.Status == "completed" && okReply.Code == "SUBAGENT_RETURN_COMPLETED" &&
            okReply.Payload?.Status == SubagentReturnStatus.Completed && rig.Dispatcher.Calls == 1,
        "routed return did not complete");

    var repeat = await router.DispatchAsync(rig.Command());
    Require(repeat.Code == "DUPLICATE_COMMAND_COMPLETED" && rig.Dispatcher.Calls == 1,
        "native idempotency did not block a repeated return");

    var gated = await Rig.CreateAsync();
    gated.Gate.Disposition = ApprovalDisposition.RequireHumanApproval;
    var gatedReply = await gated.Router().DispatchAsync(gated.Command());
    Require(gatedReply.Status == "waiting_for_approval" && gated.Resolver.Calls == 0 && gated.Dispatcher.Calls == 0,
        "outer authorization did not stop the command before the plugin");

    var forged = await Rig.CreateAsync();
    var forgedReply = await forged.Router().DispatchAsync(forged.Command() with { CorrelationId = "forged" });
    Require(forgedReply.Code == "CALLER_BINDING_MISMATCH" && forged.Dispatcher.Calls == 0 && forged.Checker.Calls == 0,
        "forged command lineage reached the plugin");

    var unbound = await Rig.CreateAsync();
    unbound.Resolver.Binding = null;
    var unboundReply = await unbound.Router().DispatchAsync(unbound.Command());
    Require(unboundReply.Code == "TRUSTED_CONTEXT_MISSING" && unbound.Dispatcher.Calls == 0,
        "a command without a trusted binding was handled");

    var timedOut = await Rig.CreateAsync();
    timedOut.Resolver.Binding = new TrustedSubagentBinding(timedOut.Invocation("timed_out"), timedOut.Grant());
    var timedOutReply = await timedOut.Router().DispatchAsync(timedOut.Command());
    Require(timedOutReply.Status == "failed" && timedOutReply.Payload?.Status == SubagentReturnStatus.TimedOut &&
            timedOut.Dispatcher.Calls == 0, "host-observed timeout was not honored through the router");
}

static async Task RoleSpoofing()
{
    var rig = await Rig.CreateAsync();
    var good = rig.Completed();
    var fake = good with { IContext = good.IContext! with { Canonical = good.IContext.Canonical! with { Role = "orchestrator" } } };
    var result = await rig.Handler().HandleAsync(fake, rig.Invocation(), rig.Grant());
    Require(result.ReasonCode == "canonical_i_context_mismatch" && rig.Dispatcher.Calls == 0, "spoofed role accepted");
}

static async Task AuthorityWidening()
{
    var rig = await Rig.CreateAsync();
    var good = rig.Completed();
    var fake = good with { IContext = good.IContext! with { Canonical = good.IContext.Canonical! with { AuthorityCeilingRef = "admin" } } };
    var result = await rig.Handler().HandleAsync(fake, rig.Invocation(), rig.Grant());
    Require(result.ReasonCode == "canonical_i_context_mismatch" && rig.Dispatcher.Calls == 0, "expanded authority accepted");
}

static async Task TerminalMissing()
{
    var rig = await Rig.CreateAsync();
    var h = rig.Handler();
    var pending = await h.HandleAsync(null, rig.Invocation("pending"), rig.Grant());
    Require(pending.Status == SubagentReturnStatus.Deferred, "pending did not defer");
    var closed = await h.HandleAsync(null, rig.Invocation("terminal_missing"), rig.Grant());
    Require(closed.Status == SubagentReturnStatus.Failed && closed.ReasonCode == "host_observed_terminal_missing", "terminal missing not recorded");
    var replay = await h.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(replay.Status == SubagentReturnStatus.Duplicate && rig.Dispatcher.Calls == 0, "late replay accepted");
}

static async Task ForgedCheckerEvidence()
{
    var rig = await Rig.CreateAsync();
    rig.Checker.Attestation = new(true, "@checker", "forged-frame", "forged-digest");
    var result = await rig.Handler().HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(result.ReasonCode == "checker_evidence_unverified" && rig.Dispatcher.Calls == 0, "fake checker evidence accepted");
}

static async Task CheckerOutageRecovery()
{
    var rig = await Rig.CreateAsync();
    var handler = rig.Handler();
    rig.Checker.Throw = true;
    var blocked = await handler.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(blocked.Status == SubagentReturnStatus.Deferred && rig.Dispatcher.Calls == 0, "outage did not defer");
    rig.Checker.Throw = false;
    var resumed = await handler.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(resumed.Status == SubagentReturnStatus.Completed && rig.Dispatcher.Calls == 1, "verification could not resume");
    var replay = await handler.HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(replay.Status == SubagentReturnStatus.Duplicate && rig.Dispatcher.Calls == 1, "duplicate dispatch");
}

static async Task ConflictingRoles()
{
    var rig = await Rig.CreateAsync();
    var good = rig.Completed();
    var changed = good with { IContext = good.IContext! with { Role = "administrator" } };
    var receipt = await rig.Handler().HandleAsync(changed, rig.Invocation(), rig.Grant());
    Require(receipt.ReasonCode == "canonical_i_context_mismatch" && rig.Dispatcher.Calls == 0, "role conflict accepted");
}

static async Task ResultDigestMismatch()
{
    var rig = await Rig.CreateAsync();
    var message = rig.Completed();
    rig.Checker.Attestation = rig.Checker.Attestation! with { ResultDigest = "sha256:bad" };
    var result = await rig.Handler().HandleAsync(message, rig.Invocation(), rig.Grant());
    Require(result.ReasonCode == "checker_evidence_unverified" && rig.Dispatcher.Calls == 0, "mismatched result digest accepted");
}

static async Task UntrustedProofGate()
{
    var rig = await Rig.CreateAsync();
    var receipt = await rig.Handler(proofVerifier: new UnconfiguredCheckerEvidenceVerifier())
        .HandleAsync(rig.Completed(), rig.Invocation(), rig.Grant());
    Require(receipt.ReasonCode == "checker_proof_unverified" && rig.Dispatcher.Calls == 0, "unverified proof accepted");
}

static async Task DurableStateRestart()
{
    var file = Path.Combine(Path.GetTempPath(), "pmcro-return-" + Guid.NewGuid().ToString("N") + ".db");
    var first = new SqliteSubagentReturnStateStore(file);
    var created = await first.TryTransitionAsync("run-1", null, DurableReturnState.Received, "sha256:abc");
    Require(created?.Version == 1, "initial transition failed");
    var second = new SqliteSubagentReturnStateStore(file);
    var reopened = await second.ReadAsync("run-1");
    Require(reopened?.State == DurableReturnState.Received && reopened.Version == 1, "restart lost state");
    var updated = await second.TryTransitionAsync("run-1", 1, DurableReturnState.Verified, "sha256:abc");
    Require(updated?.Version == 2, "CAS verification failed");
    var stale = await first.TryTransitionAsync("run-1", 1, DurableReturnState.Dispatched, "sha256:abc");
    Require(stale is null, "stale version mutated state");
}

static async Task DurableStateCas()
{
    var file = Path.Combine(Path.GetTempPath(), "pmcro-return-" + Guid.NewGuid().ToString("N") + ".db");
    var store = new SqliteSubagentReturnStateStore(file);
    await store.TryTransitionAsync("run-2", null, DurableReturnState.Received, "sha256:def");
    var candidates = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
        new SqliteSubagentReturnStateStore(file).TryTransitionAsync("run-2", 1, DurableReturnState.Verified, "sha256:def")));
    Require(candidates.Count(x => x is not null) == 1, "multiple concurrent writers won");
}

static async Task SignedCheckerProof()
{
    var rig = await Rig.CreateAsync();
    var result = rig.Completed();
    var key = Enumerable.Range(0, 32).Select(x => (byte)(x + 17)).ToArray();
    var authority = new HmacCheckerProofAuthority(key, new HashSet<string>(StringComparer.Ordinal) { "@checker" });
    var digest = SubagentResultDigest.Compute(result.FinalResult!);
    var attestation = authority.IssueForAuthenticatedChecker(
        "@checker", "frame-checker", "sha256:test-proof", digest, rig.Caller.RunId, rig.Caller.CorrelationId);
    rig.Checker.Attestation = attestation;
    var receipt = await rig.Handler(proofVerifier: authority).HandleAsync(result, rig.Invocation(), rig.Grant());
    Require(receipt.Status == SubagentReturnStatus.Completed, "valid host-issued proof rejected");

    var forged = await Rig.CreateAsync();
    var forgery = forged.Completed();
    forged.Checker.Attestation = attestation with { Signature = "hmac-sha256:" + new string('0', 64) };
    var denied = await forged.Handler(proofVerifier: authority).HandleAsync(forgery, forged.Invocation(), forged.Grant());
    Require(denied.Status == SubagentReturnStatus.Deferred && forged.Dispatcher.Calls == 0, "forged signature accepted");
}

static async Task HashTrailIntegrity()
{
    var rig = await Rig.CreateAsync();
    var file = Path.Combine(Path.GetTempPath(), "pmcro-hashtrail-" + Guid.NewGuid().ToString("N") + ".db");
    var ledger = new SqliteHashChainedTrailLedger(file);
    var frames = await rig.Trails.ReadRunAsync(rig.Caller.RunId);
    string? head = null;
    foreach (var f in frames) head = await ledger.AppendAsync(f);
    Require(await new SqliteHashChainedTrailLedger(file).VerifyAsync(rig.Caller.RunId, head),
        "hash trail failed roundtrip after restart");
    using var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + file);
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = "UPDATE trail_ledger SET payload='tampered' WHERE seq=1";
    cmd.ExecuteNonQuery();
    Require(!await ledger.VerifyAsync(rig.Caller.RunId, head), "tampered payload was accepted");
}

static async Task DurableNoSeed()
{
    var rig = await Rig.CreateAsync();
    var file = Path.Combine(Path.GetTempPath(), "pmcro-return-" + Guid.NewGuid().ToString("N") + ".db");
    var states = new SqliteSubagentReturnStateStore(file);
    var message = rig.Completed(withSeed: false);
    var first = await rig.Handler(durableStates: states).HandleAsync(message, rig.Invocation(), rig.Grant());
    Require(first.Status == SubagentReturnStatus.Completed && rig.Dispatcher.Calls == 0,
        "durable no-seed completion was not closed");
    var later = await rig.Handler(durableStates: new SqliteSubagentReturnStateStore(file))
        .HandleAsync(message, rig.Invocation(), rig.Grant());
    Require(later.Status == SubagentReturnStatus.Duplicate, "durable closed run reopened after restart");
}

static async Task DurableSeedDeferred()
{
    var rig = await Rig.CreateAsync();
    var file = Path.Combine(Path.GetTempPath(), "pmcro-return-" + Guid.NewGuid().ToString("N") + ".db");
    var message = rig.Completed();
    var first = await rig.Handler(durableStates: new SqliteSubagentReturnStateStore(file))
        .HandleAsync(message, rig.Invocation(), rig.Grant());
    var second = await rig.Handler(durableStates: new SqliteSubagentReturnStateStore(file))
        .HandleAsync(message, rig.Invocation(), rig.Grant());
    var durable = await new SqliteSubagentReturnStateStore(file).ReadAsync(
        string.Concat(new[] { SubagentReturnSchemas.SubagentReturn, rig.Caller.AgentId,
            rig.Caller.IntentId, rig.Caller.RunId, Rig.SubagentRunId }
            .Select(v => v.Length + ":" + v)));
    Require(first.Status == SubagentReturnStatus.Deferred &&
        second.Status == SubagentReturnStatus.Deferred &&
        durable?.State == DurableReturnState.Deferred && rig.Dispatcher.Calls == 0,
        "durable unconfigured dispatch was invoked or could not recover");
}

static async Task OutboxReservation()
{
    var file = Path.Combine(Path.GetTempPath(), "pmcro-outbox-" + Guid.NewGuid().ToString("N") + ".db");
    var store = new SqliteContinuationOutbox(file);
    var start = await store.AdmitAsync("idempotency-1", "sha256:result", "seed-1");
    Require(start?.Status == DurableOutboxStatus.Deferred && start.Version == 1, "admission failed");
    var instances = Enumerable.Range(0, 8).Select(_ => new SqliteContinuationOutbox(file)).ToArray();
    var reservations = await Task.WhenAll(instances.Select(x => x.ReserveAsync("idempotency-1", 1)));
    Require(reservations.Count(x => x is not null) == 1, "more than one reservation won");
    var after = await new SqliteContinuationOutbox(file).ReadAsync("idempotency-1");
    Require(after?.Status == DurableOutboxStatus.Dispatching, "reservation not persisted");
    Require(await store.ReserveAsync("idempotency-1", after!.Version) is null, "reserved dispatch replayed");
}

static async Task OutboxReconciliation()
{
    var file = Path.Combine(Path.GetTempPath(), "pmcro-outbox-" + Guid.NewGuid().ToString("N") + ".db");
    var store = new SqliteContinuationOutbox(file);
    await store.AdmitAsync("idempotency-2", "sha256:result", "seed-2");
    var reservation = await store.ReserveAsync("idempotency-2", 1);
    Require(reservation is not null, "reservation failed");
    var restarted = new SqliteContinuationOutbox(file);
    var uncertain = await restarted.MarkUncertainAsync("idempotency-2", reservation!.Version);
    Require(uncertain?.Status == DurableOutboxStatus.ExecutionUncertain, "uncertain state not recorded");
    Require(await restarted.ReserveAsync("idempotency-2", uncertain!.Version) is null, "uncertain dispatch replayed");
    var resolved = await restarted.ReconcileAsync("idempotency-2", uncertain!.Version,
        "external-operation-001", dispatched: true);
    Require(resolved?.Status == DurableOutboxStatus.Dispatched &&
        resolved.ExternalOperationId == "external-operation-001", "external reconciliation failed");
}

static async Task TrustedAnchorForgery()
{
    var rig = await Rig.CreateAsync();
    var db = Path.Combine(Path.GetTempPath(), "pmcro-anchored-" + Guid.NewGuid().ToString("N") + ".db");
    var ledger = new SqliteHashChainedTrailLedger(db);
    var anchor = new InMemoryTrustedTrailHeadAnchor();
    var frames = await rig.Trails.ReadRunAsync(rig.Caller.RunId);
    string head = "";
    foreach (var frame in frames) head = await ledger.AppendAsync(frame);
    Require(await anchor.CompareAndSetAsync(rig.Caller.RunId, null, head), "trusted head not recorded");
    Require(await new TrustedTrailHeadVerifier(ledger, anchor).VerifyAsync(rig.Caller.RunId),
        "valid anchored chain rejected");
    Require(!await anchor.CompareAndSetAsync(rig.Caller.RunId, "forged", "sha256:fake"),
        "forged prior head changed trusted anchor");
    using var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + db);
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = "UPDATE trail_ledger SET head_hash='sha256:fake' WHERE seq=1";
    cmd.ExecuteNonQuery();
    Require(!await new TrustedTrailHeadVerifier(ledger, anchor).VerifyAsync(rig.Caller.RunId),
        "modified chain accepted against anchor");
}

static async Task DurablePolicyRecovery()
{
    var file = Path.Combine(Path.GetTempPath(), "pmcro-policy-" + Guid.NewGuid().ToString("N") + ".db");
    var outbox = new SqliteContinuationOutbox(file);
    var policy = new DurableCallerPolicyPreflight(outbox);
    var noApproval = await policy.EvaluateAndAdmitAsync("key-policy", "sha256:result",
        "seed-policy", () => Task.FromResult(false));
    Require(noApproval is null, "policy deferral was consumed");
    Require(await outbox.ReadAsync("key-policy") is null, "unapproved seed was admitted");
    var yesApproval = await policy.EvaluateAndAdmitAsync("key-policy", "sha256:result",
        "seed-policy", () => Task.FromResult(true));
    Require(yesApproval?.Status == DurableOutboxStatus.Deferred, "approved policy could not recover");
    var mismatch = await policy.EvaluateAndAdmitAsync("key-policy", "sha256:forged",
        "seed-policy", () => Task.FromResult(true));
    Require(mismatch is null, "conflicting result was accepted as same idempotency key");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class Rig
{
    public CallerContext Caller { get; } = new(
        "@orchestrator", "@maker", "intent-sr", "run-sr", "corr-sr", "cmd-sr",
        "frame-parent", "/pmcro:run", 0, 5, 2);
    public const string SubagentRunId = "subrun-001";

    public JsonlEnterpriseTrailStore Trails { get; } = new(
        Path.Combine(Path.GetTempPath(), "pmcro-subagent-return-" + Guid.NewGuid().ToString("N")));
    public FakeSubagentChecker Checker { get; } = new();
    public FakeContinuationProof ContinuationProof { get; } = new();
    public FakeContinuationPolicy Policy { get; } = new();
    public FakeDispatcher Dispatcher { get; } = new();
    public FakeOuterGate Gate { get; } = new();
    public FakeResolver Resolver { get; } = new();
    public CommandExecutionContext HostContext { get; } =
        new("org-test", "project-test", "@orchestrator", "policy.subagent-return");

    public static async Task<Rig> CreateAsync(bool includeParent = true)
    {
        var rig = new Rig();
        if (includeParent) await rig.Trails.AppendAsync(rig.Frame("frame-parent"));
        await rig.Trails.AppendAsync(rig.Frame("frame-source"));
        await rig.Trails.AppendAsync(rig.Frame("frame-checker") with { AgentId = "@checker", PayloadDigest = "sha256:test-proof", CheckerVerdict = "PASS" });
        rig.Resolver.Binding = new TrustedSubagentBinding(rig.Invocation(), rig.Grant());
        return rig;
    }

    /// <summary>A handler over the same Trail and fakes but with a fresh claim store.</summary>
    public static SubagentCompletionHandler Fresh(Rig rig) => rig.Handler();

    public static PluginManifest LoadManifest() =>
        PluginManifest.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "plugin.json")));

    public SubagentCompletionHandler Handler(
        PluginManifest? manifest = null,
        ISubagentCheckerProof? checker = null,
        ISeedClaimStore? claims = null,
        IAuthenticatedCheckerEvidenceVerifier? proofVerifier = null,
        SqliteSubagentReturnStateStore? durableStates = null)
    {
        var writer = new EnterpriseContinuationTrailWriter(Trails, HostContext);
        var verifier = new EnterpriseContinuationEvidenceVerifier(Trails, ContinuationProof);
        var continuation = new CallerContinuationHandler(
            verifier, Policy, new InMemorySeedClaimStore(), Dispatcher, writer);
        return new SubagentCompletionHandler(
            manifest ?? LoadManifest(), claims ?? new InMemorySeedClaimStore(), checker ?? Checker,
            continuation, new TrailFrameIntegration(Trails), new CallerFailureFrameFactory(HostContext),
            proofVerifier ?? new FixtureAuthenticatedCheckerVerifier(), durableStates);
    }

    public PmcroApplicationCommandRouter<SubagentReturnEnvelope, SubagentReturnReceipt> Router() =>
        SubagentReturnComposition.CreateRouter(Handler(), Resolver, Gate, Trails, HostContext);

    public PmcroCommand<SubagentReturnEnvelope> Command() => new(
        "1.0", "cmd-return-01", Caller.IntentId, Caller.RunId, Caller.CorrelationId, Caller.CommandId,
        "@maker", SubagentReturnSchemas.ReturnCommandType, DateTimeOffset.UtcNow, "native-return-key",
        Completed(), new Dictionary<string, string>());

    public CanonicalIContext Canonical() => new(
        "pmcro.i-context/v1", "@maker", "@maker", "maker", "bounded-implementation",
        Caller.IntentId, Caller.RunId, Caller.AgentId, Array.Empty<string>(),
        Caller.AgentId, "scope:read-only", 1, false);

    public TrustedSubagentInvocation Invocation(string? hostObserved = null) =>
        new(Caller, SubagentRunId, "/pmcro:subagent-return", hostObserved, Canonical());

    public PluginActivationGrant Grant(bool enabled = true, bool registered = true) =>
        new("pmcro.subagent-return", "0.1.0", "pmcro.policy.subagent-return", "@host", registered, enabled);

    public IAmContext IAm() => new(
        "@maker", "maker", Caller.IntentId, Caller.RunId, Caller.CorrelationId,
        Caller.CommandId, Caller.ParentFrameId, SubagentRunId, Canonical());

    public SubagentReturnEnvelope NonCompleted(string state) => new(
        SubagentReturnSchemas.SubagentReturn, SubagentReturnSchemas.MessageType, IAm(), state,
        null, Caller.AgentId, Caller.ExpectedReturnRoute, null);

    public SubagentReturnEnvelope Completed(
        bool withSeed = true,
        string verdict = "passed",
        SeedGovernance? governance = null)
    {
        NextSeedEnvelope? seed = withSeed
            ? new NextSeedEnvelope(
                PmcrSchemas.NextSeed, "seed-002", "frame-source",
                new NextSeedCommand("@planner", "/pmcro:plan", "implement_next_increment"),
                new PMCR.O.Continuation.SeedIntent("Activate PMCR-O", "Integrate governed command routing"),
                governance ?? new SeedGovernance("pending_policy_evaluation", false),
                new SeedExecution("seed-002", 1, 5, 2), "proposed")
            : null;
        var final = new CycleResultEnvelope(
            PmcrSchemas.CycleResult, "cycle.result", "completed",
            new CycleRouting("@maker", "@orchestrator", "@orchestrator", "/pmcro:run"),
            new CorrelationContext(Caller.IntentId, Caller.RunId, Caller.CorrelationId, Caller.CommandId, Caller.ParentFrameId),
            new PMCR.O.Continuation.CycleOutcome("reflect", "completed",
                new CycleEvidence(["frame-parent", "frame-source"], verdict)),
            seed, true, "@maker", "next_seed.acceptance");
        Checker.Attestation = Checker.Attestation is null ? null
            : Checker.Attestation with { ResultDigest = SubagentResultDigest.Compute(final) };
        return new SubagentReturnEnvelope(
            SubagentReturnSchemas.SubagentReturn, SubagentReturnSchemas.MessageType, IAm(),
            "completed", null, Caller.AgentId, Caller.ExpectedReturnRoute, final);
    }

    private EnterpriseTrailFrame Frame(string id) => new(
        SchemaVersion: "1.0", OrganizationId: "org-test", ProjectId: "project-test",
        IntentId: Caller.IntentId, RunId: Caller.RunId, AgentId: "@maker",
        CorrelationId: Caller.CorrelationId, FrameId: id, Sequence: 1,
        ParentFrameId: null, CausalFrameIds: [], Timestamp: DateTimeOffset.UtcNow,
        OriginalSeed: "Activate PMCR-O", RefinedObjective: "Delegate and return",
        Constraints: [], NextSeed: null, Phase: "reflect", Capability: "pmcro.reflect",
        Action: "cycle.reflect", Status: "completed", EvidenceRefs: ["fixture:" + id],
        PolicyId: "policy.test", AuthorizationDecision: "not_applicable", ApprovalRef: null,
        CheckerVerdict: "NOT_CHECKED", Reflection: "Fixture: no independent approval implied",
        Corrections: [], ObjectiveComplete: false, ProposedLesson: null,
        LearningStatus: "observation", Confidence: null, Provenance: "test-fixture",
        ReuseEligible: false, PayloadDigest: null);
}

sealed class FixtureAuthenticatedCheckerVerifier : IAuthenticatedCheckerEvidenceVerifier
{
    public Task<bool> VerifyAsync(CheckerAttestation a, SubagentReturnEnvelope r,
        TrustedSubagentInvocation i, EnterpriseTrailFrame frame, CancellationToken ct) =>
        Task.FromResult(frame.CheckerVerdict == "PASS" &&
            frame.FrameId == "frame-checker" && frame.Provenance == "test-fixture" &&
            a.CheckerAgentId == "@checker" &&
            a.ResultDigest == SubagentResultDigest.Compute(r.FinalResult!));
}

sealed class FakeSubagentChecker : ISubagentCheckerProof
{
    public CheckerAttestation? Attestation { get; set; } = new(true, "@checker", "frame-checker", "sha256:test-proof");
    public bool Throw { get; set; }
    public int Calls { get; private set; }

    public Task<CheckerAttestation?> VerifyAsync(
        SubagentReturnEnvelope returned, TrustedSubagentInvocation invocation, CancellationToken cancellationToken)
    {
        Calls++;
        if (Throw) throw new InvalidOperationException("checker outage");
        return Task.FromResult(Attestation);
    }
}

sealed class FakeContinuationProof : IIndependentContinuationCheckerProof
{
    public bool Allowed { get; set; } = true;
    public Task<bool> VerifyAsync(
        CycleResultEnvelope result, CallerContext caller,
        IReadOnlyList<EnterpriseTrailFrame> correlatedFrames, CancellationToken cancellationToken) =>
        Task.FromResult(Allowed);
}

sealed class FakeContinuationPolicy : IContinuationPolicy
{
    public PMCR.O.Continuation.AuthorizationDisposition Disposition { get; set; } =
        PMCR.O.Continuation.AuthorizationDisposition.Approved;
    public int Calls { get; private set; }

    public Task<PMCR.O.Continuation.AuthorizationDecision> EvaluateAsync(
        NextSeedEnvelope seed, CallerContext caller, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new PMCR.O.Continuation.AuthorizationDecision(Disposition, "test-only-decision"));
    }
}

sealed class FakeDispatcher : INextSeedDispatcher
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

sealed class FakeOuterGate : ICommandAuthorizationGate<SubagentReturnEnvelope>
{
    public ApprovalDisposition Disposition { get; set; } = ApprovalDisposition.Allow;
    public Task<ApprovalDecision> AuthorizeAsync(
        PmcroCommand<SubagentReturnEnvelope> command, CancellationToken cancellationToken) =>
        Task.FromResult(new ApprovalDecision(Disposition, "test-policy", "fixture"));
}

sealed class FakeResolver : ITrustedSubagentInvocationResolver
{
    public TrustedSubagentBinding? Binding { get; set; }
    public int Calls { get; private set; }

    public Task<TrustedSubagentBinding?> ResolveAsync(
        PmcroCommand<SubagentReturnEnvelope> command, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Binding);
    }
}
