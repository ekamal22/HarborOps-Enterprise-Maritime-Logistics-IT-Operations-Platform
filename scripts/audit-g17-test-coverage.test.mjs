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

// Each row breaks exactly one fact of the real, otherwise valid mapping. The audit must
// report an error for that fact alone, so no guard may hide behind another one.
const real = (mutate, readSource = read, options = {strict: false}) => {
  const inventory = clone(baseline), map = clone(mapped);
  mutate(inventory, map);
  return auditG17Coverage(inventory, map, readSource, options);
};
const first = map => map.scenarios[0].assertions[0];
test("every single structural fault of the inventory or the mapping is reported", () => {
  const faults = [
    ["wrong coverage gate", (i, m) => { m.gate = "G16"; }, /coverage schema/],
    ["wrong coverage schema", (i, m) => { m.schema_version = 2; }, /coverage schema/],
    ["inventory with 24 scenarios", (i, m) => { i.scenarios.pop(); }, /25 inventory scenarios/],
    ["inventory scenarios not an array", (i, m) => { i.scenarios = "x"; }, /25 inventory scenarios/],
    ["mapping with 24 scenarios", (i, m) => { m.scenarios.pop(); }, /25 independently mapped/],
    ["mapping scenarios not an array", (i, m) => { m.scenarios = {}; }, /25 independently mapped/],
    ["inventory scenario with two clauses", (i, m) => { i.scenarios[3].assertions.pop(); }, /exactly three atomic clauses/],
    ["mapping scenario with two clauses", (i, m) => { m.scenarios[3].assertions.pop(); }, /exactly three atomic clauses/],
    ["scenario renamed in the mapping", (i, m) => { m.scenarios[2].name = "Other"; }, /Scenario name differs/],
    ["expected text differs", (i, m) => { first(m).expected = "other"; }, /Contradictory unexecuted assertion baseline/],
    ["inventory assertion already executed", (i, m) => { i.scenarios[0].assertions[0].status = "PASS"; }, /Contradictory unexecuted assertion baseline/],
    ["inventory assertion has a witness", (i, m) => { i.scenarios[0].assertions[0].witness = "x"; }, /Contradictory unexecuted assertion baseline/],
    ["unknown classification", (i, m) => { first(m).assessment = "DONE"; }, /Unknown test-coverage classification/],
    ["gate marked accepted", (i, m) => { i.gate_acceptance = "ACCEPTED"; }, /must remain unaccepted/],
    ["independent clause marked as a test", (i, m) => { m.scenarios[24].assertions[0].assessment = "TEST_AUTHORED"; }, /Independent S25 may not be marked/],
    ["technical clause marked as manual review", (i, m) => { first(m).assessment = "INDEPENDENT_REVIEW"; }, /may not be skipped as manual review/],
    ["missing test claiming a witness", (i, m) => { first(m).assessment = "MISSING_TEST"; }, /Missing test must not claim executable evidence/],
    ["witness path not a string", (i, m) => { first(m).witness.path = 5; }, /Absent or unsafe named technical witness/],
    ["witness case not a string", (i, m) => { first(m).witness.case = 5; }, /Absent or unsafe named technical witness/],
    ["witness case blank", (i, m) => { first(m).witness.case = "  "; }, /Absent or unsafe named technical witness/],
    ["witness path with a space", (i, m) => { first(m).witness.path = "scripts/a b.mjs"; }, /Absent or unsafe named technical witness/],
    ["witness path absolute", (i, m) => { first(m).witness.path = "/etc/passwd"; }, /Absent or unsafe named technical witness/],
    ["witness path traversal", (i, m) => { first(m).witness.path = "scripts/../x.mjs"; }, /Absent or unsafe named technical witness/],
    ["no witness at all", (i, m) => { first(m).witness = null; }, /Absent or unsafe named technical witness/],
    ["named case absent from its source", (i, m) => { first(m).witness.case = "no such case anywhere"; }, /Named test or behavior absent from source/]
  ];
  for (const [label, mutate, pattern] of faults) {
    const result = real(mutate);
    assert.ok(result.errors.some(error => pattern.test(error)),
      label + " was not reported: " + JSON.stringify(result.errors.slice(0, 3)));
  }
});
test("skip and ignore markers in a named witness source are refused one pattern at a time", () => {
  for (const marker of ["test.skip(", "describe.fixme (", "test.todo(", "xit(", "xdescribe(",
      "[Fact(Skip = \"x\")]", "[Theory(Skip = \"x\")]", "continue-on-error: true"]) {
    const result = real(() => {}, file => read(file) + "\n" + marker + "\n");
    assert.ok(result.errors.some(error => /skip or ignored failure/.test(error)), marker);
  }
  assert.equal(real(() => {}, file => read(file) + "\nthe word skip alone\n").errors.length, 0);
});
test("strict mode and total counts reject partial coverage", () => {
  const partial = real((i, m) => { first(m).assessment = "PARTIAL"; }, read, {strict: true});
  assert.ok(partial.errors.some(error => /blocks untested or partially tested clause/.test(error)));
  const fewer = real((i, m) => { first(m).assessment = "PARTIAL"; }, read, {strict: true});
  assert.ok(fewer.errors.some(error => /72 individually authored witnesses/.test(error)));
  const independent = real((i, m) => { m.scenarios[24].assertions[1].assessment = "TEST_AUTHORED"; });
  assert.ok(independent.errors.some(error => /Exactly three S25 reviewer assertions/.test(error)));
  assert.equal(real(() => {}, read, {strict: true}).passed, true);
});
