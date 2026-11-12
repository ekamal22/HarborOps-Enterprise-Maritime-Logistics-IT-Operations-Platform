import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");

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
  assert.match(registration, /MaxQueueSize\s*=\s*2048/);
  assert.match(registration, /ExporterTimeoutMilliseconds\s*=\s*1000/);
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
  assert.match(base, /127\.0\.0\.1:155432:5432/);
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
