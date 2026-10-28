# Continuation capability as a PMCR-O plugin

The `pmcro.continuation` plugin wraps the standalone continuation contracts and
caller-side handler without modifying their governance boundary.

Plugin package: `plugins/pmcro.continuation/plugin.json`.
Entrypoint: `PMCR.O.Continuation.Plugin.ContinuationPlugin`.

## Discovery and invocation

1. The host loads the manifest and checks its JSON shape and semantic validity.
2. Registry discovery exposes the declared `/pmcro:next-seed` and
   `/pmcro:continue` routes. Discovery is not activation.
3. The plugin starts **disabled**. The authenticated host must produce a
   trusted `PluginActivationGrant` from registry state and the current policy.
4. The plugin checks registration, version, route, enablement, and policy
   reference. Failures produce a correlated Trail decision and no dispatch.
5. Only then does `CallerContinuationHandler` validate caller lineage,
   independent evidence, authorization, recursion and idempotency before
   invoking its dispatcher.

The plugin does not create grants, install external tools, or automatically
dispatch a `nextSeed` received from the marketplace or a child agent. Runtime
adapters must never deserialize a trusted grant from an untrusted agent payload.

## Required host adapters

- Registry/loader: maps stable plugin ID and version to a trusted activation
  grant, with tenant and actor scope.
- Policy: resolves `pmcro.policy.continuation` against live permissions and
  human approval requirements.
- Evidence verifier: validates genuine Enterprise Trail Frames and checker
  identity independently of model-generated claims.
- Trail writer: durable, redacted and access-controlled events for registration,
  disabled/denied invocation and command execution.
- Dispatcher and claim store: authenticated skill/agent route, deadline,
  cancellation, reconciliation and durable atomic idempotency claims.

The host and transport adapters are **not implemented** in this increment.
The plugin is a candidate package on a feature branch, not a live installation.

## Reuse and package validation

The plugin reuses `PMCR.O.Continuation` rather than copying its contracts.
No new `SKILL.md` is created because this artifact is a plugin, not an
installable standalone skill. A future nested skill must include its own
agent metadata and independent evaluation requirements.

The reference test harness now contains 20 checks, including plugin manifest,
disabled-by-default, route resolution, registration and policy-bypass attempts.
`scripts/validate.ps1` parses the manifests and builds/runs the test harness
when PowerShell and .NET 10 are available.

Promotion remains `generated` until compilation, contract tests, an independent
checker and policy approval succeed. The manifest itself grants no authority.
