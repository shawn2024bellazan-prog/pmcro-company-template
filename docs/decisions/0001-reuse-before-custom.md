# ADR 0001: Reuse Before Custom Implementation

- Status: Accepted
- Scope: All PMCR-O products, agents, tools, adapters, and enterprise deployments

## Decision
PMCR-O uses a reuse-first engineering policy. Before building a custom capability, evaluate existing repository code, framework/platform features, maintained libraries, standard protocols, and enterprise services. Record material decisions and the reason for custom code.

## Rationale
This reduces duplicate code, security exposure, maintenance burden, integration drift, and delivery time while preserving PMCR-O's differentiated value in orchestration, governance, Trail evidence, and recursive improvement.

## Guardrails
Reuse is conditional on license, security, maintenance, compatibility, and enterprise requirements. Vendor-specific implementations should sit behind adapters when that enables meaningful portability. Dependencies must be pinned and scanned; critical outputs remain subject to PMCR-O policy and independent verification.

## Consequences
- Every material capability has a build-vs-buy record.
- CI should validate dependency metadata and run available security/license checks.
- Custom code requires a documented gap that existing options cannot reasonably fill.
- Re-evaluate choices when maintenance, licensing, security, or platform compatibility changes.
