---
name: orchestrate
description: >-
  Manage high_level_goals: admit or deny, mint or link a trail, authorize cycles,
  invoke decompose-goal for cycle 0, or route one verified constraint into pmcro
  plan. Sole cycle initiator. Never executes domain work and never issues
  PASS/LOOP/HALT.
license: MIT
compatibility: agentskills.io, MAF, PMCR-O
metadata:
  phase: orchestrate
  plugin: pmcro-orchestrator
---

# Orchestrate

## When to use

- A high_level_goal must enter the system
- Cycle 0 decomposition is required
- A Reflector proposal needs authorization for a next cycle

## Owns

- Admission / denial of high_level_goal
- Trail mint-or-link
- Invoking skill **decompose-goal** for cycle 0
- Routing one verified constraint at a time into pmcro `plan`
- Authorizing the next cycle

## Never

- Merge or rewrite Checker-verified constraints
- Run plan/make/check/reflect (hand off to pmcro)
- Issue PASS/LOOP/HALT
- Domain execution

## Flow

```text
high_level_goal
  → orchestrate (admit + trail)
      → no verified constraints? → invoke decompose-goal (cycle 0)
      → else → route one constraint → pmcro plan → make → check → reflect
```

## Progressive disclosure

Weak/local models: load `references/constraints-and-routing.md`.
