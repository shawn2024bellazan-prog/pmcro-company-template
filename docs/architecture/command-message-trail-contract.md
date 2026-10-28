# PMCR-O command, message, and Trail contract

Every command carries schema version, command/intent/run/correlation identifiers, optional causation ID, actor, command type, issue time, idempotency key, payload, and context. Every result—including denial, failure, timeout, and partial completion—returns a correlated message with status, stable code, evidence references, Trail Frame reference, and whether human action is required.

The contract is protocol-neutral. Adapters map it to A2A task/message semantics, MCP tool calls/results, HTTP/OpenAPI requests, or internal workflow events without pretending the wire formats are identical. Each adapter must preserve correlation and authorization context.

## Trail requirements

- Emit frames for discovery, validation, policy decision, approval lifecycle, invocation start/result, exception, retry, correction, rollback, and escalation.
- Preserve original seed separately from refined objective; never overwrite the user's raw intent.
- Link each frame to the run, intent, command/message, capability ID/version/digest, policy result, approval reference, evidence, and causal parent where available.
- Store sensitive payloads in protected artifacts and reference them; apply tenant scoping, access controls, retention, and redaction.
- Hash chaining is tamper evidence, not immutability or protection against a privileged writer rewriting all records.
- A trail event or learned pattern does not itself grant execution permissions. Only a separately validated and policy-authorized state can enable execution.

## Registry persistence

`JsonCapabilityRegistryStore` stores source and capability metadata in a local JSON snapshot. It validates records, updates atomically through a temporary file, and increments a revision. It is a single-process local store, not a distributed database; production deployments should supply a transactional store with tenant isolation, concurrency control, backup, and audit controls. It does not fetch, install, or execute remote code.
