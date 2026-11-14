// Binds every script-based G17 technical assertion to a specific passing fact in the
// raw result written by that script. A script report that merely says PASS is not a
// witness for an individual assertion: the named case must be present and passing.
// Anything without an entry here fails closed.

const passed = (report, name) =>
  (report.checks ?? []).some(c => (c.name ?? c.fault) === name &&
    (c.status ?? c.result) === "PASS");
// Different scripts record a check's payload as detail or as data.
const detail = (report, name) => {
  const found = (report.checks ?? []).find(c => (c.name ?? c.fault) === name);
  return found?.detail ?? found?.data;
};
const ok = report => report?.result === "PASS";
const kinds = report => new Set((report.snapshots ?? []).map(s => s.kind));

export const RAW_REPORT_FILES = new Map([
  ["scripts/check-g17-network.mjs", "m17-network-results.json"],
  ["scripts/check-g17-worker.mjs", "m17-worker-results.json"],
  ["scripts/check-g17-worker-journey.mjs", "m17-worker-journey-results.json"],
  ["scripts/check-g17-collector-fault.mjs", "m17-collector-fault-results.json"],
  ["scripts/check-g17-database-fault.mjs", "m17-database-fault-results.json"],
  ["scripts/check-g17-restart.mjs", "m17-restart-results.json"],
  ["scripts/check-g17-export-privacy.mjs", "m17-export-privacy-results.json"],
  ["scripts/check-g17-malformed-export.mjs", "m17-malformed-export-results.json"],
  ["scripts/check-g17-cardinality-memory.mjs", "m17-cardinality-memory-results.json"],
  ["scripts/check-g17-performance.mjs", "m17-performance-raw.json"],
  ["scripts/check-g17-storage.mjs", "m17-storage-results.json"],
  ["scripts/check-g17-retention.mjs", "m17-retention-age-results.json"],
  ["scripts/check-g17-tempo-retention.mjs", "m17-tempo-retention-results.json"],
  ["scripts/check-g17-retention-matrix.mjs", "m17-retention-matrix-results.json"],
  ["scripts/check-g17-alert-matrix.mjs", "m17-alert-matrix-results.json"],
  ["scripts/check-g17-pressure-matrix.mjs", "m17-pressure-matrix-results.json"],
  ["scripts/check-g17-regression-matrix.mjs", "g17-regression-matrix-results.json"],
  ["scripts/check-g17-live-latency.mjs", "m17-live-latency-alert-results.json"]
]);

const privacy = (report) => ok(report) && report.traces_inspected >= 4 &&
  report.spans_inspected >= 2 &&
  report.retained_trace_documents === report.traces_inspected &&
  report.retained_metric_series === report.exported_metric_series_inspected &&
  report.retained_log_bytes > 0 && report.exported_metric_series_inspected > 0 &&
  kinds(report).has("tempo-trace") && kinds(report).has("prometheus-series") &&
  kinds(report).has("owned-sanitized-service-logs");

