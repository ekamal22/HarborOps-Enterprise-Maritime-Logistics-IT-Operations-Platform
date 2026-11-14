import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import crypto from "node:crypto";
import {verifyS25RawSnapshots} from "./g17-s25-raw-evidence.mjs";
import {writeS25Fixture as fixture,source,project,doc,digest,put} from "./g17-s25-fixture.mjs";
test("retained real-shaped raw evidence is independently rechecked",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-test-"));
 try {fixture(dir);assert.equal(verifyS25RawSnapshots(dir,source,project).status,"PASS");}
 finally {fs.rmSync(dir,{recursive:true,force:true});}
});
test("changed trace, missing series and false browser state are rejected",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-negative-"));
 try {
  fixture(dir);
  const traces=JSON.parse(fs.readFileSync(path.join(dir,"m17-export-privacy-traces.json")));
  traces.records[0].document.trace.resourceSpans[0].scopeSpans[0].spans[0]
   .attributes.push({key:"http.request.header.cookie",value:{stringValue:"secret"}});
  put(dir,"m17-export-privacy-traces.json",traces);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  fs.unlinkSync(path.join(dir,"m17-worker-journey-series.json"));
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  const f="m17-ui-state-webkit.json";
  const ui=JSON.parse(fs.readFileSync(path.join(dir,f)));
  ui.verified_statuses.down="PostgreSQL readiness Ready";
  put(dir,f,ui);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  // A matching log digest must not excuse a credential in retained raw bytes.
  // This isolates the privacy detector from the independent hash-integrity gate.
  for(const injected of [
    "Bearer abcdefghijklmnopqrstuvwxyz0123456789",
    "Password: syntheticLongCredentialValue123456"
  ]) {
    fixture(dir);
    const raw="worker-1 | Tracking work polling started\n"+injected;
    const witness=JSON.parse(fs.readFileSync(
      path.join(dir,"m17-worker-journey-results.json"),"utf8"));
    witness.worker_log_bytes=Buffer.byteLength(raw);
    witness.worker_log_sha256=digest(raw);
    put(dir,"m17-worker-journey-results.json",witness);
    put(dir,"m17-worker-journey-logs.txt",raw);
    assert.throws(()=>verifyS25RawSnapshots(dir,source,project),
      /regular expression|expected.*not.*match/i);
  }
  fixture(dir);
  const diagnosticPath="m17-diagnostic-journey-results.json";
  const diagnostic=JSON.parse(fs.readFileSync(path.join(dir,diagnosticPath)));
  diagnostic.server_correlation="f".repeat(32);
  put(dir,diagnosticPath,diagnostic);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  const displayPath="m17-page-fault-dashboard.json";
  const display=JSON.parse(fs.readFileSync(path.join(dir,displayPath)));
  display.dashboard_target_format="time_series";
  put(dir,displayPath,display);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  const storagePath="m17-storage-results.json";
  const storage=JSON.parse(fs.readFileSync(path.join(dir,storagePath),"utf8"));
  storage.positive_history.tables.m8_receipts=0;
  put(dir,storagePath,storage);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),
    /positive retained business witness/);
  fixture(dir);
  const bad="m17-ui-no-traffic-chromium.json";
  const traffic=JSON.parse(fs.readFileSync(path.join(dir,bad)));
  traffic.panels["API 5xx rate"]="0 healthy";
  put(dir,bad,traffic);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
 } finally {fs.rmSync(dir,{recursive:true,force:true});}
});
test("below-threshold real-traffic rendering rejects false healthy values and forged windows",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-low-"));
 const edit=(name,fn)=>{const p=path.join(dir,name);const v=JSON.parse(fs.readFileSync(p,"utf8"));fn(v);put(dir,name,v);};
 try {
  fixture(dir);
  edit("m17-ui-no-traffic-firefox.json",v=>{v.low_traffic.panels["API 5xx rate"]="API 5xx rate 0 req/s";});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/False healthy value|not rendered Unknown/);
  fixture(dir);
  edit("m17-ui-no-traffic-webkit.json",v=>{v.low_traffic.panels["API p95 latency"]="API p95 latency 0 s";});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/not rendered Unknown|False healthy value/);
  fixture(dir);
  edit("m17-ui-no-traffic-chromium.json",v=>{v.low_traffic.panels["API request rate"]="API request rate Unknown";});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/Measured low request rate/);
  fixture(dir);
  edit("m17-ui-no-traffic-chromium.json",v=>{delete v.low_traffic;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/No rendered below-threshold/);
  fixture(dir);
  edit("m17-ui-no-traffic-firefox.json",v=>{v.low_traffic.window_end+=60;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  edit("m17-low-traffic-window.json",v=>{v.requests_sent=140;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/fewer than the approved/);
  fixture(dir);
  edit("m17-low-traffic-window.json",v=>{v.eligible_max=140;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/not real below-threshold/);
  fixture(dir);
  edit("m17-low-traffic-window.json",v=>{v.raw_5xx_zero_points=0;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/healthy-looking zero hazard/);
  fixture(dir);
  fs.unlinkSync(path.join(dir,"m17-low-traffic-window.json"));
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
 } finally {fs.rmSync(dir,{recursive:true,force:true});}
});
test("alert table cells, worker participation and independent privacy shapes are enforced",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-f02-f03-"));
 const edit=(name,fn)=>{const p=path.join(dir,name);const v=JSON.parse(fs.readFileSync(p,"utf8"));fn(v);put(dir,name,v);};
 try {
  fixture(dir);
  // The alert name appears in loose page text but not as an alertname firing cell.
  edit("m17-page-fault-dashboard.json",v=>{v.table_rows=[["x","HarborOpsDatabaseUnavailable","ALERTS","firing"]];});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/rendered firing table cell/);
  fixture(dir);
  edit("m17-page-fault-dashboard.json",v=>{v.table_rows=[["x","ALERTS","HarborOpsDatabaseUnavailable","pending"]];});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/rendered firing table cell/);
  fixture(dir);
  edit("m17-page-fault-dashboard.json",v=>{v.scrolled_into_view=false;});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project));
  fixture(dir);
  // Only the API exported traces: the participating worker is not demonstrated.
  edit("m17-worker-journey-traces.json",v=>{v.traces=v.traces.map(x=>({...x,service:"harborops-api"}));});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/participating worker service/);
  fixture(dir);
  edit("m17-worker-journey-series.json",v=>{v.series=[{__name__:"harborops_db_readiness_ratio",service:"api"}];});
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/tracking worker/);
  // Hash-consistent worker log injections of credential-, account- and tenant-shaped values.
  for(const injected of [
    "source token hop_v1_"+"a".repeat(32)+"_abcdefghijklmnopqrstu",
    "account g17-ab12cd34@example.test",
    "tenant g17-worker-0123456789ab"
  ]) {
   fixture(dir);
   const raw="worker-1 | Tracking work polling started\n"+injected;
   edit("m17-worker-journey-results.json",v=>{v.worker_log_bytes=Buffer.byteLength(raw);
     v.worker_log_sha256=digest(raw);});
   put(dir,"m17-worker-journey-logs.txt",raw);
   assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/synthetic credential, account or tenant/);
  }
  // A canary prefix in a hash-consistent retained service log.
  fixture(dir);
  const logs="api-1 | g17-private-canary-0123456789abcdef";
  edit("m17-export-privacy-results.json",v=>{v.retained_log_bytes=Buffer.byteLength(logs);
    v.snapshots.find(x=>x.kind==="owned-sanitized-service-logs").sha256=digest(logs);});
  put(dir,"m17-export-privacy-logs.txt",logs);
  assert.throws(()=>verifyS25RawSnapshots(dir,source,project),/canary prefix/);
 } finally {fs.rmSync(dir,{recursive:true,force:true});}
});
test("F08 storage evidence requires measured degradation, recovery and row-level identity",()=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),"g17-raw-f08-"));
 const edit=(fn)=>{const p=path.join(dir,"m17-storage-results.json");
  const v=JSON.parse(fs.readFileSync(p,"utf8"));fn(v);put(dir,"m17-storage-results.json",v);};
 const cases=[
  [v=>{v.telemetry_degradation.storage_error_logged=false;},/logged no storage error/],
  [v=>{v.telemetry_degradation.readiness_panel_expression_unknown=false;},/Readiness stayed current/],
  [v=>{v.telemetry_degradation.stale_age_seconds=5;},/No stale-sample measurement/],
  [v=>{v.telemetry_degradation.fresh_before_seconds=90;},/healthy-observation control/],
  [v=>{v.telemetry_recovery.recovered=false;},/did not recover/],
  [v=>{v.fixture_rows_after.m15_outbox.sha256="f".repeat(64);},/fixture row changed/],
  [v=>{delete v.fixture_rows_before.m8_work_items;delete v.fixture_rows_after.m8_work_items;},/No row-level hash/],
  [v=>{v.fixture_rows_before.m16_audit.rows=0;v.fixture_rows_after.m16_audit.rows=0;},/No row-level hash/]
 ];
 try {
  for(const [mutate,pattern] of cases) {
   fixture(dir);edit(mutate);
   assert.throws(()=>verifyS25RawSnapshots(dir,source,project),pattern);
  }
 } finally {fs.rmSync(dir,{recursive:true,force:true});}
});
