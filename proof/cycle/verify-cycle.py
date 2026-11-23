#!/usr/bin/env python3
"""Independent verification of actual five-phase MAF invocation evidence and hash chain."""
import hashlib
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "evidence/cycle"
PHASES = ("orchestrate", "plan", "make", "check", "reflect")
ROLES = ("pmcro-orchestrator", "pmcro-planner", "pmcro-maker", "pmcro-checker", "pmcro-reflector")
SKILLS = (
    "plugins/pmcro-orchestrator/skills/orchestrate/SKILL.md",
    "plugins/pmcro/skills/plan/SKILL.md",
    "plugins/pmcro/skills/make/SKILL.md",
    "plugins/pmcro/skills/check/SKILL.md",
    "plugins/pmcro/skills/reflect/SKILL.md",
)

def digest(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()

def verify(folder):
    frames = [json.loads(x) for x in (folder / "cycle.jsonl").read_text(encoding="utf-8").splitlines() if x.strip()]
    calls = [json.loads(x) for x in (folder / "phase-invocations.jsonl").read_text(encoding="utf-8").splitlines() if x.strip()]
    if len(frames) != len(PHASES) or len(calls) != len(PHASES):
        raise ValueError("Expected exactly five output frames and five real MAF invocation records")
    previous = "GENESIS"
    prev_output = "seed-native-five-phase-001"
    for index, (frame, call) in enumerate(zip(frames, calls), 1):
        phase = PHASES[index-1]
        role = ROLES[index-1]
        skill = SKILLS[index-1]
        content = (ROOT / skill).read_bytes()
        source_hash = hashlib.sha256(content).hexdigest()
        required = (
            frame.get("sequence") == index and frame.get("phase") == phase
            and frame.get("agent_id") == role
            and frame.get("agent_type") == "PmcroPhaseAgent"
            and frame.get("invocation") == "AIAgent.RunAsync"
            and frame.get("source_skill") == skill
            and frame.get("source_skill_sha256") == source_hash
            and frame.get("previous_frame_sha256") == previous
            and frame.get("input") == prev_output
            and frame.get("input_sha256") == digest(frame["input"])
            and frame.get("output_sha256") == digest(frame["output"])
        )
        if not required:
            raise ValueError(f"Invalid frame {index} for phase {phase}")
        expect_hash = digest("|".join((str(index), phase, role, source_hash,
                            frame["input_sha256"], frame["output_sha256"], previous)))
        if frame["frame_sha256"] != expect_hash:
            raise ValueError(f"Frame {index} hash chain mismatch")
        if not (
            call.get("method") == "RunCoreAsync" and call.get("phase") == phase
            and call.get("agent_id") == role and call.get("agent_type") == "PmcroPhaseAgent"
            and call.get("source_skill") == skill and call.get("source_skill_sha256") == source_hash
            and call.get("input") == frame["input"] and call.get("output") == frame["output"]
        ):
            raise ValueError(f"Core native MAF method evidence mismatch at {phase}")
        obj = json.loads(frame["output"])
        if obj.get("phase") != phase or obj.get("seed") != "seed-native-five-phase-001":
            raise ValueError(f"Invalid output semantics for {phase}")
        if phase == "plan" and obj.get("plan_status") != "READY":
            raise ValueError("Plan did not reach READY")
        if phase == "make":
            if obj.get("artifact") != "PMCRO-CYCLE-ARTIFACT|seed-native-five-phase-001":
                raise ValueError("Unexpected Make artifact")
            if obj.get("artifact_sha256") != digest(obj["artifact"]):
                raise ValueError("Make artifact integrity mismatch")
        if phase == "check" and obj.get("check_status") != "PASS":
            raise ValueError("Check did not approve")
        if phase == "reflect" and (obj.get("disposition") != "PASS" or obj.get("next_phase") != "STOP"):
            raise ValueError("Reflect did not terminate")
        prev_output = frame["output"]
        previous = expect_hash
    return {"status": "PASS", "phase_order": list(PHASES),
            "native_maf_run_invocations": len(calls), "trail_frames": len(frames),
            "trail_head_sha256": previous, "final_disposition": "PASS",
            "source_skills_verified": len(SKILLS),
            "llm_inference": "NOT_INVOKED",
            "role_executor": "deterministic AIAgent subclass"}

if __name__ == "__main__":
    try:
        folder = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT
        print(json.dumps(verify(folder), indent=2))
    except (OSError, ValueError, IndexError, TypeError, KeyError) as exc:
        print(json.dumps({"status": "FAIL", "error": str(exc)}))
        sys.exit(1)
