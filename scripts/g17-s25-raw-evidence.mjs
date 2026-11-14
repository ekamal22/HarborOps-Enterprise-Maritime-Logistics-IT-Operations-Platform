import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import {verifyMalformedBaselineTrace} from "./g17-malformed-trace-evidence.mjs";

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
  }
  return {status:"PASS",raw_trace_records:trace.records.length,
    raw_worker_traces:wt.traces.length,raw_metric_series:metric.series.length,
    malformed_traces:mt.records.length,rendered_engines:3};
}
