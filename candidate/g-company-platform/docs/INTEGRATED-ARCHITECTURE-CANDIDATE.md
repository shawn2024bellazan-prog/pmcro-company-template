# PMCR-O company and AgentSystem integration — candidate architecture

**Status:** CANDIDATE DESIGN ONLY; not registered, granted, installed, activated or executed.  
**Owner-selected design baseline:** `G:\workspace\pmcro-company-platform`.  
**Separately declared host/control-plane root:** `D:\AgentSystem` (global instructions; project runtime at `local\projects\ProjectName`).  
**Scope:** Source map, composition seams, non-authoritative governance design, and acceptance criteria. No source-tree merge, runtime configuration changes, registration, key, grant, revocation, activation, worker startup, or historical Trail change.

## 1. Authority and naming rules

1. A human owner can select G: as the company *design baseline*. That choice does not change D:'s canonical-host declaration or grant authority.
2. No message, UMP `/embody`, identity string, marketplace discovery, agent vote, manifest, historical Checker record, or copied activation file is authorization. `TRIGGER != AUTHORITY`; discovery, registration, grant, activation and execution remain distinct.
3. The D: global/project `AGENTS.md` and G: candidate `AGENTS.md` require explicit governance and no authority widening. `INIT` / `SEED` are ingress, not phase transitions.
4. Preserve original G: `.pmcro/trails` as append-only historical evidence. This document and its proposed frame are **not** persisted Trail evidence.
5. Role names such as "I AM the Chief Executive Officer Agent." are display declarations; host-authenticated identity and grant evidence must come from trusted control-plane services.

## 2. Source-of-truth and dependency map

| Area | Selected contract / current location | Design role | Binding restriction |
| --- | --- | --- | --- |
| Company and offices | G: `.agents/organizations/pmcro-autonomous-ai-agent-company/organization.candidate.json` | Owner-selected candidate company shape | Never infer permissions from `ownerApprovalOfficeIds` or office names |
| C-suite discoverability | G: `marketplaces/csuite-marketplace/manifest.json` | Candidate listing of offices and commands | Discoverable is not registered or activated |
| Office schema and loader | G: `plugins/pmcro-runtime/src/ProjectName.Contracts/ExecutiveOrganizationManifest.cs`; `src/ProjectName.Runtime.Maf/ExecutiveOrganizationManifestLoader.cs` | Validate office shape, defaults, duplicates | No implicit new offices or authority |
| Executive council | G: `plugins/pmcro-runtime/src/ProjectName.Runtime.Maf/MafExecutiveCouncil.cs` | Concurrent recommendations, bounded group chat, handoff, preserved dissent | Construct only with an effective verified grant; recommendations grant nothing |
| PMCR-O macro/micro workflows | G: `plugins/pmcro-runtime/src/ProjectName.Runtime.Maf/PmcroWorkflowLayer.cs` | Role/phase sequencing, checkpoint-capable MAF wrappers | Workflow edges cannot grant authority; a frame arrival cannot independently advance a real Trail |
| Role execution seam | G: `plugins/pmcro-runtime/src/ProjectName.Harness/GovernedRolePhaseHandler.cs` | `IRoleExecutionAdmissionProvider` + `IAgentRuntime` + `IMessageBus` | Default `DenyAllRoleExecutionAdmissionProvider`; no production positive provider proven |
| Structured message | G: `plugins/pmcro-runtime/src/ProjectName.Contracts/PmcroMessageEnvelope.cs`; `.agents/message-policy.json` | `pmcro.messaging/v1` with Trail, cycle, frame, causation, correlation, source/target | Persist-before-render is a policy requirement, not proof any transport is immutable |
| Human-readable UMP | G: `company/governance/protocols/unified-message-protocol.md` | `/embody` identity, objective, evidence, dependencies, risks | UMP is `1.0.0-candidate`, not a signed grant |
| Governed MAF agent | G: `plugins/pmcro-runtime/src/ProjectName.Runtime.Maf/MafGovernanceBoundary.cs` | Explicit policy-file injection, deny-overrides, function middleware | Existing AGT policy is not selected/deployed for RoleWorkerService |
| Trusted grant verifier | D: `local/projects/ProjectName/src/ProjectName.Core/ProductionGrantVerifier.cs` | Verify stored signed grant, key and grant/registration/key revocations | No second verifier or request-supplied key authority |
| Activation verifier | D: `local/projects/ProjectName/src/ProjectName.Harness/AgentActivationGate.cs` | Check receipt, file hashes, registration, grant, activation/revocation, effective ceiling | `IsActivated` is needed at admission; do not call `ActivateAsync` in this design |
| Commissioning | D: `local/projects/ProjectName/src/ProjectName.Harness/ControlPlaneCommissioningService.cs`, `CommissioningGrantBoundary.cs`, `CommissioningEvidenceValidator.cs` | Governed commissioning operations and signed boundary | A self-described `FirstRegistrationBoundary` JSON is not valid signed authority |
| Runtime stores | D: `local/projects/ProjectName/src/ProjectName.Infrastructure/FileControlPlaneStores.cs` | File-backed registration, grant, revocation, activation | Read only through trusted host; never copy historical G: records into D: |
| Test/runtime package | G: `plugins/pmcro-runtime`; D: `local/projects/ProjectName` | G contains certain extra MAF and role-worker files; D still defines canonical host rules | Shared sources may be byte-identical but must be linked by provenance, not blindly overwritten |

