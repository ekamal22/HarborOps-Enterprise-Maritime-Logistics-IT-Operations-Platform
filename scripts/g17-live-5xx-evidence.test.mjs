import test from "node:test";
import assert from "node:assert/strict";
import {validateLive5xxEvidence} from "./g17-live-5xx-evidence.mjs";
const good={baseline_counter:0,observed_counter:121,
 failed_increase_5m:120,eligible_requests_5m:121,
 failure_fraction_5m:0.99,requested_503_responses:120};
test("real exported counter increase, volume and fraction are all required",()=>{
  assert.equal(validateLive5xxEvidence(good),true);
  for(const bad of [
    {observed_counter:0},{failed_increase_5m:0},{eligible_requests_5m:99},
    {failure_fraction_5m:0.01},{failure_fraction_5m:1.1},
    {requested_503_responses:119},{baseline_counter:121}
  ])assert.throws(()=>validateLive5xxEvidence({...good,...bad}));
});
test("invalid or missing measured samples fail closed",()=>{
  for(const value of [NaN,Infinity,-1,undefined,null])
    assert.throws(()=>validateLive5xxEvidence({...good,failed_increase_5m:value}));
});
