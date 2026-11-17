import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import {execFileSync} from "node:child_process";

const project=process.env.HARBOROPS_G17_PROJECT;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const source=process.env.HARBOROPS_SOURCE_SHA;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence));
const files=["compose.m17.verify.yaml","compose.m17.observability.override.yaml"];
const on=files.flatMap(f=>["-f",f]);
const off=[...on,"-f","compose.m17.telemetry-off.override.yaml"];
const rows=[];let failure=null;
function compose(args,extra) {
  return execFileSync("docker",["compose","-p",project,...args,
    "--profile","observability",...extra],{
      encoding:"utf8",timeout:120000,maxBuffer:1024*1024}).trim();
}
function mib(raw) {
  const m=raw.match(/^\s*(\d+(?:\.\d+)?)\s*(B|KiB|MiB|GiB|TiB)/i);
  assert.ok(m,"Invalid memory-usage unit");
  return Number(m[1])*({B:1/(1024*1024),KiB:1/1024,MiB:1,GiB:1024,TiB:1024*1024})[m[2]];
}
function apiMemory() {
  const ids=execFileSync("docker",["ps","-q",
    "--filter","label=com.docker.compose.project="+project,
    "--filter","label=com.docker.compose.service=api"],{encoding:"utf8"}).trim()
    .split(/\s+/).filter(Boolean);
  assert.equal(ids.length,1,"Exactly one owned API process required");
  const row=JSON.parse(execFileSync("docker",["stats","--no-stream","--format",
    "{{json .}}",ids[0]],{encoding:"utf8",timeout:13000}).trim());
  return mib(row.MemUsage.split("/")[0]);
}
async function ready() {
  for(let attempt=0;attempt<50;attempt++) {
    try {
      const r=await fetch("http://127.0.0.1:18080/health/ready",
        {signal:AbortSignal.timeout(7000)});
      if(r.status===200)return;
    }catch{}
    await new Promise(resolve=>setTimeout(resolve,1500));
  }
  throw Error("Synthetic API not ready for bounded-memory workload");
}
async function wave(round,enabled) {
  const args=enabled?on:off;
  compose(args,["up","-d","--no-deps","--force-recreate","api"]);
  await ready();
  const before=apiMemory();
  const samples=[before];
  for(let offset=0;offset<10000;offset+=32) {
    const jobs=Array.from({length:Math.min(32,10000-offset)},async(_,j)=>{
      const pathId="g17-cardinality-"+round+"-"+(enabled?"on":"off")+"-"+(offset+j);
      const response=await fetch("http://127.0.0.1:18080/g17-unknown/"+pathId,{
        headers:{"x-correlation-id":"untrusted-"+pathId,
          "x-synthetic-tenant":"unknown-"+pathId},
        signal:AbortSignal.timeout(15000)});
      assert.equal(response.status,404);
    });
    await Promise.all(jobs);
    if(offset%1024===0)samples.push(apiMemory());
  }
  samples.push(apiMemory());
  return {round,telemetry_enabled:enabled,distinct_paths:10000,
    baseline_mib:before,peak_mib:Math.max(...samples),
    ending_mib:samples.at(-1),memory_samples_mib:samples};
}
try {
  for(let round=1;round<=2;round++) {
    // Alternating order minimizes caching bias and uses the same API image,
    // isolated PostgreSQL and host for on/off modes.
    const modes=round===1?[false,true]:[true,false];
    for(const enabled of modes)rows.push(await wave(round,enabled));
  }
  const pairs=[1,2].map(round=>{
    const onRow=rows.find(x=>x.round===round&&x.telemetry_enabled);
    const offRow=rows.find(x=>x.round===round&&!x.telemetry_enabled);
    const relativeIncreasePct=100*(onRow.peak_mib/offRow.peak_mib-1);
    return {round,relative_memory_peak_increase_pct:relativeIncreasePct,
      baseline_off_mib:offRow.peak_mib,telemetry_on_mib:onRow.peak_mib,
      pass:relativeIncreasePct<=15};
  });
  const onRows=rows.filter(x=>x.telemetry_enabled);
  const growthPct=100*(onRows[1].ending_mib/onRows[0].ending_mib-1);
  const bounded=pairs.every(x=>x.pass) &&
    Number.isFinite(growthPct) && growthPct<=15;
  const output={source_sha:source,project,
    run_kind:"FOUR_TEN_THOUSAND_PATH_STRESS_WAVES",
    count_per_wave:10000,total_paths:40000,
    environment:{platform:process.platform,arch:process.arch,
      cpus:os.cpus().length,physical_ram_bytes:os.totalmem()},
    rows,pairs,post_stress_memory_growth_pct:growthPct,
    maximum_allowed_memory_overhead_pct:15,result:bounded?"PASS":"FAIL"};
  fs.writeFileSync(path.join(evidence,"m17-cardinality-memory-results.json"),
    JSON.stringify(output,null,2)+"\n",{flag:"wx"});
  if(!bounded)throw Error("Sustained cardinality memory growth exceeded approved bound");
}catch(e) {
  failure=e;
  if(!fs.existsSync(path.join(evidence,"m17-cardinality-memory-results.json")))
    fs.writeFileSync(path.join(evidence,"m17-cardinality-memory-results.json"),
      JSON.stringify({source_sha:source,project,result:"FAILED_OR_INCOMPLETE",
        completed_waves:rows.length,rows,failure_type:e?.name||"Error"},null,2)+"\n",
      {flag:"wx"});
}finally {
  try {compose(on,["up","-d","--no-deps","--force-recreate","api"]);await ready();}
  catch(e){failure=failure||e;}
}
if(failure)throw Error("G17 sustained hostile-path memory campaign failed");
