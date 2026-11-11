#!/usr/bin/env python3
"""Verify immutable campaign artifacts and assemble conservative G13/G15 gate coverage."""
from __future__ import annotations

import hashlib
import json
import os
import re
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
problems: list[str] = []

# Two mutually exclusive evidence modes. Hosted mode (default) is unchanged and
# requires the GitHub-provided identity. Local mode never reads or fabricates any
# GITHUB_* value: it takes the actual source SHA, a local campaign id, the input
# and output folders explicitly, and derives every job result from the recorded
# exit status of each executed command (commands.tsv), never from a summary.
LOCAL = os.environ.get("HARBOROPS_EVIDENCE_MODE") == "local"
MODE = "local" if LOCAL else "github-actions"
if LOCAL:
    SOURCE = os.environ["HARBOROPS_SOURCE_SHA"]
    CAMPAIGN = os.environ["HARBOROPS_CAMPAIGN_ID"]
    RUN = "local:" + CAMPAIGN
    INPUT = Path(os.environ["HARBOROPS_GATE_INPUT"])
    OUTPUT = Path(os.environ["HARBOROPS_GATE_OUTPUT"])
else:
    SOURCE = os.environ["GITHUB_SHA"]
    RUN = os.environ["GITHUB_RUN_ID"]
    INPUT = ROOT / ".verification" / "gate-input"
    OUTPUT = ROOT / ".verification" / "gate-ledger"

JOB_ARTIFACTS = {
    "source": "combined-source", "database": "m15-database",
    "browser": "m15-browser", "scale": "g13-scale",
    "sql_mutations": "g13-security-mutations", "faults": "g13-faults",
    "source_mutations": "g13-mutations", "notification_mutations": "g15-mutations",
    "notification_scale": "g15-scale", "notification_fanout": "g15-fanout",
    "reporting_concurrency": "g13-concurrency", "crash_recovery": "g13-g15-crashes",
    "dependency_security": "dependency-security",
    "combined_regressions": "combined-regressions",
}

def local_job_result(prefix: str) -> str:
    """success only when commands.tsv exists, is non empty and every exit is 0."""
    folders = [p for p in INPUT.iterdir()
               if p.is_dir() and p.name.startswith(prefix + "-")] if INPUT.is_dir() else []
    if len(folders) != 1:
        return "missing"
    table = folders[0] / "commands.tsv"
    if not table.is_file():
        return "missing"
    rows = [line.split("\t") for line in table.read_text(encoding="utf-8").splitlines()
            if line.strip()]
    if not rows or any(len(r) < 6 for r in rows):
        return "malformed"
    return "success" if all(r[3] == "0" for r in rows) else "failure"

if LOCAL:
    JOBS = {key: local_job_result(prefix) for key, prefix in JOB_ARTIFACTS.items()}
else:
    JOBS = {
        "source": os.environ.get("GATE_JOB_SOURCE", "unknown"),
        "database": os.environ.get("GATE_JOB_DATABASE", "unknown"),
        "browser": os.environ.get("GATE_JOB_BROWSER", "unknown"),
        "scale": os.environ.get("GATE_JOB_SCALE", "unknown"),
        "sql_mutations": os.environ.get("GATE_JOB_SECURITY", "unknown"),
        "faults": os.environ.get("GATE_JOB_FAULTS", "unknown"),
        "source_mutations": os.environ.get("GATE_JOB_MUTATIONS", "unknown"),
        "notification_mutations": os.environ.get("GATE_JOB_G15_MUTATIONS", "unknown"),
        "notification_scale": os.environ.get("GATE_JOB_G15_SCALE", "unknown"),
        "notification_fanout": os.environ.get("GATE_JOB_G15_FANOUT", "unknown"),
        "reporting_concurrency": os.environ.get("GATE_JOB_G13_CONCURRENCY", "unknown"),
        "crash_recovery": os.environ.get("GATE_JOB_G13_G15_CRASHES", "unknown"),
        "dependency_security": os.environ.get("GATE_JOB_DEPENDENCY_SECURITY", "unknown"),
        "combined_regressions": os.environ.get("GATE_JOB_COMBINED_REGRESSIONS", "unknown"),
    }

