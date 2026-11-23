#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
mkdir -p evidence/maf
export PMCRO_MAF_AGENT_DEFINITION="$PWD/proof/maf/agents/pmcro-native-echo.definition.json"
export PMCRO_MAF_INVOCATIONS="$PWD/evidence/maf/invocations.jsonl"
dotnet --info > evidence/maf/dotnet-info.txt
dotnet restore proof/maf/Pmcro.Maf.Proof/Pmcro.Maf.Proof.csproj
dotnet build proof/maf/Pmcro.Maf.Proof/Pmcro.Maf.Proof.csproj -c Release --no-restore
dotnet proof/maf/Pmcro.Maf.Proof/bin/Release/net10.0/Pmcro.Maf.Proof.dll | tee evidence/maf/result.json
python3 proof/maf/verify-maf-evidence.py | tee evidence/maf/verification.json
sha256sum proof/maf/agents/pmcro-native-echo.definition.json plugins/pmcro/skills/plan/SKILL.md > evidence/maf/source-sha256.txt
