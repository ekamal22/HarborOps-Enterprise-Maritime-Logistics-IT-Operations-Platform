#!/usr/bin/env python3
"""Source-locked disposable G13 mutation checks. Never writes to Git history."""
from __future__ import annotations
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "src/HarborOps.Infrastructure/Reporting/OperationalKpiStore.cs"
EVIDENCE = ROOT / ".verification/g13-mutations"
FILTER = "FullyQualifiedName~M13OperationalKpiRuntimeTests.TwelveShipmentsAndTwoForeignDecoysReconcileExactMembers"
MUTANTS = [
    ("k01-include-completed",
     "WHERE s.record_status='Active' AND s.workflow_status NOT IN ('Completed','Cancelled')",
     "WHERE s.record_status='Active' AND s.workflow_status <> 'Cancelled'"),
    ("k05-count-all-arrivals",
     "WHERE c.arrived_at <= c.planned_arrival_at_utc\n",
     "WHERE c.arrived_at IS NOT NULL\n"),
    ("k06-remove-tolerance",
     "WHERE c.arrived_at <= c.planned_arrival_at_utc + interval '60 minutes'",
     "WHERE c.arrived_at <= c.planned_arrival_at_utc + interval '0 minutes'"),
    ("k04-drop-cancellations",
     "JOIN events e ON e.shipment_id=s.id AND e.to_status='Cancelled'",
     "JOIN events e ON e.shipment_id=s.id AND e.to_status='Booked'"),
    ("k02-false-status",
     "SELECT 'K02',s.workflow_status,s.id,s.id,NULL,NULL FROM scoped s",
     "SELECT 'K02','Draft',s.id,s.id,NULL,NULL FROM scoped s"),
    ("coverage-force-complete",
     "WHERE q.fresh_rules=4",
     "WHERE q.fresh_rules>=0"),
    ("quality-include-draft",
     "FROM heads h LEFT JOIN evaluated e ON e.id=h.id\n            WHERE h.record_status='Active' AND h.workflow_status NOT IN ('Draft','Cancelled')",
     "FROM heads h LEFT JOIN evaluated e ON e.id=h.id\n            WHERE h.record_status='Active'"),
    ("unassessed-suppress-missing",
     "WHERE q.fresh_rules<4 OR q.unknown_rules>0",
     "WHERE q.fresh_rules>10 OR q.unknown_rules>10"),
    ("report-cap-wrong-comparison",
     "if (count > OperationalKpiPolicy.MaximumReportMembers)",
     "if (count < OperationalKpiPolicy.MaximumReportMembers)"),
]
def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def execute(label: str) -> tuple[int, str, float]:
    args = [
        "dotnet", "test",
        "tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj",
        "-c", "Release", "--filter", FILTER,
        "--logger", "console;verbosity=normal",
    ]
    start = time.monotonic()
    try:
        p = subprocess.run(args, cwd=ROOT, stdout=subprocess.PIPE,
                           stderr=subprocess.STDOUT, text=True, timeout=300,
                           env=os.environ.copy(), check=False)
        output = p.stdout
        code = p.returncode
    except subprocess.TimeoutExpired as exc:
        raw = exc.stdout or b""
        output = raw.decode("utf-8", errors="replace") if isinstance(raw, bytes) else raw
        output += "\nTIMEOUT after 300 seconds\n"
        code = 124
    seconds = round(time.monotonic() - start, 2)
    (EVIDENCE / f"{label}.log").write_text(output, encoding="utf-8")
    return code, output, seconds

def main() -> int:
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    original = SOURCE.read_bytes()
    source_sha = digest(original)
    identity = os.getenv("GITHUB_SHA", "local")
    records: list[dict[str, object]] = []
    try:
        code, output, elapsed = execute("control")
        records.append({"name":"control","source_sha256":source_sha,
                        "exit_code":code,"elapsed_seconds":elapsed,
                        "outcome":"PASS" if code == 0 else "FAIL"})
        if code:
            print("G13 mutation control failed. Mutations not attempted.")
            return 2
        text = original.decode("utf-8")
        for name, before, after in MUTANTS:
            if text.count(before) != 1:
                raise RuntimeError(f"{name}: expected exactly one replacement, found {text.count(before)}")
            changed = text.replace(before, after)
            SOURCE.write_text(changed, encoding="utf-8")
            code, output, elapsed = execute(name)
            if code == 124 or "Build FAILED" in output or "error CS" in output or "error xUnit" in output:
                outcome = "INVALID"
            elif code == 0:
                outcome = "SURVIVED"
            elif "TwelveShipmentsAndTwoForeignDecoysReconcileExactMembers" in output and (
                "Failed " in output or "[FAIL]" in output):
                outcome = "KILLED"
            else:
                outcome = "INVALID"
            records.append({"name":name,"source_sha256":digest(changed.encode()),
                            "exit_code":code,"elapsed_seconds":elapsed,"outcome":outcome})
            print(f"G13_MUTANT {name}: {outcome} ({elapsed}s)", flush=True)
            SOURCE.write_bytes(original)
    finally:
        SOURCE.write_bytes(original)
        if digest(SOURCE.read_bytes()) != source_sha:
            raise RuntimeError("G13 mutant source not restored")
        (EVIDENCE / "manifest.json").write_text(json.dumps({
            "source_commit":identity,"clean_source_sha256":source_sha,
            "filter":FILTER,"records":records
        }, indent=2) + "\n", encoding="utf-8")
    return 0 if records and all(x["outcome"] == "KILLED" for x in records[1:]) else 1

if __name__ == "__main__":
    sys.exit(main())
