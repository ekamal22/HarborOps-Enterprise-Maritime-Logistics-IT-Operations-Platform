import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const source=process.env.HARBOROPS_SOURCE_SHA;
const project=process.env.HARBOROPS_G17_PROJECT;
assert.match(source||"",/^[0-9a-f]{40}$/);
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.ok(evidence&&fs.existsSync(evidence));
const read=file=>JSON.parse(fs.readFileSync(path.join(evidence,file),"utf8"));
const result={source_sha:source,project,result:"FAIL",checks:[],
  technical_acceptance:"PENDING"};
let error=null;
try{
  const db=read("m17-database-fault-results.json");
  const latency=read("m17-live-latency-alert-results.json");
  assert.equal(db.source_sha,source);
  assert.equal(db.project,project);
  assert.equal(db.result,"PASS");
  assert.equal(db.database_restored,true);
  assert.equal(latency.source_sha,source);
  assert.equal(latency.project,project);
  assert.equal(latency.result,"PASS");
  const dbNames=new Map(db.checks.map(check=>[check.name,check]));
  const latencyNames=new Map(latency.checks.map(check=>[check.name,check]));
  for(const name of [
    "real-db-outage-keeps-live-and-errors-classified",
    "real-http-5xx-rate-fires-reference-alert",
    "database-alert-identifies-only-safe-component",
    "recovery-clears-alert-and-preserves-history"
  ]){
    assert.equal(dbNames.get(name)?.status,"PASS",
      "Missing actual PostgreSQL/HTTP fault witness: "+name);
    result.checks.push({fault:name,status:"PASS",
      source_artifact:"m17-database-fault-results.json"});
  }
  const fifth=dbNames.get("real-http-5xx-rate-fires-reference-alert");
  assert.equal(fifth.data?.real_503_responses,120);
  assert.equal(fifth.data?.alert,"HarborOpsElevatedApiFailureRate");
  const critical=dbNames.get("database-alert-identifies-only-safe-component");
  assert.equal(critical.data?.alert?.name,"HarborOpsDatabaseUnavailable");
  assert.equal(latencyNames.get("real-histogram-triggers-p95-alert")?.status,"PASS");
  assert.ok(latency.request_count>=100&&latency.slow_requests>=15);
  const p95=latencyNames.get("real-histogram-triggers-p95-alert").detail;
  assert.equal(p95?.alert,"HarborOpsHighApiP95");
  assert.ok(p95.real_p95_seconds>1);
  result.checks.push({fault:"actual-api-p95-alert",status:"PASS",
    source_artifact:"m17-live-latency-alert-results.json"});
  const fixture=fs.readFileSync(path.join(evidence,"m17-promtool-test.txt"),"utf8");
  assert.match(fixture,/SUCCESS/);
  for(const marker of ["HarborOpsHighApiP95",
    "HarborOpsElevatedApiFailureRate","HarborOpsDatabaseUnavailable",
    "HarborOpsTrackingBacklogStale"]){
    const sourceRules=fs.readFileSync("monitoring/m17/alert-test.yaml","utf8");
    assert.ok(sourceRules.includes(marker),
      "Prometheus synthetic fixture omitted rule "+marker);
  }
  result.checks.push({fault:"real-alerts-and-promtool-hysteresis-fixtures",
    status:"PASS",source_artifact:"m17-promtool-test.txt"});
  assert.equal(result.checks.length,6);
  result.result="PASS";
}catch(e){error=e;result.error_type=e?.name||"Error";result.error_message=String(e?.message||"").slice(0,240);}
fs.writeFileSync(path.join(evidence,"m17-alert-matrix-results.json"),
  JSON.stringify(result,null,2)+"\n",{flag:"wx"});
if(error||result.result!=="PASS")
  throw Error("G17 end-to-end 5xx/p95/DB alert matrix incomplete");
