import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import {hasRawPredicate, rawWitnessPasses, RAW_WITNESS_SCRIPTS, RAW_REPORT_FILES}
  from "./g17-raw-witness.mjs";

const coverage = JSON.parse(fs.readFileSync(new URL(
  "../docs/M17_G17_TEST_COVERAGE_20261113.json", import.meta.url), "utf8"));
const clone = value => JSON.parse(JSON.stringify(value));
const check = (name, extra = {}) => ({name, status: "PASS", ...extra});

// Reports shaped exactly like the ones the verification scripts write.
const EXPECTED = {postgres: ["internal"], migrator: ["internal"],
  api: ["internal", "telemetry_ingest"], web: ["internal"],
  otelcol: ["telemetry_ingest", "telemetry_monitor"], tempo: ["telemetry_monitor"],
  prometheus: ["telemetry_monitor"], grafana: ["telemetry_monitor"],
  dashboard_gateway: ["dashboard_access", "telemetry_monitor"]};
const FIXTURES = {
  "scripts/check-g17-export-privacy.mjs": {result: "PASS", traces_inspected: 21,
    spans_inspected: 42, exported_metric_series_inspected: 158,
    snapshots: [{kind: "tempo-trace"}, {kind: "prometheus-series"},
      {kind: "owned-sanitized-service-logs"}],
    injected_controls: {oversized_baggage: true, requests: 320,
      malformed_traceparent_requests: 160, all_business_responses_200: true}},
  "scripts/check-g17-database-fault.mjs": {result: "PASS", database_restored: true,
    checks: [check("real-db-outage-keeps-live-and-errors-classified"),
      check("database-alert-identifies-only-safe-component",
        {data: {alert: {name: "HarborOpsDatabaseUnavailable"}}})]},
  "scripts/check-g17-collector-fault.mjs": {result: "PASS", collector_restored: true,
    checks: [{name: "collector-recovery-automatic", result: "PASS"},
      {name: "collector-fault-business-isolation", result: "PASS"}]},
  "scripts/check-g17-restart.mjs": {result: "PASS", api_restored: true,
    checks: [check("real-client-abort-does-not-commit-business"),
      check("owned-api-shutdown-bounded"),
      check("stale-readiness-is-unknown-not-healthy",
        {detail: {actual_spans_inspected: 12}}),
      check("restart-recovers-health-without-business-duplication")]},
  "scripts/check-g17-cardinality-memory.mjs": {result: "PASS",
    run_kind: "FOUR_TEN_THOUSAND_PATH_STRESS_WAVES", total_paths: 40000,
    count_per_wave: 10000, post_stress_memory_growth_pct: 1.9,
    maximum_allowed_memory_overhead_pct: 15,
    rows: Array.from({length: 4}, () => ({distinct_paths: 10000})),
    pairs: [{pass: true}, {pass: true}]},
  "scripts/check-g17-worker-journey.mjs": {result: "PASS", worker_role: "restricted",
    receipts_accepted: 50, reconciled: 50, duplicate_replay: true,
    matched_safe_trace_pairs: 7, exported_metric_series_checked_for_tenant_privacy: 186},
  "scripts/check-g17-network.mjs": {result: "PASS", expected: EXPECTED,
    observed: EXPECTED, process_states: Object.fromEntries(
      Object.keys(EXPECTED).map(k => [k, {status: "running"}])),
    protected_ports_refused: [4317, 9464, 9090, 3200],
    dashboard_listener: {host_ip: "127.0.0.1", host_port: "3301",
      loopback_http_status: 200, published_by: "dashboard_gateway",
      grafana_publishes_nothing: true, non_loopback_addresses_refused: 1}},
  "scripts/check-g17-alert-matrix.mjs": {result: "PASS", checks: [
    {fault: "real-http-5xx-rate-fires-reference-alert", status: "PASS"}]},
  "scripts/check-g17-worker.mjs": {result: "PASS", checks: [
    check("fault-worker-stop-suppresses-stale-backlog"), check("worker-recovers-after-fault")]},
  "scripts/check-g17-performance.mjs": {result: "PASS", run_kind: "THREE_MATCHED_PAIRS",
    measured_routes: ["baseline", "authorized"], rows: new Array(6).fill({}),
    p95_increase_stddev_pct: 3.1, rss_increase_stddev_pct: 1.2,
    system: {cpus: 12, memory_bytes: 16e9},
    pairs: [1, 2, 3].map(() => ({pass: true, memory_increase_pct: 13.9,
      route_pairs: {baseline: {p95_increase_pct: 6.7}, authorized: {p95_increase_pct: 5.9}}}))},
  "scripts/check-g17-pressure-matrix.mjs": {result: "PASS", cases: [
    {name: "business-under-tenfold-pressure-and-exporter-outage", status: "PASS"},
    {name: "a", status: "PASS"}, {name: "b", status: "PASS"}, {name: "c", status: "PASS"}]},
  "scripts/check-g17-retention-matrix.mjs": {result: "PASS", metrics_retention_hours: 24,
    traces_retention_hours: 24, raw_sources: ["m17-retention-age-results.json",
      "m17-tempo-retention-results.json", "m17-storage-results.json"]},
  "scripts/check-g17-storage.mjs": {result: "PASS",
    database_sha256_before: "a".repeat(64), database_sha256_after: "a".repeat(64),
    checks: [check("business-remains-responsive-during-full-telemetry-store"),
      check("all-application-history-byte-identical")]},
  "scripts/check-g17-regression-matrix.mjs": {result: "PASS", cases: {
    M13: {class: "M13AdmissionControlAcceptanceTests", passed: 3},
    audit: {class: "G15AuditAcceptanceTests", passed: 2}}}
};

