import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {execFileSync} from "node:child_process";
import {verifyBoundedDiagnosticJourney} from "./g17-diagnostic-evidence.mjs";
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const source=process.env.HARBOROPS_SOURCE_SHA;
const project=process.env.HARBOROPS_G17_PROJECT;
const pass=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(source??"",/^[a-f0-9]{40}$/);
assert.match(project??"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.ok(evidence&&fs.existsSync(evidence)&&pass);
const read=file=>JSON.parse(fs.readFileSync(path.join(evidence,file),"utf8"));
const injection=read("m17-page-fault-injection.json");
const rendered=read("m17-page-fault-dashboard.json");
for(const x of [injection,rendered]){
 assert.equal(x.source_sha,source);assert.equal(x.project,project);
}
assert.equal(injection.page_originated,true);
assert.equal(injection.observations.length,96);
const auth="Basic "+Buffer.from("local_operator:"+pass).toString("base64");
async function tempo(id) {
 const u="http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-traces/api/v2/traces/"+id;
 const response=await fetch(u,{headers:{authorization:auth},
   signal:AbortSignal.timeout(12000)});
 if(response.status===404)return null;
 assert.equal(response.status,200);
 const doc=await response.json();
 return doc.trace?.resourceSpans?.length?doc:null;
}
async function alert() {
 const url=new URL("http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-metrics/api/v1/query");
 url.searchParams.set("query",
   'ALERTS{alertname="HarborOpsDatabaseUnavailable",alertstate="firing"}');
 const rsp=await fetch(url,{headers:{authorization:auth},
   signal:AbortSignal.timeout(12000)});
 assert.equal(rsp.status,200);
 const body=await rsp.json();
 return body.data?.result?.length===1;
}
const compose=["compose","-p",project,"-f","compose.m17.verify.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"
    ? ["-f","compose.m9.worker.yaml"]:[]),
  "-f","compose.m17.observability.override.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"
    ? ["-f","compose.m17.worker.override.yaml"]:[]),
  "--profile","observability"];
const logs=execFileSync("docker",[...compose,"logs","--no-color",
 "--tail","15000","api"],{encoding:"utf8",maxBuffer:8*1024*1024,timeout:35000});
const lines=logs.split("\n");
let found=null;
const deadline=Date.now()+110000;
while(Date.now()<deadline&&!found){
 for(const item of injection.observations) {
  if(!lines.some(line=>line.includes(item.correlation_id)&&
     line.includes(item.trace_id)&&line.includes("dependency is unavailable")))
   continue;
  const doc=await tempo(item.trace_id);
  if(!doc)continue;
  const candidate={source_sha:source,project,result:"PASS",
    page_originated:true,browser_engine:injection.browser_engine,
    response_status:item.status,error_code:item.error_code,
    server_correlation:item.correlation_id,trace_id:item.trace_id,
    trace_correlation_reconciled:true,trace_document:doc,
    sanitized_log_line:lines.find(line=>line.includes(item.correlation_id)&&
      line.includes(item.trace_id)&&line.includes("dependency is unavailable")),
    alert_name:rendered.dashboard_alert,alert_firing:await alert(),
    dashboard_rendered:rendered.rendered,dashboard_dom_text:rendered.visible_dom_text};
  try {verifyBoundedDiagnosticJourney(candidate);found=candidate;break;}
  catch {}
 }
 if(!found)await new Promise(resolve=>setTimeout(resolve,3500));
}
assert.ok(found,"No same-request page, API, Tempo, log and alert journey");
fs.writeFileSync(path.join(evidence,"m17-diagnostic-journey-results.json"),
 JSON.stringify(found,null,2)+"\n",{flag:"wx"});
