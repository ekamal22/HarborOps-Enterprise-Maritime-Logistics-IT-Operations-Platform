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
    retained_trace_documents:21,retained_metric_series:158,retained_log_bytes:40000,
    snapshots: [{kind: "tempo-trace"}, {kind: "prometheus-series"},
      {kind: "owned-sanitized-service-logs"}],
    injected_controls: {oversized_baggage: true, requests: 320,
      malformed_traceparent_requests: 160, all_business_responses_200: true}},
  "scripts/check-g17-malformed-export.mjs": {result:"PASS",
    malformed_trace_context:true,injected_requests:960,
    validated_exported_traces:6,trace_records_retained:6,
    negative_canary_absent:true},
  "scripts/check-g17-diagnostic-journey.mjs": {
    result:"PASS",page_originated:true,response_status:503,
    trace_correlation_reconciled:true,server_correlation:"a".repeat(32),
    trace_id:"b".repeat(32),
    sanitized_log_line:"dependency is unavailable Correlation "+ "a".repeat(32)+" Trace "+ "b".repeat(32),
    trace_document:{trace:{resourceSpans:[{scopeSpans:[{spans:[{name:"http.request"}]}]}]}},
    alert_firing:true,dashboard_rendered:true,
    dashboard_dom_text:"HarborOpsDatabaseUnavailable",
    checks:[check("page-originated-same-request"),check("real-trace-to-db"),
      check("same-fault-rendered-alert")]},
  "scripts/check-g17-database-fault.mjs": {result: "PASS", database_restored: true,
    checks: [check("real-db-outage-keeps-live-and-errors-classified"),
      check("database-alert-identifies-only-safe-component",
        {data: {alert: {name: "HarborOpsDatabaseUnavailable"}}})]},
  "scripts/check-g17-collector-fault.mjs": {result: "PASS", collector_restored: true,
    checks: [{name: "collector-recovery-automatic", result: "PASS"},
      {name: "collector-fault-business-isolation", result: "PASS"}]},
  "scripts/check-g17-restart.mjs": {result: "PASS", api_restored: true,
    positive_worker_lease_witness:true,
    source_locked_lease_method:"ApiRestartPreservesActiveWorkerLeaseAndNoDuplicateEffects",
    lease:{receipt:"11111111-1111-4111-8111-111111111111",
      token:"22222222-2222-4222-8222-222222222222",epoch:1,attempts:1,
      identity_equal_before_stop_during_stop_after_restart:true,
      forged_token_rejected:true,forged_epoch_rejected:true,
      original_holder_finished_once:true,duplicate_finish_rejected:true,effects_after:0},
    canceled_trace_correlation:{canceled_requests:40,found_in_tempo:12,absent_in_tempo:28,
      spans_inspected:12,all_started_before_shutdown:true,none_exported_as_server_error:true},
    checks: [check("real-client-abort-does-not-commit-business",
        {detail:{canceled:40,lease_unchanged_by_cancellation:true}}),
      check("owned-api-shutdown-bounded",{detail:{lease_unchanged_during_stop:true}}),
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
    matched_safe_trace_pairs: 7, exported_metric_series_checked_for_tenant_privacy: 186,
    retained_metric_series:186,retained_trace_documents:24},
  "scripts/check-g17-network.mjs": {result: "PASS", expected: EXPECTED,
    observed: EXPECTED, process_states: Object.fromEntries(
      Object.keys(EXPECTED).map(k => [k, {status: "running"}])),
    protected_ports_refused: [4317, 9464, 9090, 3200],
    dashboard_listener: {host_ip: "127.0.0.1", host_port: "3301",
      loopback_http_status: 200, published_by: "dashboard_gateway",
      grafana_publishes_nothing: true, non_loopback_addresses_refused: 1}},
  "scripts/check-g17-alert-matrix.mjs": {result: "PASS", checks: [
    {fault: "real-http-5xx-rate-fires-reference-alert", status: "PASS",
      source_artifact: "m17-database-fault-results.json"}]},
  "scripts/check-g17-worker.mjs": {result: "PASS", checks: [
    check("fault-worker-stop-suppresses-stale-backlog",{detail:{stale_backlog_seconds_at_start:640,
      stale_backlog_seconds_at_end:702,heartbeat_absent:true,alert_firing_count:0}}),
    check("worker-recovers-after-fault",{detail:{stale_work_state_after_recovery:"NeedsReconciliation"}})]},
  "scripts/check-g17-performance.mjs": {result: "PASS", run_kind: "THREE_MATCHED_PAIRS",
    measured_routes: ["baseline", "authorized"],
    rows: new Array(6).fill({raw_ms: {baseline: new Array(1200).fill(1),
      authorized: new Array(1200).fill(1)}}),
    gate: {pass: true, pooled_p95_pass: true, memory_pass: true, mean_memory_increase_pct: 13.0},
    pooled: {baseline: {p95_increase_pct: 0.7}, authorized: {p95_increase_pct: 4.2}},
    p95_increase_stddev_pct: 3.1, rss_increase_stddev_pct: 1.2,
    system: {cpus: 12, memory_bytes: 16e9},
    pairs: [1, 2, 3].map(() => ({memory_increase_pct: 13.9,
      route_pairs: {baseline: {p95_increase_pct: 6.7}, authorized: {p95_increase_pct: 5.9}}}))},
  "scripts/check-g17-pressure-matrix.mjs": {result: "PASS", cases: [
    {name: "business-under-tenfold-pressure-and-exporter-outage", status: "PASS"},
    {name: "a", status: "PASS"}, {name: "b", status: "PASS"}, {name: "c", status: "PASS"}]},
  "scripts/check-g17-retention-matrix.mjs": {result: "PASS", metrics_retention_hours: 24,
    traces_retention_hours: 24, raw_sources: ["m17-retention-age-results.json",
      "m17-tempo-retention-results.json", "m17-storage-results.json"]},
  "scripts/check-g17-storage.mjs": {result: "PASS",
    telemetry_degradation:{signal:"OWNED_PROMETHEUS_STORAGE_ENOSPC",error_observed:true,
      storage_error_logged:true,readiness_panel_expression_unknown:true,
      stale_age_seconds:72,prometheus_unreachable:false,fresh_before_seconds:9,
      observed_after_seconds:80},
    telemetry_recovery:{recovered:true,how:"released-space"},
    fixture_rows_before:Object.fromEntries(["m8_receipts","m8_events","m8_work_items",
      "m11_exceptions","m11_history","m15_outbox","m16_audit"].map(n=>
      [n,{rows:1,sha256:"c".repeat(64),keys:["k"]}])),
    fixture_rows_after:Object.fromEntries(["m8_receipts","m8_events","m8_work_items",
      "m11_exceptions","m11_history","m15_outbox","m16_audit"].map(n=>
      [n,{rows:1,sha256:"c".repeat(64),keys:["k"]}])),
    positive_history:{tables:{users:1,organizations:1,ports:2,shipments:1,
      m8_receipts:1,m8_events:1,m8_work_items:1,
      m11_exceptions:1,m11_history:1,m15_outbox:1,m16_audit:5}},
    database_sha256_before: "a".repeat(64), database_sha256_after: "a".repeat(64),
    checks: [check("business-remains-responsive-during-full-telemetry-store"),
      check("all-application-history-byte-identical"),
      check("telemetry-observation-recovers-after-fault")]},
  "scripts/check-g17-regression-matrix.mjs": {result: "PASS", cases: {
    M13: {class: "M13AdmissionControlAcceptanceTests", passed: 3,
      declared_methods: 3, executed_distinct_methods: 3},
    audit: {class: "G15AuditAcceptanceTests", passed: 2,
      declared_methods: 2, executed_distinct_methods: 2}}}
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
    "no canceled trace correlation": r => { delete r.canceled_trace_correlation; }
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
