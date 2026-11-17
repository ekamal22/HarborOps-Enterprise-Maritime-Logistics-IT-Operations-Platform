import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import {fileURLToPath} from "node:url";
import {checkPerAssertion,nodeTestNames} from "./reconcile-g17-assertions.mjs";

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

test("JUnit case names come from the name attribute, not from classname",()=>{
  // node:test writes classname="test" on every case. A greedy match on name=" would read the
  // tail of classname and collapse every witness into the single name "test".
  const xml='<testsuites>'+
    '<testcase name="first witness" time="0.1" classname="test" file="a.test.mjs"/>'+
    '<testcase name="second witness" time="0.2" classname="test" file="b.test.mjs"/></testsuites>';
  const names=nodeTestNames(xml);
  assert.deepEqual([...names].sort(),["first witness","second witness"]);
  assert.throws(()=>nodeTestNames('<testsuites><testcase name="x" classname="test"/>'+
    '<testcase name="x" classname="test"/></testsuites>'),/unique/);
});

// Second-pass material survivors (S25-M series): each fault is isolated, so no other guard reports it.
test("a mislabeled scenario or an extra fourth clause is reported on its own", () => {
  const pass = () => ({passed: true, artifact: "raw.trx"});
  const relabeled = complete();
  relabeled.scenarios[3].id = "G17-S99";
  assert.ok(checkPerAssertion(relabeled, pass).errors.some(x => /Missing or reordered scenario: G17-S04/.test(x)));
  const extra = complete();
  extra.scenarios[3].assertions.push({id: "G17-S04-A04", assessment: "TEST_AUTHORED",
    witness: {path: "tests/mock-technical.test.mjs", case: "G17-S04-A04"}});
  assert.ok(checkPerAssertion(extra, pass).errors.some(x => /Missing or reordered scenario: G17-S04/.test(x)));
});
test("an S25 clause must be both independent review and unwitnessed", () => {
  const pass = () => ({passed: true, artifact: "raw.trx"});
  const authored = complete();
  authored.scenarios[24].assertions[1].assessment = "TEST_AUTHORED";
  authored.scenarios[24].assertions[1].witness = null;
  assert.ok(checkPerAssertion(authored, pass).errors.some(x => x.startsWith("G17-S25-A02: independent reviewer must remain unassigned")));
  const witnessed = complete();
  witnessed.scenarios[24].assertions[2].witness = {path: "tests/x.test.mjs", case: "x"};
  assert.ok(checkPerAssertion(witnessed, pass).errors.some(x => x.startsWith("G17-S25-A03: independent reviewer must remain unassigned")));
  assert.equal(checkPerAssertion(witnessed, pass).records[74].status, "INVALID_INDEPENDENT_REVIEW_CLAIM");
});
