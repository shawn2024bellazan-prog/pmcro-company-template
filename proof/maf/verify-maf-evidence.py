#!/usr/bin/env python3
"""Correlate MAF response with invocation logs and hash-bound agent definitions."""
import hashlib
import json
from pathlib import Path
import sys

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    root = Path(__file__).resolve().parents[2]
    evidence = root / "evidence/maf"
    defpath = root / "proof/maf/agents/pmcro-native-echo.definition.json"
    definition = json.loads(defpath.read_text(encoding="utf-8"))
    response = json.loads((evidence / "result.json").read_text(encoding="utf-8"))
    events = [json.loads(x) for x in (evidence / "invocations.jsonl").read_text(encoding="utf-8").splitlines() if x.strip()]
    source = root / definition["source_skill"]
    checks = {
        "definition_source_exists": source.is_file(),
        "agent_type": response.get("agent_type") == "PmcroNativeEchoAgent",
        "agent_id": response.get("loaded_agent_id") == definition["agent_id"],
        "definition_sha256": response.get("definition_sha256") == sha(defpath),
        "skill_sha256": response.get("source_skill_sha256") == sha(source),
        "run_response": response.get("run_response") == definition["response_prefix"]+"|"+definition["agent_id"]+"|"+response.get("run_input", ""),
        "stream_response": response.get("streaming_response") == definition["response_prefix"]+"|"+definition["agent_id"]+"|"+response.get("streaming_input", ""),
        "streaming_chunks": int(response.get("streaming_chunks", 0)) > 0,
        "model_run_executed": any(e.get("operation") == "RunCoreAsync"
            and e.get("agent_id") == definition["agent_id"]
            and e.get("definition_sha256") == sha(defpath)
            and e.get("source_skill_sha256") == sha(source)
            and e.get("output") == response.get("run_response") for e in events),
        "model_stream_executed": any(e.get("operation") == "RunCoreStreamingAsync"
            and e.get("agent_id") == definition["agent_id"]
            and e.get("definition_sha256") == sha(defpath)
            and e.get("source_skill_sha256") == sha(source)
            and e.get("output") == response.get("streaming_response") for e in events),
        "returned_verified": response.get("verified") is True,
    }
    ok=all(checks.values())
    print(json.dumps({"status":"PASS" if ok else "FAIL", "checks":checks,
                      "invocation_count":len(events),
                      "agent_id":definition["agent_id"],
                      "definition_sha256":sha(defpath),
                      "llm_inference":"NOT_INVOKED",
                      "pmcro_five_phase_runtime":"NOT_INVOKED"},indent=2))
    return 0 if ok else 1

if __name__ == "__main__":
    try: sys.exit(main())
    except (OSError, ValueError, KeyError, TypeError) as exc:
        print(json.dumps({"status":"FAIL", "error":str(exc)}))
        sys.exit(2)
