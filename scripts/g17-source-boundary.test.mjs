import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {execFileSync} from "node:child_process";
import {fileURLToPath} from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read = name => fs.readFileSync(path.join(root,name),"utf8");
const base = "b490fb8cfc879cdf05cb1f7484b99f6a9b264ed3";

test("M17 four work packages exist with actual referenced production and verification boundaries",()=>{
  const packages = [
    ["src/HarborOps.Api/Observability/M17TelemetryRegistration.cs","WithMetrics"],
    ["src/HarborOps.Api/Observability/M17TraceSanitizer.cs","OnEnd"],
    ["src/HarborOps.Api/Observability/M17Telemetry.cs","ObserveDatabaseReadiness"],
    ["src/HarborOps.Worker/Observability/M17WorkerTelemetry.cs","ObserveAttempt"],
    ["src/HarborOps.Worker/WorkerShell.cs","tracking.process"],
    ["monitoring/m17/collector.yaml","exporters:"],
    ["monitoring/m17/prometheus.yaml","scrape_configs:"],
    ["monitoring/m17/tempo.yaml","block_retention:"],
    ["monitoring/m17/alerts.yaml","HarborOpsDatabaseUnavailable"],
    ["monitoring/m17/alert-test.yaml","alert_rule_test:"],
    ["monitoring/m17/grafana/dashboard.json","API p95 latency"],
    ["compose.m17.observability.override.yaml","telemetry_monitor"],
    ["tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs",
      "HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan"],
    ["tests/HarborOps.IntegrationTests/M17WorkerShellFunctionalTests.cs",
      "ActualWorkerShellEmitsProcessedAndReconciliationDispositionsWithoutDuplicateEffects"],
    ["tests/browser/m17/dashboard.spec.mjs","dashboard requires authentication"],
    ["scripts/check-g17-database-fault.mjs","database-alert-identifies-only-safe-component"],
    ["scripts/check-g17-export-privacy.mjs","tempo-trace"],
    ["scripts/check-g17-performance.mjs","THREE_MATCHED_PAIRS"],
    ["scripts/check-g17-storage.mjs","full-telemetry-store"],
    ["scripts/run-g17-local.ps1","s25-atomic-coverage-preflight"],
    ["scripts/verify-g17-manifest.mjs","verifyTrxCases"],
    ["scripts/reconcile-g17-assertions.mjs","TECHNICAL_PASS"]
  ];
  for (const [file,proof] of packages)
    assert.ok(read(file).includes(proof),file+" has no expected implementation");
  const plan=read("docs/M17_IMPLEMENTATION_PLAN.md");
  for (const item of ["M17-01","M17-02","M17-03","M17-04"])
    assert.ok(plan.includes(item),"M17 work package absent: "+item);
});

