# PMCR-O Company Template

A bounded cognitive-loop foundation: **Plan → Make → Check → Reflect → continue, retry, or escalate**.

## Continuation application integration (candidate)

This branch adds a separate, governed `PMCR.O.Continuation.Integration`
assembly linking the native application command router to the portable
`pmcro.continuation` plugin. The plugin and its catalog route are disabled by
default. Runtime caller context, checker proof, policy and activation grants
must be supplied by authenticated host services; neither a child agent nor a
manifest can grant its own continuation. See
`docs/architecture/continuation-application-integration.md`.

## Current increment
This scaffold establishes the loop contracts, conservative approval policy, ordered JSONL Trail Frames, a persistent local Seed Intent queue, queue lifecycle rules, demo adapters, contract tests, and GitHub Actions CI. The queue preserves the raw user message separately from any refined objective. A sequential local queue worker now refines and executes eligible intents, parks ambiguous requests for clarification, enforces a pre-Maker approval-policy gate, parks approval-required work, and only records completion after independent verification. The worker is not yet a distributed/restart-safe broker consumer. MAF, Aspire, Python worker, GitHub marketplace, and client-specific plugin adapters remain subsequent increments—not implemented claims.

## Run
Requires .NET 10 SDK:

```bash
dotnet build PMCR-O.sln
dotnet run --project tests/PMCR.O.Core.Tests
dotnet run --project src/PMCR.O.Demo
```

## Autonomy and safety
The Orchestrator owns cycle continuation. It may retry recoverable failures or continue toward an incomplete objective within explicit budgets. Auto-continuation does not grant permission to mutate repositories, install plugins, execute untrusted code, disclose secrets, or expand privileges. Unknown actions require review. Messy or incomplete messages should be enqueued verbatim and remain non-terminal until completion, cancellation, or escalation. See `docs/architecture/intent-queue.md`, `docs/architecture/queue-worker.md`, `AGENTS.md`, and `docs/architecture/approval-and-autonomy.md`.

## Verification status
Static checks passed for project XML, solution project paths, queue lifecycle markers, and test registration. The source files are **not compiled in this environment** because the .NET SDK is unavailable. CI is configured for push, pull-request, and manual (`workflow_dispatch`) runs, but has not been triggered because GitHub write access is unavailable.

## Enterprise ecosystem foundation (Increment 5)

This increment establishes a portable Enterprise Trail Frame schema, application-level validation, recursive self-improvement guard, and a product portfolio map. See:

- `schemas/trail-frame.schema.json`
- `docs/architecture/trail-frame-platform.md`
- `docs/architecture/recursive-cycle-contract.md`
- `docs/ecosystem/product-map.md`
- `src/PMCR.O.Core/EnterpriseTrailFrames.cs`

The recursive guard rejects exhausted budgets, repeated self-targets, missing rollback/snapshot plans, and missing independent verification; production or security/permission changes require explicit approval. The local JSONL store is still a development adapter, not a tamper-proof enterprise ledger. Build and runtime validation require a .NET 10 SDK; this environment currently does not have `dotnet` installed.

## Declarative, template-driven enterprise design (Increment 6)

The platform is being organized around a generic manifest envelope and reusable templates—not per-product hard-coded configuration. Starter examples live in `manifests/examples/`, the shared envelope schema is `manifests/schemas/component-manifest.schema.json`, and template rules are documented in `templates/README.md` and `docs/architecture/declarative-enterprise-design.md`.

The core now includes a dependency-free JSON envelope parser/validator for supported manifest kinds. It rejects unknown kinds, malformed JSON, missing metadata, invalid names, malformed policy references, and duplicate policy references. This is the first validation layer only: YAML host parsing, kind-specific semantic validation, version-pinned inheritance resolution, capability binding, and runtime policy enforcement remain subsequent work. Declarative data never grants itself permission; trusted runtime policy remains authoritative.

## Engineering defaults

- **Declarative first:** products, agents, workflows, and enterprise profiles are configured from versioned manifests and reusable templates.
- **Reuse before custom:** evaluate existing libraries, platform capabilities, standard protocols, and repository code before writing replacements. See [Dependency Reuse Policy](docs/architecture/dependency-reuse-policy.md) and [ADR 0001](docs/decisions/0001-reuse-before-custom.md).
- **Enterprise by design:** policy enforcement, least privilege, tenant boundaries, provenance, audit trails, observability, retention, and recovery are runtime responsibilities—not optional prompt instructions.
- **Verify before release:** CI validates manifests, builds, runs contract tests, and smoke-tests the demo. External dependency security/license scanning and SBOM generation should be added to the release gate as the chosen tooling is integrated.


## Protocol interoperability (A2A, MCP, and service APIs)

PMCR-O follows a standard-protocol-first approach. The declarative component manifest supports protocol declarations for A2A, MCP, OpenAPI, CloudEvents, and HTTP+JSON. An example is in `manifests/examples/a2a-agent.yaml`; adapter and security requirements are in `docs/architecture/interoperability-protocols.md`. These are contract/design foundations, not a claim that live A2A or MCP network adapters have already been implemented. Runtime adapters must reuse maintained SDKs and pass policy checks before every governed action.


## Marketplace and capability registry (Increment 9)

Increment 9 introduces normalized source/capability contracts, a declarative source catalog, capability registration schema, and a policy-gated invocation boundary. It is designed to reuse existing marketplaces and protocols rather than replace them. See `docs/architecture/marketplace-capability-integration.md`, `docs/decisions/0002-unified-capability-registry.md`, `manifests/marketplace-sources/catalog.json`, and `schemas/capability.schema.json`.

