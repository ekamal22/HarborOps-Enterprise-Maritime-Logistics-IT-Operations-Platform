import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {execFileSync} from "node:child_process";
import {fileURLToPath} from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read = name => fs.readFileSync(path.join(root,name),"utf8");
const base = "b490fb8cfc879cdf05cb1f7484b99f6a9b264ed3";

test("M17 four work packages exist with actual referenced production and verification boundaries",()=>{
  const packages = [
    ["src/HarborOps.Api/Observability/M17TelemetryRegistration.cs","WithMetrics"],
    ["src/HarborOps.Api/Observability/M17TraceSanitizer.cs","OnEnd"],
    ["monitoring/m17/alerts.yaml","HarborOpsDatabaseUnavailable"],
    ["tests/browser/m17/dashboard.spec.mjs","test("]
  ];
  for (const [file,proof] of packages)
    assert.ok(read(file).includes(proof),file+" has no expected implementation");
  const plan=read("docs/M17_IMPLEMENTATION_PLAN.md");
  for (const item of ["M17-01","M17-02","M17-03","M17-04"])
    assert.ok(plan.includes(item),"M17 work package absent: "+item);
});

test("every approved M17 policy has a concrete source-side verification control",()=>{
  const policy=read("docs/M17_POLICY_PROPOSAL_20261112.md");
  for (let i=1;i<=9;i++) {
    const id="M17-P"+String(i).padStart(2,"0");
    assert.ok(policy.includes(id),"Approved policy missing: "+id);
  }
  const api=read("src/HarborOps.Api/Observability/M17TelemetryRegistration.cs");
  const limits=read("src/HarborOps.Api/Observability/M17BoundedSampler.cs");
  const compose=read("compose.m17.observability.override.yaml");
  const dashboards=JSON.parse(read("monitoring/m17/grafana/dashboard.json"));
  assert.match(api,/maxQueueSize:\s*2048/);
  assert.match(limits,/TraceIdRatioBasedSampler\(0\.10\)/);
  assert.match(limits,/admitted\s*>=\s*200/);
  assert.match(compose,/127\.0\.0\.1:3301:3000/);
  assert.match(compose,/retention\.time=24h/);
  assert.match(compose,/retention\.size=120MB/);
  assert.ok(dashboards.panels.some(p=>p.targets.some(t=>t.expr.includes("harborops_db_readiness"))));
});

test("source diff does not change the accepted default Compose or database migrations",()=>{
  const changed=execFileSync("git",["diff","--name-only",base+"...HEAD"],{
    cwd:root,encoding:"utf8"
  }).trim().split(/\r?\n/).filter(Boolean);
  assert.ok(changed.length>0);
  assert.doesNotContain(changed,"compose.yaml");
  assert.ok(!changed.some(name=>/Migrations\//i.test(name)),
    "The M17 candidate changed an accepted database migration");
  assert.ok(!changed.some(name=>name.startsWith("docker/postgres/")),
    "The accepted PostgreSQL image was unexpectedly modified");
});
