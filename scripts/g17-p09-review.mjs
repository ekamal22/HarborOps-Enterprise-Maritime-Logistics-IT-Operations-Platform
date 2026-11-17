import assert from "node:assert/strict";

function p95(values) {
  assert.ok(Array.isArray(values)&&values.length>0);
  for(const v of values)assert.ok(Number.isFinite(v)&&v>0);
  const sorted=[...values].sort((a,b)=>a-b);
  return sorted[Math.ceil(sorted.length*0.95)-1];
}
const pct=(after,before)=>100*(after/before-1);
const near=(actual,expected,label,tolerance=0.00005)=>{
  assert.ok(Number.isFinite(actual)&&Math.abs(actual-expected)<=tolerance,
    "Recorded P09 "+label+" is not reproducible from raw observations");
};
export function recalculateP09(report,perRouteSamples=1200) {
  assert.equal(report.run_kind,"THREE_MATCHED_PAIRS");
  assert.equal(report.gate.method,
    "POOLED_P95_AND_MEAN_PEAK_MEMORY_OVER_THREE_MATCHED_PAIRS");
  assert.equal(report.gate.p95_budget_pct,10);
  assert.equal(report.gate.memory_budget_pct,15);
  assert.equal(report.rows?.length,6);
  assert.equal(report.pairs?.length,3);
  const modes=new Map();
  for(const row of report.rows) {
    assert.ok([1,2,3].includes(row.round));
    assert.equal(typeof row.telemetry_enabled,"boolean");
    const key=row.round+":"+row.telemetry_enabled;
    assert.ok(!modes.has(key),"Duplicate paired mode");
    assert.ok(Number.isFinite(row.rss_peak_mib)&&row.rss_peak_mib>0);
    for(const route of ["baseline","authorized"]) {
      assert.equal(row.raw_ms?.[route]?.length,perRouteSamples);
      near(p95(row.raw_ms[route]),row.per_route[route].p95_ms,
        key+"/"+route+" p95",0.011);
    }
    modes.set(key,row);
  }
  const pairs=[];
  for(let round=1;round<=3;round++) {
    const off=modes.get(round+":false"),on=modes.get(round+":true");
    assert.ok(off&&on);
    const pair=report.pairs.find(x=>x.round===round);
    assert.ok(pair);
    const routes={};
    for(const route of ["baseline","authorized"]) {
      const offP=off.per_route[route].p95_ms,onP=on.per_route[route].p95_ms;
      near(pair.route_pairs[route].p95_increase_pct,pct(onP,offP),
        "pair "+round+"/"+route);
      routes[route]={off_p95_ms:offP,on_p95_ms:onP,
        increase_pct:pct(onP,offP)};
    }
    const memoryIncrease=pct(on.rss_peak_mib,off.rss_peak_mib);
    near(pair.memory_increase_pct,memoryIncrease,"pair "+round+" memory");
    pairs.push({round,routes,memory_increase_pct:memoryIncrease,
      exceeds_individual_p95_budget:Object.values(routes)
        .some(x=>x.increase_pct>10)});
  }
  const pooled={};
  for(const route of ["baseline","authorized"]) {
    const samples=enabled=>[1,2,3].flatMap(round=>
      modes.get(round+":"+enabled).raw_ms[route]);
    const off=p95(samples(false)),on=p95(samples(true));
    const increase=pct(on,off);
    const recorded=report.pooled[route];
    assert.equal(recorded.telemetry_off_samples,3*perRouteSamples);
    assert.equal(recorded.telemetry_on_samples,3*perRouteSamples);
    near(recorded.telemetry_off_p95_ms,off,"pooled "+route+" off");
    near(recorded.telemetry_on_p95_ms,on,"pooled "+route+" on");
    near(recorded.p95_increase_pct,increase,"pooled "+route+" increase");
    pooled[route]={off_p95_ms:off,on_p95_ms:on,increase_pct:increase};
  }
  const meanMemory=pairs.reduce((s,x)=>s+x.memory_increase_pct,0)/3;
  near(report.gate.mean_memory_increase_pct,meanMemory,"mean memory");
  const pass=Object.values(pooled).every(x=>x.increase_pct<=10)&&meanMemory<=15;
  assert.equal(report.gate.pass,pass);
  assert.equal(report.result,pass?"PASS":"FAIL");
  assert.equal(report.gate.pairs_over_p95_budget,
    pairs.filter(x=>x.exceeds_individual_p95_budget).length);
  return {result:pass?"PASS":"FAIL",
    method:report.gate.method,latency_budget_pct:10,memory_budget_pct:15,
    pooled,mean_memory_increase_pct:meanMemory,pairs,
    individual_p95_outliers:pairs.filter(x=>x.exceeds_individual_p95_budget),
    qualification:"Individual p95 exceedances are disclosed, not waived by silence"};
}
