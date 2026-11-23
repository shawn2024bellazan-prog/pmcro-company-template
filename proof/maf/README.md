# Native Microsoft Agent Framework AIAgent execution

**PASS:** [direct MAF run 45117537243](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117537243) and [integrated A2A → MAF run 45117764473](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45117764473).

The standalone .NET 10 program loads a SHA-256-bound agent definition and an existing PMCR-O phase skill. It instantiates `PmcroNativeEchoAgent : AIAgent` and invokes the real `AIAgent.RunAsync` and `AIAgent.RunStreamingAsync` APIs, recording two actual core method invocations. The independent verifier correlates the runtime result with both source hashes.

The integrated A2A exercise is in `proof/a2a/`: its A2A task handler invokes a separate native `PmcroMafInnerAgent : AIAgent`, returns the native response as an A2A artifact, and verifies both runtime invocation records for the same task.

These are deterministic agent-runtime tests. Generative model inference and the complete five-phase PMCR-O cognitive cycle remain NOT_INVOKED.

Run standalone: `bash proof/maf/run-maf-proof.sh`.