# References identify executed technical checks, not complete acceptance scenarios.
REFERENCES = {
    "G13": {
        3: ["TwelveShipmentsAndTwoForeignDecoysReconcileExactMembers",
            "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"],
        4: ["TwelveShipmentsAndTwoForeignDecoysReconcileExactMembers",
            "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"],
        5: ["TwelveShipmentsAndTwoForeignDecoysReconcileExactMembers",
            "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"],
        7: ["RealM11OverdueEvaluationProducesDistinctDashboardRisk",
            "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"],
        8: ["ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"],
        6: ["PostgreSqlMicrosecondGraceAndUtcWeekBoundariesAreExact"],
        9: ["SUITE:M11ResidualRuntimeTests",
            "StolenDatabaseSessionIdsCannotForgeAnM10Correction",
            "M10HttpRoutesRunUnderTheRestrictedApiRoleWithManagerAndTenantGuards"],
        11: ["SUITE:M12ResidualBoundaryTests"],
        10: ["ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"],
        12: ["ForgedTokensUnsafeQueriesAndDirectSqlCannotDiscloseReports"],
        13: ["AuditorGrantRevocationDeniesDashboardAndOldReportMembers",
             "SignedAdminAndUngrantedAuditorScopesCannotReadKpiData"],
        14: ["AuditorGrantRevocationDeniesDashboardAndOldReportMembers"],
        15: ["CommittedReportMembersRemainStableAcrossLaterWorkflowChanges",
             "RepeatableReadReportIgnoresUncommittedConcurrentShipmentTransition"],
        16: ["RestrictedRoleReportReconcilesRecordedArrivalAndMemberSnapshots",
             "FrozenKpiMembersPageExactlyOnceAcrossOneHundredRows"],
        17: ["WindowsAreStrictHalfOpenAndBounded", "SUITE:OperationalKpiPolicyTests"],
        18: ["SUITE:MigrationSnapshotTests"],
        19: ["BlockedMemberMaterializationRollsBackAndRecovers",
             "ExpiredReportPruningIsBoundedAndCannotDeleteForeignTenant",
             "FourLiveSnapshotsAreAdmittedFifthDeniedAndExpiredPruningRestoresCapacity",
             "IdleCleanupRemovesAtMostSixteenExpiredSnapshotsPerCallAndRejectsInvalidAuthority",
             "CRASH:G13BeforeReportCommit", "CRASH:G13AfterReportCommit"],
        20: ["Mixed170kShipmentsProduceExactBoundedReport",
             "ExpiredReportPruningIsBoundedAndCannotDeleteForeignTenant",
             "FourAuthorizedPrincipalsBuildIndependentTenantScopedReportsConcurrently",
             "BUDGET:G13"],
        21: ["BROWSER:operational-kpis"],
        22: ["SUITE:IdentityLifecycleTests", "SUITE:ShipmentBoundaryTests",
             "SUITE:M11ResidualRuntimeTests", "SUITE:M12ResidualBoundaryTests",
             "SUITE:AuditHardeningTests"],
        23: ["ForgedTokensUnsafeQueriesAndDirectSqlCannotDiscloseReports",
             "SignedAdminAndUngrantedAuditorScopesCannotReadKpiData",
             "FourLiveSnapshotsAreAdmittedFifthDeniedAndExpiredPruningRestoresCapacity",
             "MUTATION:g13-security-mutations"],
        24: ["MUTATION:g13-mutations", "MUTATION:g13-security-mutations",
             "SCAN:dependencies"],
    },
    "G15": {
        3: ["OnlyImmutableM11OpeningAndClosureActionsGenerateOutboxRows"],
        4: ["SUITE:M11ResidualRuntimeTests"],
        9: ["SUITE:IdentityLifecycleTests"],
        10: ["SUITE:ShipmentBoundaryTests"],
        5: ["M11HistoryProducesScopedDeduplicatedPersonalReceipts",
            "FiveRolesFanOutOnlyEligibleRecipientsAndLateJoinersGetNoBackfill"],
        6: ["M11HistoryProducesScopedDeduplicatedPersonalReceipts",
            "FiveRolesFanOutOnlyEligibleRecipientsAndLateJoinersGetNoBackfill"],
        7: ["ConcurrentDispatchAndMembershipVersionChangesNeverExposeStaleReceipts"],
        8: ["ConcurrentDispatchAndMembershipVersionChangesNeverExposeStaleReceipts",
            "ExistingReceiptsDoNotReappearAfterMembershipRemovalAndRejoin"],
        11: ["M11HistoryProducesScopedDeduplicatedPersonalReceipts"],
        12: ["ConcurrentDispatchAndMembershipVersionChangesNeverExposeStaleReceipts",
             "TwoDispatchersFanOutToTwoHundredFiftyAdditionalMembersAndServeOneHundredReaders"],
        13: ["CommittedPersonalActionCanBeRecoveredAfterResponseIsDiscarded",
             "CRASH:G15BeforeHistoryCommit", "CRASH:G15BeforeClaimCommit",
             "CRASH:G15BeforeFanoutCommit", "CRASH:G15AfterFanoutCommit",
             "CRASH:G15AfterReceiptCommit", "CRASH:G15AfterResponseDiscarded"],
        14: ["FailedOutboxInsertRollsBackNewImmutableHistoryInSameTransaction"],
        15: ["SUITE:MigrationSnapshotTests"],
        16: ["RestrictedSqlLoginCannotEnumerateOutboxOrWriteAnotherRecipient",
             "SignedRecipientContextCannotReadAnotherUserNotifications"],
        17: ["ShortLivedProtectedCursorExpiresAndNewEventsDoNotLeakIntoOldCutoff",
             "MoreThanOneHundredPersonalRowsPageOnceThroughLiveReceiptChange",
             "OneMillionRowsPreserveBoundedScopedKeysetPagination"],
        18: ["M11HistoryProducesScopedDeduplicatedPersonalReceipts",
             "CursorCannotCrossSessionsAndReplayCannotChangeItsOriginalMeaning"],
        19: ["CursorCannotCrossSessionsAndReplayCannotChangeItsOriginalMeaning",
             "CommittedPersonalActionCanBeRecoveredAfterResponseIsDiscarded"],
        20: ["RequiredAuditFailureRollsBackReceiptAndReplay"],
        21: ["FailedFanoutIsRetriedWithoutPartialOrDuplicateRecipients"],
        22: ["BROWSER:notifications"],
        23: ["SUITE:M11ResidualRuntimeTests", "SUITE:M12ResidualBoundaryTests",
             "SUITE:AuditHardeningTests",
             "FailedFanoutIsRetriedWithoutPartialOrDuplicateRecipients"],
        24: ["MUTATION:g15-mutations",
             "DuplicatePersonalReceiptDatabaseConstraintRejectsReplay",
             "SignedRecipientContextCannotReadAnotherUserNotifications",
             "SCAN:dependencies"],
    },
}

