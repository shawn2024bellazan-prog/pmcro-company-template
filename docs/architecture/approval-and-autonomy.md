# Approval and Autonomy

Auto-continuation means the orchestrator proceeds to another bounded cycle without prompting at every routine step. It does not authorize every tool call. Evaluate each requested action independently.

Default: allow only explicitly allowlisted read-only or local-validation capabilities. Unknown risk, secrets, destructive changes, privilege expansion, and external-code execution require human approval. A production approval service should issue short-lived, single-use grants bound to action digest, target scope, actor, and environment.

These are intended production properties; this starter includes only a conservative in-process policy interface, not a signed approval-token service.

## Runtime enforcement

`CycleRunner` evaluates every planned step with `IApprovalPolicy` before invoking the Maker. Each decision is written to the cycle Trail Frames. `Allow` is the only disposition that proceeds to execution. `RequireHumanApproval` parks the queue item in `WaitingForApproval`; `Deny` and policy/compliance failures escalate. A plan's capability name is explicit and defaults to `tests.local` only for safe starter examples; production adapters must set truthful capability identifiers. The policy is a preflight gate, not a substitute for tool-level authorization: each real tool/MCP adapter must enforce the same or stricter policy at the point of execution.
