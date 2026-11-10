#!/usr/bin/env python3
"""Reconcile each mandatory G13/G15 assertion against same-run technical evidence.

A scenario-level passing test is a candidate, not proof for all of its
individual assertions. Unassigned behavior remains OPEN, never auto-PASS.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import os
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
    if len(seen) != 257:
        errors.append("Expected 257 separately enumerated requirements, got " + str(len(seen)))
    return rows, errors

def test_match(name: str, tests: dict[str, str]) -> bool:
    return any(
        (key.endswith("." + name) or
         key.endswith("." + name + "()") or
         "." + name + "(" in key)
        and outcome == "Passed"
        for key, outcome in tests.items())

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
                    elif category == "mutation" and (
                            data["mutations"].get(name) is True):
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
