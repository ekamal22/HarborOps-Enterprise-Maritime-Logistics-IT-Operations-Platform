import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import {execFileSync,spawnSync} from "node:child_process";

const project=process.env.HARBOROPS_G17_PROJECT;
const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const sha=process.env.HARBOROPS_SOURCE_SHA;
assert.match(project||"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.match(sha||"",/^[a-f0-9]{40}$/);
assert.ok(evidence&&fs.existsSync(evidence));
const files=["compose.m17.verify.yaml","compose.m17.observability.override.yaml"];
const args=["compose","-p",project,...files.flatMap(f=>["-f",f]),"--profile","observability"];
const report=path.join(evidence,"m17-storage-results.json");
const evidenceData={source_sha:sha,project,checks:[],result:"FAIL"};
let fullStoreCreated=false;
function command(cmd,a,timeout=30000) {
  return execFileSync(cmd,a,{encoding:"utf8",timeout,maxBuffer:8*1024*1024}).trim();
}
function compose(a) {return command("docker",[...args,...a]);}
function container(service) {
  const ids=compose(["ps","-a","-q",service]).split(/\s+/).filter(Boolean);
  assert.equal(ids.length,1,"Exactly one owned "+service+" container required");
  const details=JSON.parse(command("docker",["inspect",ids[0]]))[0];
  assert.equal(details.Config.Labels["com.docker.compose.project"],project);
  assert.equal(details.Config.Labels["com.docker.compose.service"],service);
  return details;
}
function sql(query) {
 const pg=container("postgres");
 return command("docker",["exec","-i",pg.Id,"psql",
  "-U","harborops","-d","harborops","-v","ON_ERROR_STOP=1","-At","-c",query],90000);
}
function seedProtectedHistory() {
 const nonce=crypto.randomBytes(7).toString("hex");
 // The durable shipment reference has an uppercase-only database constraint.
 // Tenant slugs and synthetic emails retain lowercase identifiers.
 const shipmentRef="G17-STORAGE-"+nonce.toUpperCase();
 assert.match(shipmentRef,/^[A-Z0-9]+(?:-[A-Z0-9]+)*$/);
 sql(`DO $g17$
DECLARE u uuid:=gen_random_uuid(); o uuid:=gen_random_uuid();
 p1 uuid:=gen_random_uuid();p2 uuid:=gen_random_uuid();
 sh uuid:=gen_random_uuid();e uuid:=gen_random_uuid();
 src uuid:=gen_random_uuid();cred uuid:=gen_random_uuid();
 rc uuid:=gen_random_uuid();ev uuid:=gen_random_uuid();
 at_utc timestamptz:=clock_timestamp();
 payload text:='{"eventType":"TRANSPORT","eventCode":"ARRI","reference":"G17-STORAGE"}';
BEGIN
 INSERT INTO users(id,email,normalized_email,display_name,
  status,security_version,created_at_utc)
 VALUES(u,'g17-storage-${nonce}@example.test','g17-storage-${nonce}@example.test',
  'G17 Synthetic Storage','Active',1,clock_timestamp());
 INSERT INTO organizations(id,slug,display_name,status,version,created_at_utc,updated_at_utc)
 VALUES(o,'g17-storage-${nonce}','G17 Synthetic Storage','Active',1,clock_timestamp(),clock_timestamp());
 INSERT INTO organization_memberships(id,organization_id,user_id,role,status,version,
  created_at_utc,updated_at_utc)
 VALUES(gen_random_uuid(),o,u,'Administrator','Active',1,clock_timestamp(),clock_timestamp());
 INSERT INTO ports(id,organization_id,reference_code,name,country_code,status,
  source_kind,version,created_at_utc,updated_at_utc)
 VALUES(p1,o,'G17-ORIGIN','G17 Origin','TR','Active','Synthetic',1,clock_timestamp(),clock_timestamp()),
 (p2,o,'G17-DEST','G17 Destination','TR','Active','Synthetic',1,clock_timestamp(),clock_timestamp());
 INSERT INTO shipments(id,organization_id,reference_code,origin_port_id,
  destination_port_id,workflow_status,record_status,source_kind,version,
  created_at_utc,updated_at_utc,changed_by_user_id)
 VALUES(sh,o,'${shipmentRef}',p1,p2,'Draft','Active','Synthetic',1,
  clock_timestamp(),clock_timestamp(),u);
 -- Positive M8 history is a complete source-scoped receipt/event/work unit.
 -- Its availability is in the future so the opt-in worker cannot mutate it
 -- during the byte-for-byte storage-preservation window.
 INSERT INTO integration_sources(id,organization_id,source_code,display_name,
  status,capability,version,created_at_utc,updated_at_utc)
 VALUES(src,o,'g17-storage-${nonce}','G17 Synthetic Tracking Source',
  'Active','tracking_event_validate',1,at_utc,at_utc);
 INSERT INTO integration_credentials(id,organization_id,source_id,secret_digest,
  key_version,not_before_utc,expires_at_utc,created_at_utc)
 VALUES(cred,o,src,decode(repeat('a',64),'hex'),'v1',at_utc,
  at_utc+interval '1 day',at_utc);
 INSERT INTO tracking_inbox_receipts(id,organization_id,source_id,credential_id,
  profile,source_event_id,canonical_digest,canonical_payload_utf8,
  received_at_utc,correlation_id)
 VALUES(rc,o,src,cred,'tnt-2.2-harborops-v1','g17-storage-${nonce}',
  digest(convert_to(payload,'UTF8'),'sha256'),convert_to(payload,'UTF8'),
  at_utc,md5(payload));
 INSERT INTO tracking_accepted_events(id,receipt_id,organization_id,source_id,
  event_family,event_code,classifier,external_schema_version,occurred_at_utc,
  transport_reference,raw_payload_utf8,canonical_payload)
 VALUES(ev,rc,o,src,'Transport','ARRI','ACT','2.2.0',at_utc,
  'G17-STORAGE',convert_to(payload,'UTF8'),payload::jsonb);
 INSERT INTO tracking_inbox_work_items(receipt_id,organization_id,source_id,
  state,attempt_count,created_at_utc,updated_at_utc,available_at_utc)
 VALUES(rc,o,src,'Pending',0,at_utc,at_utc,at_utc+interval '1 day');
 INSERT INTO shipment_exceptions(id,organization_id,shipment_id,rule_code,
  occurrence,condition_state,lifecycle_state,severity,confidence,reason_code,
  source_fingerprint,shipment_version,tracking_input_revision,correction_generation,evidence)
 VALUES(e,o,sh,'EquipmentConflict',1,'Active','Open','Warning','Unverified',
  'synthetic_storage_proof',repeat('a',64),1,0,0,'{}'::jsonb);
 INSERT INTO shipment_exception_history(id,organization_id,exception_id,
  action,old_state,new_state,actor_kind,source_fingerprint)
 VALUES(gen_random_uuid(),o,e,'Opened',NULL,'Open','System',repeat('a',64));
END $g17$;`);
 const counts=JSON.parse(sql(`SELECT json_build_object(
  'users',(SELECT count(*) FROM users WHERE email='g17-storage-${nonce}@example.test'),
  'organizations',(SELECT count(*) FROM organizations WHERE slug='g17-storage-${nonce}'),
  'ports',(SELECT count(*) FROM ports WHERE organization_id=(SELECT id FROM organizations WHERE slug='g17-storage-${nonce}')),
  'shipments',(SELECT count(*) FROM shipments WHERE reference_code='${shipmentRef}'),
  'm8_receipts',(SELECT count(*) FROM tracking_inbox_receipts r
    JOIN integration_sources s ON s.id=r.source_id WHERE s.source_code='g17-storage-${nonce}'),
  'm8_events',(SELECT count(*) FROM tracking_accepted_events e
    JOIN integration_sources s ON s.id=e.source_id WHERE s.source_code='g17-storage-${nonce}'),
  'm8_work_items',(SELECT count(*) FROM tracking_inbox_work_items w
    JOIN integration_sources s ON s.id=w.source_id WHERE s.source_code='g17-storage-${nonce}'),
  'm11_exceptions',(SELECT count(*) FROM shipment_exceptions e JOIN shipments s ON s.id=e.shipment_id WHERE s.reference_code='${shipmentRef}'),
  'm11_history',(SELECT count(*) FROM shipment_exception_history h JOIN shipment_exceptions e ON h.exception_id=e.id JOIN shipments s ON s.id=e.shipment_id WHERE s.reference_code='${shipmentRef}'),
  'm15_outbox',(SELECT count(*) FROM m15_notification_outbox x JOIN shipment_exception_history h ON h.id=x.source_history_id JOIN shipment_exceptions e ON e.id=h.exception_id JOIN shipments s ON s.id=e.shipment_id WHERE s.reference_code='${shipmentRef}'),
  'm16_audit',(SELECT count(*) FROM audit_entries)
 )::text`));
 for(const [name,count] of Object.entries(counts))
  assert.ok(count>0,"Missing positive protected synthetic history: "+name);
 assert.equal(counts.users,1);
 assert.equal(counts.organizations,1);
 assert.equal(counts.ports,2);
 assert.equal(counts.shipments,1);
 for(const table of ["m8_receipts","m8_events","m8_work_items"])
  assert.equal(counts[table],1,"Missing complete immutable M8 fixture: "+table);
 return {kind:"synthetic-nonempty-protected-history",tables:counts,
  fixture_sha256:crypto.createHash("sha256").update(nonce).digest("hex")};
}

function snapshotDatabase() {
  const pg=container("postgres");
  // Hash the complete owned application public-schema data stream, including
  // all M8/M11/M15/M16 tables, rather than trusting counts or wiping history.
  const result=execFileSync("docker",["exec",pg.Id,
    "pg_dump","-U","harborops","-d","harborops",
    "--data-only","--schema=public","--no-owner","--no-privileges"],{
      timeout:90000,maxBuffer:64*1024*1024
    });
  const stable=result.toString("utf8").split(/\r?\n/)
    // PostgreSQL 16.15 pg_dump writes a random \restrict/\unrestrict token pair
    // into every dump. It is not data and would make any two hashes differ.
    .filter(line=>!/^(-- (Dumped|Completed|Started) on )/.test(line) &&
      !/^\\(?:un)?restrict\s/.test(line))
    .join("\n");
  return crypto.createHash("sha256").update(stable).digest("hex");
}
function check(name,fn) {
  const value=fn();evidenceData.checks.push({name,status:"PASS",value});return value;
}
async function checkApi() {
  for(const [url,status] of [["/health/live",200],["/health/ready",200],
    ["/api/v1/system/baseline",200]]) {
    const response=await fetch("http://127.0.0.1:18080"+url,
      {signal:AbortSignal.timeout(10000)});
    assert.equal(response.status,status,"Business endpoint changed during local telemetry storage fault");
  }
}
let failure=null;
try {
  const retention=check("owned-retention-config",()=>{
    const prometheus=fs.readFileSync("compose.m17.observability.override.yaml","utf8");
    const tempo=fs.readFileSync("monitoring/m17/tempo.yaml","utf8");
    assert.match(prometheus,/--storage\.tsdb\.retention\.time=24h/);
    assert.match(prometheus,/--storage\.tsdb\.retention\.size=120MB/);
    assert.match(tempo,/block_retention:\s*24h/);
    return {metrics_hours:24,traces_hours:24};
  });
  const cap=check("telemetry-storage-owned-and-under-250-mib",()=>{
    const expected={otelcol:"/tmp",tempo:"/var/tempo",prometheus:"/prometheus",
      grafana:"/var/lib/grafana"};
    const sizes=[];
    for(const [service,mount] of Object.entries(expected)) {
      const info=container(service);
      const opts=info.HostConfig.Tmpfs?.[mount];
      assert.ok(opts&&/size=\d+/.test(opts),"Telemetry store is not an owned tmpfs: "+service);
      sizes.push({service,mount,bytes:Number(opts.match(/size=(\d+)/)[1])});
      assert.ok(!info.Mounts.some(x=>x.Destination===mount&&x.Type==="bind"),
        "Retained bind-mount in telemetry storage: "+service);
    }
    const total=sizes.reduce((n,x)=>n+x.bytes,0);
    assert.ok(total>0&&total<=250*1024*1024,"Storage cap exceeds policy");
    return {total_bytes:total,volumes:sizes};
  });
  const positive=check("positive-m8-m11-m15-m16-retained-history",
    ()=>seedProtectedHistory());
  evidenceData.positive_history=positive;
  const before=snapshotDatabase();
  evidenceData.database_sha256_before=before;
  await checkApi();
  const prometheus=container("prometheus");
  const script="dd if=/dev/zero of=/prometheus/g17-owned-fill.tmp bs=1048576 count=256";
  const fill=spawnSync("docker",["exec",prometheus.Id,"/bin/sh","-c",script],{
    encoding:"utf8",timeout:60000,maxBuffer:512*1024
  });
  // Filling a bounded tmpfs must terminate with ENOSPC, not silently succeed.
  assert.notEqual(fill.status,0,"Store unexpectedly accepted more than its tmpfs cap");
  assert.match(fill.stderr||"",/No space left on device|ENOSPC/i,
    "Pressure command did not prove a full tmpfs: "+String(fill.stderr).slice(0,100));
  fullStoreCreated=true;
  evidenceData.telemetry_degradation={
    signal:"OWNED_PROMETHEUS_STORAGE_ENOSPC",
    write_exit_code:fill.status,error_observed:/No space left on device|ENOSPC/i.test(fill.stderr??"")
  };
  assert.ok(evidenceData.telemetry_degradation.error_observed);
  await checkApi();
  evidenceData.checks.push({name:"business-remains-responsive-during-full-telemetry-store",
    status:"PASS"});
  const after=snapshotDatabase();
  evidenceData.database_sha256_after=after;
  assert.equal(after,before,"Telemetry storage saturation modified application history");
  evidenceData.checks.push({name:"all-application-history-byte-identical",
    status:"PASS",nonempty_protected_tables:positive.tables,
    storage_degradation:evidenceData.telemetry_degradation.signal});
  evidenceData.result="PASS";
}catch(e){
  failure=e;
  evidenceData.failure_type=e?.name??"Error";
  evidenceData.failure_message=String(e?.message??"").slice(0,240);
}finally{
  if(fullStoreCreated) {
    try {
      const pg=container("prometheus");
      command("docker",["exec",pg.Id,"/bin/sh","-c",
        "rm -f /prometheus/g17-owned-fill.tmp"],20000);
      evidenceData.checks.push({name:"test-owned-fill-file-cleanup",status:"PASS"});
    } catch(e) {
      // Recreate only the project-owned disposable Prometheus tmpfs container.
      try {
        compose(["up","-d","--no-deps","--force-recreate","prometheus"]);
        evidenceData.checks.push({name:"test-owned-prometheus-recovery",status:"PASS"});
      } catch {
        evidenceData.result="FAIL";
        failure=failure||e;
      }
    }
  }
  fs.writeFileSync(report,JSON.stringify(evidenceData,null,2)+"\n",{flag:"wx"});
}
if(failure||evidenceData.result!=="PASS")
  throw Error("Owned telemetry storage fault scenario failed; raw evidence retained");
