import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

// S25-F01. Builds a real below-threshold traffic window on an otherwise idle owned
// stack: a few dozen genuine business reads, fewer than the approved 100 eligible
// requests. The raw unguarded 5xx rate is a healthy-looking zero in that window;
// the guarded dashboard series must be absent so the rendered panels say Unknown.
// The browser specification later renders exactly this recorded window.
const project=process.env.HARBOROPS_G17_PROJECT;
const source=process.env.HARBOROPS_SOURCE_SHA;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const password=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence)&&password);
const base="http://127.0.0.1:18080";
const proxy="http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-metrics/api/v1/";
const auth="Basic "+Buffer.from("local_operator:"+password).toString("base64");
const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const THRESHOLD=100;
async function api(endpoint,params) {
  const url=new URL(proxy+endpoint);
  for(const [key,value] of Object.entries(params))url.searchParams.set(key,String(value));
  const response=await fetch(url,{headers:{authorization:auth},signal:AbortSignal.timeout(15000)});
  assert.equal(response.status,200,endpoint+" failed");
  const body=await response.json();
  assert.equal(body.status,"success");
  return body.data.result;
}
const instant=expr=>api("query",{query:expr});
const range=(expr,start,end)=>api("query_range",{query:expr,start,end,step:15});
const points=result=>result.flatMap(series=>series.values.map(([t,v])=>({t:Number(t),v:Number(v)})));
async function poll(label,fn,ms=150000) {
  const end=Date.now()+ms;
  while(Date.now()<end) {
    try {const value=await fn();if(value)return value;} catch {}
    await wait(4000);
  }
  throw Error("Timed out before "+label);
}
const report={source_sha:source,project,result:"FAIL",requests_sent:0};
let failure=null;
try {
  assert.equal((await fetch(base+"/health/ready")).status,200);
  const idle=await instant("sum(increase(harborops_request_duration_seconds_count[5m]))");
  const prior=idle.length?Number(idle[0].value[1]):0;
  assert.ok(prior<5,"Stack is not idle; a below-threshold traffic window cannot be constructed ("+prior+")");
  report.idle_prior_eligible=prior;
  const startedAt=Math.floor(Date.now()/1000);
  // Four spaced rounds so that at least two OTLP exports and Prometheus scrapes
  // observe a real increase. Total stays far below the approved 100 request floor.
  for(let round=0;round<4;round++) {
    for(let i=0;i<8;i++) {
      const response=await fetch(base+"/api/v1/system/baseline",{signal:AbortSignal.timeout(10000)});
      assert.equal(response.status,200);
      report.requests_sent++;
    }
    await wait(20000);
  }
  assert.ok(report.requests_sent>0&&report.requests_sent<THRESHOLD);
  const eligible=await poll("a recorded positive below-threshold eligible-request sample",async()=>{
    const rows=await instant("harborops:m17:eligible_requests:5m");
    if(rows.length!==1)return null;
    const value=Number(rows[0].value[1]);
    return value>=1&&value<THRESHOLD?value:null;
  });
  // The failure counter exists and is zero, which is precisely the hazard.
  const counter=await instant('harborops_request_failures_total{operation="business",status_class="5xx"}');
  assert.equal(counter.length,1,"Zero-seeded failure counter is required for the false-healthy control");
  const counterValue=Number(counter[0].value[1]);
  assert.ok(Number.isFinite(counterValue)&&counterValue>=0);
  // On a fresh stack the first positive eligible-request sample only appears once two
  // scrapes have seen an increase. No further traffic is sent: simply wait, within a bound,
  // until at least six recorded below-threshold evaluation points exist (the five minute
  // window keeps them below the floor).
  let end=Math.floor(Date.now()/1000),history=[];
  await poll("six recorded below-threshold evaluation points",async()=>{
    end=Math.floor(Date.now()/1000);
    history=points(await range("harborops:m17:eligible_requests:5m",startedAt-30,end))
      .filter(point=>point.v>=1&&point.v<THRESHOLD);
    return history.length>=6;
  },180000);
  assert.ok(history.length>=4,"Fewer than four recorded below-threshold evaluation points");
  const windowStart=history[0].t,windowEnd=history[history.length-1].t;
  const eligibleValues=history.map(point=>point.v);
  const guarded5xx=await range("harborops:m17:api_5xx_rate_sufficient:5m",windowStart,windowEnd);
  const guardedP95=await range("harborops:m17:api_p95_seconds_sufficient:5m",windowStart,windowEnd);
  assert.equal(points(guarded5xx).length,0,"Guarded 5xx series exists below the sufficiency floor");
  assert.equal(points(guardedP95).length,0,"Guarded p95 series exists below the sufficiency floor");
  const raw5xx=points(await range("sum(rate(harborops_request_failures_total[5m]))",windowStart,windowEnd));
  assert.ok(raw5xx.length>=1&&raw5xx.every(point=>point.v===0),
    "The raw 5xx rate must be a measured healthy-looking zero in this window");
  const rate=points(await range("sum(rate(harborops_request_duration_seconds_count[5m]))",windowStart,windowEnd));
  assert.ok(rate.some(point=>point.v>0),"The request rate must be measurably positive in this window");
  Object.assign(report,{window_start:windowStart,window_end:windowEnd,
    eligible_points:history.length,eligible_min:Math.min(...eligibleValues),
    eligible_max:Math.max(...eligibleValues),eligible_instant:eligible,
    sufficiency_floor:THRESHOLD,guarded_5xx_points:0,guarded_p95_points:0,
    raw_5xx_zero_points:raw5xx.length,request_rate_max:Math.max(...rate.map(point=>point.v)),
    failure_counter_value:counterValue,result:"PASS"});
}catch(error) {
  failure=error;
  report.failure_message=String(error?.message??"").slice(0,240);
}
fs.writeFileSync(path.join(evidence,"m17-low-traffic-window.json"),
  JSON.stringify(report,null,2)+"\n",{flag:"wx"});
if(failure)throw failure;
