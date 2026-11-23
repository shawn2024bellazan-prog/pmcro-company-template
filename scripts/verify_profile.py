#!/usr/bin/env python3
"""Independent re-check of PMCR-O managed profile, inventory and sealed receipt."""
import argparse
import hashlib
import json
from pathlib import Path

def digest(obj):
    return hashlib.sha256(json.dumps(obj, sort_keys=True, separators=(",", ":"),
                                     ensure_ascii=False).encode()).hexdigest()

def verify(root):
    root = root.resolve(strict=True)
    target = root / ".pmcro"
    rp = target / "trails/init-receipt.json"
    pp = target / "profile.json"
    if target.is_symlink() or rp.is_symlink() or pp.is_symlink():
        raise ValueError("Symlinked managed profile, receipt, or manifest")
    receipt = json.loads(rp.read_text(encoding="utf-8"))
    profile = json.loads(pp.read_text(encoding="utf-8"))
    body = {k: v for k, v in receipt.items() if k != "receipt_id"}
    if digest(body) != receipt.get("receipt_id"):
        raise ValueError("Receipt hash mismatch")
    if receipt.get("profile_sha256") != hashlib.sha256(pp.read_bytes()).hexdigest():
        raise ValueError("Manifest hash mismatch")
    if receipt.get("skill_count") != len(profile["skills"]):
        raise ValueError("Skill count mismatch")
    if receipt.get("native_runtime") != "NOT_INVOKED":
        raise ValueError("Runtime provenance violation")
    if profile.get("implementation") != "RECOVERY_MODULES_NOT_ORIGINAL_UPSTREAM":
        raise ValueError("Recovery module provenance missing")
    hashes = profile.get("artifacts", {})
    for rel, expected in hashes.items():
        relpath = Path(rel)
        if relpath.is_absolute() or ".." in relpath.parts:
            raise ValueError("Unsafe artifact path")
        path = target / relpath
        if path.is_symlink() or not path.is_file():
            raise ValueError("Missing/symlinked artifact: " + rel)
        if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            raise ValueError("Artifact SHA256 mismatch: " + rel)
    return {"status": "PASS", "receipt_id": receipt["receipt_id"],
            "skills": len(profile["skills"]), "artifacts_verified": len(hashes),
            "runtime": "NOT_INVOKED", "scope": profile["scope"]}

if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--root", type=Path, required=True)
    args = p.parse_args()
    try:
        print(json.dumps(verify(args.root), indent=2))
    except Exception as exc:
        print(json.dumps({"status": "FAILED", "reason": str(exc)}))
        raise SystemExit(1)
