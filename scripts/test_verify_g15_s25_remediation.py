#!/usr/bin/env python3
"""Local contract tests for the G15 evidence reconciliation guards."""
from __future__ import annotations

import hashlib
import importlib.util
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


VERIFIER_FILE = Path(__file__).with_name("verify-g15-s25-remediation.py")
spec = importlib.util.spec_from_file_location("g15_verifier", VERIFIER_FILE)
assert spec is not None and spec.loader is not None
verifier = importlib.util.module_from_spec(spec)
# Standalone local tests must not depend on the GitHub Actions environment.
os.environ.setdefault("GITHUB_SHA", "a" * 40)
os.environ.setdefault("GITHUB_RUN_ID", "777")
spec.loader.exec_module(verifier)


class G15EvidenceGuardTests(unittest.TestCase):
    def test_manifest_checks_every_file_and_digest(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            folder = root / "g15-test-777"
            folder.mkdir()
            (folder / "source-sha.txt").write_text("a" * 40 + "\n")
            (folder / "case.log").write_text("PASS\n")
            def seal():
                paths = ("source-sha.txt", "case.log")
                (folder / "sha256.txt").write_text("".join(
                    hashlib.sha256((folder / p).read_bytes()).hexdigest()
                    + "  ./" + p + "\n" for p in paths))
            seal()
            with patch.object(verifier, "ROOT", root), \
                 patch.object(verifier, "RUN", "777"), \
                 patch.object(verifier, "SHA", "a" * 40):
                self.assertEqual(verifier.artifact("g15-test"), folder)
                (folder / "case.log").write_text("ALTERED\n")
                with self.assertRaises(AssertionError):
                    verifier.artifact("g15-test")
                (folder / "case.log").write_text("PASS\n")
                (folder / "unsealed.log").write_text("not listed\n")
                with self.assertRaises(AssertionError):
                    verifier.artifact("g15-test")
                (folder / "unsealed.log").unlink()
                seal()
                with patch.object(verifier, "SHA", "b" * 40):
                    with self.assertRaises(AssertionError):
                        verifier.artifact("g15-test")
                digest_entry = (folder / "sha256.txt").read_text().splitlines()[0]
                with (folder / "sha256.txt").open("a") as manifest:
                    manifest.write(digest_entry + "\n")
                with self.assertRaises(AssertionError):
                    verifier.artifact("g15-test")

    def test_browser_case_failure_retry_and_omission_are_rejected(self):
        def sample():
            return {"suites": [{"specs": [
                {"title": f"Case {number}",
                 "tests": [{"expectedStatus": "passed", "status": "expected",
                            "results": [{"status": "passed", "retry": 0}]}]}
                for number in range(14)]}]}
        clean = sample()
        self.assertEqual(len(verifier.playwright_titles(clean)), 14)
        failed = sample()
        failed["suites"][0]["specs"][3]["tests"][0]["results"][0]["status"] = "failed"
        with self.assertRaises(AssertionError):
            verifier.playwright_titles(failed)
        retried = sample()
        retried["suites"][0]["specs"][3]["tests"][0]["results"].append({"status": "passed"})
        with self.assertRaises(AssertionError):
            verifier.playwright_titles(retried)
        omitted = sample()
        omitted["suites"][0]["specs"].pop()
        with self.assertRaises(AssertionError):
            verifier.playwright_titles(omitted)
        retry_only = sample()
        retry_only["suites"][0]["specs"][3]["tests"][0]["results"][0]["retry"] = 1
        with self.assertRaises(AssertionError):
            verifier.playwright_titles(retry_only)
        expected_failure = sample()
        expected_failure["suites"][0]["specs"][3]["tests"][0]["expectedStatus"] = "failed"
        with self.assertRaises(AssertionError):
            verifier.playwright_titles(expected_failure)

    def test_parameterized_method_names_are_matched_without_prefix_collisions(self):
        method = "RevokedNotificationScopeCannotReadCountDetailOrAcknowledge"
        self.assertTrue(verifier.trx_method_matches(
            "HarborOps.IntegrationTests.G13G15RevocationAcceptanceTests."
            + method + '(revocation: "logout")', method))
        self.assertTrue(verifier.trx_method_matches(
            "HarborOps.UnitTests.NotificationTextTests.ExactMethod", "ExactMethod"))
        self.assertFalse(verifier.trx_method_matches(
            "HarborOps.UnitTests.NotificationTextTests.ExactMethodExtra(value: 1)",
            "ExactMethod"))
        self.assertFalse(verifier.trx_method_matches(
            "HarborOps.UnitTests.NotificationTextTests.AnotherMethod"
            '(expected: "ExactMethod")', "ExactMethod"))

    def test_every_parameterized_variant_is_required(self):
        recorded = {}
        for method, expected in verifier.THEORY_CASES.items():
            for case in expected:
                name = f"HarborOps.Tests.Example.{method}({case})"
                recorded[name] = "Passed"
        verifier.verify_parameterized_witnesses(recorded)
        for method, expected in verifier.THEORY_CASES.items():
            missing = dict(recorded)
            missing.pop(f"HarborOps.Tests.Example.{method}({expected[0]})")
            with self.assertRaises(AssertionError):
                verifier.verify_parameterized_witnesses(missing)
            duplicated = dict(recorded)
            duplicated[f"HarborOps.Tests.Example.{method}(duplicate: true)"] = "Passed"
            with self.assertRaises(AssertionError):
                verifier.verify_parameterized_witnesses(duplicated)

    def test_failed_or_skipped_trx_cannot_be_counted_as_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / "selected.trx"
            for outcome in ("Failed", "Skipped"):
                p.write_text(
                    '<TestRun><Results><UnitTestResult testName="BrokenWitness" '
                    f'outcome="{outcome}" /></Results></TestRun>')
                with self.assertRaises(AssertionError):
                    verifier.trx_results(Path(directory))
            p.write_text(
                '<TestRun><Results><UnitTestResult testName="ValidWitness" '
                'outcome="Passed" /></Results></TestRun>')
            self.assertEqual(verifier.trx_results(Path(directory)),
                             {"ValidWitness": "Passed"})


if __name__ == "__main__":
    unittest.main()
