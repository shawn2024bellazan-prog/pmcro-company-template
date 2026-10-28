# Interoperability and protocol strategy

## Rule: standard protocols first

PMCR-O must integrate existing protocols through adapters rather than inventing a proprietary wire protocol. Protocol support is declarative and replaceable; the orchestration and governance contracts remain PMCR-O-owned.

- **A2A (Agent2Agent):** agent discovery and agent-to-agent task interaction across independently built agent systems. Use an A2A-compatible adapter for agent cards, task lifecycle, and supported streaming/push behavior. Pin the actual protocol version and implementation package in the runtime lock/configuration; do not assume every peer supports every optional feature.
- **MCP (Model Context Protocol):** expose and consume tools, resources, and prompts where supported. Treat each tool call as a governed operation with explicit schemas, identity, policy checks, timeouts, output limits, and Trail evidence. MCP is not a substitute for A2A agent-task semantics.
- **OpenAPI / HTTP+JSON:** integrate ordinary service APIs using generated or existing clients where possible. Keep API schemas versioned and validate requests and responses.
- **CloudEvents:** use a standard event envelope for asynchronous domain events where appropriate; do not use event delivery alone as proof that an operation completed.

## Declarative protocol bindings

`spec.protocols` in the component manifest declares protocol intent, role, version, endpoint reference, capability list, authentication reference, trust-policy reference, and enabled state. Endpoint and auth references resolve through the deployment's secret/configuration provider. Never store tokens, passwords, private keys, or raw credentials in manifests or Trail Frames.

A manifest is configuration, not authorization. At runtime the policy engine must independently validate tenant, caller identity, destination allowlist, requested capability, data classification, quotas, timeout, and action risk. Unknown peers, protocol versions, capabilities, and auth modes fail closed or require review.

## Adapter contract

Each protocol adapter should provide: capability discovery; schema/version negotiation; authentication integration; timeout/cancellation; bounded retries with idempotency rules; structured errors; health/readiness; telemetry; and Trail Frame correlation. Adapter-specific data must map into shared PMCR-O operation and evidence contracts without erasing original protocol identifiers or provenance.

## Reuse-first implementation order

1. Inspect existing repository code and supported Microsoft Agent Framework / Aspire integration points.
2. Select maintained, compatible protocol SDKs; record license, version, security posture, and support status.
3. Wrap SDKs behind small PMCR-O adapter interfaces; avoid duplicating transport, retry, serialization, or auth libraries.
4. Add contract tests against local fixtures and at least one compatible implementation.
5. Add conformance tests, dependency scanning, SBOM, and pinned dependency updates to CI before production rollout.

## Security boundaries

Agent-to-agent communication does not imply trust. Apply authenticated identities, tenant isolation, explicit destination policies, least privilege, data minimization, prompt-injection-resistant handling of remote content, rate limits, and human approval for high-impact actions. Never let an A2A peer directly mutate Trail storage or bypass the PMCR-O Orchestrator's policy gate.
