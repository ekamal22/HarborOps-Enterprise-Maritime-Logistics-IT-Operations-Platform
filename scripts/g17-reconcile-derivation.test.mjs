import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {spawnSync} from "node:child_process";
import {fileURLToPath} from "node:url";
import {deriveWitnessChecks, checkPerAssertion} from "./reconcile-g17-assertions.mjs";
import {writeReconcileEvidence, SOURCE, PROJECT, STAGES, witnesses} from "./g17-reconcile-fixture.mjs";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const realCoverage = JSON.parse(fs.readFileSync(path.join(repo, "docs/M17_G17_TEST_COVERAGE_20261113.json"), "utf8"));
const copy = value => JSON.parse(JSON.stringify(value));
const readJson = (dir, name) => JSON.parse(fs.readFileSync(path.join(dir, name), "utf8"));
const writeJson = (dir, name, value) => fs.writeFileSync(path.join(dir, name),
  typeof value === "string" ? value : JSON.stringify(value));

// Builds fresh evidence, applies one change, and reports either the thrown reason or the ledger.
function attempt(t, change = () => {}, mutateCoverage = () => {}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-reconcile-"));
  t.after(() => fs.rmSync(dir, {recursive: true, force: true}));
  const runStatus = writeReconcileEvidence(dir);
  const patched = change(dir, runStatus) ?? runStatus;
  const coverage = copy(realCoverage);
  mutateCoverage(coverage);
  try {
    const ledger = checkPerAssertion(coverage, deriveWitnessChecks(dir, patched, SOURCE));
    return {ledger, dir};
  } catch (error) {
    return {threw: String(error.message), dir};
  }
}
const stage = (status, name) => status.stage_results.find(x => x.stage === name);

test("the complete synthetic campaign resolves all 72 technical witnesses and leaves 3 pending", t => {
  const {ledger, threw} = attempt(t);
  assert.equal(threw, undefined);
  assert.equal(ledger.pass, true, ledger.errors.join("; "));
  assert.equal(ledger.technical_passes, 72);
  assert.equal(ledger.independent_pending, 3);
  assert.equal(ledger.records.filter(r => r.status === "TECHNICAL_PASS" && r.source_artifact).length, 72);
  // Every artifact named by a record is one of the real evidence file kinds.
  const kinds = new Set(ledger.records.filter(r => r.source_artifact).map(r => r.source_artifact));
  for (const expected of ["g17-contract.trx", "g17-integration.trx", "g17-node-tests.xml", "m17-browser-results.json",
      "m17-cardinality-results.json", "m17-promtool-test.txt", "run-status.json"])
    assert.ok(kinds.has(expected), "No witness resolved from " + expected);
});

const THROWS = [
  ["raw report with another source sha", dir => {const v = readJson(dir, "m17-restart-results.json"); v.source_sha = "d".repeat(40); writeJson(dir, "m17-restart-results.json", v);}, /failed or foreign execution report/],
  ["raw report from another project", dir => {const v = readJson(dir, "m17-network-results.json"); v.project = "ho-verify-m17-other"; writeJson(dir, "m17-network-results.json", v);}, /failed or foreign execution report/],
  ["raw report with result FAIL", dir => {const v = readJson(dir, "m17-storage-results.json"); v.result = "FAIL"; writeJson(dir, "m17-storage-results.json", v);}, /failed or foreign execution report/],
  ["raw report without a source sha", dir => {const v = readJson(dir, "m17-worker-results.json"); delete v.source_sha; writeJson(dir, "m17-worker-results.json", v);}, /failed or foreign execution report/],
  ["raw report file missing", dir => fs.unlinkSync(path.join(dir, "m17-pressure-matrix-results.json")), /ENOENT/],
  ["promtool stage failed", (dir, rs) => {stage(rs, "promtool-test").status = "FAIL";}, /No passing Prometheus rule fixture stage/],
  ["promtool stage absent", (dir, rs) => ({...rs, stage_results: rs.stage_results.filter(x => x.stage !== "promtool-test")}), /No passing Prometheus rule fixture stage/],
  ["promtool output without SUCCESS", dir => fs.writeFileSync(path.join(dir, "m17-promtool-test.txt"), "FAILED\n"), /did not report SUCCESS/],
  ["run state FAILED", (dir, rs) => {rs.run_state = "FAILED";}, /stages did not succeed/],
  ["only ten stages recorded", (dir, rs) => ({...rs, stage_results: rs.stage_results.slice(0, 10)}), /stages did not succeed/],
  ["a stage failed", (dir, rs) => {stage(rs, "filler-1").status = "FAIL";}, /stages did not succeed/],
  ["a stage with a non-zero exit code", (dir, rs) => {stage(rs, "filler-2").exit_code = 1;}, /stages did not succeed/],
  ...["dotnet-build", "unit-tests", "contract-tests", "integration-m17-tests", "accepted-milestone-regressions-opted-in",
    "playwright-dashboard-three-engines", "playwright-cardinality"].map(name =>
    ["required stage " + name + " missing", (dir, rs) => ({...rs, stage_results: rs.stage_results.filter(x => x.stage !== name)}),
      /Build, \.NET regression or targeted browser stage missing/]),
  ["unit TRX with a failure", dir => fs.writeFileSync(path.join(dir, "g17-unit.trx"),
    '<TestRun><ResultSummary><Counters total="1" passed="0" failed="1" notExecuted="0" /></ResultSummary><UnitTestResult testId="a" testName="N.x" outcome="Failed" /></TestRun>'), /TRX/],
  ["node JUnit with a skipped case", dir => {const x = fs.readFileSync(path.join(dir, "g17-node-tests.xml"), "utf8");
    fs.writeFileSync(path.join(dir, "g17-node-tests.xml"), x.replace("</testsuites>", "<testcase name=\"s\" classname=\"test\"><skipped/></testcase></testsuites>"));}, /failed or skipped test/],
  ["node JUnit with a duplicate case", dir => {const x = fs.readFileSync(path.join(dir, "g17-node-tests.xml"), "utf8");
    const first = x.match(/<testcase [^>]*\/>/)[0]; fs.writeFileSync(path.join(dir, "g17-node-tests.xml"), x.replace("</testsuites>", first + "</testsuites>"));}, /no unique executed cases/],
  ["dashboard browser case skipped", dir => {const v = readJson(dir, "m17-browser-results.json"); v.stats.skipped = 1; writeJson(dir, "m17-browser-results.json", v);}, /skipped, flaky or failing/],
  ["dashboard run without WebKit", dir => {const v = readJson(dir, "m17-browser-results.json");
    for (const spec of v.suites[0].specs) spec.tests = spec.tests.filter(x => x.projectName !== "webkit");
    v.stats.expected = 16; writeJson(dir, "m17-browser-results.json", v);}, /Missing, unexpected or duplicated browser cases/]
];
for (const [label, change, pattern] of THROWS) {
  test("derivation refuses: " + label, t => {
    const outcome = attempt(t, change);
    assert.ok(outcome.threw, "The derivation accepted: " + label);
    assert.match(outcome.threw, pattern);
  });
}

