#!/usr/bin/env python3
"""Reconcile each mandatory G13/G15 assertion against same-run technical evidence.

A scenario-level passing test is a candidate, not proof for all of its
individual assertions. Unassigned behavior remains OPEN, never auto-PASS.
"""
from __future__ import annotations

import argparse
import importlib.util
import csv
import json
import os
import re
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent.parent
INVENTORY = ROOT / "docs" / "G13_G15_ASSERTION_INVENTORY_20261109.json"
spec = importlib.util.spec_from_file_location(
    "g13_g15_evidence", ROOT / "scripts" / "verify-g13-g15-evidence.py")
if spec is None or spec.loader is None:
    raise RuntimeError("Source-locked evidence contract not importable")
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)
# Identity and folders come from the evidence contract so hosted and local modes agree.
SHA = gate.SOURCE
OUTPUT = gate.OUTPUT

def validate_inventory() -> tuple[list[dict], list[str]]:
    payload = json.loads(INVENTORY.read_text(encoding="utf-8"))
    rows = payload["scenarios"]
    errors = []
    expected = [f"{g}-S{n:02}" for g in ("G13", "G15")
                for n in range(1, 26)]
    if [x.get("scenario") for x in rows] != expected:
        errors.append("Scenario matrix must contain exactly G13/G15 S01 through S25")
    seen = set()
    for row in rows:
        if not row.get("assertions"):
            errors.append(row["scenario"] + ": empty assertion inventory")
        for item in row["assertions"]:
            marker = item.get("id")
            if marker in seen or not marker or not marker.startswith(row["scenario"] + "-A"):
                errors.append("Duplicate, malformed or missing assertion ID: " + str(marker))
            seen.add(marker)
            if not item.get("requirement") or item.get("kind") not in (
                "automated", "policy", "independent_review"):
                errors.append("Malformed assertion: " + str(marker))
            if not isinstance(item.get("evidence"), list):
                errors.append("Evidence must be an explicit list: " + str(marker))
            if item.get("assessment") not in (
                "FULLY_TESTED", "PARTIALLY_TESTED", "MISSING_TEST", "MANUAL_REVIEW"):
                errors.append("Unclassified assertion: " + str(marker))
            if item.get("kind") != "automated" and item.get("assessment") != "MANUAL_REVIEW":
                errors.append("Nonautomated assertion improperly marked tested: " + str(marker))
            if item.get("kind") == "automated" and item.get("assessment") == "MANUAL_REVIEW":
                errors.append("Automated assertion improperly marked manual: " + str(marker))
    if len(seen) != 263:
        errors.append("Expected 263 separately enumerated requirements, got " + str(len(seen)))
    return rows, errors

def test_match(name: str, tests: dict[str, str]) -> bool:
    return any(
        (key.endswith("." + name) or
         key.endswith("." + name + "()") or
         "." + name + "(" in key)
        and outcome == "Passed"
        for key, outcome in tests.items())

def _artifact(prefix: str) -> Path | None:
    folders = [p for p in gate.INPUT.iterdir()
               if p.is_dir() and p.name.startswith(prefix + "-")] if gate.INPUT.is_dir() else []
    return folders[0] if len(folders) == 1 else None

def step_passed(prefix: str, step: str) -> bool:
    """Read a command exit record or validate the source-locked preflight job.

    The hosted preflight uses multiple set -euo pipefail steps, not a
    commands.tsv ledger. A successful source job, sealed source markers,
    required raw logs and passing TRX results together establish these steps.
    """
    folder = _artifact(prefix)
    table = folder / "commands.tsv" if folder else None
    if table and table.is_file():
        for line in table.read_text(encoding="utf-8").splitlines():
            cells = line.split("\t")
            if len(cells) >= 6 and cells[0] == step:
                return cells[3] == "0"
        return False
    if prefix != "combined-source" or not folder or gate.JOBS.get("source") != "success":
        return False
    for marker in ("source-sha.txt", "final-sha.txt"):
        p = folder / marker
        if not p.is_file() or p.read_text(encoding="utf-8").strip() != SHA:
            return False
    necessary = {
        "build": ("build.log",),
        "unit": ("unit.log", "unit.trx"),
        "contract": ("contract.log", "contract.trx"),
        "web-build": ("npm.log", "web-build.log"),
        "web-test": ("npm.log", "web-test.log"),
        "contracts": ("m11-contract.log", "m15-contract.log", "m13-csp.log", "graph.log"),
        "planning": ("planning.log",),
        "sha": ("source-sha.txt", "final-sha.txt"),
        "csp": ("m13-csp.log",)
    }
    if step not in necessary:
        return False
    return all((folder / name).is_file() and (folder / name).stat().st_size > 0
               for name in necessary[step])