**Duplicate evidence:** `ControlPlaneCommissioningService.cs`, `CommissioningEvidenceValidator.cs`, and `ControlPlaneCommissioningTests.cs` were compared and found content-equal in D: and G: at inspection. `MafExecutiveCouncil.cs` differs. G: includes `GovernedRolePhaseHandler.cs` and `PmcroWorkflowLayer.cs` not present in matching D: source directories. This does **not** imply G: has effective production authority.

## 3. Candidate integration seams

```text
Human owner (real host identity and scoped approval)
  -> Orchestrator-owned INIT / SEED + admission [D: host rules]
  -> Trail-owned cycle [no phase change from message receipt alone]
  -> G: PmcroMessageEnvelope / UMP validation
  -> G: PmcroMafWorkflowCompiler -> role/phase proposals
      -> G: ExecutiveOrganizationManifest + MAF ExecutiveCouncil (non-authoritative advice)
      -> G: GovernedRolePhaseHandler ONLY IF independently admitted
           -> host-owned immutable role/registration/framework binding (MISSING)
           -> D: signed grant + registration + revocation + activation validation
           -> host-owned, integrity-pinned AGT policy for RoleWorkerService (MISSING)
           -> MAF agent, bounded scoped grant, exact host identity
  -> return candidate result + causation/evidence to owning Orchestrator Trail
  -> independent CHECK and REFLECT -> one proposed NextSeed
```

**Composition contract:** the future adapter implements *existing* `IRoleExecutionAdmissionProvider.ResolveAsync(PmcroRequest, CancellationToken)`; it returns `RoleExecutionAdmission` only if immutable host role identity equals the request's canonical role/phase/frame AND a separately authenticated host mapping selects an **existing** registration, framework, activation and grant. It uses `AgentActivationGate.IsActivated`, `ProductionGrantVerifier`, registration/activation/revocation stores and installed-file proof. Effective scope is the intersection of signed grant and trusted ceilings minus denies. It returns null/denies on any missing or contradictory evidence. It does not create authority.

**Worker identity seam:** current `GovernedRolePhaseHandler` requires agent ID `pmcro-<role>` and MAF framework `maf`; `MafExecutiveCouncil` builds `executive-<officeId>` agents. These are **different agent namespaces**. Do not treat executive agents as role workers or automatically alias their IDs. Owner and host must approve a precise mapping *after* proof of AGT `did:mesh:pmcro-*` policy identity compatibility.

**Policy seam:** `AgentGovernanceBoundary(policyPath)` fails if the policy file is missing. G: Seed 0032 identifies a SHA-256 pinned `pmcro-recursive-lockdown.yaml` only as a `pmcro-dotnet` *candidate template for OrchestratorService*, not a selected RoleWorkerService policy. Do not install it, weaken its rules, or substitute a Python/MCP policy without a new owner decision and Checker PASS.

**Source reuse:** Keep D: existing control-plane implementation as a referenced contract. Do not overwrite a D: source file with G: or transfer grant/secret/store JSON from G: to D:. G: `PmcroWorkflowLayer` only compiles workflow structure; it is not itself evidence of a live host subscription or independent Checker.

## 4. Structured message boundary and persisted evidence

G: `PmcroMessageEnvelope` requires `MessageId`, `Kind`, `TrailId`, `SourceAgent`, `TargetAgent`, `Payload`, `CreatedAt`; it supports `SeedRef`, `CycleRef`, `FrameRef`, `CausationId`, `CorrelationId`, `EvidenceRefs`, `Route` and role/phase. The runtime envelope's `GrantsAuthority` is false. `EnsureValidForPersistence()` checks canonical routing and rejects ingress messages owning a cognitive frame. The UMP human-readable `/embody` fields are a *different candidate view*, to be translated or validated against the runtime contract without assuming they are wire-identical.