def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

def fail(message: str) -> None:
    problems.append(message)

def files(prefix: str) -> list[Path]:
    return list(INPUT.glob(prefix + "/**/*"))

def find_artifact(prefix: str) -> Path | None:
    matches = [path for path in INPUT.iterdir()
               if path.is_dir() and path.name.startswith(prefix + "-")]
    if len(matches) != 1:
        fail(prefix + ": expected one artifact, found " + str(len(matches)))
        return None
    return matches[0]

def verify_manifest(root: Path) -> None:
    for manifest in root.rglob("sha256.txt"):
        for line in manifest.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            try:
                expected, relative = line.split(maxsplit=1)
                relative = relative.removeprefix("*")
                target = (manifest.parent / relative).resolve()
                target.relative_to(manifest.parent.resolve())
            except (ValueError, IndexError):
                fail("Invalid checksum record: " + str(manifest))
                continue
            if not target.is_file() or digest(target) != expected:
                fail("Evidence checksum mismatch: " + str(target.relative_to(INPUT)))

def read_tests() -> tuple[dict[str,str],dict[str,int]]:
    tests: dict[str,str] = {}
    counters = {"Passed": 0, "Failed": 0, "Skipped": 0, "Other": 0}
    paths = sorted(INPUT.rglob("*.trx"))
    if len(paths) < 5:
        fail("Insufficient TRX records: " + str(len(paths)))
    for path in paths:
        try:
            tree = ET.parse(path)
            results = list(tree.getroot().findall(".//{*}UnitTestResult"))
            if not results:
                fail("TRX has no executed test outcomes: " + str(path.relative_to(INPUT)))
            for item in results:
                name = item.get("testName", "")
                status = item.get("outcome", "Other")
                counters[status if status in counters else "Other"] += 1
                if name in tests and tests[name] != status:
                    fail("Conflicting test outcomes: " + name)
                tests[name] = status
        except ET.ParseError:
            fail("Invalid TRX: " + str(path.relative_to(INPUT)))
    return tests, counters