def postgres_resources_present() -> bool:
    """Require real PostgreSQL container samples and WAL/temp measurements."""
    folder = _artifact("g13-scale")
    if not folder or gate.JOBS.get("scale") != "success":
        return False
    record = folder / "pg-resources" / "resource-summary.json"
    samples = folder / "pg-resources" / "database-container-resources.csv"
    log = folder / "scale-realistic.log"
    if not (record.is_file() and samples.is_file() and log.is_file()):
        return False
    try:
        data = json.loads(record.read_text(encoding="utf-8"))
        if (data.get("source_sha") != SHA or data.get("status") != "PASS"
                or data.get("exit_code") != 0 or data.get("sample_errors")
                or data.get("sample_count", 0) < 2):
            return False
        with samples.open(encoding="utf-8", newline="") as stream:
            rows = list(csv.DictReader(stream))
        if len(rows) != data["sample_count"]:
            return False
        if not any(float(r["cpu_percent"].rstrip("%")) > 0 for r in rows):
            return False
        if not all(re.search(r"\d+(?:\.\d+)?(?:KiB|MiB|GiB)",
                             r["memory_usage"]) for r in rows):
            return False
        readings = re.search(
            r"G13_DB_RESOURCES workload=realistic40k "
            r"wal_bytes=(\d+) temp_bytes=(\d+) temp_files=(\d+)",
            log.read_text(encoding="utf-8"))
        return (readings is not None and int(readings.group(1)) > 0
                and int(readings.group(2)) >= 0 and int(readings.group(3)) >= 0)
    except (ValueError, KeyError, TypeError, OSError, json.JSONDecodeError):
        return False


def mutant_killed(prefix: str, name: str) -> bool:
    """One named mutant of a mutation campaign is recorded as KILLED."""
    folder = _artifact(prefix)
    manifest = folder / "manifest.json" if folder else None
    if not manifest or not manifest.is_file():
        return False
    data = json.loads(manifest.read_text(encoding="utf-8"))
    for record in data.get("records", data.get("results", [])):
        label = record.get("name") or record.get("mutation")
        if label == name:
            killed = (record.get("outcome") or record.get("verdict")) == "KILLED"
            if name == "member-sql-tenant-filter-silently-excludes-draft":
                return killed and record.get("kill_basis") == "named_behavior_assertion"
            return killed
    return False

def browser_case_passed(suite: str, title: str) -> bool:
    """One named Playwright case passed on Chromium, Firefox and WebKit."""
    folder = _artifact("m15-browser")
    if not folder:
        return False
    for engine in ("chromium", "firefox", "webkit"):
        report = folder / f"{engine}-{suite}.json"
        if not report.is_file():
            return False
        found = False
        stack = list(json.loads(report.read_text(encoding="utf-8")).get("suites", []))
        while stack:
            node = stack.pop()
            stack.extend(node.get("suites", []))
            for spec in node.get("specs", []):
                if spec.get("title") == title:
                    if not spec.get("ok"):
                        return False
                    found = True
        if not found:
            return False
    return True

