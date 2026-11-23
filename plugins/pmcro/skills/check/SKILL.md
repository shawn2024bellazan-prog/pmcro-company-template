---
name: check
description: >-
  Independently score the cycle against PlanFrame success criteria and laws.
  Emit PASS, LOOP, or HALT with evidence. Never repairs artifacts and never
  re-executes Make work.
license: MIT
compatibility: agentskills.io, MAF, PMCR-O
metadata:
  phase: check
  plugin: pmcro
---

# Check

## Owns

- Verdict: PASS | LOOP | HALT
- Evidence bound to acceptance criteria

## Never

- Repair or re-make artifacts
- Treat rehearsal/simulated PASS as verified execution
