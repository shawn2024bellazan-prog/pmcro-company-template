# PMCR-O CLI candidate verification - 2026-10-28

Scope: read-only init/doctor and local .NET tool packaging only.
Canonical source: G:\workspace\pmcro-company-platform.
GitHub candidate mirror: shawn2024bellazan-prog/pmcro-company-template, feat/pmcro-subagent-return-v1, commit c71e09f0fafbf424469443321edb2cb104d5a7af.
GitHub CI: run 40652906413 completed SUCCESS on the commit. This workflow validates its own repository; it does not build/test the G: candidate subtree.

## Observed local commands

1. dotnet build ProjectName.AgentCli.csproj -c Release --no-restore
   PASS, 0 errors, 0 warnings. The host used .NET 11 preview SDK while targeting net10.0.
2. dotnet test ProjectName.Runtime.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~PmcroInitCommandTests
   PASS, 9/9 new CLI safety tests.
3. dotnet <Release/net10.0/ProjectName.AgentCli.dll> init --root G:\workspace\pmcro-company-platform --ui both --model hybrid --json
   PASS; schema pmcro.bootstrap-plan/v1; outcome CANDIDATE_PLAN_ONLY; all mutation flags false.
4. dotnet <Release/net10.0/ProjectName.AgentCli.dll> doctor --root G:\workspace\pmcro-company-platform
   PASS; found VS Code MCP config, marketplace catalog, Aspire AppHost source, read-only MCP source, dotnet/code/ollama binaries. MAUI and local CopilotKit product shell not found.
5. dotnet pack ProjectName.AgentCli.csproj -c Release --no-restore
   PASS, produced candidate tool nupkg. No NuGet publication or installation.
6. tar -tf local nupkg
   PASS, archive directory listing succeeded and contains README and tools/net10.0/any/DotnetToolSettings.xml.
7. tar -xOf local nupkg tools/net10.0/any/DotnetToolSettings.xml
   PASS; tool command Name=pmcro, EntryPoint=ProjectName.AgentCli.dll, Runner=dotnet.
8. dotnet test ProjectName.Runtime.Tests.csproj -c Release --no-build --no-restore
   LOOP, 202 passed, 1 failed, 0 skipped, 203 total. Failing test: MarketplaceDiscoveryTests.Candidate_marketplace_indexes_declared_skills_without_authoring_projection; expected AuthoringSkillCount=0, observed=1. The G: workspace contains .agents/skills/download-action/SKILL.md from an earlier staging increment. No test/manifest expectation was altered to hide the drift.

## Package receipt

Path:
G:\workspace\pmcro-company-platform\plugins\pmcro-runtime\src\ProjectName.AgentCli\bin\Release\PMCR.O.Cli.Candidate.0.1.0-preview.1.nupkg

SHA-256: 2E935BCF46F89884926BF854BB4A15B8B2C70AF00BFCED9D5B0A8A797EE93225

## Changed local source file SHA-256

plugins/pmcro-runtime/src/ProjectName.AgentCli/InitCommand.cs
1A958E274D06704B9E55D5B645C38D924C322ACF236C883F93FCBE73F872D943

plugins/pmcro-runtime/src/ProjectName.AgentCli/Program.cs
4A4914354EEE135B988ECA88E87D1CE85DA93256E1E903E9471A3FFCC57759D9

plugins/pmcro-runtime/src/ProjectName.AgentCli/ProjectName.AgentCli.csproj
A48FA1B99A95F6E33A47A236E8E627BF9CC728BE29CB11E989CBEE8057578D5A

plugins/pmcro-runtime/src/ProjectName.AgentCli/README-PMCRO-CLI.md
A4CE1D55EC1B45C85FF827EC0D031F886C116C2727AF71218FA4BB1A1D2DC560

plugins/pmcro-runtime/tests/ProjectName.Runtime.Tests/PmcroInitCommandTests.cs
8573396ADC329420EF791D6D69920A1E29740B96B9140FBAE648B40A17302E10

plugins/pmcro-runtime/tests/ProjectName.Runtime.Tests/ProjectName.Runtime.Tests.csproj
09BFA92A16028972BF74FE2720D4B51050D9498A15EA0CB5367150549ECF785A

docs/PMCRO-CLI-INIT-CANDIDATE-2026-10-28.md
0C949416DA1D4B90237CBFC378687A1FE0D6A11B9642CA359588FADC885D6C19

Hashes above are from G: source bytes, not independently authenticated Checker signatures or attestations of GitHub byte equality.

## Authority and final status

Candidate CLI unit verification: PASS.
Full G: runtime test suite: LOOP (authoring-skill discovery count drift).
Independent frozen integration Checker: HALT; no verified host-authenticated Checker registration, grant, activation, identity, and evidence-writing permission was provided in this increment.
G: Seed 0033 remains HALT. G: Seed 0034 remains unexecuted. No historical Trail was rewritten.
No downloads, installations, capability registrations, operational grants, activations, AppHost, model runtime, or agent workers were invoked by init/doctor.

NextSeed (unexecuted): determine the lawful source contract for .agents/skills/download-action, reconcile the discovery projection without suppressing the test, and obtain the authenticated independent Checker route before staging any install/activation feature.