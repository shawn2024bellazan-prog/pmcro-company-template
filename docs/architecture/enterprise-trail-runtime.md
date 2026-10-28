# Enterprise Trail Runtime

## Runtime behavior

Capability execution can be wired to `EnterpriseCapabilityTrailRecorder` and `JsonlEnterpriseTrailStore`. When configured, the governed executor records policy denials, registration failures, adapter-selection failures, execution start, and execution result. Policy evaluation remains the authorization boundary; writing a frame never grants permission.

Frames include intent/run/correlation identifiers, capability identity and provenance, policy reference and decision, status, evidence references, and causal linkage to the preceding frame in the run. The store assigns per-run sequence numbers and maintains a SHA-256 hash chain to help detect accidental or partial tampering.

## Security and limits

- JSONL persistence is single-process/local-file storage, not a distributed ledger.
- Hash chaining is tamper evidence, not immutability: an actor able to rewrite the entire file can recompute the chain. Production deployments should add restricted storage identities, external checkpoints/signatures, retention controls, backups, tenant isolation, and access auditing.
- Do not put secrets, credentials, private prompts, or raw sensitive payloads in frame fields. Store protected artifacts separately and reference them with access-controlled evidence IDs.
- The current capability invocation request carries an objective/intent ID, not the full raw Seed Intent. Integrations must preserve the verbatim raw seed in the intent system and link it by ID; do not overwrite it with a refined objective.
- Exception messages are intentionally excluded from trails; only a coarse error type is recorded.
- Recorder injection is optional for backwards compatibility. Enterprise deployments should require it through dependency-injection configuration and test that omission is rejected by deployment policy.

## Extension points

`IEnterpriseTrailStore` permits a tenant-aware database/event-store adapter. `ICapabilityTrailRecorder` permits a recorder that emits additional source-discovery, digest-validation, approval lifecycle, rollback, and marketplace synchronization frames. Those events should share the same intent/run/correlation lineage and evidence conventions.
