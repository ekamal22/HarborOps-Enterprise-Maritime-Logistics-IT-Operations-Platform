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
  scrolled_into_view:true,viewport:{width:1280,height:720},
  table_headers:["Time","__name__","alertname","alertstate"],
  table_rows:[["2026-11-14 14:46:32","ALERTS","HarborOpsDatabaseUnavailable","firing"]],
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
  exported_metric_series_checked_for_tenant_privacy:1,retained_trace_documents:3,
  worker_log_bytes:Buffer.byteLength("worker-1 | Tracking work polling started\n"),
  worker_log_sha256:digest("worker-1 | Tracking work polling started\n"),
  worker_log_checked_values:4,worker_log_positive_signal:true}));
 put(dir,"m17-worker-journey-traces.json",wrap({traces:[
  {service:"harborops-api",document:doc},
  {service:"harborops-tracking-worker",document:doc},
  {service:"harborops-tracking-worker",document:doc}]}));
 put(dir,"m17-worker-journey-series.json",wrap({series:[
  {__name__:"harborops_worker_attempts_total",worker:"tracking",outcome:"reconciliation_required"}]}));
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
    error_observed:true,storage_error_logged:true,readiness_panel_expression_unknown:true,
    stale_age_seconds:75,prometheus_unreachable:false,fresh_before_seconds:8},
  telemetry_recovery:{recovered:true,how:"released-space"},
  checks:[{name:"telemetry-observation-recovers-after-fault",status:"PASS"}],
  fixture_rows_before:Object.fromEntries(["m8_receipts","m8_events","m8_work_items",
   "m11_exceptions","m11_history","m15_outbox","m16_audit"].map(n=>
   [n,{rows:1,sha256:"e".repeat(64),keys:["k"]}])),
  fixture_rows_after:Object.fromEntries(["m8_receipts","m8_events","m8_work_items",
   "m11_exceptions","m11_history","m15_outbox","m16_audit"].map(n=>
   [n,{rows:1,sha256:"e".repeat(64),keys:["k"]}]))}));
 put(dir,"m17-performance-raw.json",wrap({gate:{pairs_over_p95_budget:0}}));
 put(dir,"m17-p09-independent-recalculation.json",wrap({result:"PASS",
  method:"POOLED_P95_AND_MEAN_PEAK_MEMORY_OVER_THREE_MATCHED_PAIRS",
  latency_budget_pct:10,memory_budget_pct:15,pairs:[{},{},{}],
  individual_p95_outliers:[]}));
 put(dir,"m17-low-traffic-window.json",wrap({result:"PASS",requests_sent:32,
  window_start:1700000100,window_end:1700000200,eligible_points:6,eligible_min:8,
  eligible_max:30,sufficiency_floor:100,guarded_5xx_points:0,guarded_p95_points:0,
  raw_5xx_zero_points:6,request_rate_max:0.1}));
 for(const engine of ["chromium","firefox","webkit"]) {
  put(dir,"m17-ui-state-"+engine+".json",{source_sha:source,engine,
   verified_statuses:{missing:"PostgreSQL readiness Unknown",
    down:"PostgreSQL readiness Unavailable",ready:"PostgreSQL readiness Ready"}});
  put(dir,"m17-ui-no-traffic-"+engine+".json",{source_sha:source,engine,
   window_start:1700000000,window_end:1700000300,
   source_result_counts:{eligible_requests:0,failure_fraction:0},
   panels:{"API request rate":"No data","API 5xx rate":"No data",
    "API p95 latency":"No data"},
   low_traffic:{window_start:1700000100,window_end:1700000200,requests_sent:32,
    raw_5xx_value_at_midpoint:0,guarded_series_at_midpoint:0,
    panels:{"API request rate":"API request rate 0.0831 req/s",
     "API 5xx rate":"API 5xx rate Unknown","API p95 latency":"API p95 latency Unknown"}}});
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
test("below-threshold real-traffic rendering rejects false healthy values and forged windows",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-low-"));
 const edit=(name,fn)=>{const p=path.join(dir,name);const v=JSON.parse(fs.readFileSync(p,"utf8"));fn(v);put(dir,name,v);};
 try {
  fixture(dir);
  edit("m17-ui-no-traffic-firefox.json",v=>{v.low_traffic.panels["API 5xx rate"]="API 5xx rate 0 req/s";});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/False healthy value|not rendered Unknown/);
  fixture(dir);
  edit("m17-ui-no-traffic-webkit.json",v=>{v.low_traffic.panels["API p95 latency"]="API p95 latency 0 s";});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/not rendered Unknown|False healthy value/);
  fixture(dir);
  edit("m17-ui-no-traffic-chromium.json",v=>{v.low_traffic.panels["API request rate"]="API request rate Unknown";});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/Measured low request rate/);
  fixture(dir);
  edit("m17-ui-no-traffic-chromium.json",v=>{delete v.low_traffic;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/No rendered below-threshold/);
  fixture(dir);
  edit("m17-ui-no-traffic-firefox.json",v=>{v.low_traffic.window_end+=60;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  edit("m17-low-traffic-window.json",v=>{v.requests_sent=140;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/fewer than the approved/);
  fixture(dir);
  edit("m17-low-traffic-window.json",v=>{v.eligible_max=140;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/not real below-threshold/);
  fixture(dir);
  edit("m17-low-traffic-window.json",v=>{v.raw_5xx_zero_points=0;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/healthy-looking zero hazard/);
  fixture(dir);
  fs.unlinkSync(path.join(dir,"m17-low-traffic-window.json"));
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
 } finally {fs.rmSync(dir,{recursive:true,force:true});}
});
test("alert table cells, worker participation and independent privacy shapes are enforced",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-f02-f03-"));
 const edit=(name,fn)=>{const p=path.join(dir,name);const v=JSON.parse(fs.readFileSync(p,"utf8"));fn(v);put(dir,name,v);};
 try {
  fixture(dir);
  // The alert name appears in loose page text but not as an alertname firing cell.
  edit("m17-page-fault-dashboard.json",v=>{v.table_rows=[["x","HarborOpsDatabaseUnavailable","ALERTS","firing"]];});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/rendered firing table cell/);
  fixture(dir);
  edit("m17-page-fault-dashboard.json",v=>{v.table_rows=[["x","ALERTS","HarborOpsDatabaseUnavailable","pending"]];});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/rendered firing table cell/);
  fixture(dir);
  edit("m17-page-fault-dashboard.json",v=>{v.scrolled_into_view=false;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  // Only the API exported traces: the participating worker is not demonstrated.
  edit("m17-worker-journey-traces.json",v=>{v.traces=v.traces.map(x=>({...x,service:"harborops-api"}));});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/participating worker service/);
  fixture(dir);
  edit("m17-worker-journey-series.json",v=>{v.series=[{__name__:"harborops_db_readiness_ratio",service:"api"}];});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/tracking worker/);
  // Hash-consistent worker log injections of credential-, account- and tenant-shaped values.
  for(const injected of [
    "source token hop_v1_"+"a".repeat(32)+"_abcdefghijklmnopqrstu",
    "account g17-ab12cd34@example.test",
    "tenant g17-worker-0123456789ab"
  ]) {
   fixture(dir);
   const raw="worker-1 | Tracking work polling started\n"+injected;
   edit("m17-worker-journey-results.json",v=>{v.worker_log_bytes=Buffer.byteLength(raw);
     v.worker_log_sha256=digest(raw);});
   put(dir,"m17-worker-journey-logs.txt",raw);
   assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/synthetic credential, account or tenant/);
  }
  // A canary prefix in a hash-consistent retained service log.
  fixture(dir);
  const logs="api-1 | g17-private-canary-0123456789abcdef";
  edit("m17-export-privacy-results.json",v=>{v.retained_log_bytes=Buffer.byteLength(logs);
    v.snapshots.find(x=>x.kind==="owned-sanitized-service-logs").sha256=digest(logs);});
  put(dir,"m17-export-privacy-logs.txt",logs);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/canary prefix/);
 } finally {fs.rmSync(dir,{recursive:true,force:true});}
});
test("F08 storage evidence requires measured degradation, recovery and row-level identity",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-f08-"));
 const edit=(fn)=>{const p=path.join(dir,"m17-storage-results.json");
  const v=JSON.parse(fs.readFileSync(p,"utf8"));fn(v);put(dir,"m17-storage-results.json",v);};
 const cases=[
  [v=>{v.telemetry_degradation.storage_error_logged=false;},/logged no storage error/],
  [v=>{v.telemetry_degradation.readiness_panel_expression_unknown=false;},/Readiness stayed current/],
  [v=>{v.telemetry_degradation.stale_age_seconds=5;},/No stale-sample measurement/],
  [v=>{v.telemetry_degradation.fresh_before_seconds=90;},/healthy-observation control/],
  [v=>{v.telemetry_recovery.recovered=false;},/did not recover/],
  [v=>{v.fixture_rows_after.m15_outbox.sha256="f".repeat(64);},/fixture row changed/],
  [v=>{delete v.fixture_rows_before.m8_work_items;delete v.fixture_rows_after.m8_work_items;},/No row-level hash/],
  [v=>{v.fixture_rows_before.m16_audit.rows=0;v.fixture_rows_after.m16_audit.rows=0;},/No row-level hash/]
 ];
 try {
  for(const [mutate,pattern] of cases) {
   fixture(dir);edit(mutate);
   assert.throws(()=>verifyS25RawSnapshots(dir,source,project),pattern);
  }
 } finally {fs.rmSync(dir,{recursive:true,force:true});}
});
