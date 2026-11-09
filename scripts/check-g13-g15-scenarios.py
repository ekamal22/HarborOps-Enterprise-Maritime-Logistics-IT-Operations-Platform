#!/usr/bin/env python3
"""Check all G13/G15 S01-S24 technical evidence against the exact executed source.

Independent S25 reviews and unresolved policy decisions are not self-approved.
"""
from __future__ import annotations

import importlib.util
import json
import os
from pathlib import Path
import sys
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
LEDGER = ROOT / ".verification" / "gate-ledger"
INPUT = ROOT / ".verification" / "gate-input"
SOURCE = os.environ["GITHUB_SHA"]
spec = importlib.util.spec_from_file_location(
    "source_evidence", ROOT / "scripts" / "verify-g13-g15-evidence.py")
if spec is None or spec.loader is None:
    raise RuntimeError("Source evidence contract not available")
source_evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(source_evidence)
data = json.loads((LEDGER / "index.json").read_text(encoding="utf-8"))
tests, counters = source_evidence.read_tests()
matrix = {r["scenario"]: r for gate in ("g13", "g15") for r in data[gate]}
outcomes: dict[str, dict[str, str]] = {}

# Distinct, executed behavioral assertions required in addition to the
# established acceptance-scenario references. No test-name-only PASS is
# allowed when the referenced TRX, artifact, or source digest is missing.
EXTRA = {
    "G13-S01": ("ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys",),
    "G13-S03": ("ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys",),
    "G13-S04": ("ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys",),
    "G13-S05": ("ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys",),
    "G13-S06": ("PostgreSqlMicrosecondGraceAndUtcWeekBoundariesAreExact",
                "WindowsAreStrictHalfOpenAndBounded"),
    "G13-S07": ("RealM11OverdueEvaluationProducesDistinctDashboardRisk",),
    "G13-S08": ("ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys",),
    "G13-S09": ("StolenDatabaseSessionIdsCannotForgeAnM10Correction",),
    "G13-S10": ("ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys",),
    "G13-S12": ("ForgedTokensUnsafeQueriesAndDirectSqlCannotDiscloseReports",),
    "G13-S13": ("SignedAdminAndUngrantedAuditorScopesCannotReadKpiData",),
    "G13-S14": ("AuditorGrantRevocationDeniesDashboardAndOldReportMembers",),
    "G13-S15": ("RepeatableReadReportIgnoresUncommittedConcurrentShipmentTransition",),
    "G13-S16": ("FrozenKpiMembersPageExactlyOnceAcrossOneHundredRows",),
    "G13-S17": ("WindowsAreStrictHalfOpenAndBounded",),
    "G13-S19": ("BlockedMemberMaterializationRollsBackAndRecovers",
                "ExpiredReportPruningIsBoundedAndCannotDeleteForeignTenant"),
    "G13-S20": ("Mixed170kShipmentsProduceExactBoundedReport",
                "FourAuthorizedPrincipalsBuildIndependentTenantScopedReportsConcurrently"),
    "G13-S23": ("ForgedTokensUnsafeQueriesAndDirectSqlCannotDiscloseReports",),
    "G15-S01": ("FailedOutboxInsertRollsBackNewImmutableHistoryInSameTransaction",),
    "G15-S03": ("OnlyImmutableM11OpeningAndClosureActionsGenerateOutboxRows",),
    "G15-S05": ("FiveRolesFanOutOnlyEligibleRecipientsAndLateJoinersGetNoBackfill",),
    "G15-S06": ("FiveRolesFanOutOnlyEligibleRecipientsAndLateJoinersGetNoBackfill",),
    "G15-S07": ("SignedRecipientContextCannotReadAnotherUserNotifications",),
    "G15-S08": ("ExistingReceiptsDoNotReappearAfterMembershipRemovalAndRejoin",),
    "G15-S11": ("DuplicatePersonalReceiptDatabaseConstraintRejectsReplay",),
    "G15-S12": ("TwoDispatchersFanOutToTwoHundredFiftyAdditionalMembersAndServeOneHundredReaders",),
    "G15-S13": ("CommittedPersonalActionCanBeRecoveredAfterResponseIsDiscarded",),
    "G15-S14": ("FailedOutboxInsertRollsBackNewImmutableHistoryInSameTransaction",),
    "G15-S16": ("RestrictedSqlLoginCannotEnumerateOutboxOrWriteAnotherRecipient",
                "SignedRecipientContextCannotReadAnotherUserNotifications"),
    "G15-S17": ("ShortLivedProtectedCursorExpiresAndNewEventsDoNotLeakIntoOldCutoff",
                "OneMillionRowsPreserveBoundedScopedKeysetPagination"),
    "G15-S18": ("M11HistoryProducesScopedDeduplicatedPersonalReceipts",),
    "G15-S19": ("CursorCannotCrossSessionsAndReplayCannotChangeItsOriginalMeaning",),
    "G15-S20": ("RequiredAuditFailureRollsBackReceiptAndReplay",),
    "G15-S21": ("InFlightCancellationRollsBackBlockedFanout",
                "CancelledDeliveryPreservesIntentAndSubsequentRetryDeliversExactlyOnce"),
    "G15-S23": ("FailedFanoutIsRetriedWithoutPartialOrDuplicateRecipients",),
    "G15-S24": ("DuplicatePersonalReceiptDatabaseConstraintRejectsReplay",),
}
MUTANTS = {
    "g13-mutations": {"tenant-materialization-scope-removed",
                      "coverage-force-complete", "k06-remove-tolerance",
                      "k11-include-resolved-occurrence", "k12-suppress-first-openings"},
    "g13-security-mutations": {"report-select-unrestricted",
                               "administrator-sql-grant-bypass",
                               "auditor-sql-grant-bypass", "member-select-unrestricted"},
    "g15-mutations": {"history-action-filter-removed",
                      "personal-read-policy-bypassed",
                      "membership-version-revocation-ignored",
                      "dispatch-scope-proof-bypassed",
                      "source-intent-failure-swallowed",
                      "receipt-replay-session-unbound", "receipt-cas-bypassed",
                      "receipt-recipient-uniqueness-removed",
                      "signed-recipient-scope-unbound", "receipt-audit-skipped"},
}

