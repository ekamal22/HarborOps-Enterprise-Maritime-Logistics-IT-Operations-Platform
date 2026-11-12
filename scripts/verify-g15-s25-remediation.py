#!/usr/bin/env python3
"""Check G15 remediation evidence without granting an independent S25 decision."""
from __future__ import annotations

import hashlib
import json
import os
import re
import subprocess
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(".verification/gate-input")
OUT = Path(".verification/g15-s25-ledger")
SHA = os.environ["GITHUB_SHA"]
RUN = os.environ["GITHUB_RUN_ID"]
PREFIXES = (
    "g15-s25-source", "m15-database", "m15-browser", "g15-scale",
    "g15-fanout", "g15-crashes", "g15-mutations",
    "combined-regressions", "dependency-security"
)
MIGRATION_METHODS = (
    "ExistingM7SourceAndCredentialSurviveM8UpgradeAndEmptyRollback",
    "AcceptedM8UpgradeAddsImmutableAuditWithoutChangingPriorRecords",
    "M2SeedAndAuthenticatedSessionSurviveM3UpgradeAndRepeat",
    "RetainedAttemptHistoryOnRedrivenPendingWorkBlocksTheM9Downgrade",
    "PopulatedM8ReceiptSurvivesM9UpgradeAndClaimedWorkPreventsDowngrade",
    "FreshInstallAppliesThirteenMigrationsWithTheNullSafeProjectorScopeFunction",
    "GenuineTwelveMigrationDatabaseKeepsTheOriginalFunctionUntilMigrationThirteenThenIsNullSafeAndIdempotent",
    "PopulatedM8DowngradeIsRejectedAndPreservesImmutableHistory",
)
BROWSER_CASES = (
    "G15 successful browser pagination appends distinct second-page receipts in order",
    "G15 pending notification request displays and clears the loading state",
    "G15 archived historical alert navigates to an authorized real archived shipment as read only",
)
CRASH_PHASES = (
    "G15BeforeHistoryCommit", "G15DuringRealM11HistoryWrite",
    "G15BeforeClaimCommit", "G15DuringRealDispatcherFanout",
    "G15BeforeFanoutCommit", "G15AfterFanoutCommit",
    "G15AfterReceiptCommit", "G15AfterResponseDiscarded"
)

def require(test: bool, message: str) -> None:
    if not test:
        raise AssertionError(message)

def artifact(prefix: str) -> Path:
    location = ROOT / f"{prefix}-{RUN}"
    require(location.is_dir(), f"Missing artifact: {location}")
    recorded = (location / "source-sha.txt").read_text().strip()
    require(recorded == SHA, f"{prefix} was produced by different source {recorded}")
    manifest = location / "sha256.txt"
    require(manifest.is_file(), f"{prefix} lacks a SHA-256 manifest")
    entries = 0
    recorded_files = set()
    for line in manifest.read_text().splitlines():
        if not line.strip():
            continue
        digest, relative = line.split(maxsplit=1)
        member = relative.strip().removeprefix("*").removeprefix("./")
        require(member not in recorded_files, f"{prefix} has duplicate manifest path {member}")
        recorded_files.add(member)
        target = (location / member).resolve()
        require(target.is_relative_to(location.resolve()) and target.is_file(),
                f"{prefix} has missing or unsafe manifest path {relative}")
        measured = hashlib.sha256(target.read_bytes()).hexdigest()
        require(digest == measured, f"{prefix} has a digest mismatch: {relative}")
        entries += 1
    require(entries > 0, f"{prefix} has an empty manifest")
    actual_files = {str(p.relative_to(location)) for p in location.rglob("*")
                    if p.is_file() and p.name != "sha256.txt"}
    require(recorded_files == actual_files,
            f"{prefix} manifest omits files or lists nonexistent files: "
            f"omitted={sorted(actual_files-recorded_files)} extra={sorted(recorded_files-actual_files)}")
    return location

