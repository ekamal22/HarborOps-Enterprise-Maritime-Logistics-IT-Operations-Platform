import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {verifyTrxCases} from "./g17-results-contract.mjs";

const root=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const sha=process.env.HARBOROPS_SOURCE_SHA;
const project=process.env.HARBOROPS_G17_PROJECT;
assert.match(sha||"",/^[0-9a-f]{40}$/);
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.ok(root&&fs.existsSync(root));
const file=path.join(root,"g17-regressions.trx");
const suites={
  M9:"TrackingWorkerG9ProcessTests",
  M10:"TrackingProjectionG10Tests",
  M11:"M11ClosureRuntimeTests",
  M13:"M13AdmissionControlAcceptanceTests",
  M15:"M15NotificationRuntimeTests",
  audit:"G15AuditAcceptanceTests",
  authorization:"ReferenceDataHttpTests",
  held_feature_defaults:"G13G15FeatureDefaultAcceptanceTests"
};
let result="FAIL",errors=[],cases={};
try{
  const trx=verifyTrxCases(fs.readFileSync(file,"utf8"));
  for(const [name,type] of Object.entries(suites)){
    const names=trx.methods.filter(testCase=>testCase.includes("."+type+"."));
    assert.ok(names.length>=1,
      "Milestone "+name+" has no executed passing source-locked regression tests");
    cases[name]={class:type,passed: names.length,
      test_names:names};
  }
  assert.ok(trx.passed>=Object.keys(suites).length);
  result="PASS";
}catch(error){errors.push(error?.message||"Unknown regression error");}
fs.writeFileSync(path.join(root,"g17-regression-matrix-results.json"),
  JSON.stringify({source_sha:sha,project,result,cases,errors,
    required_groups:Object.keys(suites).length,
    opt_in_status:"M17 enabled for full named suite; unavailable telemetry exporter",
    accepted_assertions:0},null,2)+"\n",{flag:"wx"});
if(result!=="PASS")throw Error("G17 accepted-milestone opt-in regression suite incomplete");
