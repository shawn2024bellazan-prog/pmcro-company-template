---
name: plan
description: >-
  Turn an admitted cycle goal (or one discrete constraint) into a minimal PlanFrame:
  ordered tasks with subject agents, success criteria, and out_of_scope. Appends to
  plan trail only. Never executes steps and never issues PASS/LOOP/HALT.
license: MIT
compatibility: agentskills.io, MAF, PMCR-O
metadata:
  phase: plan
  plugin: pmcro
---

# Plan

## When to use

- Orchestrator admitted a cycle and handed off a goal or single constraint.
- A Checker-testable plan is required before Make.

## Owns

- PlanFrame: goal, ordered tasks, success criteria, out_of_scope
- Trail frames under `plan/` only

## Never

- Execute tasks/steps (Make)
- Issue verdicts (Check)
- Decompose high_level_goal (skill `decompose-goal` in pmcro-orchestrator)
- Invent unregistered capabilities

## Steps

1. Load laws, trail, validated resources only.
2. Name the minimum tasks for this cycle goal.
3. Attach independently checkable success criteria per task.
4. Write PlanFrame; stop.
