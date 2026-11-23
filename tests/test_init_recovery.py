"""Subprocess tests using the repository's REAL skill source, copied to temporary roots.
No native agent execution is claimed.
"""
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGINS = ROOT / "plugins"
VERIFY = ROOT / "scripts/verify_profile.py"

def run(*argv):
    return subprocess.run([sys.executable, *map(str, argv)], text=True, capture_output=True)

class InitRecoveryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="pmcro-init-test-")
        self.addCleanup(self.temp.cleanup)
        self.project = Path(self.temp.name) / "project"
        self.project.mkdir()
        self.plugins = Path(self.temp.name) / "plugins"
        shutil.copytree(PLUGINS, self.plugins)
        self.handler = self.plugins / "pmcro-commands/skills/init/scripts/init.py"

    def invoke(self, operation):
        return run(self.handler, operation, "--root", self.project)

    def test_real_source_write_verify_and_idempotence(self):
        first = self.invoke("write")
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
        self.assertEqual(json.loads(first.stdout)["status"], "PASS")
        receipt = self.project / ".pmcro/trails/init-receipt.json"
        original = receipt.read_bytes()
        self.assertEqual(self.invoke("verify").returncode, 0)
        independ = run(VERIFY, "--root", self.project)
        self.assertEqual(independ.returncode, 0, independ.stdout + independ.stderr)
        second = self.invoke("write")
        self.assertEqual(second.returncode, 0)
        self.assertEqual(json.loads(second.stdout)["status"], "ALREADY_INITIALIZED")
        self.assertEqual(receipt.read_bytes(), original)

    def test_receipt_tamper_rejected(self):
        self.assertEqual(self.invoke("write").returncode, 0)
        receipt = self.project / ".pmcro/trails/init-receipt.json"
        data = json.loads(receipt.read_text())
        data["skill_count"] = 9999
        receipt.write_text(json.dumps(data))
        self.assertNotEqual(self.invoke("verify").returncode, 0)
        self.assertNotEqual(run(VERIFY, "--root", self.project).returncode, 0)

    def test_skill_tamper_rejected_by_independent_verifier(self):
        self.assertEqual(self.invoke("write").returncode, 0)
        skill = self.project / ".pmcro/skills/pmcro--plan/SKILL.md"
        skill.write_text(skill.read_text() + "\nTAMPERED\n")
        self.assertNotEqual(run(VERIFY, "--root", self.project).returncode, 0)

    def test_missing_skill_rejected(self):
        self.assertEqual(self.invoke("write").returncode, 0)
        (self.project / ".pmcro/skills/pmcro--plan/SKILL.md").unlink()
        self.assertNotEqual(self.invoke("verify").returncode, 0)

    def test_failed_source_is_atomic(self):
        (self.plugins / "pmcro/skills/plan/SKILL.md").unlink()
        self.assertNotEqual(self.invoke("write").returncode, 0)
        self.assertFalse((self.project / ".pmcro").exists())

if __name__ == "__main__":
    unittest.main()
