# PMCR-O Company Template

Minimal PMCR-O cognitive-loop **authoring source**, based on the existing `pmcro` and `pmcro-orchestrator` plugins from [pmcro-marketplace-skills](https://github.com/shawn2024bellazan-prog/pmcro-marketplace-skills).

Authoritative authored source lives in `AGENTS.md` and `plugins/`; generated `.pmcro/` configuration is a managed projection and must not be committed.

## Components

- `plugins/pmcro`: the five core cognitive-phase skill definitions.
- `plugins/pmcro-orchestrator`: goal admission and cycle-zero decomposition.
- `plugins/pmcro-commands/skills/init/scripts/init.py`: unchanged installed `/init` handler.
- `plugins/pmcro/skills/pmcro-agent/scripts/`: **new recovery implementations** of the three dependencies missing from the installed `/init` package. They are not falsely described as the original upstream modules.
- `scripts/verify_profile.py`: independent SHA-256 verification of generated receipts and files.
- `proof/`: separate official .NET MCP C# SDK stdio client/server exercise.

## Recorded execution evidence

- PMCR-O `/init`: [GitHub Actions PASS](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45116577877). Seven projected skills, 11 domains, five memory partitions, 22 artifact hashes, and an independently checked receipt. Idempotence and negative tamper tests pass.
- MCP client/server: [GitHub Actions PASS](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45115843847). Actual `initialize`, `tools/list`, and `tools/call Add(2,3)` returned `5`.

**Important:** Source initialization and MCP execution are different tests. Neither demonstrates a running native PMCR-O agent, MAF orchestrator, or A2A delegation (`NOT_INVOKED`).

See `docs/initialization-status.md` and `docs/init-recovery-provenance.md` for the boundaries.
