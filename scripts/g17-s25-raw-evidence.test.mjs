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
  exported_metric_series_checked_for_tenant_privacy:1,retained_trace_documents:1}));
 put(dir,"m17-worker-journey-traces.json",wrap({traces:[{document:doc}]}));
 put(dir,"m17-worker-journey-series.json",wrap({series}));
 put(dir,"m17-malformed-export-results.json",wrap({result:"PASS",injected_requests:960,
  validated_exported_traces:4,trace_records_retained:4,negative_canary_absent:true}));
 put(dir,"m17-malformed-export-traces.json",wrap({window_start:1700000000,
  window_end:1700000001,records:Array.from({length:4},()=>({document:doc}))}));
 for(const engine of ["chromium","firefox","webkit"])
  put(dir,"m17-ui-state-"+engine+".json",{source_sha:source,engine,
   verified_statuses:{missing:"PostgreSQL readiness Unknown",
    down:"PostgreSQL readiness Unavailable",ready:"PostgreSQL readiness Ready"}});
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
 } finally {fs.rmSync(dir,{recursive:true,force:true});}
});
