# Independent Checker review request — PMCR-O G: candidate integration

**Request state:** PROPOSED, NOT DISPATCHED. **No authenticated Checker PASS claimed.**

**Checker task:** Independently inspect the **frozen G: candidate source bytes** and `docs/CANDIDATE-INTEGRATION-FREEZE-2026-10-28.md` before any further integration, using host-authenticated independent Checker identity. The Maker/test-plan author cannot sign their own acceptance.

## Files to verify

1. `plugins/pmcro-runtime/src/ProjectName.Contracts/CandidateCompanyIntegration.cs` (SHA-256 `1ef4befadfb203c26c78a097b34627e41f381e89ff67fd7a181e8058da62b571`).
2. `plugins/pmcro-runtime/tests/ProjectName.Runtime.Tests/CandidateCompanyIntegrationTests.cs` (SHA-256 `f0eeeb3834b69c2d724c3fcd4e0e58f396748d280fc433ba0f196cb060111ebd`).
3. `docs/candidate-test-results/pmcro-g-candidate-integration-20261028.trx` (SHA-256 `4cfc1659c50d605738cc6d247d3d93cba7f1e6f67f78017f635f6fd59ce2eb1f`).
4. `docs/INTEGRATED-ARCHITECTURE-CANDIDATE.md` and `docs/INTEGRATION-ACCEPTANCE-TESTS.md`.

## Independent acceptance requirements

- Verify exact current file bytes and non-authoritative code paths, with no alias creation for `CISO/COS` vs `CIO/CSO/ChiefOfStaff`.
- Check UMP-to-`PmcroMessageEnvelope` output retains payload and exact correlation/causation but never grants authority, creates a cognitive phase, or accepts grant-bearing payload fields.
- Verify explicit deny-all `IRoleExecutionAdmissionProvider` causes no `IAgentRuntime.RunAsync` or bus publication.
- Verify the Orchestrator-owned MAF workflow rejects cross-role phase and authority-carrier frames.
- Confirm no D: grant/registration/activation/key mutation, no G: historical Trail rewrite, no registration, activation, real model call, or user-selected policy/role mapping.
- Re-run the focused .NET 10 tests in an independent environment; compare actual output and hashes.
- Require Checker identity and result digest to be independently attested by the trusted host; a message claiming `I AM the Checker Agent` is not sufficient.
- If preconditions are missing, issue **HALT/BLOCKED** with exact evidence, not a fabricated PASS.

**Target outcome:** one independent signed verdict bound to the candidate source + test receipt, and one proposed unexecuted NextSeed. This document does not trigger that action or authorize any side effects.