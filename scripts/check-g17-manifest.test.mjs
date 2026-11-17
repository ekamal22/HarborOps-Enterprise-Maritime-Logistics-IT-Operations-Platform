import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import crypto from "node:crypto";
import { verifyManifest } from "./verify-g17-manifest.mjs";

const sha = "a".repeat(40);
const inventoryBytes = fs.readFileSync(new URL("../docs/M17_G17_ASSERTION_INVENTORY_20261112.json", import.meta.url));
const inventoryHash = crypto.createHash("sha256").update(inventoryBytes).digest("hex");
const digest = bytes => crypto.createHash("sha256").update(bytes).digest("hex");
function fixture({complete = false, missing = false} = {}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-manifest-"));
  const required = ["source-sha.txt","expected-artifacts.json","run-status.json","results.txt"];
  if (complete) required.push("g17-unit.trx","g17-contract.trx","g17-coverage-preflight.json");
  fs.writeFileSync(path.join(dir,"source-sha.txt"),sha);
  fs.writeFileSync(path.join(dir,"expected-artifacts.json"),JSON.stringify({
    schema_version:1,gate:"G17",source_sha:sha,project:"ho-verify-m17-test",
    disposable_stack:false,require_full_g17:false,required_artifacts:required
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
    fs.writeFileSync(path.join(dir,"g17-coverage-preflight.json"),JSON.stringify({
      passed:true,strict:true,technically_authored:72,assertions:75,scenarios:25,errors:[]
    }));
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
    assertion_inventory_sha256:inventoryHash,
    scenario_count:25,assertion_count:75,reviewed_assertion_count:0,
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
  assert.throws(()=>verifyManifest(path.join(dir,"manifest.json"),sha),/missing (?:TRX|test) counters/i);
});

// S25-F04. Playwright's .last-run.json is a hidden file inside a nested directory. It is
// indexed and hash-verified like any other evidence, so removing, altering or adding a
// hidden record is detected rather than skipped by a default hidden-file filter.
function withHidden(t) {
  const dir=fixture();cleanup(t,dir);
  for(const nested of ["browser-artifacts","cardinality-artifacts"]) {
    fs.mkdirSync(path.join(dir,nested));
    fs.writeFileSync(path.join(dir,nested,".last-run.json"),
      JSON.stringify({status:"passed",failedTests:[]}));
  }
  const crawl=(root,prefix="")=>fs.readdirSync(root,{withFileTypes:true}).flatMap(entry=>{
    const rel=prefix?prefix+"/"+entry.name:entry.name;
    if(rel==="manifest.json")return [];
    if(entry.isDirectory())return crawl(path.join(root,entry.name),rel);
    const bytes=fs.readFileSync(path.join(root,entry.name));
    return [{path:rel,bytes:bytes.length,sha256:digest(bytes)}];
  }).sort((a,b)=>a.path.localeCompare(b.path));
  const p=path.join(dir,"manifest.json"),m=JSON.parse(fs.readFileSync(p,"utf8"));
  m.files=crawl(dir);fs.writeFileSync(p,JSON.stringify(m));
  return {dir,manifest:p};
}
test("hidden Playwright last-run records are manifest-indexed",t=>{
  const {manifest}=withHidden(t);
  const index=JSON.parse(fs.readFileSync(manifest,"utf8")).files.map(x=>x.path);
  assert.ok(index.includes("browser-artifacts/.last-run.json"));
  assert.ok(index.includes("cardinality-artifacts/.last-run.json"));
  assert.equal(verifyManifest(manifest,sha).gate_acceptance,"NOT_ACCEPTED");
});
test("an altered, removed or extra hidden record is detected",t=>{
  const {dir,manifest}=withHidden(t);
  const hidden=path.join(dir,"browser-artifacts",".last-run.json");
  const original=fs.readFileSync(hidden);
  fs.writeFileSync(hidden,JSON.stringify({status:"passed",failedTests:["x"]}));
  assert.throws(()=>verifyManifest(manifest,sha),/Evidence tampered or unlisted: browser-artifacts\/\.last-run\.json/);
  fs.writeFileSync(hidden,original);
  assert.equal(typeof verifyManifest(manifest,sha),"object");
  fs.unlinkSync(path.join(dir,"cardinality-artifacts",".last-run.json"));
  assert.throws(()=>verifyManifest(manifest,sha),/coverage mismatch/);
  fs.writeFileSync(path.join(dir,"cardinality-artifacts",".last-run.json"),
    JSON.stringify({status:"passed",failedTests:[]}));
  fs.writeFileSync(path.join(dir,"cardinality-artifacts",".extra-hidden.json"),"{}");
  assert.throws(()=>verifyManifest(manifest,sha),/coverage mismatch/);
});
