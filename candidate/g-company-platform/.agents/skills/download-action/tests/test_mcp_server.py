"""Smoke-test real .NET MCP stdio JSON-RPC with a persistent client session."""
import json
import os
from pathlib import Path
import queue
import subprocess
import threading
import unittest

ROOT = Path(__file__).resolve().parents[4]
DLL = ROOT / "plugins/pmcro-runtime/src/ProjectName.Marketplace.Mcp/bin/Release/net10.0/ProjectName.Marketplace.Mcp.dll"


class McpSmokeTests(unittest.TestCase):
    def test_stdio_initializes_lists_and_uses_readonly_tools(self):
        self.assertTrue(DLL.is_file(), "Build the .NET MCP server in Release first")
        env = dict(os.environ, PMCRO_CANDIDATE_ROOT=str(ROOT),
                   PMCRO_LOCAL_INFERENCE_ENABLED="false")
        proc = subprocess.Popen(
            ["dotnet", str(DLL)], cwd=str(ROOT), env=env,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, bufsize=1
        )
        responses = queue.Queue()

        def receiver():
            for line in proc.stdout:
                try:
                    message = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if "id" in message:
                    responses.put(message)

        worker = threading.Thread(target=receiver, daemon=True)
        worker.start()

        def rpc(msg_id, method, params=None):
            message = {"jsonrpc": "2.0", "method": method}
            if msg_id is not None:
                message["id"] = msg_id
            if params is not None:
                message["params"] = params
            proc.stdin.write(json.dumps(message, separators=(",", ":")) + "\n")
            proc.stdin.flush()
            if msg_id is None:
                return None
            response = responses.get(timeout=18)
            self.assertEqual(response.get("id"), msg_id, response)
            self.assertNotIn("error", response, response)
            return response["result"]

        try:
            hello = rpc(1, "initialize", {
                "protocolVersion": "2025-06-18",
                "capabilities": {},
                "clientInfo": {"name": "pmcro-local-smoke-test", "version": "0.1"}
            })
            self.assertIn("protocolVersion", hello)
            rpc(None, "notifications/initialized")
            tools = rpc(2, "tools/list")["tools"]
            names = {x["name"] for x in tools}
            for name in ("list_candidate_marketplaces", "inspect_candidate_marketplace",
                         "get_pmcro_candidate_status", "ollama_local_status",
                         "draft_with_local_ollama"):
                self.assertIn(name, names)
            for n, tool, args in (
                (3, "get_pmcro_candidate_status", {}),
                (4, "list_candidate_marketplaces", {}),
                (5, "draft_with_local_ollama", {"messyIntent": "Propose an intent, execute nothing"})
            ):
                data = rpc(n, "tools/call", {"name": tool, "arguments": args})
                self.assertFalse(data.get("isError", False), data)
                value = json.loads(data["content"][0]["text"])
                self.assertFalse(value.get("grantsAuthority", False))
                if n == 5:
                    self.assertEqual(value["reason"], "local-inference-opt-in-required")
        finally:
            proc.stdin.close()
            try:
                proc.wait(timeout=8)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait(timeout=5)
            proc.stdout.close()
            proc.stderr.close()


if __name__ == "__main__":
    unittest.main()