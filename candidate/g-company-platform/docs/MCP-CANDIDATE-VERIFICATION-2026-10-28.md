# PMCR-O VS Code + .NET MCP candidate verification — 2026-10-28

**Status:** CANDIDATE / LOCAL EXECUTION EVIDENCE. Not an authenticated Checker receipt, grant, registration, activation, or persisted Trail Frame.

**Workspace:** `G:\workspace\pmcro-company-platform`
**Target:** `net10.0`. Installed build SDK observed `11.0.100-rc.1.26425.128` (preview); build output targets `net10.0`.

## Candidate MCP surface

Project: `plugins/pmcro-runtime/src/ProjectName.Marketplace.Mcp/` (isolated stdio process).
Configured by: `.vscode/mcp.json` with `PMCRO_LOCAL_INFERENCE_ENABLED=false`.
Project also listed in `Pmcro.slnx` for solution discovery.

Advertised MCP tools:
- `list_candidate_marketplaces` and `inspect_candidate_marketplace`: candidate manifest inspection only.
- `get_pmcro_candidate_status`: non-authoritative status, no Checker PASS.
- `ollama_local_status`: local endpoint reachability only.
- `draft_with_local_ollama`: opt-in, bounded, untrusted text proposal; disabled by workspace config; no tool or worker dispatch.

## Observed local commands and results

- `dotnet build "G:\workspace\pmcro-company-platform\plugins\pmcro-runtime\src\ProjectName.Marketplace.Mcp\ProjectName.Marketplace.Mcp.csproj" -c Release --no-restore` — exit 0; **0 warnings, 0 errors**; preview SDK notice.
- `py -3.12 -m unittest discover -s "G:\workspace\pmcro-company-platform\.agents\skills\download-action\tests" -p test_mcp_server.py -v` — **1 passed**; real stdio JSON-RPC initialize, tools/list, three tools/call requests; opt-in inference denied; process closed.
- Same Python invocation with `-p test_download_action.py` — **6 passed**.
- `node --test "G:\workspace\pmcro-company-platform\plugins\pmcro-local-actions\skills\download-action\tests\download-action.test.cjs"` — **5 passed**.
- `dotnet sln "G:\workspace\pmcro-company-platform\Pmcro.slnx" list` — exit 0; includes candidate MCP project.
- Default `python` is broken by environment path resolution, but `py -3.12` works.
- `ollama list` returned an empty model list; no model was downloaded or inference run.
- Microsoft VS Code was found at the user's local Programs path; `code.cmd` currently resolves to Cursor, so do not use that alias to verify VS Code integration.

## Frozen candidate integrity

Recomputed SHA-256 values match `docs/CANDIDATE-INTEGRATION-FREEZE-2026-10-28.md` for all five frozen source/document files:
- `CandidateCompanyIntegration.cs`: `1ef4befadfb203c26c78a097b34627e41f381e89ff67fd7a181e8058da62b571`
- `CandidateCompanyIntegrationTests.cs`: `f0eeeb3834b69c2d724c3fcd4e0e58f396748d280fc433ba0f196cb060111ebd`
- `INTEGRATED-ARCHITECTURE-CANDIDATE.md`: `9afbd722de0a9d50d204a190e1e0964538885b379978b5619530a59c5e318e1b`
- `INTEGRATION-ACCEPTANCE-TESTS.md`: `60089d5864377b0055b0960f8497c0540bf7d07e92e8735833d0d5867cbb8070`
- `INTEGRATION-DESIGN-VALIDATION-CANDIDATE.md`: `0e60caa6703b6a045aa89c29f3fe1ceb4654d0420b98c42066190cbc8c5e022c`
- 52/52 TRX receipt: `4cfc1659c50d605738cc6d247d3d93cba7f1e6f67f78017f635f6fd59ce2eb1f`.

## Boundary and verdict

Marketplace repositories configured in `.vscode/settings.json` are **discovery sources only**. No external plugin or action artifact was downloaded or installed. No PMCR-O capability registration, grant, activation, key provisioning, worker admission, AppHost start, historical Trail rewrite, or candidate promotion was performed.

**Verdict:** PASS for bounded local candidate build and protocol/security tests; **HALT** for independent Checker acceptance and governed execution. G: Seed 0033 HALT and unexecuted Seed 0034 remain unchanged.

**One unexecuted NextSeed:** Obtain an existing host-authenticated independent Checker registration, explicit active grant, and evidence append permission; independently verify the frozen bytes and deny-all admission under that identity, then issue a signed verdict. Do not create or activate the Checker as part of this seed.