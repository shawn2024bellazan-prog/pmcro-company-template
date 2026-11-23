"""Independent profile audit for the recovery init source projection."""
import hashlib
import json
from pathlib import Path
from config import CATALOG

def _sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def audit(target: Path):
    failures = []
    manifest = target / "profile.json"
    if manifest.is_symlink() or not manifest.is_file():
        return {"status": "FAIL", "checks": [], "failures": ["profile.json missing/unsafe"]}
    try:
        data = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {"status": "FAIL", "checks": [], "failures": ["profile.json invalid"]}
    if data.get("profile") != "pmcro" or data.get("scope") != "MANAGED_CONFIGURATION_PROJECTION":
        failures.append("Profile identity or scope wrong")
    domains = [*CATALOG["domains"], "trails"]
    if data.get("domains") != domains:
        failures.append("Managed domains mismatch")
    for name in domains:
        path = target / name
        if path.is_symlink() or not path.is_dir():
            failures.append("Missing/unsafe domain: " + name)
    expected = sorted(f"{p}--{s}" for p, names in CATALOG["required_source_skills"].items() for s in names)
    if sorted(data.get("skills", [])) != expected:
        failures.append("Required skills omitted")
    for name in expected:
        if not (target / "skills" / name / "SKILL.md").is_file():
            failures.append("Missing SKILL.md: " + name)
    paths = data.get("artifacts", {})
    if not isinstance(paths, dict) or len(paths) < 7:
        failures.append("Artifact index missing/incomplete")
    else:
        for rel, digest in paths.items():
            path = Path(rel)
            if path.is_absolute() or ".." in path.parts:
                failures.append("Unsafe inventory path")
                continue
            out = target / rel
            if out.is_symlink() or not out.is_file() or _sha(out) != digest:
                failures.append("Missing/tampered: " + rel)
    for role in CATALOG["phase_agents"]:
        if role not in data.get("memory_partitions", []):
            failures.append("Missing memory partition: " + role)
    return {"status": "FAIL" if failures else "PASS",
            "checks": ["profile_structure", "expected_source_skills", "artifact_sha256", "memory_partitions"],
            "failures": failures, "artifact_count": len(paths) if isinstance(paths, dict) else 0}
