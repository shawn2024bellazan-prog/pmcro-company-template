# PMCR-O portable G-company marketplace / MCP candidate

**Candidate-only snapshot** of owner-selected G: source. This directory intentionally does NOT include historical PMCR-O Trail state, grants, registrations, activation receipts, credentials, compiled output, Ollama weights or the full G: MAF solution.

Includes a .NET 10 **stdio** MCP server that discovers PMCR-O candidate marketplaces and products; a portable `.agents/skills/download-action` MAF-native skill for **manual SHA-256-pinned staging only**; candidate executive structure and message validation sources; local test/Checker instructions; and a VS Code workspace MCP config. These files are **not** registered or activated production capabilities.

## Local setup

1. Open this folder (`candidate/g-company-platform`) as the workspace root in VS Code (not the GitHub repository root), or copy only the candidate folder to another trusted development location.
2. Run `dotnet build plugins/pmcro-runtime/src/ProjectName.Marketplace.Mcp/ProjectName.Marketplace.Mcp.csproj -c Release`.
3. Run `node --test .agents/skills/download-action/tests/download-action.test.cjs` and `node --test docs/candidate-test-results/mcp-smoke.mjs`.
4. VS Code's `.vscode/mcp.json` points to the Release DLL and sets `PMCRO_LOCAL_INFERENCE_ENABLED=false`. Review MCP server trust and tool approvals before connecting.
5. If desired, install the official Ollama extension (`ollama.ollama`) from its published marketplace, start local Ollama, and select a model separately. An Ollama model does not acquire PMCR-O authorization by making tool calls.

See `docs/VSCODE-MCP-OLLAMA-MARKETPLACES.md` for verified status, settings, and remaining prerequisites.

## Testing boundaries

- This snapshot's GitHub CI checks candidate manifests, the Node downloader, builds the standalone .NET 10 MCP project, and runs an MCP JSON-RPC stdio smoke test.
- The **52/52 G-host tests** recorded in `docs/CANDIDATE-INTEGRATION-FREEZE-2026-10-28.md` were run on the owner's fuller G runtime. The isolated snapshot does **not** contain the full MAF solution needed to independently replay that entire suite.
- The preserved 10-office versus 11-identifier mismatch is unresolved; no automatic alias is authorized.
- The independent Checker verdict remains pending. The original Seed 0033 HALT and Seed 0034 unexecuted are not modified.

Creation != Activation. Discovery != Registration != Grant != Activation != Execution.
