$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $projectRoot 'tests/PMCR.O.Continuation.Tests/PMCR.O.Continuation.Tests.csproj'

Push-Location $projectRoot
try {
    dotnet build $testProject --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $testProject --configuration Release --no-build
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