**Status honesty:** the catalog contains source references and adapter boundaries, not live marketplace connections. No external listing fetch, package installation, MCP/A2A transport, signature verification, vulnerability scanning, or remote invocation adapter is implemented yet. `CapabilityStatus.Discovered` and `Available` never imply executable or approved. Validated-or-later registrations require digest and validation provenance; execution is denied unless status is `Executable`, a matching invoker exists, and runtime policy returns `Allow`.

### Enterprise Trail runtime

Increment 10 adds a JSONL enterprise Trail Frame store with per-run sequence assignment, parent/causal linkage, and SHA-256 hash-chain verification, plus an optional capability-execution recorder. When injected into `GovernedCapabilityExecutor`, it records policy blocks, validation/adapter failures, execution start, and execution outcome. This is tamper-evident local persistence, not immutable or distributed storage. See `docs/architecture/enterprise-trail-runtime.md` for production requirements and limitations.


## Increment 11 — persistent registry and command/message contracts

Increment 11 adds a local JSON capability registry store with validation and atomic snapshot replacement, plus protocol-neutral command/message envelopes that preserve intent/run/correlation/causation and Trail Frame references. See `docs/architecture/command-message-trail-contract.md`. The local store is single-process and does not fetch or execute remote capabilities.

## Application-wide command routing (Increment 12)

All clients should normalize requests into the shared `PmcroCommand<TPayload>` envelope and dispatch through `PmcroApplicationCommandRouter<TPayload,TResult>`. The router validates correlation/idempotency fields, checks the authorization gate before execution, and writes Enterprise Trail Frames for denied, started, completed, failed, and cancelled operations. See `docs/architecture/application-wide-command-routing.md`.

The JSONL trail store is a local single-process implementation with hash-chain tamper evidence; it is not immutable or distributed storage. Protocol adapters must preserve native A2A/MCP/HTTP semantics while mapping to the shared PMCR-O contract.


## Increment 13 — Skill routing and adapter conformance

Adds a declarative skill-route contract, fail-closed route resolver, local JSON catalog implementation, JSON Schema, and adapter conformance requirements. The example route is disabled by default; resolving a route never bypasses policy authorization. See `docs/architecture/skill-routing-and-adapter-conformance.md`.

## Increment 14 — Command idempotency and duplicate suppression

The application router now claims an idempotency scope before handler execution, blocks repeated/in-flight command keys, traces duplicate decisions, and releases claims for approval-required requests or cancellation. A handler cannot request approval after dispatch has begun. See [Command Idempotency](docs/architecture/command-idempotency.md).

**Runtime limitation:** the default idempotency store is thread-safe but process-local and non-durable. It does not provide distributed exactly-once effects; production must inject a shared durable implementation and use downstream idempotency where available. A failed handler is conservatively blocked from same-key retry until an operator checks evidence, because its side effects may be uncertain.

### Increment 15 — Skill dispatch and route-resolution trails

The application command-handler contract now has a skill-routed adapter. It resolves a single enabled declarative route, writes a dedicated Enterprise Trail Frame for both successful and blocked route decisions, and only then delegates to the configured skill dispatcher. Missing, disabled, ambiguous, or malformed routes fail closed. The dispatcher remains responsible for runtime permission and policy enforcement before any skill side effect; a route's manifest metadata is not itself authorization.

This is an adapter boundary with a test fake, not a live connection to a marketplace, VS Code, MCP registry, or A2A agent.

## Adapter conformance

- Adapter conformance: see `docs/architecture/adapter-conformance.md`.


### Increment 17 — Execution boundaries & operational readiness

- `ExecutionBoundary` wraps calls with finite deadlines and explicitly marks timeout, cancellation, and failure outcomes as potentially unknown.
- `RetrySafetyPolicy` avoids automatic retries for uncertain non-idempotent side effects and permanent failures.
- `TrailEvidenceSanitizer` provides best-effort redaction and truncation for common secret-bearing evidence strings.
- See `docs/architecture/execution-boundaries-and-evidence-hygiene.md` for limitations and production readiness checklist.
- Tests were added for successful bounded execution, timeout semantics, retry classification, and evidence redaction. They must be run in CI with .NET 10; local compilation is not claimed unless verified.

### Increment 18 — Declarative execution policy

- Added `ExecutionPolicyManifest` and validation for finite timeouts, bounded retries/backoff, evidence-size limits, required redaction fields, and mandatory reconciliation for unknown non-idempotent outcomes.
- Added `manifests/execution/default-policy.json` and `schemas/execution-policy.schema.json`.
- Added contract tests for safe defaults and rejected unsafe policy values.
- These are declarative controls: hosts must load, bind, and enforce them at runtime. The sample is not automatically wired into every adapter.


## Increment 19 — CI activation on push, PR, and manual run

The GitHub Actions workflow in `.github/workflows/ci.yml` now explicitly runs on pushes to branches, pull-request updates, manual `workflow_dispatch`, reusable `workflow_call`, and a weekly scheduled drift check. It validates JSON/YAML syntax, restores and builds the .NET 10 solution, runs the contract-test executable, and smoke-tests the demo. The manual `run_demo` input can skip only the demo step.

See [CI automation and activation](docs/architecture/ci-automation.md) for how to enable it in GitHub and optionally require its status check before merging. The workflow is read-only and does not auto-merge or deploy. This local change has not yet been published to GitHub, so no remote Actions run is claimed.
