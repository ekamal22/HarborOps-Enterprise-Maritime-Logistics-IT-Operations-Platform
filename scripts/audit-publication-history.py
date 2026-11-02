#!/usr/bin/env python3
"""Read-only disclosure review across Git refs and historical hosted runs."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

REPOSITORY = "ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform"


def execute(arguments, output=None, check=True, env=None):
    with open(output, "wb") if output is not None else open(os.devnull, "wb") as sink:
        return subprocess.run(arguments, stdout=sink, stderr=subprocess.PIPE,
                              check=check, env=env)


def capture(arguments, env=None):
    return subprocess.check_output(arguments, stderr=subprocess.PIPE, text=True, env=env)


def is_e_drive(value):
    normalized = str(value).replace("\\", "/").lower()
    return normalized.startswith(("e:/", "/mnt/e/", "/e/"))


def main():
    if len(sys.argv) != 2 or not is_e_drive(sys.argv[1]) or not is_e_drive(os.getcwd()):
        sys.exit("Run from the E: repository with an E: evidence directory argument.")
    for tool in ("git", "gitleaks", "gh"):
        if not shutil.which(tool):
            sys.exit("Missing required tool: " + tool)

    root = Path(sys.argv[1])
    history = root / "history"
    runs = root / "runs"
    for folder in (history, runs, root / "temp", root / "cache"):
        folder.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    env["TMPDIR"] = str(root / "temp")
    env["TMP"] = str(root / "temp")
    env["TEMP"] = str(root / "temp")
    env["XDG_CACHE_HOME"] = str(root / "cache")
    env["TRIVY_CACHE_DIR"] = str(root / "cache" / "trivy")

    actual = capture(["gh", "repo", "view", "--json", "nameWithOwner",
                      "--jq", ".nameWithOwner"], env).strip()
    if actual != REPOSITORY:
        sys.exit("Repository mismatch: " + actual)

    (history / "source-sha.txt").write_text(capture(["git", "rev-parse", "HEAD"], env))
    inventories = {
        "refs.txt": ["git", "for-each-ref", "--format=%(refname) %(objectname)"],
        "reachable-objects.txt": ["git", "rev-list", "--objects", "--all"],
        "reflog-objects.txt": ["git", "rev-list", "--objects", "--all", "--reflog"],
        "all-commits.txt": ["git", "log", "--all", "--format=%H %aI %cI %s"],
        "unreachable.txt": ["git", "fsck", "--full", "--no-reflogs", "--unreachable"],
    }
    incomplete = []
    for name, arguments in inventories.items():
        result = execute(arguments, history / name, check=False, env=env)
        if result.returncode:
            incomplete.append("inventory: " + name)
            (history / (name + ".error")).write_bytes(result.stderr)

    result = execute(["gitleaks", "git", "--redact", "--log-opts=--all",
                      "--report-format", "json",
                      "--report-path", str(history / "gitleaks.json"), "."],
                     history / "gitleaks-output.txt", check=False, env=env)
    if result.returncode:
        incomplete.append("history findings or scanner error")

    ids_file = runs / "run-ids.txt"
    listed = execute(["gh", "api", "--paginate", "--jq", ".workflow_runs[].id",
                      "repos/" + REPOSITORY + "/actions/runs?per_page=100"],
                     ids_file, check=False, env=env)
    if listed.returncode:
        incomplete.append("run inventory unavailable")
        ids = []
    else:
        ids = ids_file.read_text().splitlines()

    for identifier in ids:
        if not identifier.isdecimal():
            incomplete.append("invalid run identifier")
            continue
        directory = runs / identifier
        directory.mkdir(exist_ok=True)
        logs = execute(["gh", "run", "view", identifier, "--log"],
                       directory / "workflow.log", check=False, env=env)
        if logs.returncode:
            incomplete.append(identifier + ": workflow logs unavailable")
        artifacts = execute(["gh", "api", "repos/" + REPOSITORY +
                             "/actions/runs/" + identifier + "/artifacts?per_page=100"],
                            directory / "artifacts.json", check=False, env=env)
        if artifacts.returncode:
            incomplete.append(identifier + ": artifact metadata unavailable")
            continue
        try:
            count = json.loads((directory / "artifacts.json").read_text()).get("total_count", 0)
        except (ValueError, OSError):
            incomplete.append(identifier + ": artifact metadata unreadable")
            continue
        if count:
            download = execute(["gh", "run", "download", identifier, "--dir",
                                str(directory / "downloaded")],
                               directory / "download-log.txt", check=False, env=env)
            if download.returncode:
                incomplete.append(identifier + ": artifacts unavailable")

    result = execute(["gitleaks", "dir", "--redact", "--report-format", "json",
                      "--report-path", str(runs / "gitleaks.json"), str(runs)],
                     runs / "gitleaks-output.txt", check=False, env=env)
    if result.returncode:
        incomplete.append("historical run findings or scanner error")
    if (history / "unreachable.txt").stat().st_size:
        incomplete.append("unreachable objects require separate review")

    hashes = []
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.name != "file-hashes.txt":
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            hashes.append(digest + "  " + str(path.relative_to(root)))
    (root / "file-hashes.txt").write_text("\n".join(hashes) + "\n")
    summary = {
        "source_sha": (history / "source-sha.txt").read_text().strip(),
        "refs": len((history / "refs.txt").read_text().splitlines()),
        "reachable_objects": len((history / "reachable-objects.txt").read_text().splitlines()),
        "workflow_runs": len(ids),
        "outstanding": incomplete,
        "automated_clearance": not incomplete,
        "manual_clearance_required": True
    }
    (root / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print(json.dumps(summary, indent=2))
    return 1 if incomplete else 0


if __name__ == "__main__":
    raise SystemExit(main())
