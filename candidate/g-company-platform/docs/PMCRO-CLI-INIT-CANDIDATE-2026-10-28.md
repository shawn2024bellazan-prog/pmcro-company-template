# PMCR-O CLI Candidate - init / doctor (2026-10-28)

Status: candidate only; no installation, publication, registration, grants, activation, worker admission, or AppHost start.

## Product topology

ProjectName.AgentCli is the existing command entry point. It is now configured for .NET tool packaging with command name pmcro (candidate package ID PMCR.O.Cli.Candidate, version 0.1.0-preview.1).

- pmcro init: read-only local inspection and structured setup plan (pmcro.bootstrap-plan/v1).
- pmcro doctor: read-only local inventory of the project, VS Code configuration, marketplace catalog, MCP source, Aspire AppHost source, optional UI targets, and selected tools on PATH.
- /pmcro:init in chat: an intended adapter alias, not yet wired to a host; the chat message does not grant execution privileges.
- PMCR-O Studio: future product front end (CopilotKit web app, .NET MAUI shell, or both), using the same command/governance boundary.

The Aspire dashboard's MCP capabilities are not interchangeable with PMCR-O marketplace MCP tools. No plan result implies a server is authenticated, granted, or active.

## Run locally from existing checkout

dotnet run --project G:\workspace\pmcro-company-platform\plugins\pmcro-runtime\src\ProjectName.AgentCli\ProjectName.AgentCli.csproj -- init --root G:\workspace\pmcro-company-platform --ui both --model hybrid --client vscode --json

dotnet run --project G:\workspace\pmcro-company-platform\plugins\pmcro-runtime\src\ProjectName.AgentCli\ProjectName.AgentCli.csproj -- doctor --root G:\workspace\pmcro-company-platform

Package a local candidate NuGet tool (not install or publish):

dotnet pack G:\workspace\pmcro-company-platform\plugins\pmcro-runtime\src\ProjectName.AgentCli\ProjectName.AgentCli.csproj -c Release --no-restore

A later owner-approved local tool installation can use the produced nupkg. Do not perform an implicit global installation or use this preview package as evidence of production authority.

## Bootstrap contract

1. Discover prerequisites without starting processes.
2. Select UI: copilotkit | maui | both | none.
3. Select model: ollama | cloud | hybrid.
4. Select client: vscode | none.
5. Plan pinned/provenance-verified downloads.
6. Propose VS Code/MCP marketplace projections.
7. Test isolated product build.
8. Obtain independently authenticated Checker verdict on exact artifacts.
9. Request separate host owner grants before activation.

Safe flags: --root, --ui, --model, --client, --json. Mutating flags --apply, --install, --activate, --grant, --start, and --yes are rejected. The program does not invoke a shell, package manager, runtime host, local Ollama process, or network source during init/doctor.

A positive FOUND result means a file or executable was detected; it does not mean a service started, a model loaded, or permission was granted. Missing optional UI files are expected at this candidate stage. The CopilotKit frontend in ShawnDelaineBellazanLoop/PMCR-O-Marketplace should be integrated by verified source/version, not assumed present in the G: root.

## Governance

G: Seed 0033 remains HALT; Seed 0034 remains unexecuted. The prior verified 52/52 TRX receipt is historical and does not become a Checker verdict for these new CLI changes. No historical Trail was rewritten.

Real activation requires an existing host-authenticated Checker with verified registration, grant, activation, identity, and evidence-writing permissions. A marketplace manifest, editor setting, local CLI, or self-produced test run cannot manufacture that authority.

## NextSeed (unexecuted)

After a separate independent Checker route is proven and the frozen integration reviewed, extend the planner into an explicitly approved, pinned-download staging command, then add a governed client adapter for /pmcro:init. Do not add unattended installs or worker startup.