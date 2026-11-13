import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {execFileSync} from "node:child_process";
import {performance} from "node:perf_hooks";

const project=process.env.HARBOROPS_G17_PROJECT;
const sha=process.env.HARBOROPS_SOURCE_SHA;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(sha||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence));
const root="http://127.0.0.1:18080";
const args=["compose","-p",project,"-f","compose.m17.verify.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m9.worker.yaml"]:[]),
  "-f","compose.m17.observability.override.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m17.worker.override.yaml"]:[]),
  "--profile","observability",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["--profile","worker"]:[])];
const docker=cmd=>execFileSync("docker",[...args,...cmd],{
  encoding:"utf8",timeout:45000,maxBuffer:1024*1024}).trim();
const checks=[];let stopped=false;let failure=false;
const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function check(name,fn) {
  try {
    const detail=await fn();
    checks.push({name,status:"PASS",detail});
  } catch(err) {
    checks.push({name,status:"FAIL",error_type:err?.name||"Error"});
    failure=true;throw err;
  }
}
async function status(uri,code=200) {
  const r=await fetch(root+uri,{signal:AbortSignal.timeout(8000)});
  assert.equal(r.status,code,uri+" returned unexpected status");
  return await r.json();
}
async function ready() {
  for(let k=0;k<50;k++) {
    try {
      const body=await status("/health/ready");
      if(body.status==="healthy")return true;
    } catch {}
    await wait(1200);
  }
  throw Error("Restarted API was not ready within bounded recovery time");
}
function databaseFootprint() {
  const sql="SELECT (SELECT count(*) FROM tracking_inbox_work_items)::text || ',' || "+
    "(SELECT count(*) FROM tracking_processing_effects)::text || ',' || "+
    "(SELECT count(*) FROM tracking_inbox_receipts)::text";
  return docker(["exec","-T","postgres","psql","-U","harborops",
    "-d","harborops","-At","-v","ON_ERROR_STOP=1","-c",sql]).trim();
}
let before;
try {
  await check("pre-restart-synthetic-baseline",async()=>{
    await ready();
    before=databaseFootprint();
    assert.equal((await status("/api/v1/system/baseline")).service,"HarborOps");
    return {durable_counts:before};
  });
  await check("real-client-abort-does-not-commit-business",async()=>{
    const operations=Array.from({length:80},async(_,i)=>{
      const controller=new AbortController();
      const timer=setTimeout(()=>controller.abort(),i%2?2:6);
      try {
        const res=await fetch(root+"/api/v1/system/baseline",
          {signal:controller.signal});
        assert.equal(res.status,200);
        return "completed";
      }catch(err) {
        assert.ok(err.name==="AbortError"||err.name==="TimeoutError",
          "Unexpected network exception during deliberate cancellation");
        return "canceled";
      }finally{clearTimeout(timer);}
    });
    const cases=await Promise.all(operations);
    assert.ok(cases.includes("canceled"),"Cancellation injector did not cancel a request");
    assert.equal(databaseFootprint(),before,"Cancellation changed durable business state");
    return {total:cases.length,canceled:cases.filter(x=>x==="canceled").length};
  });
  await check("owned-api-shutdown-bounded",async()=>{
    const started=performance.now();
    docker(["stop","-t","10","api"]);
    stopped=true;
    const elapsed=(performance.now()-started)/1000;
    assert.ok(elapsed<=20,"Telemetry shutdown exceeded the bounded stop interval");
    const states=docker(["ps","-a","api","--format","{{.State}}"]);
    assert.ok(states==="exited","API was not cleanly stopped");
    return {shutdown_seconds:elapsed};
  });
} catch {failure=true;}
finally {
  if(stopped) {
    try {docker(["start","api"]);stopped=false;}
    catch {failure=true;checks.push({name:"owned-api-restart",status:"FAIL"});}
  }
}
if(!stopped) {
  try {
    await check("restart-recovers-health-without-business-duplication",async()=>{
      await ready();
      assert.equal((await status("/health/live")).status,"healthy");
      assert.equal((await status("/api/v1/system/baseline")).service,"HarborOps");
      assert.equal(databaseFootprint(),before,
        "Restart created a duplicate receipt, lease or business effect");
      return {durable_counts_after:databaseFootprint()};
    });
  }catch {failure=true;}
}
const report={source_sha:sha,project,result:!failure&&!stopped&&checks.length===4&&
  checks.every(x=>x.status==="PASS")?"PASS":"FAIL",
  checks,api_restored:!stopped,assertion_acceptance:"PENDING"};
fs.writeFileSync(path.join(evidence,"m17-restart-results.json"),
  JSON.stringify(report,null,2)+"\n",{flag:"wx"});
if(report.result!=="PASS")throw Error("G17 restart, cancellation or shutdown bound failed");
