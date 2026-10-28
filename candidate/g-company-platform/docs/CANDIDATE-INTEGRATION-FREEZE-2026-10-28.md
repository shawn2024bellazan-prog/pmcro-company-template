# PMCR-O G: candidate integration — frozen local test evidence

**Status:** CANDIDATE SOURCE FREEZE / LOCAL TEST EVIDENCE. This document is **not** an independent Checker receipt or a persisted PMCR-O Trail Frame.

**Workspace:** `G:\workspace\pmcro-company-platform`  
**Target framework:** `net10.0`; test host reports `.NET 10.0.14`. First build showed an SDK 11 preview diagnostic while .NET SDK 10.0.403 was also initialized; therefore this evidence claims the **target/runtime .NET 10**, not a verified exclusively .NET 10 SDK toolchain.

## New files and source SHA-256

Paths below are relative to `G:\workspace\pmcro-company-platform`.

| Artifact | SHA-256 |
| --- | --- |
| `plugins/pmcro-runtime/src/ProjectName.Contracts/CandidateCompanyIntegration.cs` | `1ef4befadfb203c26c78a097b34627e41f381e89ff67fd7a181e8058da62b571` |
| `plugins/pmcro-runtime/tests/ProjectName.Runtime.Tests/CandidateCompanyIntegrationTests.cs` | `f0eeeb3834b69c2d724c3fcd4e0e58f396748d280fc433ba0f196cb060111ebd` |
| `docs/INTEGRATED-ARCHITECTURE-CANDIDATE.md` | `9afbd722de0a9d50d204a190e1e0964538885b379978b5619530a59c5e318e1b` |
| `docs/INTEGRATION-ACCEPTANCE-TESTS.md` | `60089d5864377b0055b0960f8497c0540bf7d07e92e8735833d0d5867cbb8070` |
| `docs/INTEGRATION-DESIGN-VALIDATION-CANDIDATE.md` | `0e60caa6703b6a045aa89c29f3fe1ceb4654d0420b98c42066190cbc8c5e022c` |

The hashes were obtained by local .NET `System.Security.Cryptography.SHA256` over the exact G: files. No Git commit is claimed: this G: workspace has no visible `.git` folder at its root.

## Actual tests

**Focused increment:** `CandidateCompanyIntegrationTests` — **9 passed / 0 failed**, exit code 0, .NET 10 test host. Includes UMP integrity, cross-catalog name mismatch, fake grant-field rejection, MAF ownership and deny-all provider without a model call.

**Isolated regression:** local .NET VSTest run with `-c Release --no-build --no-restore` and filter classes `CandidateCompanyIntegrationTests`, `ExecutiveEmbodimentConventionTests`, `ExecutiveCouncilTests`, `PmcroMafWorkflowTests`, `GovernedRolePhaseHandlerTests`, `MessageConventionTests`, and `PmcroRoleOwnershipTests`: **52 passed / 0 failed / 0 skipped**, exit code 0.

**Reproduced regression + raw VSTest receipt:** exact command:

```powershell
dotnet test "G:\workspace\pmcro-company-platform\plugins\pmcro-runtime\tests\ProjectName.Runtime.Tests\ProjectName.Runtime.Tests.csproj" --no-build --no-restore -c Release --filter "FullyQualifiedName~CandidateCompanyIntegrationTests|FullyQualifiedName~ExecutiveEmbodimentConventionTests|FullyQualifiedName~ExecutiveCouncilTests|FullyQualifiedName~PmcroMafWorkflowTests|FullyQualifiedName~GovernedRolePhaseHandlerTests|FullyQualifiedName~MessageConventionTests|FullyQualifiedName~PmcroRoleOwnershipTests" --logger "trx;LogFileName=pmcro-g-candidate-integration-20261028.trx" --results-directory "G:\workspace\pmcro-company-platform\docs\candidate-test-results"
```

- Raw receipt: `docs/candidate-test-results/pmcro-g-candidate-integration-20261028.trx`
- TRX file size: **80,692 bytes**
- TRX SHA-256: `4cfc1659c50d605738cc6d247d3d93cba7f1e6f67f78017f635f6fd59ce2eb1f`
- VSTest summary: **Passed 52, Failed 0, Skipped 0, Total 52**.

## Scope and limitations

- Tests are isolated local unit/integration fixtures, not production use. They do not prove registered identity, signed Checker credentials, actual production grant/activation, external worker dispatch or real independent Checker PASS.
- Newly created proposal adapter is not configured or started as a live RoleWorkerService. No positive `IRoleExecutionAdmissionProvider` is installed; default continues to deny.
- G: Seed 0033 HALT and unexecuted Seed 0034 are unchanged. Historical G: `.pmcro` Trails are unchanged; D: authority files remained read-only.
- Existing 38 acceptance-plan cases are **not all individually certified** by these 52 tests; the plan remains the wider integration target.
- No local `act` executable was found on PATH; this is not evidence that no local Actions installation exists elsewhere.
- An **independent authenticated Checker has not been invoked or attested**. See `CHECKER-REVIEW-REQUEST-CANDIDATE.md` for a proposed review package.

**Verdict:** `LOOP` for production readiness; `PASS` for the bounded local candidate test execution only.