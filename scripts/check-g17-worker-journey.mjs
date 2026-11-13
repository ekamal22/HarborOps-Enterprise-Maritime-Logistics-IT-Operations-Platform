import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {spawnSync} from "node:child_process";

const project=process.env.HARBOROPS_G17_PROJECT;
const sha=process.env.HARBOROPS_SOURCE_SHA;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const key=process.env.HARBOROPS_M17_LINK_KEY;
const grafana=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(sha||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence)&&grafana);
assert.equal(Buffer.from(key||"","base64").length,32);
assert.equal(process.env.HARBOROPS_G17_VERIFY_WORKER,"true");
const compose=["compose","-p",project,"-f","compose.m17.verify.yaml",
 "-f","compose.m9.worker.yaml","-f","compose.m17.observability.override.yaml",
 "-f","compose.m17.worker.override.yaml","--profile","observability","--profile","worker"];
function run(args,input) {
 const r=spawnSync("docker",[...compose,...args],
   {input,encoding:"utf8",timeout:120000,maxBuffer:4*1024*1024});
 assert.equal(r.status,0,"Disposable worker fixture command failed");
 return r.stdout.trim();
}
const sql=q=>run(["exec","-T","postgres","psql","-U","harborops","-d","harborops",
 "-At","-v","ON_ERROR_STOP=1","-c",q]);
