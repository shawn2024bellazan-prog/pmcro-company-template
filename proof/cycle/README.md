# Deterministic PMCR-O five-phase native MAF proof

Verified actual execution: [run 45118225215](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45118225215).

The .NET 10 process invokes five native `AIAgent.RunAsync` phase processors, in order: orchestrate → plan → make → check → reflect. Every processor is bound to an existing authored SKILL.md and its SHA-256; every handler invocation is recorded separately. Check verifies the test artifact and Reflect returns PASS with the next seed held for Orchestrator authorization.

An independent Python checker validates all five invocation records, frame ordering and semantics, source hashes and the hash-linked chain. Trail head from the passing run: `b94c19dc3f1dbd77b7a3ed285c0d7f9b548d05509faee8db013c98d9d8845c14`.

A follow-up hardening commit added a negative tamper test that alters a copy of the Make frame. That test must fail verification to be accepted; inspect its GitHub run evidence before claiming it passed.

Scope: deterministic phase invocations, not autonomous model reasoning, production domain tools, or separately deployed phase agents.

Run: `bash proof/cycle/run-cycle-proof.sh` on a .NET 10 SDK machine.
