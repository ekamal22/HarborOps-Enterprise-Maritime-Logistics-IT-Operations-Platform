import test from "node:test";
import assert from "node:assert/strict";
import {recalculateP09} from "./g17-p09-review.mjs";
const samples=4;
function fixture() {
 const rows=[];
 for(let round=1;round<=3;round++)for(const enabled of [false,true]) {
  const value=enabled?10.5:10;
  rows.push({round,telemetry_enabled:enabled,rss_peak_mib:enabled?104:100,
   raw_ms:{baseline:[value,value,value,value],
     authorized:[value,value,value,value]},
   per_route:{baseline:{p95_ms:value},authorized:{p95_ms:value}}});
 }
 const pairs=[1,2,3].map(round=>({round,
   route_pairs:{baseline:{p95_increase_pct:5},
    authorized:{p95_increase_pct:5}},memory_increase_pct:4}));
 return {run_kind:"THREE_MATCHED_PAIRS",rows,pairs,
  pooled:{baseline:{telemetry_off_samples:12,telemetry_on_samples:12,
    telemetry_off_p95_ms:10,telemetry_on_p95_ms:10.5,p95_increase_pct:5},
   authorized:{telemetry_off_samples:12,telemetry_on_samples:12,
    telemetry_off_p95_ms:10,telemetry_on_p95_ms:10.5,p95_increase_pct:5}},
  gate:{method:"POOLED_P95_AND_MEAN_PEAK_MEMORY_OVER_THREE_MATCHED_PAIRS",
    p95_budget_pct:10,memory_budget_pct:15,
    mean_memory_increase_pct:4,pairs_over_p95_budget:0,pass:true},
  result:"PASS"};
}
test("raw P09 observations independently reproduce the approved pooled gate",()=>{
 assert.equal(recalculateP09(fixture(),samples).result,"PASS");
});
test("tampered raw samples, pairs, pooled gate and policy limits fail closed",()=>{
 for(const mutate of [
  x=>x.rows[0].raw_ms.baseline=[],
  x=>x.rows[0].round=2,
  x=>x.pairs[0].route_pairs.baseline.p95_increase_pct=0,
  x=>x.pooled.baseline.p95_increase_pct=0,
  x=>x.gate.mean_memory_increase_pct=0,
  x=>x.gate.pass=false,
  x=>x.gate.p95_budget_pct=11,
  x=>x.gate.pairs_over_p95_budget=1
 ]) {const x=fixture();mutate(x);assert.throws(()=>recalculateP09(x,samples));}
});

// S25-F05. Build a report from raw arrays with an oracle that is independent of the
// module under test (nearest-rank p95 written out here), so that both a failing pooled
// gate and a passing pooled gate with a disclosed individual outlier can be reproduced.
const rank95=values=>[...values].sort((a,b)=>a-b)[Math.ceil(values.length*0.95)-1];
const pctChange=(on,off)=>100*(on/off-1);
function build({n=40,off=10,on=10.5,round3High=null,rssOff=100,rssOn=110}) {
 const rows=[];
 for(let round=1;round<=3;round++)for(const enabled of [false,true]) {
  const base=enabled?on:off;
  const raw=Array.from({length:n},()=>base);
  if(enabled&&round===3&&round3High)for(let i=0;i<round3High.count;i++)raw[i]=round3High.value;
  rows.push({round,telemetry_enabled:enabled,rss_peak_mib:enabled?rssOn:rssOff,
   raw_ms:{baseline:[...raw],authorized:[...raw]},
   per_route:{baseline:{p95_ms:rank95(raw)},authorized:{p95_ms:rank95(raw)}}});
 }
 const find=(round,enabled)=>rows.find(r=>r.round===round&&r.telemetry_enabled===enabled);
 const pairs=[1,2,3].map(round=>{
  const increase=pctChange(find(round,true).per_route.baseline.p95_ms,find(round,false).per_route.baseline.p95_ms);
  return {round,route_pairs:{baseline:{p95_increase_pct:increase},authorized:{p95_increase_pct:increase}},
   memory_increase_pct:pctChange(find(round,true).rss_peak_mib,find(round,false).rss_peak_mib)};
 });
 const pooled={};
 for(const route of ["baseline","authorized"]) {
  const off1=[1,2,3].flatMap(r=>find(r,false).raw_ms[route]);
  const on1=[1,2,3].flatMap(r=>find(r,true).raw_ms[route]);
  pooled[route]={telemetry_off_samples:off1.length,telemetry_on_samples:on1.length,
   telemetry_off_p95_ms:rank95(off1),telemetry_on_p95_ms:rank95(on1),
   p95_increase_pct:pctChange(rank95(on1),rank95(off1))};
 }
 const mean=pairs.reduce((s,x)=>s+x.memory_increase_pct,0)/3;
 const pass=Object.values(pooled).every(x=>x.p95_increase_pct<=10)&&mean<=15;
 const outliers=pairs.filter(p=>Object.values(p.route_pairs).some(x=>x.p95_increase_pct>10)).length;
 return {run_kind:"THREE_MATCHED_PAIRS",rows,pairs,pooled,
  gate:{method:"POOLED_P95_AND_MEAN_PEAK_MEMORY_OVER_THREE_MATCHED_PAIRS",
   p95_budget_pct:10,memory_budget_pct:15,mean_memory_increase_pct:mean,
   pairs_over_p95_budget:outliers,pass},result:pass?"PASS":"FAIL"};
}
test("a pooled p95 above the approved ten percent budget is reported FAIL, never waived",()=>{
 const report=build({on:12});
 const reviewed=recalculateP09(report,40);
 assert.equal(reviewed.result,"FAIL");
 assert.ok(reviewed.pooled.baseline.increase_pct>10);
 // Quietly marking the failing report as passing is rejected.
 const lie=build({on:12});lie.gate.pass=true;lie.result="PASS";
 assert.throws(()=>recalculateP09(lie,40),/Expected values to be strictly equal/);
});
test("mean paired peak memory above fifteen percent is reported FAIL",()=>{
 const reviewed=recalculateP09(build({rssOn:117}),40);
 assert.equal(reviewed.result,"FAIL");
 assert.ok(reviewed.mean_memory_increase_pct>15);
 // Exactly the approved 15 percent budget still passes.
 assert.equal(recalculateP09(build({rssOn:115}),40).result,"PASS");
});
test("an individual third-pair outlier is disclosed while the approved pooled gate still passes",()=>{
 // Three of forty telemetry-on samples in the third round are slow: that round's own p95
 // is about 23 percent over its baseline, but pooling the three rounds keeps the p95 at 5.
 const report=build({round3High:{count:3,value:12.3}});
 const reviewed=recalculateP09(report,40);
 assert.equal(reviewed.result,"PASS");
 assert.ok(Math.abs(reviewed.pooled.baseline.increase_pct-5)<1e-9);
 assert.equal(reviewed.individual_p95_outliers.length,1);
 assert.equal(reviewed.individual_p95_outliers[0].round,3);
 assert.ok(Math.abs(reviewed.individual_p95_outliers[0].routes.baseline.increase_pct-23)<0.001);
 assert.equal(report.gate.pairs_over_p95_budget,1);
 // Hiding the outlier by under-reporting the count is rejected.
 const hidden=build({round3High:{count:3,value:12.3}});hidden.gate.pairs_over_p95_budget=0;
 assert.throws(()=>recalculateP09(hidden,40),/Expected values to be strictly equal/);
 // The approved limits are constants, not report-controlled.
 const loose=build({round3High:{count:3,value:12.3}});loose.gate.p95_budget_pct=25;
 assert.throws(()=>recalculateP09(loose,40),/Expected values to be strictly equal/);
});
test("a recorded pooled p95 that raw samples cannot reproduce is rejected",()=>{
 const report=build({});
 report.pooled.baseline.telemetry_on_p95_ms=9;
 report.pooled.baseline.p95_increase_pct=-10;
 assert.throws(()=>recalculateP09(report,40),/not reproducible from raw observations/);
});

