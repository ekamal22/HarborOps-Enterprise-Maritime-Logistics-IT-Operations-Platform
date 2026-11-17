import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import {hasRawPredicate, rawWitnessPasses, RAW_WITNESS_SCRIPTS, RAW_REPORT_FILES}
  from "./g17-raw-witness.mjs";

const coverage = JSON.parse(fs.readFileSync(new URL(
  "../docs/M17_G17_TEST_COVERAGE_20261113.json", import.meta.url), "utf8"));
import {clone, check, FIXTURES} from "./g17-raw-fixtures.mjs";

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
    "scripts/check-g17-malformed-export.mjs": r => { r.validated_exported_traces = 0; },
    "scripts/check-g17-diagnostic-journey.mjs": r => { r.checks=[]; r.trace_correlation_reconciled=false; },
    "scripts/check-g17-database-fault.mjs": r => { r.checks = []; },
    "scripts/check-g17-collector-fault.mjs": r => { r.checks = []; },
    "scripts/check-g17-restart.mjs": r => { r.checks = r.checks.slice(0, 1); r.positive_worker_lease_witness=false; },
    "scripts/check-g17-cardinality-memory.mjs": r => { r.total_paths = 39999; },
    "scripts/check-g17-worker-journey.mjs": r => { r.matched_safe_trace_pairs = 0;
      r.exported_metric_series_checked_for_tenant_privacy = 0; },
    "scripts/check-g17-network.mjs": r => { r.dashboard_listener.host_ip = "0.0.0.0";
      r.protected_ports_refused = [4317]; },
    "scripts/check-g17-alert-matrix.mjs": r => { r.checks = []; },
    "scripts/check-g17-worker.mjs": r => { r.checks = []; },
    "scripts/check-g17-performance.mjs": r => { r.pooled.authorized.p95_increase_pct = 10.5;
      r.gate.mean_memory_increase_pct = 15.5; r.rows = []; },
    "scripts/check-g17-pressure-matrix.mjs": r => { r.cases = []; },
    "scripts/check-g17-retention-matrix.mjs": r => { r.raw_sources = []; },
    "scripts/check-g17-storage.mjs": r => { r.database_sha256_after = "b".repeat(64); r.checks = [];
      r.positive_history.tables.m15_outbox=0; r.telemetry_degradation.error_observed=false; },
    "scripts/check-g17-regression-matrix.mjs": r => { r.cases = {}; }
  };
  for (const w of scriptWitnesses) {
    const broken = clone(FIXTURES[w.path]);
    breakers[w.path](broken);
    assert.equal(rawWitnessPasses(w.path, w.case, broken), false, w.id + " accepted a broken report");
  }
});

test("the held-worker alert suppression witness cannot pass vacuously", () => {
  const path = "scripts/check-g17-worker.mjs";
  const witnessed = scriptWitnesses.filter(w => w.path === path);
  assert.ok(witnessed.length >= 1);
  const single = {
    "no stale backlog existed": r => { r.checks[0].detail.stale_backlog_seconds_at_start = 12; },
    "backlog vanished during the observation": r => { r.checks[0].detail.stale_backlog_seconds_at_end = 100; },
    "heartbeat was still present": r => { r.checks[0].detail.heartbeat_absent = false; },
    "an alert fired": r => { r.checks[0].detail.alert_firing_count = 1; },
    "backlog never drained": r => { r.checks[1].detail.stale_work_state_after_recovery = "Pending"; },
    "same process instance resumed": r => { r.checks[1].detail.new_process_instance_resumed = false; },
    "no detail at all": r => { delete r.checks[0].detail; }
  };
  for (const [name, mutate] of Object.entries(single)) {
    const broken = clone(FIXTURES[path]);
    mutate(broken);
    const outcomes = witnessed.map(w => rawWitnessPasses(path, w.case, broken));
    assert.ok(outcomes.includes(false), "Mutation not detected by the worker witness: " + name);
  }
});

