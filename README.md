# PMCR-O Company Template

Minimal .NET-first PMCR-O cognitive-loop authoring source. Authored source remains in `AGENTS.md` and `plugins/`; `.pmcro/` is a managed output and is never authoritative or committed.

## Installed component source

- `plugins/pmcro`: the five core phase skills, `orchestrate`, `plan`, `make`, `check`, and `reflect`.
- `plugins/pmcro-orchestrator`: high-level goal admission and constraint decomposition.
- `plugins/pmcro-commands/skills/init/scripts/init.py`: unchanged installed `/init` executable.
- `plugins/pmcro/skills/pmcro-agent/scripts/`: new, explicitly labeled recovery dependencies for `/init`. These are not represented as the missing original upstream modules.
- `proof/`: independent native C# MCP, A2A, MAF and integrated A2A-to-MAF runtime exercises.

## Real execution evidence on GitHub Actions (November 23, 2026)

| Gate | Result | Evidence |
| --- | --- | --- |
| `/init` managed profile | PASS | [run 45117764412](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764412): seven skills, 11 domains, five memory partitions, independent receipt verification |
| Official .NET MCP | PASS | [run 45117764347](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764347): initialize, tools/list, Add(2,3) = 5 |
| Native Microsoft Agent Framework | PASS | [run 45117764406](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764406): real AIAgent.RunAsync and RunStreamingAsync |
| A2A to native MAF | PASS | [run 45117764473](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764473): Agent Card, JSON-RPC, task artifact and same-task MAF handler evidence |

## Critical boundaries

All proofs ran in an ephemeral GitHub Actions runner, not on a Windows host or a production deployment. The A2A worker is deterministic and uses a native MAF AIAgent internally, but does not use a generative model. The actual PMCR-O five-phase cognitive execution with separately invoked orchestrator/planner/maker/checker/reflector roles remains **NOT_INVOKED**. Its invocation and trails must be independently proved as a distinct gate.

See `docs/initialization-status.md`, `docs/init-recovery-provenance.md`, and `docs/a2a-maf-bridge.md`.
