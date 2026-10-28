# PMCR-O make plan: continuation plugin packaging

Approved command: `@maker /pmcro:make package_continuation_as_plugin`.
Additional user requirement: preserve the marketplace ecosystem previously
discussed in the session without conflating discovery and execution.

Scope: declarative package and reference host adapter on
`feat/pmcro-activation`. This is not a production plugin installation.

Named file targets:

- `plugins/pmcro.continuation/plugin.json`
- `schemas/pmcro-plugin.schema.json`
- `manifests/marketplace-sources.json`
- `src/PMCR.O.Continuation.Plugin/PMCR.O.Continuation.Plugin.csproj`
- `src/PMCR.O.Continuation.Plugin/PluginManifest.cs`
- `src/PMCR.O.Continuation.Plugin/ContinuationPlugin.cs`
- `tests/PMCR.O.Continuation.Tests/PMCR.O.Continuation.Tests.csproj`
- `tests/PMCR.O.Continuation.Tests/Program.cs`
- `scripts/validate.ps1`
- `docs/architecture/continuation-plugin.md`
- `docs/ecosystem/marketplace-catalog.md`
- `docs/decisions/0003-continuation-plugin-make-plan.md`
- `.pmcro/trails/orchestrator/2026-10-28-continuation-plugin-make.json`

Proof: verify JSON and project XML parse; check manifest is disabled by default,
routes are unique and explicit, grants are host-supplied, denied calls trail,
and the underlying caller policy remains mandatory. Run the 20-case harness
with the provided PowerShell validator when the SDK is available. A separate
checker must decide promotion; the maker must not self-certify.
