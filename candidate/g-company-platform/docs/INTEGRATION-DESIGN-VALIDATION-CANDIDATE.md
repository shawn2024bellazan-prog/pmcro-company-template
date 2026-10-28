# PMCR-O integrated design — static validation note

**Status:** CANDIDATE STATIC REVIEW ONLY. **Independent authenticated Checker: NOT_RUN.** No production or development activation follows from this document.

## Scope

This note reviews the documents [INTEGRATED-ARCHITECTURE-CANDIDATE.md](INTEGRATED-ARCHITECTURE-CANDIDATE.md) and [INTEGRATION-ACCEPTANCE-TESTS.md](INTEGRATION-ACCEPTANCE-TESTS.md) on the owner-selected G: design workspace, alongside the existing D: host/control-plane contracts. Both documents were created and read back; Markdown backtick formatting was corrected and checked. No source files or historical Trails were modified.

## Observed source checks

- **11/11 required source paths found** using Desktop Commander file metadata checks: G: AGENTS.md, message policy, organization and marketplace manifests, message envelope, governed role handler, MAF workflow, executive council, G: Seed 0034, D: ProductionGrantVerifier and D: AgentActivationGate.
- **Organization candidate parsed:** ten office IDs; defaults are `ceo` and `cto`; `ownerApprovalOfficeIds` is empty. This data grants no real owner authority.
- **Marketplace manifest parsed:** eleven executive identifiers. Cross-map yields organization-only `CISO`, `COS` and marketplace-only `CIO`, `CSO`, `ChiefOfStaff`. The Chief of Staff name might be an alias, but no owner-approved mapping was observed.
- **G: `.agents/message-policy.json`:** candidate and `activation=false`; message reception does not grant authority.
- **G: Seed 0032:** source inventory identifies deny-all role admission and no host-selected RoleWorkerService AGT policy. Its template policy remains only a candidate.
- **G: Seed 0033 / HALT:** a positive provider was blocked by missing owner-controlled role binding and selected worker policy.
- **G: Seed 0034:** explicitly unexecuted; requires owner decisions before provider implementation.
- **D: global/local AGENTS.md:** configuration inheritance cannot widen authority; INIT/SEED are ingress, not phase transitions; discovery, registration, grant, activation and execution are distinct.
- **D: ProductionGrantVerifier and AgentActivationGate:** actual source verifies signed store state, revocations, registration/candidate digest and installed files. This is not equivalent to granting permissions by design documentation.

## Acceptance inventory

The test plan contains 38 numbered Given/When/Then scenario rows: A01–A07 (7), B01–B08 (8), C01–C15 (15), D01–D08 (8), plus five owner/external Checker gate checkboxes E01–E05.

All **38 runnable scenarios remain NOT_RUN** in this cycle, because no code changes, workers, model calls, or live authority changes are authorized. The test plan is design specification, not a newly passed test suite.

## Review outcome

- **Design traceability:** STATIC_REVIEW_PASS_WITH_OPEN_GATES — source and constraint references were found; incompatible identities, office manifests and missing authority inputs are explicitly called out.
- **Independent Checker:** PENDING — no separately authenticated Checker invocation, signed verdict, or persisted Checker Trail receipt was produced.
- **Implementation status:** CANDIDATE_DOCUMENTED — no integration code was written or executed.
- **Production commissioning / role admission / C-suite activation:** HALT until owner inputs, trusted control-plane authority and separate authenticated Checker evidence exist.

## No authority changes

G: `.pmcro` historical Trails, D: global/project `.pmcro` state, keys, registry, grant, revocation and activation stores were not written. The design's proposed frame is a paragraph/code block under `docs`; it is **not a persisted PMCR-O Trail Frame**.

## Remaining exact decisions

1. Owner chooses one versioned executive-office source of truth and explicitly resolves CISO / CIO / CSO and COS / ChiefOfStaff identity semantics.
2. Owner supplies already-existing RoleWorkerService role-to-framework/registration/capability/subject binding and exact authority ceiling, or separately authorizes creation as new work.
3. Owner identifies an existing AGT policy artifact actually intended for RoleWorkerService, including immutable digest, identity compatibility, owner and deployment responsibility.
4. Host independently verifies non-quarantined registration, signed grant, revocation-aware activation and actual checked source bytes.
5. A genuinely separate, authenticated Checker validates frozen integration implementation and runs real tests before any later governed activation.