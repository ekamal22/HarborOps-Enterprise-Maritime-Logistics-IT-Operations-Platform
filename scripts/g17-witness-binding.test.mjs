import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {alertCaseBlock, alertCaseBound, ALERT_CASE_REQUIREMENTS, MANIFEST_REJECTION_CASES, manifestRejectionBound} from "./g17-witness-binding.mjs";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const alertTest = fs.readFileSync(path.join(repo, "monitoring/m17/alert-test.yaml"), "utf8");
const coverage = JSON.parse(fs.readFileSync(path.join(repo, "docs/M17_G17_TEST_COVERAGE_20261113.json"), "utf8"));
const yamlWitnesses = coverage.scenarios.flatMap(s => s.assertions).filter(a => a.witness?.path === "monitoring/m17/alert-test.yaml");

test("every promtool witness in the coverage register is bound to a real executable case in the real file", () => {
  assert.ok(yamlWitnesses.length >= 3);
  for (const a of yamlWitnesses) assert.equal(alertCaseBound(alertTest, a.witness.case), true, `${a.id}: ${a.witness.case}`);
  assert.deepEqual(Object.keys(ALERT_CASE_REQUIREMENTS).sort(), yamlWitnesses.map(a => a.witness.case).sort(), "every witnessed case has its own requirement");
});

const sample = `tests:
  # Readiness must not remain critical after the probe recovers.
  - interval: 15s
    input_series:
      - series: 'harborops_db_readiness_ratio'
        values: '0x20 1x40'
    alert_rule_test:
      - eval_time: 2m
        alertname: HarborOpsDatabaseUnavailable
        exp_alerts:
          - exp_labels: {severity: critical}
      - eval_time: 8m
        alertname: HarborOpsDatabaseUnavailable
        exp_alerts: []

  - interval: 15s
    input_series:
      - series: 'other'
        values: '0x1'
    promql_expr_test:
      - expr: other
        eval_time: 1m
        exp_samples: []
`;

test("a complete case binds, and the sample used for the controls is itself valid", () => {
  assert.equal(alertCaseBound(sample, "Readiness must not remain critical"), true);
  assert.ok(alertCaseBlock(sample, "Readiness must not remain critical").includes("exp_alerts: []"));
});

test("each way a case can be hollowed out is rejected on its own", () => {
  const marker = "Readiness must not remain critical";
  const cases = {
    "the marker exists only as text, not in a comment": sample.replace("# Readiness must not remain critical after the probe recovers.", "note: Readiness must not remain critical"),
    "the block under the comment was deleted": sample.replace(/  - interval: 15s\n    input_series:\n      - series: 'harborops_db_readiness_ratio'[\s\S]*?exp_alerts: \[\]\n\n/, ""),
    "the case block was commented out": sample.replace("  - interval: 15s\n    input_series:\n      - series: 'harborops_db_readiness_ratio'", "  # - interval: 15s\n    input_series:\n      - series: 'harborops_db_readiness_ratio'"),
    "the input series were removed": sample.replace(/    input_series:\n      - series: 'harborops_db_readiness_ratio'\n        values: '0x20 1x40'\n/, ""),
    "the executable assertions were removed": sample.replace(/    alert_rule_test:[\s\S]*?exp_alerts: \[\]\n\n/, "\n"),
    "another alert is asserted instead": sample.replaceAll("alertname: HarborOpsDatabaseUnavailable", "alertname: HarborOpsHighApiP95"),
    "the firing expectation was removed": sample.replace(/        exp_alerts:\n          - exp_labels: \{severity: critical\}\n/, "        exp_alerts: []\n"),
    "the cleared expectation was removed": sample.replace("exp_alerts: []\n\n", "exp_alerts:\n          - exp_labels: {severity: critical}\n\n"),
    "something other than comments sits between the comment and the block": sample.replace("  # Readiness must not remain critical after the probe recovers.\n", "  # Readiness must not remain critical after the probe recovers.\n  stray: value\n")
  };
  for (const [label, text] of Object.entries(cases)) {
    assert.notEqual(text, sample, `${label}: the control must change the sample`);
    assert.equal(alertCaseBound(text, marker), false, label);
  }
  assert.equal(alertCaseBound(sample, "A case that does not exist"), false);
});

test("the real file loses its binding if a witnessed case is hollowed out", () => {
  for (const a of yamlWitnesses) {
    const marker = a.witness.case;
    const block = alertCaseBlock(alertTest, marker);
    assert.ok(block && block.length > 100, marker);
    const hollow = alertTest.replace(block, "");
    assert.equal(alertCaseBound(hollow, marker), false, `${a.id}: deleting the block must unbind the case even though other text remains`);
  }
});

test("the manifest rejection witness needs the three executed Node cases that provoke the refusal", () => {
  assert.equal(manifestRejectionBound(new Set(MANIFEST_REJECTION_CASES)), true);
  for (const missing of MANIFEST_REJECTION_CASES) {
    const executed = new Set(MANIFEST_REJECTION_CASES.filter(name => name !== missing));
    assert.equal(manifestRejectionBound(executed), false, missing);
  }
  assert.equal(manifestRejectionBound(new Set()), false);
  const manifestTests = fs.readFileSync(path.join(repo, "scripts/check-g17-manifest.test.mjs"), "utf8");
  for (const name of MANIFEST_REJECTION_CASES) assert.ok(manifestTests.includes(`test("${name}"`), `${name} exists in the Node suite`);
});

test("a line that is not a comment can never name a case, even directly above a real block", () => {
  const text = sample.replace("  # Readiness must not remain critical after the probe recovers.\n", "  note: Readiness must not remain critical after the probe recovers.\n");
  assert.notEqual(text, sample);
  assert.equal(alertCaseBound(text, "Readiness must not remain critical"), false);
});

test("a case with series but no executable assertion is not bound, even for a marker without special requirements", () => {
  const text = "tests:\n  # A hollow case\n  - interval: 15s\n    input_series:\n      - series: 'x'\n        values: '0x1'\n";
  assert.equal(alertCaseBound(text, "A hollow case"), false);
  const withAssertion = text + "    promql_expr_test:\n      - expr: x\n        eval_time: 1m\n        exp_samples: []\n";
  assert.equal(alertCaseBound(withAssertion, "A hollow case"), true);
});

test("the three manifest rejection cases are pinned by name", () => {
  assert.deepEqual([...MANIFEST_REJECTION_CASES], [
    "fake complete claim without required raw result is rejected",
    "fake complete claim with incomplete stage evidence is rejected",
    "altered evidence cannot pass hash verification"
  ]);
});
