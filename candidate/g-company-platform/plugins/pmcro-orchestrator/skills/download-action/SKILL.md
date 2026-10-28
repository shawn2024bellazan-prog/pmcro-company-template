---
name: download-action
description: Candidate skill to inspect and stage only SHA-256 pinned, explicitly approved GitHub action artifacts locally without installing or executing them.
compatibility: Microsoft Agent Framework Agent Skills
metadata:
  kind: skill
  runtime: maf-native
---
# pmcro-orchestrator:download-action

Lifecycle: **candidate-only; not registered, granted, activated or published**.

Owner agent: pmcro-orchestrator. A skill and marketplace record do not grant authority.

## Operation

Requires an exact approved HTTPS URL on one of the fixed GitHub hosts, independently obtained SHA-256, safe filename, and preexisting folder named `candidate-action-downloads`. The default command is inspect (no network). Verification reads local file bytes only.

```powershell
pwsh -NoProfile -File scripts/download-action.ps1 -Mode inspect
pwsh -NoProfile -File scripts/download-action.ps1 -Mode verify -File .\artifact.zip -Sha256 <64hex>
pwsh -NoProfile -File scripts/download-action.ps1 -Mode stage -Url https://github.com/OWNER/REPO/releases/download/TAG/FILE -Sha256 <64hex> -Filename artifact.zip -StageDir G:\workspace\pmcro-company-platform\artifacts\candidate-action-downloads -ApproveStage
```

Only a verified byte-identical file can be moved into the dedicated staging directory; no extraction, installation, process execution, registration, grants, or activation. CLI flags are not an authenticated PMCR-O grant or Checker verdict.

## Constraints

Reject file://, HTTP, credentials in URLs, untrusted hosts or redirects, mismatched hashes, unsafe filenames, oversized artifacts, and existing destinations. Without a source URL and SHA-256, no download occurs. A downloaded artifact is untrusted until independently verified and admitted by the authenticated host.

## Offline tests

```powershell
pwsh -NoProfile -File tests/test-download-action.ps1
```