const nonce=crypto.randomBytes(6).toString("hex");
const account="g17-"+nonce+"@example.test";
const password=crypto.randomBytes(32).toString("base64url");
const slug="g17-worker-"+nonce;
const output=path.join(evidence,"m17-worker-journey-results.json");
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
const auth="Basic "+Buffer.from("local_operator:"+grafana).toString("base64");
let failure=null,pairCount=0,accepted=0,replay=false,reconciled=0;
const protectedValues=[];
let privateLabelsChecked=0;
async function tempo(endpoint,params={}) {
 const u=new URL("http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-traces"+endpoint);
 for(const [key,val] of Object.entries(params))u.searchParams.set(key,String(val));
 const r=await fetch(u,{headers:{authorization:auth},signal:AbortSignal.timeout(14000)});
 assert.equal(r.status,200,"Local Tempo unavailable");
 return r.json();
}
async function poll(name,fn,ms=150000) {
 const deadline=Date.now()+ms;
 while(Date.now()<deadline) {
   try {const value=await fn();if(value)return value;} catch {}
   await sleep(2500);
 }
 throw Error("No positive live worker witness: "+name);
}
async function digests(service) {
 const listed=await tempo("/api/search",{"tags":"service.name="+service,limit:200});
 const found=new Set();
 for(const id of (listed.traces||[]).slice(0,100).map(t=>t.traceID)) {
   const doc=await tempo("/api/v2/traces/"+id);
   const raw=JSON.stringify(doc).toLowerCase();
   for(const secret of protectedValues)
     assert.ok(!raw.includes(secret.toLowerCase()),
       "Tempo exported private tenant/source/account detail");
   for(const resource of doc.resourceSpans||doc.batches||[]) {
     for(const scope of resource.scopeSpans||resource.instrumentationLibrarySpans||[]) {
       for(const span of scope.spans||[]) {
         for(const a of span.attributes||[]) {
           if(a.key==="harborops.link_digest") {
             const value=a.value?.stringValue||a.value?.string_value||"";
             assert.match(value,/^[0-9a-f]{64}$/);
             found.add(value);
           }
         }
       }
     }
   }
 }
 return found;
}
try {
 run(["run","--rm","-T","-e","HARBOROPS_ALLOW_ACCOUNT_CLI=true","api",
    "--account-cli","provision",account,"G17 Synthetic"],password+"\n");
 run(["run","--rm","-T","-e","HARBOROPS_ALLOW_ORGANIZATION_CLI=true","api",
    "--organization-cli","bootstrap",slug,"G17 Synthetic Org",account]);
 const org=sql("SELECT id FROM organizations WHERE slug='"+slug+"'");
 assert.match(org,/^[0-9a-f-]{36}$/);
 const sourceId=crypto.randomUUID(),credentialId=crypto.randomUUID();
 protectedValues.push(org,org.replaceAll("-",""),slug,account);
 const secret=crypto.randomBytes(32);
 const digest=crypto.createHash("sha256").update(secret).digest("hex");
 const bearer="hop_v1_"+credentialId.replaceAll("-","")+"_"+secret.toString("base64url");
 protectedValues.push(sourceId,sourceId.replaceAll("-",""),credentialId,bearer);
 const insert="BEGIN; INSERT INTO integration_sources "+
   "(id,organization_id,source_code,display_name,status,capability,version,created_at_utc,updated_at_utc) "+
   "VALUES ('"+sourceId+"','"+org+"','g17_source','G17 Test Source','Active','tracking_event_validate',1,now(),now()); "+
   "INSERT INTO integration_credentials "+
   "(id,organization_id,source_id,secret_digest,key_version,not_before_utc,expires_at_utc,created_at_utc) "+
   "VALUES ('"+credentialId+"','"+org+"','"+sourceId+"',decode('"+digest+"','hex'),"+
   "'v1',now(),now()+interval '90 days',now()); COMMIT;";
 assert.match(sql(insert),/COMMIT/);
 const correlations=[];
 async function ingest(i,repeated=false) {
   const res=await fetch("http://127.0.0.1:18080/integrations/v1/tracking-events",{
     method:"POST",headers:{
       "Content-Type":"application/json; charset=utf-8",
       Authorization:"Bearer "+bearer,
       "X-HarborOps-Tracking-Profile":"tnt-2.2-harborops-v1",
       traceparent:"00-"+crypto.randomBytes(16).toString("hex")+
         "-"+crypto.randomBytes(8).toString("hex")+"-01"
     },body:JSON.stringify({
       sourceEventId:"g17-worker-"+nonce+"-"+i,eventFamily:"Equipment",
       eventCode:"GTIN",classifier:"ACT",occurredAtUtc:"2026-11-03T10:00:00Z",
       equipmentReference:"SYN-BOX-NOT-FOUND",externalSchemaVersion:"2.2.0"
     }),signal:AbortSignal.timeout(20000)});
   assert.equal(res.status,202,"Synthetic source must receive durable receipt");
   const body=await res.json(),correlation=res.headers.get("x-correlation-id");
   assert.match(correlation||"",/^[0-9a-f]{32}$/);
   assert.equal(body.correlationId,correlation);
   if(!repeated)correlations.push(correlation);
   assert.equal(body.duplicate,repeated);
   return body.receiptId;
 }
 const first=await ingest(0);
 for(let i=1;i<50;i++)await ingest(i);
 accepted=50;
 assert.equal(await ingest(0,true),first);
 replay=true;
 await poll("fifty real restricted worker reconciliations",()=>{
   const count=Number(sql("SELECT count(*) FROM tracking_inbox_work_items "+
     "WHERE source_id='"+sourceId+"' AND state='NeedsReconciliation'"));
   return count===accepted;
 },180000);
 reconciled=Number(sql("SELECT count(*) FROM tracking_work_attempt_events "+
   "WHERE source_id='"+sourceId+"' AND event_kind='ReconciliationRequired'"));
 assert.equal(reconciled,accepted);
 assert.equal(Number(sql("SELECT count(*) FROM tracking_processing_effects "+
   "WHERE source_id='"+sourceId+"'")),0);
 const expected=new Set(correlations.map(c=>
   crypto.createHmac("sha256",Buffer.from(key,"base64"))
     .update(c,"utf8").digest("hex")));
 const pairs=await poll("real API and worker safe trace pairs",async()=>{
   const api=await digests("harborops-api");
   const worker=await digests("harborops-tracking-worker");
   const intersection=[...expected].filter(d=>api.has(d)&&worker.has(d));
   return intersection.length>=2?intersection:null;
 },180000);
 pairCount=pairs.length;
 const metricUrl=new URL("http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-metrics/api/v1/series");
 metricUrl.searchParams.set("match[]",'{__name__=~"harborops_.*"}');
 const exported=await fetch(metricUrl,{headers:{authorization:auth},
   signal:AbortSignal.timeout(13000)});
 assert.equal(exported.status,200);
 const metricBody=await exported.json();
 assert.equal(metricBody.status,"success");
 assert.ok(metricBody.data?.length>0,"No exported custom metric series");
 for(const row of metricBody.data) {
   const raw=JSON.stringify(row).toLowerCase();
   for(const secret of protectedValues)
     assert.ok(!raw.includes(secret.toLowerCase()),
       "Prometheus exported private tenant/source/account identifiers");
   privateLabelsChecked++;
 }

 assert.equal(Number(sql("SELECT count(*) FROM tracking_inbox_receipts "+
   "WHERE source_id='"+sourceId+"'")),accepted,"Replay created a duplicate receipt");
}catch(error){failure=error;}
const status=!failure&&replay&&accepted===50&&reconciled===50&&pairCount>=2?"PASS":"FAIL";
fs.writeFileSync(output,JSON.stringify({
 source_sha:sha,project,result:status,worker_role:"restricted",
 receipts_accepted:accepted,reconciled,duplicate_replay:replay,
 matched_safe_trace_pairs:pairCount,
 exported_metric_series_checked_for_tenant_privacy:privateLabelsChecked,
 failure_type:failure?.name??null,accepted_assertions:0
},null,2)+"\n",{flag:"wx"});
if(status!=="PASS")throw Error("G17 real worker causal-link campaign failed");
