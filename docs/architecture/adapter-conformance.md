# Adapter conformance contract

Every native entry point (A2A task/message, MCP tool call, HTTP/OpenAPI request, VS Code/agent command, or internal UI action) must normalize into `PmcroCommand<TPayload>` before the shared application router. The adapter retains native protocol semantics and identifiers in its own metadata; the PMCR-O envelope is the common governance boundary, not a replacement protocol.

## Required checks

- Validate required command, intent, run, correlation, actor, command type, issue-time and idempotency fields before dispatch.
- Preserve command, intent, run and correlation IDs in every response. Keep causation lineage when present.
- Return a machine-readable status and stable code for success, partial completion, denial, cancellation, approval wait and failure.
- `waiting_for_approval` must set `RequiresHumanAction=true`. A denied/failed command must not be mapped as success.
- Include evidence references and a Trail Frame reference when one was produced. Never fabricate a Trail Frame ID.
- Enforce policy, permissions, tenant scope, timeouts, cancellation and idempotency at the runtime boundary; a manifest or adapter declaration is not authorization.
- Avoid leaking secrets, credentials, raw exception messages or sensitive payloads in logs and evidence.
- Preserve native protocol error semantics when mapping a PMCR-O response back to the client.

`AdapterConformance` checks protocol-neutral envelope invariants. It does not prove that an adapter authenticates correctly, honors a native protocol specification, isolates tenants, or enforces runtime policy; each concrete adapter needs integration and security tests for those behaviors.
