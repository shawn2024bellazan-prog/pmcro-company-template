# PMCR-O execution ledger

- `/init`: **PASS** — [run 45117764412](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764412). Managed configuration generated in CI and verified. Original installed handler unchanged; three dependencies are explicitly new recovery modules, not lost original source.
- MCP: **PASS** — [run 45117764347](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764347), genuine C# protocol client/server Add(2,3) returned 5.
- Native MAF direct invocation and streaming: **PASS** — [run 45117764406](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764406).
- A2A→native MAF task delegation: **PASS** — [run 45117764473](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764473), task `603414bd085e4b03a33ff237445d6bab`, independent task/source/hash correlation.
- Five-phase deterministic MAF execution: **PASS** — [run 45118225215](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45118225215). Five native RunAsync invocations, strict orchestrate/plan/make/check/reflect sequence, five verified source-bound trail frames, trail head `b94c19dc3f1dbd77b7a3ed285c0d7f9b548d05509faee8db013c98d9d8845c14`.
- Negative tamper rejection for the five-phase trail has been added as a separate CI acceptance check. Its result must be read from the subsequent CI run, not inferred from its presence.

## Not established

- Autonomous generative LLM-driven agent reasoning and complete domain-task execution.
- Independently hosted five A2A subject agents or A2A streaming across them.
- User Windows-host installation or deployment to production.

All generated profile and proof files are CI outputs; the repository stores authored source and test definitions, not live `.pmcro/` state.