def passed(name: str) -> bool:
    return any(k.endswith("." + name) and v == "Passed" for k, v in tests.items())

def killed_mutants(kind: str) -> set[str]:
    folders = [p for p in INPUT.iterdir()
               if p.is_dir() and p.name.startswith(kind + "-")]
    if len(folders) != 1:
        raise AssertionError(kind + ": missing exact run artifact")
    obj = json.loads((folders[0] / "manifest.json").read_text(encoding="utf-8"))
    if obj.get("source_commit", obj.get("commit")) != SOURCE:
        raise AssertionError(kind + ": different source SHA")
    entries = obj.get("records", obj.get("results", []))
    if not entries:
        raise AssertionError(kind + ": empty mutation manifest")
    killed = set()
    for row in entries[1:]:
        if row.get("outcome", row.get("verdict")) != "KILLED":
            raise AssertionError(kind + ": ineffective mutation")
        killed.add(row.get("name", row.get("mutation")))
    return killed

def verify(case: unittest.TestCase, scenario: str) -> None:
    gate, suffix = scenario.split("-S")
    number = int(suffix)
    case.assertEqual(SOURCE, data["source_sha"])
    case.assertEqual("PASS", data["technical_evidence_integrity"])
    case.assertEqual([], data["technical_evidence_issues"])
    case.assertEqual({"success"}, set(data["job_results"].values()))
    case.assertEqual(0, counters["Failed"])
    case.assertEqual(0, counters["Skipped"])
    case.assertEqual(0, counters["Other"])
    entry = matrix[scenario]
    if number == 1:
        if gate == "G13":
            case.assertIs(data["g13_approved_budget_satisfied"], True)
            case.assertTrue(passed("ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"))
        else:
            case.assertEqual(14, len(data["policies"]))
            case.assertNotIn("UNVERIFIED", set(data["policies"].values()))
            case.assertTrue(all(data["crash_recovery"].values()))
    elif number == 2:
        case.assertEqual("PASS", entry["status"])
    else:
        expected = source_evidence.REFERENCES[gate].get(number, [])
        case.assertTrue(expected, scenario + ": no scenario references")
        case.assertEqual([], entry["missing_checks"],
                         scenario + ": required references not executed")
        case.assertEqual(set(expected), set(entry["executed_checks"]))
    for name in EXTRA.get(scenario, ()):
        case.assertTrue(passed(name), scenario + ": failed or missing " + name)
    if scenario in ("G15-S07", "G15-S09", "G13-S14"):
        if scenario == "G15-S07":
            case.assertTrue(passed(
                "PersonalNotificationDetailHidesAnotherRecipientLikeAnUnknownId"))
        if scenario == "G15-S09":
            for mode in ("session_revoked", "idle_expired", "absolute_expired",
                         "security_version", "account_disabled",
                         "organization_suspended", "membership_removed",
                         "role_support", "organization_changed", "logout",
                         "logout_all"):
                case.assertTrue(any(
                    ".RevokedNotificationScopeCannotReadCountDetailOrAcknowledge(" in name
                    and mode in name and result == "Passed"
                    for name, result in tests.items()),
                    "Missing notification revocation scenario: " + mode)
            case.assertTrue(passed(
                "QueuedNotificationAcknowledgmentCannotCommitAfterRevocation"))
        if scenario == "G13-S14":
            for mode in ("session_revoked", "security_version",
                         "organization_suspended", "membership_removed"):
                case.assertTrue(any(
                    ".ExistingKpiReportCannotBeReadAfterAuthorityRevocation(" in name
                    and mode in name and result == "Passed"
                    for name, result in tests.items()),
                    "Missing KPI report revocation scenario: " + mode)
    if scenario in ("G15-S17", "G15-S18", "G15-S23"):
        case.assertTrue(passed(
            "InvalidNotificationQueriesAndMutationsNeverChangeReceipt"))
    if scenario in ("G13-S19", "G15-S13"):
        phases = ("G13BeforeReportCommit", "G13AfterReportCommit") if gate == "G13" else (
            "G15BeforeHistoryCommit", "G15BeforeClaimCommit",
            "G15BeforeFanoutCommit", "G15AfterFanoutCommit",
            "G15AfterReceiptCommit", "G15AfterResponseDiscarded")
        for phase in phases:
            case.assertIs(data["crash_recovery"].get(phase), True)
    if scenario in ("G13-S21", "G15-S22"):
        suite = "operational-kpis" if gate == "G13" else "notifications"
        for engine in ("chromium", "firefox", "webkit"):
            case.assertIs(data["browser_suites"].get(engine + ":" + suite), True)
    if scenario in ("G13-S23", "G13-S24", "G15-S24"):
        case.assertIs(data["dependency_security_satisfied"], True)
    if scenario == "G13-S24":
        for kind in ("g13-mutations", "g13-security-mutations"):
            case.assertTrue(MUTANTS[kind].issubset(killed_mutants(kind)))
    if scenario == "G15-S24":
        case.assertTrue(MUTANTS["g15-mutations"].issubset(
            killed_mutants("g15-mutations")))

