# Execution Boundaries & Evidence Hygiene

## Bounded execution

Use `ExecutionBoundary.RunAsync` around calls that need a finite deadline. It returns explicit completed, timed-out/unknown, cancelled/unknown, or failed/unknown outcomes. A timeout or cancellation is **not proof that the underlying operation stopped**: cancellation is cooperative, and a remote side effect may have committed before the response was lost. Keep an operation ID and reconcile external state before retrying uncertain side effects.

## Retry decision

`RetrySafetyPolicy` is a small policy helper, not a distributed retry engine:

- Permanent failures are never retried automatically.
- Confirmed-not-started operations may be retried.
- Operations with enforced idempotency may be retried according to the idempotency contract.
- Non-idempotent operations with uncertain outcomes must be reconciled before retry.

Retries still require bounded attempt budgets, backoff, policy authorization, and Trail Frame evidence. A client-supplied idempotency key alone does not make an operation idempotent; the target system must enforce it.

## Trail evidence hygiene

Prefer structured, allow-listed evidence fields and protected artifact references. Do not put raw request bodies, environment dumps, exception messages, credentials, or personal data into ordinary Trail Frames. `TrailEvidenceSanitizer` is a best-effort last-mile scrubber for common secret assignments and bearer tokens; it is not a DLP engine and does not replace data classification, allow-lists, access control, encryption, tenant isolation, or retention policy. Sanitize each field before persistence and use bounded lengths.

## Operational readiness checklist

- [ ] Configure finite per-capability and end-to-end deadlines.
- [ ] Ensure cancellation propagates to adapters where supported.
- [ ] Record timeout/cancel/failure as potentially uncertain outcomes.
- [ ] Reconcile before retrying non-idempotent external operations.
- [ ] Enforce idempotency at the actual side-effect boundary with a durable shared store.
- [ ] Redact before Trail persistence; restrict access and retention.
- [ ] Use bounded retries, exponential backoff, jitter, and circuit breaking in production adapters.
- [ ] Validate adapter contracts and run the complete .NET CI suite in a supported SDK environment.

This increment supplies reusable primitives and guidance. It does not itself add a distributed scheduler, circuit breaker, production secret scanner, or remote-operation reconciliation service.
