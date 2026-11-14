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
