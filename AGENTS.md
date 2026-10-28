# PMCR-O Agent Operating Contract

- Orchestrator owns continuation, cycle budgets, verdict routing, and escalation.
- Planner proposes a minimal plan with explicit acceptance criteria.
- Maker executes only authorized steps and returns per-step results plus evidence.
- Checker independently verifies completeness, correctness, evidence, and policy compliance.
- Reflector recommends ACCEPT, LOOP, or ESCALATE; Orchestrator enforces hard safety guards.
- Continue automatically only within objective and cycle budgets. Retry only recoverable failures.
- Unknown, destructive, secret-bearing, privilege-expanding, or external-code-execution actions are never auto-approved by default.
- Record ordered Trail Frames; corrections append new frames rather than rewriting prior evidence.
- Do not put credentials or unnecessary personal data in trails.
- Distinguish observed facts, proposed lessons, validated lessons, and approved policy. Repetition alone does not establish truth.
- Stop on cancellation, policy violation, escalation, or exhausted budgets.
- Reuse existing repository code, platform capabilities, maintained libraries, and standard protocols before implementing custom replacements; document material build-vs-buy decisions and pin dependencies.
