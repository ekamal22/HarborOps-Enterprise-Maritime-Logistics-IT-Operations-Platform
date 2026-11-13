import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {spawnSync} from "node:child_process";

const project=process.env.HARBOROPS_G17_PROJECT;
const source=process.env.HARBOROPS_SOURCE_SHA;
const root=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const storage=process.env.HARBOROPS_G17_STORAGE_ROOT;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[0-9a-f]{40}$/);
const hostedEvidence = process.env.GITHUB_ACTIONS === "true" &&
  process.env.HARBOROPS_G17_HOSTED === "true" &&
  process.env.RUNNER_TEMP &&
  path.resolve(process.env.HARBOROPS_G17_EVIDENCE_DIR || "").startsWith(
    path.resolve(process.env.RUNNER_TEMP) + path.sep);
assert.ok(/^[Ee]:[\\/]/.test(storage || "") || (hostedEvidence &&
  path.resolve(storage||"").startsWith(path.resolve(process.env.RUNNER_TEMP)+path.sep)),
  "Disposable telemetry storage must be isolated on E: or hosted runner temp");
assert.ok(root&&fs.existsSync(root));
const image="grafana/tempo:2.10.8";
const container=project+"-tempo-ttl-probe";
const scratch=path.resolve(storage,"tempo-retention-probe");
if(fs.existsSync(scratch))throw Error("Refuse to overwrite an existing trace expiry fixture");
fs.mkdirSync(path.join(scratch,"data"),{recursive:true});
const config=[
  "server:",
  "  http_listen_port: 3200",
  "distributor:",
  "  receivers:",
  "    otlp:",
  "      protocols:",
  "        http:",
  "          endpoint: 0.0.0.0:4318",
  "ingester:",
  "  trace_idle_period: 2s",
  "  max_block_duration: 10s",
  "compactor:",
  "  compaction:",
  "    block_retention: 45s",
  "    compacted_block_retention: 20s",
  "    compaction_cycle: 5s",
  "storage:",
  "  trace:",
  "    backend: local",
  "    wal:",
  "      path: /var/tempo/wal",
  "    local:",
  "      path: /var/tempo/blocks",
  ""
].join("\n");
fs.writeFileSync(path.join(scratch,"tempo.yaml"),config,{flag:"wx"});
const report={source_sha:source,project,result:"FAIL",image,
  reference_policy_hours:24,accelerated_retention_seconds:45,
  checks:[],runtime_trace_ingested:false,expired_trace_removed:false,
  recent_trace_preserved:false};
