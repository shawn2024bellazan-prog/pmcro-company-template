# PMCR-O in VS Code: marketplace + MCP + Ollama (development candidate)

**Status:** locally tested candidate. Not a registered/activated production marketplace. Do not treat model-produced suggestions as an authorized grant.

## What is already present on G:

- `.vscode/mcp.json` defines the `pmcro-candidate` .NET 10 **stdio** server, using the Release DLL at `plugins/pmcro-runtime/src/ProjectName.Marketplace.Mcp/bin/Release/net10.0/ProjectName.Marketplace.Mcp.dll`.
- The actual MCP server is `plugins/pmcro-runtime/src/ProjectName.Marketplace.Mcp/Program.cs`. It lists and inspects candidate marketplaces and products; reports its no-authority status; checks local Ollama reachability; optionally drafts **untrusted text only**. It has **no tool to install/download an action**, register capabilities, grant keys, activate agents, or execute remote code.
- `marketplaces/pmcro-marketplace/products/download-action.candidate.json` points to `.agents/skills/download-action/SKILL.md`, a **candidate-only** progressive-disclosure skill. Its Node script only inspects, verifies existing files by hash, or stages a single user-pinned GitHub-hosted artifact with explicit staging approval. A CLI flag is not a host grant.
- `.vscode/settings.json` retains the legacy `chat.plugins.marketplaces` preference and also enables the currently experimental `chat.customizations.marketplace.enabled`. These settings **do not install a marketplace or assert that its GitHub repository exists**.
- `.vscode/extensions.json` recommends C# and Ollama's official extension `ollama.ollama` but does not install extensions.

## Local observations, October 28, 2026

- Desktop Commander connected to `PMCRO_Company`.
- `dotnet build .../ProjectName.Marketplace.Mcp.csproj -c Release --no-restore`: **PASS, 0 errors, 0 warnings** targeting `net10.0`.
- Real MCP stdio JSON-RPC integration smoke: **PASS** via `docs/candidate-test-results/mcp-smoke.mjs`; verified `initialize`, `tools/list`, candidate product discovery, and disabled Ollama inference.
- Node downloader tests: **5/5 PASS** for the canonical `.agents/skills/download-action` copy. A separate authoring copy under `G:\plugins\pmcro-orchestrator` exists and passed 6/6 checks; **workspace-local `.agents/skills` is the candidate portability target**, not a second authority root.
- Ollama executable found but `ollama list` reported **zero locally available models**; no model download or inference performed.
- The `code` command on this Windows PATH currently resolves to **Cursor**, not confirmed Microsoft Visual Studio Code. These workspace files may be opened in VS Code; installing or modifying the editor itself has **not** been performed.
- Local Python 3.11 environment did not contain its normal standard library, so Node scripts are the supported local downloader path here.

## Run from the G: workspace root

```powershell
dotnet build .\plugins\pmcro-runtime\src\ProjectName.Marketplace.Mcp\ProjectName.Marketplace.Mcp.csproj -c Release
node --test .\.agents\skills\download-action\tests\download-action.test.cjs
node --test .\docs\candidate-test-results\mcp-smoke.mjs
node .\.agents\skills\download-action\scripts\download-action.cjs --mode inspect
```

Open **`G:\workspace\pmcro-company-platform`** in a recent Microsoft VS Code version; inspect the workspace trust prompt and installed MCP servers. VS Code supports either `.vscode/mcp.json` with top-level `servers`, or modern portable `.mcp.json` with top-level `mcpServers`. The existing workspace uses the former and is tested with the current SDK. See <https://code.visualstudio.com/docs/agents/reference/mcp-configuration>.

To discover customization marketplaces, use **Chat: Open Customizations → Discover** with the experimental marketplace setting; review the actual source, vendor, version and permissions before installing. VS Code's agent plugin marketplaces are not a substitute for PMCR-O's control-plane registry, signed grants or activation state: <https://code.visualstudio.com/docs/agent-customization/agent-plugins>.

To use Ollama as VS Code's chat model, install the official **Ollama for VS Code** extension (`ollama.ollama`) *after reviewing it*, make sure Ollama is running, choose/download an appropriate model, and use the model picker. Do **not** set `PMCRO_LOCAL_INFERENCE_ENABLED=true` automatically. The existing MCP tool's local inference is intentionally off. See <https://marketplace.visualstudio.com/items?itemName=ollama.ollama>.

## Download local actions safely

The staged downloader **does not download anything in inspect mode**. A real staging run needs an owner-chosen immutable GitHub HTTPS artifact URL, the independently checked exact SHA-256, pre-existing `candidate-action-downloads` folder, and explicit host/owner approval. Do not assume that a downloaded artifact is installed or ready to run. No production key, runtime grant, registry, or activation stores are touched by this candidate.

## What's still missing

- Install/verify Microsoft VS Code itself and compatible editor extension support (current `code` executable is Cursor).
- Choose and download an Ollama model (none was present).
- Independently authenticate/check the frozen G: candidate files and real MCP tool boundaries.
- Resolve G: Seed 0034's owner RoleWorkerService mapping and AGT policy, plus the C-suite office identifier mismatches.
- Commission and activate the trusted runtime separately, only if its signed grant and Trail requirements are met.

**Creation != Activation. Discovery != Registration != Grant != Activation != Execution.** No hosted agent side effects or real C-suite activation are claimed here.