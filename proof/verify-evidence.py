#!/usr/bin/env python3
"""Independent evidence gate. A prewritten success string without full data fails."""
import json
import pathlib
import sys
try:
    source = pathlib.Path(sys.argv[1])
    item = json.loads(source.read_text(encoding="utf-8"))
    checks = {
        "initialize": item.get("initialize") is True,
        "tools_list_add": any(str(s).lower() == "add" for s in item.get("tools_list", [])),
        "tools_call_add": str(item.get("tools_call", "")).lower() == "add",
        "arguments": item.get("arguments") == {"a": 2, "b": 3},
        "result_equals_five": str(item.get("content_text", "")).strip(' \n\r"') == "5",
        "client_verified": item.get("verified") is True,
    }
    passed = all(checks.values())
    print(json.dumps({"status": "PASS" if passed else "FAILED", "checks": checks}, indent=2))
    sys.exit(0 if passed else 1)
except (OSError, ValueError, IndexError, TypeError, KeyError) as exc:
    print(json.dumps({"status": "FAILED", "error": str(exc)}))
    sys.exit(2)
