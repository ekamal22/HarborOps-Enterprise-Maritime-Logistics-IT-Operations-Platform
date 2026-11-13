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
  assert.match(read("scripts/run-g17-local.ps1"), new RegExp("Port=" + dbPort + ";Database=harborops_integration"));
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
