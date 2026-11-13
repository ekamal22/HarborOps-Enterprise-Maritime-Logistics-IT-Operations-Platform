import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import {performance} from "node:perf_hooks";
import {execFileSync} from "node:child_process";

const project=process.env.HARBOROPS_G17_PROJECT;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const source=process.env.HARBOROPS_SOURCE_SHA;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[a-f0-9]{40}$/);
assert.ok(evidence && fs.existsSync(evidence),"A source-locked evidence root is required");
const api="http://127.0.0.1:18080";
const files=["compose.m17.verify.yaml","compose.m17.observability.override.yaml"];
const on=files.flatMap(x=>["-f",x]);
const off=[...on,"-f","compose.m17.telemetry-off.override.yaml"];
const recordPath=path.join(evidence,"m17-performance-raw.json");
const rows=[];
let fail=null;
function compose(overlay,cmd,timeout=120000) {
  return execFileSync("docker",["compose","-p",project,...overlay,
    "--profile","observability",...cmd],{
      encoding:"utf8",timeout,maxBuffer:2*1024*1024
    }).trim();
}
const parseMib = value=>{
  const found=value.match(/^\s*(\d+(?:\.\d+)?)\s*(B|KiB|MiB|GiB|TiB)\b/i);
  assert.ok(found,"Docker memory sample did not include a recognized unit");
  return Number(found[1])*({B:1/(1024*1024),KiB:1/1024,MiB:1,GiB:1024,TiB:1024*1024})[found[2]];
};
function memory() {
  const ids=execFileSync("docker",["ps","-q",
    "--filter","label=com.docker.compose.project="+project,
    "--filter","label=com.docker.compose.service=api"],{encoding:"utf8"}).trim()
      .split(/\s+/).filter(Boolean);
  assert.equal(ids.length,1,"Exactly one owned API must exist");
  const sample=JSON.parse(execFileSync("docker",["stats","--no-stream",
    "--format","{{json .}}",ids[0]],{encoding:"utf8",timeout:15000}).trim());
  return parseMib(sample.MemUsage.split("/")[0]);
}
async function ready() {
  for (let i=0;i<50;i++) {
    try {
      const r=await fetch(api+"/health/ready",{signal:AbortSignal.timeout(5000)});
      if(r.status===200)return;
    }catch { /* restart window */ }
    await new Promise(resolve=>setTimeout(resolve,1500));
  }
  throw Error("API did not become ready after controlled profile switch");
}
async function batch(total, concurrency) {
  const samples=[];let completed=0;
  for(let offset=0;offset<total;offset+=concurrency) {
    const jobs=Array.from({length:Math.min(concurrency,total-offset)},async()=>{
      const started=performance.now();
      const response=await fetch(api+"/api/v1/system/baseline",
        {signal:AbortSignal.timeout(15000)});
      const json=await response.json();
      assert.equal(response.status,200,"A bounded baseline read did not succeed");
      assert.equal(json.service,"HarborOps");
      completed++;
      return performance.now()-started;
    });
    samples.push(...await Promise.all(jobs));
  }
  assert.equal(completed,total);
  return samples;
}
function p95(samples) {
  const ordered=[...samples].sort((a,b)=>a-b);
  return ordered[Math.ceil(ordered.length*0.95)-1];
}
async function mode(enabled,round) {
  const overlay=enabled?on:off;
  compose(overlay,["up","-d","--no-deps","--force-recreate","api"]);
  await ready();
  // Warmup excluded. No traffic mutations or tenant data changes.
  await batch(80,8);
  const rss=[memory()];
  const latencies=[];
  for(let k=0;k<5;k++) {
    latencies.push(...await batch(80,8));
    rss.push(memory());
  }
  assert.equal(latencies.length,400);
  return {round,telemetry_enabled:enabled,requests:400,
    p95_ms:p95(latencies),rss_peak_mib:Math.max(...rss),
    rss_samples_mib:rss,mean_ms:latencies.reduce((a,b)=>a+b,0)/latencies.length};
}
function sd(values) {
  const mean=values.reduce((a,b)=>a+b,0)/values.length;
  return Math.sqrt(values.reduce((sum,x)=>sum+(x-mean)**2,0)/values.length);
}
try {
  for(let round=1;round<=3;round++) {
    const order=round===2?[true,false]:[false,true];
    for(const enabled of order)rows.push(await mode(enabled,round));
  }
  const pairs=[1,2,3].map(round=>{
    const offRow=rows.find(x=>x.round===round&&!x.telemetry_enabled);
    const onRow=rows.find(x=>x.round===round&&x.telemetry_enabled);
    const p95_increase_pct=100*(onRow.p95_ms/offRow.p95_ms-1);
    const memory_increase_pct=100*(onRow.rss_peak_mib/offRow.rss_peak_mib-1);
    return {round,telemetry_off_p95_ms:offRow.p95_ms,
      telemetry_on_p95_ms:onRow.p95_ms,p95_increase_pct,
      telemetry_off_rss_mib:offRow.rss_peak_mib,
      telemetry_on_rss_mib:onRow.rss_peak_mib,memory_increase_pct,
      pass:p95_increase_pct<=10&&memory_increase_pct<=15};
  });
  const p95Values=pairs.map(x=>x.p95_increase_pct);
  const rssValues=pairs.map(x=>x.memory_increase_pct);
  const pass=pairs.every(x=>x.pass);
  fs.writeFileSync(recordPath,JSON.stringify({
    source_sha:source,project,run_kind:"THREE_MATCHED_PAIRS",
    benchmark_path:"GET /api/v1/system/baseline",
    sample_size_per_mode_per_round:400,warmup_per_mode_per_round:80,
    concurrency:8,system:{platform:process.platform,arch:process.arch,
      cpus:os.cpus().length,memory_bytes:os.totalmem(),release:os.release(),
      node:process.version},rows,pairs,
    p95_increase_stddev_pct:sd(p95Values),
    rss_increase_stddev_pct:sd(rssValues),
    thresholds:{p95_increase_pct:10,memory_increase_pct:15},
    result:pass?"PASS":"FAIL",
    assessment_boundary:"Single baseline-read workload; other workload families still require separate evidence"
  },null,2)+"\n",{flag:"wx"});
  if(!pass)throw Error("Approved P09 baseline-read overhead thresholds were exceeded");
}catch(e){
  fail=e;
  if(!fs.existsSync(recordPath))
    fs.writeFileSync(recordPath,JSON.stringify({source_sha:source,project,
      result:"FAILED_OR_INCOMPLETE",completed_modes:rows.length,
      rows,error_type:e?.name||"Error"},null,2)+"\n",{flag:"wx"});
}finally{
  try {compose(on,["up","-d","--no-deps","--force-recreate","api"]);await ready();}
  catch(e){fail=fail||e;}
}
if(fail)throw Error("Paired G17 workload failed: "+fail.name);
