# CI automation and activation

The PMCR-O CI workflow is defined in `.github/workflows/ci.yml`. GitHub Actions activates it on:

- **Push:** every branch, so commits pushed by the developer trigger validation.
- **Pull request:** opened, synchronized, or reopened PRs trigger validation.
- **Manual run:** select **Actions → PMCR-O CI → Run workflow**. The optional `run_demo` input controls the demo smoke test for that manual run.
- **Reusable workflow call:** another trusted workflow can call this workflow.
- **Weekly schedule:** a scheduled run checks for drift even when there are no new commits.

The workflow uses read-only repository permissions and a concurrency group that cancels obsolete runs for the same branch or pull request. It validates JSON and YAML syntax, restores and builds the .NET 10 solution, runs the contract-test executable, and smoke-tests the demo. A failed step fails the workflow.

## How to activate it in GitHub

1. Commit `.github/workflows/ci.yml` and push it to the repository.
2. Open the repository's **Actions** tab and enable workflows if GitHub prompts for that.
3. Push a commit or open/update a pull request to trigger CI. For a manual run, choose **PMCR-O CI → Run workflow**.
4. Optionally configure branch protection/rulesets to require the CI status check before merging. This repository setting is intentionally not changed by the workflow.

## Boundaries

The workflow only runs after it exists on GitHub and GitHub Actions is enabled for the repository. A local ZIP or local commit does not trigger a remote workflow. This automation does not grant deployment or repository-write permissions and does not auto-merge pull requests. The weekly schedule is best-effort and may be delayed by GitHub.
