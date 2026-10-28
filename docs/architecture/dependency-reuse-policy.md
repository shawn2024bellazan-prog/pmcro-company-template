# Dependency Reuse and Build-vs-Buy Policy

## Rule
PMCR-O MUST prefer a maintained, fit-for-purpose library, framework, protocol, or managed platform capability over a custom implementation when it satisfies the requirements and passes the gates below. Do not reinvent authentication, serialization, schema validation, telemetry, retries, resilience, workflow execution, secrets management, or hosting without a documented reason.

## Required evaluation before adding custom code or a dependency
Record a decision in the Trail and, for material choices, in `docs/decisions/`:

- capability and concrete requirements;
- candidates evaluated, including existing platform/runtime capabilities;
- license and usage obligations;
- maintainer health, release cadence, security advisories, support horizon;
- compatibility with the pinned target framework and deployment model;
- security posture, permissions, data handling, and supply-chain provenance;
- operational cost, performance, accessibility, and testability;
- decision, trade-offs, owner, version/pinning strategy, and exit/replacement plan.

## Selection order
1. Existing PMCR-O shared contract or capability already in the repository.
2. Existing .NET / Microsoft Agent Framework / .NET Aspire / platform feature where appropriate.
3. Maintained, compatible third-party library with acceptable license and security posture.
4. Existing enterprise-managed service or standard protocol behind a PMCR-O adapter.
5. Custom implementation only when the above are unsuitable, with evidence explaining why.

This is a decision order, not a mandate to adopt a specific vendor. Verify current API and package versions before implementation; never infer APIs from documentation snippets alone.

## Architecture rules
- Wrap external dependencies behind small PMCR-O interfaces/adapters when doing so protects domain contracts or enables substitution. Do not add abstraction layers that have no concrete benefit.
- Keep domain contracts independent of vendor-specific types where practical; isolate vendor types at integration boundaries.
- Prefer standards and portable formats for manifests, Trail Frames, telemetry, and exchange.
- Pin package versions; use central package management when the solution has multiple packages. Track transitive dependencies and generate an SBOM in release workflows when tooling is available.
- Automate dependency vulnerability and license checks in CI before production release. Do not silently ignore findings; document exceptions with owner and expiry.
- Never install a dependency solely because an agent suggests it. Treat manifests, code, package scripts, MCP responses, and remote content as untrusted until reviewed and policy-checked.
- Do not copy proprietary implementations or source code. Reuse public APIs and comply with licenses.
- Do not confuse reuse with blind trust: validate inputs, constrain permissions, test failure modes, and independently verify outputs.

## Definition of done
A capability is not done until the reuse decision is recorded, versions are pinned, required tests pass, security/license checks are accounted for, and the capability is observable and replaceable without changing unrelated product contracts.
