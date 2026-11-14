import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {auditG17Coverage} from "./audit-g17-test-coverage.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const baseline = JSON.parse(fs.readFileSync(path.join(root,
  "docs/M17_G17_ASSERTION_INVENTORY_20261112.json"),"utf8"));
const mapped = JSON.parse(fs.readFileSync(path.join(root,
  "docs/M17_G17_TEST_COVERAGE_20261113.json"),"utf8"));
const read = file => fs.readFileSync(path.join(root,file),"utf8");
const clone = data => JSON.parse(JSON.stringify(data));
function syntheticComplete() {
  const map = clone(mapped);
  for (const scenario of map.scenarios.slice(0,24)) {
    for (const a of scenario.assertions) {
      a.assessment = "TEST_AUTHORED";
      a.witness = {path:"tests/g17-proof.spec.mjs",case:"synthetic proof"};
      a.coverage_gap = "";
    }
  }
  return map;
}
test("preflight refuses every remaining incomplete technical assertion", () => {
  const result=auditG17Coverage(baseline,mapped,read,{strict:true});
  const incomplete=mapped.scenarios.slice(0,24).flatMap(s=>s.assertions)
    .filter(a=>a.assessment!=="TEST_AUTHORED");
  assert.equal(result.scenarios,25);
  assert.equal(result.assertions,75);
  assert.equal(result.technical_required,72);
  assert.equal(result.independent_review_required,3);
  if (incomplete.length) {
    assert.equal(result.passed,false);
    for (const clause of incomplete)
      assert.ok(result.errors.some(x=>x.includes(clause.id)),clause.id);
  } else assert.equal(result.passed,true,result.errors.join("\n"));
});
test("non-strict inventory audit maps exactly 75 clauses without corrupt identities", () => {
  const result=auditG17Coverage(baseline,mapped,read,{strict:false});
  assert.equal(result.scenarios,25);
  assert.equal(result.assertions,75);
  assert.equal(result.stats.INDEPENDENT_REVIEW,3);
  assert.equal(result.errors.length,0,result.errors.join("\n"));
});
test("a fully authored synthetic inventory satisfies the structural contract", () => {
  const fake=syntheticComplete();
  const result=auditG17Coverage(baseline,fake,
    path=>path==="tests/g17-proof.spec.mjs"?'test("synthetic proof",()=>assert.ok(true));':"",
    {strict:true});
  assert.equal(result.passed,true,result.errors.join("\n"));
});
test("a skipped technical test is rejected even if assigned", () => {
  const fake=syntheticComplete();
  const result=auditG17Coverage(baseline,fake,
    ()=>'test.skip("synthetic proof",()=>{});',{strict:true});
  assert.ok(result.errors.some(x=>x.includes("skip or ignored")));
  assert.equal(result.passed,false);
});
test("missing, duplicated or permuted scenario identities are rejected", () => {
  for(const mutate of [
    f=>f.scenarios.pop(),
    f=>f.scenarios[0].assertions[1].id=f.scenarios[0].assertions[0].id,
    f=>f.scenarios.reverse()
  ]) {
    const fake=clone(mapped);mutate(fake);
    const result=auditG17Coverage(baseline,fake,read);
    assert.equal(result.passed,false);
  }
});
test("S25 cannot be credited as an automated test or used as a skip exemption", () => {
  const fake=syntheticComplete();
  fake.scenarios[24].assertions[0].assessment="TEST_AUTHORED";
  fake.scenarios[24].assertions[0].witness={path:"tests/g17-proof.spec.mjs",case:"synthetic proof"};
  const result=auditG17Coverage(baseline,fake,
    ()=>'test("synthetic proof",()=>{});',{strict:true});
  assert.equal(result.passed,false);
  assert.ok(result.errors.some(x=>x.includes("Independent S25")));
});
test("wrong source text and a test renamed away cannot satisfy a witness", () => {
  const fake=syntheticComplete();
  const result=auditG17Coverage(baseline,fake,
    ()=>'test("different implementation",()=>{});',{strict:true});
  assert.equal(result.passed,false);
  assert.ok(result.errors.some(x=>x.includes("absent from source")));
});