def mutation_result(artifact: Path | None, prefix: str, expected: int) -> bool:
    if artifact is None:
        return False
    p = artifact / "manifest.json"
    if not p.is_file():
        fail(prefix + ": missing mutation manifest")
        return False
    data = json.loads(p.read_text(encoding="utf-8"))
    if data.get("commit", data.get("source_commit")) != SOURCE:
        fail(prefix + ": different source SHA")
        return False
    rows = data.get("records", data.get("results", []))
    if len(rows) != expected + 1:
        fail(prefix + ": missing mutation variants")
        return False
    verdict = [item.get("outcome", item.get("verdict")) for item in rows]
    if verdict != ["PASS"] + ["KILLED"] * expected:
        fail(prefix + ": control or mutation did not pass")
        return False
    return True

def browser_result(root: Path | None) -> dict[str, bool]:
    result = {}
    if root is None:
        return result
    for engine in ("chromium", "firefox", "webkit"):
        for suite, expected in (("notifications", 11), ("operational-kpis", 8)):
            path = root / (engine + "-" + suite + ".json")
            key = engine + ":" + suite
            if not path.is_file():
                fail("Missing browser output: " + key)
                result[key] = False
                continue
            try:
                stats = json.loads(path.read_text(encoding="utf-8"))["stats"]
                ok = (stats.get("expected") == expected
                      and stats.get("unexpected") == 0
                      and stats.get("skipped") == 0)
            except (KeyError, json.JSONDecodeError):
                ok = False
            if not ok:
                fail("Browser suite incomplete: " + key)
            result[key] = ok
    return result

def crash_evidence(root: Path | None) -> dict[str, bool]:
    phases = ("G13BeforeReportCommit", "G15BeforeHistoryCommit",
              "G15BeforeClaimCommit", "G15BeforeFanoutCommit",
              "G15AfterFanoutCommit", "G13AfterReportCommit",
              "G15AfterReceiptCommit", "G15AfterResponseDiscarded")
    recovered = {"G15BeforeClaimCommit", "G15BeforeFanoutCommit",
                 "G15AfterFanoutCommit", "G13AfterReportCommit",
                 "G15AfterReceiptCommit", "G15AfterResponseDiscarded"}
    results = {phase: False for phase in phases}
    if root is None:
        return results
    manifest = root / "manifest.json"
    if not manifest.is_file():
        fail("Crash recovery manifest missing")
        return results
    try:
        data = json.loads(manifest.read_text(encoding="utf-8"))
        if data.get("source_commit") != SOURCE or not data.get("disposable_postgres"):
            fail("Crash recovery source or database identity mismatch")
            return results
        records = data.get("records", [])
        if [v.get("phase") for v in records] != list(phases):
            fail("Crash recovery missing a required SIGKILL phase")
            return results
        for row in records:
            phase = row["phase"]
            marker = root / (phase + ".json")
            log = root / (phase + ".log")
            ok = (row.get("result") == "KILLED_AND_VERIFIED"
                  and row.get("recovery") == ("PASS" if phase in recovered else "NOT_APPLICABLE")
                  and marker.is_file() and log.is_file()
                  and digest(marker) == row.get("marker_sha256")
                  and digest(log) == row.get("log_sha256"))
            if phase in recovered and not (root / (phase + "-recovery.log")).is_file():
                ok = False
            if not ok:
                fail("Missing complete crash proof: " + phase)
            results[phase] = ok
    except (ValueError, TypeError, KeyError) as exc:
        fail("Crash manifest malformed: " + str(exc))
    return results

