import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const dir=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const sha=process.env.HARBOROPS_SOURCE_SHA;
const project=process.env.HARBOROPS_G17_PROJECT;
assert.match(sha||"",/^[0-9a-f]{40}$/);
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.ok(dir&&fs.existsSync(dir));
const read=file=>JSON.parse(fs.readFileSync(path.join(dir,file),"utf8"));
const output={source_sha:sha,project,result:"FAIL",
  metrics_retention_hours:24,traces_retention_hours:24,
  raw_sources:["m17-retention-age-results.json",
    "m17-tempo-retention-results.json","m17-storage-results.json"],
  assertion_acceptance:"PENDING"};
let failure=null;
try{
  const metrics=read("m17-retention-age-results.json");
  const traces=read("m17-tempo-retention-results.json");
  const cap=read("m17-storage-results.json");
  for(const report of [metrics,traces,cap]){
    assert.equal(report.source_sha,sha);
    assert.equal(report.project,project);
    assert.equal(report.result,"PASS");
  }
  assert.equal(metrics.policy_hours,24);
  assert.ok(metrics.checks.some(c=>
    c.name==="expired-metric-blocks-only-removed"&&c.status==="PASS"&&
    c.old_removed>=1&&c.new_preserved>=1));
  assert.equal(traces.reference_policy_hours,24);
  assert.equal(traces.runtime_trace_ingested,true);
  assert.equal(traces.expired_trace_removed,true);
  assert.equal(traces.recent_trace_preserved,true);
  assert.ok(traces.checks.some(x=>
    x.name==="live-accelerated-trace-expiration"&&x.status==="PASS"));
  assert.ok(cap.checks.some(c=>
    c.name==="owned-retention-config"&&c.status==="PASS"));
  assert.ok(cap.checks.some(c=>
    c.name==="telemetry-storage-owned-and-under-250-mib"&&c.status==="PASS"));
  assert.ok(cap.checks.some(c=>
    c.name==="all-application-history-byte-identical"&&c.status==="PASS"));
  output.result="PASS";
}catch(e){failure=e;output.failure_type=e?.name||"Error";}
fs.writeFileSync(path.join(dir,"m17-retention-matrix-results.json"),
  JSON.stringify(output,null,2)+"\n",{flag:"wx"});
if(failure||output.result!=="PASS")
  throw Error("G17 metrics and trace expiry/cap/isolation evidence is incomplete");
