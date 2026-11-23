# PMCR-O A2A worker discovery, task and native MAF delegation

**PASS:** [GitHub Actions run 45117764473](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764473).

The source-bound `pmcro-echo-worker` definition loads from `agents/pmcro-echo.definition.json`, with its referenced PMCR-O orchestrator SKILL.md verified by SHA-256. Official A2A ASP.NET Core server exposes a well-known Agent Card and the JSON-RPC endpoint `/a2a/pmcro-echo`.

An independent `A2ACardResolver` and `A2AClient` discovers the card, invokes an actual task, retrieves it in Completed state, and reads the artifact. The A2A handler delegates execution to `PmcroMafInnerAgent : AIAgent` and publishes its native MAF `RunAsync` output. The Python evidence gate checks matching task IDs, hashes, and two runtime invocation records.

Actual result: `PMCRO-WORKER|pmcro-echo-worker|seed-a2a-proof-001`.

The proof uses official A2A .NET SDK packages and `Microsoft.Agents.AI.Abstractions` on .NET 10. No model credentials needed. A2A streaming and the full PMCR-O five-phase cognitive chain are not part of this test.

Run on a .NET 10 SDK host: `bash proof/a2a/run-a2a-proof.sh`.
