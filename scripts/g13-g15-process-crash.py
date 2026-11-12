#!/usr/bin/env python3
"""Kill disposable API testhost processes before and after database commit."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import time
from uuid import UUID

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / ".verification" / "g13-g15-crash"
PROJECT = "tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj"
TYPE = "HarborOps.IntegrationTests.G13G15CrashRecoveryTests"
PHASES = (
    ("G13BeforeReportCommit", None),
    ("G13DuringLiveApiBuild", "G13LiveBuildRecoveredInNewApiProcess"),
    ("G15BeforeHistoryCommit", None),
    ("G15BeforeClaimCommit", "G15FanoutRecoveredInNewApiProcess"),
    ("G15BeforeFanoutCommit", "G15FanoutRecoveredInNewApiProcess"),
    ("G15AfterFanoutCommit", "G15FanoutRecoveredInNewApiProcess"),
    ("G13AfterReportCommit", "G13AfterReportCommitRecoveredInNewApiProcess"),
    ("G15AfterReceiptCommit", "G15AfterReceiptCommitRecoveredInNewApiProcess"),
    ("G15AfterResponseDiscarded", "G15AfterReceiptCommitRecoveredInNewApiProcess"),
)

def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _source_identity() -> str:
    """Hosted runs use GITHUB_SHA. Local runs record the actual checked out HEAD."""
    value = os.environ.get("GITHUB_SHA")
    if value:
        return value
    return subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True,
                          stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                          check=True).stdout.strip()

def _postgres_filter() -> list[str]:
    """Optional exact container name for local runs with several disposable servers."""
    name = os.environ.get("HARBOROPS_EXCLUSIVE_PG_CONTAINER")
    base = ["--filter", "ancestor=postgres:16.15"]
    return base + (["--filter", "name=^/" + name + "$"] if name else [])

def docker_server() -> str:
    found = subprocess.run(
        ["docker", "ps", *_postgres_filter(),
         "--format", "{{.ID}}"], text=True, capture_output=True, check=True)
    containers = found.stdout.strip().splitlines()
    if len(containers) != 1:
        raise RuntimeError("Expected exactly one exclusive disposable PostgreSQL service")
    return containers[0]

def sql_count(container: str, query: str) -> int:
    result = subprocess.run([
        "docker", "exec", container, "psql", "-X", "-A", "-t",
        "-v", "ON_ERROR_STOP=1", "-U", "harborops",
        "-d", "harborops_integration", "-c", query
    ], text=True, capture_output=True, timeout=30, check=True)
    return int(result.stdout.strip())

def verify_phase(container: str, phase: str, marker: dict) -> None:
    org = str(UUID(marker["Organization"]))
    target = str(UUID(marker["Target"]))
    if phase == "G13BeforeReportCommit":
        count = sql_count(container,
            f"SELECT count(*) FROM operational_kpi_reports "
            f"WHERE id='{target}'::uuid AND organization_id='{org}'::uuid")
        members = sql_count(container,
            f"SELECT count(*) FROM operational_kpi_members "
            f"WHERE report_id='{target}'::uuid AND organization_id='{org}'::uuid")
        if count != 0 or members != 0:
            raise AssertionError("Killed pre-commit report left materialized state")
    elif phase == "G13DuringLiveApiBuild":
        reports = sql_count(container,
            f"SELECT count(*) FROM operational_kpi_reports "
            f"WHERE organization_id='{org}'::uuid")
        members = sql_count(container,
            f"SELECT count(*) FROM operational_kpi_members "
            f"WHERE organization_id='{org}'::uuid")
        if reports != 0 or members != 0:
            raise AssertionError("Killed live API report left partial committed state")
    elif phase == "G15BeforeHistoryCommit":
        facts = sql_count(container,
            f"SELECT count(*) FROM shipment_exception_history WHERE id='{target}'::uuid")
        intents = sql_count(container,
            f"SELECT count(*) FROM m15_notification_outbox "
            f"WHERE source_history_id='{target}'::uuid")
        if facts != 0 or intents != 0:
            raise AssertionError("Killed pre-commit history leaked M11/M15 rows")
    elif phase == "G13AfterReportCommit":
        count = sql_count(container,
            f"SELECT count(*) FROM operational_kpi_reports "
            f"WHERE id='{target}'::uuid AND organization_id='{org}'::uuid")
        members = sql_count(container,
            f"SELECT count(*) FROM operational_kpi_members "
            f"WHERE report_id='{target}'::uuid AND organization_id='{org}'::uuid")
        if count != 1 or members < 1:
            raise AssertionError("Committed report disappeared after process termination")
    elif phase in ("G15BeforeClaimCommit", "G15BeforeFanoutCommit",
                   "G15AfterFanoutCommit"):
        outbox = sql_count(container,
            f"SELECT count(*) FROM m15_notification_outbox "
            f"WHERE source_history_id='{target}'::uuid "
            f"AND organization_id='{org}'::uuid "
            f"AND completed_at_utc IS "
            + ("NOT NULL" if phase == "G15AfterFanoutCommit" else "NULL"))
        receipts = sql_count(container,
            f"SELECT count(*) FROM m15_user_notifications "
            f"WHERE source_history_id='{target}'::uuid "
            f"AND organization_id='{org}'::uuid")
        if outbox != 1:
            raise AssertionError(phase + ": outbox state did not survive as expected")
        if phase == "G15AfterFanoutCommit":
            if receipts < 1:
                raise AssertionError("Committed fanout receipts did not survive kill")
        elif receipts:
            raise AssertionError("Uncommitted claim/fanout exposed personal receipts")
    elif phase in ("G15AfterReceiptCommit", "G15AfterResponseDiscarded"):
        count = sql_count(container,
            f"SELECT count(*) FROM m15_user_notifications "
            f"WHERE id='{target}'::uuid AND organization_id='{org}'::uuid "
            "AND acknowledged_at_utc IS NOT NULL AND read_at_utc IS NOT NULL")
        audit = sql_count(container,
            f"SELECT count(*) FROM audit_entries "
            f"WHERE organization_id='{org}'::uuid AND resource_id='{target}'::uuid "
            "AND event_type='notification.receipt_acknowledged'")
        if count != 1 or audit != 1:
            raise AssertionError("Committed receipt or required audit missing after kill")

def run() -> int:
    if os.environ.get("HARBOROPS_G13_EXCLUSIVE_POSTGRES") != "1" or \
       os.environ.get("HARBOROPS_G13_G15_CRASH_PROBE") != "1":
        raise RuntimeError("Exclusive disposable crash campaign required")
    database = os.environ.get("HARBOROPS_TEST_DB", "")
    if not re.search(r"Database=harborops_integration(?:;|$)", database):
        raise RuntimeError("Refusing an unrecognized database")
    OUT.mkdir(parents=True, exist_ok=True)
    container = docker_server()
    records = []
    scope = os.environ.get("HARBOROPS_CRASH_SCOPE", "combined")
    if scope not in ("combined", "G15"):
        raise RuntimeError("Unsupported crash verification scope")
    selected = tuple((phase, recovery) for phase, recovery in PHASES
                     if scope == "combined" or phase.startswith("G15"))
    if not selected:
        raise RuntimeError("No crash phases selected")
    try:
        for phase, recovery in selected:
            marker = OUT / (phase + ".json")
            marker.unlink(missing_ok=True)
            logpath = OUT / (phase + ".log")
            env = os.environ.copy()
            env["HARBOROPS_CRASH_MARKER"] = str(marker)
            start = time.monotonic()
            with logpath.open("wb") as log:
                process = subprocess.Popen([
                    "dotnet", "test", PROJECT, "-c", "Release",
                    "--no-build", "--filter", "FullyQualifiedName~" + TYPE + "." + phase,
                    "--logger", "console;verbosity=normal"
                ], cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT,
                    start_new_session=True)
                try:
                    deadline = time.monotonic() + 150
                    while not marker.is_file() and time.monotonic() < deadline:
                        if process.poll() is not None:
                            raise RuntimeError(
                                phase + ": testhost exited before crash marker")
                        time.sleep(0.2)
                    if not marker.is_file():
                        raise TimeoutError(phase + ": crash marker deadline exceeded")
                    payload = json.loads(marker.read_text(encoding="utf-8"))
                    if payload.get("Phase") != phase:
                        raise RuntimeError(phase + ": unexpected crash marker")
                finally:
                    if process.poll() is None:
                        os.killpg(process.pid, signal.SIGKILL)
                    process.wait(timeout=15)
            if process.returncode >= 0:
                raise RuntimeError(phase + ": process did not terminate by signal")
            verify_phase(container, phase, payload)
            if recovery is not None:
                result = subprocess.run([
                    "dotnet", "test", PROJECT, "-c", "Release",
                    "--no-build", "--filter", "FullyQualifiedName~" + TYPE + "." + recovery,
                    "--logger", "console;verbosity=normal",
                    "--logger", f"trx;LogFileName={phase}-recovery.trx",
                    "--results-directory", str(OUT),
                ], cwd=ROOT, env=env, capture_output=True, text=True,
                   timeout=180, check=False)
                (OUT / (phase + "-recovery.log")).write_text(
                    result.stdout + "\n" + result.stderr, encoding="utf-8")
                if result.returncode:
                    raise RuntimeError(phase + ": restarted API recovery test failed")
            records.append({
                "phase": phase, "result": "KILLED_AND_VERIFIED",
                "recovery": "PASS" if recovery else "NOT_APPLICABLE",
                "duration_seconds": round(time.monotonic()-start,2),
                "marker_sha256": digest(marker),
                "log_sha256": digest(logpath),
            })
            print("G13_G15_CRASH " + phase + " KILLED_AND_VERIFIED", flush=True)
        return 0
    finally:
        (OUT / "manifest.json").write_text(json.dumps({
            "source_commit": _source_identity(),
            "disposable_postgres": True, "records": records
        }, indent=2) + "\n", encoding="utf-8")

if __name__ == "__main__":
    sys.exit(run())