test("every individual F08 degradation, recovery and row-identity fact is load-bearing", () => {
  const path = "scripts/check-g17-storage.mjs";
  const witnessed = scriptWitnesses.filter(w => w.path === path);
  assert.ok(witnessed.length >= 2);
  const single = {
    "no storage error logged": r => { r.telemetry_degradation.storage_error_logged = false; },
    "readiness expression still current": r => { r.telemetry_degradation.readiness_panel_expression_unknown = false; },
    "samples not stale and prometheus reachable": r => { r.telemetry_degradation.stale_age_seconds = 10; },
    "no healthy control before the fault": r => { r.telemetry_degradation.fresh_before_seconds = 70; },
    "dd write error only": r => { r.telemetry_degradation.error_observed = false; },
    "no recovery recorded": r => { r.telemetry_recovery.recovered = false; },
    "recovery check absent": r => { r.checks = r.checks.filter(c => c.name !== "telemetry-observation-recovers-after-fault"); },
    "no M8 receipt history": r => { r.positive_history.tables.m8_receipts = 0; },
    "fixture row hash changed": r => { r.fixture_rows_after.m15_outbox.sha256 = "d".repeat(64); },
    "fixture row removed": r => { r.fixture_rows_after.m16_audit.rows = 0; },
    "fixture row hash missing": r => { delete r.fixture_rows_before.m8_work_items; delete r.fixture_rows_after.m8_work_items; },
    "whole database hash changed": r => { r.database_sha256_after = "b".repeat(64); }
  };
  for (const [name, mutate] of Object.entries(single)) {
    const broken = clone(FIXTURES[path]);
    mutate(broken);
    const outcomes = witnessed.map(w => rawWitnessPasses(path, w.case, broken));
    assert.ok(outcomes.includes(false), "Mutation not detected by any storage witness: " + name);
  }
});

// Each fact is removed alone: a clause that no mutation can flip is an inert assertion.
test("every individual restart lease and cancellation fact is load-bearing", () => {
  const path = "scripts/check-g17-restart.mjs";
  const witnessed = scriptWitnesses.filter(w => w.path === path);
  assert.ok(witnessed.length >= 3);
  const single = {
    "lease identity not equal across restart": r => { r.lease.identity_equal_before_stop_during_stop_after_restart = false; },
    "lease epoch changed": r => { r.lease.epoch = 2; },
    "lease attempts changed": r => { r.lease.attempts = 2; },
    "forged token not rejected": r => { r.lease.forged_token_rejected = false; },
    "forged epoch not rejected": r => { r.lease.forged_epoch_rejected = false; },
    "original holder did not finish": r => { r.lease.original_holder_finished_once = false; },
    "duplicate finish accepted": r => { r.lease.duplicate_finish_rejected = false; },
    "duplicate effect exists": r => { r.lease.effects_after = 1; },
    "lease token not a uuid": r => { r.lease.token = "not-a-token"; },
    "no lease block at all": r => { delete r.lease; },
    "lease changed during API stop": r => { r.checks[1].detail.lease_unchanged_during_stop = false; },
    "cancellation changed lease": r => { r.checks[0].detail.lease_unchanged_by_cancellation = false; },
    "no request canceled": r => { r.checks[0].detail.canceled = 0; },
    "late canceled span": r => { r.canceled_trace_correlation.all_started_before_shutdown = false; },
    "canceled request exported as error": r => { r.canceled_trace_correlation.none_exported_as_server_error = false; },
    "canceled traces unaccounted": r => { r.canceled_trace_correlation.absent_in_tempo = 1; },
    "no canceled trace correlation": r => { delete r.canceled_trace_correlation; },
    "no request was in flight": r => { r.canceled_trace_correlation.inflight_canceled_requests = 39; },
    "no in-flight trace found": r => { r.canceled_trace_correlation.inflight_found_in_tempo = 0; },
    "in-flight detail missing": r => { r.checks[0].detail.inflight_canceled = 39; }
  };
  const flagged = new Set();
  for (const [name, mutate] of Object.entries(single)) {
    const broken = clone(FIXTURES[path]);
    mutate(broken);
    const outcomes = witnessed.map(w => rawWitnessPasses(path, w.case, broken));
    assert.ok(outcomes.includes(false), "Mutation not detected by any restart witness: " + name);
    flagged.add(name);
  }
  assert.equal(flagged.size, Object.keys(single).length);
});

