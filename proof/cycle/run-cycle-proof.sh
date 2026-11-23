#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
mkdir -p evidence/cycle
export PMCRO_CYCLE_EVIDENCE_DIR="$PWD/evidence/cycle"
dotnet --info > evidence/cycle/dotnet-info.txt
dotnet restore proof/cycle/Pmcro.Phases.Proof/Pmcro.Phases.Proof.csproj
dotnet build proof/cycle/Pmcro.Phases.Proof/Pmcro.Phases.Proof.csproj -c Release --no-restore
timeout 45s dotnet proof/cycle/Pmcro.Phases.Proof/bin/Release/net10.0/Pmcro.Phases.Proof.dll | tee evidence/cycle/result.json
python3 proof/cycle/verify-cycle.py "$PMCRO_CYCLE_EVIDENCE_DIR" | tee evidence/cycle/independent-verification.json
sha256sum evidence/cycle/cycle.jsonl evidence/cycle/phase-invocations.jsonl > evidence/cycle/sha256sums.txt
