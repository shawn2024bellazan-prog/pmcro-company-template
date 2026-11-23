#!/usr/bin/env python3
"""Local /init profile generator, using authoritative source and atomic publish."""
import argparse
import hashlib
import json
import sys
import tempfile
from pathlib import Path

SCRIPT = Path(__file__).resolve()
PLUGINS = SCRIPT.parents[4]
sys.path.insert(0, str(PLUGINS / "pmcro/skills/pmcro-agent/scripts"))
from config import CATALOG, ROOT, capabilities  # noqa: E402
from profile_builder import build_profile  # noqa: E402
from audit import audit  # noqa: E402


def digest(obj):
    return hashlib.sha256(json.dumps(obj, sort_keys=True, separators=(",", ":"),
                                     ensure_ascii=False).encode()).hexdigest()


def project_root(path):
    if path.is_symlink() or not path.is_dir():
        raise ValueError("Authorized project root must be an existing non-symlink directory")
    root = path.resolve(strict=True)
    if (root / ".agents/plugins/marketplace.json").is_file() and (root / "plugins/pmcro").is_dir():
        raise ValueError("Refusing to initialize the authoritative marketplace source itself")
    return root


def verify(root):
    root = project_root(root)
    target = root / ".pmcro"
    if target.is_symlink() or not target.is_dir():
        raise ValueError("Missing or symlinked managed .pmcro profile")
    receipt_path = target / "trails/init-receipt.json"
    profile_path = target / "profile.json"
    if (receipt_path.is_symlink() or profile_path.is_symlink() or
            not receipt_path.is_file() or not profile_path.is_file()):
        raise ValueError("Missing or unsafe initialization receipt/manifest")
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    body = dict(receipt)
    seal = body.pop("receipt_id", None)
    if seal != digest(body) or receipt.get("operation") != "/init":
        raise ValueError("Initialization receipt hash mismatch")
    if receipt.get("profile_sha256") != hashlib.sha256(profile_path.read_bytes()).hexdigest():
        raise ValueError("Profile manifest does not match initialization receipt")
    profile = json.loads(profile_path.read_text(encoding="utf-8"))
    if profile.get("profile") != "pmcro" or profile.get("scope") != "MANAGED_CONFIGURATION_PROJECTION":
        raise ValueError("Invalid profile metadata")
    domains = [*CATALOG["domains"], "trails"]
    for domain in domains:
        path = target / domain
        if path.is_symlink() or not path.is_dir():
            raise ValueError("Missing or symlinked initialized domain: " + domain)
    for name in profile.get("skills", []):
        path = target / "skills" / name
        if path.is_symlink() or not (path / "SKILL.md").is_file():
            raise ValueError("Missing projected skill: " + name)
    if len(profile.get("skills", [])) != receipt.get("skill_count"):
        raise ValueError("Projected skill count differs from receipt")
    for name in ("seed", "init"):
        if not (target / "commands" / (name + ".prompt.md")).is_file():
            raise ValueError("Missing projected command: " + name)
    return {"status": "PASS", "self_audit": "PASS", "profile": "pmcro",
            "domains": len(domains), "skills": len(profile["skills"]),
            "memory_partitions": len(profile["memory_partitions"]),
            "receipt_id": seal, "receipt_path": ".pmcro/trails/init-receipt.json",
            "execution": "LOCAL_PYTHON", "native_runtime": "NOT_INVOKED"}


def write(root):
    root = project_root(root)
    target = root / ".pmcro"
    if target.exists() or target.is_symlink():
        result = verify(root)
        result["status"] = "ALREADY_INITIALIZED"
        return result
    with tempfile.TemporaryDirectory(prefix=".pmcro-init-", dir=root) as temp:
        staging = Path(temp) / ".pmcro"
        metadata = build_profile(staging, "pmcro", with_proof=False)
        checks = audit(staging)
        if checks["status"] != "PASS":
            raise ValueError("Source profile audit failed")
        manifest = staging / "profile.json"
        body = {"operation": "/init", "status": "INITIALIZED", "profile": "pmcro",
                "profile_sha256": hashlib.sha256(manifest.read_bytes()).hexdigest(),
                "skill_count": len(metadata["skills"]),
                "memory_partitions": len(metadata["memory_partitions"]),
                "audit": checks["checks"], "native_runtime": "NOT_INVOKED"}
        receipt = {**body, "receipt_id": digest(body)}
        with (staging / "trails/init-receipt.json").open("x", encoding="utf-8") as out:
            json.dump(receipt, out, indent=2)
            out.write("\n")
        if target.exists() or target.is_symlink():
            raise ValueError("Another initializer already created .pmcro; refusing to replace")
        staging.rename(target)
    return verify(root)


def main():
    parser = argparse.ArgumentParser(allow_abbrev=False)
    sub = parser.add_subparsers(dest="operation", required=True)
    for op in ("write", "verify"):
        sub.add_parser(op, allow_abbrev=False).add_argument("--root", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = write(args.root) if args.operation == "write" else verify(args.root)
        result.update({"/pmcro:command": "/init", "/pmcro:status": result["status"]})
        print(json.dumps(result, indent=2))
        return 0
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as exc:
        print(json.dumps({"status": "FAIL", "error": str(exc), "native_runtime": "NOT_INVOKED"}))
        return 1


if __name__ == "__main__":
    sys.exit(main())
