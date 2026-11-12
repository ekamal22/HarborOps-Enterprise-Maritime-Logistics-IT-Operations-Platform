import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import crypto from "node:crypto";
import { verifyManifest } from "./verify-g17-manifest.mjs";

const sha = "a".repeat(40);
const h = bytes => crypto.createHash("sha256").update(bytes).digest("hex");
function fixture() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "g17-manifest-"));
  const bytes = Buffer.from("clean output\n");
  fs.writeFileSync(path.join(dir, "results.txt"), bytes);
  fs.writeFileSync(path.join(dir, "manifest.json"), JSON.stringify({
    schema_version:2,gate:"G17",source_sha:sha,
    gate_acceptance:"NOT_ACCEPTED",assertion_disposition:"NOT_REVIEWED",
    files:[{path:"results.txt",sha256:h(bytes),bytes:bytes.length}]
  }));
  return dir;
}
test("untampered evidence index succeeds", t => {
  const dir=fixture();t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));
  assert.deepEqual(verifyManifest(path.join(dir,"manifest.json"),sha),{
    source_sha:sha,files:1
  });
});
test("tampered file is rejected",t=>{
  const dir=fixture();t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));
  fs.appendFileSync(path.join(dir,"results.txt"),"changed");
  assert.throws(()=>verifyManifest(path.join(dir,"manifest.json"),sha),/tampered/);
});
test("unlisted additional file is rejected",t=>{
  const dir=fixture();t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));
  fs.writeFileSync(path.join(dir,"unexpected.png"),"binary");
  assert.throws(()=>verifyManifest(path.join(dir,"manifest.json"),sha),/coverage/);
});
test("wrong SHA is rejected",t=>{
  const dir=fixture();t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));
  assert.throws(()=>verifyManifest(path.join(dir,"manifest.json"),"b".repeat(40)),/Wrong source/);
});
test("false gate acceptance is rejected",t=>{
  const dir=fixture();t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));
  const p=path.join(dir,"manifest.json");
  const m=JSON.parse(fs.readFileSync(p,"utf8"));m.gate_acceptance="ACCEPTED";
  fs.writeFileSync(p,JSON.stringify(m));
  assert.throws(()=>verifyManifest(p,sha),/Invalid source/);
});
