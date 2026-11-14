import test from "node:test";
import assert from "node:assert/strict";
import {verifyBoundedDiagnosticJourney} from "./g17-diagnostic-evidence.mjs";
const record={result:"PASS",page_originated:true,browser_engine:"chromium",
 response_status:503,error_code:"dependency_unavailable",
 server_correlation:"a".repeat(32),trace_id:"b".repeat(32),
 trace_correlation_reconciled:true,
 trace_document:{trace:{resourceSpans:[{scopeSpans:[{spans:[
  {name:"http.request",attributes:[]},
  {name:"harborops.operation",attributes:[{key:"harborops.operation",
    value:{stringValue:"baseline_read"}}]}
 ]}]}]}},
 sanitized_log_line:"TraceId "+ "b".repeat(32) +" CorrelationId "+ "a".repeat(32) +
 " dependency is unavailable",alert_name:"HarborOpsDatabaseUnavailable",
 alert_firing:true,dashboard_rendered:true,
 dashboard_dom_text:"Active local alerts HarborOpsDatabaseUnavailable"};
test("one real-shaped page fault joins a trace, log and rendered alert",()=>{
 assert.equal(verifyBoundedDiagnosticJourney(record),true);
});
test("unrelated traces, spoofed log, missing UI and report-only passes fail",()=>{
 for(const change of [
  x=>x.page_originated=false,
  x=>x.server_correlation="f".repeat(32),
  x=>x.trace_document.trace.resourceSpans[0].scopeSpans[0].spans.pop(),
  x=>x.dashboard_dom_text="Active local alerts",
  x=>x.alert_firing=false,
  x=>x.trace_correlation_reconciled=false,
  x=>x.sanitized_log_line="unrelated error"
 ]){const x=structuredClone(record);change(x);
  assert.throws(()=>verifyBoundedDiagnosticJourney(x));}
});