const forId = (ledger, id) => ledger.records.find(r => r.id === id);
const LEDGER_FAILS = [
  ["a contract method missing from the contract TRX", dir => {
    const w = witnesses.find(x => x.path.startsWith("tests/HarborOps.ContractTests/"));
    const file = path.join(dir, "g17-contract.trx"); const x = fs.readFileSync(file, "utf8");
    const keep = x.split("<UnitTestResult").filter(part => !part.includes("." + w.case.split("(")[0] + '"'));
    const body = keep.slice(1).map((part, i) => "<UnitTestResult" + part.replace(/testId="[^"]*"/, 'testId="k' + i + '"')).join("");
    fs.writeFileSync(file, '<TestRun><ResultSummary><Counters total="' + keep.slice(1).length + '" passed="' + keep.slice(1).length +
      '" failed="0" notExecuted="0" /></ResultSummary>' + body.replace(/<\/TestRun>.*$/s, "") + "</TestRun>");
    return null;
  }, witnesses.filter(x => x.path.startsWith("tests/HarborOps.ContractTests/") &&
    x.case === witnesses.find(y => y.path.startsWith("tests/HarborOps.ContractTests/")).case).map(x => x.id)],
  ["an integration method reported under another class", dir => {
    const file = path.join(dir, "g17-integration.trx"); const x = fs.readFileSync(file, "utf8");
    fs.writeFileSync(file, x.replaceAll(".M17MetricSchemaTests.", ".OtherClass."));
  }, witnesses.filter(x => x.path.endsWith("/M17MetricSchemaTests.cs")).map(x => x.id)],
  ["an integration method reported only in the contract TRX", dir => {
    const file = path.join(dir, "g17-integration.trx"); const x = fs.readFileSync(file, "utf8");
    const contract = path.join(dir, "g17-contract.trx");
    const moved = x.match(/<UnitTestResult[^>]*M17MetricSchemaTests\.EveryLocalInstrumentHasTheApprovedNameTypeAndUnit"[^>]*\/>/)[0];
    fs.writeFileSync(file, x.replace(moved, '<UnitTestResult testId="zz" testName="N.Suite.M17ReplayRedriveSpanTests.Spare" outcome="Passed" />'));
    fs.writeFileSync(contract, fs.readFileSync(contract, "utf8").replace("</TestRun>", moved.replace("testId=\"", "testId=\"c") + "</TestRun>")
      .replace(/<Counters[^>]*>/, m => m.replace(/total="(\d+)"/, (_, n) => 'total="' + (Number(n) + 1) + '"').replace(/passed="(\d+)"/, (_, n) => 'passed="' + (Number(n) + 1) + '"')));
  }, ["G17-S02-A01"]],
  ["a node case missing from the JUnit file", dir => {
    const w = witnesses.find(x => x.path.endsWith(".test.mjs"));
    const file = path.join(dir, "g17-node-tests.xml"); const x = fs.readFileSync(file, "utf8");
    fs.writeFileSync(file, x.split("<testcase").filter((part, i) => i === 0 || !part.includes(w.case.slice(0, 40))).join("<testcase"));
  }, ["G17-S01-A01"]],
  ["a raw report failing its predicate", dir => {const v = readJson(dir, "m17-restart-results.json"); v.api_restored = false; writeJson(dir, "m17-restart-results.json", v);},
    ["G17-S15-A03"]],
  ["a raw report that no longer names its case", dir => {const v = readJson(dir, "m17-diagnostic-journey-results.json"); v.checks = []; writeJson(dir, "m17-diagnostic-journey-results.json", v);},
    ["G17-S20-A01", "G17-S20-A02", "G17-S20-A03"]]
];
for (const [label, change, ids] of LEDGER_FAILS) {
  test("ledger names the assertions affected by: " + label, t => {
    const {ledger, threw} = attempt(t, change);
    assert.equal(threw, undefined, threw);
    assert.equal(ledger.pass, false, label);
    for (const id of ids) {
      assert.equal(forId(ledger, id).status, "NOT_EXECUTED", id);
      assert.ok(ledger.errors.some(error => error.startsWith(id)), id);
    }
    assert.ok(ledger.technical_passes < 72);
  });
}

const COVERAGE_FAULTS = [
  ["a witness path outside the supported roots", c => {c.scenarios[0].assertions[0].witness.path = "docs/STATUS.md";}, "G17-S01-A01"],
  ["a witness path with parent traversal", c => {c.scenarios[0].assertions[0].witness.path = "scripts/../scripts/g17-source-boundary.test.mjs";}, "G17-S01-A01"],
  ["a witness without a case", c => {c.scenarios[0].assertions[0].witness.case = "";}, "G17-S01-A01"],
  ["a witness without a path", c => {c.scenarios[0].assertions[0].witness.path = "";}, "G17-S01-A01"],
  ["a case that its source does not contain", c => {c.scenarios[0].assertions[0].witness.case = "a case that exists nowhere";}, "G17-S01-A01"],
  ["a .NET witness naming a missing method", c => {const a = c.scenarios[1].assertions[0]; a.witness.case = "MethodThatDoesNotExist";}, "G17-S02-A01"],
];
for (const [label, mutate, id] of COVERAGE_FAULTS) {
  test("an unresolvable witness is reported: " + label, t => {
    const {ledger, threw} = attempt(t, undefined, mutate);
    assert.equal(threw, undefined, threw);
    assert.equal(ledger.pass, false);
    assert.equal(forId(ledger, id).status, "NOT_EXECUTED");
    assert.equal(ledger.technical_passes, 71);
  });
}

test("a witness path that does not exist stops the derivation instead of passing", t => {
  const outcome = attempt(t, undefined, c => {c.scenarios[1].assertions[0].witness.path = "tests/fixtures/readme.md";});
  assert.match(outcome.threw, /ENOENT/);
});

// The command line entry point.
function cli(dir, sha, extra = []) {
  return spawnSync(process.execPath, [path.join(repo, "scripts/reconcile-g17-assertions.mjs"), ...(dir ? [dir] : []), ...(sha ? [sha] : []), ...extra],
    {cwd: repo, encoding: "utf8"});
}
test("the command writes the ledger and reports 72 of 72", t => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-reconcile-cli-"));
  t.after(() => fs.rmSync(dir, {recursive: true, force: true}));
  writeReconcileEvidence(dir);
  const run = cli(dir, SOURCE);
  assert.equal(run.status, 0, run.stderr);
  assert.match(run.stdout, /72\/72/);
  const ledger = readJson(dir, "g17-technical-assertion-ledger.json");
  assert.equal(ledger.source_sha, SOURCE);
  assert.equal(ledger.project, PROJECT);
  assert.equal(ledger.technical_passes, 72);
  assert.equal(ledger.pass, true);
  // The ledger is never overwritten.
  assert.notEqual(cli(dir, SOURCE).status, 0);
});
for (const [label, args, change, pattern] of [
  ["no arguments", [null, null], null, /Usage/],
  ["no source sha", ["dir", null], null, /Usage/],
  ["no evidence directory", [null, SOURCE], null, /Usage/],
  ["a short source sha", ["dir", "abc123"], null, /Usage/],
  ["a source sha that differs from the run ledger", ["dir", "e".repeat(40)], null, /source differs/],
  ["a run ledger that is not source verified", ["dir", SOURCE], (dir, rs) => {rs.source_verified = false; writeJson(dir, "run-status.json", rs);}, /source differs/],
  ["a failing witness", ["dir", SOURCE], dir => {const v = readJson(dir, "m17-restart-results.json"); v.api_restored = false; writeJson(dir, "m17-restart-results.json", v);}, /Missing or skipped technical witnesses/]
]) {
  test("the command fails for " + label, t => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-reconcile-cli-"));
    t.after(() => fs.rmSync(dir, {recursive: true, force: true}));
    const rs = writeReconcileEvidence(dir);
    if (change) change(dir, rs);
    const run = cli(args[0] === "dir" ? dir : null, args[1]);
    assert.notEqual(run.status, 0, label);
    assert.match(run.stderr, pattern);
  });
}