const RAW_PREDICATES = new Map([
  ["scripts/check-g17-malformed-export.mjs",new Map([
    ["malformed-context-export-safe",r=>ok(r) &&
      r.malformed_trace_context===true && r.injected_requests===960 &&
      r.validated_exported_traces>=4 &&
      r.trace_records_retained===r.validated_exported_traces &&
      r.negative_canary_absent===true]
  ])],
  ["scripts/check-g17-export-privacy.mjs", new Map([
    ["owned-sanitized-service-logs", privacy],
    // The same exported traces were produced by requests carrying an oversized
    // baggage header and a malformed traceparent on half of them; every one had to
    // receive HTTP 200 and no canary may appear in anything exported.
    ["baggage:\"secret=\"", r => privacy(r) && r.injected_controls?.oversized_baggage === true &&
      r.injected_controls.requests >= 300],
    ["invalid-", r => privacy(r) && r.injected_controls?.malformed_traceparent_requests > 0 &&
      r.injected_controls.all_business_responses_200 === true]
  ])],
  ["scripts/check-g17-database-fault.mjs", new Map([
    ["real-db-outage-keeps-live-and-errors-classified",
      r => ok(r) && passed(r, "real-db-outage-keeps-live-and-errors-classified")],
    ["database-alert-identifies-only-safe-component",
      r => ok(r) && passed(r, "database-alert-identifies-only-safe-component") &&
        detail(r, "database-alert-identifies-only-safe-component")?.alert?.name ===
          "HarborOpsDatabaseUnavailable" && r.database_restored === true]
  ])],
  ["scripts/check-g17-collector-fault.mjs", new Map([
    ["collector-recovery-automatic",
      r => ok(r) && passed(r, "collector-recovery-automatic") &&
        passed(r, "collector-fault-business-isolation") && r.collector_restored === true]
  ])],
  ["scripts/check-g17-restart.mjs", new Map([
    ["actual_spans_inspected",
      r => ok(r) && passed(r, "real-client-abort-does-not-commit-business") &&
        detail(r, "stale-readiness-is-unknown-not-healthy")?.actual_spans_inspected > 0],
    ["owned-api-shutdown-bounded", r => ok(r) && passed(r, "owned-api-shutdown-bounded")],
    ["restart-recovers-health-without-business-duplication",
      r => ok(r) && passed(r, "restart-recovers-health-without-business-duplication") &&
        r.api_restored === true],
    ["stale-readiness-is-unknown-not-healthy",
      r => ok(r) && passed(r, "stale-readiness-is-unknown-not-healthy")]
  ])],
  ["scripts/check-g17-cardinality-memory.mjs", new Map([
    ["FOUR_TEN_THOUSAND_PATH_STRESS_WAVES",
      r => ok(r) && r.run_kind === "FOUR_TEN_THOUSAND_PATH_STRESS_WAVES" &&
        r.total_paths === 40000 && r.rows?.length === 4 &&
        r.rows.every(row => row.distinct_paths >= r.count_per_wave) &&
        r.post_stress_memory_growth_pct <= r.maximum_allowed_memory_overhead_pct &&
        (r.pairs ?? []).length >= 1 && r.pairs.every(p => p.pass === true)]
  ])],
  ["scripts/check-g17-worker-journey.mjs", new Map([
    ["Prometheus exported private tenant/source/account",
      r => ok(r) && r.worker_role === "restricted" &&
        r.exported_metric_series_checked_for_tenant_privacy > 0 &&
        r.retained_metric_series === r.exported_metric_series_checked_for_tenant_privacy &&
        r.retained_trace_documents > 0 &&
        r.receipts_accepted === 50 && r.reconciled === 50],
    ["matched_safe_trace_pairs",
      r => ok(r) && r.matched_safe_trace_pairs >= 2 && r.duplicate_replay === true &&
        r.receipts_accepted === 50 && r.reconciled === 50]
  ])],
  ["scripts/check-g17-network.mjs", new Map([
    ["Untrusted host client reached protected monitoring",
      r => ok(r) && JSON.stringify(r.observed) === JSON.stringify(r.expected) &&
        Object.keys(r.expected).length === 9 &&
        Object.values(r.process_states ?? {}).length === 9 &&
        JSON.stringify(r.protected_ports_refused) === JSON.stringify([4317, 9464, 9090, 3200])],
    ["dashboard_listener",
      r => ok(r) && r.dashboard_listener?.host_ip === "127.0.0.1" &&
        r.dashboard_listener.host_port === "3301" &&
        r.dashboard_listener.loopback_http_status === 200 &&
        r.dashboard_listener.published_by === "dashboard_gateway" &&
        r.dashboard_listener.grafana_publishes_nothing === true &&
        r.dashboard_listener.non_loopback_addresses_refused >= 1]
  ])],
  ["scripts/check-g17-alert-matrix.mjs", new Map([
    ["real-http-5xx-rate-fires-reference-alert",
      r => ok(r) && (r.checks ?? []).some(c =>
        c.fault === "real-http-5xx-rate-fires-reference-alert" && c.status === "PASS")]
  ])],
  ["scripts/check-g17-worker.mjs", new Map([
    ["fault-worker-stop-suppresses-stale-backlog",
      r => ok(r) && passed(r, "fault-worker-stop-suppresses-stale-backlog") &&
        passed(r, "worker-recovers-after-fault")]
  ])],
  ["scripts/check-g17-performance.mjs", new Map([
    ["measured_routes",
      r => ok(r) && JSON.stringify(r.measured_routes) ===
        JSON.stringify(["baseline", "authorized"]) && r.pairs?.length === 3 &&
        r.gate?.pass === true && r.gate.pooled_p95_pass === true &&
        r.pooled?.baseline?.p95_increase_pct <= 10 &&
        r.pooled?.authorized?.p95_increase_pct <= 10 &&
        r.gate.memory_pass === true && r.gate.mean_memory_increase_pct <= 15 &&
        r.pairs.every(p => Number.isFinite(p.memory_increase_pct))],
    ["THREE_MATCHED_PAIRS",
      r => ok(r) && r.run_kind === "THREE_MATCHED_PAIRS" && r.rows?.length === 6 &&
        Number.isFinite(r.p95_increase_stddev_pct) &&
        Number.isFinite(r.rss_increase_stddev_pct) && r.system?.cpus >= 1 &&
        r.rows.every(row => row.raw_ms?.baseline?.length >= 800 &&
          row.raw_ms?.authorized?.length >= 800) &&
        r.system?.memory_bytes > 0]
  ])],
  ["scripts/check-g17-pressure-matrix.mjs", new Map([
    ["business-under-tenfold-pressure-and-exporter-outage",
      r => ok(r) && (r.cases ?? []).length === 4 && r.cases.some(c =>
        c.name === "business-under-tenfold-pressure-and-exporter-outage" &&
        c.status === "PASS")]
  ])],
  ["scripts/check-g17-retention-matrix.mjs", new Map([
    ["m17-tempo-retention-results.json",
      r => ok(r) && r.metrics_retention_hours === 24 && r.traces_retention_hours === 24 &&
        (r.raw_sources ?? []).includes("m17-tempo-retention-results.json") &&
        (r.raw_sources ?? []).includes("m17-retention-age-results.json")]
  ])],
  ["scripts/check-g17-storage.mjs", new Map([
    ["full-telemetry-store",
      r => ok(r) && passed(r, "business-remains-responsive-during-full-telemetry-store")],
    ["all-application-history-byte-identical",
      r => ok(r) && passed(r, "all-application-history-byte-identical") &&
        /^[0-9a-f]{64}$/.test(r.database_sha256_before ?? "") &&
        r.database_sha256_before === r.database_sha256_after]
  ])],
  ["scripts/check-g17-regression-matrix.mjs", new Map([
    ["M13AdmissionControlAcceptanceTests",
      r => ok(r) && r.cases?.M13?.class === "M13AdmissionControlAcceptanceTests" &&
        r.cases.M13.passed >= 1],
    ["G15AuditAcceptanceTests",
      r => ok(r) && r.cases?.audit?.class === "G15AuditAcceptanceTests" &&
        r.cases.audit.passed >= 1]
  ])]
]);

/** Returns true only when the named case is present and passing in this raw report. */
export function rawWitnessPasses(script, caseName, report) {
  const table = RAW_PREDICATES.get(script);
  if (!table || !report) return false;
  for (const [needle, predicate] of table)
    if (caseName.includes(needle)) {
      try { return predicate(report) === true; } catch { return false; }
    }
  return false;
}
export const RAW_WITNESS_SCRIPTS = new Set(RAW_PREDICATES.keys());

export function hasRawPredicate(script, caseName) {
  const table = RAW_PREDICATES.get(script);
  return Boolean(table) && [...table.keys()].some(needle => caseName.includes(needle));
}
