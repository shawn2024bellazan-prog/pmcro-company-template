# Durable Seed Intent Queue

## Contract

Every incoming user message is stored verbatim as a `Pending` queue item before refinement. Preserve the raw message even when grammar, spelling, transcription, or intent is ambiguous. Refinement adds a separate `RefinedObjective`; it never replaces the source message.

Lifecycle:

`Pending → Refining → Ready → Running → (Running | WaitingForApproval | Completed | Escalated | Failed)`

A retry remains the same intent and increments cycle metadata. A cycle boundary is not completion. Mark `Completed` only when independent Check evidence proves the objective's acceptance criteria. A new child intent may be linked through `ParentIntentId` when Reflector/Orchestrator identifies a distinct next objective.

## Continuation rules

- `ACCEPT` + objective incomplete: retain the same queue item, record the cycle outcome, and continue in `Running` with the next cycle seed.
- `LOOP`: retain the intent and retry only within configured cycle/run budgets.
- `ESCALATE`: persist the reason and pause the affected action. Do not spin in a retry loop while approval is outstanding.
- Explicit cancellation: mark `Cancelled`; preserve history.
- Process restart: reload non-terminal items and resume only after checking leases/idempotency in the production queue implementation.

## Persistence status

`JsonIntentQueue` is a local JSON file store with atomic replacement and in-process locking. It is a useful first increment, **not** a distributed queue: it does not yet provide cross-process locks, visibility timeouts, leases, dead-letter handling, deduplication, or transactional coupling with Trail Frames. Production deployment should implement `IIntentQueue` using a transactional database or durable broker and use idempotency keys.

## GitHub Actions boundary

GitHub Actions should build, test, scan, and package PMCR-O on pushes and pull requests. Actions are not the product's durable user-message queue: runs can time out, be cancelled, or be retried. A future Actions adapter may dispatch bounded jobs, while the persistent PMCR-O queue remains the source of truth.
