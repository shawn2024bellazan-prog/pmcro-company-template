# PMCR-O plan: execute continuation checks in GitHub Actions

Intent: carry the proposed `@checker /pmcro:check validate_continuation_plugin`
seed to an independently executing build environment.

The local executor lacks the .NET SDK and PowerShell; the connected development
devices were offline when inspected. A read-only GitHub Actions job is the
bounded alternative. It does not deploy or promote the plugin.

Named files:

- `.github/workflows/continuation-ci.yml`
- `docs/decisions/0004-continuation-ci-plan.md`
- `.pmcro/trails/orchestrator/2026-10-28-continuation-ci-make.json`

Proof:

1. YAML parses and `actionlint` exits zero.
2. GitHub loads the workflow after a feature-branch push.
3. The hosted runner sets up .NET 10, parses plugin/catalog manifests, and
   invokes `scripts/validate.ps1` to compile and run all contract checks.
4. Store the exact workflow run URL, conclusion, and failure diagnostics in a
   separate check record. Do not call the plugin checked/promoted unless the
   independent checker and governance gates pass.

The job has read-only repository permissions, a 15-minute limit, and no
deployment, merge, or repository mutation step.
