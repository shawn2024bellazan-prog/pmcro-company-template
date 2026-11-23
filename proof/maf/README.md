# Native Microsoft Agent Framework AIAgent execution proof

The .NET 10 console program loads its authored JSON definition, derives SHA-256 for the definition and referenced PMCR-O phase skill, instantiates a real Microsoft.Agents.AI.AIAgent subclass, and calls AIAgent.RunAsync and AIAgent.RunStreamingAsync. It emits an independently verified invocation record for each core method.

Package: Microsoft.Agents.AI.Abstractions 1.28.0. No paid provider and no simulated results. This proves native framework agent invocation, not LLM inference, PMCR-O full five-phase orchestration, or A2A integration into MAF. The A2A task proof remains independently hosted in proof/a2a.

Run: bash proof/maf/run-maf-proof.sh
