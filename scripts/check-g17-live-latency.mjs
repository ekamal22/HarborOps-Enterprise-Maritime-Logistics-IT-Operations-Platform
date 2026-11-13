import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {spawn,execFileSync} from "node:child_process";
import {performance} from "node:perf_hooks";

const project=process.env.HARBOROPS_G17_PROJECT;
const source=process.env.HARBOROPS_SOURCE_SHA;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const password=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence)&&password);
const compose=["compose","-p",project,"-f","compose.m17.verify.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m9.worker.yaml"]:[]),
  "-f","compose.m17.observability.override.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m17.worker.override.yaml"]:[]),
  "--profile","observability"];
const ps=args=>execFileSync("docker",[...compose,...args],{
  encoding:"utf8",timeout:20000,maxBuffer:1024*1024}).trim();
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const proxy="http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-metrics/api/v1/query";
const auth="Basic "+Buffer.from("local_operator:"+password).toString("base64");
const samples=[],checks=[];
let child=null,error=null;
async function metric(expr) {
  const u=new URL(proxy);u.searchParams.set("query",expr);
  const res=await fetch(u,{headers:{authorization:auth},signal:AbortSignal.timeout(12000)});
  assert.equal(res.status,200);const doc=await res.json();
  assert.equal(doc.status,"success");return doc.data.result;
}
async function poll(name,fn,timeout=120000) {
  const end=Date.now()+timeout;
  while(Date.now()<end) {
    try {const val=await fn();if(val)return val;}catch{}
    await sleep(2500);
  }
  throw Error("Missing live latency witness: "+name);
}
async function request() {
  const start=performance.now();
  const response=await fetch("http://127.0.0.1:18080/api/v1/system/baseline",
    {signal:AbortSignal.timeout(28000)});
  assert.equal(response.status,200,"Delayed but valid baseline read must succeed");
  const body=await response.json();
  assert.equal(body.service,"HarborOps");
  samples.push(performance.now()-start);
}
async function stage(name,fn) {
  try {const value=await fn();checks.push({name,status:"PASS",detail:value});}
  catch(e){checks.push({name,status:"FAIL",failure_type:e?.name||"Error"});throw e;}
}
try {
  await stage("control-normal-healthy-read",async()=>{
    const ready=await fetch("http://127.0.0.1:18080/health/ready");
    assert.equal(ready.status,200);
    await request();
    return {baseline_elapsed_ms:samples.at(-1)};
  });
  // Lock a read-only baseline table in this unique project. No table data,
  // migration, or retained business row is modified. The lock is released
  // by the bounded transaction and in the process cleanup path.
  child=spawn("docker",[...compose,"exec","-T","postgres","psql",
    "-U","harborops","-d","harborops","-v","ON_ERROR_STOP=1",
    "-c","BEGIN; LOCK TABLE bootstrap_records IN ACCESS EXCLUSIVE MODE; "+
      "SELECT pg_sleep(15); COMMIT;"],{
      stdio:["ignore","pipe","pipe"]});
  child.stdout.on("data",()=>{});
  child.stderr.on("data",()=>{});
  await stage("owned-lock-causes-real-api-latency",async()=>{
    await poll("owned exclusive test lock",async()=>{
      const value=ps(["exec","-T","postgres","psql","-U","harborops",
        "-d","harborops","-At","-c",
        "SELECT count(*) FROM pg_locks WHERE relation='bootstrap_records'::regclass "+
        "AND mode='AccessExclusiveLock' AND granted"]);
      return Number(value)>=1;
    },12000);
    // First 25 requests block until the synthetic transaction releases.
    await Promise.all(Array.from({length:25},()=>request()));
    // Top up to >100 eligible samples without introducing another fault.
    for(let i=0;i<4;i++)await Promise.all(
      Array.from({length:25},()=>request()));
    const delayed=samples.filter(value=>value>1000);
    assert.ok(delayed.length>=15,
      "Real latency injection did not produce at least 15 slow requests");
    return {measured_requests:samples.length,slow_over_one_second:delayed.length};
  });
  await stage("real-histogram-triggers-p95-alert",async()=>{
    const p95=await poll("real p95 above one second",async()=>{
      const data=await metric("harborops:m17:api_p95_seconds:10m");
      const val=data.length?Number(data[0].value[1]):0;
      return val>1?val:false;
    });
    await poll("high latency alert firing",async()=>{
      const data=await metric('ALERTS{alertname="HarborOpsHighApiP95",alertstate="firing"}');
      return data.length>0;
    });
    return {real_p95_seconds:p95,alert:"HarborOpsHighApiP95"};
  });
} catch(e){error=e;}
finally {
  if(child && child.exitCode===null)child.kill("SIGTERM");
  try {
    await poll("business readiness after owned SQL lock",async()=>{
      const r=await fetch("http://127.0.0.1:18080/health/ready",
        {signal:AbortSignal.timeout(9000)});
      return r.status===200;
    },70000);
  }catch(e){error=error||e;}
}
const result=!error&&checks.length===3&&checks.every(x=>x.status==="PASS")?"PASS":"FAIL";
fs.writeFileSync(path.join(evidence,"m17-live-latency-alert-results.json"),
  JSON.stringify({source_sha:source,project,result,checks,
    request_count:samples.length,slow_requests:samples.filter(x=>x>1000).length,
    lock_scope:"OWNED_DISPOSABLE_BOOTSTRAP_READ_ONLY",accepted_assertions:0,
    failure_type:error?.name||null},null,2)+"\n",{flag:"wx"});
if(result!=="PASS")throw Error("G17 live API p95 histogram alert fault failed");
