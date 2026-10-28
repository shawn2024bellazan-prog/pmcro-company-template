# PMCR-O implementation status — development snapshot

**Updated development snapshot:** the bounded authorization-to-return integration now stages verified intent in the durable handler and requires the exact checker frame in an anchored hash chain. This document describes the branch after the late-October-28 integration cycle; verify the most recent commit and GitHub CI run before using it.

**Earlier verified baseline:** `9f4d470eac205f2987d7cf817d1db22d3c306644` on `feat/pmcro-subagent-return-v1`, October 28, 2026.

**CI evidence:** [GitHub Actions run 40642959766](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/40642959766), completed successfully: .NET 10 solution build; 47 Core, 20 continuation, 9 integration and 38 subagent-return checks; demo smoke test. These are **test counts**, not deployed agents or live work.

## Already implemented in this development branch

| Area | What exists |
| --- | --- |
| Core cognitive loop | Bounded intent lifecycle, queue/worker reference implementation, policy gates and Trail contracts |
| Command and plugin scaffolding | Versioned manifests, routing contracts, application router, idempotency guards and disabled-by-default plugin descriptors |
| Return-to-caller | Versioned `SubagentReturnEnvelope`, canonical `I AM` context checks, host lifecycle observations, caller-owned failure/decision frames |
| Continuation | `CycleResultEnvelope`, proposed `NextSeed`, caller-side checks for lineage, evidence, policy and idempotency |
| Local durable-state foundation | SQLite versioned subagent return-state transitions, concurrent CAS and restart test coverage |
| Checker proof component | HMAC proof authority and result-digest binding. A genuine production issuer, independently authenticated Checker and host secret custody are **not** wired up |
| Trails | JSONL Enterprise Trails, SQLite hash-linked Trail component, verifier interface for a separately trusted head, and test-only in-memory anchor |
| Outbox | SQLite admission, versioned dispatch reservation, uncertain state and reconciliation reference methods |
| Caller policy preflight | A fail-closed preflight and a coordinator reference implementation; both have integration limits |
| Authorization snapshots | In `9f4d470`: an outbox table storing result digest, seed, policy scope/version, Checker fields, caller/run/correlation and evidence reference; reservation checks current mirrored policy version |

## Important boundaries that remain

- **Production activation is not done.** The subagent-return plugin is explicitly disabled by default and subject to an authenticated host grant.
- **End-to-end external dispatch is unfinished.** The authorized snapshot API is now composed into the opt-in durable `SubagentCompletionHandler` to stage intentions only after host Checker verification and an exact anchored checker frame. It intentionally does not invoke a side-effecting dispatcher. A trusted external operation service and authoritative reconciliation remain missing.
- **The authorized-source and verification interfaces remain fail-closed unless configured.** A self-reported PASS or forged signature cannot be accepted as proof of an independently executed Checker.
- **Hash chaining is not immutable storage.** External head anchoring, trusted writer isolation and durable independent retention are operational requirements.
- **Production key and identity custody are not configured.**
- **No external dispatch, production deploy, promotion or separately authenticated Checker review has been demonstrated for this increment.**
- **Local reference adapters are not a complete distributed exactly-once infrastructure.**

## What should happen next?

1. Validate the new authorized-snapshot integration and host-observed terminal closure, keeping external dispatch disabled by default.
2. Add tests for revoked policy versions, concurrent authorization, restart/reservation, forged checker evidence, and untrusted anchors.
3. Verify CI on the exact resulting commit.
4. Obtain a genuinely independent Checker verdict and host approvals for any later environment change.

**Human authorization:** the human orchestrator retains the final approval decision, constrained by actual host and organization permissions. Writing a `/pmcro:seed` message is not itself an activation grant.

For everyday definitions see [Glossary](GLOSSARY.md); for the conceptual walkthrough see [Start here](START-HERE.md).
