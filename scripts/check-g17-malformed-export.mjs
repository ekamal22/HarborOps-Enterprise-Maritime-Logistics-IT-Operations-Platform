import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {verifyMalformedBaselineTrace} from "./g17-malformed-trace-evidence.mjs";

const project=process.env.HARBOROPS_G17_PROJECT;
const sha=process.env.HARBOROPS_SOURCE_SHA;
const root=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const password=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(project??"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(sha??"",/^[a-f0-9]{40}$/);
assert.ok(root&&fs.existsSync(root)&&password);
const tag="malformed-g17-"+crypto.randomBytes(12).toString("hex");
const auth="Basic "+Buffer.from("local_operator:"+password).toString("base64");
const base="http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-traces";
const headers={authorization:auth,accept:"application/json"};
const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const fetchJson=async url=>{
  const response=await fetch(url,{headers,signal:AbortSignal.timeout(16000)});
  assert.equal(response.status,200,"Private Tempo query failed");
  return response.json();
};
let status="FAIL",failure=null,requests=0,validated=0;
const inspected=[],matched=new Set();
const started=Math.floor(Date.now()/1000);
let ended=started;
try {
  // Deliberately isolate this stage: only malformed-parent baseline requests
  // are generated between the two source-time boundaries.
  for(let offset=0;offset<960;offset+=24) {
    const batch=await Promise.all(Array.from({length:Math.min(24,960-offset)},async()=>{
      const url=new URL("http://127.0.0.1:18080/api/v1/system/baseline");
      url.searchParams.set("g17-private",tag);
      const response=await fetch(url,{headers:{
        traceparent:"invalid-"+tag,tracestate:"privatevendor="+tag,
        baggage:"sensitive="+tag+";"+("x".repeat(4000)),
        cookie:"g17="+tag
      },signal:AbortSignal.timeout(15000)});
      assert.equal(response.status,200,"Malformed trace context changed business response");
      assert.equal((await response.json()).service,"HarborOps");
      return true;
    }));
    requests+=batch.length;
  }
  ended=Math.floor(Date.now()/1000)+1;
  assert.equal(requests,960);
  const deadline=Date.now()+180000;
  while(Date.now()<deadline && matched.size<4) {
    const url=new URL(base+"/api/search");
    url.searchParams.set("tags","service.name=harborops-api");
    url.searchParams.set("start",String(started));
    url.searchParams.set("end",String(ended+45));
    url.searchParams.set("limit","1000");
    try {
      const search=await fetchJson(url);
      for(const row of search.traces??[]) {
        const id=row.traceID;
        if(!/^[a-f0-9]{16,32}$/i.test(id??"")||matched.has(id))continue;
        const doc=await fetchJson(base+"/api/v2/traces/"+id);
        if(!verifyMalformedBaselineTrace(doc,started,ended,tag))continue;
        matched.add(id);
        inspected.push({trace_id:id,document:doc});
        if(matched.size>=24)break;
      }
    } catch(error) {
      if(error?.name==="AssertionError")throw error;
    }
    if(matched.size<4)await wait(3500);
  }
  validated=matched.size;
  assert.ok(validated>=4,
    "Four real server-created malformed-parent baseline traces required");
  status="PASS";
} catch(error) {failure=error;}
finally {
  if(status==="PASS") {
    fs.writeFileSync(path.join(root,"m17-malformed-export-traces.json"),
      JSON.stringify({source_sha:sha,project,window_start:started,
        window_end:ended,records:inspected},null,2)+"\n",{flag:"wx"});
  }
  fs.writeFileSync(path.join(root,"m17-malformed-export-results.json"),
    JSON.stringify({source_sha:sha,project,result:status,
      scenario:"malformed-context-export-safe",injected_requests:requests,
      malformed_trace_context:true,validated_exported_traces:validated,
      trace_records_retained:inspected.length,
      negative_canary_absent:status==="PASS",
      failure_type:failure?.name??null,
      failure_message:failure?String(failure.message).slice(0,240):null
    },null,2)+"\n",{flag:"wx"});
}
if(status!=="PASS")throw Error("Malformed context export witness failed");
