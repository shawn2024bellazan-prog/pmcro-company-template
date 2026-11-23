# PMCR-O MCP proof — independent execution lane

This proof is intentionally **separate** from the blocked authoritative `/init` profile initialization. It is not an A2A/MAF proof and does not establish a managed `.pmcro/` profile.

## Actual native SDK proof
- Server: .NET 10 console process using the official `ModelContextProtocol` 2.4.0 NuGet package and Microsoft.Extensions.Hosting 10.0.15.
- Tool: `Add(int a,int b)`, read-only and deterministic.
- Client: independent .NET 10 process. `McpClient.CreateAsync` negotiates initialize, then `ListToolsAsync` and `CallToolAsync` interact with the server over stdio.
- Verified acceptance: tool is listed, invocation is `Add(2,3)`, actual response text is exactly `5`, independent Python gate passes.
- GitHub Actions workflow triggers on pull requests and uploads evidence; no default-branch changes.
- Never claim PASS before collecting a real workflow run result and its evidence artifact. A valid workflow file alone is `NOT_INVOKED`.
- Run locally with `bash proof/run-proof.sh` on a system that has .NET 10 SDK and network access to NuGet.

## Blocked initializer
The installed `pmcro-commands:init` script imports `config.py`, `profile_builder.py`, and `audit.py` from `plugins/pmcro/skills/pmcro-agent/scripts/`. Those authoritative modules were not available in the installed plugin bundle and cannot be fabricated. See the separate blocked-attempt evidence package and do not confuse this MCP proof with PMCR-O profile initialization.

Official SDK guide: https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html