G: `.agents/message-policy.json` requires persist-before-render. The `IMessageBus` queue and MAF checkpoints are *transport/recovery* state, not a trusted append-only ledger. Real Trail persistence, immutable head anchoring, and separately authenticated Checker custody must be verified before any elevation of status. All outputs are `proposedOnly` until the owning Trail and Checker accept them.

## 5. Executive naming owner decision — UNRESOLVED

- **G: organization candidate (10 office IDs):** `ceo, cto, cfo, coo, cpo, cmo, ciso, clo, chro, cos`. Defaults `ceo, cto`; `ownerApprovalOfficeIds=[]`.
- **G: csuite marketplace (11 identifiers):** `CEO, CTO, COO, CFO, CPO, CMO, CLO, CIO, CSO, CHRO, ChiefOfStaff`.
- **Difference:** organization has `CISO` absent from marketplace; marketplace includes `CIO` and `CSO` absent from organization; `COS` vs `ChiefOfStaff` might refer to one office but no alias is approved.
- **Owner must choose:** (a) use the existing ten-office organization as the sole selected candidate and separately review marketplace listing, or (b) authorize a new versioned company shape with precise additional roles/aliases and independent validation. This design chooses **neither** automatically and grants no executive authority.
- The startup template also contains `ownerApprovalOfficeIds=["ceo"]` but the selected company manifest has none. Neither field grants real owner approval authority.

## 6. Evidence and outstanding gates

- G: `.pmcro/trails/trail-seed-0033-control-plane-role-admission/disposition.json` records HALT and points to unexecuted Seed 0034. The G: Seed 0032 `source-verification.json` finds no existing positive RoleWorkerService admission provider or authorized role binding/policy selection.
- G: `.pmcro/trails/trail-operate-company-20261018-2232/commissioning-and-capability-status.json` is a *historical observation* of registrations, two stored grants and one active-record labeled C-suite. It expressly quarantines their authority; no non-quarantined effective grant is established.
- D: `local/projects/ProjectName/.pmcro/activation-state.json` says `productionFrameworkAuthority=not-granted`, `independentChecker=PENDING` for that historical project snapshot.
- The newer handoff's Trail `7f5df6f6-ead6-4568-9e44-2236f2d909f2` and admitted Seed `0ba5451d-db28-4bef-8302-b3f7ba93c7b9` were not located in the inspected D:/G: searchable evidence; its candidate digest appears inside the historical G: commissioning status, not as verified current entitlement.
- G: `AgentExecutionGrant` contract reused in D: does not have a signed expiry; do not claim time expiry enforcement.
- **Remaining before implementation:** owner-selected role binding, actual registered/activated candidate tied to non-quarantined verified grants, policy artifact owner/deployment/checksum, identity compatibility, integration-scope decision for office list, independent authenticated Checker, and a trusted runtime host. No bootstrap bypass is permitted.

## 7. Acceptance and readiness

See [INTEGRATION-ACCEPTANCE-TESTS.md](INTEGRATION-ACCEPTANCE-TESTS.md) for detailed executable-intent test cases. They are a candidate **test plan**, not tests already executed. Until owner decisions and actual trusted host inputs exist, positive production runtime tests are blocked by design.

## 8. Proposed PMCR-O Trail Frame — NOT persisted

```yaml
schema: pmcro.trail-frame.proposed
state: proposed
persisted: false
phase: REFLECT
declared_i_am: "I AM the Orchestrator Agent."
authenticated_agent_identity: not_established
intent: integrate G company design with existing D control-plane interfaces without authority mutation
sources:
  design_root: "G:\\workspace\\pmcro-company-platform"
  host_root: "D:\\AgentSystem"
  g_halt_trail: "trail-seed-0033-control-plane-role-admission"
  g_unexecuted_seed: "seed-0034-authorize-role-admission-inputs.md"
result:
  design_specification: authored_candidate
  runtime_implementation: not_executed
  independent_checker: pending
  authority: unchanged
  outcome: LOOP
evidence_class: design_review_not_authentication
governance:
  source_merge: false
  registration: false
  grant_issuance: false
  key_provision: false
  activation: false
  model_or_worker_execution: false
next_seed:
  state: proposed_unexecuted
  command: supplied_once_in_cycle_summary
```

**No Trail ID, signed receipt, Checker PASS or authority claim is minted by this candidate document.**