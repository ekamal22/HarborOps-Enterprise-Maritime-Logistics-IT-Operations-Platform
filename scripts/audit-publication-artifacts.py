#!/usr/bin/env python3
"""Inspect retained verification artifacts for disclosure without recording secret values."""
import hashlib
import io
import json
import os
from pathlib import Path
import re
import sys
import zipfile

AUTH_COOKIE = re.compile(
    rb"HarborOps\.(?:Session|Antiforgery)\s*=\s*[A-Za-z0-9_.%+\-/=]{12,}", re.I
)
API_SECRET = re.compile(
    rb"(?:github_pat_[A-Za-z0-9_]{25,}|gh[pousr]_[A-Za-z0-9_]{25,}|AKIA[0-9A-Z]{16})"
)
PRIVATE_KEY = re.compile(rb"-----BEGIN (?:RSA |OPENSSH |EC |DSA )?PRIVATE KEY-----")
LIMIT = 80 * 1024 * 1024
TEXT_EXTS = frozenset({
    ".trace", ".json", ".har", ".log", ".txt", ".md", ".xml", ".http",
    ".html", ".mjs", ".js", ".yml", ".yaml", ".css", ".csv"
})


def on_e(path):
    normalized = str(path).replace("\\", "/").lower()
    return normalized.startswith(("e:/", "/mnt/e/", "/e/"))


def main():
    if len(sys.argv) != 3 or not on_e(sys.argv[1]) or not on_e(sys.argv[2]):
        sys.exit("Usage: python scripts/audit-publication-artifacts.py E:/review/runs E:/review/artifact-summary.json")
    os.umask(0o077)
    root = Path(sys.argv[1])
    report = Path(sys.argv[2])
    issues = []
    checked = {"archives": 0, "entries": 0, "text_bytes": 0, "binary_omitted": 0}
    hashes = []

    def scan_data(data, label):
        if len(data) > LIMIT:
            issues.append({"path": label, "kind": "oversize", "count": 1})
            return
        checked["text_bytes"] += len(data)
        for name, expression in (("test_cookie_value", AUTH_COOKIE),
                                 ("provider_token", API_SECRET),
                                 ("private_key", PRIVATE_KEY)):
            count = len(expression.findall(data))
            if count:
                issues.append({"path": label, "kind": name, "count": count})

    def scan_archive(container, label, depth=0):
        if depth > 4:
            issues.append({"path": label, "kind": "nested_archive_depth", "count": 1})
            return
        try:
            with zipfile.ZipFile(container) as archive:
                checked["archives"] += 1
                for item in archive.infolist():
                    if item.is_dir():
                        continue
                    checked["entries"] += 1
                    inner_label = label + "/" + item.filename
                    if item.file_size > LIMIT:
                        issues.append({"path": inner_label, "kind": "oversize", "count": 1})
                        continue
                    lower = item.filename.lower()
                    suffix = Path(lower).suffix
                    if suffix not in TEXT_EXTS and suffix != ".zip":
                        checked["binary_omitted"] += 1
                        continue
                    data = archive.read(item)
                    if suffix == ".zip":
                        scan_archive(io.BytesIO(data), inner_label, depth + 1)
                    else:
                        scan_data(data, inner_label)
        except (OSError, zipfile.BadZipFile, RuntimeError) as error:
            issues.append({"path": label, "kind": "archive_read_failure", "count": 1})

    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        relative = str(path.relative_to(root))
        if path.stat().st_size > LIMIT:
            issues.append({"path": relative, "kind": "oversize", "count": 1})
            continue
        if path.suffix.lower() == ".zip":
            scan_archive(path, relative)
        elif path.suffix.lower() in TEXT_EXTS:
            scan_data(path.read_bytes(), relative)
        else:
            checked["binary_omitted"] += 1
        hashes.append({
            "path": relative, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()
        })

    result = {
        "checked": checked,
        "issues": issues,
        "file_hashes": hashes,
        "publication_clearance": False,
        "manual_review_required": True
    }
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("Artifact paths and issue counts saved; credential contents omitted.")
    print("Issue count:", len(issues))
    return 1 if issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
