import test from "node:test";
import assert from "node:assert/strict";
import {verifyPlaywrightCases,verifyTrxCases} from "./g17-results-contract.mjs";

const titles=[
  "dashboard requires authentication and shows actual monitoring panels",
  "authenticated samples match the disposable ready API and suppress held workers",
  "dashboard is unavailable to an anonymous browser context"
];
const engines=["chromium","firefox","webkit"];
function browser(mode) {
  const dashboard=mode==="dashboard";
  const names=dashboard?titles:["ten thousand hostile HTTP requests remain within complete exported-series budget"];
  const projects=dashboard?engines:["chromium"];
  const specs=names.map(title=>({
    title,ok:true,tests:projects.map(projectName=>({
      projectName,expectedStatus:"passed",status:"expected",
      results:[{status:"passed",retry:0}]
    }))
  }));
  return {suites:[{specs}],stats:{
    expected:names.length*projects.length,unexpected:0,skipped:0,flaky:0
  }};
}
const trx=names=>{
  const results=names.map(name=>'<UnitTestResult testName="Suite.'+name+
    '" outcome="Passed" />').join("");
  return '<TestRun><ResultSummary><Counters total="'+names.length+
    '" passed="'+names.length+'" failed="0" notExecuted="0" /></ResultSummary>'+
    results+'</TestRun>';
};
test("all nine required browser and one cardinality case pass exact identity checks",()=>{
  assert.equal(verifyPlaywrightCases(browser("dashboard"),"dashboard").passed,9);
  assert.equal(verifyPlaywrightCases(browser("cardinality"),"cardinality").passed,1);
});
test("browser skip, retry, expected failure, extra case or missing engine fails",()=>{
  for(const mutate of [
    a=>a.stats.skipped=1,
    a=>a.suites[0].specs[0].tests[0].results[0].retry=1,
    a=>a.suites[0].specs[0].tests[0].expectedStatus="failed",
    a=>a.suites[0].specs[0].tests[0].status="unexpected",
    a=>a.suites[0].specs[0].tests.shift(),
    a=>a.suites[0].specs.push(a.suites[0].specs[0])
  ]) {
    const report=browser("dashboard"); mutate(report);
    assert.throws(()=>verifyPlaywrightCases(report,"dashboard"));
  }
});
test("TRX requires raw UnitTestResult outcomes and exactly one named method",()=>{
  assert.equal(verifyTrxCases(trx(["RequiredMethod","OtherMethod"]),
    ["RequiredMethod"]).passed,2);
  assert.throws(()=>verifyTrxCases(trx(["OtherMethod"]),["RequiredMethod"]),
    /Required M17 method/);
});
test("TRX rejects skips, duplicate cases, inaccurate counters or fake wrapper passes",()=>{
  for(const raw of [
    trx(["A"]).replace('outcome="Passed"','outcome="NotExecuted"'),
    trx(["A","A"]),
    trx(["A"]).replace('passed="1"','passed="0"'),
    trx(["A"]).replace('total="1"','total="2"'),
    '<TestRun><Counters total="1" passed="1" failed="0" notExecuted="0" /></TestRun>'
  ]) assert.throws(()=>verifyTrxCases(raw));
});
