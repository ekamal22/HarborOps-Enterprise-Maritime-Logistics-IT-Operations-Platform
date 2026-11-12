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

test("G17 remains explicitly unexecuted until source-locked raw evidence exists", () => {
  const j = JSON.parse(read("docs/M17_G17_ASSERTION_INVENTORY_20261112.json"));
  assert.equal(j.scenarios.length, 25);
  assert.equal(j.scenarios.reduce((total, s) => total + s.assertions.length, 0), 75);
  assert.equal(j.gate_acceptance, "NOT_ACCEPTED");
  assert.ok(j.scenarios.every(s => s.status === "NOT_EXECUTED"));
});
