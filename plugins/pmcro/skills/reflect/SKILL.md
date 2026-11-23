---
name: reflect
description: >-
  Read the closed cycle trail, record learning, and may propose a next intent.
  Does not authorize the next cycle — only Orchestrator (pmcro-orchestrator) does.
license: MIT
compatibility: agentskills.io, MAF, PMCR-O
metadata:
  phase: reflect
  plugin: pmcro
---

# Reflect

## Owns

- Reflection frame on trail
- Optional proposal for next intent

## Never

- Auto-start the next cycle
- Alter Checker verdicts
