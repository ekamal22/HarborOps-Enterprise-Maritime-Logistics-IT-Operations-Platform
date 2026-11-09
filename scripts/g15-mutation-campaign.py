#!/usr/bin/env python3
"""Run effective M15 security mutations against an exclusive disposable database."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "src/HarborOps.Infrastructure/Persistence/Migrations/20261109010000_M15ScopedNotifications.cs"
OUT = ROOT / ".verification/g15-mutations"
MUTANTS = [
    (
        "history-action-filter-removed",
        "IF NEW.action IN ('Opened','Resolved','AutoResolved') THEN",
        "IF NEW.action IS NOT NULL THEN",
        "OnlyImmutableM11OpeningAndClosureActionsGenerateOutboxRows",
    ),
    (
        "personal-read-policy-bypassed",
        "USING (m15_can_read(organization_id,recipient_user_id,\n"
        "                    membership_id,membership_version,shipment_id));",
        "USING (true);",
        "M11HistoryProducesScopedDeduplicatedPersonalReceipts",
    ),
    (
        "membership-version-revocation-ignored",
        "AND m.user_id=p_recipient AND m.version=p_membership_version",
        "AND m.user_id=p_recipient",
        "ConcurrentDispatchAndMembershipVersionChangesNeverExposeStaleReceipts",
    ),
    (
        "dispatch-scope-proof-bypassed",
        "IF session_user <> 'harborops_api' OR NOT m15_dispatch_allowed() THEN",
        "IF session_user <> 'harborops_api' THEN",
        "RestrictedSqlLoginCannotEnumerateOutboxOrWriteAnotherRecipient",
    ),
]

def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def call(args: list[str], timeout: int = 400) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args, cwd=ROOT, check=False, text=True, stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, timeout=timeout, env=os.environ.copy())

def main() -> int:
    if os.getenv("HARBOROPS_G13_EXCLUSIVE_POSTGRES") != "1":
        raise RuntimeError("Exclusive disposable PostgreSQL is required")
    db_connection = os.getenv("HARBOROPS_TEST_DB", "")
    match = re.search(r"Database=([a-zA-Z0-9_]+)", db_connection)
    if not match or match.group(1) not in ("harborops_integration", "harborops_test"):
        raise RuntimeError("Expected a recognized disposable database")
    found = call(["docker", "ps", "--filter", "ancestor=postgres:16.15",
                  "--format", "{{.ID}}"])
    containers = found.stdout.strip().splitlines()
    if found.returncode or len(containers) != 1:
        raise RuntimeError("Expected exactly one exclusive disposable PostgreSQL server")
    container = containers[0]
    original = SOURCE.read_bytes()
    clean = original.decode("utf-8")
    OUT.mkdir(parents=True, exist_ok=True)
    records: list[dict] = []
    ordered = [("clean-control", None, None, None)] + MUTANTS
    try:
        for name, before, after, target in ordered:
            if before is not None and clean.count(before) != 1:
                raise RuntimeError(name + ": unique mutation anchor not found")
            mutated = clean.replace(before, after) if before is not None else clean
            SOURCE.write_text(mutated, encoding="utf-8")
            reset = call([
                "docker", "exec", container, "psql", "-v", "ON_ERROR_STOP=1",
                "-U", "harborops", "-d", match.group(1), "-c",
                "DROP SCHEMA IF EXISTS public CASCADE; "
                "CREATE SCHEMA public AUTHORIZATION harborops; "
                "GRANT USAGE,CREATE ON SCHEMA public TO PUBLIC;"
            ], timeout=60)
            if reset.returncode:
                raise RuntimeError("Disposable schema reset failed: " + reset.stdout[-500:])
            # The boundary suite intentionally alters global database constraints.
            # Keep the clean controls in separate test processes to prevent
            # collisions with the regular notification runtime suite.
            filters = (
                ["FullyQualifiedName~M15NotificationRuntimeTests",
                 "FullyQualifiedName~M15NotificationBoundaryTests"]
                if target is None else ["FullyQualifiedName~" + target])
            test_filter = " ; ".join(filters)
            start = time.monotonic()
            code = 0
            chunks = []
            for active_filter in filters:
                try:
                    proc = call([
                        "dotnet", "test",
                        "tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj",
                        "-c", "Release", "--filter", active_filter,
                        "--logger", "console;verbosity=normal"
                    ])
                    chunks.append(proc.stdout)
                    if proc.returncode:
                        code = proc.returncode
                        break
                except subprocess.TimeoutExpired as exc:
                    raw = exc.stdout or b""
                    chunks.append(raw.decode("utf-8", errors="replace")
                                  if isinstance(raw, bytes) else raw)
                    chunks.append("\nTIMEOUT\n")
                    code = 124
                    break
            output = "\n".join(chunks)
            (OUT / (name + ".log")).write_text(output, encoding="utf-8")
            if code == 124 or "Build FAILED" in output or "error CS" in output:
                verdict = "INVALID"
            elif target is None:
                verdict = "PASS" if code == 0 else "FAIL"
            elif code == 0:
                verdict = "SURVIVED"
            elif target in output and ("Failed " in output or "[FAIL]" in output):
                verdict = "KILLED"
            else:
                verdict = "INVALID"
            records.append({
                "name": name, "outcome": verdict, "source_sha256": sha(mutated.encode()),
                "exit_code": code, "seconds": round(time.monotonic() - start, 2),
                "target": target, "filter": test_filter,
            })
            print("G15_MUTANT " + name + " " + verdict, flush=True)
            SOURCE.write_bytes(original)
            if name == "clean-control" and verdict != "PASS":
                break
    finally:
        SOURCE.write_bytes(original)
        if sha(SOURCE.read_bytes()) != sha(original):
            raise RuntimeError("Mutation source restoration failed")
        (OUT / "manifest.json").write_text(json.dumps({
            "source_commit": os.getenv("GITHUB_SHA", ""),
            "source_sha256": sha(original),
            "records": records,
        }, indent=2) + "\n", encoding="utf-8")
    return 0 if (
        len(records) == len(MUTANTS) + 1 and records[0]["outcome"] == "PASS"
        and all(row["outcome"] == "KILLED" for row in records[1:])
    ) else 1

if __name__ == "__main__":
    sys.exit(main())
