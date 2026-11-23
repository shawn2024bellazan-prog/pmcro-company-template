# PMCR-O Company Template — Agent Contract

## Source of truth
The **authored** cognitive loop lives in `AGENTS.md` and `plugins/`. This repository bundles only the core `pmcro` and high-level-goal `pmcro-orchestrator` plugins, pinned from `shawn2024bellazan-prog/pmcro-marketplace-skills` commit `9093b91df2527a7fbb0d390e90b8d04775077587`.

## Responsibility boundary
- `pmcro-orchestrator:orchestrate` handles high-level goal admission, trail mint/link, cycle-zero `decompose-goal`, and routing one approved constraint at a time.
- `pmcro` provides `orchestrate → plan → make → check → reflect` skills; it owns the core cognitive phases.
- Orchestrator does not perform domain work, issue Checker verdicts, or silently run another cycle.
- These are skill definitions. Installing them does **not** demonstrate that a native MAF, MCP, A2A, or PMCR-O process ran.

## Managed outputs
The authoritative PMCR-O `/init` executable creates `.pmcro/` as a managed projection. Do not commit `.pmcro/`, fabricate an initialization receipt, or treat this bootstrap as a successful runtime invocation. `.agents/` discovers skills; `.csuite/` is a separate optional organizational layer.

## Evidence rules
For every phase record actual invocation, returned result, verification, and a trail reference, or mark it `NOT_INVOKED`/`BLOCKED`. Read `docs/initialization-status.md` before executing the installer.
