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

# Negative verification control: a tampered Make frame must fail.
mkdir -p evidence/cycle/tampered
cp evidence/cycle/cycle.jsonl evidence/cycle/tampered/cycle.jsonl
cp evidence/cycle/phase-invocations.jsonl evidence/cycle/tampered/phase-invocations.jsonl
python3 - <<'PY'
import json
from pathlib import Path
path = Path("evidence/cycle/tampered/cycle.jsonl")
lines = path.read_text(encoding="utf-8").splitlines()
frame = json.loads(lines[2])
assert frame["phase"] == "make"
frame["output"] = "TAMPERED-ARTIFACT"
lines[2] = json.dumps(frame, separators=(",", ":"))
path.write_text("\n".join(lines) + "\n", encoding="utf-8")
PY
if python3 proof/cycle/verify-cycle.py "$PWD/evidence/cycle/tampered" > evidence/cycle/tamper-rejection.json; then
  echo "FAIL: verifier accepted corrupted trail" >&2
  exit 1
fi
python3 - <<'PY'
import json
from pathlib import Path
data = json.loads(Path("evidence/cycle/tamper-rejection.json").read_text(encoding="utf-8"))
assert data["status"] == "FAIL", data
print("TAMPER_TEST: PASS (corrupted Make frame rejected)")
PY
