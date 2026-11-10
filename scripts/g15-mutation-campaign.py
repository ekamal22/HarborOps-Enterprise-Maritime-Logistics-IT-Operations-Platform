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
        "IF session_user <> 'harborops_api' OR NOT m15_dispatch_allowed()\n"
        "                    OR p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 20 THEN",
        "IF session_user <> 'harborops_api'\n"
        "                    OR p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 20 THEN",
        "RestrictedSqlLoginCannotEnumerateOutboxOrWriteAnotherRecipient",
    ),
    (
        "source-intent-failure-swallowed",
        "RETURN NEW;\n            END $m15$;",
        "RETURN NEW;\n            EXCEPTION WHEN OTHERS THEN RETURN NEW;\n            END $m15$;",
        "FailedOutboxInsertRollsBackNewImmutableHistoryInSameTransaction",
    ),
    (
        "receipt-replay-session-unbound",
        "AND old_replay.actor_session_id=p_session",
        "AND true",
        "CursorCannotCrossSessionsAndReplayCannotChangeItsOriginalMeaning",
    ),
    (
        "receipt-cas-bypassed",
        "IF item.version <> p_expected_version THEN",
        "IF false THEN",
        "CursorCannotCrossSessionsAndReplayCannotChangeItsOriginalMeaning",
    ),
    (
        "receipt-recipient-uniqueness-removed",
        "CONSTRAINT uq_m15_event_recipient UNIQUE(source_history_id,recipient_user_id),",
        "CONSTRAINT uq_m15_event_recipient CHECK (recipient_user_id IS NOT NULL),",
        "DuplicatePersonalReceiptDatabaseConstraintRejectsReplay",
    ),
    (
        "source-history-outbox-uniqueness-removed",
        "source_history_id uuid PRIMARY KEY REFERENCES shipment_exception_history(id) ON DELETE RESTRICT,",
        "source_history_id uuid NOT NULL REFERENCES shipment_exception_history(id) ON DELETE RESTRICT,",
        "SourceHistoryOutboxPrimaryKeyRejectsDuplicateEvent",
    ),
    (
        "signed-recipient-scope-unbound",
        "IF fields[2]::uuid IS DISTINCT FROM p_recipient THEN RETURN false; END IF;",
        "IF false THEN RETURN false; END IF;",
        "SignedRecipientContextCannotReadAnotherUserNotifications",
    ),
    (
        "receipt-audit-skipped",
        "IF p_action='Acknowledge' THEN\n                        SELECT m.role",
        "IF false THEN\n                        SELECT m.role",
        "RequiredAuditFailureRollsBackReceiptAndReplay",
    ),
]

def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def call(args: list[str], timeout: int = 400) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args, cwd=ROOT, check=False, text=True, stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, timeout=timeout, env=os.environ.copy())


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

def main() -> int:
    if os.getenv("HARBOROPS_G13_EXCLUSIVE_POSTGRES") != "1":
        raise RuntimeError("Exclusive disposable PostgreSQL is required")
    db_connection = os.getenv("HARBOROPS_TEST_DB", "")
    match = re.search(r"Database=([a-zA-Z0-9_]+)", db_connection)
    if not match or match.group(1) not in ("harborops_integration", "harborops_test"):
        raise RuntimeError("Expected a recognized disposable database")
    found = call(["docker", "ps", *_postgres_filter(),
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
            if name == "source-history-outbox-uniqueness-removed":
                # The dependent receipt FK and ON CONFLICT target must be moved
                # with the removed primary key. Otherwise PostgreSQL rejects
                # DDL and the variant is not a valid effective mutation.
                for original_anchor, changed_anchor in (
                    ("REFERENCES m15_notification_outbox(source_history_id) ON DELETE RESTRICT,",
                     "REFERENCES shipment_exception_history(id) ON DELETE RESTRICT,"),
                    ("ON CONFLICT (source_history_id) DO NOTHING;",
                     "ON CONFLICT DO NOTHING;"),
                ):
                    if mutated.count(original_anchor) != 1:
                        raise RuntimeError(name + ": dependent anchor mismatch")
                    mutated = mutated.replace(original_anchor, changed_anchor)
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
            "source_commit": _source_identity(),
            "source_sha256": sha(original),
            "records": records,
        }, indent=2) + "\n", encoding="utf-8")
    return 0 if (
        len(records) == len(MUTANTS) + 1 and records[0]["outcome"] == "PASS"
        and all(row["outcome"] == "KILLED" for row in records[1:])
    ) else 1

if __name__ == "__main__":
    sys.exit(main())
