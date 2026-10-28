# Skill routing and adapter conformance

## Contract

All UI, CLI, API, workflow, plugin, and agent transports normalize inbound operations to `PmcroCommand<TPayload>`. Responses use `PmcroMessage<TResult>`. Transport adapters must preserve command ID, intent ID, run ID, correlation ID, causation ID, actor, idempotency key, timestamps, and evidence references.

## Skill routing

`SkillRouteResolver` resolves a command type to exactly one enabled declarative route. Missing, disabled, malformed, or ambiguous routes fail closed. Route resolution is not authorization: the application router, capability status validator, and runtime policy gate must still authorize execution. Catalog mutation requires host-level access control; this local JSON implementation is intended for single-process/template use.

## Adapter conformance requirements

1. Normalize the incoming transport envelope without discarding caller or lineage metadata.
2. Validate schema and required fields before dispatch.
3. Resolve route and capability by stable IDs and pinned/constraint-based versions.
4. Enforce permissions and policy at runtime; route metadata alone is not proof of authorization.
5. Emit Trail Frames for accepted, rejected, denied, started, completed, failed, cancelled, and approval-required transitions.
6. Return a correlated response with stable status/code and evidence references.
7. Do not expose stack traces, secrets, tokens, or unredacted private payloads in messages or trails.
8. Retry only idempotent operations, using the idempotency key and persisted execution state.
9. Preserve protocol-native semantics in A2A/MCP/HTTP adapters; the common contract is internal normalization, not a replacement wire protocol.
10. Provide conformance tests for success, invalid envelope, unknown/ambiguous route, disabled route, policy denial, approval required, timeout/cancellation, handler exception, redaction, and correlation preservation.

## Status

The resolver and JSON catalog are implemented as a local reference boundary. Live VS Code Marketplace, GitHub Marketplace, MCP Registry, A2A network, and external plugin installations are not implied by this code. Production adapters still require independently verified SDK/API implementations, credential handling, tenant isolation, and operational testing.
