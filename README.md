# PMCR-O Company Template

Minimal .NET-first PMCR-O cognitive-loop authoring source. `AGENTS.md` and `plugins/` are authored; `.pmcro/` is a managed generated projection.

The five original PMCR-O phase skills and two orchestrator skills are source-pinned from the existing marketplace. The installed `/init` handler is preserved verbatim; its three missing dependencies were **newly implemented recovery modules**, not recovered originals.

## Real GitHub Actions execution evidence — November 23, 2026

| Gate | Evidence |
| --- | --- |
| Managed `/init` profile | [PASS](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764412) — 7 skills, 11 domains, 5 memory partitions, independently verified receipt |
| .NET MCP C# SDK | [PASS](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764347) — initialize, tools/list, Add(2,3)=5 |
| Native Microsoft Agent Framework | [PASS](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764406) — AIAgent.RunAsync and RunStreamingAsync |
| A2A to native MAF | [PASS](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764473) — Agent Card, A2A task, real inner MAF invocation, artifact |
| Five-phase native MAF cycle | [PASS](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45118225215) — 5 actual AIAgent.RunAsync calls, source-bound phases, SHA-256-linked trail |

## Scope boundary

The five phase agents are **deterministic MAF processors**. They actually execute seed admission, plan generation, concrete test artifact creation, hash validation and reflection, but not generative inference, production tool use or five independently hosted agents. All verified executions occurred on ephemeral GitHub Actions runners—not a user Windows host. The full production PMCR-O agent company remains a separate implementation goal.

See `docs/initialization-status.md`, `docs/init-recovery-provenance.md`, `docs/a2a-maf-bridge.md`, and `proof/cycle/README.md`.
