# ADR 0005: Governed continuation application integration

Decision: import the exact user-supplied cumulative Increments 1-19 source
into the existing feature branch and add a separate host-specific continuation
adapter, preserving the already checked standalone plugin code.

Why: Core already has a typed command/message router, authorization gate,
Trail storage, skill routes and idempotency. Continuation should reuse those
services instead of introducing a parallel ungoverned command path.

Planned files:

- Cumulative source: `PMCR-O.sln`, `global.json`, `Directory.Build.props`,
  `AGENTS.md`, `src/PMCR.O.Core/`, `src/PMCR.O.Demo/`, imported manifests,
  schemas, docs, and `.github/workflows/ci.yml`.
- Host adapter: `src/PMCR.O.Continuation.Integration/`.
- Integration tests: `tests/PMCR.O.Continuation.Integration.Tests/`.
- Governance catalog: `manifests/capability-registrations/examples.json`,
  `manifests/skill-routes/catalog.json` (disabled by default).
- Combined validation: `PMCR-O.sln`, `.github/workflows/ci.yml`,
  `scripts/validate.ps1`.
- Architecture and make record: this ADR,
  `docs/architecture/continuation-application-integration.md`, and the
  dated `.pmcro/trails/orchestrator/` maker record.

Acceptance proof: archive SHA and integrity; no accidental overwrites of the
portable continuation projects; valid JSON and solution project references;
all Core, continuation, and integration test harnesses passing in GitHub
Actions with .NET 10; disabled route and plugin governance retained;
independent semantic review explicitly pending.

Non-goals: automatic plugin installation, live A2A/MCP/marketplace adapters,
distributed exactly-once execution, secret/permission expansion, CI approval
as independent checker approval, merge, production deployment or promotion.

No native runtime intent/run/correlation IDs have been invented for this
development command. The make evidence identifier is explicitly derived.
