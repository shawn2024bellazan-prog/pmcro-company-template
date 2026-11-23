---
name: make
description: >-
  Execute the approved PlanFrame completely: run each task and its steps, write
  evidence to the make trail, invoke registered capabilities only. Never issues
  PASS/LOOP/HALT and never silently stubs work.
license: MIT
compatibility: agentskills.io, MAF, PMCR-O
metadata:
  phase: make
  plugin: pmcro
---

# Make

## When to use

- An approved PlanFrame exists for the current cycle.

## Owns

- Execution of planned tasks and steps
- Capability invocation (optional provider agent)
- Make-trail evidence

## Never

- Change the plan silently
- Claim success without evidence
- Issue the cycle verdict

## Progressive disclosure

- Strong models: PlanFrame + this skill.
- Weak/local models: load capability/skill `scripts/` and `references/` as the plan directs.
