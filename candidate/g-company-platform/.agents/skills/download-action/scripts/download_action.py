#!/usr/bin/env python3
"""Candidate-only action staging: pinned HTTPS bytes; never install, import or execute."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import tempfile
import urllib.error
import urllib.parse
import urllib.request

HOSTS = frozenset(("github.com", "api.github.com", "raw.githubusercontent.com",
                   "objects.githubusercontent.com", "release-assets.githubusercontent.com"))
MAX_BYTES = 100 * 1024 * 1024
SHA256 = re.compile(r"[a-fA-F0-9]{64}\Z")
SAFE_NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,127}\Z")


def validate_url(url: str) -> None:
    parsed = urllib.parse.urlsplit(url)
    try:
        port = parsed.port
    except ValueError as error:
        raise ValueError("source-invalid-port") from error
    if (parsed.scheme != "https" or parsed.hostname not in HOSTS or
            parsed.username is not None or parsed.password is not None or
            port not in (None, 443) or not parsed.path.startswith("/")):
        raise ValueError("source-not-approved-https-github-host")


class BoundedRedirects(urllib.request.HTTPRedirectHandler):
    def __init__(self):
        super().__init__()
        self.count = 0

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        self.count += 1
        if self.count > 5:
            raise ValueError("too-many-redirects")
        validate_url(newurl)
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def digest_file(path: Path):
    sha = hashlib.sha256()
    count = 0
    with path.open("rb") as stream:
        while part := stream.read(1048576):
            sha.update(part)
            count += len(part)
    return sha.hexdigest(), count


def stage(url, sha256, filename, stage_dir, approved):
    if not approved:
        raise ValueError("explicit-owner-stage-approval-required")
    validate_url(url)
    if not SHA256.fullmatch(sha256):
        raise ValueError("expected-sha256-required")
    if filename in (".", "..") or not SAFE_NAME.fullmatch(filename):
        raise ValueError("unsafe-filename")
    original = Path(stage_dir)
    if original.is_symlink() or not original.is_dir() or original.name != "candidate-action-downloads":
        raise ValueError("preexisting-candidate-action-downloads-directory-required")
    root = original.resolve(strict=True)
    target = root / filename
    if target.exists() or target.is_symlink():
        raise FileExistsError("staging-target-exists-no-overwrite")
    tmp = None
    try:
        with tempfile.NamedTemporaryFile(dir=root, prefix=".candidate-", suffix=".part",
                                         delete=False) as out:
            tmp = Path(out.name)
            sha = hashlib.sha256()
            size = 0
            opener = urllib.request.build_opener(BoundedRedirects())
            req = urllib.request.Request(url, headers={"User-Agent": "PMCR-O-candidate-stager/0.1"})
            with opener.open(req, timeout=20) as response:
                length = response.headers.get("Content-Length")
                if length is not None and int(length) > MAX_BYTES:
                    raise ValueError("artifact-too-large")
                while part := response.read(1048576):
                    size += len(part)
                    if size > MAX_BYTES:
                        raise ValueError("artifact-too-large")
                    out.write(part)
                    sha.update(part)
            out.flush()
            os.fsync(out.fileno())
            observed = sha.hexdigest()
            if observed.lower() != sha256.lower():
                raise ValueError("sha256-mismatch")
        # Atomic no-overwrite hardlink: do not install, extract, execute or register.
        os.link(tmp, target)
        return {"status": "staged-only", "path": str(target),
                "sha256": observed, "bytes": size, "activated": False, "executed": False}
    finally:
        if tmp is not None:
            tmp.unlink(missing_ok=True)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Candidate action staging; never execute or install")
    parser.add_argument("--mode", choices=("inspect", "verify", "stage"), default="inspect")
    parser.add_argument("--url")
    parser.add_argument("--sha256")
    parser.add_argument("--filename")
    parser.add_argument("--stage-dir")
    parser.add_argument("--file")
    parser.add_argument("--approve-stage", action="store_true")
    args = parser.parse_args(argv)
    try:
        if args.mode == "inspect":
            print(json.dumps({"status": "candidate-only", "hosts": sorted(HOSTS),
                              "maxBytes": MAX_BYTES, "downloaded": False,
                              "registered": False, "granted": False, "activated": False, "executed": False}))
            return 0
        if not args.sha256 or not SHA256.fullmatch(args.sha256):
            raise ValueError("expected-sha256-required")
        if args.mode == "verify":
            if not args.file:
                raise ValueError("file-required")
            observed, size = digest_file(Path(args.file))
            if observed.lower() != args.sha256.lower():
                raise ValueError("sha256-mismatch")
            print(json.dumps({"status": "verified-local-only", "sha256": observed, "bytes": size}))
            return 0
        if not all((args.url, args.filename, args.stage_dir)):
            raise ValueError("stage-requires-url-filename-and-directory")
        print(json.dumps(stage(args.url, args.sha256, args.filename,
                               args.stage_dir, args.approve_stage)))
        return 0
    except (ValueError, OSError, urllib.error.URLError) as error:
        print(json.dumps({"status": "blocked", "reason": str(error)}), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())