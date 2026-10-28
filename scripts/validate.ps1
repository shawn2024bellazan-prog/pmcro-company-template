$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $projectRoot 'PMCR-O.sln'

Push-Location $projectRoot
try {
    foreach ($relativePath in @(
        'plugins/pmcro.continuation/plugin.json',
        'plugins/pmcro.subagent-return/plugin.json',
        'schemas/pmcro-plugin.schema.json',
        'manifests/marketplace-sources.json',
        'manifests/capability-registrations/examples.json',
        'manifests/skill-routes/catalog.json',
        '.pmcro/trails/orchestrator/2026-10-28-continuation-plugin-make.json',
        '.pmcro/trails/orchestrator/2026-10-27-subagent-return-make.json'
    )) {
        $jsonPath = Join-Path $projectRoot $relativePath
        $null = Get-Content -Path $jsonPath -Raw | ConvertFrom-Json -ErrorAction Stop
    }

    dotnet build $solution --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    foreach ($testProject in @(
        'tests/PMCR.O.Core.Tests/PMCR.O.Core.Tests.csproj',
        'tests/PMCR.O.Continuation.Tests/PMCR.O.Continuation.Tests.csproj',
        'tests/PMCR.O.Continuation.Integration.Tests/PMCR.O.Continuation.Integration.Tests.csproj',
        'tests/PMCR.O.SubagentReturn.Tests/PMCR.O.SubagentReturn.Tests.csproj'
    )) {
        dotnet run --project (Join-Path $projectRoot $testProject) --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    exit 0
}
finally {
    Pop-Location
}
