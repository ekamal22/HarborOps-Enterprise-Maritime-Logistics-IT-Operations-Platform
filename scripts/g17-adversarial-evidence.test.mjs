import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import crypto from "node:crypto";
import {verifyTrxCases,verifyPlaywrightCases} from "./g17-results-contract.mjs";
import {verifyManifest} from "./verify-g17-manifest.mjs";
import {checkPerAssertion} from "./reconcile-g17-assertions.mjs";

const SHA="a".repeat(40);
test("G17 verifier rejects tampering, skipped cases, duplicate results and wrong source SHA",t=>{
  const root=fs.mkdtempSync(path.join(os.tmpdir(),"g17-evidence-negative-"));
  t.after(()=>fs.rmSync(root,{recursive:true,force:true}));
  const status=path.join(root,"run-status.json");
  fs.writeFileSync(status,'{"gate":"G17","run_state":"FAILED"}');
  const bytes=fs.readFileSync(status);
  const manifestPath=path.join(root,"manifest.json");
  fs.writeFileSync(manifestPath,JSON.stringify({
    schema_version:2,gate:"G17",gate_acceptance:"NOT_ACCEPTED",
    source_sha:SHA,files:[{path:"run-status.json",bytes:bytes.length,
      sha256:crypto.createHash("sha256").update(bytes).digest("hex")}]
  }));
  assert.throws(()=>verifyManifest(manifestPath,"b".repeat(40)),/Wrong source SHA/);
  fs.appendFileSync(status,"tampered");
  assert.throws(()=>verifyManifest(manifestPath,SHA),/Evidence tampered/);

  const skipped='<TestRun><Counters total="1" passed="0" failed="0" notExecuted="1" />'+
    '<UnitTestResult testName="Suite.One" outcome="NotExecuted" /></TestRun>';
  const duplicate='<TestRun><Counters total="2" passed="2" failed="0" notExecuted="0" />'+
    '<UnitTestResult testName="Suite.One" outcome="Passed" />'+
    '<UnitTestResult testName="Suite.One" outcome="Passed" /></TestRun>';
  assert.throws(()=>verifyTrxCases(skipped),/Incomplete/);
  assert.throws(()=>verifyTrxCases(duplicate),/Duplicated/);

  const browser={suites:[{specs:[{title:"dashboard requires authentication and shows actual monitoring panels",
    tests:[{projectName:"chromium",expectedStatus:"passed",status:"expected",
      results:[{status:"passed",retry:0}]}]}]}],
    stats:{expected:1,unexpected:0,skipped:0,flaky:0}};
  assert.throws(()=>verifyPlaywrightCases(browser,"dashboard"),/Missing/);

  const coverage={scenarios:Array.from({length:25},(_,i)=>({
    id:"G17-S"+String(i+1).padStart(2,"0"),
    assertions:Array.from({length:3},(_,k)=>({
      id:"G17-S"+String(i+1).padStart(2,"0")+"-A0"+(k+1),
      expected:"Synthetic proof",assessment:i===24?"INDEPENDENT_REVIEW":"TEST_AUTHORED",
      witness:i===24?null:{path:"tests/dummy",case:"Synthetic"}
    }))
  }))};
  const ledger=checkPerAssertion(coverage,(w,id)=>
    id==="G17-S24-A01"?null:{passed:true,artifact:"raw.trx"});
  assert.equal(ledger.pass,false);
  assert.ok(ledger.errors.some(x=>x.includes("G17-S24-A01")));
});
