import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {deriveWitnessChecks} from "./reconcile-g17-assertions.mjs";
import {RAW_REPORT_FILES} from "./g17-raw-witness.mjs";
import {writeReconcileEvidence, SOURCE, PROJECT} from "./g17-reconcile-fixture.mjs";
import {syntheticFirstFailureReport} from "./g17-first-failure-evidence.mjs";

// The first-failure report must be a mandatory input of the technical ledger. These controls write the report
// themselves (not through the registry), so removing the registry entry cannot make them pass.
const REPORT = "m17-first-failure-results.json";

function evidence(t, adjust = () => {}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-ff-ledger-"));
  t.after(() => fs.rmSync(dir, {recursive: true, force: true}));
  const runStatus = writeReconcileEvidence(dir);
  const report = syntheticFirstFailureReport(SOURCE, PROJECT);
  adjust(report, dir);
  if (report !== null) fs.writeFileSync(path.join(dir, REPORT), JSON.stringify(report));
  return {dir, runStatus};
}
const derive = ({dir, runStatus}) => () => deriveWitnessChecks(dir, runStatus, SOURCE);

test("the registry names the first-failure script and its report", () => {
  assert.equal(RAW_REPORT_FILES.get("scripts/check-g17-first-failure.mjs"), REPORT);
});

test("a passing first-failure report for this source and project is accepted", t => {
  assert.doesNotThrow(derive(evidence(t)));
});

test("a failing, foreign or missing first-failure report stops the ledger", t => {
  const cases = [
    ["not PASS", report => { report.result = "FAIL"; }],
    ["another source", report => { report.source_sha = "d".repeat(40); }],
    ["another project", report => { report.project = "ho-verify-m17-someone-else"; }]
  ];
  for (const [label, edit] of cases)
    assert.throws(derive(evidence(t, edit)), /failed or foreign execution report/, label);
  const missing = evidence(t);
  fs.rmSync(path.join(missing.dir, REPORT));
  assert.throws(derive(missing), /ENOENT/);
});

// ---- The two witness bindings are enforced by the ledger itself, not only by their helper functions.
import {checkPerAssertion} from "./reconcile-g17-assertions.mjs";
import {MANIFEST_REJECTION_CASES} from "./g17-witness-binding.mjs";
import {fileURLToPath} from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const realCoverage = JSON.parse(fs.readFileSync(path.join(repoRoot, "docs/M17_G17_TEST_COVERAGE_20261113.json"), "utf8"));
const ledgerFor = (bundle, coverage = realCoverage) => checkPerAssertion(coverage, deriveWitnessChecks(bundle.dir, bundle.runStatus, SOURCE));

test("with every case executed the ledger resolves all 72 technical witnesses", t => {
  const ledger = ledgerFor(evidence(t));
  assert.equal(ledger.pass, true, ledger.errors.join("; "));
  assert.equal(ledger.technical_passes, 72);
});

test("the manifest-rejection witness is unresolved when any of its three Node cases did not execute", t => {
  for (const missing of MANIFEST_REJECTION_CASES) {
    const bundle = evidence(t);
    const file = path.join(bundle.dir, "g17-node-tests.xml");
    const kept = fs.readFileSync(file, "utf8").split("<testcase").filter((part, index) => index === 0 || !part.includes(missing.slice(0, 40)));
    fs.writeFileSync(file, kept.join("<testcase"));
    const ledger = ledgerFor(bundle);
    assert.equal(ledger.pass, false, missing);
    assert.ok(ledger.errors.some(error => error.includes("G17-S23-A03")), `${missing}: ${JSON.stringify(ledger.errors)}`);
  }
});

test("a promtool witness whose text appears only outside a real case comment is unresolved", t => {
  const coverage = JSON.parse(JSON.stringify(realCoverage));
  const assertion = coverage.scenarios.flatMap(s => s.assertions).find(a => a.id === "G17-S17-A02");
  // This sentence exists in the fixture file (inside a case's expected annotations) but is not a case comment.
  assertion.witness.case = "Two consecutive recent PostgreSQL readiness samples failed";
  const ledger = ledgerFor(evidence(t), coverage);
  assert.equal(ledger.pass, false);
  assert.ok(ledger.errors.some(error => error.includes("G17-S17-A02")), JSON.stringify(ledger.errors));
});
