import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import {execFileSync,spawnSync} from "node:child_process";

const project=process.env.HARBOROPS_G17_PROJECT;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const sha=process.env.HARBOROPS_SOURCE_SHA;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(sha||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence));
const files=["compose.m17.verify.yaml","compose.m17.observability.override.yaml"];
const args=["compose","-p",project,...files.flatMap(f=>["-f",f]),"--profile","observability"];
const report=path.join(evidence,"m17-storage-results.json");
const evidenceData={source_sha:sha,project,checks:[],result:"FAIL"};
let fullStoreCreated=false;
function command(cmd,a,timeout=30000) {
  return execFileSync(cmd,a,{encoding:"utf8",timeout,maxBuffer:8*1024*1024}).trim();
}
function compose(a) {return command("docker",[...args,...a]);}
function container(service) {
  const ids=compose(["ps","-a","-q",service]).split(/\s+/).filter(Boolean);
  assert.equal(ids.length,1,"Exactly one owned "+service+" container required");
  const details=JSON.parse(command("docker",["inspect",ids[0]]))[0];
  assert.equal(details.Config.Labels["com.docker.compose.project"],project);
  assert.equal(details.Config.Labels["com.docker.compose.service"],service);
  return details;
}
function quote(sql) {return sql;}
function snapshotDatabase() {
  const pg=container("postgres");
  // Hash the complete owned application public-schema data stream, including
  // all M8/M11/M15/M16 tables, rather than trusting counts or wiping history.
  const result=execFileSync("docker",["exec",pg.Id,
    "pg_dump","-U","harborops","-d","harborops",
    "--data-only","--schema=public","--no-owner","--no-privileges"],{
      timeout:90000,maxBuffer:64*1024*1024
    });
  const stable=result.toString("utf8").split(/\r?\n/)
    // PostgreSQL 16.15 pg_dump writes a random \restrict/\unrestrict token pair
    // into every dump. It is not data and would make any two hashes differ.
    .filter(line=>!/^(-- (Dumped|Completed|Started) on )/.test(line) &&
      !/^\\(?:un)?restrict\s/.test(line))
    .join("\n");
  return crypto.createHash("sha256").update(stable).digest("hex");
}
function check(name,fn) {
  const value=fn();evidenceData.checks.push({name,status:"PASS",value});return value;
}
async function checkApi() {
  for(const [url,status] of [["/health/live",200],["/health/ready",200],
    ["/api/v1/system/baseline",200]]) {
    const response=await fetch("http://127.0.0.1:18080"+url,
      {signal:AbortSignal.timeout(10000)});
    assert.equal(response.status,status,"Business endpoint changed during local telemetry storage fault");
  }
}
let failure=null;
try {
  const retention=check("owned-retention-config",()=>{
    const prometheus=fs.readFileSync("compose.m17.observability.override.yaml","utf8");
    const tempo=fs.readFileSync("monitoring/m17/tempo.yaml","utf8");
    assert.match(prometheus,/--storage\.tsdb\.retention\.time=24h/);
    assert.match(prometheus,/--storage\.tsdb\.retention\.size=120MB/);
    assert.match(tempo,/block_retention:\s*24h/);
    return {metrics_hours:24,traces_hours:24};
  });
  const cap=check("telemetry-storage-owned-and-under-250-mib",()=>{
    const expected={otelcol:"/tmp",tempo:"/var/tempo",prometheus:"/prometheus",
      grafana:"/var/lib/grafana"};
    const sizes=[];
    for(const [service,mount] of Object.entries(expected)) {
      const info=container(service);
      const opts=info.HostConfig.Tmpfs?.[mount];
      assert.ok(opts&&/size=\d+/.test(opts),"Telemetry store is not an owned tmpfs: "+service);
      sizes.push({service,mount,bytes:Number(opts.match(/size=(\d+)/)[1])});
      assert.ok(!info.Mounts.some(x=>x.Destination===mount&&x.Type==="bind"),
        "Retained bind-mount in telemetry storage: "+service);
    }
    const total=sizes.reduce((n,x)=>n+x.bytes,0);
    assert.ok(total>0&&total<=250*1024*1024,"Storage cap exceeds policy");
    return {total_bytes:total,volumes:sizes};
  });
  const before=snapshotDatabase();
  evidenceData.database_sha256_before=before;
  await checkApi();
  const prometheus=container("prometheus");
  const script="dd if=/dev/zero of=/prometheus/g17-owned-fill.tmp bs=1048576 count=256";
  const fill=spawnSync("docker",["exec",prometheus.Id,"/bin/sh","-c",script],{
    encoding:"utf8",timeout:60000,maxBuffer:512*1024
  });
  // Filling a bounded tmpfs must terminate with ENOSPC, not silently succeed.
  assert.notEqual(fill.status,0,"Store unexpectedly accepted more than its tmpfs cap");
  assert.match(fill.stderr||"",/No space left on device|ENOSPC/i,
    "Pressure command did not prove a full tmpfs: "+String(fill.stderr).slice(0,100));
  fullStoreCreated=true;
  await checkApi();
  evidenceData.checks.push({name:"business-remains-responsive-during-full-telemetry-store",
    status:"PASS"});
  const after=snapshotDatabase();
  evidenceData.database_sha256_after=after;
  assert.equal(after,before,"Telemetry storage saturation modified application history");
  evidenceData.checks.push({name:"all-application-history-byte-identical",
    status:"PASS"});
  evidenceData.result="PASS";
}catch(e){
  failure=e;
  evidenceData.failure_type=e?.name??"Error";
  evidenceData.failure_message=String(e?.message??"").slice(0,240);
}finally{
  if(fullStoreCreated) {
    try {
      const pg=container("prometheus");
      command("docker",["exec",pg.Id,"/bin/sh","-c",
        "rm -f /prometheus/g17-owned-fill.tmp"],20000);
      evidenceData.checks.push({name:"test-owned-fill-file-cleanup",status:"PASS"});
    } catch(e) {
      // Recreate only the project-owned disposable Prometheus tmpfs container.
      try {
        compose(["up","-d","--no-deps","--force-recreate","prometheus"]);
        evidenceData.checks.push({name:"test-owned-prometheus-recovery",status:"PASS"});
      } catch {
        evidenceData.result="FAIL";
        failure=failure||e;
      }
    }
  }
  fs.writeFileSync(report,JSON.stringify(evidenceData,null,2)+"\n",{flag:"wx"});
}
if(failure||evidenceData.result!=="PASS")
  throw Error("Owned telemetry storage fault scenario failed; raw evidence retained");
