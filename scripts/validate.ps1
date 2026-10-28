$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $projectRoot 'tests/PMCR.O.Continuation.Tests/PMCR.O.Continuation.Tests.csproj'

Push-Location $projectRoot
try {
    foreach ($relativePath in @(
        'plugins/pmcro.continuation/plugin.json',
        'schemas/pmcro-plugin.schema.json',
        'manifests/marketplace-sources.json',
        '.pmcro/trails/orchestrator/2026-10-28-continuation-plugin-make.json'
    )) {
        $jsonPath = Join-Path $projectRoot $relativePath
        $null = Get-Content -Path $jsonPath -Raw | ConvertFrom-Json -ErrorAction Stop
    }

    dotnet build $testProject --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $testProject --configuration Release --no-build
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
