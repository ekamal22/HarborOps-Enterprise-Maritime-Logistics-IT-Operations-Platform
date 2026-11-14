import test from "node:test";
import assert from "node:assert/strict";
import {verifyMalformedBaselineTrace} from "./g17-malformed-trace-evidence.mjs";
const trace={trace:{resourceSpans:[{scopeSpans:[{spans:[{
  startTimeUnixNano:"1700000000500000000",
  attributes:[{key:"harborops.operation",value:{stringValue:"baseline_read"}}],
  events:[],links:[]
}]}]}]}};
test("real malformed-parent witness requires a source-time baseline span",()=>{
  assert.equal(verifyMalformedBaselineTrace(trace,1700000000,1700000001,"unique-canary"),true);
  assert.equal(verifyMalformedBaselineTrace(trace,1700000002,1700000010,"unique-canary"),false);
  assert.equal(verifyMalformedBaselineTrace({trace:{resourceSpans:[]}},1700000000,1700000001,"unique-canary"),false);
});
test("exported malformed context and secret fields fail closed",()=>{
  const bad=structuredClone(trace);
  bad.trace.resourceSpans[0].scopeSpans[0].spans[0].attributes.push(
    {key:"http.request.header.cookie",value:{stringValue:"private"}});
  assert.throws(()=>verifyMalformedBaselineTrace(bad,1700000000,1700000001,"unique-canary"));
  const canary=structuredClone(trace);
  canary.trace.resourceSpans[0].scopeSpans[0].spans[0].name="unique-canary";
  assert.throws(()=>verifyMalformedBaselineTrace(canary,1700000000,1700000001,"unique-canary"));
  const state=structuredClone(trace);
  state.trace.resourceSpans[0].scopeSpans[0].spans[0].traceState="private=secret";
  assert.throws(()=>verifyMalformedBaselineTrace(state,1700000000,1700000001,"unique-canary"));
});
