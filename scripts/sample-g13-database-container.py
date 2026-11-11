#!/usr/bin/env python3
"""Measure a dedicated disposable PostgreSQL container while a command runs.

Read-only resource sampling. The caller provides the exact disposable container
and command. Does not start, stop, prune or change any container.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time


def source_sha() -> str:
    result = subprocess.run(["git", "rev-parse", "HEAD"], check=True,
                            capture_output=True, text=True)
    return result.stdout.strip()


def sample(container: str) -> dict[str, str]:
    result = subprocess.run(
        ["docker", "stats", "--no-stream", "--format", "{{json .}}", container],
        capture_output=True, text=True, timeout=12, check=True)
    rows = [json.loads(line) for line in result.stdout.splitlines() if line.strip()]
    if len(rows) != 1:
        raise RuntimeError("One exact disposable PostgreSQL container required")
    data = rows[0]
    return {
        "cpu_percent": str(data.get("CPUPerc", "")),
        "memory_usage": str(data.get("MemUsage", "")),
        "memory_percent": str(data.get("MemPerc", "")),
        "net_io": str(data.get("NetIO", "")),
        "block_io": str(data.get("BlockIO", "")),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--container", required=True,
                        help="Exact dedicated disposable PostgreSQL container name")
    parser.add_argument("--output", required=True, type=Path,
                        help="Output directory outside the repository")
    parser.add_argument("--interval", type=float, default=1.0)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if not args.command or args.command[0] != "--":
        parser.error("Supply the command after a standalone --")
    command = args.command[1:]
    if not command or args.interval < 0.2:
        parser.error("Provide a command and interval >= 0.2 seconds")
    args.output.mkdir(parents=True, exist_ok=True)
    # Fail before starting the measured task if the selected container is not
    # uniquely accessible. Never discover or substitute another container.
    initial = sample(args.container)
    rows: list[dict[str, str]] = []
    errors: list[str] = []
    started = time.time()
    with (args.output / "command.log").open("wb") as log:
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT)
        while process.poll() is None:
            try:
                rows.append({"utc_unix": f"{time.time():.3f}",
                             **sample(args.container)})
            except (subprocess.CalledProcessError, subprocess.TimeoutExpired,
                    ValueError, RuntimeError) as error:
                errors.append(type(error).__name__ + ": " + str(error))
            time.sleep(args.interval)
        code = process.wait()
    if not rows:
        rows.append({"utc_unix": f"{time.time():.3f}", **initial})
    csv_path = args.output / "database-container-resources.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    def digest(path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()
    summary = {
        "source_sha": source_sha(),
        "container": args.container,
        "command": command,
        "exit_code": code,
        "sample_count": len(rows),
        "sample_errors": errors,
        "first_sample": initial,
        "started_unix": started,
        "finished_unix": time.time(),
        "csv_sha256": digest(csv_path),
        "command_log_sha256": digest(args.output / "command.log"),
        "status": "PASS" if code == 0 and rows and not errors else "INCOMPLETE",
    }
    (args.output / "resource-summary.json").write_text(
        json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"source_sha": summary["source_sha"],
                      "status": summary["status"], "samples": len(rows),
                      "exit_code": code, "errors": len(errors)}), flush=True)
    return 0 if summary["status"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
