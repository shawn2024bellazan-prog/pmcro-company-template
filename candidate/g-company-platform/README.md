# G company candidate snapshot — DEVELOPMENT ONLY

This tree mirrors bounded candidate integration and a proposed local action downloader from the owner-selected G: design workspace. It does not replace the host source on D: or integrate these files into the PMCR-O .NET solution. No registration, grant, activation, worker, model execution, release, merge, or production promotion is implied by this commit.

## Source and reproducible evidence

- `plugins/pmcro-runtime/src/ProjectName.Contracts/CandidateCompanyIntegration.cs` — SHA-256 `1ef4befadfb203c26c78a097b34627e41f381e89ff67fd7a181e8058da62b571`.
- `plugins/pmcro-runtime/tests/ProjectName.Runtime.Tests/CandidateCompanyIntegrationTests.cs` — SHA-256 `f0eeeb3834b69c2d724c3fcd4e0e58f396748d280fc433ba0f196cb060111ebd`.
- Prior G: .NET 10 local regression suite: **52 passed / 0 failed / 0 skipped**. Original TRX SHA-256 `4cfc1659c50d605738cc6d247d3d93cba7f1e6f67f78017f635f6fd59ce2eb1f`. That TRX remains on the user's G: workspace and is deliberately not included here because it contains environment-specific metadata.
- Candidate PowerShell action downloader: **6 passed / 0 failed** offline security checks; captured output in `docs/candidate-test-results/pmcro-action-download-offline-20261028.txt`.

## Candidate download-action marketplace product

`marketplaces/pmcro-marketplace/products/download-action.candidate.json` describes a **candidate** listing only. Its owner skill is `plugins/pmcro-orchestrator/skills/download-action/SKILL.md`. Its PowerShell script verifies local files and stages only explicitly approved SHA-256-pinned HTTPS downloads from specific GitHub hosts into a pre-existing `candidate-action-downloads` directory. It never installs, registers, activates, or executes downloaded code.

Run the candidate-only offline test:

```powershell
pwsh -NoProfile -File plugins/pmcro-orchestrator/skills/download-action/tests/test-download-action.ps1
```

No URL and independently trusted SHA-256 were supplied, so **no action artifact has been downloaded**. The local `act` GitHub Actions runner was not found on the connected Windows machine's PATH; this staging skill is not a replacement for that runner.

## Trust and release gates

The independent `pmcro-checker` plugin inspected locally is unregistered/ungranted/unactivated, so there is **no genuine authenticated Checker PASS** for this snapshot. G: Seed 0033 HALT and Seed 0034 UNEXECUTED stay unchanged. All imported files are intentionally isolated as candidate sources, not production authority.
