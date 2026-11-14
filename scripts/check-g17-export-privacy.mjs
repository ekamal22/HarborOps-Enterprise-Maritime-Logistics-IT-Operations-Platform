import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {execFileSync} from "node:child_process";

const project=process.env.HARBOROPS_G17_PROJECT;
const source=process.env.HARBOROPS_SOURCE_SHA;
const root=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const password=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[a-f0-9]{40}$/);
assert.ok(root&&fs.existsSync(root)&&password);
const tag="g17-private-canary-"+crypto.randomBytes(12).toString("hex");
const auditCanary=tag+"-audit";
const cookies=tag+"-cookie";
const baggage=tag+"-baggage";
const query=tag+"-query";
const rejects=[tag,auditCanary,cookies,baggage,query,
  "http.request.header.cookie","http.request.header.authorization",
  "db.statement","db.query.text","exception.stacktrace","exception.message",
  "tracking_inbox_receipts","audit_entries","user.email","session.cookie"];
const base="http://127.0.0.1:3301/api/datasources/proxy/uid/";
const auth="Basic "+Buffer.from("local_operator:"+password).toString("base64");
const fetchJson=async url=>{
  const r=await fetch(url,{headers:{authorization:auth,accept:"application/json"},
    signal:AbortSignal.timeout(13000)});
  assert.equal(r.status,200,"Private Tempo/Prometheus proxy returned "+r.status);
  return await r.json();
};
const promiseWait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const results=[];
const generatedTraceIds=new Set();
const retainedTraces=[];
let retainedLogs="";
let retainedSeries=[];
function scan(raw,kind) {
  const value=typeof raw==="string"?raw:JSON.stringify(raw);
  for(const secret of rejects)
    assert.ok(!value.toLowerCase().includes(secret.toLowerCase()),
      kind+" exported forbidden canary or sensitive attribute "+secret.split("-")[0]);
  results.push({kind,bytes:value.length,sha256:crypto.createHash("sha256")
    .update(value).digest("hex")});
}
async function generateRealRequests(count) {
  let successful=0;
  for(let offset=0;offset<count;offset+=24) {
    const rsp=await Promise.all(
      Array.from({length:Math.min(24,count-offset)},async(_,j)=>{
        const req=new URL("http://127.0.0.1:18080/api/v1/system/baseline");
        req.searchParams.set("auditTag",auditCanary);
        req.searchParams.set("untrusted",query);
        const validTrace=(offset+j)%2!==0;
        if(!validTrace)injectedControls.malformed_traceparent_requests++;
        const injectedTraceId=validTrace?crypto.randomBytes(16).toString("hex"):null;
        if(injectedTraceId)generatedTraceIds.add(injectedTraceId);
        const response=await fetch(req,{
          headers:{
            cookie:"g17="+cookies,
            baggage:"secret="+baggage+";"+("x".repeat(4000)),
            "x-g17-secret":tag,
            "traceparent":validTrace ?
              "00-"+injectedTraceId+"-"+crypto.randomBytes(8).toString("hex")+"-01" :
              "invalid-"+tag,
            "tracestate":"privatevendor="+tag
          },signal:AbortSignal.timeout(12000)});
        assert.equal(response.status,200,"Synthetic baseline read must succeed");
        injectedControls.requests++;
        const json=await response.json();
        assert.equal(json.service,"HarborOps");
        assert.ok(!JSON.stringify(json).includes(tag),
          "Untrusted header or query appeared in the business response");
        return true;
      }));
    successful+=rsp.length;
  }
  assert.equal(successful,count);
  injectedControls.oversized_baggage=true;
  injectedControls.all_business_responses_200=true;
}
let failure=null,observedTraces=0,exportedSpans=0,seriesCount=0;
const injectedControls={oversized_baggage:false,requests:0,
  malformed_traceparent_requests:0,all_business_responses_200:false};
