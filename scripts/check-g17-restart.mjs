import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import {execFileSync,spawn} from "node:child_process";
import {performance} from "node:perf_hooks";
import {verifyTrxCases} from "./g17-results-contract.mjs";
import {seedLeasedSyntheticWork,leaseSnapshot,leaseIdentity} from "./g17-lease-fixture.mjs";

const project=process.env.HARBOROPS_G17_PROJECT;
const sha=process.env.HARBOROPS_SOURCE_SHA;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(sha||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence));
const root="http://127.0.0.1:18080";
const args=["compose","-p",project,"-f","compose.m17.verify.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m9.worker.yaml"]:[]),
  "-f","compose.m17.observability.override.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m17.worker.override.yaml"]:[]),
  "--profile","observability",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["--profile","worker"]:[])];
const docker=cmd=>execFileSync("docker",[...args,...cmd],{
  encoding:"utf8",timeout:45000,maxBuffer:1024*1024}).trim();
const checks=[];let stopped=false;let failure=false;let shutdownEpochMs=0;
const password=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.ok(password,"Disposable Grafana credentials must exist");
async function prom(expression) {
  const target=new URL("http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-metrics/api/v1/query");
  target.searchParams.set("query",expression);
  const response=await fetch(target,{headers:{authorization:"Basic "+
    Buffer.from("local_operator:"+password).toString("base64")},
    signal:AbortSignal.timeout(11000)});
  assert.equal(response.status,200);
  const result=await response.json();
  assert.equal(result.status,"success");
  return result.data.result;
}
const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function check(name,fn) {
  try {
    const detail=await fn();
    checks.push({name,status:"PASS",detail});
  } catch(err) {
    checks.push({name,status:"FAIL",error_type:err?.name||"Error",error_message:String(err?.message||"").slice(0,240)});
    failure=true;throw err;
  }
}
async function status(uri,code=200) {
  const r=await fetch(root+uri,{signal:AbortSignal.timeout(8000)});
  assert.equal(r.status,code,uri+" returned unexpected status");
  return await r.json();
}
async function ready() {
  for(let k=0;k<50;k++) {
    try {
      const body=await status("/health/ready");
      if(body.status==="healthy")return true;
    } catch {}
    await wait(1200);
  }
  throw Error("Restarted API was not ready within bounded recovery time");
}
const sql=query=>docker(["exec","-T","postgres","psql","-U","harborops",
  "-d","harborops","-At","-v","ON_ERROR_STOP=1","-c",query]).trim();
function databaseFootprint() {
  return sql("SELECT (SELECT count(*) FROM tracking_inbox_work_items)::text || ',' || "+
    "(SELECT count(*) FROM tracking_processing_effects)::text || ',' || "+
    "(SELECT count(*) FROM tracking_inbox_receipts)::text");
}
async function tempoTrace(id) {
  const base="http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-traces";
  const response=await fetch(base+"/api/v2/traces/"+id,{headers:{authorization:"Basic "+
    Buffer.from("local_operator:"+password).toString("base64")},
    signal:AbortSignal.timeout(12000)});
  assert.equal(response.status,200,"Tempo must remain observable after API shutdown");
  const raw=await response.json();
  return (raw.trace?.resourceSpans||raw.resourceSpans||raw.batches||[]).flatMap(resource=>
    (resource.scopeSpans||resource.instrumentationLibrarySpans||[]).flatMap(scope=>scope.spans||[]));
}
const leaseProof=verifyTrxCases(
  fs.readFileSync(path.join(evidence,"g17-integration.trx"),"utf8"),
  ["ApiRestartPreservesActiveWorkerLeaseAndNoDuplicateEffects"]);
