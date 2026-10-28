# 0006: Subagent return plugin (`pmcro.subagent-return.v1`)

Status: proposed, on feature branch `feat/pmcro-subagent-return-v1`. Not merged, activated or promoted.

## Context

`CallerContinuationHandler` governs what a caller does with a child's proposed next seed.
It does not define how a delegated subagent terminates: there was no contract for a child
that is interrupted, times out, or returns without a final result, and no rule that a
caller must record such outcomes itself.

## Decision

Add a versioned plugin, `pmcro.subagent-return` (disabled by default), that requires
an I AM context, a correlated terminal result **or** a caller-owned failure frame, and
parent return routing. It composes the existing continuation handler, command router and
Enterprise Trail store rather than reimplementing them.

## Build versus reuse (per the repository's reuse policy)

| Need | Decision |
| --- | --- |
| Continuation, policy, claims, dispatch | Reuse `CallerContinuationHandler` unchanged. |
| Command routing, authorization, idempotency | Reuse `PmcroApplicationCommandRouter`. |
| Trail persistence and hash linkage | Reuse `IEnterpriseTrailStore` and its validator. |
| Plugin grant and manifest shape | Reuse `PluginManifest`, `PluginRoute` and `PluginActivationGrant`. |
| Manifest governance validation | **New** `SubagentReturnManifestValidator`: `PluginManifest.Validate` is pinned to the `pmcro.continuation` identity and was left unmodified. |
| Terminal return envelope, validator, caller failure frames | **New**: no existing equivalent. |
| New third-party dependencies | None. |

## Consequences

- Existing assemblies are unchanged; the new project and tests are additive.
- The shared `ci.yml` and `scripts/validate.ps1` gain a step for the new test project.
- A terminal return is consumed once per subagent run. A deferred decision (for example an
  unavailable checker) therefore needs a new reconciliation, not an automatic retry, which
  matches the existing "never auto-retry an uncertain outcome" rule.
- Production adapters (durable claims, a real independent checker, host resolvers) remain
  future work. Until then the plugin cannot accept a completed return.
