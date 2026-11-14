import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");

test("G17 runner restricts exact-witness integration checks and keeps other gates explicit", () => {
  const runner=read("scripts/run-g17-local.ps1");
  assert.match(runner, /FullyQualifiedName~HarborOps\.IntegrationTests\.M17/);
  assert.match(runner, /HARBOROPS_G12_EXCLUSIVE_POSTGRES = '1'/);
  assert.match(runner, /HARBOROPS_G13_EXCLUSIVE_POSTGRES = '1'/);
  assert.match(runner, /g17-integration\.trx/);
  assert.match(runner, /g17-regressions\.trx/);
  assert.match(runner, /check-g17-regression-matrix\.mjs/);
});

test("the G17 stack validates collector config and refuses dead diagnostic services", () => {
  const runner=read("scripts/run-g17-local.ps1");
  const network=read("scripts/check-g17-network.mjs");
  const collector=read("monitoring/m17/collector.yaml");
  assert.match(runner,/collector-config-validation/);
  assert.match(runner,/m17-collector-config-validation\.txt/);
  assert.match(network,/verifyServiceState\(service,state\)/);
  assert.match(network,/process_states:processStates/);
  assert.match(network,/isRefusedConnection\(error\)/);
  assert.match(collector,/otlp_grpc\/tempo/);
  assert.doesNotMatch(collector,/^  otlp\/tempo:/m);
});

