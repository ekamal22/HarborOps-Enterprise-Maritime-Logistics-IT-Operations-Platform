import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import {fileURLToPath} from "node:url";
import {checkPerAssertion} from "./reconcile-g17-assertions.mjs";

const template=JSON.parse(fs.readFileSync(new URL(
  "../docs/M17_G17_TEST_COVERAGE_20261113.json",import.meta.url),"utf8"));
const copy=value=>JSON.parse(JSON.stringify(value));
function complete() {
  const j=copy(template);
  for(const s of j.scenarios.slice(0,24)) {
    for(const a of s.assertions) {
      a.assessment="TEST_AUTHORED";
      a.witness={path:"tests/mock-technical.test.mjs",case:a.id};
    }
  }
  return j;
}
test("one positive raw witness is required for every one of 72 technical assertions",()=>{
  const data=complete();
  const results=checkPerAssertion(data,(w,id)=>({passed:true,
    artifact:"source-locked-"+id+".trx"}));
  assert.equal(results.pass,true,results.errors.join("\n"));
  assert.equal(results.technical_passes,72);
  assert.equal(results.independent_pending,3);
  assert.equal(results.total_assertions,75);
  assert.equal(results.acceptance,"PENDING");
});
test("rejects a missing assertion evidence row even when other suites pass",()=>{
  const data=complete();
  const result=checkPerAssertion(data,(w,id)=>
    id==="G17-S13-A03"?null:{passed:true,artifact:"raw.trx"});
  assert.equal(result.pass,false);
  assert.equal(result.technical_passes,71);
  assert.ok(result.errors.some(x=>x.includes("G17-S13-A03")));
});
test("rejects partial, missing and skipped technical assertion classifications",()=>{
  for(const state of ["PARTIAL","MISSING_TEST","INDEPENDENT_REVIEW"]) {
    const data=complete();
    data.scenarios[14].assertions[0].assessment=state;
    if(state==="MISSING_TEST")data.scenarios[14].assertions[0].witness=null;
    const result=checkPerAssertion(data,()=>({passed:true,artifact:"raw.trx"}));
    assert.equal(result.pass,false,state);
    assert.equal(result.technical_passes,71);
  }
});
test("rejects a duplicated, displaced or removed technical clause",()=>{
  for(const mutate of [
    x=>x.scenarios[0].assertions[1].id=x.scenarios[0].assertions[0].id,
    x=>x.scenarios[4].assertions.pop(),
    x=>x.scenarios.reverse()
  ]) {
    const data=complete();mutate(data);
    const result=checkPerAssertion(data,()=>({passed:true,artifact:"raw.trx"}));
    assert.equal(result.pass,false);
  }
});
test("cannot automatically claim an independent S25 decision",()=>{
  const data=complete();
  data.scenarios[24].assertions[0].assessment="TEST_AUTHORED";
  data.scenarios[24].assertions[0].witness={path:"test",case:"fake"};
  const result=checkPerAssertion(data,()=>({passed:true,artifact:"raw.trx"}));
  assert.equal(result.pass,false);
  assert.equal(result.independent_pending,2);
});
