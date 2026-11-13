import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {spawnSync} from "node:child_process";

const project=process.env.HARBOROPS_G17_PROJECT;
const source=process.env.HARBOROPS_SOURCE_SHA;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const storage=process.env.HARBOROPS_G17_STORAGE_ROOT;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[a-f0-9]{40}$/);
assert.match(storage||"",/^[Ee]:[\\/]/);
assert.ok(evidence&&fs.existsSync(evidence),"A uniquely owned evidence directory is required");

const scratch=path.resolve(storage,"retention-age-probe");
if(fs.existsSync(scratch))throw Error("Refuse to overwrite any retention probe fixture");
fs.mkdirSync(path.join(scratch,"blocks"),{recursive:true});
const out=path.join(evidence,"m17-retention-age-results.json");
const now=Date.now();
const expired=now-27*60*60*1000;
const current=now-2*60*60*1000;
const text=[
  "# TYPE g17_retention_age_probe gauge",
  ...[0,15000,30000,45000].map(offset=>
    'g17_retention_age_probe{band="expired"} 1 '+(expired+offset)),
  ...[0,15000,30000,45000].map(offset=>
    'g17_retention_age_probe{band="recent"} 1 '+(current+offset)),
  "# EOF"
].join("\n")+"\n";
fs.writeFileSync(path.join(scratch,"samples.openmetrics"),text,{flag:"wx"});
fs.writeFileSync(path.join(scratch,"prometheus.yaml"),
  "global:\n  scrape_interval: 15s\nscrape_configs: []\n",{flag:"wx"});

function docker(args,timeout=45000){
  const result=spawnSync("docker",args,{encoding:"utf8",timeout,maxBuffer:3*1024*1024});
  if(result.status!==0)throw Error("Disposable TSDB command failed: "+
    String(result.stderr).slice(0,350));
  return result.stdout.trim();
}
const mount=scratch.replaceAll("\\","/")+":/verification";
const image="prom/prometheus:v3.16.0";
function blocks(){
  return fs.readdirSync(path.join(scratch,"blocks"),{withFileTypes:true})
    .filter(x=>x.isDirectory()&&/^[0-9A-HJKMNP-TV-Z]{26}$/.test(x.name))
    .map(x=>{const meta=JSON.parse(fs.readFileSync(
      path.join(scratch,"blocks",x.name,"meta.json"),"utf8"));
      return{id:x.name,minTime:meta.minTime,maxTime:meta.maxTime};});
}
const report={source_sha:source,project,policy_hours:24,
  metrics_image:image,trace_retention_hours:24,
  checks:[],source_material:"synthetic-offline-TSDB",
  result:"FAIL"};
const name=project+"-retention";
let launched=false,failed=null;
try{
  const existing=docker(["ps","-a","--filter","name=^/"+name+"$",
    "--format","{{.ID}}"]);
  if(existing)throw Error("Retention process name already belongs to a container");
  docker(["run","--rm","--user","0:0","--network","none",
    "-v",mount,"--entrypoint","/bin/promtool",image,
    "tsdb","create-blocks-from","openmetrics",
    "/verification/samples.openmetrics","/verification/blocks"],120000);
  const initially=blocks();
  const expiredBlocks=initially.filter(x=>x.maxTime<now-24*60*60*1000);
  const currentBlocks=initially.filter(x=>x.maxTime>now-4*60*60*1000);
  assert.ok(expiredBlocks.length>=1&&currentBlocks.length>=1,
    "Promtool failed to create separate expired and recent real TSDB blocks");
  report.checks.push({name:"real-expired-and-recent-tsdb-blocks",
    status:"PASS",expired:expiredBlocks.length,current:currentBlocks.length});
  const id=docker(["run","-d","--name",name,
    "--label","harborops.g17.project="+project,
    "--network","none","--user","0:0","-v",mount,
    "--entrypoint","/bin/prometheus",image,
    "--config.file=/verification/prometheus.yaml",
    "--storage.tsdb.path=/verification/blocks",
    "--storage.tsdb.retention.time=24h",
    "--storage.tsdb.retention.size=120MB",
    "--web.listen-address=127.0.0.1:9090"]);
  assert.match(id,/^[a-f0-9]{64}$/);
  launched=true;
  const deadline=Date.now()+180000;
  let left=[];
  while(Date.now()<deadline) {
    left=blocks();
    const expiredGone=expiredBlocks.every(b=>!left.some(x=>x.id===b.id));
    const currentPreserved=currentBlocks.every(b=>left.some(x=>x.id===b.id));
    if(expiredGone&&currentPreserved)break;
    await new Promise(resolve=>setTimeout(resolve,5000));
  }
  const expiredGone=expiredBlocks.every(b=>!left.some(x=>x.id===b.id));
  const currentPreserved=currentBlocks.every(b=>left.some(x=>x.id===b.id));
  assert.ok(expiredGone&&currentPreserved,
    "Actual 24h Prometheus retention did not expire old blocks while preserving recent ones");
  report.checks.push({name:"expired-metric-blocks-only-removed",
    status:"PASS",old_removed:expiredBlocks.length,new_preserved:currentBlocks.length});
  const sourceConfig=fs.readFileSync("monitoring/m17/tempo.yaml","utf8");
  const sourceOverlay=fs.readFileSync("compose.m17.observability.override.yaml","utf8");
  assert.match(sourceConfig,/block_retention:\s*24h/);
  assert.match(sourceOverlay,/--storage\.tsdb\.retention\.time=24h/);
  assert.match(sourceOverlay,/--storage\.tsdb\.retention\.size=120MB/);
  report.checks.push({name:"exact-tempo-and-metrics-retention-config",
    status:"PASS",tempo_trace_compaction:"24h",
    metrics_actual_age_expiry:"24h"});
  report.result="PASS";
}catch(e){failed=e;report.error_type=e?.name||"Error";}
finally{
  if(launched){
    try{
      docker(["stop","-t","10",name],30000);
      docker(["rm",name],20000);
      report.checks.push({name:"owned-probe-process-cleanup",status:"PASS"});
    }catch(e){
      failed=failed||e;
      report.result="FAIL";
      report.checks.push({name:"owned-probe-process-cleanup",status:"FAIL"});
    }
  }
  fs.writeFileSync(out,JSON.stringify(report,null,2)+"\n",{flag:"wx"});
}
if(failed||report.result!=="PASS")
  throw Error("Synthetic 24h retention expiry verification failed; scratch data retained");
