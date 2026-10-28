# PMCR-O make plan: caller-returned next seed

Approved user command: `@maker /pmcro:make implement_next_seed_contract`.

Scope: develop an independent, compile-ready reference component on the
`feat/pmcro-activation` branch. No default-branch writes or deployment.

Named file targets:

- `src/PMCR.O.Continuation/PMCR.O.Continuation.csproj`
- `src/PMCR.O.Continuation/Contracts.cs`
- `src/PMCR.O.Continuation/CallerContinuationHandler.cs`
- `src/PMCR.O.Continuation/InMemorySeedClaimStore.cs`
- `tests/PMCR.O.Continuation.Tests/PMCR.O.Continuation.Tests.csproj`
- `tests/PMCR.O.Continuation.Tests/Program.cs`
- `scripts/validate.ps1`
- `docs/architecture/next-seed-return-contract.md`
- `docs/decisions/0002-next-seed-return-make-plan.md`
- `.pmcro/trails/orchestrator/2026-10-28-next-seed-make.json`

Proof obligations: preserve caller return routing and correlation; reject
unverified evidence and inherited authority; enforce recursion and retry
bounds; prevent duplicate dispatch; record denial and uncertain execution;
stop dispatch on Trail write failure. Run the PowerShell validation script and
record honestly when .NET or PowerShell is unavailable.

Checker disposition: pending an independent check. Promotion and deployment
are outside this maker operation.
