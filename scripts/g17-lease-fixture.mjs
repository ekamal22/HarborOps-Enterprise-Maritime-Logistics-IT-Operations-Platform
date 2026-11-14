import crypto from "node:crypto";
import assert from "node:assert/strict";

// Owned synthetic worker-lease fixture for the live disposable stack (S25-F07).
// A complete M8 receipt, accepted event and work item is created for a private
// synthetic organization, then leased through the same state transition the
// database trigger allows for a real claim (Pending to Leased, epoch 1, attempt 1).
// The lease is deliberately long enough to outlive the restart observation, and
// short enough for the trigger bound of ten minutes.
export function seedLeasedSyntheticWork(sql, leaseMinutes = 9) {
  assert.ok(leaseMinutes > 0 && leaseMinutes < 10);
  const nonce = crypto.randomBytes(7).toString("hex");
  const ids = {
    org: crypto.randomUUID(), source: crypto.randomUUID(), credential: crypto.randomUUID(),
    receipt: crypto.randomUUID(), event: crypto.randomUUID(), token: crypto.randomUUID()
  };
  const payload = '{"eventType":"EQUIPMENT","eventCode":"GTIN","reference":"G17-LEASE-' +
    nonce.toUpperCase() + '"}';
  sql(`DO $g17$
DECLARE at_utc timestamptz:=clock_timestamp();
 payload text:='${payload}';
BEGIN
 INSERT INTO organizations(id,slug,display_name,status,version,created_at_utc,updated_at_utc)
 VALUES('${ids.org}','g17-lease-${nonce}','G17 Synthetic Lease','Active',1,at_utc,at_utc);
 INSERT INTO integration_sources(id,organization_id,source_code,display_name,
  status,capability,version,created_at_utc,updated_at_utc)
 VALUES('${ids.source}','${ids.org}','g17-lease-${nonce}','G17 Synthetic Lease Source',
  'Active','tracking_event_validate',1,at_utc,at_utc);
 INSERT INTO integration_credentials(id,organization_id,source_id,secret_digest,
  key_version,not_before_utc,expires_at_utc,created_at_utc)
 VALUES('${ids.credential}','${ids.org}','${ids.source}',decode(repeat('b',64),'hex'),
  'v1',at_utc,at_utc+interval '1 day',at_utc);
 INSERT INTO tracking_inbox_receipts(id,organization_id,source_id,credential_id,
  profile,source_event_id,canonical_digest,canonical_payload_utf8,
  received_at_utc,correlation_id)
 VALUES('${ids.receipt}','${ids.org}','${ids.source}','${ids.credential}',
  'tnt-2.2-harborops-v1','g17-lease-${nonce}',
  digest(convert_to(payload,'UTF8'),'sha256'),convert_to(payload,'UTF8'),
  at_utc,md5(payload));
 INSERT INTO tracking_accepted_events(id,receipt_id,organization_id,source_id,
  event_family,event_code,classifier,external_schema_version,occurred_at_utc,
  transport_reference,raw_payload_utf8,canonical_payload)
 VALUES('${ids.event}','${ids.receipt}','${ids.org}','${ids.source}','Transport','ARRI',
  'ACT','2.2.0',at_utc,'G17-LEASE',convert_to(payload,'UTF8'),payload::jsonb);
 INSERT INTO tracking_inbox_work_items(receipt_id,organization_id,source_id,
  state,attempt_count,created_at_utc,updated_at_utc,available_at_utc)
 VALUES('${ids.receipt}','${ids.org}','${ids.source}','Pending',0,at_utc,at_utc,at_utc);
 UPDATE tracking_inbox_work_items
  SET state='Leased',attempt_count=1,lease_epoch=1,lease_token='${ids.token}',
      lease_expires_at_utc=clock_timestamp()+interval '${leaseMinutes} minutes',
      updated_at_utc=clock_timestamp()
  WHERE receipt_id='${ids.receipt}';
END $g17$;`);
  return { nonce, ...ids };
}

// Complete observable state of one lease: the work row, its immutable attempt
// history, durable effects, and the receipt and event it belongs to.
export function leaseSnapshot(sql, receipt) {
  assert.match(receipt, /^[0-9a-f-]{36}$/);
  const raw = sql(`SELECT json_build_object(
   'work',(SELECT row_to_json(w) FROM tracking_inbox_work_items w WHERE w.receipt_id='${receipt}'),
   'attempt_events',(SELECT coalesce(json_agg(json_build_object('kind',e.event_kind,
     'attempt',e.attempt_count,'epoch',e.lease_epoch,'generation',e.generation)
     ORDER BY e.occurred_at_utc,e.id),'[]'::json)
     FROM tracking_work_attempt_events e WHERE e.receipt_id='${receipt}'),
   'effects',(SELECT count(*) FROM tracking_processing_effects x WHERE x.receipt_id='${receipt}'),
   'receipts',(SELECT count(*) FROM tracking_inbox_receipts r WHERE r.id='${receipt}'),
   'events',(SELECT count(*) FROM tracking_accepted_events a WHERE a.receipt_id='${receipt}'))::text`);
  return JSON.parse(raw);
}