def reporting_budget(root: Path | None) -> bool:
    if root is None:
        return False
    # The reference and realistic budgets may be produced by separate test runs on
    # separate fresh databases, so every scale*.log of the artifact is read.
    logs = sorted(root.glob("scale*.log"))
    if not logs:
        fail("G13 thirty-sample benchmark log missing")
        return False
    raw = chr(10).join(log.read_text(encoding="utf-8") for log in logs)
    match = re.search(r"G13_BUDGET count=(\d+) p50_ms=(\d+)"
                      r" p95_ms=(\d+) initial_report_bytes=(\d+)"
                      r" final_report_bytes=(\d+)", raw)
    if match is None:
        fail("G13 source-locked numerical envelope missing")
        return False
    count, p50, p95, initial, final = map(int, match.groups())
    ok = count >= 30 and p50 <= 15000 and p95 <= 20000 and final >= initial
    # The same approved budgets must also hold on the realistic skewed tenant.
    realistic = re.search(r"G13_REALISTIC_BUDGET count=(\d+) p50_ms=(\d+)"
                          r" p95_ms=(\d+) max_ms=(\d+) members=(\d+)", raw)
    if realistic is None:
        fail("G13 realistic skewed-tenant budget line missing")
        ok = False
    else:
        r_count, r_p50, r_p95, _r_max, r_members = map(int, realistic.groups())
        if not (r_count >= 30 and r_p50 <= 15000 and r_p95 <= 20000
                and 0 < r_members <= 500000):
            fail("G13 realistic skewed-tenant budget exceeded or incomplete")
            ok = False
    for label in ("small", "medium10k", "skewed170k", "empty", "realistic85k"):
        plan = root / ("explain-api-" + label + ".json")
        receipt = re.search(
            r"G13_RESTRICTED_PLAN sample=" + re.escape(label) +
            r" elapsed_ms=([0-9]+) role=harborops_api bytes=([0-9]+) "
            r"sha256=([0-9A-F]{64})", raw)
        if not plan.is_file() or receipt is None:
            fail("G13 verified application-role plan or source binding missing: " + label)
            ok = False
            continue
        try:
            if plan.stat().st_size != int(receipt.group(2)):
                raise ValueError("Reported plan length differs from archived bytes")
            if digest(plan).upper() != receipt.group(3):
                raise ValueError("Reported plan digest differs from archived bytes")
            rows = json.loads(plan.read_text(encoding="utf-8"))
            if not isinstance(rows, list) or len(rows) != 1 or not isinstance(rows[0].get("Plan"), dict):
                raise ValueError("Invalid EXPLAIN JSON")
            result = rows[0]["Plan"]
            if not isinstance(result.get("Actual Total Time"), (float, int)):
                raise ValueError("EXPLAIN has no measured execution")
            if not isinstance(rows[0].get("Execution Time"), (float, int)):
                raise ValueError("EXPLAIN timing missing")
        except (ValueError, TypeError, AttributeError, KeyError, json.JSONDecodeError) as exc:
            fail("G13 application-role EXPLAIN malformed: " + label + ": " + str(exc))
            ok = False
    if not ok:
        fail("G13 approved KPI budget or required four application-role plans incomplete")
    return ok

def scan_evidence(root: Path | None) -> bool:
    if root is None:
        return False
    summary = root / "summary.json"
    if not summary.is_file():
        fail("Security review summary missing")
        return False
    try:
        contents = json.loads(summary.read_text(encoding="utf-8"))
        result = (contents.get("scan_completed") is True
                  and contents.get("npm_high") == 0
                  and contents.get("npm_critical") == 0
                  and contents.get("dotnet_high_advisories") == 0)
    except (ValueError, TypeError):
        result = False
    if not result:
        fail("Dependency advisory or committed-secret campaign incomplete")
    return result

