# PMCR-O MCP proof — separate native execution lane

The .NET C# SDK MCP proof is distinct from managed PMCR-O profile initialization and does not establish native agent dispatch, MAF orchestration or A2A delegation.

## Real execution

- Independent .NET 10 server and client built using the official `ModelContextProtocol` SDK over stdio.
- Server exposes deterministic, read-only `Add(int a,int b)`.
- Client performs MCP `initialize` through `McpClient.CreateAsync`, discovers `add` via `ListToolsAsync`, calls `Add(2,3)` and validates returned text `5`.
- The independent `proof/verify-evidence.py` gate checked the complete client evidence.
- [GitHub Actions run 45115843847: PASS](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45115843847). A distinct downloadable run artifact contains the MCP session evidence.

Run in a .NET SDK environment using `bash proof/run-proof.sh`.

## Initializer boundary

The formerly blocked `pmcro-commands:init` handler now has explicitly labeled **recovery** modules and passed a real source-based CI test: [run 45116577877](https://github.com/shawn2024bellazan-prog/pmcro-company-template/actions/runs/45116577877). This does not imply MCP or native PMCR-O and A2A execution occurred during `/init`.

Official MCP SDK guide: https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html
