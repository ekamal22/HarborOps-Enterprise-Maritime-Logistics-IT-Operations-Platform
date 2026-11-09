#!/usr/bin/env python3
"""Reject exported authentication and report credentials in retained G13/G15 evidence."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / ".verification" / "gate-input"
OUTPUT = ROOT / ".verification" / "gate-ledger"

# These signatures require credential-shaped values, not mere mentions of
# security headers or identifier strings in test names.
PATTERNS = {
    "session-cookie-value": re.compile(
        rb"(?i)\bHarborOps\.Session\s*=\s*[A-Za-z0-9_%+\-/\.]{32,}"),
    "authorization-header-value": re.compile(
        rb"(?im)^\s*Authorization\s*:\s*(?:Bearer|Basic)\s+[A-Za-z0-9+/=_\-.]{20,}"),
    "report-token-response-value": re.compile(
        rb'(?i)"reportToken"\s*:\s*"[A-Za-z0-9_\-\.]{50,}"'),
    "signed-scope-proof": re.compile(
        rb"(?i)m11-read-v1\|[a-f0-9]{32}\|[a-f0-9]{32}\|[a-f0-9]{32}"
        rb"\|[0-9]{10,11}\|[a-f0-9]{64}"),
    "sql-dispatch-proof": re.compile(
        rb"(?i)m15-dispatch-v1\|[0-9]{10,11}\|[a-f0-9]{64}"),
}
EXTENSIONS = {".log", ".txt", ".json", ".trx", ".xml", ".md", ".jsonl"}
SOURCE = os.environ.get("GITHUB_SHA", "")

def main() -> int:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    if not ARTIFACTS.is_dir() or not SOURCE or len(SOURCE) != 40:
        raise RuntimeError("Source-locked artifact input is required")
    files = sorted(p for p in ARTIFACTS.rglob("*")
                   if p.is_file() and p.suffix.lower() in EXTENSIONS)
    if len(files) < 30:
        raise RuntimeError("Incomplete source-locked evidence input: too few files")
    issues = []
    checks = []
    for path in files:
        data = path.read_bytes()
        kinds = [name for name, pattern in PATTERNS.items()
                 if pattern.search(data)]
        entry = {"path": str(path.relative_to(ARTIFACTS)),
                 "sha256": hashlib.sha256(data).hexdigest(),
                 "bytes": len(data), "credential_signatures": kinds}
        checks.append(entry)
        if kinds:
            issues.append({"path": entry["path"], "rules": kinds})
    report = {
        "source_sha": SOURCE,
        "run_id": os.environ.get("GITHUB_RUN_ID", ""),
        "status": "PASS" if not issues else "FAIL",
        "inspected_files": len(files),
        "checks": checks,
        "findings": issues,
        "sample_values_logged": False
    }
    target = OUTPUT / "sensitive-evidence-audit.json"
    target.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    with (OUTPUT / "sha256.txt").open("a", encoding="utf-8") as hashes:
        hashes.write(hashlib.sha256(target.read_bytes()).hexdigest() +
                     "  " + target.name + "\n")
    print(json.dumps({"source_sha": SOURCE, "status": report["status"],
                      "files": len(files), "findings": issues}, indent=2))
    return 0 if not issues else 1

if __name__ == "__main__":
    sys.exit(main())
