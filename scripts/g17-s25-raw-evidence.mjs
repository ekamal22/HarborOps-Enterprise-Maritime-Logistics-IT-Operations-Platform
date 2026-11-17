import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import {verifyMalformedBaselineTrace} from "./g17-malformed-trace-evidence.mjs";
import {verifyBoundedDiagnosticJourney} from "./g17-diagnostic-evidence.mjs";

const load=(root,name)=>JSON.parse(fs.readFileSync(path.join(root,name),"utf8"));
const sha=value=>crypto.createHash("sha256").update(value).digest("hex");
const rawSha=value=>sha(JSON.stringify(value));
const looksPrivate=/cookie|authorization|password|token|credential|sql|query|session|tenant|organization_id|user\.id|email|shipment|receipt_id/i;
function matchesSource(data,source,project) {
  assert.equal(data.source_sha,source,"Raw privacy source SHA mismatch");
  assert.equal(data.project,project,"Raw privacy project mismatch");
}
function safeTrace(record) {
  const resources=record?.trace?.resourceSpans??record?.resourceSpans??[];
  const spans=resources.flatMap(r=>(r.scopeSpans??[]).flatMap(s=>s.spans??[]));
  assert.ok(spans.length>0,"Retained trace lacks actual exported spans");
  for(const span of spans) {
    for(const attr of span.attributes??[])
      assert.ok(!looksPrivate.test(attr.key??""),"Protected attribute escaped in retained trace");
    assert.ok(!span.events?.length&&!span.links?.length&&
      !span.traceState&&!span.status?.message,
      "Retained trace contains raw events, links, state or status message");
  }
  return spans.length;
}
function safeSeries(series) {
  assert.ok(Array.isArray(series)&&series.length>0);
  for(const row of series) {
    assert.ok(row&&typeof row==="object");
    for(const [key,value] of Object.entries(row)) {
      assert.ok(!looksPrivate.test(key),"Protected metric label escaped");
      assert.ok(String(value).length<=120,"Unbounded metric value");
    }
  }
}
export function verifyS25RawSnapshots(root,source,project) {
  const diagnostic=load(root,"m17-diagnostic-journey-results.json");
  const browserFault=load(root,"m17-page-fault-injection.json");
  const display=load(root,"m17-page-fault-dashboard.json");
  for(const x of [diagnostic,browserFault,display])
    matchesSource(x,source,project);
  assert.equal(browserFault.page_originated,true);
  assert.equal(browserFault.observations?.length,96);
  assert.ok(browserFault.observations.some(x=>
    x.trace_id===diagnostic.trace_id &&
    x.correlation_id===diagnostic.server_correlation &&
    x.status===503 && x.body_correlation===x.correlation_id));
  assert.equal(display.rendered,true);
  assert.equal(display.dashboard_alert,"HarborOpsDatabaseUnavailable");
  assert.equal(display.dashboard_panel_id,8);
  assert.equal(display.dashboard_panel_type,"table");
  assert.equal(display.dashboard_target_format,"table");
  assert.equal(display.dashboard_query,'ALERTS{alertstate="firing"}');
  assert.equal(display.alert_panel_text_scoped,true,
    "Actual browser text must come from the active-alert panel");
  // The label must be a rendered cell of the alert table, in the alertname column
  // with state firing, after the panel was scrolled into the viewport.
  assert.equal(display.scrolled_into_view,true);
  assert.ok(display.viewport?.width>0&&display.viewport?.height>0);
  const nameColumn=Array.isArray(display.table_headers)
    ?display.table_headers.indexOf("alertname"):-1;
  const stateColumn=Array.isArray(display.table_headers)
    ?display.table_headers.indexOf("alertstate"):-1;
  assert.ok(nameColumn>=0&&stateColumn>=0&&Array.isArray(display.table_rows)&&
    display.table_rows.some(cells=>cells[nameColumn]==="HarborOpsDatabaseUnavailable"&&
      cells[stateColumn]==="firing"),
    "Alert name must be a rendered firing table cell in the alert panel");
  assert.equal(display.visible_dom_text,diagnostic.dashboard_dom_text);
  assert.equal(verifyBoundedDiagnosticJourney(diagnostic),true);
  const privacy=load(root,"m17-export-privacy-results.json");
  const trace=load(root,"m17-export-privacy-traces.json");
  const metric=load(root,"m17-export-privacy-series.json");
  const logs=fs.readFileSync(path.join(root,"m17-export-privacy-logs.txt"),"utf8");
  for(const value of [privacy,trace,metric])matchesSource(value,source,project);
  assert.equal(privacy.result,"PASS");
  assert.equal(trace.records.length,privacy.traces_inspected);
  assert.equal(trace.records.length,privacy.retained_trace_documents);
  assert.equal(metric.series.length,privacy.exported_metric_series_inspected);
  assert.equal(metric.series.length,privacy.retained_metric_series);
  assert.equal(Buffer.byteLength(logs),privacy.retained_log_bytes);
  const traceDigests=(privacy.snapshots??[]).filter(x=>x.kind==="tempo-trace");
  assert.equal(traceDigests.length,trace.records.length);
  trace.records.forEach((item,i)=>{
    assert.ok(safeTrace(item.document)>0);
    assert.equal(rawSha(item.document),traceDigests[i].sha256,
      "Retained trace differs from originally inspected bytes");
  });
  const logDigest=(privacy.snapshots??[])
    .find(x=>x.kind==="owned-sanitized-service-logs");
  assert.ok(logDigest);
  // Independent of the script's per-run random canary: its fixed prefix may appear
  // in no retained trace, series or service log byte.
  for(const [kind,body] of [["logs",logs],["traces",JSON.stringify(trace)],
      ["series",JSON.stringify(metric)]])
    assert.doesNotMatch(body,/g17-private-canary-/i,
      "Privacy canary prefix found in retained "+kind);
  assert.equal(sha(logs),logDigest.sha256,
    "Retained service logs differ from originally scanned bytes");
  const metricDigests=(privacy.snapshots??[]).filter(x=>x.kind==="prometheus-series");
  assert.equal(metricDigests.length,metric.series.length);
  safeSeries(metric.series);
  metric.series.forEach((item,i)=>
    assert.equal(rawSha(item),metricDigests[i].sha256,
      "Retained series differs from originally scanned bytes"));

  const worker=load(root,"m17-worker-journey-results.json");
  const ws=load(root,"m17-worker-journey-series.json");
  const wt=load(root,"m17-worker-journey-traces.json");
  for(const value of [worker,ws,wt])matchesSource(value,source,project);
  assert.equal(worker.result,"PASS");
  assert.equal(worker.retained_metric_series,ws.series.length);
  assert.equal(worker.exported_metric_series_checked_for_tenant_privacy,ws.series.length);
  assert.equal(worker.retained_trace_documents,wt.traces.length);
  safeSeries(ws.series);
  assert.ok(wt.traces.length>0);
  wt.traces.forEach(item=>safeTrace(item.document));
  // Positive participation: retained traces must come from the worker service, and
  // the retained worker metric series must carry the tracking worker identity.
  assert.ok(wt.traces.filter(x=>x.service==="harborops-tracking-worker").length>=2,
    "Fewer than two retained traces exported by the participating worker service");
  assert.ok(ws.series.some(row=>/^harborops_worker_/.test(row.__name__??"")&&
    row.worker==="tracking"),
    "No retained worker metric series for the participating tracking worker");
  const workerLogs=fs.readFileSync(path.join(root,"m17-worker-journey-logs.txt"),"utf8");
  assert.ok(workerLogs.length>0);
  assert.match(workerLogs,/Tracking work polling started|Tracking worker disposition/);
  assert.equal(Buffer.byteLength(workerLogs),worker.worker_log_bytes);
  assert.equal(sha(workerLogs),worker.worker_log_sha256);
  assert.ok(worker.worker_log_checked_values>=4);
  assert.equal(worker.worker_log_positive_signal,true);
  assert.doesNotMatch(workerLogs,/\bBearer\s+[A-Za-z0-9._~-]{16,}|\bPassword\s*[:=]\s*\S{8,}/i);
  // Shapes of the synthetic private material the script injects, checked without
  // trusting the script's own canary list: source credential, account and tenant slugs.
  assert.doesNotMatch(workerLogs,/hop_v1_[0-9a-f]{32}_|@example\.test|g17-worker-[0-9a-f]{6,}/i,
    "Worker log contains synthetic credential, account or tenant shaped material");

  const malformed=load(root,"m17-malformed-export-results.json");
  const mt=load(root,"m17-malformed-export-traces.json");
  for(const value of [malformed,mt])matchesSource(value,source,project);
  assert.equal(malformed.result,"PASS");
  assert.equal(malformed.injected_requests,960);
  assert.ok(malformed.validated_exported_traces>=4);
  assert.equal(malformed.validated_exported_traces,mt.records.length);
  assert.equal(malformed.trace_records_retained,mt.records.length);
  assert.equal(malformed.negative_canary_absent,true);
  for(const item of mt.records)
    assert.equal(verifyMalformedBaselineTrace(item.document,mt.window_start,
      mt.window_end,"unrelated-negative-control"),true);

  const storage=load(root,"m17-storage-results.json");
  matchesSource(storage,source,project);
  assert.equal(storage.result,"PASS");
  assert.match(storage.database_sha256_before??"",/^[0-9a-f]{64}$/);
  assert.equal(storage.database_sha256_after,storage.database_sha256_before,
    "Full owned business history changed under telemetry-storage pressure");
  for(const name of ["m8_receipts","m8_events","m8_work_items",
    "m11_exceptions","m11_history","m15_outbox","m16_audit"]) {
    assert.ok(Number.isInteger(storage.positive_history?.tables?.[name]) &&
      storage.positive_history.tables[name]>0,
      "No positive retained business witness: "+name);
  }
  assert.equal(storage.telemetry_degradation?.signal,
    "OWNED_PROMETHEUS_STORAGE_ENOSPC");
  assert.equal(storage.telemetry_degradation?.error_observed,true);
  // The degradation is a measured change of the observation path, not only a failed
  // write by the test: logged by the store, stale samples, an Unknown readiness expression.
  const degradation=storage.telemetry_degradation;
  assert.equal(degradation.storage_error_logged,true,"Owned store logged no storage error");
  assert.equal(degradation.readiness_panel_expression_unknown,true,
    "Readiness stayed current during the full telemetry store");
  assert.ok(degradation.prometheus_unreachable===true||degradation.stale_age_seconds>=45,
    "No stale-sample measurement during the full telemetry store");
  assert.ok(degradation.fresh_before_seconds<45,"No healthy-observation control before the fault");
  assert.equal(storage.telemetry_recovery?.recovered,true,"Observation did not recover");
  assert.ok((storage.checks??[]).some(c=>c.name==="telemetry-observation-recovers-after-fault"&&
    c.status==="PASS"));
  // Row-level identity of every protected fixture record, before and after.
  assert.deepEqual(storage.fixture_rows_after,storage.fixture_rows_before,
    "A retained M8/M11/M15/M16 fixture row changed under telemetry-storage pressure");
  for(const name of ["m8_receipts","m8_events","m8_work_items","m11_exceptions",
      "m11_history","m15_outbox","m16_audit"])
    assert.ok(storage.fixture_rows_before?.[name]?.rows>0&&
      /^[0-9a-f]{64}$/.test(storage.fixture_rows_before[name].sha256??""),
      "No row-level hash for protected history: "+name);

  const performance=load(root,"m17-performance-raw.json");
  const independently=load(root,"m17-p09-independent-recalculation.json");
  matchesSource(performance,source,project);
  matchesSource(independently,source,project);
  assert.equal(independently.result,"PASS");
  assert.equal(independently.method,
    "POOLED_P95_AND_MEAN_PEAK_MEMORY_OVER_THREE_MATCHED_PAIRS");
  assert.equal(independently.latency_budget_pct,10);
  assert.equal(independently.memory_budget_pct,15);
  assert.equal(independently.pairs.length,3);
  assert.equal(independently.individual_p95_outliers.length,
    performance.gate.pairs_over_p95_budget);
  const lowWindow=load(root,"m17-low-traffic-window.json");
  matchesSource(lowWindow,source,project);
  assert.equal(lowWindow.result,"PASS");
  assert.ok(lowWindow.requests_sent>0&&lowWindow.requests_sent<lowWindow.sufficiency_floor,
    "Below-threshold control must send fewer than the approved eligible-request floor");
  assert.equal(lowWindow.sufficiency_floor,100);
  assert.ok(lowWindow.eligible_min>=1&&lowWindow.eligible_max<100,
    "Window is not real below-threshold traffic");
  assert.ok(lowWindow.eligible_points>=4);
  assert.equal(lowWindow.guarded_5xx_points,0);
  assert.equal(lowWindow.guarded_p95_points,0);
  assert.ok(lowWindow.raw_5xx_zero_points>=1,
    "The raw healthy-looking zero hazard was not demonstrated");
  assert.ok(lowWindow.request_rate_max>0);
  for(const engine of ["chromium","firefox","webkit"]) {
    const state=load(root,"m17-ui-state-"+engine+".json");
    assert.equal(state.source_sha,source);
    assert.equal(state.engine,engine);
    assert.match(state.verified_statuses?.missing??"",/\b(?:Unknown|No data)\b/i);
    assert.match(state.verified_statuses?.down??"",/\bUnavailable\b/i);
    assert.match(state.verified_statuses?.ready??"",/\bReady\b/i);
    const traffic=load(root,"m17-ui-no-traffic-"+engine+".json");
    assert.equal(traffic.source_sha,source);
    assert.equal(traffic.engine,engine);
    assert.equal(traffic.source_result_counts.eligible_requests,0);
    assert.equal(traffic.source_result_counts.failure_fraction,0);
    assert.ok(traffic.window_end<Math.floor(Date.now()/1000));
    for(const title of ["API request rate","API 5xx rate","API p95 latency"]) {
      assert.match(traffic.panels?.[title]??"",/\b(?:No data|Unknown)\b/i,
        "No independent rendered no-traffic API panel: "+engine+"/"+title);
      assert.doesNotMatch(traffic.panels[title],/\b(?:Healthy|Sufficient)\b/i);
    }
    const low=traffic.low_traffic;
    assert.ok(low,"No rendered below-threshold traffic evidence: "+engine);
    assert.equal(low.window_start,lowWindow.window_start);
    assert.equal(low.window_end,lowWindow.window_end);
    assert.equal(low.requests_sent,lowWindow.requests_sent);
    assert.equal(low.raw_5xx_value_at_midpoint,0,
      "The independent raw 5xx re-query must be the healthy-looking zero");
    assert.equal(low.guarded_series_at_midpoint,0);
    assert.match(low.panels?.["API request rate"]??"",/\d(?:\.\d+)?\s*req\/s/,
      "Measured low request rate was not rendered: "+engine);
    assert.doesNotMatch(low.panels["API request rate"],/\b(?:No data|Unknown)\b/i);
    for(const title of ["API 5xx rate","API p95 latency"]) {
      assert.match(low.panels?.[title]??"",/\bUnknown\b/i,
        "Below-threshold traffic was not rendered Unknown: "+engine+"/"+title);
      assert.doesNotMatch(low.panels[title],
        /\b(?:Healthy|Sufficient)\b|\b0(?:\.0+)?\s*(?:req\/s|ms|s)\b/i,
        "False healthy value rendered under insufficient traffic: "+engine+"/"+title);
    }
  }
  return {status:"PASS",raw_trace_records:trace.records.length,
    raw_worker_traces:wt.traces.length,raw_metric_series:metric.series.length,
    malformed_traces:mt.records.length,rendered_engines:3};
}
