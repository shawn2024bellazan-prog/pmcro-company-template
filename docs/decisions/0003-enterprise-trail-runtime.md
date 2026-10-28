# ADR 0003: Enterprise Trail runtime recording

- Status: Accepted for local reference implementation
- Decision: Introduce `IEnterpriseTrailStore` and `ICapabilityTrailRecorder`; instrument governed capability execution when a recorder is configured.
- Rationale: Cross-product operations need structured evidence and causal lineage, not unstructured log lines. A storage interface keeps persistence replaceable.
- Integrity: Sequence numbers plus a SHA-256 hash chain detect common edits/truncation but are not a substitute for immutable storage or externally signed checkpoints.
- Safety: Policy evaluation occurs before invocation. Trail recording cannot turn a denial into an allow. Sensitive payloads are referenced, not copied into frames.
- Limitation: Local JSONL store is single-process; discovery/catalog ingestion and full intent-seed linkage require production adapters and tenant-aware services.
