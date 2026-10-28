---
name: download-action
description: Inspect, verify, or stage a manually approved SHA-256-pinned GitHub action artifact without installing or executing it.
compatibility: Microsoft Agent Framework Agent Skills
metadata:
  lifecycle: candidate-only
---
# PMCR-O local action download

Candidate Agent Skill with progressive disclosure. Nothing runs when VS Code discovers this file.
No signature, grant, approval, plugin activation, or Checker PASS is implied by discovery.

First read this file. Inspect scripts/download-action.cjs only when needed.
Commands:
- node scripts/download-action.cjs --mode inspect
- node scripts/download-action.cjs --mode verify --file PATH --sha256 SHA256HEX
- node scripts/download-action.cjs --mode stage --url EXACT_GITHUB_HTTPS_URL --sha256 SHA256HEX --filename FILE --stage-dir EXISTING_candidate-action-downloads --approve-stage

A human must separately select and approve exact URL and digest. The CLI flag alone is NOT authenticated PMCR-O authority.
Stage uses HTTPS with checked redirect hosts, 50 MB limit, SHA-256 verification and no-overwrite creation.
No archive extraction, installation, code execution, registration, grant, activation or marketplace import.
Tests: node --test tests/download-action.test.cjs