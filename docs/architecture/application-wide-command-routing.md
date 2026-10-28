# Application-wide command routing and response contracts

## Principle
Every interface normalizes input to `PmcroCommand<TPayload>` and dispatches through `PmcroApplicationCommandRouter<TPayload,TResult>`. Chat, CLI, APIs, dashboards, agents, skills, marketplace adapters, scheduled jobs, and workflow triggers must not call privileged handlers around this router.

## Processing order
1. Validate envelope and required correlation/idempotency fields.
2. Resolve a registered command handler.
3. Ask the authorization gate; fail closed if authorization is unavailable.
4. Record a Trail Frame before handler execution.
5. Execute the handler and record the result, denial, exception, or cancellation.
6. Return a correlated `PmcroMessage<TResult>` with status, stable code, evidence references, Trail Frame reference, and human-action flag.

## Protocol adapters
`@agent /plugin:skill` is a PMCR-O command-line convention, not universal protocol syntax. A2A, MCP, HTTP/OpenAPI, VS Code, and internal adapters map their native formats into the shared command contract and map responses back to their native response envelopes. Protocol-native authentication and semantics remain intact.

## Trail semantics and limits
The router records command lifecycle evidence with intent/run/correlation IDs, actor, policy decision, status, and next seed. Original seed and refined objective can be carried in command context. Sensitive payloads must not be copied into trail frames; use protected artifact references and digests. The JSONL store is single-process and hash-chained for tamper evidence, not immutable storage or a distributed concurrency solution. Production deployments should use tenant-scoped durable storage, access control, retention, redaction, integrity monitoring, and concurrency controls.

## Safety
Unknown handlers and authorization failures do not execute. Approval-required actions return `waiting_for_approval` and require a separate authorized approval transition. A trail record is evidence, never an authorization grant. Cancellation is recorded and rethrown so caller cancellation semantics remain clear.
