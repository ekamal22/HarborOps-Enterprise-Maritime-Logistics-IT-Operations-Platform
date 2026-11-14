import fs from "node:fs";
import path from "node:path";
import {RAW_REPORT_FILES} from "./g17-raw-witness.mjs";
import {FIXTURES, clone} from "./g17-raw-fixtures.mjs";

// Synthetic source-locked evidence from which the ledger deriver (deriveWitnessChecks) can resolve
// every one of the 72 technical witnesses of the real coverage map. Its test names are generated from
// that map, so a witness that is renamed or removed in the map is renamed or removed here too.
export const SOURCE = "c".repeat(40);
export const PROJECT = "ho-verify-m17-reconcile-test";
const coverage = JSON.parse(fs.readFileSync(new URL(
  "../docs/M17_G17_TEST_COVERAGE_20261113.json", import.meta.url), "utf8"));
export const witnesses = coverage.scenarios.flatMap(s => s.assertions)
  .filter(a => a.witness).map(a => ({id: a.id, ...a.witness}));

const trx = (names) => '<TestRun><ResultSummary><Counters total="' + names.length + '" passed="' +
  names.length + '" failed="0" notExecuted="0" /></ResultSummary>' + names.map((name, i) =>
  '<UnitTestResult testId="t' + i + '" testName="' + name + '" outcome="Passed" />').join("") + "</TestRun>";
const dashboardTitles = [
  "dashboard requires authentication and shows actual monitoring panels",
  "authenticated samples match the disposable ready API and suppress held workers",
  "dashboard is unavailable to an anonymous browser context",
  "alert dashboard links stay local and never embed private diagnostic parameters",
  "critical monitoring panels use live series and no-data views are explicitly unknown",
  "zero and insufficient traffic render unknown rather than a healthy zero",
  "rendered readiness distinguishes missing data from real downtime in three browsers",
  "browser API denial retains server-issued correlation under spoofed context"
];
function playwright(titles, engines) {
  return {suites: [{specs: titles.map(title => ({title, ok: true, tests: engines.map(projectName => ({
    projectName, expectedStatus: "passed", status: "expected", results: [{status: "passed", retry: 0}]}))}))}],
    stats: {expected: titles.length * engines.length, unexpected: 0, skipped: 0, flaky: 0}};
}
export const STAGES = ["promtool-test", "dotnet-build", "unit-tests", "contract-tests", "integration-m17-tests",
  "accepted-milestone-regressions-opted-in", "playwright-dashboard-three-engines", "playwright-cardinality"];

export function writeReconcileEvidence(dir, options = {}) {
  const put = (name, value) => fs.writeFileSync(path.join(dir, name),
    typeof value === "string" ? value : JSON.stringify(value));
  const dotnet = prefix => witnesses.filter(w => w.path.startsWith(prefix))
    .map(w => "Suite." + path.posix.basename(w.path, ".cs") + "." + w.case.split("(")[0]);
  put("g17-contract.trx", trx(dotnet("tests/HarborOps.ContractTests/")));
  put("g17-integration.trx", trx(dotnet("tests/HarborOps.IntegrationTests/")));
  put("g17-unit.trx", trx(["Suite.UnitSuite.One"]));
  const nodeCases = [...new Set(witnesses.filter(w => w.path.endsWith(".test.mjs")).map(w => w.case))];
  put("g17-node-tests.xml", "<testsuites>" + nodeCases.map(name => '<testcase name="' +
    name.replaceAll("&", "&amp;").replaceAll('"', "&quot;").replaceAll("<", "&lt;").replaceAll(">", "&gt;") +
    '" time="0.1" classname="test"/>').join("") + "</testsuites>");
  put("m17-browser-results.json", playwright(dashboardTitles, ["chromium", "firefox", "webkit"]));
  put("m17-cardinality-results.json", playwright(
    ["ten thousand hostile HTTP requests remain within complete exported-series budget"], ["chromium"]));
  for (const [script, file] of RAW_REPORT_FILES)
    put(file, {source_sha: SOURCE, project: PROJECT, ...(FIXTURES[script] ? clone(FIXTURES[script]) : {}), result: "PASS"});
  put("m17-promtool-test.txt", "SUCCESS\n");
  const runStatus = {schema_version: 2, gate: "G17", source_sha: SOURCE, source_verified: true, project: PROJECT,
    run_state: "COMPLETE", stage_results: [...STAGES, ...Array.from({length: 6}, (_, i) => "filler-" + i)]
      .map(stage => ({stage, status: "PASS", exit_code: 0}))};
  put("run-status.json", runStatus);
  return runStatus;
}
