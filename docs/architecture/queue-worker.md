# Queue worker and completion gate

## Current local execution model

`IntentQueueWorker` drains a bounded number of `Pending` or `Ready` intents in creation order. For each pending item it:

1. transitions to `Refining` while retaining the original raw message;
2. calls an `IIntentRefiner` adapter;
3. parks ambiguous work in `NeedsClarification` rather than guessing;
4. persists the refined objective and transitions to `Ready`;
5. runs the bounded Plan → Make → Check → Reflect controller;
6. marks `Completed` only when the final verdict is `ACCEPT`, the objective is complete, and the independent check and policy check both pass;
7. persists `Escalated` or `Failed` outcomes otherwise.

The worker is deliberately sequential and local. `JsonIntentQueue` uses a process-local semaphore and atomic file replacement. It is not a distributed queue, does not provide leases, and does not guarantee multi-process compare-and-swap. Do not run multiple worker replicas against one JSON file.

## Restart and side-effect semantics

A process crash can leave an item in `Running`. Automatic replay is not enabled by default because the Maker may have completed a non-idempotent external action immediately before the crash. A future production adapter must add durable execution checkpoints, idempotency keys, action reconciliation, lease expiry, and a recovery policy before replaying such work. Until then, inspect and reconcile interrupted work before moving it back to `Ready`.

## Human interaction

`NeedsClarification` preserves the original request and records why execution is parked. A future message-ingress adapter should attach the user's clarification as a child/correction Trail Frame and resume the same intent rather than replacing the raw original. `WaitingForApproval` is separate: it represents a policy gate, not a missing-understanding gate.

## Production path

Keep `IIntentQueue` and `IntentQueueWorker` independent of storage and hosting. Add adapters incrementally for a transactional database or durable broker, MAF-backed refiner and cycle runner, and an Aspire-hosted worker. GitHub Actions validates builds/tests; it is not the runtime message queue.
