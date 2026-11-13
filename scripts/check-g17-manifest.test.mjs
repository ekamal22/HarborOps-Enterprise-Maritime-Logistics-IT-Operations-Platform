import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import crypto from "node:crypto";
import { verifyManifest } from "./verify-g17-manifest.mjs";

const sha = "a".repeat(40);
const digest = bytes => crypto.createHash("sha256").update(bytes).digest("hex");
function fixture({complete = false, missing = false} = {}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-manifest-"));
  const required = ["source-sha.txt","expected-artifacts.json","run-status.json","results.txt"];
  if (complete) required.push("g17-unit.trx","g17-contract.trx");
  fs.writeFileSync(path.join(dir,"source-sha.txt"),sha);
  fs.writeFileSync(path.join(dir,"expected-artifacts.json"),JSON.stringify({
    schema_version:1,gate:"G17",source_sha:sha,project:"ho-verify-m17-test",
    disposable_stack:false,required_artifacts:required
  }));
  fs.writeFileSync(path.join(dir,"run-status.json"),JSON.stringify({
    schema_version:2,gate:"G17",source_sha:sha,requested_source_sha:sha,
    source_verified:true,gate_acceptance:"NOT_ACCEPTED",
    g17_assertion_review:"NOT_REVIEWED",
    run_state:complete?"COMPLETE":"FAILED",
    required_artifacts:required,stage_results:complete
      ? [{stage:"synthetic",status:"PASS",exit_code:0}]
      : [{stage:"synthetic",status:"FAIL",exit_code:1}]
  }));
  if (!missing) fs.writeFileSync(path.join(dir,"results.txt"),"clean synthetic result\n");
  if (complete) {
    for (const file of ["g17-unit.trx","g17-contract.trx"])
      fs.writeFileSync(path.join(dir,file),"<TestRun></TestRun>");
  }
  const names = fs.readdirSync(dir).sort();
  const files = names.map(name => {
    const bytes=fs.readFileSync(path.join(dir,name));
    return {path:name,bytes:bytes.length,sha256:digest(bytes)};
  });
  fs.writeFileSync(path.join(dir,"manifest.json"),JSON.stringify({
    schema_version:2,gate:"G17",source_sha:sha,
    gate_acceptance:"NOT_ACCEPTED",assertion_disposition:"NOT_REVIEWED",
    run_state:complete?"COMPLETE":"FAILED",files,
    missing_expected_artifacts:missing?["results.txt"]:[]
  }));
  return dir;
}
function cleanup(t,dir) { t.after(()=>fs.rmSync(dir,{recursive:true,force:true})); }
test("failed run preserves independently verifiable partial evidence", t => {
  const dir=fixture({missing:true});cleanup(t,dir);
  const report=verifyManifest(path.join(dir,"manifest.json"),sha);
  assert.equal(report.complete,false);
  assert.deepEqual(report.missing_expected_artifacts,["results.txt"]);
  assert.equal(report.gate_acceptance,"NOT_ACCEPTED");
});
test("altered evidence cannot pass hash verification",t=>{
  const dir=fixture();cleanup(t,dir);
  fs.appendFileSync(path.join(dir,"results.txt"),"altered");
  assert.throws(()=>verifyManifest(path.join(dir,"manifest.json"),sha),/tampered/);
});
test("unlisted extra evidence is rejected",t=>{
  const dir=fixture();cleanup(t,dir);
  fs.writeFileSync(path.join(dir,"unexpected.png"),"binary");
  assert.throws(()=>verifyManifest(path.join(dir,"manifest.json"),sha),/coverage/);
});
test("wrong source is rejected",t=>{
  const dir=fixture();cleanup(t,dir);
  assert.throws(()=>verifyManifest(path.join(dir,"manifest.json"),"b".repeat(40)),/Wrong source/);
});
test("false accepted gate is rejected",t=>{
  const dir=fixture();cleanup(t,dir);
  const p=path.join(dir,"manifest.json"), m=JSON.parse(fs.readFileSync(p,"utf8"));
  m.gate_acceptance="ACCEPTED";fs.writeFileSync(p,JSON.stringify(m));
  assert.throws(()=>verifyManifest(p,sha),/Invalid source/);
});
test("fake complete claim without required raw result is rejected",t=>{
  const dir=fixture({complete:true,missing:true});cleanup(t,dir);
  assert.throws(()=>verifyManifest(path.join(dir,"manifest.json"),sha),/missing evidence/i);
});
test("fake complete claim with incomplete stage evidence is rejected",t=>{
  const dir=fixture({complete:true});cleanup(t,dir);
  // No required TRX case output, even though the wrapper stage is marked green.
  assert.throws(()=>verifyManifest(path.join(dir,"manifest.json"),sha),/missing test counters/i);
});
