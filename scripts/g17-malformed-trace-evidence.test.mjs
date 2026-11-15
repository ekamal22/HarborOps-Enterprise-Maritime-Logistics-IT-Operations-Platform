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

test("window edges are inclusive, the operation must be the baseline read and inputs are validated",()=>{
  // Span at second 1700000000: the first and last second of the window both count.
  assert.equal(verifyMalformedBaselineTrace(trace,1700000000,1700000000,"unique-canary"),true);
  assert.equal(verifyMalformedBaselineTrace(trace,1699999999,1700000000,"unique-canary"),true);
  assert.equal(verifyMalformedBaselineTrace(trace,1700000001,1700000002,"unique-canary"),false);
  assert.equal(verifyMalformedBaselineTrace(trace,1699999990,1699999999,"unique-canary"),false);
  const other=structuredClone(trace);
  other.trace.resourceSpans[0].scopeSpans[0].spans[0].attributes[0].value.stringValue="other_read";
  assert.equal(verifyMalformedBaselineTrace(other,1700000000,1700000001,"unique-canary"),false);
  const key=structuredClone(trace);
  key.trace.resourceSpans[0].scopeSpans[0].spans[0].attributes[0].key="harborops.other";
  assert.equal(verifyMalformedBaselineTrace(key,1700000000,1700000001,"unique-canary"),false);
  assert.throws(()=>verifyMalformedBaselineTrace(trace,1700000001,1700000000,"unique-canary"));
  assert.throws(()=>verifyMalformedBaselineTrace(trace,1.5,1700000001,"unique-canary"));
  assert.throws(()=>verifyMalformedBaselineTrace(trace,1700000000,1700000001,""));
  assert.throws(()=>verifyMalformedBaselineTrace(trace,1700000000,1700000001,undefined));
  assert.throws(()=>verifyMalformedBaselineTrace(trace,1700000000,1700000001,42));
});
test("a canary that is not a non-empty string is refused by the canary assertion itself, not by a later accident",()=>{
  // The canary assertion raises an AssertionError. A later TypeError (toLowerCase on a non-string) or the
  // "canary reached" assertion must not be what refuses these inputs.
  const refusedByCanaryAssertion=error=>error.name==="AssertionError"&&!/canary reached/.test(error.message);
  for(const canary of [undefined,null,"",0,5,{},["unique-canary"],true])
    assert.throws(()=>verifyMalformedBaselineTrace(trace,1700000000,1700000001,canary),refusedByCanaryAssertion,String(canary));
});
import {tempoSearchWindow} from "./g17-malformed-trace-evidence.mjs";
// Tempo caps a search end at its own clock, so the query window must stay open when the injected requests all finish
// within the second in which they began (a quiet, fast host). The legacy window started at that second and collapsed.
const cappedWidth=(window,now)=>Math.min(window.end,now)-window.start;
test("the Tempo search window stays open when every request finishes in the second it began",()=>{
  const started=1794741593,ended=started+1;
  const window=tempoSearchWindow(started,ended);
  for(const now of [started,started+0.5,started+1,started+30,started+200]) {
    assert.ok(cappedWidth(window,now)>0,"window collapsed at clock "+now);
  }
  // The legacy window (start at the first second) collapses exactly in that case, which Tempo rejects with 400.
  assert.equal(cappedWidth({start:started,end:ended+45},started),0);
});
test("the Tempo search window starts one second early, ends 45 seconds late and rejects an invalid source window",()=>{
  assert.deepEqual(tempoSearchWindow(1000,1001),{start:999,end:1046});
  assert.deepEqual(tempoSearchWindow(1000,1000),{start:999,end:1045});
  for(const [a,b] of [[1000.5,1001],[1000,1001.5],[1001,1000],[undefined,1000],[1000,undefined]])
    assert.throws(()=>tempoSearchWindow(a,b),undefined,String([a,b]));
});
