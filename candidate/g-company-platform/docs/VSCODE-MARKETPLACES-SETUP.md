# VS Code marketplace and portable agent setup — PMCR-O

**Candidate configuration only.** Source marketplace discovery is not installation, runtime grant, Checker activation, or production execution.

## Verified target

Microsoft VS Code installed on this Windows device. The shell command named code currently points to Cursor, NOT Microsoft VS Code.
Open the folder G:\workspace\pmcro-company-platform in the real Microsoft VS Code application, not the Cursor command alias, to use these .vscode settings.

## Marketplace sources

This workspace settings.json configures:
- github/copilot-plugins — GitHub's official Copilot plugin marketplace
- github/awesome-copilot — curated Copilot skills/plugins
- anthropics/claude-plugins-official — official Anthropic-managed Claude plugin directory
- ShawnDelaineBellazanLoop/PMCR-O-Marketplace — existing public PMCR-O discovery source (separate from G: capability authority)

No third-party plugins, hooks, MCP servers or actions are automatically installed. Treat all downloaded code as untrusted, inspect publishers and exact sources, and use marketplace trust prompts.

To inspect:
1. Open VS Code with G: workspace.
2. Chat: Open Customizations → Discover, select the source and filter by skill/plugin/MCP.
3. Extensions → search @agentPlugins; check plugin sources and actual installed inventory.
4. To inspect a candidate capability, use .agents/skills/download-action/SKILL.md.
5. For manually approved downloads, choose an immutable artifact URL and a separate independently verified SHA-256; do not execute the downloaded bytes.

## Compatibility distinction

The PMCR-O directories under marketplaces/ contain PMCR-O candidate product manifests. They are not automatically VS Code plugin marketplace indexes.
VS Code agent plugins require recognized plugin metadata; portable Agent Skills use SKILL.md in a supported skill folder (here .agents/skills/download-action). This G skill follows native VS Code/Copilot discovery, while the G candidate PMCR-O marketplace points to it in its own schema.
AGENTS.md supplies shared instructions. The candidate PMCR-O orchestrator role is NOT authenticated or activated by simply opening the workspace.

## Explicit non-automation

No credentials are stored in settings.
The `.vscode/mcp.json` file configures the isolated `ProjectName.Marketplace.Mcp` candidate stdio server. It can be started by the VS Code MCP host only after normal workspace trust/tool approval; it is not the AppHost or a registered PMCR-O runtime. The local smoke test exercised read-only tools without starting role workers.
No external plugin source was installed (the marketplace list only configures discovery).
No extension was automatically installed; .vscode/extensions.json contains recommendations only.
No .pmcro Trail, production registration, grant, key, revocation or activation record was modified.