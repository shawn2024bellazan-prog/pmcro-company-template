# Marketplace and Capability Integration (Increment 9)

## Goal

Normalize capability discovery across existing catalogs without replacing them with a new proprietary marketplace. PMCR-O owns governance, capability lifecycle, execution policy, evidence, and verification. Upstream catalogs remain authoritative for their own listings and distribution.

## Current scope

- Common `CapabilitySource` and `CapabilityRegistration` records.
- Explicit lifecycle states: Discovered → Available → Validated → Approved → Executable; Disabled is a separate stop state.
- JSON schema and sample catalog records.
- `ICapabilitySourceAdapter`, `ICapabilityInvoker`, and `ICapabilityPolicyGate` boundaries.
- `GovernedCapabilityExecutor` denies non-executable registrations, mismatched identities, missing adapters, and non-Allow policy decisions before invoking an adapter.
- Source catalog records are references/configuration placeholders. They do not prove live marketplace connectivity, account authorization, installation, or availability.

## Lifecycle rules

1. Discovery records source and provenance; discovery never grants trust.
2. Availability means a listing can be retrieved, not that its code is safe.
3. Validation requires a content digest, validator identity, timestamp, compatibility checks, and test evidence.
4. Approval is a policy decision tied to a specific artifact/version/digest and requested permissions.
5. Executability requires approved policy state, a matching invoker, and runtime permission checks on each invocation.
6. Every source response, version pin, decision, invocation, result, and rollback reference should be correlated to Enterprise Trail Frames by the production adapter.
7. Secrets must be supplied by approved secret managers or host identity; never stored in manifests, source URLs, logs, or Trail evidence.
8. Marketplace installation, repository writes, external code execution, permission expansion, and production changes are approval-gated operations.

## Adapter plan

- `MarketplaceAdapters`: GitHub Marketplace and VS Code Marketplace listing/distribution adapters, each with independent authentication and policy.
- `SkillAdapters`: local and repository-based skill discovery, manifest parsing, content hashing, and safe packaging.
- `McpAdapters`: MCP tool/resource discovery and invocation through a maintained SDK.
- `A2AAdapters`: agent-card discovery and task lifecycle through a maintained A2A SDK.
- `ServiceAdapters`: OpenAPI/HTTP clients generated or reused from trusted schemas.
- `Security`: signature/digest verification, license and vulnerability policy, secret references, allowlists, sandboxing, and permission checks.
- `TrailEvidence`: common evidence envelope and redaction/retention controls.

## Reuse-first requirements

Before implementing a protocol adapter, record a short dependency decision covering maintainer, license, supported protocol version, security posture, update cadence, transitive dependencies, testability, and replacement path. Prefer maintained SDKs and generated clients over custom protocol implementations.

## Known limitations

No marketplace network client, installer, MCP transport, A2A transport, signature verifier, SBOM generator, vulnerability scanner, or external registry credentials are implemented in this increment. The contracts establish safe extension points; each adapter requires implementation and conformance tests before production use.