const output=path.join(root,"m17-tempo-retention-results.json");
function docker(args,timeout=45000) {
  const r=spawnSync("docker",args,{encoding:"utf8",timeout,maxBuffer:2*1024*1024});
  if(r.status!==0)throw Error("Disposable Tempo TTL command failed: "+
    (r.stderr||"").slice(0,350));
  return r.stdout.trim();
}
const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function poll(fn,timeoutMs=80000){
  const until=Date.now()+timeoutMs;
  while(Date.now()<until){
    try{const value=await fn();if(value)return value;}catch{}
    await wait(2500);
  }
  throw Error("Tempo live trace TTL observation did not converge");
}
async function traceVisible(id){
  const response=await fetch("http://127.0.0.1:13200/api/v2/traces/"+id,
    {signal:AbortSignal.timeout(7000)});
  if(response.status===404)return false;
  assert.equal(response.status,200);
  const body=await response.json();
  return JSON.stringify(body).toLowerCase().includes(id.slice(0,16));
}
async function ingest() {
  const id=crypto.randomBytes(16).toString("hex");
  const span=crypto.randomBytes(8).toString("hex");
  const start=(BigInt(Date.now())*1000000n).toString();
  const end=(BigInt(Date.now()+5)*1000000n).toString();
  const message={resourceSpans:[{
    resource:{attributes:[{key:"service.name",value:{stringValue:"g17-expiry-only"}}]},
    scopeSpans:[{scope:{name:"g17-tempo-retention"},spans:[{
      traceId:id,spanId:span,name:"g17.synthetic.expiry",
      kind:1,startTimeUnixNano:start,endTimeUnixNano:end
    }]}]
  }]};
  const response=await fetch("http://127.0.0.1:14318/v1/traces",{
    method:"POST",headers:{"Content-Type":"application/json"},
    body:JSON.stringify(message),signal:AbortSignal.timeout(11000)});
  assert.ok(response.status===200||response.status===202,
    "Synthetic OTLP/HTTP ingest was rejected");
  await poll(()=>traceVisible(id),70000);
  return id;
}
let started=false,failed=null;
try{
  const occupied=docker(["ps","-a","--filter","name=^/"+container+"$",
    "--format","{{.ID}}"]);
  assert.equal(occupied,"","The isolated TTL fixture name is already occupied");
  const original=fs.readFileSync("monitoring/m17/tempo.yaml","utf8");
  assert.match(original,/block_retention:\s*24h/);
  assert.ok(!original.includes("45s"),"The retained M17 configuration was mutated");
  const mountConfig=path.join(scratch,"tempo.yaml").replaceAll("\\","/")+
    ":/etc/tempo.yaml:ro";
  const mountData=path.join(scratch,"data").replaceAll("\\","/")+
    ":/var/tempo";
  const id=docker(["run","-d","--name",container,
    "--label","harborops.g17.project="+project,"--user","0:0",
    "--network","bridge","-p","127.0.0.1:13200:3200",
    "-p","127.0.0.1:14318:4318","-v",mountConfig,"-v",mountData,
    image,"-config.file=/etc/tempo.yaml"],60000);
  assert.match(id,/^[0-9a-f]{64}$/);
  started=true;
  await poll(async()=>{
    const ready=await fetch("http://127.0.0.1:13200/ready",
      {signal:AbortSignal.timeout(3000)});
    return ready.status===200;
  },90000);
  const expired=await ingest();
  report.runtime_trace_ingested=true;
  report.checks.push({name:"positive-otlp-trace-query",status:"PASS"});
  await wait(55000);
  const recent=await ingest();
  const deadline=Date.now()+200000;
  while(Date.now()<deadline){
    const oldVisible=await traceVisible(expired);
    const newVisible=await traceVisible(recent);
    if(!oldVisible&&newVisible){
      report.expired_trace_removed=true;
      report.recent_trace_preserved=true;
      break;
    }
    await wait(5000);
  }
  assert.ok(report.expired_trace_removed&&report.recent_trace_preserved,
    "Tempo failed to expire only the old trace while retaining a newer one");
  report.checks.push({name:"live-accelerated-trace-expiration",status:"PASS"});
  const counts=fs.readdirSync(path.join(scratch,"data"),{withFileTypes:true});
  assert.ok(counts.length>0,"No owned trace backend storage was created");
  report.checks.push({name:"only-owned-disposable-storage-used",status:"PASS"});
  report.result="PASS";
}catch(e){failed=e;report.error_type=e?.name||"Error";report.error_message=String(e?.message||"").slice(0,240);}
finally{
  if(started){
    try{
      const existing=docker(["ps","-a","--filter","name=^/"+container+"$",
        "--format","{{.ID}}"]);
      if(existing){
        docker(["stop","-t","10",container],30000);
        docker(["rm",container],20000);
      }
      report.checks.push({name:"owned-tempo-probe-stopped",status:"PASS"});
    }catch(e){
      failed=failed||e;
      report.result="FAIL";
      report.checks.push({name:"owned-tempo-probe-stopped",status:"FAIL"});
    }
  }
  fs.writeFileSync(output,JSON.stringify(report,null,2)+"\n",{flag:"wx"});
}
if(failed||report.result!=="PASS")
  throw Error("Isolated trace TTL retention demonstration failed");