assert.ok(leaseProof.passed>0,"A source-locked positive worker lease fixture is required");
let before,lease,leaseBefore,leaseDuringStop,leaseAfter;
const canceledTraces=[];
const lifecycle={};
try {
  await check("pre-restart-synthetic-baseline",async()=>{
    await ready();
    // An owned, positive, unexpired worker lease on the live stack. No worker runs
    // in this stage, so nothing but the API lifecycle and this script can touch it.
    lease=seedLeasedSyntheticWork(sql,9);
    leaseBefore=leaseSnapshot(sql,lease.receipt);
    const identity=leaseIdentity(leaseBefore);
    assert.equal(identity.state,"Leased");
    assert.equal(identity.lease_token,lease.token);
    assert.equal(identity.lease_epoch,1);
    assert.equal(identity.attempt_count,1);
    assert.equal(leaseBefore.receipts,1);
    assert.equal(leaseBefore.events,1);
    assert.equal(leaseBefore.effects,0);
    assert.ok(new Date(identity.lease_expires_at_utc).getTime()>Date.now()+5*60*1000,
      "The seeded lease must outlive the restart observation");
    before=databaseFootprint();
    assert.ok(Number(before.split(",")[0])>=1,"Footprint must contain the positive leased work item");
    assert.equal((await status("/api/v1/system/baseline")).service,"HarborOps");
    return {durable_counts:before,lease_receipt:lease.receipt,lease_epoch:identity.lease_epoch,
      lease_attempts:identity.attempt_count,lease_expires_at_utc:identity.lease_expires_at_utc};
  });
  await check("real-client-abort-does-not-commit-business",async()=>{
    // Each canceled request carries its own W3C trace identifier, so its exported
    // telemetry (if any) can be found and judged after the restart.
    const operations=Array.from({length:80},async(_,i)=>{
      const controller=new AbortController();
      if(i===0)controller.abort();
      const traceId=crypto.randomBytes(16).toString("hex");
      const timer=setTimeout(()=>controller.abort(),i%2?2:6);
      try {
        const res=await fetch(root+"/api/v1/system/baseline",
          {signal:controller.signal,headers:{
            traceparent:"00-"+traceId+"-0123456789abcdef-01"}});
        assert.equal(res.status,200);
        return "completed";
      }catch(err) {
        assert.ok(err.name==="AbortError"||err.name==="TimeoutError",
          "Unexpected network exception during deliberate cancellation");
        canceledTraces.push({trace_id:traceId,canceled_at_ms:Date.now()});
        return "canceled";
      }finally{clearTimeout(timer);}
    });
    const cases=await Promise.all(operations);
    assert.ok(cases.includes("canceled"),"Cancellation injector did not cancel a request");
    assert.equal(databaseFootprint(),before,"Cancellation changed durable business state");
    // The active lease is byte-identical after the canceled in-flight requests.
    assert.deepEqual(leaseSnapshot(sql,lease.receipt),leaseBefore,
      "Request cancellation changed the active worker lease");
    // Genuinely in-flight cancellation. The fast aborts above usually end before the
    // server has begun work, so they cannot show what the API does with a request it
    // already started. Here an exclusive lock on the baseline table blocks the reads
    // inside the API; each blocked request is then canceled by its client.
    const locker=spawn("docker",[...args,"exec","-T","postgres","psql","-U","harborops",
      "-d","harborops","-v","ON_ERROR_STOP=1","-c",
      "BEGIN; LOCK TABLE bootstrap_records IN ACCESS EXCLUSIVE MODE; SELECT pg_sleep(9); COMMIT;"],
      {stdio:"ignore"});
    const lockerDone=new Promise(resolve=>locker.on("exit",resolve));
    let lockHeld=false;
    for(let attempt=0;attempt<40&&!lockHeld;attempt++) {
      lockHeld=Number(sql("SELECT count(*) FROM pg_locks WHERE relation='bootstrap_records'::regclass "+
        "AND mode='AccessExclusiveLock' AND granted"))>=1;
      if(!lockHeld)await wait(250);
    }
    assert.ok(lockHeld,"The owned exclusive lock that blocks in-flight reads was not granted");
    const inflightIds=[];
    const flight=await Promise.all(Array.from({length:60},async()=>{
      const controller=new AbortController();
      const traceId=crypto.randomBytes(16).toString("hex");
      const timer=setTimeout(()=>controller.abort(),2500);
      try {
        await fetch(root+"/api/v1/system/baseline",{signal:controller.signal,headers:{
          traceparent:"00-"+traceId+"-0123456789abcdef-01"}});
        return "completed";
      }catch(err) {
        assert.ok(err.name==="AbortError"||err.name==="TimeoutError",
          "Unexpected network exception during in-flight cancellation");
        inflightIds.push(traceId);
        canceledTraces.push({trace_id:traceId,canceled_at_ms:Date.now(),in_flight:true});
        return "canceled";
      }finally{clearTimeout(timer);}
    }));
    await Promise.race([lockerDone,wait(25000)]);
    const inflightCanceled=flight.filter(x=>x==="canceled").length;
    assert.equal(inflightCanceled,60,
      "Every blocked in-flight request must be canceled by its client, not answered");
    await ready();
    assert.equal((await status("/api/v1/system/baseline")).service,"HarborOps");
    assert.equal(databaseFootprint(),before,"In-flight cancellation changed durable business state");
    assert.deepEqual(leaseSnapshot(sql,lease.receipt),leaseBefore,
      "In-flight request cancellation changed the active worker lease");
    return {total:cases.length+flight.length,canceled:cases.filter(x=>x==="canceled").length+inflightCanceled,
      fast_canceled:cases.filter(x=>x==="canceled").length,inflight_canceled:inflightCanceled,
      canceled_trace_ids:canceledTraces.length,lease_unchanged_by_cancellation:true};
  });
  await check("owned-api-shutdown-bounded",async()=>{
    const started=performance.now();
    docker(["stop","-t","10","api"]);
    stopped=true;
    const elapsed=(performance.now()-started)/1000;
    shutdownEpochMs=Date.now();
    assert.ok(elapsed<=20,"Telemetry shutdown exceeded the bounded stop interval");
    const states=docker(["ps","-a","api","--format","{{.State}}"]);
    assert.ok(states==="exited","API was not cleanly stopped");
    const id=docker(["ps","-a","-q","api"]);
    assert.match(id,/^[a-f0-9]+$/);
    const exit=execFileSync("docker",["inspect","--format","{{.State.ExitCode}}",id],{
      encoding:"utf8",timeout:10000}).trim();
    assert.notEqual(exit,"137","API required SIGKILL rather than bounded graceful shutdown");
    // While the API is down the lease is still the very same lease.
    leaseDuringStop=leaseSnapshot(sql,lease.receipt);
    assert.deepEqual(leaseDuringStop,leaseBefore,"API shutdown changed the active worker lease");
    return {shutdown_seconds:elapsed,exit_code:Number(exit),lease_unchanged_during_stop:true};
  });
  await check("stale-readiness-is-unknown-not-healthy",async()=>{
    // The newest readiness sample can already be up to 15 seconds old when the API stops,
    // and it stops counting as current only once it is older than 60 seconds.
    await wait(80000);
    const collector=await prom('up{job="m17_collector"}');
    assert.equal(collector.length,1,"The monitoring backend itself must be observable");
    assert.equal(Number(collector[0].value[1]),1);
    const current=await prom(
      "harborops_db_readiness_ratio and on() (time() - harborops_db_readiness_sample_unix_seconds < 60)");
    assert.equal(current.length,0,
      "Readiness data from a stopped API was incorrectly displayed as current");
    const base="http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-traces";
    const headers={authorization:"Basic "+
      Buffer.from("local_operator:"+password).toString("base64")};
    const search=new URL(base+"/api/search");
    search.searchParams.set("tags","service.name=harborops-api");
    search.searchParams.set("limit","100");
    const response=await fetch(search,{headers,signal:AbortSignal.timeout(12000)});
    assert.equal(response.status,200,"Tempo must remain observable after API shutdown");
    const listing=await response.json();
    assert.ok((listing.traces||[]).length>0,
      "A positive API trace witness is required before stale-span review");
    let inspected=0;
    for(const item of (listing.traces||[]).slice(0,50)) {
      const detail=await fetch(base+"/api/v2/traces/"+item.traceID,
        {headers,signal:AbortSignal.timeout(12000)});
      assert.equal(detail.status,200);
      const raw=await detail.json();
      for(const resource of raw.trace?.resourceSpans||raw.resourceSpans||raw.batches||[]) {
        for(const scope of resource.scopeSpans||resource.instrumentationLibrarySpans||[]) {
          for(const span of scope.spans||[]) {
            const startTime=span.startTimeUnixNano||span.start_time_unix_nano;
            assert.ok(startTime,"Exported span lacks its source timestamp");
            assert.ok(BigInt(startTime)<BigInt(shutdownEpochMs+1000)*1000000n,
              "A new API span was exported with a start time after the API stopped");
            inspected++;
          }
        }
      }
    }
    assert.ok(inspected>0,"No real serialized API spans inspected for stale-data leakage");
    // Correlate cancellation to telemetry: look up each canceled request by the
    // trace identifier it carried. Whatever the API exported for it must have
    // started before the shutdown, and a trace may not claim a server error.
    let found=0,absent=0,canceledSpans=0,inflightTotal=0,inflightFound=0;
    for(const item of canceledTraces) {
      const spans=await tempoTrace(item.trace_id);
      if(item.in_flight)inflightTotal++;
      if(spans.length===0) {absent++;continue;}
      found++;
      if(item.in_flight)inflightFound++;
      for(const span of spans) {
        const startTime=span.startTimeUnixNano||span.start_time_unix_nano;
        assert.ok(startTime&&BigInt(startTime)<BigInt(shutdownEpochMs+1000)*1000000n,
          "A canceled request produced a span that started after the API stopped");
        assert.notEqual(span.status?.code,"STATUS_CODE_ERROR",
          "A client-canceled request was exported as a server error");
        canceledSpans++;
      }
    }
    assert.equal(found+absent,canceledTraces.length);
    lifecycle.canceled_trace_correlation={canceled_requests:canceledTraces.length,
      inflight_canceled_requests:inflightTotal,inflight_found_in_tempo:inflightFound,
      found_in_tempo:found,absent_in_tempo:absent,spans_inspected:canceledSpans,
      all_started_before_shutdown:true,none_exported_as_server_error:true};
    return {stale_readiness:"unknown",post_shutdown_spans:0,
      actual_spans_inspected:inspected,...lifecycle.canceled_trace_correlation};
  });

} catch {failure=true;}
finally {
  if(stopped) {
    try {docker(["start","api"]);stopped=false;}
    catch {failure=true;checks.push({name:"owned-api-restart",status:"FAIL"});}
  }
}
if(!stopped) {
  try {
    await check("restart-recovers-health-without-business-duplication",async()=>{
      await ready();
      assert.equal((await status("/health/live")).status,"healthy");
      assert.equal((await status("/api/v1/system/baseline")).service,"HarborOps");
      assert.equal(databaseFootprint(),before,
        "Restart created a duplicate receipt, lease or business effect");
      leaseAfter=leaseSnapshot(sql,lease.receipt);
      assert.deepEqual(leaseAfter,leaseBefore,
        "The active worker lease differs after the API restart");
      const identity=leaseIdentity(leaseAfter);
      assert.equal(identity.lease_token,lease.token);
      assert.equal(identity.lease_epoch,1);
      assert.equal(identity.attempt_count,1);
      // Fencing across the restart. A forged token or epoch cannot finish the work.
      const finish=(token,epoch)=>Number(sql(`WITH moved AS (UPDATE tracking_inbox_work_items
        SET state='NeedsReconciliation',lease_token=NULL,lease_expires_at_utc=NULL,
            last_error_code='unresolved_reference',updated_at_utc=clock_timestamp()
        WHERE receipt_id='${lease.receipt}' AND state='Leased'
          AND lease_token='${token}' AND lease_epoch=${epoch}
          AND lease_expires_at_utc>clock_timestamp() RETURNING 1)
        SELECT count(*) FROM moved`).split("\n").pop());
      assert.equal(finish(crypto.randomUUID(),1),0,"A forged lease token finished the work");
      assert.equal(finish(lease.token,2),0,"A forged lease epoch finished the work");
      assert.deepEqual(leaseSnapshot(sql,lease.receipt),leaseBefore);
      // The original holder still owns the lease after the restart and finishes it
      // exactly once. The duplicate completion changes nothing and creates no effect.
      assert.equal(finish(lease.token,1),1,"The original lease holder could not finish the work");
      assert.equal(finish(lease.token,1),0,"A duplicate completion changed the work");
      const done=leaseSnapshot(sql,lease.receipt);
      assert.equal(done.work.state,"NeedsReconciliation");
      assert.equal(done.work.lease_token,null);
      assert.equal(done.work.lease_epoch,1);
      assert.equal(done.work.attempt_count,1);
      assert.equal(done.effects,0,"A duplicate processing effect exists");
      assert.equal(done.receipts,1);
      assert.equal(done.events,1);
      lifecycle.lease={receipt:lease.receipt,token:lease.token,epoch:1,attempts:1,
        expires_before:leaseIdentity(leaseBefore).lease_expires_at_utc,
        identity_equal_before_stop_during_stop_after_restart:true,
        forged_token_rejected:true,forged_epoch_rejected:true,
        original_holder_finished_once:true,duplicate_finish_rejected:true,
        effects_after:done.effects};
      return {durable_counts_after:before,...lifecycle.lease};
    });
  }catch {failure=true;}
}
const report={source_sha:sha,project,result:!failure&&!stopped&&checks.length===5&&
  checks.every(x=>x.status==="PASS")?"PASS":"FAIL",
  checks,api_restored:!stopped,
  positive_worker_lease_witness:Boolean(lifecycle.lease?.identity_equal_before_stop_during_stop_after_restart),
  lease:lifecycle.lease??null,
  canceled_trace_correlation:lifecycle.canceled_trace_correlation??null,
  source_locked_lease_method:"ApiRestartPreservesActiveWorkerLeaseAndNoDuplicateEffects",
  assertion_acceptance:"PENDING"};
fs.writeFileSync(path.join(evidence,"m17-restart-results.json"),
  JSON.stringify(report,null,2)+"\n",{flag:"wx"});
if(report.result!=="PASS")throw Error("G17 restart, cancellation or shutdown bound failed");
