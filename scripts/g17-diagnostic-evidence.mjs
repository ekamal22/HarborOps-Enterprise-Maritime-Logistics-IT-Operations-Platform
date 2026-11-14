import assert from "node:assert/strict";
export function verifyBoundedDiagnosticJourney(data) {
 assert.equal(data.result,"PASS");
 assert.equal(data.page_originated,true);
 assert.equal(data.browser_engine,"chromium");
 assert.equal(data.response_status,503);
 assert.equal(data.error_code,"dependency_unavailable");
 assert.match(data.server_correlation??"",/^[a-f0-9]{32}$/);
 assert.match(data.trace_id??"",/^[a-f0-9]{32}$/);
 assert.notEqual(data.server_correlation,data.trace_id);
 assert.equal(data.trace_correlation_reconciled,true);
 const spans=(data.trace_document?.trace?.resourceSpans??
  data.trace_document?.resourceSpans??[]).flatMap(r=>
    (r.scopeSpans??[]).flatMap(scope=>scope.spans??[]));
 assert.ok(spans.length>=2,"Real exported HTTP and database spans required");
 assert.ok(spans.some(s=>s.name==="http.request"),
   "No real HTTP server span");
 assert.ok(spans.some(s=>(s.attributes??[]).some(a=>
   a.key==="harborops.operation" &&
   (a.value?.stringValue??a.value?.string_value)==="baseline_read")),
   "No real same-trace baseline database span");
 assert.match(data.sanitized_log_line??"",/dependency is unavailable/i);
 assert.ok(data.sanitized_log_line.includes(data.server_correlation));
 assert.ok(data.sanitized_log_line.includes(data.trace_id));
 assert.equal(data.alert_name,"HarborOpsDatabaseUnavailable");
 assert.equal(data.alert_firing,true);
 assert.equal(data.dashboard_rendered,true);
 assert.match(data.dashboard_dom_text??"",/HarborOpsDatabaseUnavailable/);
 assert.ok(!/Bearer\s+[a-z0-9._~-]{16,}|password\s*[:=]/i.test(
   data.sanitized_log_line+data.dashboard_dom_text));
 return true;
}
