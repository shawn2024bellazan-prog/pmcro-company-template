# PMCR-O next-seed caller return contract

Status: standalone contract implementation on a feature branch; not connected to an application runtime.

## Message boundary

A child agent returns a `CycleResultEnvelope` to its **calling** agent. It may include
a `NextSeedEnvelope` with state `proposed`. The caller supplies a trusted
`CallerContext`, rather than trusting addresses and IDs echoed by the child.

The implementation uses these internal version identifiers:

- `pmcro.cycle-result.v1` for the complete return message;
- `pmcro.next-seed.v1` for the proposed continuation.

Adapters may translate this contract into A2A, MCP, HTTP, or chat syntax. These
strings are PMCR-O application conventions, not external protocol standards.

## Fail-closed caller decision

`CallerContinuationHandler`:

1. Verifies the sender, calling-agent return address, route, response target,
   original intent, run, correlation, causation, and parent frame IDs.
2. Rejects a missing source frame, malformed route, inherited authorization,
   or an inconsistent recursion/retry budget.
3. Defers an unpassed checker verdict and asks an independent evidence verifier
   to validate the Trail evidence against an authoritative source.
4. Consults the **caller's** current policy. A child's approval field never
   grants execution rights.
5. Claims a scoped seed/idempotency key atomically before dispatch. The supplied
   `InMemorySeedClaimStore` is a single-process reference only.
6. Writes a decision Trail record **before** invoking the dispatcher. A failed
   Trail write prevents dispatch.
7. Returns a correlated receipt and records the outcome. If dispatch throws or
   is cancelled after starting, the outcome is `ExecutionUncertain`; the claim
   remains held for explicit reconciliation, never automatic same-key retry.

The stable outcomes are `NoContinuation`, `Dispatched`, `Deferred`, `Rejected`,
`Duplicate`, and `ExecutionUncertain`. Error codes do not expose dispatcher
exception messages. Trail storage failure propagates as a failure and does not
silently report a successful dispatch.

## Required production adapters

These are interfaces, **not working external connections**:

- `IContinuationEvidenceVerifier`: trust boundary for frame existence, integrity,
  checker identity, tenant scope, and checker verdict.
- `IContinuationPolicy`: current actor, tenant, capability, permission, and
  human-approval policy evaluation.
- `ISeedClaimStore`: replace the example with a shared atomic **durable** store.
- `INextSeedDispatcher`: route to registered skills/agents using authenticated
  protocol adapters and enforce timeouts and cancellation.
- `IContinuationTrailWriter`: durable access-controlled Trail Frame recording,
  integrity, redaction, and causal lineage.

Callers must supply bounded storage and timeouts. A Trail write following an
external side effect can fail; reconciliation must preserve that uncertainty.
Do not treat a report of `Dispatched` as proof that an external workflow
completed successfully.

## Checks

Run `pwsh -NoProfile -File scripts/validate.ps1` with PowerShell and .NET 10.
The script builds the console contract test project and runs its 13 checks
without third-party test packages.

This increment does **not** publish, merge, deploy, or register a skill. It also
does not claim an independent checker verdict. Full runtime integration awaits
the cumulative PMCR-O source tree.
