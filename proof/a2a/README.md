# PMCR-O A2A worker discovery and task proof

This bounded proof exercises the official A2A .NET SDK v1 directly, with no LLM credentials and no hand-written protocol transport.

- Authored definition: agents/pmcro-echo.definition.json, bound to the PmcroEchoWorker C# handler.
- Server loads that exact definition and SHA-256 hashes its associated PMCR-O orchestrator SKILL.md.
- Well-known Agent Card is available from /.well-known/agent-card.json and advertises the /a2a/pmcro-echo endpoint.
- Separate .NET client uses A2ACardResolver to discover the card and A2AClient to send a task and retrieve the completed artifact.
- An independent Python checker correlates the network result, Agent Card, source hashes and server-side handler execution record.
- A successful run proves A2A discovery, task invocation and artifact delivery. It does not prove Microsoft Agent Framework native orchestration, LLM inference, multi-agent delegation, or PMCR-O five-phase execution. Those remain NOT_INVOKED.
- Based on the official a2aproject/a2a-dotnet AgentServer and AgentClient samples, with A2A 1.0.0-preview4 and A2A.AspNetCore 1.0.0-preview4.

Run with bash proof/a2a/run-a2a-proof.sh on a .NET 10 environment.
