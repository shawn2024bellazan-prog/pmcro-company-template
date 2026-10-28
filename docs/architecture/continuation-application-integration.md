# Governed continuation inside the PMCR-O application

Status: candidate integration on `feat/pmcro-activation`. No automatic activation,
production approval, independent checker verdict, installation or deployment is claimed.

## Source lineage

The cumulative application was imported from the user-provided
`pmcro-activation-kit-increments-1-to-19.zip`, SHA-256:
`003bb927a7f7e2a4128e907cffcbbbd7c27b0cadda71a99b7dd9040c10f5bcce`.
All 19 inner archives passed CRC checks. The 84 source files in Increment 19
matched their counterparts in the cumulative project; two local activation
guides were present only in the cumulative folder.

This archive is user-supplied, not cryptographically attested by a trusted
publisher. Neither archived CI claims nor model-generated Trail metadata
count as executed proof in this repository.

## Integration boundary

The portable plugin remains in `PMCR.O.Continuation.Plugin`, without a
dependency on the host's Core. The optional
`PMCR.O.Continuation.Integration` project references both assemblies and
implements the existing Core command-handler contract.

1. A native `PmcroCommand<CycleResultEnvelope>` reaches
   `PmcroApplicationCommandRouter` using command type
   `pmcro.continuation.return`.
2. The native router validates its envelope, calls its authorization gate,
   and claims command idempotency before invoking the adapter.
3. `ITrustedContinuationInvocationResolver` must resolve the *original*
   `CallerContext` and `PluginActivationGrant` from authenticated runtime
   state, never from the child message or untrusted command context.
4. `ApplicationContinuationHandler` compares the native intent/run/
   correlation/causation IDs to that trusted context, and calls the plugin.
5. The plugin verifies registration, enablement, route, policy reference and
   grant version. The caller handler then checks return route, source frames,
   independent checker proof, policy, recursion bounds and seed claims.
6. An accepted and authorized seed may reach the injected trusted dispatcher.
   The adapter never serializes a proposed seed to Core's legacy `NextSeed`
   string as if that string granted execution authority.
7. Decisions are written to `IEnterpriseTrailStore`. Integration decision
   frames explicitly say `CheckerVerdict: NOT_CHECKED`; the native router's
   generic `PASS` on successful command lifecycle events is not considered
   evidence of an independent semantic checker.

`EnterpriseContinuationEvidenceVerifier` correlates real parent/source frame
IDs against the runtime Trail store. Its `IIndependentContinuationCheckerProof`
dependency is mandatory. The supplied `UnconfiguredContinuationCheckerProof`
returns false; integration tests use an explicit fake to exercise the positive
path, which is **not production checker evidence**.

Both `manifests/capability-registrations/examples.json` and
`manifests/skill-routes/catalog.json` describe the continuation capability as
discovered/disabled. No host activation grant or dispatcher is configured by
those manifests. A candidate route is not an executable authorization.

## Known runtime limits

- The imported Core's JSONL trail and in-memory idempotency stores are local,
  not durable distributed infrastructure. The host must supply a trusted,
  tenant-scoped checker proof, atomic claims and post-timeout reconciliation.
- Core completes its outer command idempotency claim even when a continuation
  is deferred. After external approval, a caller must issue a distinct
  authorized native command with appropriate lineage/key; reusing the old
  command key does not silently retry.
- `ITrustedContinuationInvocationResolver` has no production implementation in
  this package. Every actual client transport must bind its own authenticated
  identity and persisted parent-frame context.
- The plugin's internal claim store in the reference tests is in-memory.
  Uncertain dispatch outcomes remain claimed and require reconciliation.
- The core Trail writer may mark successful *operations* PASS. Only the
  independent checker proof service can validate a semantic checker verdict.

## Checks

```bash
dotnet build PMCR-O.sln --configuration Release
dotnet run --project tests/PMCR.O.Core.Tests --configuration Release --no-build
dotnet run --project tests/PMCR.O.Continuation.Tests --configuration Release --no-build
dotnet run --project tests/PMCR.O.Continuation.Integration.Tests --configuration Release --no-build
```

The combined `.github/workflows/ci.yml` runs these and the demo smoke test
using the .NET 10 SDK. The PowerShell `scripts/validate.ps1` runs the same
three test harnesses. Passing CI is a prerequisite, not self-promotion.

Next gate: an independently assigned checker must inspect code, provenance,
real branch CI, contract coverage, and the missing production adapters before
any plugin or application promotion.
