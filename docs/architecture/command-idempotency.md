# Command idempotency and duplicate-execution control

## Contract

The application router claims an idempotency scope **after** authorization allows the command and **before** the handler is called. The scope is `(ActorId, CommandType, IdempotencyKey)`. A second claim while running is rejected as `DUPLICATE_COMMAND_IN_PROGRESS`; a repeat after completion is rejected as `DUPLICATE_COMMAND_COMPLETED`. Both paths emit a correlated response and Trail Frame. A policy-denied or approval-required command does not hold a claim, so it may be retried after governance changes.

The in-memory implementation uses a semaphore for atomicity within one process. It is a reference adapter only: state is lost on restart and multiple replicas do not coordinate. Production requires a durable shared store with a uniqueness constraint/atomic compare-and-set, expiry/retention policy, tenant-aware scope, observability, and operational recovery. Never assume the in-memory adapter provides distributed exactly-once execution.

On cancellation before a known outcome, the claim is released. If a handler throws after execution begins, the claim is conservatively marked completed because the system cannot know whether an external side effect already happened. Operators must inspect Trail evidence before any retry; use downstream idempotency keys for external APIs where available. This is duplicate suppression, not a guarantee of exactly-once effects.

A handler may not return `RequiresHumanAction=true` or `waiting_for_approval` after dispatch began. Approval is a pre-execution decision. Such a result is treated as a handler contract violation and the claim is released; implementations must not perform side effects before returning that response.

## Trail evidence

Frames include `idempotency:claimed`, `idempotency:completed`, duplicate status evidence, and release evidence where applicable. The current Trail store remains local, single-process, and tamper-evident rather than immutable or distributed. Idempotency keys and raw sensitive payloads should not be copied into Trail evidence; this implementation records lifecycle markers only.
