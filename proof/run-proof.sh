#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p evidence
dotnet --info | tee evidence/dotnet-info.txt
dotnet restore proof/Pmcro.Mcp.Server/Pmcro.Mcp.Server.csproj
dotnet restore proof/Pmcro.Mcp.Client/Pmcro.Mcp.Client.csproj
dotnet build proof/Pmcro.Mcp.Server/Pmcro.Mcp.Server.csproj -c Release --no-restore
dotnet build proof/Pmcro.Mcp.Client/Pmcro.Mcp.Client.csproj -c Release --no-restore
dotnet list proof/Pmcro.Mcp.Server/Pmcro.Mcp.Server.csproj package | tee evidence/server-packages.txt
dotnet list proof/Pmcro.Mcp.Client/Pmcro.Mcp.Client.csproj package | tee evidence/client-packages.txt
find proof -type f \( -name '*.cs' -o -name '*.csproj' \) -print0 | sort -z | xargs -0 sha256sum > evidence/source-sha256.txt
timeout 45s dotnet proof/Pmcro.Mcp.Client/bin/Release/net10.0/Pmcro.Mcp.Client.dll \
  "$PWD/proof/Pmcro.Mcp.Server/bin/Release/net10.0/Pmcro.Mcp.Server.dll" | tee evidence/mcp-session.json
python3 proof/verify-evidence.py evidence/mcp-session.json | tee evidence/verification.json
