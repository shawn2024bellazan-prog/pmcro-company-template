# Cognitive Loop

`SeedIntent → Plan → Make → Check → Reflect → {Accept | Loop | Escalate}`

- Planner returns a minimal plan and acceptance criteria.
- Maker executes authorized steps and returns evidence.
- Checker independently verifies results and policy compliance.
- Reflector recommends a verdict and evidence-based constraints.
- Orchestrator enforces hard guards and owns continuation.

`LOOP` retries recoverable failures within the cycle budget. `ACCEPT` with objective incomplete continues automatically. `ACCEPT` with objective complete stops. `ESCALATE`, policy violations, cancellation, and exhausted retry budgets stop the affected run.

MAF and Aspire integration are intentionally adapter seams for a later increment; this scaffold does not claim those integrations are complete.