// The work row fields that define lease identity and exclusivity.
export function leaseIdentity(snapshot) {
  const w = snapshot.work;
  return {
    state: w.state, lease_token: w.lease_token, lease_epoch: w.lease_epoch,
    attempt_count: w.attempt_count, lease_expires_at_utc: w.lease_expires_at_utc,
    updated_at_utc: w.updated_at_utc, redrive_generation: w.redrive_generation
  };
}

// An owned, eligible, deliberately old pending work item for a private synthetic
// organization. It is created while no worker runs, so a held or stopped worker has a
// genuinely stale eligible backlog to suppress an alert for (S18-A02). Its first claim
// by a restarted worker resolves it to NeedsReconciliation (Transport family).
export function seedStaleEligibleWork(sql, ageSeconds = 600) {
  assert.ok(Number.isInteger(ageSeconds) && ageSeconds >= 300 && ageSeconds <= 3600);
  const nonce = crypto.randomBytes(7).toString("hex");
  const ids = {
    org: crypto.randomUUID(), source: crypto.randomUUID(), credential: crypto.randomUUID(),
    receipt: crypto.randomUUID(), event: crypto.randomUUID()
  };
  const payload = '{"eventType":"TRANSPORT","eventCode":"ARRI","reference":"G17-STALE-' +
    nonce.toUpperCase() + '"}';
  sql(`DO $g17$
DECLARE at_utc timestamptz:=clock_timestamp()-interval '${ageSeconds} seconds';
 payload text:='${payload}';
BEGIN
 INSERT INTO organizations(id,slug,display_name,status,version,created_at_utc,updated_at_utc)
 VALUES('${ids.org}','g17-stale-${nonce}','G17 Synthetic Stale','Active',1,at_utc,at_utc);
 INSERT INTO integration_sources(id,organization_id,source_code,display_name,
  status,capability,version,created_at_utc,updated_at_utc)
 VALUES('${ids.source}','${ids.org}','g17-stale-${nonce}','G17 Synthetic Stale Source',
  'Active','tracking_event_validate',1,at_utc,at_utc);
 INSERT INTO integration_credentials(id,organization_id,source_id,secret_digest,
  key_version,not_before_utc,expires_at_utc,created_at_utc)
 VALUES('${ids.credential}','${ids.org}','${ids.source}',decode(repeat('c',64),'hex'),
  'v1',at_utc,at_utc+interval '1 day',at_utc);
 INSERT INTO tracking_inbox_receipts(id,organization_id,source_id,credential_id,
  profile,source_event_id,canonical_digest,canonical_payload_utf8,
  received_at_utc,correlation_id)
 VALUES('${ids.receipt}','${ids.org}','${ids.source}','${ids.credential}',
  'tnt-2.2-harborops-v1','g17-stale-${nonce}',
  digest(convert_to(payload,'UTF8'),'sha256'),convert_to(payload,'UTF8'),
  at_utc,md5(payload));
 INSERT INTO tracking_accepted_events(id,receipt_id,organization_id,source_id,
  event_family,event_code,classifier,external_schema_version,occurred_at_utc,
  transport_reference,raw_payload_utf8,canonical_payload)
 VALUES('${ids.event}','${ids.receipt}','${ids.org}','${ids.source}','Transport','ARRI',
  'ACT','2.2.0',at_utc,'G17-STALE',convert_to(payload,'UTF8'),payload::jsonb);
 INSERT INTO tracking_inbox_work_items(receipt_id,organization_id,source_id,
  state,attempt_count,created_at_utc,updated_at_utc,available_at_utc)
 VALUES('${ids.receipt}','${ids.org}','${ids.source}','Pending',0,at_utc,at_utc,at_utc);
END $g17$;`);
  return { nonce, ...ids };
}

// Age in seconds of the oldest eligible pending work, as the worker's own backlog
// gauge defines it (Pending or RetryWaiting, available, in an Active organization).
export function oldestEligibleBacklogSeconds(sql) {
  const raw = sql(`SELECT coalesce(extract(epoch FROM (clock_timestamp()-min(w.created_at_utc))),-1)::text
   FROM tracking_inbox_work_items w JOIN organizations o ON o.id=w.organization_id
   WHERE o.status='Active' AND w.state IN ('Pending','RetryWaiting')
     AND w.available_at_utc<=clock_timestamp()`);
  return Number(raw);
}
