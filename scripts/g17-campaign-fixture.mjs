import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {G17_REQUIRED_METHODS} from "./g17-results-contract.mjs";
import {writeS25Fixture,source,project,put} from "./g17-s25-fixture.mjs";
import {syntheticFirstFailureReport} from "./g17-first-failure-evidence.mjs";

// A complete, internally consistent synthetic G17 campaign evidence directory. It lets the
// final manifest verifier (the last gate of a multi-hour campaign) be exercised in seconds,
// with one fact broken at a time, instead of being discovered only at the end of a real run.
export {source, project};
const coverage = JSON.parse(fs.readFileSync(new URL(
  "../docs/M17_G17_TEST_COVERAGE_20261113.json", import.meta.url), "utf8"));
const inventoryBytes = fs.readFileSync(new URL(
  "../docs/M17_G17_ASSERTION_INVENTORY_20261112.json", import.meta.url));
const wrap = x => ({source_sha: source, project, ...x});
const pass = name => ({name, status: "PASS"});
const trx = names => {
  const results = names.map((name, i) => '<UnitTestResult testId="t' + i + '" testName="N.' + name +
    '" outcome="Passed" />').join("");
  return '<TestRun><ResultSummary><Counters total="' + names.length + '" passed="' + names.length +
    '" failed="0" notExecuted="0" /></ResultSummary>' + results + "</TestRun>";
};
function playwright(mode) {
  const titles = mode === "dashboard" ? [
    "dashboard requires authentication and shows actual monitoring panels",
    "authenticated samples match the disposable ready API and suppress held workers",
    "dashboard is unavailable to an anonymous browser context",
    "alert dashboard links stay local and never embed private diagnostic parameters",
    "critical monitoring panels use live series and no-data views are explicitly unknown",
    "zero and insufficient traffic render unknown rather than a healthy zero",
    "rendered readiness distinguishes missing data from real downtime in three browsers",
    "browser API denial retains server-issued correlation under spoofed context"
  ] : ["ten thousand hostile HTTP requests remain within complete exported-series budget"];
  const engines = mode === "dashboard" ? ["chromium", "firefox", "webkit"] : ["chromium"];
  const specs = titles.map(title => ({title, ok: true, tests: engines.map(projectName => ({
    projectName, expectedStatus: "passed", status: "expected",
    results: [{status: "passed", retry: 0}]}))}));
  return {suites: [{specs}], stats: {expected: titles.length * engines.length,
    unexpected: 0, skipped: 0, flaky: 0}};
}
const walk = (root, prefix = "") => fs.readdirSync(root, {withFileTypes: true}).flatMap(entry => {
  const relative = prefix ? prefix + "/" + entry.name : entry.name;
  if (relative === "manifest.json") return [];
  if (entry.isDirectory()) return walk(path.join(root, entry.name), relative);
  return [relative];
}).sort();