class ScenarioEvidence(unittest.TestCase):
    """48 separate source-locked scenario technical evidence contracts."""

def scenario_test(name: str):
    def run(self):
        verify(self, name)
    return run

for gate in ("G13", "G15"):
    for number in range(1, 25):
        item = f"{gate}-S{number:02}"
        setattr(ScenarioEvidence, "test_" + item.replace("-", "_"),
                scenario_test(item))

class Capture(unittest.TextTestResult):
    def addSuccess(self, test):
        super().addSuccess(test)
        outcomes[test._testMethodName] = {"status": "PASS", "detail": ""}
    def addFailure(self, test, err):
        super().addFailure(test, err)
        outcomes[test._testMethodName] = {
            "status": "FAIL", "detail": self._exc_info_to_string(err, test)}
    def addError(self, test, err):
        super().addError(test, err)
        outcomes[test._testMethodName] = {
            "status": "ERROR", "detail": self._exc_info_to_string(err, test)}

class Runner(unittest.TextTestRunner):
    resultclass = Capture

def main() -> int:
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(ScenarioEvidence)
    result = Runner(verbosity=2).run(suite)
    if result.testsRun != 48 or len(outcomes) != 48:
        raise RuntimeError("48 individually executed scenarios are mandatory")
    technical = "PASS" if result.wasSuccessful() else "FAIL"
    by_name = {name.removeprefix("test_").replace("_S", "-S"): row
               for name, row in sorted(outcomes.items())}
    report = {
        "source_sha": SOURCE, "run_id": os.environ["GITHUB_RUN_ID"],
        "technical_evidence": technical, "scenarios": by_name,
        "independent_reviews_pending": ["G13-S25", "G15-S25"],
        "resource_decision_pending": "G13 permanent storage and cleanup limit",
        "milestone_acceptance": "PENDING", "merge_authorization": False,
    }
    (LEDGER / "technical-scenarios.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8")
    xml = ET.Element("testsuite", name="G13G15ScenarioEvidence", tests="48",
                     failures=str(len(result.failures) + len(result.errors)))
    for key, val in by_name.items():
        node = ET.SubElement(xml, "testcase", classname="G13G15ScenarioEvidence",
                             name=key)
        if val["status"] != "PASS":
            failure = ET.SubElement(node, "failure", message=val["status"])
            failure.text = val["detail"]
    ET.ElementTree(xml).write(LEDGER / "technical-scenarios.xml",
                               encoding="utf-8", xml_declaration=True)
    lines = ["# G13/G15 Source-Locked Technical Scenario Evidence", "",
             "Commit: " + SOURCE, "Workflow run: " + os.environ["GITHUB_RUN_ID"],
             "Result: " + technical, "",
             "| Scenario | Technical evidence check |",
             "| --- | --- |"]
    lines.extend("| " + key + " | " + row["status"] + " |"
                 for key, row in by_name.items())
    lines += ["", "These checks validate executed technical evidence, not review signoff.",
              "G13-S25 and G15-S25 require independent source-locked review.",
              "G13 permanent storage/cleanup numeric approval is pending.",
              "No deployment, retention cleanup, production activation or merge approved.", ""]
    (LEDGER / "technical-scenarios.md").write_text(
        "\n".join(lines), encoding="utf-8")
    with (LEDGER / "sha256.txt").open("a", encoding="utf-8") as hashes:
        for name in ("technical-scenarios.json", "technical-scenarios.xml",
                     "technical-scenarios.md"):
            hashes.write(source_evidence.digest(LEDGER / name) + "  " + name + "\n")
    print(json.dumps({"technical_scenarios": len(outcomes),
                      "passed": sum(r["status"] == "PASS" for r in outcomes.values()),
                      "failed": sum(r["status"] != "PASS" for r in outcomes.values()),
                      "independent_reviews_pending": ["G13-S25", "G15-S25"]}, indent=2))
    return 0 if result.wasSuccessful() else 1

if __name__ == "__main__":
    sys.exit(main())
