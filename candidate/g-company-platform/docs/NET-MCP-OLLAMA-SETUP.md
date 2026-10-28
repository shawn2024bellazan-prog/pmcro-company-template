# PMCR-O .NET MCP and VS Code marketplaces — candidate integration

**Status:** Candidate only. No live tool execution, plugin installation, authenticated Checker, registration, signing-key operation, grant or activation is performed.

This additive snapshot is based on the owner-selected `G:\workspace\pmcro-company-platform` source. It reuses existing Node-based `download-action` staging from the other candidate commit rather than replacing it with another installer.

## VS Code marketplaces

`candidate/g-company-platform/.vscode/settings.json` lists:
- `github/copilot-plugins`
- `github/awesome-copilot`
- `anthropics/claude-plugins-official`
- `ShawnDelaineBellazanLoop/PMCR-O-Marketplace`

Open the **candidate/g-company-platform** folder as the VS Code workspace (or use the original G: root on your device). Review prompts in VS Code's `@agentPlugins` view. `chat.plugins.marketplaces` is discovery only; manually install selected plugins after source review. The new GitHub marketplace repository is readable but **not writable** with the connected GitHub account.

## Local .NET MCP server

Source: `plugins/pmcro-runtime/src/ProjectName.Marketplace.Mcp`.

The server uses the existing workspace central NuGet versions: `ModelContextProtocol 2.2.0` and `Microsoft.Extensions.Hosting 10.0.13`. It communicates over **stdio**.

```powershell
dotnet build plugins/pmcro-runtime/src/ProjectName.Marketplace.Mcp/ProjectName.Marketplace.Mcp.csproj -c Release
```

The workspace `.vscode/mcp.json` runs the built Release DLL. Review the MCP trust prompt before connecting. All marketplace-discovery tools are read-only.

Exposed tools:
- `list_candidate_marketplaces`
- `inspect_candidate_marketplace`
- `get_pmcro_candidate_status`
- `ollama_local_status`
- `draft_with_local_ollama` (blocked by default)

Ollama must be separately running and a model explicitly chosen. Set `PMCRO_LOCAL_INFERENCE_ENABLED=true` and `PMCRO_OLLAMA_MODEL` **only after** approving candidate text-only inference. Ollama output is an untrusted proposal; this server has no shell, install, registration, grant, activation or arbitrary external-action tool.

## Local actions and role governance

The previously published `.agents/skills/download-action` is a portable MAF Agent Skill with Node-based offline tests and approved-only artifact staging. It cannot install or run downloaded code. `act` was not found on the Windows device's PATH.

The candidate runtime C-suite design retains the 10 versus 11 office identifier mismatch, deny-all positive role admission, G: Seed 0033 HALT and Seed 0034 UNEXECUTED. No independent Checker PASS was produced.

## Verification

On G: the .NET 10 Release build completed with **0 warnings and errors**. The MCP server's actual JSON-RPC initialization, tools/list, candidate status, marketplace listing and disabled Ollama draft were verified via Python 3.12 smoke tests. The local Python safety tests for the alternate staged downloader passed but **are not installed in this GitHub snapshot**.

The GitHub candidate workflow `.github/workflows/g-company-mcp.yml` builds the isolated MCP server and tests its real stdio protocol plus existing Node offline staging tests. CI is separate from production activation.

Documentation: [VS Code MCP server configuration](https://code.visualstudio.com/docs/agents/reference/mcp-configuration) · [VS Code plugin marketplaces](https://code.visualstudio.com/docs/agent-customization/agent-plugins).

**To allow Ollama to execute real tools later:** implement a separate, host-authenticated, scope-limited MCP execution adapter that verifies real signed grants, registration, activation, policies, revocations, Trail admission and independent Checker evidence before performing a side effect, with idempotency and uncertain-dispatch reconciliation. No such adapter is activated by this candidate.