def trx_results(folder: Path) -> dict[str, str]:
    outcomes = {}
    for path in folder.rglob("*.trx"):
        root = ET.parse(path).getroot()
        current = [e for e in root.iter() if e.tag.endswith("UnitTestResult")]
        require(bool(current), f"TRX contains no tests: {path}")
        for entry in current:
            name = entry.attrib.get("testName", "")
            outcome = entry.attrib.get("outcome", "")
            require(outcome == "Passed", f"{path} has {name}: {outcome}")
            outcomes[name] = outcome
    require(outcomes, f"No TRX evidence found under {folder}")
    return outcomes


def playwright_titles(data: dict) -> set[str]:
    titles = []
    def inspect(node: dict) -> None:
        for suite in node.get("suites", []):
            for spec in suite.get("specs", []):
                for case in spec.get("tests", []):
                    outcomes = [r.get("status") for r in case.get("results", [])]
                    require(case.get("expectedStatus") == "passed" and
                            case.get("status") == "expected" and outcomes == ["passed"] and
                            case["results"][0].get("retry", 0) == 0,
                            f"Browser case failed, retried or expected failure: {spec.get('title')}: {outcomes}")
                    titles.append(spec.get("title"))
            inspect(suite)
    inspect(data)
    require(len(titles) == 14 and len(set(titles)) == 14,
            f"Expected 14 distinct executed browser cases, got {titles}")
    return set(titles)


