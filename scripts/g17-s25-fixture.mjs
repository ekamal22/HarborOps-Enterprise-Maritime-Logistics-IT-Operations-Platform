import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
// Real-shaped raw evidence for the S25 verifier, shared by its tests and the full-campaign tests.
export const source="a".repeat(40),project="ho-verify-m17-evidence-test";
const span={startTimeUnixNano:"1700000000500000000",
 attributes:[{key:"harborops.operation",value:{stringValue:"baseline_read"}}]};
export const doc={trace:{resourceSpans:[{scopeSpans:[{spans:[span]}]}]}};
export const digest=s=>crypto.createHash("sha256").update(s).digest("hex");
export const put=(dir,name,x)=>fs.writeFileSync(path.join(dir,name),
 typeof x==="string"?x:JSON.stringify(x));
export function writeS25Fixture(dir) {
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
