# Enterprise Trail Frame platform

Trail Frames are the shared evidence envelope for the entire PMCR-O ecosystem, not just logs emitted by the main loop. Frames connect user intent, agent/tool activity, authorization, verification, correction, and learning across products.

## Canonical field groups

- Identity: organization, project, intent, run, agent, correlation IDs.
- Sequence/lineage: frame ID, sequence, parent, causal links, timestamp.
- Intent: original seed, refined objective, constraints, next seed.
- Execution: phase, capability, action, status.
- Evidence: references to test results, artifacts, source records, and observations.
- Governance: policy ID, authorization decision, approval reference.
- Outcome: checker verdict, reflection, corrections, completion flag.
- Learning: proposed lesson, learning status, confidence, provenance, reuse eligibility.

## Storage and security

The JSON Schema is portable across .NET, Python, MAF, Aspire, GitHub adapters, and marketplace packages. The current JSONL store remains a local development adapter, not an enterprise ledger. Production storage must add authenticated writes, tenant isolation, authorization, retention/deletion policy, encryption, redaction, integrity verification or signatures, concurrency control, backup/recovery, and audited exports. Append-only file writes alone do not establish immutability or tamper evidence.

Sensitive payloads should be stored in a protected artifact store and referenced from a frame. Redact secrets before persistence. Schema version changes require compatibility tests and migration rules.