def policy_approvals() -> dict[str, str]:
    lines = (ROOT / "docs" / "M15_POLICY_RECOMMENDATIONS_20261109.md").read_text(
        encoding="utf-8").splitlines()
    result = {}
    for i in range(1, 15):
        name = f"M15-P{i:02}"
        row = next((line for line in lines if line.startswith("| " + name + " |")), None)
        fields = row.split("|") if row else []
        if len(fields) < 5 or "APPROVED" not in fields[4]:
            fail("Missing source-locked M15 approval: " + name)
            result[name] = "UNVERIFIED"
        else:
            result[name] = ("APPROVED WITH CONDITION" if i == 10 else
                            "PROVISIONAL PERIOD, NO DELETION" if i == 12 else
                            "APPROVED")
    return result

def reference_present(reference: str, tests: dict[str,str],
                      browsers: dict[str,bool], mutations: dict[str,bool],
                      crash: dict[str,bool], budget: bool,
                      scan: bool) -> bool:
    if reference == "SCAN:dependencies":
        return scan
    if reference.startswith("SUITE:"):
        marker = "." + reference.split(":", 1)[1] + "."
        return any(marker in key and status == "Passed"
                   for key, status in tests.items())
    if reference.startswith("CRASH:"):
        return crash.get(reference.split(":", 1)[1], False)
    if reference == "BUDGET:G13":
        return budget
    if reference.startswith("BROWSER:"):
        name = reference.split(":", 1)[1]
        return all(browsers.get(engine + ":" + name) is True
                   for engine in ("chromium", "firefox", "webkit"))
    if reference.startswith("MUTATION:"):
        return mutations.get(reference.split(":", 1)[1], False)
    return any(key.endswith(reference) and status == "Passed"
               for key, status in tests.items())

def build_gate(gate: str, tests: dict[str,str],
               browsers: dict[str,bool], mutations: dict[str,bool],
               crash: dict[str,bool], budget: bool, scan: bool,
               clean: bool) -> list[dict]:
    results = []
    for number in range(1, 26):
        scenario = gate + "-S" + str(number).zfill(2)
        refs = REFERENCES[gate].get(number, [])
        present = [ref for ref in refs if reference_present(
            ref, tests, browsers, mutations, crash, budget, scan)]
        missing = [ref for ref in refs if ref not in present]
        if number == 1:
            status = "BLOCKED"
            note = ("D-005 is approved, but complete fixture and numerical"
                    " resource reconciliation is outstanding" if gate == "G13"
                    else "M15 policies approved; full source conformance remains under review")
        elif number == 25:
            status = "NOT EXECUTED"
            note = "Independent artifact review and separate gate decision required"
        elif number == 2 and clean:
            status = "PASS"
            note = "Source/contract tests and web build passed at the frozen SHA"
        elif present:
            status = "BLOCKED"
            note = "Targeted technical evidence exists; complete scenario still needs proof"
        else:
            status = "NOT EXECUTED"
            note = "No mapped complete scenario evidence in this campaign"
        if missing:
            note += "; referenced checks missing or not passing"
        results.append({
            "scenario": scenario, "status": status,
            "executed_checks": present, "missing_checks": missing,
            "assessment": note,
        })
    return results

