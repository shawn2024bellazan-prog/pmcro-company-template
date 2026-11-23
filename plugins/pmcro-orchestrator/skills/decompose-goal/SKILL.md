---
name: decompose-goal
description: >-
  Cycle-0 skill: turn one high_level_goal into discrete constraints with
  acceptance criteria. Artifact must pass Check before downstream routing.
  Does not execute domain work.
license: MIT
compatibility: agentskills.io, MAF, PMCR-O
metadata:
  phase: orchestrate
  plugin: pmcro-orchestrator
  cycle: 0
---

# Decompose goal

Orchestrate invokes this skill. This **is** the decomposition.

## Input

- `high_level_goal`
- Inherited laws / constraints

## Output

```yaml
constraints:
  - id: c1
    statement: "..."
    acceptance: "..."
    out_of_scope: []
```

## Rules

1. Discrete, routable constraints only.
2. Each has an independently checkable acceptance string.
3. No domain execution (no project scaffolding here).
4. Checker must PASS the list before orchestrate routes any constraint.

## Progressive disclosure

Weak/local models: load `references/decomposition-patterns.md`.
