import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import {performance} from "node:perf_hooks";
import {execFileSync,spawnSync} from "node:child_process";
import crypto from "node:crypto";

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
const nonce=crypto.randomBytes(5).toString("hex");
let managerCookie="",authorizedPath="";
function disposableCli(args,input) {
  const res=spawnSync("docker",["compose","-p",project,...on,
    "--profile","observability",...args],{
    input,encoding:"utf8",timeout:120000,maxBuffer:1024*1024});
  assert.equal(res.status,0,"Synthetic account/organization provisioning failed");
  return res.stdout.trim();
}
function sql(query) {
  return disposableCli(["exec","-T","postgres","psql","-U","harborops",
    "-d","harborops","-At","-v","ON_ERROR_STOP=1","-c",query]);
}
class Session {
  jar=new Map();
  cookie() {return [...this.jar].map(([key,value])=>key+"="+value).join("; ");}
  async send(method,route,body,csrf=true) {
    const headers={Origin:api};
    if(this.jar.size)headers.Cookie=this.cookie();
    if(csrf&&method!=="GET")headers["X-HarborOps-CSRF"]=await this.token();
    if(body!==undefined)headers["Content-Type"]="application/json";
    const res=await fetch(api+route,{method,headers,
      body:body===undefined?undefined:JSON.stringify(body),
      signal:AbortSignal.timeout(14000)});
    for(const cookie of res.headers.getSetCookie?.()||[]) {
      const pair=cookie.split(";")[0], index=pair.indexOf("=");
      if(index>0)this.jar.set(pair.slice(0,index),pair.slice(index+1));
    }
    const text=await res.text();let json=null;
    try{json=JSON.parse(text);}catch{}
    return {status:res.status,json,text};
  }
  async token() {
    const headers={Origin:api};
    if(this.jar.size)headers.Cookie=this.cookie();
    const res=await fetch(api+"/api/v1/auth/csrf",
      {headers,signal:AbortSignal.timeout(14000)});
    assert.equal(res.status,200);
    for(const cookie of res.headers.getSetCookie?.()||[]) {
      const pair=cookie.split(";")[0],index=pair.indexOf("=");
      if(index>0)this.jar.set(pair.slice(0,index),pair.slice(index+1));
    }
    return (await res.json()).token;
  }
}
async function seedAuthorizedRead() {
  const adminEmail="g17-bench-admin-"+nonce+"@example.test";
  const managerEmail="g17-bench-manager-"+nonce+"@example.test";
  const pw=crypto.randomBytes(24).toString("base64url");
  for(const email of [adminEmail,managerEmail])
    disposableCli(["run","--rm","-T","-e",
      "HARBOROPS_ALLOW_ACCOUNT_CLI=true","api",
      "--account-cli","provision",email,"G17 Bench User"],pw+"\n");
  const slug="g17-bench-"+nonce;
  disposableCli(["run","--rm","-T","-e",
    "HARBOROPS_ALLOW_ORGANIZATION_CLI=true","api",
    "--organization-cli","bootstrap",slug,"G17 Benchmark Organization",adminEmail]);
  const org=sql("SELECT id FROM organizations WHERE slug='"+slug+"'");
  const managerId=sql("SELECT id FROM users WHERE email='"+managerEmail+"'");
  assert.match(org,/^[0-9a-f-]{36}$/);
  assert.match(managerId,/^[0-9a-f-]{36}$/);
  const admin=new Session();
  const login=await admin.send("POST","/api/v1/auth/login",
    {email:adminEmail,password:pw});
  assert.equal(login.status,200,"Synthetic administrator must log in");
  const selected=await admin.send("PUT","/api/v1/auth/session/organization",
    {organizationId:org});
  assert.ok([200,409].includes(selected.status));
  const membership=await admin.send("PUT",
    "/api/v1/organizations/"+org+"/memberships/"+managerId,
    {role:"Manager",status:"Active"});
  assert.equal(membership.status,200,"Authorized synthetic membership required");
  const manager=new Session();
  assert.equal((await manager.send("POST","/api/v1/auth/login",
    {email:managerEmail,password:pw})).status,200);
  assert.ok([200,409].includes((await manager.send("PUT",
    "/api/v1/auth/session/organization",{organizationId:org})).status));
  managerCookie=manager.cookie();
  authorizedPath="/api/v1/organizations/"+org+"/ports";
  const check=await fetch(api+authorizedPath,{headers:{Cookie:managerCookie},
    signal:AbortSignal.timeout(12000)});
  assert.equal(check.status,200,"Benchmark Manager cannot read scoped reference data");
}

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
async function batch(total, concurrency, kind) {
  const samples=[];let completed=0;
  const route=kind==="authorized" ? authorizedPath : "/api/v1/system/baseline";
  const headers=kind==="authorized" ? {Cookie:managerCookie} : {};
  assert.ok(route&&["authorized","baseline"].includes(kind));
  for(let offset=0;offset<total;offset+=concurrency) {
    const jobs=Array.from({length:Math.min(concurrency,total-offset)},async()=>{
      const started=performance.now();
      const response=await fetch(api+route,{
        headers,signal:AbortSignal.timeout(15000)});
      const json=await response.json();
      assert.equal(response.status,200,"A bounded "+kind+" read did not succeed");
      if(kind==="baseline")assert.equal(json.service,"HarborOps");
      else assert.ok(Array.isArray(json.ports),"Authenticated scoped port response was not returned");
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
  // Both a public database read and a real authenticated organization
  // read are measured on the same API image, tenant and disposable database.
  await batch(80,8,"baseline");
  await batch(80,8,"authorized");
  const rss=[memory()];
  const runs={baseline:[],authorized:[]};
  for(let k=0;k<5;k++) {
    for(const kind of ["baseline","authorized"])
      runs[kind].push(...await batch(80,8,kind));
    rss.push(memory());
  }
  for(const kind of ["baseline","authorized"])assert.equal(runs[kind].length,400);
  const per_route=Object.fromEntries(Object.entries(runs).map(([kind,values])=>[
    kind,{requests:values.length,p95_ms:p95(values),
      mean_ms:values.reduce((a,b)=>a+b,0)/values.length}
  ]));
  return {round,telemetry_enabled:enabled,requests:800,per_route,
    p95_ms:Math.max(...Object.values(per_route).map(x=>x.p95_ms)),
    rss_peak_mib:Math.max(...rss),rss_samples_mib:rss};
}
function sd(values) {
  const mean=values.reduce((a,b)=>a+b,0)/values.length;
  return Math.sqrt(values.reduce((sum,x)=>sum+(x-mean)**2,0)/values.length);
}
try {
  await seedAuthorizedRead();
  assert.ok(managerCookie.length>0 && authorizedPath.startsWith("/api/v1/organizations/"),
    "Both synthetic authenticated credentials and route are required");
  for(let round=1;round<=3;round++) {
    const order=round===2?[true,false]:[false,true];
    for(const enabled of order)rows.push(await mode(enabled,round));
  }
  const pairs=[1,2,3].map(round=>{
    const offRow=rows.find(x=>x.round===round&&!x.telemetry_enabled);
    const onRow=rows.find(x=>x.round===round&&x.telemetry_enabled);
    const route_pairs=Object.fromEntries(["baseline","authorized"].map(kind=>{
      const offP95=offRow.per_route[kind].p95_ms;
      const onP95=onRow.per_route[kind].p95_ms;
      return [kind,{telemetry_off_p95_ms:offP95,
        telemetry_on_p95_ms:onP95,p95_increase_pct:100*(onP95/offP95-1)}];
    }));
    const p95_increase_pct=Math.max(...Object.values(route_pairs)
      .map(row=>row.p95_increase_pct));
    const memory_increase_pct=100*(onRow.rss_peak_mib/offRow.rss_peak_mib-1);
    return {round,route_pairs,telemetry_off_p95_ms:offRow.p95_ms,
      telemetry_on_p95_ms:onRow.p95_ms,p95_increase_pct,
      telemetry_off_rss_mib:offRow.rss_peak_mib,
      telemetry_on_rss_mib:onRow.rss_peak_mib,memory_increase_pct,
      pass:Object.values(route_pairs).every(row=>row.p95_increase_pct<=10)
        &&memory_increase_pct<=15};
  });
  const p95Values=pairs.map(x=>x.p95_increase_pct);
  const rssValues=pairs.map(x=>x.memory_increase_pct);
  const pass=pairs.every(x=>x.pass);
  fs.writeFileSync(recordPath,JSON.stringify({
    source_sha:source,project,run_kind:"THREE_MATCHED_PAIRS",
    benchmark_path:"GET /api/v1/system/baseline + authenticated GET /api/v1/organizations/{synthetic}/ports",
    measured_routes:["baseline","authorized"],
    sample_size_per_mode_per_round:800,warmup_per_mode_per_round:160,
    concurrency:8,system:{platform:process.platform,arch:process.arch,
      cpus:os.cpus().length,memory_bytes:os.totalmem(),release:os.release(),
      node:process.version},rows,pairs,
    p95_increase_stddev_pct:sd(p95Values),
    rss_increase_stddev_pct:sd(rssValues),
    thresholds:{p95_increase_pct:10,memory_increase_pct:15},
    result:pass?"PASS":"FAIL",
    assessment_boundary:"Baseline plus authenticated scoped read. Fault and 10x burst witnesses are separate."
  },null,2)+"\n",{flag:"wx"});
  if(!pass)throw Error("Approved P09 baseline-read overhead thresholds were exceeded");
}catch(e){
  fail=e;
  if(!fs.existsSync(recordPath))
    fs.writeFileSync(recordPath,JSON.stringify({source_sha:source,project,
      result:"FAILED_OR_INCOMPLETE",completed_modes:rows.length,
      rows,error_type:e?.name||"Error",error_message:String(e?.message||"").slice(0,240)},null,2)+"\n",{flag:"wx"});
}finally{
  try {compose(on,["up","-d","--no-deps","--force-recreate","api"]);await ready();}
  catch(e){fail=fail||e;}
}
if(fail)throw Error("Paired G17 workload failed: "+fail.name);
