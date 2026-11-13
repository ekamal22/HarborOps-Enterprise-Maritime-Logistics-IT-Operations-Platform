import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {execFileSync} from "node:child_process";

const project=process.env.HARBOROPS_G17_PROJECT;
const root=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const source=process.env.HARBOROPS_SOURCE_SHA;
const password=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[a-f0-9]{40}$/);
assert.ok(root&&fs.existsSync(root),"Fresh G17 evidence directory required");
assert.ok(password,"Disposable dashboard credentials required");
const compose=["compose","-p",project,"-f","compose.m17.verify.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m9.worker.yaml"]:[]),
  "-f","compose.m17.observability.override.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m17.worker.override.yaml"]:[]),
  "--profile","observability",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["--profile","worker"]:[])];
const base="http://127.0.0.1:18080";
const reportFile=path.join(root,"m17-collector-fault-results.json");
const checks=[],measurements=[];
const command=args=>execFileSync("docker",[...compose,...args],{
  encoding:"utf8",timeout:60000,maxBuffer:2*1024*1024}).trim();
const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function health(uri,expected) {
  const r=await fetch(base+uri,{signal:AbortSignal.timeout(9000)});
  assert.equal(r.status,expected,uri+" changed response during collector fault");
  return await r.json();
}
async function prometheus(expression) {
  const u=new URL("http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-metrics/api/v1/query");
  u.searchParams.set("query",expression);
  const reply=await fetch(u,{headers:{
    authorization:"Basic "+Buffer.from("local_operator:"+password).toString("base64")
  },signal:AbortSignal.timeout(12000)});
  assert.equal(reply.status,200,"Prometheus proxy returned a non-200 response");
  const body=await reply.json();
  assert.equal(body.status,"success");
  return body.data.result;
}
async function poll(name,predicate,maxMs=130000) {
  const until=Date.now()+maxMs;
  while(Date.now()<until) {
    try {const value=await predicate();if(value)return value;}
    catch { /* instrumentation may still be starting; deadline still applies */ }
    await wait(3000);
  }
  throw Error("Timed out waiting for "+name);
}
async function step(name,action) {
  const started=new Date().toISOString();
  try {
    const result=await action();
    checks.push({name,result:"PASS",started,result_summary:result??null});
  }catch(e) {
    checks.push({name,result:"FAIL",started,error_type:e?.name||"Error"});
    throw e;
  }
}
async function burst(count,concurrency) {
  let passed=0;
  for(let offset=0;offset<count;offset+=concurrency) {
    const rs=await Promise.all(
      Array.from({length:Math.min(concurrency,count-offset)},()=>health(
        "/api/v1/system/baseline",200)));
    for(const body of rs) {
      assert.equal(body.service,"HarborOps");
      passed++;
    }
  }
  assert.equal(passed,count);
  return count;
}
async function readyState() {
  assert.equal((await health("/health/live",200)).status,"healthy");
  assert.equal((await health("/health/ready",200)).status,"healthy");
  return "business liveness/readiness preserved";
}
let collectorStopped=false,failed=false;
try {
  await step("baseline-good",async()=>{
    await readyState();
    await poll("collector scrape baseline",async()=>{
      const xs=await prometheus('up{job="m17_collector"}');
      return xs.length===1 && Number(xs[0].value[1])===1;
    });
    return await burst(12,4);
  });
  command(["stop","otelcol"]);
  collectorStopped=true;
  await step("collector-scrape-reports-outage",async()=>{
    await poll("collector scrape down",async()=>{
      const xs=await prometheus('up{job="m17_collector"}');
      return xs.length===1 && Number(xs[0].value[1])===0;
    });
    await poll("missing-telemetry alert",async()=>{
      const xs=await prometheus('ALERTS{alertname="HarborOpsMonitoringMissing",alertstate="firing"}');
      return xs.length>0;
    });
    return "collector down and local alert firing";
  });
  await step("collector-fault-business-isolation",async()=>{
    const n=await burst(4000,80);
    await readyState();
    measurements.push({at:new Date().toISOString(),requests_completed:n,
      concurrency:80,baseline_concurrency:8,concurrency_multiplier:10});
    return "completed "+n+" real database reads during tenfold parallel load while exporter unreachable";
  });
} catch(e) {
  failed=true;
} finally {
  if(collectorStopped) {
    try {command(["start","otelcol"]);collectorStopped=false;}
    catch {failed=true;checks.push({name:"collector-restore",result:"FAIL"});}
  }
}
if(!collectorStopped) {
  try {
    await step("collector-recovery-automatic",async()=>{
      await readyState();
      await poll("collector scrape resumes",async()=>{
        const xs=await prometheus('up{job="m17_collector"}');
        return xs.length===1&&Number(xs[0].value[1])===1;
      });
      await poll("telemetry resumes after exporter failure",async()=>{
        const xs=await prometheus('increase(harborops_telemetry_heartbeat_total[2m])');
        return xs.length>0&&Number(xs[0].value[1])>0;
      });
      await poll("exporter losses observed after recovery",async()=>{
        const xs=await prometheus('sum(increase(harborops_telemetry_dropped_total{signal="trace",reason="exporter_failure"}[5m]))');
        return xs.length>0&&Number(xs[0].value[1])>0;
      });
      return await burst(12,4);
    });
  }catch{failed=true;}
}
const report={source_sha:source,project,checks,measurements,
  result:!failed&&!collectorStopped&&checks.length===4&&checks.every(x=>x.result==="PASS")?"PASS":"FAIL",
  accepted_assertions:0,collector_restored:!collectorStopped};
fs.writeFileSync(reportFile,JSON.stringify(report,null,2)+"\n",{flag:"wx"});
if(report.result!=="PASS")throw Error("G17 collector fault or recovery witness failed");
