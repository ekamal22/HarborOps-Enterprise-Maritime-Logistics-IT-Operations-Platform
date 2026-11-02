#!/usr/bin/env python3
"""Current-source dependency and container vulnerability review on E:."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys


def resolved(arguments):
    """Resolve the executable through PATH so Windows .cmd wrappers (npm, pinned scanner images) work."""
    return [shutil.which(arguments[0]) or arguments[0], *arguments[1:]]


def on_e(path):
    path = str(path).replace("\\", "/").lower()
    return path.startswith(("e:/", "/mnt/e/", "/e/"))


def run(command, destination, env):
    with destination.open("wb") as output:
        result = subprocess.run(resolved(command), stdout=output, stderr=subprocess.PIPE, env=env)
    if result.stderr:
        destination.with_suffix(destination.suffix + ".stderr").write_bytes(result.stderr)
    return result.returncode


def main():
    if len(sys.argv) != 2 or not on_e(sys.argv[1]) or not on_e(os.getcwd()):
        sys.exit("Run from an E:-backed checkout with an E:-backed evidence directory.")
    for name in ("git", "dotnet", "npm", "docker", "trivy"):
        if shutil.which(name) is None:
            sys.exit("Missing scanner prerequisite: " + name)
    os.umask(0o077)
    root = Path(sys.argv[1])
    root.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    locations = {
        "TMPDIR": "temp", "TMP": "temp", "TEMP": "temp", "XDG_CACHE_HOME": "xdg",
        "NUGET_PACKAGES": "nuget/packages", "NUGET_HTTP_CACHE_PATH": "nuget/http",
        "DOTNET_CLI_HOME": "dotnet-home", "npm_config_cache": "npm",
        "TRIVY_CACHE_DIR": "trivy"
    }
    for key, relative in locations.items():
        directory = root / relative
        directory.mkdir(parents=True, exist_ok=True)
        env[key] = str(directory)
    sha = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True, env=env).strip()
    (root / "source-sha.txt").write_text(sha + "\n")

    results = {}
    commands = {
        "dotnet": ["dotnet", "list", "HarborOps.sln", "package",
                   "--vulnerable", "--include-transitive", "--format", "json"],
        "npm-web": ["npm", "audit", "--prefix", "web", "--audit-level=high", "--json"],
        "npm-browser": ["npm", "audit", "--prefix", "tests/browser",
                        "--audit-level=high", "--json"]
    }
    for name, command in commands.items():
        results[name] = run(command, root / (name + ".json"), env)

    for service in ("postgres", "api", "web"):
        image = subprocess.run(["docker", "compose", "images", "-q", service],
                               capture_output=True, text=True, env=env)
        image_id = image.stdout.strip()
        if image.returncode != 0 or not image_id:
            results["image-" + service] = "missing image"
            continue
        revision = subprocess.run(
            ["docker", "image", "inspect", image_id, "--format",
             "{{index .Config.Labels \"org.opencontainers.image.revision\"}}"],
            capture_output=True, text=True, env=env)
        if revision.returncode != 0 or revision.stdout.strip() != sha:
            results["image-" + service] = "image revision does not match checked source"
            continue
        results["image-" + service] = run(
            ["trivy", "image", "--scanners", "vuln", "--severity", "HIGH,CRITICAL",
             "--exit-code", "1", "--format", "json", "--output",
             str(root / ("trivy-" + service + ".json")), image_id],
            root / ("trivy-" + service + ".log"), env)

    # A successful dotnet command must also have empty vulnerability lists.
    try:
        projects = json.loads((root / "dotnet.json").read_text()).get("projects", [])
        vulnerable = [
            package for project in projects for framework in project.get("frameworks", [])
            for group in ("topLevelPackages", "transitivePackages")
            for package in framework.get(group, [])
            if package.get("vulnerabilities")
        ]
        if not projects or not any(project.get("frameworks") for project in projects):
            results["dotnet"] = "no usable package inventory"
        elif vulnerable:
            results["dotnet"] = "vulnerable packages reported"
    except (OSError, ValueError, AttributeError, TypeError):
        results["dotnet"] = "unreadable scanner output"

    digest = []
    for path in sorted(root.glob("*.json")):
        digest.append(hashlib.sha256(path.read_bytes()).hexdigest() + "  " + path.name)
    (root / "hashes.sha256").write_text("\n".join(digest) + "\n")
    (root / "results.json").write_text(json.dumps(
        {"source_sha": sha, "scan_results": results,
         "all_checks_passed": all(value == 0 for value in results.values())}, indent=2) + "\n")
    print((root / "results.json").read_text())
    return 0 if all(value == 0 for value in results.values()) else 1


if __name__ == "__main__":
    raise SystemExit(main())