test("every approved M17 policy has a concrete source-side verification control",()=>{
  const policy=read("docs/M17_POLICY_PROPOSAL_20261112.md");
  for (let i=1;i<=9;i++) {
    const id="M17-P"+String(i).padStart(2,"0");
    assert.ok(policy.includes(id),"Approved policy missing: "+id);
  }
  const api=read("src/HarborOps.Api/Observability/M17TelemetryRegistration.cs");
  const limits=read("src/HarborOps.Api/Observability/M17BoundedSampler.cs");
  const compose=read("compose.m17.observability.override.yaml");
  const dashboards=JSON.parse(read("monitoring/m17/grafana/dashboard.json"));
  assert.match(api,/maxQueueSize:\s*2048/);
  assert.match(limits,/TraceIdRatioBasedSampler\(0\.10\)/);
  assert.match(limits,/admitted\s*>=\s*200/);
  assert.match(compose,/127\.0\.0\.1:3301:3000/);
  assert.match(compose,/retention\.time=24h/);
  assert.match(compose,/retention\.size=120MB/);
  assert.ok(dashboards.panels.some(p=>p.targets.some(t=>t.expr.includes("harborops_db_readiness_ratio"))));
  assert.equal(dashboards.panels.length,15);
  const activeAlerts=dashboards.panels.find(p=>p.title==="Active local alerts");
  assert.equal(activeAlerts?.type,"table");
  assert.equal(activeAlerts?.targets?.[0]?.format,"table");
  assert.equal(activeAlerts?.targets?.[0]?.instant,true);
  assert.equal(activeAlerts?.targets?.[0]?.expr,
    'ALERTS{alertstate="firing"}');
  const browserObserver=read("tests/browser/m17/page-fault-journey.mjs");
  assert.ok(browserObserver.includes("await item.scrollIntoViewIfNeeded("),
    "Offscreen alert table must be scrolled into view before rendering proof");
  assert.ok(browserObserver.includes("[data-griditem-key=\"grid-item-'+panel.id+'\"]"),
    "Alert rendering witness must be scoped to the exact saved panel container");
  assert.ok(browserObserver.indexOf("scrollIntoViewIfNeeded(")<
    browserObserver.indexOf("[role=columnheader]"),
    "The panel must be scrolled into view before its table is read");
  assert.match(browserObserver,/\[role=gridcell\]/,
    "Alert name and state must be checked as table cells, not as loose page text");
  // F01: the API 5xx and p95 panels must read the sufficiency-guarded series, never the raw expression.
  const guarded5xx=dashboards.panels.find(p=>p.title==="API 5xx rate");
  const guardedP95=dashboards.panels.find(p=>p.title==="API p95 latency");
  assert.equal(guarded5xx?.targets?.[0]?.expr,"harborops:m17:api_5xx_rate_sufficient:5m");
  assert.equal(guardedP95?.targets?.[0]?.expr,"harborops:m17:api_p95_seconds_sufficient:5m");
  const alertRules=read("monitoring/m17/alerts.yaml");
  for(const [record,fragment] of [
    ["harborops:m17:api_5xx_rate_sufficient:5m","harborops_request_failures_total"],
    ["harborops:m17:api_p95_seconds_sufficient:5m","histogram_quantile(0.95"]]) {
    const at=alertRules.indexOf("record: "+record);
    assert.ok(at>=0,"Missing guarded dashboard rule "+record);
    const line=alertRules.slice(at,alertRules.indexOf("\n",alertRules.indexOf("expr:",at)));
    assert.ok(line.includes(fragment));
    assert.match(line,/and on\(\) \(harborops:m17:eligible_requests:5m >= 100\)/,
      "Dashboard rule lacks the approved 100-request sufficiency guard: "+record);
  }
  assert.match(browserObserver,/HarborOpsDatabaseUnavailable/,
    "Real firing database-alert label remains mandatory");
  const defaultStack=read("compose.yaml");
  assert.doesNotMatch(defaultStack,/otelcol:|M17__Enabled:|grafana:/);
  assert.doesNotMatch(compose,/0\.0\.0\.0:3301|0\.0\.0\.0:4317/);
  assert.match(compose,/OTEL_SPAN_EVENT_COUNT_LIMIT:\s*"0"/);
  assert.match(compose,/OTEL_SPAN_LINK_COUNT_LIMIT:\s*"0"/);
  assert.match(compose,/telemetry_ingest:\s*\n\s*internal:\s*true/);
  assert.match(compose,/telemetry_monitor:\s*\n\s*internal:\s*true/);
  const collector=read("monitoring/m17/collector.yaml");
  assert.match(collector,/memory_limiter:/);
  assert.match(collector,/attributes\/sanitize:/);
  assert.doesNotMatch(collector,/otlphttp\/cloud|logging\/external|otlp\/external/);
  const alert=read("monitoring/m17/alerts.yaml");
  for (const name of [
    "HarborOpsElevatedApiFailureRate","HarborOpsHighApiP95",
    "HarborOpsDatabaseUnavailable","HarborOpsTrackingBacklogStale",
    "HarborOpsMonitoringMissing","HarborOpsExporterDrops",
    "HarborOpsTraceRateDrops"
  ])assert.ok(alert.includes(name),"Required local diagnostic alert missing: "+name);
  const retention=read("monitoring/m17/tempo.yaml");
  assert.match(retention,/block_retention:\s*24h/);
  assert.match(read("scripts/check-g17-performance.mjs"),/p95_increase_pct<=10/);
  assert.match(read("scripts/check-g17-performance.mjs"),/memory_increase_pct<=15/);
  assert.match(read("scripts/check-g17-cardinality-memory.mjs"),/10_000|10000/);
  assert.match(read("tests/HarborOps.ContractTests/M17HttpContractTests.cs"),
    /EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts/);
  assert.match(read("scripts/check-g17-export-privacy.mjs"),/tempo-trace/);
});

test("source diff does not change the accepted default Compose or database migrations",()=>{
  const changed=execFileSync("git",["diff","--name-only",base+"...HEAD"],{
    cwd:root,encoding:"utf8"
  }).trim().split(/\r?\n/).filter(Boolean);
  assert.ok(changed.length>0);
  assert.equal(changed.includes("compose.yaml"),false,
    "The accepted default Compose configuration was unexpectedly modified");
  assert.ok(!changed.some(name=>/Migrations\//i.test(name)),
    "The M17 candidate changed an accepted database migration");
  assert.ok(!changed.some(name=>name.startsWith("docker/postgres/")),
    "The accepted PostgreSQL image was unexpectedly modified");
});
