#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
mkdir -p evidence/a2a
dotnet --info > evidence/a2a/dotnet-info.txt
dotnet restore proof/a2a/Pmcro.A2A.Server/Pmcro.A2A.Server.csproj
dotnet restore proof/a2a/Pmcro.A2A.Client/Pmcro.A2A.Client.csproj
dotnet build proof/a2a/Pmcro.A2A.Server/Pmcro.A2A.Server.csproj -c Release --no-restore
dotnet build proof/a2a/Pmcro.A2A.Client/Pmcro.A2A.Client.csproj -c Release --no-restore
find proof/a2a -type f \( -name '*.cs' -o -name '*.csproj' -o -name '*.json' \) -print0 | sort -z | xargs -0 sha256sum > evidence/a2a/source-sha256.txt
export PMCRO_A2A_AGENT_DEFINITION="$PWD/proof/a2a/agents/pmcro-echo.definition.json"
export PMCRO_A2A_INVOCATIONS="$PWD/evidence/a2a/agent-invocations.jsonl"
export PMCRO_A2A_URL="http://127.0.0.1:5077"
export ASPNETCORE_URLS="$PMCRO_A2A_URL"
dotnet proof/a2a/Pmcro.A2A.Server/bin/Release/net10.0/Pmcro.A2A.Server.dll > evidence/a2a/server-stdout.log 2> evidence/a2a/server-stderr.log &
server_pid=$!
cleanup() { kill "$server_pid" 2>/dev/null || true; wait "$server_pid" 2>/dev/null || true; }
trap cleanup EXIT
ready=0
for i in $(seq 1 45); do
  if curl --silent --show-error --fail "$PMCRO_A2A_URL/health" -o evidence/a2a/health.json 2>/dev/null; then ready=1; break; fi
  if ! kill -0 "$server_pid" 2>/dev/null; then break; fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then echo "A2A server did not start"; cat evidence/a2a/server-stderr.log; exit 1; fi
curl --silent --show-error --fail "$PMCRO_A2A_URL/.well-known/agent-card.json" -o evidence/a2a/agent-card.json
timeout 45s dotnet proof/a2a/Pmcro.A2A.Client/bin/Release/net10.0/Pmcro.A2A.Client.dll "$PMCRO_A2A_URL" | tee evidence/a2a/client.json
python3 proof/a2a/verify-a2a-evidence.py evidence/a2a | tee evidence/a2a/verification.json
