# PMCR-O ecosystem product map

Products share versioned contracts but may ship independently. A product can be a library, service, CLI, agent pack, workflow, hosted capability, or marketplace package; it does not need to become a separate microservice by default.

## Shared platform

| Product/capability | Responsibility | First contract |
|---|---|---|
| Enterprise Trail Frames | Common evidence, lineage, policy, and learning envelope | `schemas/trail-frame.schema.json` |
| Intent Queue | Durable raw intent, refined objective, lifecycle, recovery | Existing `IntentQueue` contract |
| Recursive Cycle Guard | Child-run identity, budgets, self-target detection, rollback/checker gates | `RecursiveCycleContext` |
| Governance & Approvals | Policy evaluation, authorization evidence, human approval lifecycle | `IApprovalPolicy` plus approval events |
| Capability Registry | Shared package manifest and compatibility metadata | To define after core contracts stabilize |

## Independently shippable product candidates

1. Trail Auditor — query and reconstruct decisions, actions, approvals, and evidence.
2. Trail Replay & Recovery — resume safe interrupted work with idempotency and reauthorization.
3. Trail Analytics — measure outcome quality, costs, repeated failures, and drift.
4. Trail Learning Factory — curate observations into evaluated lessons, skills, and workflows.
5. Agent/Skill Factory — generate, register, execute, test, and version capabilities.
6. Capability Marketplace — discover and manage packages across federated registries.
7. GitHub Integration Pack — repository discovery, PR/CI evidence, dependency and release checks.
8. VS Code/Client Adapters — portable manifests plus client-specific installation/runtime adapters.
9. Aspire Deployment Pack — compose and observe polyglot .NET/Python services.
10. Enterprise Control Plane — tenant boundaries, access, retention, export, and administration.

## Product acceptance gates

Every product must declare owner, purpose, contract version, supported runtimes, permissions, data classification, license/provenance, dependency inventory, health checks, tests, rollback/disable behavior, and Trail Frame events. Marketplace trust does not substitute for PMCR-O authorization. Products should remain libraries/modules until independent deployment provides a concrete benefit.


## Marketplace and capability integration (Increment 9)

- Unified capability/source contracts: `src/PMCR.O.Core/CapabilityRegistry.cs` and `CapabilityIntegrationContracts.cs`.
- Source catalog references: `manifests/marketplace-sources/catalog.json`.
- Registration examples: `manifests/capability-registrations/examples.json`.
- Schema: `schemas/capability.schema.json`.
- Architecture and boundaries: `docs/architecture/marketplace-capability-integration.md`.
- Status: inventory/contracts and policy-gated executor implemented in source; live source adapters and external protocol transports remain pending.
