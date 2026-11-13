import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const project=process.env.HARBOROPS_G17_PROJECT;
const source=process.env.HARBOROPS_SOURCE_SHA;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence));
const read=file=>JSON.parse(fs.readFileSync(path.join(evidence,file),"utf8"));
let error=null;
const result={source_sha:source,project,result:"FAIL",
  technical_acceptance:"PENDING",cases:[]};
try{
  const collector=read("m17-collector-fault-results.json");
  const performance=read("m17-performance-raw.json");
  const cardinality=read("m17-cardinality-memory-results.json");
  const storage=read("m17-storage-results.json");
  for(const report of [collector,performance,cardinality,storage]){
    assert.equal(report.source_sha,source);
    assert.equal(report.project,project);
    assert.equal(report.result,"PASS");
  }
  const isolating=collector.checks.find(x=>
    x.name==="collector-fault-business-isolation");
  assert.equal(isolating?.result,"PASS");
  assert.ok(collector.measurements.some(x=>
    x.requests_completed===4000&&x.concurrency===80&&
    x.baseline_concurrency===8&&x.concurrency_multiplier===10),
    "No 10x concurrent completed business requests during collector outage");
  result.cases.push({name:"business-under-tenfold-pressure-and-exporter-outage",
    status:"PASS",source_artifact:"m17-collector-fault-results.json"});
  assert.equal(performance.run_kind,"THREE_MATCHED_PAIRS");
  assert.deepEqual(performance.measured_routes,["baseline","authorized"]);
  assert.equal(performance.rows.length,6);
  for(const row of performance.rows){
    assert.equal(row.requests,800);
    for(const type of ["baseline","authorized"])
      assert.equal(row.per_route?.[type]?.requests,400);
  }
  for(const pair of performance.pairs){
    assert.equal(pair.pass,true);
    assert.ok(pair.memory_increase_pct<=15);
    for(const kind of ["baseline","authorized"])
      assert.ok(pair.route_pairs?.[kind]?.p95_increase_pct<=10);
  }
  result.cases.push({name:"authorized-and-baseline-measured-throughput-and-overhead",
    status:"PASS",source_artifact:"m17-performance-raw.json"});
  assert.equal(cardinality.total_paths,40000);
  assert.equal(cardinality.result,"PASS");
  assert.ok(cardinality.post_stress_memory_growth_pct<=15);
  result.cases.push({name:"40k-hostile-path-memory-and-business-isolation",
    status:"PASS",source_artifact:"m17-cardinality-memory-results.json"});
  assert.equal(storage.result,"PASS");
  assert.ok(storage.checks.some(x=>
    x.name==="business-remains-responsive-during-full-telemetry-store"&&
    x.status==="PASS"));
  assert.ok(storage.checks.some(x=>
    x.name==="all-application-history-byte-identical"&&x.status==="PASS"));
  result.cases.push({name:"business-under-full-disposable-telemetry-storage",
    status:"PASS",source_artifact:"m17-storage-results.json"});
  assert.equal(result.cases.length,4);
  result.result="PASS";
}catch(e){error=e;result.error_type=e?.name||"Error";}
fs.writeFileSync(path.join(evidence,"m17-pressure-matrix-results.json"),
  JSON.stringify(result,null,2)+"\n",{flag:"wx"});
if(error||result.result!=="PASS")
  throw Error("M17 10x exporter pressure, paired workload and storage campaign incomplete");