export function writeCampaignFiles(dir) {
  const out = (name, value) => {
    fs.mkdirSync(path.dirname(path.join(dir, name)), {recursive: true});
    fs.writeFileSync(path.join(dir, name), typeof value === "string" ? value : JSON.stringify(value));
  };
  writeS25Fixture(dir);
  out("source-sha.txt", source);
  out("g17-coverage-preflight.json", {passed: true, strict: true, technically_authored: 72,
    assertions: 75, scenarios: 25, errors: []});
  const ids = coverage.scenarios.flatMap(s => s.assertions.map(a => a.id));
  out("g17-technical-assertion-ledger.json", {source_sha: source, project, pass: true,
    acceptance: "PENDING", technical_passes: 72, independent_pending: 3, total_assertions: 75,
    errors: [], records: ids.map(id => id.startsWith("G17-S25")
      ? {id, status: "PENDING_INDEPENDENT_REVIEW", source_artifact: null}
      : {id, status: "TECHNICAL_PASS", source_artifact: "g17-unit.trx"})});
  out("g17-unit.trx", trx(["UnitTests.A", "UnitTests.B"]));
  out("g17-contract.trx", trx(G17_REQUIRED_METHODS.contract.map(m => "ContractTests.Suite." + m)));
  out("g17-integration.trx", trx(G17_REQUIRED_METHODS.integration.map(m => "IntegrationTests.Suite." + m)));
  out("g17-regressions.trx", trx(["IntegrationTests.Suite.Regression"]));
  out("m17-network-results.json", wrap({result: "PASS", observed: {web: ["internal"]}}));
  out("m17-browser-results.json", playwright("dashboard"));
  out("m17-cardinality-results.json", playwright("cardinality"));
  out("browser-artifacts/.last-run.json", {status: "passed", failedTests: []});
  out("cardinality-artifacts/.last-run.json", {status: "passed", failedTests: []});
  out("m17-cardinality-raw.json", {requested_paths: 10000, all_requests_returned_404: true,
    exported_series_across_window: 120, memory_samples: [100, 101, 102]});
  out("m17-cardinality-memory-results.json", wrap({result: "PASS",
    run_kind: "FOUR_TEN_THOUSAND_PATH_STRESS_WAVES", total_paths: 40000,
    rows: [{}, {}, {}, {}], pairs: [{pass: true, relative_memory_peak_increase_pct: 13},
      {pass: true, relative_memory_peak_increase_pct: 14}], post_stress_memory_growth_pct: 2}));
  out("m17-restart-results.json", wrap({result: "PASS", api_restored: true,
    checks: ["a", "b", "c", "d", "e"].map(pass)}));
  out("m17-retention-age-results.json", wrap({result: "PASS", policy_hours: 24, checks: [
    {name: "expired-metric-blocks-only-removed", status: "PASS", old_removed: 1, new_preserved: 1},
    pass("b"), pass("c")]}));
  out("m17-tempo-retention-results.json", wrap({result: "PASS", runtime_trace_ingested: true,
    expired_trace_removed: true, recent_trace_preserved: true, reference_policy_hours: 24,
    checks: ["a", "b", "c", "d"].map(pass)}));
  out("m17-retention-matrix-results.json", wrap({result: "PASS", metrics_retention_hours: 24,
    traces_retention_hours: 24, raw_sources: ["a", "b", "c"]}));
  const groups = ["M9", "M10", "M11", "M13", "M15", "audit", "authorization", "held_feature_defaults"];
  out("g17-regression-matrix-results.json", wrap({result: "PASS", required_groups: 8,
    cases: Object.fromEntries(groups.map(g => [g, {passed: 3}]))}));
  out("m17-performance-raw.json", wrap({run_kind: "THREE_MATCHED_PAIRS", result: "PASS",
    rows: new Array(6).fill({}), pairs: [{memory_increase_pct: 13}, {memory_increase_pct: 14},
      {memory_increase_pct: 12}], system: {cpus: 8}, p95_increase_stddev_pct: 3,
    rss_increase_stddev_pct: 1, gate: {pass: true, pooled_p95_pass: true, memory_pass: true,
      mean_memory_increase_pct: 13, pairs_over_p95_budget: 0},
    pooled: {baseline: {p95_increase_pct: 4, telemetry_on_samples: 3600},
      authorized: {p95_increase_pct: 5, telemetry_on_samples: 3600}}}));
  out("m17-live-latency-alert-results.json", wrap({result: "PASS", checks: [pass("a"), pass("b"), pass("c")],
    request_count: 125, slow_requests: 20}));
  out("m17-alert-matrix-results.json", wrap({result: "PASS", checks: [1, 2, 3, 4, 5, 6].map(i => pass("c" + i))}));
  out("m17-pressure-matrix-results.json", wrap({result: "PASS", cases: [1, 2, 3, 4].map(i => pass("c" + i))}));
  out("m17-first-failure-results.json", syntheticFirstFailureReport(source, project));
  out("m17-database-fault-results.json", wrap({result: "PASS", database_restored: true,
    alerts: [{name: "HarborOpsDatabaseUnavailable"}], checks: ["healthy-database-control",
      "real-db-outage-keeps-live-and-errors-classified", "real-http-5xx-rate-fires-reference-alert",
      "database-alert-identifies-only-safe-component", "page-originated-correlated-diagnostic-journey",
      "recovery-clears-alert-and-preserves-history"].map(pass)}));
  out("m17-collector-fault-results.json", wrap({result: "PASS", collector_restored: true, checks: [
    "baseline-good", "collector-scrape-reports-outage", "collector-fault-business-isolation",
    "collector-recovery-automatic"].map(name => ({name, result: "PASS"}))}));
  const journeyPath = path.join(dir, "m17-worker-journey-results.json");
  const journey = JSON.parse(fs.readFileSync(journeyPath, "utf8"));
  out("m17-worker-journey-results.json", {...journey, worker_role: "restricted",
    receipts_accepted: 50, reconciled: 50, sampled_journeys: 5, top_up_batches: 0, duplicate_replay: true, matched_safe_trace_pairs: 7});
  out("m17-worker-results.json", wrap({result: "PASS", checks: ["a", "b", "c", "d"].map(pass)}));
  const required = [...new Set([...walk(dir), "expected-artifacts.json", "run-status.json"])].sort();
  out("expected-artifacts.json", {schema_version: 1, gate: "G17", source_sha: source, project,
    required_artifacts: required, disposable_stack: true, fault_probe: true,
    worker_fixture: true, require_full_g17: true});
  out("run-status.json", {schema_version: 2, gate: "G17", source_sha: source,
    requested_source_sha: source, source_verified: true, project, run_state: "COMPLETE",
    fault_probe_requested: true, worker_fixture_requested: true, full_campaign_requested: true,
    required_artifacts: required, g17_assertion_review: "NOT_REVIEWED",
    gate_acceptance: "NOT_ACCEPTED",
    stage_results: [{stage: "synthetic", status: "PASS", exit_code: 0}]});
}

export function writeManifest(dir, adjust = value => value) {
  const files = walk(dir).map(relative => {
    const bytes = fs.readFileSync(path.join(dir, relative));
    return {path: relative, bytes: bytes.length,
      sha256: crypto.createHash("sha256").update(bytes).digest("hex")};
  });
  const manifest = adjust({schema_version: 2, gate: "G17", source_sha: source,
    gate_acceptance: "NOT_ACCEPTED", assertion_disposition: "NOT_REVIEWED",
    run_state: "COMPLETE", files, missing_expected_artifacts: [],
    assertion_inventory_sha256: crypto.createHash("sha256").update(inventoryBytes).digest("hex"),
    scenario_count: 25, assertion_count: 75, reviewed_assertion_count: 0});
  fs.writeFileSync(path.join(dir, "manifest.json"), JSON.stringify(manifest));
  return path.join(dir, "manifest.json");
}

export const readJson = (dir, name) => JSON.parse(fs.readFileSync(path.join(dir, name), "utf8"));
export const writeJson = (dir, name, value) =>
  fs.writeFileSync(path.join(dir, name), typeof value === "string" ? value : JSON.stringify(value));
