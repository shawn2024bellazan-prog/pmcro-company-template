# PMCR-O CLI (Candidate)

This is a prerelease, untrusted-by-default CLI candidate for the PMCR-O ecosystem. Its tool command name is pmcro.

Commands:
- pmcro init [--root existing-path] [--ui copilotkit|maui|both|none] [--model ollama|cloud|hybrid] [--client vscode|none] [--json]
- pmcro doctor [same options]

init and doctor are read-only. They do not download packages, write project files, provision keys, register capabilities, issue grants, activate MCP servers, or start agent workers. They are not a substitute for independent Checker evidence.

Other existing ProjectName.AgentCli commands include commands, marketplace, messaging, ui, and the separately governed control-plane. Packaging as a tool does not widen their permissions.

Full setup guidance: docs/PMCRO-CLI-INIT-CANDIDATE-2026-10-28.md in the G: candidate workspace. This package is not published to NuGet.