test("G17 independent-review remediation is enforced by source and the evidence runner",()=>{
  const runner=read("scripts/run-g17-local.ps1");
  const privacy=read("scripts/check-g17-export-privacy.mjs");
  const worker=read("scripts/check-g17-worker-journey.mjs");
  const malformed=read("scripts/check-g17-malformed-export.mjs");
  const guard=read("scripts/g17-raw-witness.mjs");
  const dashboard=JSON.parse(read("monitoring/m17/grafana/dashboard.json"));
  const browser=read("tests/browser/m17/dashboard.spec.mjs");
  const panel=dashboard.panels.find(p=>p.id===4);
  assert.equal(panel.title,"PostgreSQL readiness");
  assert.equal(panel.fieldConfig.defaults.noValue,"Unknown");
  assert.equal(panel.fieldConfig.defaults.mappings[0].options["0"].text,"Unavailable");
  assert.equal(panel.fieldConfig.defaults.mappings[0].options["1"].text,"Ready");
  for(const artifact of [
    "m17-export-privacy-traces.json","m17-export-privacy-logs.txt",
    "m17-export-privacy-series.json","m17-worker-journey-traces.json",
    "m17-worker-journey-series.json","m17-malformed-export-results.json",
    "m17-malformed-export-traces.json","m17-ui-state-chromium.json",
    "m17-ui-state-firefox.json","m17-ui-state-webkit.json"
  ])assert.ok(runner.includes(artifact),"Missing required raw artifact "+artifact);
  assert.match(runner,/check-g17-malformed-export\.mjs/);
  assert.match(runner,/g17-malformed-trace-evidence\.test\.mjs/);
  assert.match(runner,/g17-s25-raw-evidence\.test\.mjs/);
  assert.match(read("scripts/verify-g17-manifest.mjs"),/verifyS25RawSnapshots/);
  assert.match(privacy,/retainedTraces\.push\(/);
  assert.match(privacy,/retainedLogs=logs/);
  assert.match(worker,/retainedTraceDocuments\.push\(/);
  assert.match(worker,/retainedMetricSeries=metricBody\.data/);
  assert.match(malformed,/verifyMalformedBaselineTrace/);
  assert.match(guard,/non_loopback_addresses_refused >= 1/);
  assert.match(browser,/renderedPanelText/);
  assert.match(browser,/browser API denial retains server-issued correlation/);
});

test("first-incident 5xx counter and sampled failure alert are explicitly witnessed",()=>{
  const source=read("src/HarborOps.Api/Observability/M17Telemetry.cs");
  const fault=read("scripts/check-g17-database-fault.mjs");
  const matrix=read("scripts/check-g17-alert-matrix.mjs");
  const alerts=read("monitoring/m17/alerts.yaml");
  assert.ok(source.includes("FailedRequests.Add(0,"));
  assert.ok(fault.includes("validateLive5xxEvidence(observed)"));
  assert.ok(fault.includes("await wait(5000)"));
  assert.ok(matrix.includes("sampled.failed_increase_5m>0"));
  assert.ok(alerts.includes("harborops:m17:eligible_requests:5m >= 100"));
  assert.ok(alerts.includes("api_5xx_fraction:5m > 0.01"));
});

test("5xx HTTP contract distinguishes zero series seed from exactly one failure",()=>{
  const contract=read("tests/HarborOps.ContractTests/M17HttpContractTests.cs");
  assert.ok(contract.includes("Assert.Contains(0L,failures)"));
  assert.ok(contract.includes("Assert.Single(positiveFailures)"));
  assert.ok(contract.includes("Assert.Equal(1L,failures.Sum())"));
});

test("the default stack does not start or expose M17 components", () => {
  const base = read("compose.yaml");
  assert.doesNotMatch(base, /M17__Enabled|otelcol:|3301:3000/);
  const optional = read("compose.m17.observability.override.yaml");
  assert.match(optional, /M17__Enabled:\s*"true"/);
  assert.match(optional, /127\.0\.0\.1:3301:3000/);
  assert.doesNotMatch(optional, /^\s*ports:\s*\[\s*"0\.0\.0\.0:/m);
  assert.match(optional, /HARBOROPS_M17_GRAFANA_PASSWORD/);
});

test("exporter and trace controls remain bounded and private", () => {
  const registration = read("src/HarborOps.Api/Observability/M17TelemetryRegistration.cs");
  const options = read("src/HarborOps.Api/Observability/M17TelemetryOptions.cs");
  const sampler = read("src/HarborOps.Api/Observability/M17BoundedSampler.cs");
  assert.match(registration, /maxQueueSize:\s*2048/);
  assert.match(registration, /M17CountingTraceExporter/);
  assert.match(read("src/HarborOps.Worker/Observability/M17WorkerTelemetry.cs"), /M17WorkerCountingTraceExporter/);
  assert.match(registration, /exporterTimeoutMilliseconds:\s*1000/);
  assert.match(read("monitoring/m17/alerts.yaml"), /reason="exporter_failure"/);
  assert.match(read("monitoring/m17/alerts.yaml"), /error_type="queue_full"/);
  assert.match(sampler, /admitted\s*>=\s*200/);
  assert.match(sampler, /TraceIdRatioBasedSampler\(0\.10\)/);
  assert.match(options, /string\.Equals\(endpoint\.Host, "otelcol"/);
  assert.match(options, /IPAddress\.IsLoopback/);
  assert.match(registration, /TraceContextPropagator/);
});

test("service entrypoints and accepted default-off behaviors are unchanged", () => {
  const source = read("src/HarborOps.Api/Program.cs");
  for (const endpoint of [
    'app.MapHealthChecks("/health/live"',
    'app.MapHealthChecks("/health/ready"',
    "app.MapNotificationEndpoints();",
    "app.MapOperationalKpiEndpoints();",
    "app.MapAuditEndpoints();"
  ]) assert.ok(source.includes(endpoint), endpoint);
  for (const flag of [
    'M10:EnableProjector', 'M11:EnableEvaluator',
    'M13:EnableIdleCleanup', 'M15:EnableDelivery'
  ]) assert.ok(source.includes(flag), flag);
});

test("private local dashboard has real datasource mappings and no default green placeholder", () => {
  const dashboard = JSON.parse(read("monitoring/m17/grafana/dashboard.json"));
  assert.ok(dashboard.panels.length >= 7);
  assert.ok(dashboard.panels.every(p => p.fieldConfig.defaults.noValue === "Unknown"));
  assert.ok(dashboard.panels.every(p => p.targets.length > 0));
  assert.ok(dashboard.panels.some(p => /p95/i.test(p.title)));
  assert.ok(dashboard.panels.some(p => /readiness/i.test(p.title)));
});

test("G17 acceptance is never inferred from source file presence", () => {
  const j = JSON.parse(read("docs/M17_G17_ASSERTION_INVENTORY_20261112.json"));
  assert.equal(j.scenarios.length, 25);
  assert.equal(j.scenarios.reduce((total, scenario) => total + scenario.assertions.length, 0), 75);
  if (j.gate_acceptance === "ACCEPTED") {
    assert.ok(j.scenarios.every(scenario => scenario.assertions.every(a =>
      a.status === "PASS" && typeof a.witness === "string" && a.witness.length > 0)));
    assert.equal(j.technical_review, "PASS");
  } else {
    assert.equal(j.gate_acceptance, "NOT_ACCEPTED");
  }
});

test("the runnable monitoring stack uses isolated E-backed storage and a supported image", () => {
  const base = read("compose.m17.verify.yaml");
  const overlay = read("compose.m17.observability.override.yaml");
  assert.match(base, /HARBOROPS_G17_STORAGE_ROOT/);
  assert.match(base, /127\.0\.0\.1:15543:5432/);
  const dbPort = Number(base.match(/127\.0\.0\.1:(\d+):5432/)?.[1]);
  assert.ok(Number.isInteger(dbPort) && dbPort >= 1024 && dbPort <= 65535);
  // The test suites run against their own cluster so the stack's runtime roles survive.
  assert.match(base, /127\.0\.0\.1:15544:5432/);
  assert.notEqual(dbPort, 15544);
  assert.match(base, /postgres_tests:[\s\S]*?profiles:\s*\[integration-tests\]/);
  assert.match(read("scripts/run-g17-local.ps1"), /Port=15544;Database=harborops_integration/);
  assert.doesNotMatch(read("scripts/run-g17-local.ps1"), /Port=15543;Database=harborops_integration/);
  assert.match(base, /127\.0\.0\.1:18080:8080/);
  assert.match(base, /127\.0\.0\.1:15173:80/);
  assert.match(overlay, /grafana\/grafana:13\.3\.1/);
  assert.doesNotMatch(overlay, /grafana\/grafana-oss:/);
  assert.match(overlay, /OTEL_METRIC_EXPORT_INTERVAL:\s*"15000"/);
});

test("all six approved local alert categories are represented without external routing", () => {
  const text = read("monitoring/m17/alerts.yaml");
  for (const name of [
    "HarborOpsElevatedApiFailureRate", "HarborOpsHighApiP95",
    "HarborOpsDatabaseUnavailable", "HarborOpsTrackingBacklogStale",
    "HarborOpsMonitoringMissing", "HarborOpsExporterDrops"
  ]) assert.ok(text.includes("alert: " + name), name);
  assert.match(text, /keep_firing_for:\s*5m/);
  assert.match(text, /keep_firing_for:\s*10m/);
  assert.match(text, /queue_full/);
  assert.doesNotMatch(text, /https?:\/\//);
});

test("worker disposition is reflected rather than counting parked work as processed", () => {
  const source = read("src/HarborOps.Worker/WorkerShell.cs");
  assert.match(source, /TrackingWorkDisposition\.NeedsReconciliation => "needs_reconciliation"/);
  assert.match(source, /TrackingWorkDisposition\.Stale => "stale"/);
  assert.match(source, /OldestEligibleBacklogAgeSecondsAsync/);
  const api = read("src/HarborOps.Api/Observability/M17TelemetryRegistration.cs");
  assert.match(api, /AddHostedService<M17ReadinessProbeService>/);
  assert.match(api, /otel\.sdk\.experimental/);
});

test("trace export rejects raw events, links, trace state and status descriptions", () => {
  const overlay = read("compose.m17.observability.override.yaml");
  const worker = read("compose.m17.worker.override.yaml");
  for (const compose of [overlay, worker]) {
    assert.match(compose, /OTEL_SPAN_EVENT_COUNT_LIMIT:\s*"0"/);
    assert.match(compose, /OTEL_SPAN_LINK_COUNT_LIMIT:\s*"0"/);
  }
  for (const path of [
    "src/HarborOps.Api/Observability/M17CountingTraceExporter.cs",
    "src/HarborOps.Worker/Observability/M17WorkerCountingTraceExporter.cs"
  ]) assert.match(read(path), /RequireSafeExporterLimits\(\)/);
  for (const path of [
    "src/HarborOps.Api/Observability/M17TraceSanitizer.cs",
    "src/HarborOps.Worker/Observability/M17WorkerTraceSanitizer.cs"
  ]) assert.match(read(path), /SanitizeNonTagFields\(activity\)/);
  const guard = read("src/HarborOps.Infrastructure/Observability/M17TracePrivacyGuard.cs");
  assert.match(guard, /activity.TraceStateString = null/);
  assert.match(guard, /activity.SetStatus\(activity.Status\)/);
});

test("detached worker journey uses keyed shared sampling without a false parent span", () => {
  const endpoint = read("src/HarborOps.Api/Integrations/TrackingEndpoints.cs");
  const worker = read("src/HarborOps.Worker/WorkerShell.cs");
  const apiSampler = read("src/HarborOps.Api/Observability/M17BoundedSampler.cs");
  const workerSampler = read("src/HarborOps.Worker/Observability/M17WorkerBoundedSampler.cs");
  assert.match(endpoint, /StartActivity\(\s*"tracking.receipt"/);
  assert.match(worker, /StartActivity\(\s*"tracking.process"/);
  assert.match(apiSampler, /SampleLinkedJourney\(link\)/);
  assert.match(workerSampler, /SampleLinkedJourney\(link\)/);
  assert.doesNotMatch(worker, /SetParentId|ActivityContext.CreateFromString/);
});

test("the disposable campaign isolates telemetry networks and retains failures", () => {
  const overlay = read("compose.m17.observability.override.yaml");
  const runner = read("scripts/run-g17-local.ps1");
  const manifest = read("scripts/verify-g17-manifest.mjs");
  assert.match(overlay, /telemetry_monitor:\s*\n\s*internal:\s*true/);
  assert.match(overlay, /telemetry_ingest:\s*\n\s*internal:\s*true/);
  assert.match(overlay, /api:[\s\S]*?networks:\s*\[internal, telemetry_ingest\]/);
  assert.match(overlay, /grafana:[\s\S]*?networks:\s*\[telemetry_monitor\]/);
  const grafanaBlock = overlay.slice(overlay.indexOf("  grafana:"), overlay.indexOf("  dashboard_gateway:"));
  assert.doesNotMatch(grafanaBlock, /\n    ports:/);
  assert.match(overlay, /dashboard_gateway:[\s\S]*?ports:\s*\["127\.0\.0\.1:3301:3000"\]/);
  assert.match(overlay, /dashboard_gateway:[\s\S]*?networks:\s*\[dashboard_access, telemetry_monitor\]/);
  assert.match(overlay, /dashboard_access:\n\s*driver:\s*bridge/);
  assert.doesNotMatch(overlay, /dashboard_access:\n\s*driver:\s*bridge\n\s*internal:/);
  assert.match(runner, /VerifyDisposableWorker/);
  assert.match(runner, /run-status.json/);
  assert.match(runner, /g17-manifest.mjs/);
  assert.match(manifest, /missing_expected_artifacts/);
  assert.match(read("scripts/check-g17-network.mjs"), /source_sha/);
});


test("hosted bind mounts are owned and writable before starting rootless PostgreSQL",()=>{
  const runner=read("scripts/run-g17-local.ps1");
  assert.match(runner,/if \(\$LocalVolumes -or \$Hosted\)/);
  for(const name of ["postgres","postgres_tests","keyring"]) {
    assert.match(runner,new RegExp("prepare-owned-state-"));
    assert.match(runner,new RegExp("verify-owned-state-"));
  }
  assert.match(runner,/test -w \/prepared && touch \/prepared\/\.g17-writable-check/);
  const manifest=read("scripts/g17-manifest.mjs");
  assert.match(manifest,/allowedEvidencePath\(relative\)/);
  assert.match(manifest,/textEvidencePath\(relative\)/);
});

test("all S25 remediation controls are required before a green evidence manifest",()=>{
  const runner=read("scripts/run-g17-local.ps1");
  const raw=read("scripts/g17-s25-raw-evidence.mjs");
  const workflow=read(".github/workflows/verify-g17-s25.yml");
  const coverage=JSON.parse(read("docs/M17_G17_TEST_COVERAGE_20261113.json"));
  assert.equal(coverage.scenarios.length,25);
  assert.equal(coverage.scenarios.flatMap(s=>s.assertions).length,75);
  for(const artifact of [
    "m17-ui-no-traffic-chromium.json",
    "m17-ui-no-traffic-firefox.json",
    "m17-ui-no-traffic-webkit.json",
    "m17-worker-journey-logs.txt",
    "m17-page-fault-injection.json",
    "m17-page-fault-dashboard.json",
    "m17-diagnostic-journey-results.json",
    "m17-p09-independent-recalculation.json"
  ]) {
    assert.ok(runner.includes(artifact),"Required new evidence missing from runner: "+artifact);
  }
  for(const source of [
    "g17-diagnostic-evidence.test.mjs",
    "g17-worker-log-privacy.test.mjs",
    "g17-p09-review.test.mjs"
  ]) {
    assert.ok(workflow.includes(source),"Required negative control absent from preflight: "+source);
  }
  assert.ok(workflow.includes("include-hidden-files: true"));
  assert.ok(raw.includes("verifyBoundedDiagnosticJourney(diagnostic)"));
  assert.ok(raw.includes("worker.worker_log_sha256"));
  assert.ok(raw.includes("independently.individual_p95_outliers"));
  const journey=coverage.scenarios[19].assertions;
  assert.ok(journey.every(a=>a.witness?.path==="scripts/check-g17-diagnostic-journey.mjs"));
  const independent=coverage.scenarios[24].assertions;
  assert.ok(independent.every(a=>a.assessment==="INDEPENDENT_REVIEW"&&a.witness===null));
});

test("owned synthetic storage fixture obeys immutable shipment code constraints",()=>{
  const storage=read("scripts/check-g17-storage.mjs");
  assert.ok(storage.includes('nonce.toUpperCase()'));
  assert.ok(storage.includes('const shipmentRef="G17-STORAGE-"'));
  assert.ok(storage.includes('assert.match(shipmentRef,'));
  assert.ok(storage.includes('m15_notification_outbox'));
  assert.ok(storage.includes('shipment_exception_history'));
  assert.ok(storage.includes('database_sha256_before'));
  assert.ok(storage.includes('database_sha256_after'));
});