try {
  await generateRealRequests(1920);
  let traces=[];
  const deadline=Date.now()+210000;
  // A bounded search over a backend that already holds thousands of traces returns an
  // arbitrary subset, so the submitted canary traces are retrieved by their own IDs.
  const lookupHeaders={authorization:auth,accept:"application/json"};
  async function retrieveSubmitted() {
    const found=[];
    for(const id of generatedTraceIds) {
      const response=await fetch(base+"harborops-local-traces/api/v2/traces/"+id,
        {headers:lookupHeaders,signal:AbortSignal.timeout(13000)});
      if(response.status===404)continue;
      assert.equal(response.status,200,"Private Tempo proxy returned "+response.status);
      // Tempo answers 200 with an empty {trace:{}} for a trace it has not stored,
      // either because it was not sampled or because it is still in flight.
      const stored=(await response.json()).trace?.resourceSpans;
      if(!Array.isArray(stored)||stored.length===0)continue;
      found.push({traceID:id});
      if(found.length>=40)break;
    }
    return found;
  }
  while(Date.now()<deadline) {
    try {
      const found=await retrieveSubmitted();
      if(found.length>=4){traces=found;break;}
    } catch(e) {
      // Startup gaps are bounded by the deadline; no pass if traces never appear.
    }
    await promiseWait(4000);
  }
  assert.ok(traces.length>=4,
    "At least four real submitted canary trace IDs must be retrieved from Tempo");
  for(const trace of traces.slice(0,40)) {
    assert.match(trace.traceID||"",/^[0-9a-f]{16,32}$/i);
    const doc=await fetchJson(base+"harborops-local-traces/api/v2/traces/"+trace.traceID);
    const raw=JSON.stringify(doc);
    scan(raw,"tempo-trace");
    assert.ok(raw.includes("harborops-api")||raw.includes("harborops.operation")||
      raw.includes("http.request"),
      "Trace contained no permitted diagnostic component");
    // Tempo 2.x wraps the OTLP document as {trace:{resourceSpans:[...]}}.
    const resourceSpans=doc.trace?.resourceSpans??doc.resourceSpans;
    const spans=resourceSpans?.flatMap(r=>r.scopeSpans?.flatMap(x=>x.spans||[])||[])||
      doc.batches?.flatMap(b=>b.instrumentationLibrarySpans?.flatMap(
        x=>x.spans||[])||[])||[];
    assert.ok(spans.length>0,"A search result contained no serialized spans");
    for(const span of spans) {
      const attributes=span.attributes||[];
      for(const attr of attributes) {
        const key=attr.key||"";
        assert.ok(!/cookie|authorization|password|sql|query|payload|user.id|tenant|organization_id|session|email/i.test(key),
          "Trace contains a forbidden span attribute key");
      }
      assert.ok(!span.traceState,"Trace state must be erased before export");
      assert.ok(!span.events?.length,"Raw span events must not reach OTLP");
      assert.ok(!span.links?.length,"Raw span links must not reach OTLP");
      assert.ok(!span.status?.message,"Error status descriptions must not leak");
    }
    observedTraces++;
    exportedSpans+=spans.length;
    retainedTraces.push({trace_id:trace.traceID,document:doc});
  }
  // Read actual owned service log output. Negative privacy controls are not
  // satisfied by checking trace tags alone.
  const compose=["compose","-p",project,"-f","compose.m17.verify.yaml",
    ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"
      ? ["-f","compose.m9.worker.yaml"]:[]),
    "-f","compose.m17.observability.override.yaml",
    ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"
      ? ["-f","compose.m17.worker.override.yaml"]:[]),
    "--profile","observability"];
  const logs=execFileSync("docker",[...compose,"logs","--no-color",
    "--tail","15000","api","otelcol","tempo"],{
    encoding:"utf8",timeout:30000,maxBuffer:8*1024*1024});
  scan(logs,"owned-sanitized-service-logs");
  retainedLogs=logs;
  const q=new URL(base+"harborops-local-metrics/api/v1/series");
  q.searchParams.set("match[]",'{__name__=~"harborops_.*"}');
  q.searchParams.set("start",String(Math.floor(Date.now()/1000)-600));
  q.searchParams.set("end",String(Math.floor(Date.now()/1000)+60));
  const series=await fetchJson(q);
  assert.equal(series.status,"success");
  assert.ok(Array.isArray(series.data)&&series.data.length>0,
    "No exported custom metric series available for privacy validation");
  seriesCount=series.data.length;
  retainedSeries=series.data;
  for(const labelset of series.data) {
    scan(labelset,"prometheus-series");
    for(const [key,value] of Object.entries(labelset)) {
      assert.ok(!/cookie|authorization|sql|query|tenant|organization_id|user.id|email|path|shipment/i.test(key),
        "Untrusted metric label key left the process");
      assert.ok(String(value).length<=120,"Unexpected unbounded metric label value");
    }
  }
  assert.equal(retainedTraces.length,observedTraces);
  assert.equal(retainedSeries.length,seriesCount);
  // Only inspected synthetic, canary-clean records are retained. No post-scan
  // redaction is allowed because it would hide evidence of an export leak.
  fs.writeFileSync(path.join(root,"m17-export-privacy-traces.json"),
    JSON.stringify({source_sha:source,project,records:retainedTraces},null,2)+"\n",{flag:"wx"});
  fs.writeFileSync(path.join(root,"m17-export-privacy-logs.txt"),
    retainedLogs,{flag:"wx"});
  fs.writeFileSync(path.join(root,"m17-export-privacy-series.json"),
    JSON.stringify({source_sha:source,project,series:retainedSeries},null,2)+"\n",{flag:"wx"});
} catch(e) {
  failure=e;
} finally {
  const record={source_sha:source,project,result:failure?"FAIL":"PASS",
    traces_inspected:observedTraces,spans_inspected:exportedSpans,
    retained_trace_documents:retainedTraces.length,
    retained_metric_series:retainedSeries.length,
    retained_log_bytes:Buffer.byteLength(retainedLogs),
    exported_metric_series_inspected:seriesCount,
    canary_sha256:crypto.createHash("sha256").update(tag).digest("hex"),
    snapshots:results,injected_controls:injectedControls,assertion_review:"PENDING",
    failure_type:failure?.name??null,
    failure_message:failure?String(failure.message).slice(0,240):null};
  fs.writeFileSync(path.join(root,"m17-export-privacy-results.json"),
    JSON.stringify(record,null,2)+"\n",{flag:"wx"});
}
if(failure)throw Error("G17 raw exported telemetry privacy test failed: "+failure.name);
