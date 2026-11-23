#!/usr/bin/env python3
"""Independent A2A evidence gate: correlate HTTP card, actual client task,
authored definition and server-side handler invocation. No self-attested PASS accepted.
"""
import hashlib
import json
import pathlib
import sys

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main(root: pathlib.Path, evidence: pathlib.Path):
    dpath = root / "proof/a2a/agents/pmcro-echo.definition.json"
    definition = json.loads(dpath.read_text(encoding="utf-8"))
    client = json.loads((evidence / "client.json").read_text(encoding="utf-8"))
    card = json.loads((evidence / "agent-card.json").read_text(encoding="utf-8"))
    events = [json.loads(x) for x in (evidence / "agent-invocations.jsonl").read_text(encoding="utf-8").splitlines() if x.strip()]
    skill = root / definition["source_skill"]
    expected = definition["response_prefix"] + "|" + definition["agent_id"] + "|" + client["request_input"]
    checks = {
        "well_known_card_http": card.get("name") == definition["name"],
        "well_known_card_skill": any(s.get("id") == definition["skill_id"] for s in card.get("skills", [])),
        "sdk_card_resolution": client.get("card_name") == definition["name"]
            and definition["skill_id"] in client.get("card_skill_ids", []),
        "json_rpc_binding": client.get("card_protocol_binding") == "JSONRPC",
        "task_created": bool(client.get("task_id")),
        "task_retrieved_completed": client.get("task_state", "").lower() == "completed"
            and client.get("retrieved_task_state", "").lower() == "completed",
        "artifact_delivered": client.get("artifact_text") == expected,
        "actual_handler_invocation": any(
            e.get("event_type") == "handler-executed"
            and e.get("handler") == definition["handler_class"]
            and e.get("agent_id") == definition["agent_id"]
            and e.get("definition_sha256") == sha(dpath)
            and e.get("source_skill") == definition["source_skill"]
            and e.get("source_skill_sha256") == sha(skill)
            and e.get("task_id") == client.get("task_id")
            and e.get("input") == client.get("request_input")
            and e.get("output") == expected
            and e.get("result") == "completed"
            for e in events
        ),
        "agent_source_skill": skill.is_file(),
        "client_verified": client.get("verified") is True
    }
    maf_events = [json.loads(x) for x in (evidence / "maf-inner-invocations.jsonl").read_text(encoding="utf-8").splitlines() if x.strip()]
    checks["native_maf_aiaagent_invoked_for_a2a_task"] = any(
        e.get("operation") == "RunCoreAsync"
        and e.get("handler") == "PmcroMafInnerAgent"
        and e.get("agent_id") == definition["agent_id"]
        and e.get("a2a_task_id") == client.get("task_id")
        and e.get("definition_sha256") == sha(dpath)
        and e.get("source_skill_sha256") == sha(skill)
        and e.get("input") == client.get("request_input")
        and e.get("output") == expected
        for e in maf_events
    )
    good = all(checks.values())
    print(json.dumps({"status": "PASS" if good else "FAIL",
                      "checks": checks,
                      "definition_sha256": sha(dpath),
                      "source_skill_sha256": sha(skill),
                      "invocation_count": len(events),
                      "task_id": client.get("task_id"),
                      "native_maf_runtime": "NOT_INVOKED"}, indent=2))
    return 0 if good else 1

if __name__ == "__main__":
    try:
        sys.exit(main(pathlib.Path(__file__).resolve().parents[2], pathlib.Path(sys.argv[1])))
    except (OSError, ValueError, IndexError, KeyError, TypeError) as exc:
        print(json.dumps({"status": "FAIL", "error": str(exc)}))
        sys.exit(2)