test("a regression suite with fewer executed than declared tests is never a witness", () => {
  const path = "scripts/check-g17-regression-matrix.mjs";
  for (const [key, name] of [["M13", "M13AdmissionControlAcceptanceTests"],
      ["audit", "G15AuditAcceptanceTests"]]) {
    const trimmed = clone(FIXTURES[path]);
    trimmed.cases[key].executed_distinct_methods = trimmed.cases[key].declared_methods - 1;
    assert.equal(rawWitnessPasses(path, name, trimmed), false, key);
    const unaccounted = clone(FIXTURES[path]);
    delete unaccounted.cases[key].declared_methods;
    assert.equal(rawWitnessPasses(path, name, unaccounted), false, key);
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
  const missingAlertSource=clone(FIXTURES["scripts/check-g17-alert-matrix.mjs"]);
  delete missingAlertSource.checks[0].source_artifact;
  assert.equal(rawWitnessPasses("scripts/check-g17-alert-matrix.mjs",
    "real-http-5xx-rate-fires-reference-alert",missingAlertSource),false);
  const noProbe=clone(FIXTURES["scripts/check-g17-network.mjs"]);
  noProbe.dashboard_listener.non_loopback_addresses_refused=0;
  assert.equal(rawWitnessPasses("scripts/check-g17-network.mjs","dashboard_listener",noProbe),false);
});

// Mutation-driven controls. Each row removes or weakens exactly one fact; the named
// witness case must then fail. Boundary rows pair the last passing value with the first
// failing one, so a >= that degrades to > (or the reverse) is caught.
const SINGLE_FACT = [
  // export privacy
  ["scripts/check-g17-export-privacy.mjs", "baggage:\"secret=\"", "oversized baggage not injected", r => { r.injected_controls.oversized_baggage = false; }],
  ["scripts/check-g17-export-privacy.mjs", "baggage:\"secret=\"", "299 requests", r => { r.injected_controls.requests = 299; }],
  ["scripts/check-g17-export-privacy.mjs", "owned-sanitized-service-logs", "no retained log bytes", r => { r.retained_log_bytes = 0; }],
  ["scripts/check-g17-export-privacy.mjs", "owned-sanitized-service-logs", "no exported series", r => { r.exported_metric_series_inspected = 0; r.retained_metric_series = 0; }],
  ["scripts/check-g17-export-privacy.mjs", "owned-sanitized-service-logs", "three traces only", r => { r.traces_inspected = 3; r.retained_trace_documents = 3; }],
  ["scripts/check-g17-export-privacy.mjs", "invalid-", "no malformed traceparent", r => { r.injected_controls.malformed_traceparent_requests = 0; }],
  ["scripts/check-g17-export-privacy.mjs", "invalid-", "a business response was not 200", r => { r.injected_controls.all_business_responses_200 = false; }],
  // restart
  ["scripts/check-g17-restart.mjs", "stale-readiness-is-unknown-not-healthy", "overall FAIL", r => { r.result = "FAIL"; }],
  ["scripts/check-g17-restart.mjs", "stale-readiness-is-unknown-not-healthy", "stale check absent", r => { r.checks = r.checks.filter(c => c.name !== "stale-readiness-is-unknown-not-healthy"); }],
  ["scripts/check-g17-restart.mjs", "owned-api-shutdown-bounded", "shutdown check absent", r => { r.checks = r.checks.filter(c => c.name !== "owned-api-shutdown-bounded"); }],
  ["scripts/check-g17-restart.mjs", "restart-recovers-health-without-business-duplication", "api not restored", r => { r.api_restored = false; }],
  // worker journey
  ["scripts/check-g17-worker-journey.mjs", "matched_safe_trace_pairs", "one matched pair", r => { r.matched_safe_trace_pairs = 1; }],
  ["scripts/check-g17-worker-journey.mjs", "matched_safe_trace_pairs", "replay not performed", r => { r.duplicate_replay = false; }],
  ["scripts/check-g17-worker-journey.mjs", "matched_safe_trace_pairs", "49 receipts", r => { r.receipts_accepted = 49; }],
  ["scripts/check-g17-worker-journey.mjs", "matched_safe_trace_pairs", "49 reconciled", r => { r.reconciled = 49; }],
  ["scripts/check-g17-worker-journey.mjs", "matched_safe_trace_pairs", "two traced journeys", r => { r.sampled_journeys = 2; }],
  ["scripts/check-g17-worker-journey.mjs", "matched_safe_trace_pairs", "reconciled differs from accepted", r => { r.receipts_accepted = 80; r.reconciled = 50; }],
  ["scripts/check-g17-worker-journey.mjs", "Prometheus exported private tenant/source/account", "worker role not restricted", r => { r.worker_role = "owner"; }],
  ["scripts/check-g17-worker-journey.mjs", "Prometheus exported private tenant/source/account", "no retained traces", r => { r.retained_trace_documents = 0; }],
  ["scripts/check-g17-worker-journey.mjs", "Prometheus exported private tenant/source/account", "series count mismatch", r => { r.retained_metric_series = r.exported_metric_series_checked_for_tenant_privacy + 1; }],
  ["scripts/check-g17-worker-journey.mjs", "Prometheus exported private tenant/source/account", "49 receipts", r => { r.receipts_accepted = 49; }],
  ["scripts/check-g17-worker-journey.mjs", "Prometheus exported private tenant/source/account", "49 reconciled", r => { r.reconciled = 49; }],
  // pressure
  ["scripts/check-g17-pressure-matrix.mjs", "business-under-tenfold-pressure-and-exporter-outage", "named case failed", r => { r.cases.find(c => c.name === "business-under-tenfold-pressure-and-exporter-outage").status = "FAIL"; }],
  ["scripts/check-g17-pressure-matrix.mjs", "business-under-tenfold-pressure-and-exporter-outage", "named case missing", r => { r.cases.find(c => c.name === "business-under-tenfold-pressure-and-exporter-outage").name = "other"; }],
  ["scripts/check-g17-pressure-matrix.mjs", "business-under-tenfold-pressure-and-exporter-outage", "three cases only", r => { r.cases.pop(); }],
  // cardinality
  ["scripts/check-g17-cardinality-memory.mjs", "FOUR_TEN_THOUSAND_PATH_STRESS_WAVES", "no pairs", r => { r.pairs = []; }],
  ["scripts/check-g17-cardinality-memory.mjs", "FOUR_TEN_THOUSAND_PATH_STRESS_WAVES", "a pair failed", r => { r.pairs[0].pass = false; }],
  ["scripts/check-g17-cardinality-memory.mjs", "FOUR_TEN_THOUSAND_PATH_STRESS_WAVES", "growth over budget", r => { r.post_stress_memory_growth_pct = 15.1; }],
  ["scripts/check-g17-cardinality-memory.mjs", "FOUR_TEN_THOUSAND_PATH_STRESS_WAVES", "a wave short of paths", r => { r.rows[2].distinct_paths = 9999; }],
  // performance
  ["scripts/check-g17-performance.mjs", "measured_routes", "a pair without memory", r => { r.pairs[1].memory_increase_pct = null; }],
  ["scripts/check-g17-performance.mjs", "measured_routes", "memory over 15", r => { r.gate.mean_memory_increase_pct = 15.01; }],
  ["scripts/check-g17-performance.mjs", "measured_routes", "pooled baseline over 10", r => { r.pooled.baseline.p95_increase_pct = 10.01; }],
  ["scripts/check-g17-performance.mjs", "THREE_MATCHED_PAIRS", "799 baseline samples", r => { r.rows[0].raw_ms.baseline = new Array(799).fill(1); }],
  ["scripts/check-g17-performance.mjs", "THREE_MATCHED_PAIRS", "799 authorized samples", r => { r.rows[5].raw_ms.authorized = new Array(799).fill(1); }],
  ["scripts/check-g17-performance.mjs", "THREE_MATCHED_PAIRS", "no cpu count", r => { r.system.cpus = 0; }],
  ["scripts/check-g17-performance.mjs", "THREE_MATCHED_PAIRS", "no memory size", r => { r.system.memory_bytes = 0; }],
  ["scripts/check-g17-performance.mjs", "THREE_MATCHED_PAIRS", "no p95 stddev", r => { r.p95_increase_stddev_pct = NaN; }],
  // storage boundary
  ["scripts/check-g17-storage.mjs", "full-telemetry-store", "stale 44.9 s", r => { r.telemetry_degradation.stale_age_seconds = 44.9; }],
  ["scripts/check-g17-storage.mjs", "full-telemetry-store", "seven protected tables only", r => { r.positive_history.tables = {a:1,b:1,c:1,d:1,e:1,f:1,g:1}; }],
  // regression
  ["scripts/check-g17-regression-matrix.mjs", "M13AdmissionControlAcceptanceTests", "no passed tests", r => { r.cases.M13.passed = 0; }],
  ["scripts/check-g17-regression-matrix.mjs", "G15AuditAcceptanceTests", "wrong class", r => { r.cases.audit.class = "Other"; }],
  // network
  ["scripts/check-g17-network.mjs", "dashboard_listener", "grafana publishes", r => { r.dashboard_listener.grafana_publishes_nothing = false; }],
  ["scripts/check-g17-network.mjs", "dashboard_listener", "wrong publisher", r => { r.dashboard_listener.published_by = "grafana"; }],
  ["scripts/check-g17-network.mjs", "dashboard_listener", "http 500", r => { r.dashboard_listener.loopback_http_status = 500; }],
  ["scripts/check-g17-network.mjs", "Untrusted host client reached protected monitoring", "eight services only", r => { delete r.expected.web; delete r.observed.web; }],
  ["scripts/check-g17-network.mjs", "Untrusted host client reached protected monitoring", "a process state missing", r => { delete r.process_states.web; }]
];
test("each individual clause of the script predicates is load-bearing", () => {
  for (const [path, caseName, label, mutate] of SINGLE_FACT) {
    const broken = clone(FIXTURES[path]);
    mutate(broken);
    assert.equal(rawWitnessPasses(path, caseName, broken), false,
      path + " / " + caseName + " accepted: " + label);
  }
});
test("the boundary values themselves still pass", () => {
  const exportPrivacy = clone(FIXTURES["scripts/check-g17-export-privacy.mjs"]);
  exportPrivacy.injected_controls.requests = 300;
  assert.equal(rawWitnessPasses("scripts/check-g17-export-privacy.mjs", "baggage:\"secret=\"", exportPrivacy), true);
  const journey = clone(FIXTURES["scripts/check-g17-worker-journey.mjs"]);
  journey.matched_safe_trace_pairs = 2;
  assert.equal(rawWitnessPasses("scripts/check-g17-worker-journey.mjs", "matched_safe_trace_pairs", journey), true);
  const performance = clone(FIXTURES["scripts/check-g17-performance.mjs"]);
  performance.gate.mean_memory_increase_pct = 15;
  performance.pooled.baseline.p95_increase_pct = 10;
  performance.pooled.authorized.p95_increase_pct = 10;
  performance.rows.forEach(row => { row.raw_ms.baseline = new Array(800).fill(1); row.raw_ms.authorized = new Array(800).fill(1); });
  assert.equal(rawWitnessPasses("scripts/check-g17-performance.mjs", "measured_routes", performance), true);
  assert.equal(rawWitnessPasses("scripts/check-g17-performance.mjs", "THREE_MATCHED_PAIRS", performance), true);
  const storage = clone(FIXTURES["scripts/check-g17-storage.mjs"]);
  storage.telemetry_degradation.stale_age_seconds = 45;
  assert.equal(rawWitnessPasses("scripts/check-g17-storage.mjs", "full-telemetry-store", storage), true);
});

test("every minimum is inclusive: the exact approved edge passes", () => {
  const edge = [
    ["scripts/check-g17-export-privacy.mjs", "owned-sanitized-service-logs",
      r => { r.traces_inspected = 4; r.retained_trace_documents = 4; r.spans_inspected = 2; r.retained_log_bytes = 1; }],
    ["scripts/check-g17-export-privacy.mjs", "invalid-", r => { r.injected_controls.malformed_traceparent_requests = 1; }],
    ["scripts/check-g17-malformed-export.mjs", "malformed-context-export-safe",
      r => { r.validated_exported_traces = 4; r.trace_records_retained = 4; }],
    ["scripts/check-g17-restart.mjs", "actual_spans_inspected",
      r => { r.canceled_trace_correlation.inflight_found_in_tempo = 1; r.canceled_trace_correlation.inflight_canceled_requests = 40; }],
    ["scripts/check-g17-cardinality-memory.mjs", "FOUR_TEN_THOUSAND_PATH_STRESS_WAVES",
      r => { r.pairs = [{ pass: true }]; r.post_stress_memory_growth_pct = r.maximum_allowed_memory_overhead_pct; }],
    ["scripts/check-g17-worker.mjs", "fault-worker-stop-suppresses-stale-backlog",
      r => { const d = r.checks[0].detail; d.stale_backlog_seconds_at_start = 300; d.stale_backlog_seconds_at_end = 300; }]
  ];
  for (const [path, caseName, mutate] of edge) {
    const report = clone(FIXTURES[path]);
    mutate(report);
    assert.equal(rawWitnessPasses(path, caseName, report), true, path + " / " + caseName + " rejected its exact edge");
  }
});

// Second-pass material survivors (S25-M series).
test("a predicate that throws on a malformed report fails closed, and unknown cases have no predicate", () => {
  for (const w of scriptWitnesses) {
    for (const hostile of [{result: "PASS"}, {result: "PASS", checks: [null], cases: null, injected_controls: null,
        positive_history: null, telemetry_degradation: null, pairs: null, rows: null, lease: null}]) {
      assert.equal(rawWitnessPasses(w.path, w.case, hostile), false, w.id + " accepted a malformed report");
    }
  }
  assert.equal(hasRawPredicate("scripts/not-registered.mjs", "x"), false);
  assert.equal(hasRawPredicate("scripts/check-g17-network.mjs", "a case with no predicate"), false);
  assert.equal(hasRawPredicate("scripts/check-g17-network.mjs", "dashboard_listener"), true);
});
test("storage and regression predicates are exact at their edges and need each fact in every row", () => {
  const path = "scripts/check-g17-storage.mjs";
  const eight = clone(FIXTURES[path]);
  eight.positive_history.tables = {a: 1, b: 1, c: 1, d: 1, e: 1, f: 1, g: 1, h: 1};
  assert.equal(rawWitnessPasses(path, "full-telemetry-store", eight), true);
  const rowFacts = {
    "rows without a hash": r => { r.fixture_rows_before.m11_history.sha256 = ""; r.fixture_rows_after.m11_history.sha256 = ""; },
    "a hash without rows": r => { r.fixture_rows_before.m15_outbox.rows = 0; r.fixture_rows_after.m15_outbox.rows = 0; }
  };
  for (const [label, mutate] of Object.entries(rowFacts)) {
    const broken = clone(FIXTURES[path]);
    mutate(broken);
    assert.equal(rawWitnessPasses(path, "all-application-history-byte-identical", broken), false, label);
  }
  const reg = "scripts/check-g17-regression-matrix.mjs";
  const one = clone(FIXTURES[reg]);
  one.cases.M13 = {class: "M13AdmissionControlAcceptanceTests", passed: 1, declared_methods: 1, executed_distinct_methods: 1};
  one.cases.audit = {class: "G15AuditAcceptanceTests", passed: 1, declared_methods: 1, executed_distinct_methods: 1};
  assert.equal(rawWitnessPasses(reg, "M13AdmissionControlAcceptanceTests", one), true);
  assert.equal(rawWitnessPasses(reg, "G15AuditAcceptanceTests", one), true);
  const perf = "scripts/check-g17-performance.mjs";
  const single = clone(FIXTURES[perf]);
  single.system.cpus = 1;
  assert.equal(rawWitnessPasses(perf, "THREE_MATCHED_PAIRS", single), true);
});
test("the host memory size is only required to be a positive number", () => {
  const perf = "scripts/check-g17-performance.mjs";
  const tiny = clone(FIXTURES[perf]);
  tiny.system.memory_bytes = 1;
  assert.equal(rawWitnessPasses(perf, "THREE_MATCHED_PAIRS", tiny), true);
});
