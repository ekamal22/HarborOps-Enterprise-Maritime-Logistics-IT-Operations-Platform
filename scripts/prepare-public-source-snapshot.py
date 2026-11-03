#!/usr/bin/env python3
"""Prepare a source-only inspection archive without the private Git history.

The archive is never published by this program. All outputs must be on E:.
"""
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import zipfile

SAFE_DIRS = ("src/", "web/", "docker/", "tests/")
SAFE_ROOT = frozenset({
    ".dockerignore", ".env.example", ".gitattributes", ".gitignore",
    "Directory.Build.props", "HarborOps.sln", "LICENSE", "compose.yaml",
    "global.json"
})
CREDENTIAL_PATTERNS = (
    re.compile(rb"-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----"),
    re.compile(rb"gh[pousr]_[A-Za-z0-9_]{24,}"),
    re.compile(rb"AKIA[0-9A-Z]{16}"),
    re.compile(rb"(?:sk_live|rk_live)_[A-Za-z0-9]{16,}"),
)
LOCAL_PATH = re.compile(rb"(?:[A-Za-z]:\\(?:Users|APP IDEAS|HarborOps)|/home/[^/\s]+/)")
EMAIL = re.compile(rb"[A-Za-z0-9._%+-]+@([A-Za-z0-9.-]+\.[A-Za-z]{2,})", re.I)
ALLOWED_DOMAINS = {"example.test", "host.test", "example.com", "example.org", "example.net"}


def resolved(arguments):
    """Resolve the executable through PATH so Windows .cmd wrappers work."""
    return [shutil.which(arguments[0]) or arguments[0], *arguments[1:]]


def e_drive(path):
    value = str(path).replace("\\", "/").lower()
    return value.startswith(("e:/", "/mnt/e/", "/e/"))


def capture(args):
    return subprocess.check_output(args, stderr=subprocess.PIPE)


def main():
    if len(sys.argv) != 2 or not e_drive(sys.argv[1]) or not e_drive(os.getcwd()):
        sys.exit("Use an E:-backed repository and E:-backed output directory.")
    if shutil.which("git") is None or shutil.which("gitleaks") is None:
        sys.exit("The Git and Gitleaks executables are required.")
    os.umask(0o077)
    root = Path(sys.argv[1])
    root.mkdir(parents=True, exist_ok=True)
    if any(root.iterdir()):
        sys.exit("Output directory must be empty to avoid overwriting accepted evidence.")

    head = capture(["git", "rev-parse", "HEAD"]).decode().strip()
    dirty = capture(["git", "status", "--porcelain"])
    if dirty:
        sys.exit("A clean source worktree is required.")
    paths = capture(["git", "ls-tree", "-r", "--name-only", "-z", head]).decode().split("\0")
    paths = [p for p in paths if p and (p in SAFE_ROOT or p.startswith(SAFE_DIRS))]
    if "LICENSE" not in paths:
        sys.exit("A source-viewing license is required.")

    archive = root / "source-review.zip"
    manifest = []
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as output:
        for name in sorted(paths):
            item = PurePosixPath(name)
            if item.is_absolute() or ".." in item.parts:
                sys.exit("Unsafe archive path.")
            data = capture(["git", "show", head + ":" + name])
            if b"\0" in data:
                sys.exit("Unexpected binary content: " + name)
            if any(expression.search(data) for expression in CREDENTIAL_PATTERNS):
                sys.exit("Sensitive-content review required: " + name)
            if LOCAL_PATH.search(data):
                sys.exit("Workstation-specific content review required: " + name)
            domains = {v.decode().lower() for v in EMAIL.findall(data)}
            if domains - ALLOWED_DOMAINS:
                sys.exit("Non-synthetic email disclosure review required: " + name)
            output.writestr(name, data)
            manifest.append({"path": name, "size": len(data),
                             "sha256": hashlib.sha256(data).hexdigest()})

        overview = (
            "# HarborOps\n\n"
            "Source inspection snapshot of a local-first maritime logistics "
            "IT operations prototype.\n\n"
            "This archive contains a point-in-time source tree only. "
            "It deliberately excludes prior Git history, development branch "
            "metadata, historical verification evidence and local environment paths.\n\n"
            "The included Docker Compose stack is for local loopback development. "
            "It must not be exposed directly to the internet. "
            "A hosted deployment requires separate security review.\n\n"
            "Consult LICENSE before using or redistributing this source.\n"
        ).encode()
        output.writestr("README.md", overview)
        manifest.append({"path": "README.md", "size": len(overview),
                         "sha256": hashlib.sha256(overview).hexdigest()})

    # Review the exact candidate bytes before they can be considered for export.
    stage = root / "scan-tree"
    stage.mkdir()
    with zipfile.ZipFile(archive, "r") as candidate:
        candidate.extractall(stage)
    results = root / "gitleaks.json"
    scan = subprocess.run(resolved([
        "gitleaks", "dir", "--redact", "--report-format", "json",
        "--report-path", str(results), str(stage)
    ]), capture_output=True, text=True)
    if scan.returncode != 0:
        print("Snapshot secret scan did not pass. Do not distribute this archive.")
        print("Inspect the protected evidence directory for findings.")
        return 1
    shutil.rmtree(stage)

    report = root / "snapshot-manifest.json"
    report.write_text(json.dumps({"source_sha": head, "file_count": len(manifest),
                                  "files": manifest}, indent=2) + "\n", encoding="utf-8")

    # This archive is only a review candidate. Never auto-publish or mark cleared.
    print("Prepared candidate archive:", archive)
    print("Source revision:", head)
    print("Files:", len(manifest))
    print("Manual disclosure, dependency-license and security clearance are required.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
