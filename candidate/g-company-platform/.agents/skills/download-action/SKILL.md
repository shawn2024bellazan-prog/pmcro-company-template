---
name: download-action
description: Stage an explicitly human-approved GitHub-hosted action archive by HTTPS and exact SHA-256 into a candidate folder; never install or execute.
compatibility: Microsoft Agent Framework Agent Skills
metadata:
  kind: skill
  runtime: maf-native
---

# download-action — candidate only

The Orchestrator may propose this skill from the marketplace; no discovery event grants permission to run it. Scripts are optional, manually invoked tools with progressive disclosure. They cannot register, grant, activate, install, unpack, or execute a downloaded action.

## Workflow
1. Inspect candidate metadata with `node scripts/download-action.cjs --mode inspect` (Node.js is the verified local runtime; the older Python script is retained for compatibility but its local Python interpreter is currently incomplete).
2. Supply an *independently verified* expected SHA-256 and an HTTPS URL from an approved GitHub host.
3. Create a designated isolated `candidate-action-downloads` folder outside protected control-plane directories.
4. After explicit human approval for that single file, invoke `node scripts/download-action.cjs --mode stage --url URL --sha256 HEX --filename ARCHIVE --stage-dir PATH --approve-stage`.
5. Verify and separately authorize installation via the existing host; **this skill never installs or executes**.

Verified local test: `node --test tests/download-action.test.cjs`.

No action URL, digest or owner approval has been supplied for a live download. The script's command-line approval flag is not an authenticated host grant.