// Input guards and exact budget edges, found by mutation analysis of the recalculation.
test("empty, non-positive or non-finite raw samples are rejected",()=>{
 for(const bad of [[],[0],[-1],[NaN],[Infinity],[1,2,0]]) {
  const report=build({});
  report.rows[0].raw_ms.baseline=bad.length?[...bad,...Array(Math.max(0,40-bad.length)).fill(10)]:[];
  assert.throws(()=>recalculateP09(report,bad.length?40:0),/./,JSON.stringify(bad));
 }
});
test("a non-positive peak memory reading or a missing telemetry mode is rejected",()=>{
 const memory=build({});memory.rows[1].rss_peak_mib=0;
 assert.throws(()=>recalculateP09(memory,40));
 const negative=build({});negative.rows[2].rss_peak_mib=-5;
 assert.throws(()=>recalculateP09(negative,40));
 const missing=build({});missing.rows[5].telemetry_enabled=false;
 assert.throws(()=>recalculateP09(missing,40),/Duplicate paired mode|pair|ok/i);
});
test("pooled p95 and memory budgets are inclusive at their approved values",()=>{
 // 9.9 percent passes and 10.1 percent fails: the ten percent edge is decided by the raw ratio.
 assert.equal(recalculateP09(build({on:10.99}),40).result,"PASS");
 assert.equal(recalculateP09(build({on:11.01}),40).result,"FAIL");
 assert.equal(recalculateP09(build({rssOn:114.9}),40).result,"PASS");
 assert.equal(recalculateP09(build({rssOn:115.1}),40).result,"FAIL");
});

test("a negative peak memory reading is rejected even when every derived number is consistent with it", () => {
 const report = build({rssOff: -100, rssOn: -110});
 assert.throws(() => recalculateP09(report, 40));
 const zero = build({rssOff: 0.0001, rssOn: 0});
 assert.throws(() => recalculateP09(zero, 40));
});
test("a mathematically exact ten percent increase is reported as exceeding because of floating point arithmetic", () => {
 // Documented observation (S25-M05), not a policy change: 100 * (11 / 10 - 1) evaluates to
 // 10.000000000000009, so an increase of exactly ten percent is judged to exceed the budget. The
 // recalculation fails closed at that single edge; real measurements never land on it.
 const reviewed = recalculateP09(build({on: 11}), 40);
 assert.equal(reviewed.result, "FAIL");
 assert.ok(reviewed.pooled.baseline.increase_pct > 10 && reviewed.pooled.baseline.increase_pct < 10.0000001);
 assert.equal(recalculateP09(build({on: 10.99999}), 40).result, "PASS");
});
test("a recorded pair percentage is accepted at exactly the approved tolerance and refused just beyond it", () => {
 // The recomputed pair increase is exactly 0 when both modes have the same p95, so the difference to the
 // recorded value is the recorded value itself, and 0.00005 is the approved inclusive tolerance.
 const atEdge = build({on: 10});
 atEdge.pairs[0].route_pairs.baseline.p95_increase_pct = 0.00005;
 assert.equal(recalculateP09(atEdge, 40).result, "PASS");
 const beyond = build({on: 10});
 beyond.pairs[0].route_pairs.baseline.p95_increase_pct = 0.00006;
 assert.throws(() => recalculateP09(beyond, 40), /not reproducible/);
});
