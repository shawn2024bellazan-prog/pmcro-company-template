# PMCR-O A2A → native MAF bridge: verified

Hosted end-to-end execution: [GitHub Actions run 45117764473](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764473). Status **PASS**.

The official A2A .NET SDK exposes the Agent Card for `pmcro-echo-worker`, receives a JSON-RPC task, and routes it into `PmcroEchoWorker`. The handler instantiates a real Microsoft Agent Framework `AIAgent` subclass `PmcroMafInnerAgent`, invokes `RunAsync` (native `RunCoreAsync`), and publishes its returned message as the A2A artifact. An independent client retrieves the completed task.

- Actual task ID: `603414bd085e4b03a33ff237445d6bab`.
- Returned artifact: `PMCRO-WORKER|pmcro-echo-worker|seed-a2a-proof-001`.
- Agent definition SHA-256: `ad920eb0eef60af4e9142a918137a4a5acdfc2809406b11a1d770faccb339b90`.
- Authored source skill SHA-256: `5c88acdf545dc1d25a51354a60366cb5813a436ccb39762f22759d85108e5050`.
- Independent verifier: **PASS**, including `native_maf_aiaagent_invoked_for_a2a_task=true` and matching task ID in A2A and MAF invocation records.
- Uploaded GitHub Actions artifact: `pmcro-a2a-discovery-task-evidence`.

Boundary: A2A owns transport, discovery, task lifecycle and artifacts. MAF owns the inner agent invocation. This is deterministic, model-free proof—not a complete PMCR-O five-phase cycle, multi-agent task handoff, or A2A SSE streaming proof.

The two SDKs are reused directly. No custom A2A transport protocol was implemented.
