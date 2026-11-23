# PMCR-O deterministic five-phase native MAF proof

First bounded proof that dispatches all five named PMCR-O roles in strict order using five real `AIAgent.RunAsync` invocations.

Every native phase agent loads its corresponding authored source `SKILL.md` by path and records its SHA-256. The phases execute actual deterministic work: admit one seed, produce READY plan, create a concrete artifact, independently check its content/hash, and reflect with STOP and next seed requiring Orchestrator authorization.

Five source-hash-bound output frames are linked by SHA-256 and each correlated to a separate native `RunCoreAsync` invocation record. The independent Python checker rejects source, chain, phase order, semantic, or handler-record inconsistencies.

This is a deterministic phase **execution proof**. It is not five independently hosted A2A services, does not run any LLM, does not perform production domain work, and does not demonstrate multi-agent reasoning or a complete autonomous product lifecycle. Future increments can replace bounded processors with real registered PMCR-O subject agents.

Run with `bash proof/cycle/run-cycle-proof.sh` on .NET 10 SDK. The hosted GitHub Actions workflow runs the proof and uploads evidence.
