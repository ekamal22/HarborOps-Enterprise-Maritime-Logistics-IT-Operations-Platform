#!/usr/bin/env python3
"""Check G15 remediation evidence without granting an independent S25 decision."""
from __future__ import annotations

import hashlib
import json
import os
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
    for line in manifest.read_text().splitlines():
        if not line.strip():
            continue
        digest, relative = line.split(maxsplit=1)
        target = (location / relative.strip().removeprefix("*")).resolve()
        require(target.is_relative_to(location.resolve()) and target.is_file(),
                f"{prefix} has missing or unsafe manifest path {relative}")
        measured = hashlib.sha256(target.read_bytes()).hexdigest()
        require(digest == measured, f"{prefix} has a digest mismatch: {relative}")
        entries += 1
    require(entries > 0, f"{prefix} has an empty manifest")
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
               if not any(name in recorded for recorded in historical)]
    require(not missing, f"Historical migration tests absent from TRX: {missing}")
    database = trx_results(located["m15-database"])
    require(any("PopulatedG13SeventeenMigrationDatabaseAcceptsG15WithoutChangingAcceptedRows"
                in name for name in database),
            "G13-to-G15 populated migration upgrade not witnessed")
    trx_results(located["g15-fanout"])
    trx_results(located["g15-scale"])

    browser = located["m15-browser"]
    for engine in ("chromium", "firefox", "webkit"):
        report = browser / f"{engine}-notifications.json"
        require(report.is_file(), f"No {engine} notification browser report")
        data = json.loads(report.read_text())
        stats = data["stats"]
        require(stats["expected"] == 14 and stats["skipped"] == 0
                and stats["unexpected"] == 0,
                f"{engine} did not execute all 14 notification browser cases: {stats}")
        archive = report.read_text()
        for title in BROWSER_CASES:
            require(title in archive, f"{engine} omitted S22 witness: {title}")

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
    return {
        "source_sha": SHA, "run_id": RUN, "status": "TECHNICAL_WITNESSES_PASS",
        "s25_independent_attestation": "PENDING",
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