const scriptWitnesses = [];
for (const scenario of coverage.scenarios)
  for (const a of scenario.assertions)
    if (a.witness && RAW_WITNESS_SCRIPTS.has(a.witness.path))
      scriptWitnesses.push({id: a.id, ...a.witness});

test("every script-based technical assertion has an explicit raw-case predicate", () => {
  const scripts = coverage.scenarios.flatMap(s => s.assertions)
    .filter(a => a.witness?.path.startsWith("scripts/check-g17-") &&
      !a.witness.path.endsWith(".test.mjs"));
  assert.ok(scripts.length >= 25);
  for (const a of scripts) {
    assert.ok(RAW_REPORT_FILES.has(a.witness.path), a.id + " script has no raw report file");
    assert.ok(hasRawPredicate(a.witness.path, a.witness.case),
      a.id + " has no raw-case predicate for " + a.witness.case);
  }
});

test("a complete real-shaped report passes every script-based assertion", () => {
  for (const w of scriptWitnesses) {
    const report = FIXTURES[w.path];
    assert.ok(report, "No fixture for " + w.path);
    assert.equal(rawWitnessPasses(w.path, w.case, report), true, w.id + " " + w.case);
  }
});

test("a report without the named case or fact never witnesses its assertion", () => {
  const breakers = {
    "scripts/check-g17-export-privacy.mjs": r => { r.injected_controls.requests = 10; r.snapshots = []; },
    "scripts/check-g17-database-fault.mjs": r => { r.checks = []; },
    "scripts/check-g17-collector-fault.mjs": r => { r.checks = []; },
    "scripts/check-g17-restart.mjs": r => { r.checks = r.checks.slice(0, 1); },
    "scripts/check-g17-cardinality-memory.mjs": r => { r.total_paths = 39999; },
    "scripts/check-g17-worker-journey.mjs": r => { r.matched_safe_trace_pairs = 0;
      r.exported_metric_series_checked_for_tenant_privacy = 0; },
    "scripts/check-g17-network.mjs": r => { r.dashboard_listener.host_ip = "0.0.0.0";
      r.protected_ports_refused = [4317]; },
    "scripts/check-g17-alert-matrix.mjs": r => { r.checks = []; },
    "scripts/check-g17-worker.mjs": r => { r.checks = []; },
    "scripts/check-g17-performance.mjs": r => { r.pairs[1].route_pairs.authorized.p95_increase_pct = 11;
      r.rows = []; },
    "scripts/check-g17-pressure-matrix.mjs": r => { r.cases = []; },
    "scripts/check-g17-retention-matrix.mjs": r => { r.raw_sources = []; },
    "scripts/check-g17-storage.mjs": r => { r.database_sha256_after = "b".repeat(64); r.checks = []; },
    "scripts/check-g17-regression-matrix.mjs": r => { r.cases = {}; }
  };
  for (const w of scriptWitnesses) {
    const broken = clone(FIXTURES[w.path]);
    breakers[w.path](broken);
    assert.equal(rawWitnessPasses(w.path, w.case, broken), false, w.id + " accepted a broken report");
  }
});

test("a failed overall result or an unknown case is never accepted", () => {
  for (const w of scriptWitnesses) {
    const failed = clone(FIXTURES[w.path]);
    failed.result = "FAIL";
    assert.equal(rawWitnessPasses(w.path, w.case, failed), false, w.id);
  }
  assert.equal(rawWitnessPasses("scripts/check-g17-network.mjs", "invented-case",
    FIXTURES["scripts/check-g17-network.mjs"]), false);
  assert.equal(rawWitnessPasses("scripts/not-registered.mjs", "x", {result: "PASS"}), false);
  assert.equal(rawWitnessPasses("scripts/check-g17-network.mjs", "dashboard_listener", null), false);
});
