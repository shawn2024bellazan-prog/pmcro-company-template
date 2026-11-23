---
name: orchestrate
description: >-
  Admit or deny a high_level_goal, mint or link a trail, and either run cycle 0
  (decompose into discrete constraints) or route an already-decomposed constraint
  into plan. Sole cycle initiator. Never executes domain work, never merges
  constraints, never issues PASS/LOOP/HALT.
license: MIT
compatibility: agentskills.io, MAF, PMCR-O
metadata:
  phase: orchestrate
  pmcro: true
---

# Orchestrate

## When I am used

- A new `high_level_goal` arrives and must enter the lifecycle.
- Cycle 0 decomposition is required before downstream cycles.
- A later cycle must be authorized from a Reflector proposal (I authorize; runner does not auto-consume).

## What I own

- Cycle admission / denial
- Trail mint-or-link
- Cycle 0 goal: *decompose high_level_goal into discrete constraints*
- Routing of pre-decomposed constraints into subsequent cycles
- Hand-off to Plan only after admission

## What I never do

- Domain execution (that is Make)
- Merge or re-derive constraints after cycle 0
- Issue Checker verdicts
- Auto-start the next cycle without Orchestrator authorization

## Process (phases → tasks → steps)

### Phase: orchestrate

**Task A — Admit intent**
1. Read `high_level_goal` and applicable laws.
2. Resolve from validated context or HALT (no clarification gate).
3. Record admission or denial on the trail.

**Task B — Trail**
1. Mint a new trail or link an existing one.
2. Write orchestrate frame (cycle id, goal ref, constraint scope if any).

**Task C — Cycle 0 or route**
- If no verified constraint list exists:
  1. Set cycle goal to: decompose `high_level_goal` into discrete constraints.
  2. Hand off to Plan (same O→P→M→C→R).
  3. Decomposition artifact must pass Check before any downstream cycle.
- If a Checker-verified constraint list exists:
  1. Select the next constraint (do not rewrite it).
  2. Hand off to Plan for that constraint only.

## Progressive disclosure

- Strong models: this `SKILL.md` is enough.
- Weak / local models: load `references/constraints-and-routing.md` and any scripts under `scripts/` if present.

## Output contract

- Trail orchestrate frame written
- Either: cycle 0 decomposition in progress, or next constraint routed to Plan
- On failure: structured HALT with reason (no invented capabilities)

## Success criteria (for Checker on cycle 0)

- Constraint list is discrete, non-overlapping where required by laws
- Each constraint is routable as its own later cycle
- No domain work performed in orchestrate frames
