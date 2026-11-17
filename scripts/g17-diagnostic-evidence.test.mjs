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

test("every field of the page-to-alert journey is individually load-bearing",()=>{
 const spans=x=>x.trace_document.trace.resourceSpans[0].scopeSpans[0].spans;
 const single={
  "no http.request span":x=>{spans(x).splice(0,1);},
  "operation span with another name":x=>{spans(x)[1].attributes[0].value.stringValue="other_read";},
  "operation attribute under another key":x=>{spans(x)[1].attributes[0].key="harborops.other";},
  "browser engine not chromium":x=>{x.browser_engine="firefox";},
  "response status not 503":x=>{x.response_status=500;},
  "error code not dependency_unavailable":x=>{x.error_code="internal_error";},
  "server correlation not 32 hex":x=>{x.server_correlation="short";x.sanitized_log_line="TraceId "+x.trace_id+" CorrelationId short dependency is unavailable";},
  "trace id equals correlation":x=>{x.trace_id=x.server_correlation;x.sanitized_log_line="TraceId "+x.trace_id+" dependency is unavailable";},
  "log lacks the trace id":x=>{x.sanitized_log_line="CorrelationId "+x.server_correlation+" dependency is unavailable";},
  "log lacks the dependency text":x=>{x.sanitized_log_line="TraceId "+x.trace_id+" CorrelationId "+x.server_correlation;},
  "alert named differently":x=>{x.alert_name="HarborOpsHighApiP95";},
  "dashboard not rendered":x=>{x.dashboard_rendered=false;},
  "credential in the log":x=>{x.sanitized_log_line+=" Bearer abcdefghijklmnopqrstuvwxyz";},
  "password in the dashboard text":x=>{x.dashboard_dom_text+=" password=hunter2hunter2";},
  "result not PASS":x=>{x.result="FAIL";},
  "single span only":x=>{spans(x).pop();spans(x).push({name:"http.request",attributes:[]});spans(x).splice(1);}
 };
 for(const [label,mutate] of Object.entries(single)) {
  const x=structuredClone(record);mutate(x);
  assert.throws(()=>verifyBoundedDiagnosticJourney(x),/./,label);
 }
});

test("a trace with two spans but no http.request server span is rejected", () => {
 const x = structuredClone(record);
 x.trace_document.trace.resourceSpans[0].scopeSpans[0].spans[0].name = "internal.work";
 assert.throws(() => verifyBoundedDiagnosticJourney(x), /No real HTTP server span/);
});
