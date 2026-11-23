"""New recovery module for the installed pmcro-commands:init handler.
Not claimed to be a recovered upstream source implementation.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
CATALOG = {
    "domains": ["agents", "skills", "commands", "agent-memory", "rules",
                "output-styles", "workflows", "projects", "themes", "plugins"],
    "required_source_skills": {
        "pmcro": ("orchestrate", "plan", "make", "check", "reflect"),
        "pmcro-orchestrator": ("orchestrate", "decompose-goal"),
    },
    "phase_agents": ("orchestrator", "planner", "maker", "checker", "reflector"),
}

def capabilities():
    for plugin, names in CATALOG["required_source_skills"].items():
        for skill in names:
            yield {"plugin": plugin, "name": skill,
                   "skill_path": str(ROOT / plugin / "skills" / skill)}