def gather() -> tuple[dict, bool]:
    rows, issues = validate_inventory()
    data = json.loads((OUTPUT / "index.json").read_text(encoding="utf-8"))
    tests, counts = gate.read_tests()
    issues.extend(gate.problems)
    if not SHA or SHA != data.get("source_sha"):
        issues.append("Evidence ledger source differs from the executing commit")
    if data.get("technical_evidence_integrity") != "PASS":
        issues.append("Source-locked technical integrity is not PASS")
    if counts["Failed"] or counts["Skipped"] or counts["Other"]:
        issues.append("Failed, skipped or unexpected TRX outcomes found")

    scenarios = []
    states = {}
    for row in rows:
        id = row["scenario"]
        area, index = id.split("-S")
        refs = gate.REFERENCES[area].get(int(index), [])
        mapped = (data["g13"] if area == "G13" else data["g15"])[int(index)-1]
        candidate_executed = mapped["executed_checks"]
        if any(ref not in candidate_executed for ref in refs):
            issues.append(id + ": previously required scenario reference missing")

        checked = []
        for item in row["assertions"]:
            kind = item["kind"]
            declared = item["evidence"]
            successful, absent = [], []
            if kind == "policy":
                verdict = "POLICY_REVIEW"
            elif kind == "independent_review":
                verdict = "INDEPENDENT_REVIEW"
            elif not declared:
                verdict = "UNMAPPED"
            else:
                for witness in declared:
                    if not isinstance(witness, dict):
                        absent.append(str(witness))
                        continue
                    name = witness.get("id", "")
                    category = witness.get("kind")
                    if category == "test" and name and test_match(name, tests):
                        successful.append(witness)
                    elif category == "job" and (
                            data["job_results"].get(name) == "success"):
                        successful.append(witness)
                    elif category == "scan" and name == "dependencies" and (
                            data["dependency_security_satisfied"] is True):
                        successful.append(witness)
                    elif category == "scenario_reference" and name in candidate_executed:
                        successful.append(witness)
                    elif category == "browser" and name:
                        if all(data["browser_suites"].get(engine + ":" + name) is True
                               for engine in ("chromium", "firefox", "webkit")):
                            successful.append(witness)
                        else:
                            absent.append(witness)
                    elif category == "crash" and data["crash_recovery"].get(name) is True:
                        successful.append(witness)
                    elif category == "budget" and name == "G13" and (
                            data["g13_approved_budget_satisfied"] is True):
                        successful.append(witness)
                    elif category == "postgres_resources" and name == "G13" and (
                            postgres_resources_present()):
                        successful.append(witness)
                    elif category == "mutation" and (
                            data["mutations"].get(name) is True):
                        successful.append(witness)
                    elif category == "step" and step_passed(witness.get("artifact", ""), name):
                        successful.append(witness)
                    elif category == "mutant" and mutant_killed(witness.get("artifact", ""), name):
                        successful.append(witness)
                    elif category == "browser_case" and browser_case_passed(
                            witness.get("suite", ""), name):
                        successful.append(witness)
                    else:
                        absent.append(witness)
                if absent:
                    verdict = "EVIDENCE_MISSING"
                elif item["assessment"] == "PARTIALLY_TESTED":
                    verdict = "PARTIALLY_TESTED"
                elif item["assessment"] == "MISSING_TEST":
                    verdict = "MISSING_TEST"
                elif item["assessment"] == "FULLY_TESTED":
                    verdict = "TECHNICAL_EVIDENCE_PRESENT"
                else:
                    verdict = "INVENTORY_INVALID"
            states[verdict] = states.get(verdict, 0) + 1
            checked.append({
                "assertion": item["id"], "requirement": item["requirement"],
                "kind": kind, "verdict": verdict,
                "witnesses_found": successful, "witnesses_missing": absent
            })
        scenarios.append({
            "id": id, "scenario_level_evidence": candidate_executed,
            "scenario_level_missing": mapped["missing_checks"],
            "assertions": checked,
        })
    report = {
        "source_sha": SHA,
        "run_id": gate.RUN,
        "evidence_mode": gate.MODE,
        "inventory_source": str(INVENTORY.relative_to(ROOT)),
        "assertion_count": sum(len(x["assertions"]) for x in scenarios),
        "classification": states,
        "integrity_issues": issues,
        "full_automated_assertion_coverage": (
            not issues and states.get("UNMAPPED", 0) == 0
            and states.get("EVIDENCE_MISSING", 0) == 0
            and states.get("PARTIALLY_TESTED", 0) == 0
            and states.get("MISSING_TEST", 0) == 0
            and states.get("INVENTORY_INVALID", 0) == 0),
        "independent_reviews_required": ["G13-S25", "G15-S25"],
        "permanent_storage_policy_pending": True,
        "milestone_acceptance": "PENDING",
        "merge_authorization": False,
        "scenarios": scenarios,
    }
    return report, not issues

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--enforce-complete", action="store_true")
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    report, integrity = gather()
    json_path = OUTPUT / "assertion-audit.json"
    json_path.write_text(json.dumps(report, indent=2) + "\n",
                         encoding="utf-8")
    lines = [
        "# G13/G15 Atomic Acceptance Coverage Audit",
        "",
        "Source SHA: " + report["source_sha"],
        "Evidence mode: " + report["evidence_mode"],
        "Run: " + report["run_id"],
        "Total requirements: " + str(report["assertion_count"]),
        "",
        "An executed scenario-level reference is not by itself proof that "
        "each of its individual requirements has passed.",
        "",
        "| Scenario | Mapped technical | Unmapped | Other decisions |",
        "| --- | ---: | ---: | ---: |",
    ]
    for scenario in report["scenarios"]:
        checked = scenario["assertions"]
        mapped = sum(x["verdict"] == "TECHNICAL_EVIDENCE_PRESENT" for x in checked)
        missing = sum(x["verdict"] in ("UNMAPPED", "EVIDENCE_MISSING",
                                      "PARTIALLY_TESTED", "MISSING_TEST",
                                      "INVENTORY_INVALID")
                      for x in checked)
        other = len(checked) - mapped - missing
        lines.append(f"| {scenario['id']} | {mapped} | {missing} | {other} |")
    lines += [
        "", "Policy decisions and independent S25 reviews are not automated PASS.",
        "Any unmapped, missing or partially verified assertion blocks complete acceptance.",
        "An additional code review is required to confirm each mapped "
        "test actually asserts the specified behavior.",
        "No merger, activation or data deletion is authorized.", "",
    ]
    md = OUTPUT / "assertion-audit.md"
    md.write_text("\n".join(lines), encoding="utf-8")
    with (OUTPUT / "sha256.txt").open("a", encoding="utf-8") as hashes:
        for file in (json_path, md):
            hashes.write(gate.digest(file) + "  " + file.name + "\n")
    print(json.dumps({
        "assertion_count": report["assertion_count"],
        "classification": report["classification"],
        "integrity_issues": report["integrity_issues"],
        "full_automated_assertion_coverage": report["full_automated_assertion_coverage"],
    }, indent=2))
    return 0 if integrity and (
        not args.enforce_complete or report["full_automated_assertion_coverage"]) else 1

if __name__ == "__main__":
    sys.exit(main())
