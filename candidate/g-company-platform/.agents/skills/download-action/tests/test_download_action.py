import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "download_action.py"
spec = importlib.util.spec_from_file_location("download_action", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class CandidateDownloaderTests(unittest.TestCase):
    def cli(self, *args):
        return subprocess.run([sys.executable, str(SCRIPT), *args],
                              capture_output=True, text=True, timeout=15)

    def test_inspection_never_grants_or_downloads(self):
        result = self.cli()
        self.assertEqual(result.returncode, 0, result.stderr)
        data = json.loads(result.stdout)
        for field in ("downloaded", "registered", "granted", "activated", "executed"):
            self.assertFalse(data[field])

    def test_local_hash_verify_only(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / "test.bin"
            p.write_bytes(b"verified-test-only")
            digest = hashlib.sha256(b"verified-test-only").hexdigest()
            result = self.cli("--mode", "verify", "--file", str(p), "--sha256", digest)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(json.loads(result.stdout)["sha256"], digest)

    def test_wrong_hash_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / "test.bin"
            p.write_bytes(b"wrong")
            result = self.cli("--mode", "verify", "--file", str(p), "--sha256", "0" * 64)
            self.assertEqual(result.returncode, 2)
            self.assertIn("sha256-mismatch", result.stderr)

    def test_stage_needs_explicit_approval(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / "candidate-action-downloads"
            p.mkdir()
            result = self.cli("--mode", "stage", "--url", "https://github.com/x/y",
                              "--sha256", "0" * 64, "--filename", "x.zip", "--stage-dir", str(p))
            self.assertEqual(result.returncode, 2)
            self.assertEqual(list(p.iterdir()), [])

    def test_untrusted_sources_rejected(self):
        for url in ("file:///tmp/a", "http://github.com/x", "https://evil.example/x",
                    "https://github.com.evil.example/a", "https://user:pw@github.com/x"):
            with self.subTest(url=url):
                with self.assertRaises(ValueError):
                    module.validate_url(url)

    def test_no_unsafe_filename_or_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / "candidate-action-downloads"
            p.mkdir()
            for filename in ("../x", "a/b", "..", "."):
                with self.subTest(filename=filename):
                    with self.assertRaises(ValueError):
                        module.stage("https://github.com/x/y", "0" * 64, filename, str(p), True)
            self.assertEqual(list(p.iterdir()), [])
            with self.assertRaises(ValueError):
                module.stage("https://github.com/x/y", "0" * 64, "a.zip", directory, True)


if __name__ == "__main__":
    unittest.main()