def main() -> int:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    if not INPUT.is_dir():
        fail("No downloaded run artifacts")
        return 2
    artifacts = {
        key: find_artifact(key)
        for key in ("combined-source", "m15-database", "m15-browser",
                    "g13-scale", "g13-security-mutations",
                    "g13-faults", "g13-mutations", "g15-mutations",
                    "g15-scale", "g15-fanout", "g13-concurrency",
                    "g13-g15-crashes", "dependency-security",
                    "combined-regressions")
    }
    for name, root in artifacts.items():
        if root is None:
            continue
        marker = root / "source-sha.txt"
        if not marker.is_file() or marker.read_text(encoding="utf-8").strip() != SOURCE:
            fail("Missing or mismatched source marker: " + name)
        verify_manifest(root)
    tests, counters = read_tests()
    crash = crash_evidence(artifacts["g13-g15-crashes"])
    budget = reporting_budget(artifacts["g13-scale"])
    policies = policy_approvals()
    scan = scan_evidence(artifacts["dependency-security"])
    browsers = browser_result(artifacts["m15-browser"])
    mutations = {
        "g13-mutations": mutation_result(artifacts["g13-mutations"], "g13-mutations", 38),
        "g13-security-mutations": mutation_result(
            artifacts["g13-security-mutations"], "g13-security-mutations", 4),
        "g15-mutations": mutation_result(
            artifacts["g15-mutations"], "g15-mutations", 24),
    }
    if counters["Failed"] or counters["Skipped"] or counters["Other"]:
        fail("TRX contains failing, skipped or unexpected test outcomes")
    if not tests:
        fail("No executed test outcomes")
    # A green job cannot substitute for a missing newly required test. Every
    # closure-specific check must be present as a passed result at this SHA.
    required_tests = (
        "StolenDatabaseSessionIdsCannotForgeAnM10Correction",
        "M10HttpRoutesRunUnderTheRestrictedApiRoleWithManagerAndTenantGuards",
        "SignedAdminAndUngrantedAuditorScopesCannotReadKpiData",
        "ExpiredReportPruningIsBoundedAndCannotDeleteForeignTenant",
        "SignedRecipientContextCannotReadAnotherUserNotifications",
        "DuplicatePersonalReceiptDatabaseConstraintRejectsReplay",
        "CancelledDeliveryPreservesIntentAndSubsequentRetryDeliversExactlyOnce",
        "InFlightCancellationRollsBackBlockedFanout",
        "FourLiveSnapshotsAreAdmittedFifthDeniedAndExpiredPruningRestoresCapacity",
        "IdleCleanupRemovesAtMostSixteenExpiredSnapshotsPerCallAndRejectsInvalidAuthority",
        "RealNotificationDatabaseLockReturnsBoundedScopedHttpFailureAndRecovers",
        "SourceHistoryOutboxPrimaryKeyRejectsDuplicateEvent",
        "MaterializedK06IncludesExactlySixtyMinutesButNotOneMicrosecondLater",
        "UnknownEvaluationDoesNotBecomeZeroRisk",
        # Round 1 S25 closure witnesses.
        "RealisticSkewedEightyFiveThousandShipmentTenantCompletesFourSimultaneousBuilds",
        "RealisticSkewed170kShipmentTenantIsRefusedAsOverCapacityWithoutTimeouts",
        "RealisticSkewedTenantMeetsApprovedLatencyAndResourceBudgetsUnderTheRestrictedRole",
        "OneUserIsLimitedToTwoActiveReportsAcrossSessionsInsideTheTenantLimit",
        "IdleCleanupIsOffByDefaultAndRunsOnlyWhenExplicitlyEnabled",
        "ScopedReadersAndQuotaFunctionsRequireSignedReportingAuthority",
        "M15DeliveryAndM13IdleCleanupRequireExplicitOptIn",
        "RecipientCapOfOneThousandQuarantinesWithoutPartialReceiptsAndDeliversAtExactlyTheCap",
        "DispatcherStatementTimeoutIsACountedRetryAndNeverBlocksLaterEvents",
        "AcknowledgingAnAlertNeverChangesM11StateAndAlertVisibilityGrantsNoMutationOrExport",
        "DefaultPageSizeIsTwentyFiveAndCursorsExpireAfterTwentyMinutes",
        "CancelledAndArchivedTargetsKeepThePersonalEventAndGrantNothingNew",
        "MalformedOrVersionlessActionBodiesNeverChangeAReceipt",
        "FailedOutboxIntentDuringARealM11EvaluationRollsBackTheWholeActionAndRetrySucceeds",
        "CompetingDispatchersDeliverSeveralIntentsExactlyOnceWhileReadersRun",
        "StaleExpectedVersionOnAValidTransitionIsRejectedAsAConflict",
    )
    for required in required_tests:
        if not any(name.endswith("." + required) and status == "Passed"
                   for name, status in tests.items()):
            fail("Required closure test not executed successfully: " + required)
    if not all(crash.values()) or not crash:
        fail("Required source-locked process-kill or restart evidence missing")
    if not budget:
        fail("Required restricted-role query plans or KPI limits missing")
    if not scan:
        fail("Required dependency and secret-scan evidence missing")
    if not all(browsers.values()) or len(browsers) != 6:
        fail("Three-engine browser evidence incomplete")
    if not all(mutations.values()):
        fail("Mutation evidence incomplete")
    if LOCAL:
        import subprocess
        if not re.fullmatch(r"[0-9a-f]{40}", SOURCE):
            fail("Local source SHA must be a full 40 character lowercase SHA")
        head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True,
                              stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                              check=False).stdout.strip()
        if head != SOURCE:
            fail("Local campaign SHA differs from the checked out source HEAD")
        dirty = subprocess.run(["git", "status", "--porcelain", "--untracked-files=no"],
                               cwd=ROOT, text=True, stdout=subprocess.PIPE,
                               stderr=subprocess.DEVNULL, check=False).stdout.strip()
        if dirty:
            fail("Local source tree has tracked modifications")
    if set(JOBS.values()) != {"success"}:
        fail("One or more required " + ("local verification jobs" if LOCAL else "GitHub jobs")
             + " did not succeed")
    clean = not problems
    data = {
        "source_sha": SOURCE,
        "evidence_mode": MODE,
        "campaign_id": os.environ.get("HARBOROPS_CAMPAIGN_ID", "") if LOCAL else "",
        "derived_from_github_actions": not LOCAL,
        "workflow_run": RUN,
        "job_results": JOBS,
        "technical_evidence_integrity": "PASS" if clean else "FAIL",
        "technical_evidence_issues": problems,
        "trx_counts": counters,
        "browser_suites": browsers,
        "mutations": mutations,
        "crash_recovery": crash,
        "g13_approved_budget_satisfied": budget,
        "dependency_security_satisfied": scan,
        "policies": policies,
        "g13": build_gate("G13", tests, browsers, mutations, crash, budget, scan, clean),
        "g15": build_gate("G15", tests, browsers, mutations, crash, budget, scan, clean),
        "milestone_acceptance": "PENDING",
        "merge_authorization": False,
    }
    (OUTPUT / "index.json").write_text(json.dumps(data, indent=2) + "\n",
                                       encoding="utf-8")
    lines = [
        "# G13/G15 Source-Locked Acceptance Preparation",
        "",
        "Evidence mode: " + MODE + (
            " (local verification, not a GitHub Actions result)" if LOCAL else ""),
        "Run: " + RUN,
        "Source SHA: " + SOURCE,
        "Artifact integrity: " + data["technical_evidence_integrity"],
        "TRX outcomes: " + json.dumps(counters, sort_keys=True),
        "",
        "A PASS records only the exact specified scenario coverage."
        " BLOCKED and NOT EXECUTED are not acceptance.",
        "",
        "| Scenario | Disposition | Technical references |",
        "| --- | --- | --- |",
    ]
    for gate in ("g13", "g15"):
        for row in data[gate]:
            lines.append("| " + row["scenario"] + " | " + row["status"]
                         + " | " + ", ".join(row["executed_checks"]) + " |")
    lines += [
        "", "M15-P01 through P14: approved with P10 conditional and P12 no deletion.",
        "Independent S25 reviews and both milestone decisions: pending.",
        "Historical failures and earlier-source results must be retained.",
        "",
    ]
    (OUTPUT / "matrix.md").write_text("\n".join(lines), encoding="utf-8")
    for path in sorted(OUTPUT.iterdir()):
        if path.name != "sha256.txt" and path.is_file():
            with (OUTPUT / "sha256.txt").open("a", encoding="utf-8") as fh:
                fh.write(digest(path) + "  " + path.name + "\n")
    print(json.dumps({
        "source_sha": SOURCE, "integrity": data["technical_evidence_integrity"],
        "issues": problems, "trx": counters,
        "G13": {v: sum(row["status"] == v for row in data["g13"])
                for v in ("PASS", "BLOCKED", "NOT EXECUTED")},
        "G15": {v: sum(row["status"] == v for row in data["g15"])
                for v in ("PASS", "BLOCKED", "NOT EXECUTED")},
    }, indent=2))
    return 0 if clean else 1

problems: list[str] = []
if __name__ == "__main__":
    sys.exit(main())
