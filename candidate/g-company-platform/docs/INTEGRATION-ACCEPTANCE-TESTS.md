# PMCR-O G-to-D integration — acceptance test plan

**Status:** TEST DESIGN ONLY / NOT EXECUTED. No positive runtime authority is configured by this plan.  
**Parent design:** [INTEGRATED-ARCHITECTURE-CANDIDATE.md](INTEGRATED-ARCHITECTURE-CANDIDATE.md).  
**Constraints:** Test with isolated synthetic stores and fake Checker/MAF only; never connect production keys or historical grants. An independently authenticated Checker must later inspect and sign the *real frozen artifact* and logs. The author of this test plan is not that Checker.

## A. Deterministic structural contract assertions

| ID | Given / action | Expected result | Initial verification |
| --- | --- | --- | --- |
| A01 | Parse selected G: organization manifest with `ExecutiveOrganizationManifestLoader` | 10 unique offices; defaults CEO/CTO; no missing owner approval ID | Pending execution |
| A02 | Compare its IDs to G: csuite marketplace identifiers | Flag exactly unresolved CISO, CIO, CSO and COS/ChiefOfStaff alias; no silent office creation | Reviewed source, test pending |
| A03 | Load G: `PmcroMessageEnvelope` with required fields | Valid `pmcro.messaging/v1` message can persist with bounded lineage | Pending execution |
| A04 | Supply missing Trail/agent/payload or noncanonical `PmcroRouteRef.Invocation` | `EnsureValidForPersistence` rejects | Pending execution |
| A05 | Put `Phase` or `FrameRef` on INIT/SEED ingress message | Reject because ingress cannot own a cognitive Frame | Pending execution |
| A06 | Supply valid UMP `/embody` text | Rendered/candidate UMP validates but does not become signed authority | Pending execution |
| A07 | Compare D: and G: commissioning components | Detect identity of the three previously compared source/test files and report divergence in Council | Previously inspected manually; machine check pending |

## B. Workflow and executive safety

| ID | Given / action | Expected result |
| --- | --- | --- |
| B01 | Compile G: macro and micro MAF workflows without starting host | Deterministic ORCHESTRATE → PLAN → MAKE → CHECK → REFLECT graph with Orchestrator boundaries |
| B02 | Inject role/phase mismatch or `GrantsAuthority=true` into workflow frame | Reject; no unauthorized phase transition |
| B03 | A Checker or Maker message merely arrives | No phase transition without owning Orchestrator's admission |
| B04 | Council returns concurrent positions and material dissent | Preserve all positions/disagreements and evidence references; no automatic consensus grant |
| B05 | Council bounded-group-chat request with `MaxDebateRounds<=0` | Reject budgetless debate |
| B06 | Handoff request names unknown office ID | Reject; no invented executive |
| B07 | Council requests owner approval | Emit `human-decision-request` proposal, not approval or activation |
| B08 | Restart MAF workflow from an existing checkpoint | Recover the same cycle boundary and avoid duplicate side-effecting execution; if ambiguous, defer for reconciliation |

## C. Admission, registration, policy and trust

| ID | Given / action | Expected result |
| --- | --- | --- |
| C01 | No `IRoleExecutionAdmissionProvider` configured | `DenyAllRoleExecutionAdmissionProvider` returns null; worker/model not called |
| C02 | RoleWorkerService binding absent, ambiguous or request-supplied | Deny before model invocation |
| C03 | Mismatch between `pmcro-<role>`, `executive-<officeId>`, host worker identity, or AGT `did:mesh:pmcro-*` | Deny; no inferred alias |
| C04 | Wrong role, phase, canonical frame ID, Trail or cycle reference | `GovernedRolePhaseHandler` rejects |
| C05 | No signed grant, registration, or verifier key custody | Deny; do not use historical or copied grant |
| C06 | Revoked grant, key, registration or activation | Deny before model |
| C07 | Stored registration ID or candidate digest differs from checked activation | Deny without rewriting records |
| C08 | Installed files differ from `AgentActivationReceipt.Files` | `AgentActivationGate.IsActivated` false |
| C09 | Effective requested permissions exceed signed/host/environment ceiling or appear in denies | Narrow or reject; never widen |
| C10 | Missing/unpinned AGT policy file, incorrect host, or incompatible identity | Deny; never silently load the OrchestratorService template into RoleWorkerService |
| C11 | Provider returns `AgentId` other than `pmcro-<role>` or framework other than `maf` | Deny |
| C12 | Candidate manifest sets `ownerApprovalOfficeIds=["ceo"]` | No actual human owner grant can be inferred |
| C13 | Request proposes `FirstRegistrationBoundary` JSON without a live signed commissioning boundary | Reject; no registration or consumed-bootstrap receipt |
| C14 | Historic quarantined C-suite activation/grants are discovered | Remain unusable for effective authority or worker startup |
| C15 | Signed grant lacks expiry field | Do not report expiry enforcement; require separately approved contract change if needed |

## D. Messaging, evidence and restart

| ID | Given / action | Expected result |
| --- | --- | --- |
| D01 | Producer returns phase result with a valid Trail/cycle/frame and correlation | Message is `phase-result-proposal` with `proposedOnly=true`, Check verdict null |
| D02 | Publish to bus but Trail evidence not yet anchored | Queue arrival does not equal immutable evidence or CHECK PASS |
| D03 | Duplicate message/Seed/continuation returns after crash | Host idempotency and version boundary prevent a second side effect |
| D04 | Dispatcher state is uncertain | No automatic retry; request external authoritative reconciliation |
| D05 | Checker or evidence reference is forged, stale, untrusted or wrong subject | Reject; no grant or activation |
| D06 | Owner/role approval pending | Keep status deferred without consuming a production grant |
| D07 | Rendered UMP and runtime message disagree on identity, causation, or parent Trail | Reject or mark conflict; preserve raw source |
| D08 | Capability is discovered but not registered, granted or active | No execution, no side-effecting dispatch |

## E. Owner and external Checker gates

- [ ] E01: Owner explicitly resolves the **ten-office vs eleven-identifier** discrepancy, including CISO / CIO / CSO and COS / ChiefOfStaff alias decision, in a new owner-controlled candidate decision.
- [ ] E02: Owner identifies one exact **RoleWorkerService** role and existing framework/registration/capability/subject mapping and allowed authority ceiling; the host verifies the provenance.
- [ ] E03: Owner selects an **existing** AGT policy for the intended worker host with source, lifecycle, immutable digest, owner, identity compatibility and deployment responsibility.
- [ ] E04: A genuinely independently authenticated Checker validates the frozen design, implementation and real tests without relying on the Maker's self-reported PASS.
- [ ] E05: Any future activation has a separate owner-approved Trail with verified registration, signed grant, non-quarantined activation and revocation-aware fresh-runtime checks. This test plan authorizes none of these operations.

## How to use this test plan

1. Freeze the exact candidate source hashes before implementing adapters; distinguish G design from D authority state.
2. Implement only after Seed 0034 owner decisions are present and independently checked.
3. Run C# integration tests against **isolated ephemeral test stores**, not installed production data.
4. Save actual test output, Checker evidence and hashes through the owning authenticated Trail host when authorized.
5. Reject or HALT on missing evidence. Never mark these planned cases PASS simply because their code or documentation exists.

**Design-time static review (current cycle):** G and D AGENTS.md rules, G Seeds 0032–0034, role handler and UMP contracts, Council and MAF workflows, D grant verifier and activation gate were inspected. This is author review, *not* independent Checker evidence. All runnable test cases above remain NOT_RUN.