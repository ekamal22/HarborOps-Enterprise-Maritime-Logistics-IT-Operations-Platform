import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import crypto from "node:crypto";
import {verifyS25RawSnapshots} from "./g17-s25-raw-evidence.mjs";
const source="a".repeat(40),project="ho-verify-m17-evidence-test";
const span={startTimeUnixNano:"1700000000500000000",
 attributes:[{key:"harborops.operation",value:{stringValue:"baseline_read"}}]};
const doc={trace:{resourceSpans:[{scopeSpans:[{spans:[span]}]}]}};
const digest=s=>crypto.createHash("sha256").update(s).digest("hex");
const put=(dir,name,x)=>fs.writeFileSync(path.join(dir,name),
 typeof x==="string"?x:JSON.stringify(x));
function fixture(dir) {
 const wrap=x=>({source_sha:source,project,...x});
 const logs="synthetic checked service output";
 const series=[{__name__:"harborops_db_readiness_ratio",service:"api"}];
 const traceDoc={trace:{resourceSpans:[{scopeSpans:[{spans:[
  {name:"http.request",attributes:[]},
  {name:"harborops.operation",attributes:[{key:"harborops.operation",
    value:{stringValue:"baseline_read"}}]}
 ]}]}]}};
 const pageFault=wrap({page_originated:true,browser_engine:"chromium",
  observations:[{trace_id:"b".repeat(32),correlation_id:"a".repeat(32),
   body_correlation:"a".repeat(32),status:503},
    ...Array.from({length:95},()=>({trace_id:"c".repeat(32),
      correlation_id:"d".repeat(32),body_correlation:"d".repeat(32),status:503}))]});
 put(dir,"m17-page-fault-injection.json",pageFault);
 put(dir,"m17-page-fault-dashboard.json",wrap({rendered:true,
  dashboard_alert:"HarborOpsDatabaseUnavailable",dashboard_panel_id:8,
  dashboard_panel_type:"table",dashboard_target_format:"table",
  dashboard_query:'ALERTS{alertstate="firing"}',alert_panel_text_scoped:true,
  visible_dom_text:"Active local alerts HarborOpsDatabaseUnavailable"}));
 put(dir,"m17-diagnostic-journey-results.json",wrap({result:"PASS",
  page_originated:true,browser_engine:"chromium",response_status:503,
  error_code:"dependency_unavailable",server_correlation:"a".repeat(32),
  trace_id:"b".repeat(32),trace_correlation_reconciled:true,
  trace_document:traceDoc,
  sanitized_log_line:"dependency is unavailable Correlation "+ "a".repeat(32)+
   " Trace "+ "b".repeat(32),alert_name:"HarborOpsDatabaseUnavailable",
  alert_firing:true,dashboard_rendered:true,
  dashboard_dom_text:"Active local alerts HarborOpsDatabaseUnavailable"}));
 put(dir,"m17-export-privacy-results.json",wrap({result:"PASS",traces_inspected:1,
  retained_trace_documents:1,spans_inspected:1,
  exported_metric_series_inspected:1,retained_metric_series:1,
  retained_log_bytes:Buffer.byteLength(logs),snapshots:[
   {kind:"tempo-trace",sha256:digest(JSON.stringify(doc))},
   {kind:"owned-sanitized-service-logs",sha256:digest(logs)},
   {kind:"prometheus-series",sha256:digest(JSON.stringify(series[0]))}
  ]}));
 put(dir,"m17-export-privacy-traces.json",wrap({records:[{trace_id:"abcdef",document:doc}]}));
 put(dir,"m17-export-privacy-series.json",wrap({series}));
 put(dir,"m17-export-privacy-logs.txt",logs);
 put(dir,"m17-worker-journey-results.json",wrap({result:"PASS",retained_metric_series:1,
  exported_metric_series_checked_for_tenant_privacy:1,retained_trace_documents:1,
  worker_log_bytes:Buffer.byteLength("worker-1 | Tracking work polling started\n"),
  worker_log_sha256:digest("worker-1 | Tracking work polling started\n"),
  worker_log_checked_values:4,worker_log_positive_signal:true}));
 put(dir,"m17-worker-journey-traces.json",wrap({traces:[{document:doc}]}));
 put(dir,"m17-worker-journey-series.json",wrap({series}));
 const workerLogs="worker-1 | Tracking work polling started\n";
 put(dir,"m17-worker-journey-logs.txt",workerLogs);
 put(dir,"m17-malformed-export-results.json",wrap({result:"PASS",injected_requests:960,
  validated_exported_traces:4,trace_records_retained:4,negative_canary_absent:true}));
 put(dir,"m17-malformed-export-traces.json",wrap({window_start:1700000000,
  window_end:1700000001,records:Array.from({length:4},()=>({document:doc}))}));
 const history={
  m8_receipts:1,m8_events:1,m8_work_items:1,m11_exceptions:1,
  m11_history:1,m15_outbox:1,m16_audit:1
 };
 put(dir,"m17-storage-results.json",wrap({result:"PASS",
  positive_history:{tables:history},database_sha256_before:"a".repeat(64),
  database_sha256_after:"a".repeat(64),
  telemetry_degradation:{signal:"OWNED_PROMETHEUS_STORAGE_ENOSPC",
    error_observed:true}}));
 put(dir,"m17-performance-raw.json",wrap({gate:{pairs_over_p95_budget:0}}));
 put(dir,"m17-p09-independent-recalculation.json",wrap({result:"PASS",
  method:"POOLED_P95_AND_MEAN_PEAK_MEMORY_OVER_THREE_MATCHED_PAIRS",
  latency_budget_pct:10,memory_budget_pct:15,pairs:[{},{},{}],
  individual_p95_outliers:[]}));
 for(const engine of ["chromium","firefox","webkit"]) {
  put(dir,"m17-ui-state-"+engine+".json",{source_sha:source,engine,
   verified_statuses:{missing:"PostgreSQL readiness Unknown",
    down:"PostgreSQL readiness Unavailable",ready:"PostgreSQL readiness Ready"}});
  put(dir,"m17-ui-no-traffic-"+engine+".json",{source_sha:source,engine,
   window_start:1700000000,window_end:1700000300,
   source_result_counts:{eligible_requests:0,failure_fraction:0},
   panels:{"API request rate":"No data","API 5xx rate":"No data",
    "API p95 latency":"No data"}});
 }
}
test("retained real-shaped raw evidence is independently rechecked",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-test-"));
 try {fixture(dir);assert.equal(verifyS25RawSnapshots(dir,source,project).status,"PASS");}
 finally {fs.rmSync(dir,{recursive:true,force:true});}
});
test("changed trace, missing series and false browser state are rejected",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-negative-"));
 try {
  fixture(dir);
  const traces=JSON.parse(fs.readFileSync(path.join(dir,"m17-export-privacy-traces.json")));
  traces.records[0].document.trace.resourceSpans[0].scopeSpans[0].spans[0]
   .attributes.push({key:"http.request.header.cookie",value:{stringValue:"secret"}});
  put(dir,"m17-export-privacy-traces.json",traces);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  fs.unlinkSync(path.join(dir,"m17-worker-journey-series.json"));
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  const f="m17-ui-state-webkit.json";
  const ui=JSON.parse(fs.readFileSync(path.join(dir,f)));
  ui.verified_statuses.down="PostgreSQL readiness Ready";
  put(dir,f,ui);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  // A matching log digest must not excuse a credential in retained raw bytes.
  // This isolates the privacy detector from the independent hash-integrity gate.
  for(const injected of [
    "Bearer abcdefghijklmnopqrstuvwxyz0123456789",
    "Password: syntheticLongCredentialValue123456"
  ]) {
    fixture(dir);
    const raw="worker-1 | Tracking work polling started\n"+injected;
    const witness=JSON.parse(fs.readFileSync(
      path.join(dir,"m17-worker-journey-results.json"),"utf8"));
    witness.worker_log_bytes=Buffer.byteLength(raw);
    witness.worker_log_sha256=digest(raw);
    put(dir,"m17-worker-journey-results.json",witness);
    put(dir,"m17-worker-journey-logs.txt",raw);
    assert.throws(()=>verifyS25RawSnapshots(dir,source,project),
      /regular expression|expected.*not.*match/i);
  }
  fixture(dir);
  const diagnosticPath="m17-diagnostic-journey-results.json";
  const diagnostic=JSON.parse(fs.readFileSync(path.join(dir,diagnosticPath)));
  diagnostic.server_correlation="f".repeat(32);
  put(dir,diagnosticPath,diagnostic);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  const displayPath="m17-page-fault-dashboard.json";
  const display=JSON.parse(fs.readFileSync(path.join(dir,displayPath)));
  display.dashboard_target_format="time_series";
  put(dir,displayPath,display);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  const storagePath="m17-storage-results.json";
  const storage=JSON.parse(fs.readFileSync(path.join(dir,storagePath),"utf8"));
  storage.positive_history.tables.m8_receipts=0;
  put(dir,storagePath,storage);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),
    /positive retained business witness/);
  fixture(dir);
  const bad="m17-ui-no-traffic-chromium.json";
  const traffic=JSON.parse(fs.readFileSync(path.join(dir,bad)));
  traffic.panels["API 5xx rate"]="0 healthy";
  put(dir,bad,traffic);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
 } finally {fs.rmSync(dir,{recursive:true,force:true});}
});