def reconcile_atomic_inventory(located: dict[str, Path], browsers: dict[str, set[str]],
                               mutations: dict, crashes: dict) -> dict:
    inventory = json.loads(Path("docs/G13_G15_ASSERTION_INVENTORY_20261109.json").read_text())
    scenarios = [s for s in inventory["scenarios"]
                 if s.get("scenario", "").startswith("G15-S")]
    expected = {f"G15-S{i:02d}" for i in range(1,26)}
    require({s["scenario"] for s in scenarios} == expected and len(scenarios) == 25,
            "G15 assertion inventory must list precisely S01 through S25")
    clauses = [a for s in scenarios for a in s["assertions"]]
    require(len(clauses) == 119 and len({a["id"] for a in clauses}) == 119,
            "G15 atomic assertion count or identity has changed without review")
    require(sum(a["kind"] == "automated" for a in clauses) == 111,
            "Expected 111 automated G15 assertions")
    require(sum(a["assessment"] == "MANUAL_REVIEW" for a in clauses) == 8,
            "Eight G15 policy and independent clauses must remain manual")
    for a in clauses:
        if a["kind"] == "automated":
            require(a["assessment"] == "FULLY_TESTED" and bool(a.get("evidence")),
                    f"Automated clause not fully mapped: {a['id']}")
        else:
            require(a["assessment"] == "MANUAL_REVIEW",
                    f"Manual clause was automatically approved: {a['id']}")

    recorded = {}
    for prefix in ("g15-s25-source", "m15-database", "combined-regressions",
                   "g15-fanout", "g15-scale", "g15-crashes"):
        recorded.update(trx_results(located[prefix]))
    mutants = {m["name"]:m for m in mutations["records"]}
    crash_phases = {c["phase"] for c in crashes["records"]}
    browser_ids = {e["id"] for a in clauses for e in a.get("evidence",[])
                   if e.get("kind") == "browser_case"}
    require(all(browser_ids <= titles for titles in browsers.values()),
            "One or more mapped browser cases were not executed in every browser")
    require(set(mutants) == {"clean-control"} |
            {p.removesuffix(".log") for p in (
                "history-action-filter-removed.log", "personal-read-policy-bypassed.log",
                "membership-version-revocation-ignored.log", "dispatch-scope-proof-bypassed.log",
                "source-intent-failure-swallowed.log", "receipt-replay-session-unbound.log",
                "receipt-cas-bypassed.log", "receipt-recipient-uniqueness-removed.log",
                "source-history-outbox-uniqueness-removed.log", "signed-recipient-scope-unbound.log",
                "receipt-audit-failure-swallowed.log", "dispatcher-timeout-not-trapped.log",
                "recipient-limit-removed.log", "cursor-session-binding-removed.log",
                "page-limit-upper-bound-removed.log", "cursor-lifetime-extended.log",
                "receipt-audit-skipped.log", "outbox-claim-skip-locked-removed.log",
                "unread-capped-flag-never-set.log", "unread-cap-probe-row-removed.log",
                "unread-cap-raised-to-one-hundred-thousand.log",
                "dispatcher-event-time-limit-not-applied.log",
                "delivery-service-registered-by-default.log",
                "m15-downgrade-guard-turned-into-a-notice.log",
                "dispatcher-poll-stops-at-first-failed-intent.log")},
            "Mutation campaign identity changed")
    mutation_dir = located["g15-mutations"]
    for name, m in mutants.items():
        log = (mutation_dir / (name + ".log")).read_text(errors="replace")
        if name == "clean-control":
            require(m["outcome"] == "PASS" and m["exit_code"] == 0,
                    "G15 original-source clean control failed")
            continue
        target = m.get("target")
        require(m["outcome"] == "KILLED" and m["exit_code"] != 0 and target,
                f"Mutant not killed by an identified test: {name}")
        require(re.search(r"Failed\s+HarborOps\.IntegrationTests\.[^\n]*\."
                          + re.escape(target) + r"\s",log) is not None
                and "Test Run Failed." in log and "error CS" not in log,
                f"Mutant {name} lacks a behavior-linked failing test")
        if name == "outbox-claim-skip-locked-removed":
            require("G15_SKIP_LOCKED_PROGRESS_FAILURE" in log,
                    "Missing specific competing-dispatcher progress failure")
        if name == "dispatcher-event-time-limit-not-applied":
            require("Batch not bounded" in log,
                    "Missing dispatcher per-intent deadline failure")
    historical = json.loads(Path("docs/G15_S25_HISTORICAL_EQUIVALENCE_20261112.json").read_text())
    require(historical["current_status"] == "INDEPENDENT_EQUIVALENCE_REVIEW_PENDING" and
            historical["does_not_substitute_for_new_execution"] is True and
            historical["reviewer_disposition"] == "PENDING",
            "Historical G13 equivalence may not be silently approved")
    prior = historical["historical"]
    require(prior["source_sha"] == "479616a5b7f17c494d897834a66d10b6276bb517"
            and prior["run_id"] == 43139153300
            and prior["artifact_id"] == 13909188656
            and prior["test_outcome"] == "Passed",
            "Historical evidence identity mismatch")
    diff = subprocess.run(["git","diff","--quiet",prior["source_sha"],"HEAD","--",
                           *historical["unchanged_source_paths"]],check=False,
                          stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    require(diff.returncode == 0,
            "G13 source-equivalence paths changed or prior SHA unavailable: "
            + diff.stderr.decode(errors="replace"))
    test_ids = set()
    historical_ids = set()
    for a in clauses:
        if a["kind"] != "automated":
            continue
        for e in a["evidence"]:
            kind, ident = e["kind"], e.get("id", "")
            if kind == "test":
                test_ids.add(ident)
                require(any(name == ident or name.endswith("." + ident)
                            for name in recorded),
                        f"Missing same-SHA raw TRX witness {a['id']}: {ident}")
            elif kind == "historical_test":
                historical_ids.add(ident)
                require(a["id"] == "G15-S23-A03" and
                        ident == historical["test_id"] and
                        e["source_sha"] == prior["source_sha"] and
                        e["run_id"] == prior["run_id"] and
                        e["artifact_id"] == prior["artifact_id"] and
                        e["trx_sha256"] == prior["trx_sha256"] and
                        e["disposition"] == "INDEPENDENT_EQUIVALENCE_REVIEW_PENDING",
                        f"Unreviewed or mismatched historical exception at {a['id']}")
            elif kind == "mutant":
                require(ident in mutants and mutants[ident]["outcome"] == "KILLED",
                        f"Missing tested mutant for {a['id']}: {ident}")
            elif kind == "crash":
                require(ident in crash_phases,
                        f"Missing crash phase for {a['id']}: {ident}")
            elif kind == "browser_case":
                require(all(ident in titles for titles in browsers.values()),
                        f"Missing cross-browser case for {a['id']}: {ident}")
            elif kind == "mutation":
                require(ident == "g15-mutations" and len(mutants) == 26,
                        "Incorrect G15 source mutation campaign reference")
            elif kind == "browser":
                require(ident == "notifications", "Unknown browser evidence")
            elif kind == "job":
                require(ident in ("combined_regressions", "database"),
                        f"Unknown job evidence: {ident}")
            elif kind == "scan":
                require(ident == "dependencies", "Unknown scan evidence")
            elif kind == "step":
                require(ident in ("build","unit","contract","web-build","web-test","sha"),
                        f"Unknown source step {ident}")
            else:
                raise AssertionError(f"Unsupported evidence kind {kind} at {a['id']}")
    require(len(test_ids) == 68 and historical_ids == {
        "ReplayedEquipmentReceiptsAndParkedTransportNeverInventKpiProgress"},
        "Unexpected missing or historical G15 test witness")
    for phase in crashes["records"]:
        marker = located["g15-crashes"] / (phase["phase"] + ".json")
        log = located["g15-crashes"] / (phase["phase"] + ".log")
        require(marker.is_file() and log.is_file() and
                hashlib.sha256(marker.read_bytes()).hexdigest() == phase["marker_sha256"] and
                hashlib.sha256(log.read_bytes()).hexdigest() == phase["log_sha256"] and
                json.loads(marker.read_text())["Phase"] == phase["phase"],
                f"Crash phase raw marker/log digest invalid: {phase['phase']}")
        if phase["recovery"] == "PASS":
            recovery = located["g15-crashes"] / (phase["phase"] + "-recovery.trx")
            expected_recovery = (
                "G15RealM11WriteRecoveredInNewApiProcess"
                if phase["phase"] == "G15DuringRealM11HistoryWrite"
                else "G15AfterReceiptCommitRecoveredInNewApiProcess"
                if phase["phase"] in ("G15AfterReceiptCommit", "G15AfterResponseDiscarded")
                else "G15FanoutRecoveredInNewApiProcess"
            )
            require(recovery.is_file(), f"Recovery TRX absent: {phase['phase']}")
            results = [item.attrib for item in ET.parse(recovery).getroot().iter()
                       if item.tag.endswith("UnitTestResult")]
            require(len(results) == 1 and
                    results[0].get("testName", "").endswith("." + expected_recovery) and
                    results[0].get("outcome") == "Passed",
                    f"Wrong or failed recovery test for {phase['phase']}: {results}")
    summary=json.loads((located["dependency-security"]/"summary.json").read_text())
    require(summary == {"npm_high":0,"npm_critical":0,
                        "dotnet_high_advisories":0,"scan_completed":True},
            "Unresolved security dependency finding")
    return {"g15_assertions":len(clauses), "technical_assertions":111,
            "manual_policy_and_review_assertions":8,
            "distinct_same_sha_test_methods":len(test_ids),
            "historical_g13_witnesses_pending_independent_equivalence":len(historical_ids),
            "validated_mutation_failure_logs":25,
            "validated_crash_marker_and_log_pairs":len(crash_phases)}

def check() -> dict:
    require(len(SHA) == 40 and all(c in "0123456789abcdef" for c in SHA),
            "Expected complete source SHA")
    jobs = {k.removeprefix("GATE_JOB_"): value for k, value in os.environ.items()
            if k.startswith("GATE_JOB_")}
    require(len(jobs) == len(PREFIXES), "Incomplete job-result inventory")
    require(all(value == "success" for value in jobs.values()),
            f"Failed or skipped evidence jobs: {jobs}")
    located = {prefix: artifact(prefix) for prefix in PREFIXES}

    historical = trx_results(located["combined-regressions"])
    missing = [name for name in MIGRATION_METHODS
               if not any(recorded.endswith("." + name) or recorded == name
                          for recorded in historical)]
    require(not missing, f"Historical migration tests absent from TRX: {missing}")
    database = trx_results(located["m15-database"])
    require(any("PopulatedG13SeventeenMigrationDatabaseAcceptsG15WithoutChangingAcceptedRows"
                in name for name in database),
            "G13-to-G15 populated migration upgrade not witnessed")
    trx_results(located["g15-fanout"])
    trx_results(located["g15-scale"])

    browser = located["m15-browser"]
    browser_cases = {}
    for engine in ("chromium", "firefox", "webkit"):
        report = browser / f"{engine}-notifications.json"
        require(report.is_file(), f"No {engine} notification browser report")
        data = json.loads(report.read_text())
        stats = data["stats"]
        require(stats["expected"] == 14 and stats["skipped"] == 0
                and stats["unexpected"] == 0 and stats.get("flaky", 0) == 0
                and not data.get("errors"),
                f"{engine} did not execute all 14 notification browser cases: {stats}")
        browser_cases[engine] = playwright_titles(data)
        for title in BROWSER_CASES:
            require(title in browser_cases[engine],
                    f"{engine} omitted S22 witness: {title}")

    crash = json.loads((located["g15-crashes"] / "manifest.json").read_text())
    require(crash["source_commit"] == SHA, "Crash evidence source mismatch")
    records = crash["records"]
    require({row["phase"] for row in records} == set(CRASH_PHASES),
            "Missing or extra notification crash phases")
    require(all(row["result"] == "KILLED_AND_VERIFIED" for row in records),
            "Crash phase without verified post-kill state")
    for row in records:
        if row["phase"] in ("G15DuringRealM11HistoryWrite",
                             "G15DuringRealDispatcherFanout"):
            require(row["recovery"] == "PASS",
                    f"No passing live-operation recovery for {row['phase']}")

    logs = (located["g15-scale"] / "million-receipts.log").read_text()
    for signal in ("G15_STALE_STATISTICS unread_count=", "G15_STALE_STATISTICS state_unread=",
                   "G15_REVOKED_BACKLOG count_status=", "G15_REVOKED_BACKLOG list_status="):
        require(signal in logs, f"Missing scale witness: {signal}")

    mutations = json.loads((located["g15-mutations"] / "manifest.json").read_text())
    require(mutations["source_commit"] == SHA, "Mutant campaign source mismatch")
    mutants = mutations["records"]
    require(len(mutants) == 26, "Expected one control and 25 effective G15 mutants")
    require(len([row for row in mutants if row["name"] == "clean-control"
                 and row["outcome"] == "PASS"]) == 1,
            "G15 mutation control not green")
    require(len([row for row in mutants if row["outcome"] == "KILLED"]) == 25,
            "One or more G15 mutants were not behaviorally killed")
    complete = reconcile_atomic_inventory(located, browser_cases, mutations, crash)
    return {
        **complete,
        "source_sha": SHA, "run_id": RUN, "status": "TECHNICAL_PACKET_COMPLETE_REVIEW_PENDING",
        "s25_independent_attestation": "PENDING",
        "g13_historical_equivalence_approval": "PENDING",
        "unconditional_g15_acceptance": False,
        "artifact_count": len(located),
        "migration_methods_covered": len(MIGRATION_METHODS),
        "database_test_cases": len(database),
        "historical_test_cases": len(historical),
        "browser_engines": 3, "browser_tests_per_engine": 14,
        "process_crash_phases": len(records),
        "g15_mutants_killed": 25,
        "operational_qualification":
            "Results are from disposable runners, not a production sizing approval",
    }

def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    try:
        report = check()
        (OUT / "review-technical-evidence.json").write_text(
            json.dumps(report, indent=2) + "\n")
        print(json.dumps(report, indent=2))
    except Exception as exc:
        (OUT / "review-technical-evidence.json").write_text(json.dumps({
            "source_sha": SHA, "run_id": RUN,
            "status": "EVIDENCE_NOT_COMPLETE",
            "s25_independent_attestation": "PENDING",
            "error": str(exc)
        }, indent=2) + "\n")
        raise

if __name__ == "__main__":
    main()
