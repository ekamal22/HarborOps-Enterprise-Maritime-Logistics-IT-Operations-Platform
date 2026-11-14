import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {execFileSync} from "node:child_process";
import {validateLive5xxEvidence} from "./g17-live-5xx-evidence.mjs";

const project=process.env.HARBOROPS_G17_PROJECT;
const source=process.env.HARBOROPS_SOURCE_SHA;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const password=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence)&&password);
const compose=["compose","-p",project,"-f","compose.m17.verify.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m9.worker.yaml"]:[]),
  "-f","compose.m17.observability.override.yaml",
  ...(process.env.HARBOROPS_G17_VERIFY_WORKER==="true"?["-f","compose.m17.worker.override.yaml"]:[]),
  "--profile","observability"];
const cmd=parts=>execFileSync("docker",[...compose,...parts],{
  encoding:"utf8",timeout:60000,maxBuffer:2*1024*1024}).trim();
const base="http://127.0.0.1:18080";
const proxy="http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-metrics/api/v1/";
const auth="Basic "+Buffer.from("local_operator:"+password).toString("base64");
const reportFile=path.join(evidence,"m17-database-fault-results.json");
const checks=[];
let stopped=false,error=null,dbBefore=null,dbAfter=null,alerts=[];
const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function stage(name,fn) {
  try {
    const data=await fn();
    checks.push({name,status:"PASS",data});
  }catch(e) {
    checks.push({name,status:"FAIL",error_type:e?.name||"Error",error_message:String(e?.message||"").slice(0,240)});
    throw e;
  }
}
async function http(endpoint,code) {
  const response=await fetch(base+endpoint,{signal:AbortSignal.timeout(10000)});
  assert.equal(response.status,code,endpoint+" returned unexpected status");
  return {body:await response.json(),headers:response.headers};
}
async function prom(expr) {
  const url=new URL(proxy+"query");
  url.searchParams.set("query",expr);
  const response=await fetch(url,{headers:{authorization:auth},
    signal:AbortSignal.timeout(13000)});
  assert.equal(response.status,200);
  const body=await response.json();
  assert.equal(body.status,"success");
  return body.data.result;
}
async function poll(label,predicate,ms=140000) {
  const end=Date.now()+ms;
  while(Date.now()<end) {
    try {const result=await predicate();if(result)return result;}
    catch { /* transient startup and scrape delay must not pass silently */ }
    await wait(3500);
  }
  throw Error("Timed out before "+label+" was observed");
}
function counts() {
  const sql="SELECT (SELECT count(*) FROM tracking_inbox_receipts)::text || ',' || "+
    "(SELECT count(*) FROM tracking_accepted_events)::text || ',' || "+
    "(SELECT count(*) FROM tracking_processing_effects)::text || ',' || "+
    "(SELECT count(*) FROM audit_entries)::text";
  return cmd(["exec","-T","postgres","psql","-U","harborops","-d","harborops",
    "-v","ON_ERROR_STOP=1","-At","-c",sql]);
}
try {
  await stage("healthy-database-control",async()=>{
    dbBefore=counts();
    assert.equal((await http("/health/live",200)).body.status,"healthy");
    assert.equal((await http("/health/ready",200)).body.status,"healthy");
    await poll("healthy exported readiness",async()=>{
      const values=await prom("harborops_db_readiness_ratio");
      return values.length===1&&Number(values[0].value[1])===1;
    });
    return {durable_row_counts:dbBefore};
  });
  cmd(["stop","postgres"]);
  stopped=true;
  await stage("real-db-outage-keeps-live-and-errors-classified",async()=>{
    await poll("unhealthy ready, live unchanged",async()=>{
      try {
        return (await http("/health/live",200)).body.status==="healthy" &&
          (await http("/health/ready",503)).body.status==="unhealthy";
      }catch{return false;}
    },95000);
    const {body,headers}=await http("/api/v1/system/baseline",503);
    assert.equal(body.code,"dependency_unavailable");
    const correlation=headers.get("x-correlation-id");
    assert.match(correlation||"",/^[0-9a-f]{32}$/);
    assert.equal(body.correlationId,correlation);
    assert.ok(!JSON.stringify(body).includes("postgres"));
    return {http_status:503,code:body.code,correlation_format:"server-issued-hex32"};
  });
  await stage("real-http-5xx-rate-fires-reference-alert",async()=>{
    // The first ever 5xx counter sample must not arrive only after an
    // entire burst. Prometheus rate() requires at least two observations
    // with an actual increase between them. Establish the real exported
    // baseline before producing the sustained controlled fault.
    const metric='harborops_request_failures_total{operation="business",status_class="5xx"}';
    const baseline=await poll("exported API failure-counter baseline",async()=>{
      const samples=await prom(metric);
      if(samples.length!==1)return null;
      const value=Number(samples[0].value[1]);
      return Number.isFinite(value)&&value>=0?value:null;
    },90000);
    // Generate real browser-originated 503s within this same controlled outage.
    execFileSync("node",["tests/browser/m17/page-fault-journey.mjs","inject"],
      {encoding:"utf8",timeout:110000,maxBuffer:1024*1024});
    let failures=0;
    for(let offset=0;offset<120;offset+=10) {
      const responses=await Promise.all(Array.from({length:10},async()=>{
        const r=await fetch(base+"/api/v1/system/baseline",
          {signal:AbortSignal.timeout(19000)});
        assert.equal(r.status,503,"Expected real unavailable database response");
        const json=await r.json();
        assert.equal(json.code,"dependency_unavailable");
        return true;
      }));
      failures+=responses.length;
      // Twelve separate batches span multiple 15-second OTLP exports,
      // Prometheus scrapes and rule evaluations, unlike one fast burst.
      if(offset+10<120)await wait(5000);
    }
    assert.equal(failures,120);
    const measured=await poll("actual sampled 5xx increase and alert threshold",
      async()=>{
        const [counter,rate,eligible,fraction]=await Promise.all([
          prom(metric),
          prom('sum(increase(harborops_request_failures_total{operation="business",status_class="5xx"}[5m]))'),
          prom('harborops:m17:eligible_requests:5m'),
          prom('harborops:m17:api_5xx_fraction:5m')
        ]);
        if([counter,rate,eligible,fraction].some(values=>values.length!==1))return null;
        const observed={
          baseline_counter:baseline,observed_counter:Number(counter[0].value[1]),
          failed_increase_5m:Number(rate[0].value[1]),
          eligible_requests_5m:Number(eligible[0].value[1]),
          failure_fraction_5m:Number(fraction[0].value[1]),
          requested_503_responses:failures
        };
        try{return validateLive5xxEvidence(observed)?observed:null;}
        catch{return null;}
      },110000);
    await poll("actual API 5xx fraction alert",async()=>{
      const signal=await prom('ALERTS{alertname="HarborOpsElevatedApiFailureRate",alertstate="firing"}');
      return signal.length===1;
    },110000);
    return {real_503_responses:failures,
      alert:"HarborOpsElevatedApiFailureRate",sampled_failure_evidence:measured};
  });
  await stage("database-alert-identifies-only-safe-component",async()=>{
    await poll("fresh database unavailable sample",async()=>{
      const data=await prom("harborops_db_readiness_ratio");
      return data.length===1&&Number(data[0].value[1])===0;
    });
    const current=await prom(
      "harborops_db_readiness_ratio and on() (time() - harborops_db_readiness_sample_unix_seconds < 60)");
    assert.equal(current.length,1);
    assert.equal(Number(current[0].value[1]),0);
    await poll("database alert firing",async()=>{
      const data=await prom('ALERTS{alertname="HarborOpsDatabaseUnavailable",alertstate="firing"}');
      return data.length===1;
    });
    const url=new URL(proxy+"alerts");
    const response=await fetch(url,{headers:{authorization:auth},
      signal:AbortSignal.timeout(14000)});
    assert.equal(response.status,200);
    const json=await response.json();
    assert.equal(json.status,"success");
    const selected=json.data.alerts.filter(a=>a.labels?.alertname==="HarborOpsDatabaseUnavailable");
    assert.equal(selected.length,1);
    const rule=selected[0];
    assert.equal(rule.annotations.summary,
      "Two consecutive recent PostgreSQL readiness samples failed");
    const raw=JSON.stringify(rule);
    assert.ok(!/password|token|credential|shipment|audit_entries|sql statement/i.test(raw),
      "Database diagnostic leaked a protected business or credential field");
    alerts.push({name:rule.labels.alertname,severity:rule.labels.severity,
      summary:rule.annotations.summary});
    return {freshness:"current",alert:alerts[0]};
  });
  await stage("page-originated-correlated-diagnostic-journey",async()=>{
    execFileSync("node",["tests/browser/m17/page-fault-journey.mjs","observe"],
      {encoding:"utf8",timeout:130000,maxBuffer:1024*1024});
    execFileSync("node",["scripts/check-g17-diagnostic-journey.mjs"],
      {encoding:"utf8",timeout:160000,maxBuffer:1024*1024});
    const proof=JSON.parse(fs.readFileSync(path.join(evidence,
      "m17-diagnostic-journey-results.json"),"utf8"));
    assert.equal(proof.result,"PASS");
    return {trace_id:proof.trace_id,server_correlation:proof.server_correlation,
      source_artifact:"m17-diagnostic-journey-results.json"};
  });
} catch(e) {error=e;}
finally {
  if(stopped) {
    try {cmd(["start","postgres"]);stopped=false;}
    catch(err){error=error||err;checks.push({name:"database-owned-restore",status:"FAIL"});}
  }
}
if(!stopped) {
  try {
    await stage("recovery-clears-alert-and-preserves-history",async()=>{
      await poll("API ready after PostgreSQL restart",async()=>{
        try{return (await http("/health/ready",200)).body.status==="healthy";}
        catch{return false;}
      });
      await poll("exported database ready",async()=>{
        const values=await prom("harborops_db_readiness_ratio");
        return values.length===1&&Number(values[0].value[1])===1;
      });
      await poll("database alert cleared",async()=>{
        const values=await prom('ALERTS{alertname="HarborOpsDatabaseUnavailable",alertstate="firing"}');
        return values.length===0;
      },95000);
      assert.equal((await http("/api/v1/system/baseline",200)).body.service,"HarborOps");
      dbAfter=counts();
      assert.equal(dbAfter,dbBefore,"PostgreSQL outage changed retained business or audit state");
      return {before:dbBefore,after:dbAfter,alert_state:"cleared"};
    });
  }catch(err){error=error||err;}
}
const success=!error&&!stopped&&checks.length===5&&checks.every(x=>x.status==="PASS");
fs.writeFileSync(reportFile,JSON.stringify({source_sha:source,project,
  result:success?"PASS":"FAIL",checks,alerts,database_restored:!stopped,
  source_environment:"OWNED_DISPOSABLE_POSTGRES_ONLY",accepted_assertions:0,
  failure_type:error?.name??null
},null,2)+"\n",{flag:"wx"});
if(!success)throw Error("G17 database outage/recovery and diagnostic alert campaign failed");
