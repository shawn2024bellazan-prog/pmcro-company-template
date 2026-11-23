"""Source-derived .pmcro configuration projection for pmcro-commands:init.
RECOVERY IMPLEMENTATION; original upstream modules were not available.
"""
import hashlib
import json
import shutil
from pathlib import Path
from config import CATALOG, ROOT

def sha(data):
    return hashlib.sha256(data).hexdigest()

def json_write(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, indent=2, sort_keys=True, ensure_ascii=False) + "\n", encoding="utf-8")

def safe_tree(source):
    if source.is_symlink() or not source.is_dir():
        raise ValueError("Missing or symlinked source: " + str(source))
    for child in source.rglob("*"):
        if child.is_symlink():
            raise ValueError("Source symlink rejected: " + str(child))
        if not (child.is_file() or child.is_dir()):
            raise ValueError("Source special file rejected: " + str(child))

def build_profile(target: Path, profile: str, with_proof=False):
    if profile != "pmcro" or with_proof:
        raise ValueError("Only pmcro managed projection supported; no simulated proof")
    if target.exists() and any(target.iterdir()):
        raise ValueError("Projection target must be empty")
    if not ROOT.is_dir():
        raise ValueError("Marketplace source plugin directory missing")
    for domain in [*CATALOG["domains"], "trails"]:
        (target / domain).mkdir(parents=True, exist_ok=True)
    artifacts = {}
    projected = []
    source_skills = []
    for plugin, names in CATALOG["required_source_skills"].items():
        manifest = ROOT / plugin / "plugin.json"
        if manifest.is_symlink() or not manifest.is_file():
            raise ValueError("Missing plugin manifest: " + str(manifest))
        parsed = json.loads(manifest.read_text(encoding="utf-8"))
        if parsed.get("name") != plugin:
            raise ValueError("Wrong plugin manifest name: " + str(manifest))
        source_skills.append({"path": manifest.relative_to(ROOT).as_posix(),
                              "sha256": sha(manifest.read_bytes())})
        for name in names:
            source = ROOT / plugin / "skills" / name
            safe_tree(source)
            skill = source / "SKILL.md"
            if not skill.is_file():
                raise ValueError("Missing required source SKILL.md: " + str(skill))
            dest_name = f"{plugin}--{name}"
            dest = target / "skills" / dest_name
            shutil.copytree(source, dest)
            projected.append(dest_name)
            for file in sorted(dest.rglob("*")):
                if file.is_file():
                    rel = file.relative_to(target).as_posix()
                    artifacts[rel] = sha(file.read_bytes())
                    source_skills.append({
                        "path": (source / file.relative_to(dest)).relative_to(ROOT).as_posix(),
                        "sha256": artifacts[rel],
                    })
    for role in CATALOG["phase_agents"]:
        rel = f"agents/{role}/AGENT.md"
        out = target / rel
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(f"# {role.title()}\n\nPMCR-O role projection; native runtime NOT_INVOKED.\n", encoding="utf-8")
        artifacts[rel] = sha(out.read_bytes())
        mem_rel = f"agent-memory/{role}/MEMORY.md"
        mem = target / mem_rel
        mem.parent.mkdir(parents=True, exist_ok=True)
        mem.write_text(f"# {role.title()} memory partition\n\nNo runtime memories.\n", encoding="utf-8")
        artifacts[mem_rel] = sha(mem.read_bytes())
    for command in ("seed", "init"):
        rel = f"commands/{command}.prompt.md"
        out = target / rel
        out.write_text(
            f"---\nname: {command}\n---\n\nUse the installed pmcro-commands:{command} handler.\n"
            "Generated command projections do not invoke handlers.\n",
            encoding="utf-8",
        )
        artifacts[rel] = sha(out.read_bytes())
    data = {
        "profile": profile, "scope": "MANAGED_CONFIGURATION_PROJECTION",
        "schema_version": "1.0.0-recovery",
        "skills": projected, "memory_partitions": list(CATALOG["phase_agents"]),
        "domains": CATALOG["domains"] + ["trails"],
        "source": "versioned-repository-snapshot",
        "source_skills": source_skills,
        "artifacts": artifacts,
        "native_runtime": "NOT_INVOKED",
        "implementation": "RECOVERY_MODULES_NOT_ORIGINAL_UPSTREAM",
    }
    json_write(target / "profile.json", data)
    return data
