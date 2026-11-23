# `/init` recovery implementation: provenance

The installed `pmcro-commands:init` handler referenced three modules unavailable in the installed bundle: `config.py`, `profile_builder.py` and `audit.py`.

The handler at `plugins/pmcro-commands/skills/init/scripts/init.py` is a verbatim copy of the installed skill's Python source. The companion modules at `plugins/pmcro/skills/pmcro-agent/scripts/` are **new source-derived recovery code**, not recovered original upstream code.

The recovery code discovers seven actual skill definitions already authored in the two source plugins; stages `.pmcro/` atomically; audits its domains and copied file hashes; records SHA-256 source inventory, a profile manifest and a hash-sealed receipt. An additional independent verifier checks the receipt, profile and all inventoried artifact bytes.

Real automated evidence: [PMCR-O initialization run 45116577877](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45116577877), containing five passing negative and positive tests and an actual verified profile generated from this repository source.

The original upstream modules remain absent. This recovery is a newly implemented compatible profile generator, not proof of historical implementation equivalence.

**Boundary:** `/.pmcro` is managed configuration, not a live MAF agent or A2A service. This initialization does not execute the PMCR-O model agents. The independent .NET MCP proof is recorded under `proof/`.
