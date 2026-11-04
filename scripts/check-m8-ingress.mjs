#!/usr/bin/env node
import assert from "node:assert/strict";
import { readFileSync, writeFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { join } from "node:path";

const mode = process.argv[2] || "healthy";
assert.ok(["healthy", "outage", "recovered"].includes(mode), "Invalid G8 ingress stage");
const root = process.env.HARBOROPS_VERIFICATION_ROOT;
assert.match(root || "", /^(?:[Ee]:[\\/]|\/e\/)/);
const bearer = readFileSync(join(root, "private", "source-bearer.txt"), "utf8").trim();
assert.match(bearer, /^hop_v1_[0-9a-f]{32}_[A-Za-z0-9_-]{43}$/);
const url = "http://127.0.0.1:5178/integrations/v1/tracking-events";
const body = JSON.stringify({
  sourceEventId:"g8-synthetic-1", eventFamily:"Equipment", eventCode:"GTIN",
  classifier:"ACT", occurredAtUtc:"2026-11-02T10:00:00Z",
  equipmentReference:"SYN-BOX-G8SOURCE", externalSchemaVersion:"2.2.0"
});
function snapshotReceipt(receiptId) {
  assert.match(receiptId, /^[a-f0-9-]{36}$/i, "Only a validated receipt UUID may be queried");
  const sql = `
    SELECT row_to_json(snapshot) FROM (
      SELECT r.id AS "receiptId", r.received_at_utc AS "receivedAt",
             encode(r.canonical_digest,'hex') AS "canonicalDigest",
             encode(r.canonical_payload_utf8,'hex') AS "canonicalPayload",
             encode(e.raw_payload_utf8,'hex') AS "rawAcceptedPayload",
             e.canonical_payload::text AS "canonicalEvent",
             w.receipt_id AS "workReceiptId",
             w.state AS "workState", w.attempt_count AS "attemptCount",
             w.created_at_utc AS "workCreatedAt",
             w.updated_at_utc AS "workUpdatedAt"
        FROM tracking_inbox_receipts r
        JOIN tracking_accepted_events e ON e.receipt_id = r.id
        JOIN tracking_inbox_work_items w ON w.receipt_id = r.id
       WHERE r.id = '${receiptId}'::uuid
         AND e.organization_id = r.organization_id AND e.source_id = r.source_id
         AND w.organization_id = r.organization_id AND w.source_id = r.source_id
    ) AS snapshot`;
  const raw = execFileSync("docker", ["compose", "-p", "ho-verify-g8", "exec", "-T",
    "postgres", "psql", "-U", "harborops", "-d", "harborops", "-Atc", sql],
    { encoding:"utf8", timeout:20000 }).trim();
  const row = JSON.parse(raw);
  assert.ok(row && row.receiptId === receiptId, "Tracking history snapshot must exist");
  assert.equal(row.workState, "Pending");
  assert.equal(row.attemptCount, 0);
  assert.ok(row.canonicalDigest && row.canonicalPayload && row.rawAcceptedPayload);
  return row;
}
async function send(token, data=body, profile="tnt-2.2-harborops-v1", headers={}) {
  const result = await fetch(url, {
    method:"POST", headers: {
      "Content-Type":"application/json; charset=utf-8",
      "X-HarborOps-Tracking-Profile":profile,
      ...(token ? {Authorization:"Bearer " + token} : {}),
      ...headers
    },
    body:data, signal:AbortSignal.timeout(15_000)
  });
  const content = await result.text();
  let parsed;
  try { parsed = JSON.parse(content); } catch { parsed = {}; }
  assert.ok(typeof parsed.code === "string" || parsed.status === "received");
  assert.ok(parsed.correlationId);
  assert.match(result.headers.get("cache-control") || "", /no-store/);
  return { status:result.status, code:parsed.code, receiptId:parsed.receiptId,
    duplicate:parsed.duplicate, receivedAtUtc:parsed.receivedAtUtc,
    retry:result.headers.get("retry-after") };
}
if (mode === "healthy") {
  const missing = await send(null);
  assert.deepEqual([missing.status,missing.code], [401,"invalid_source_credentials"]);
  const valid = await send(bearer);
  assert.equal(valid.status, 202);
  assert.ok(valid.receiptId);
  writeFileSync(join(root, "evidence", "g8-ingress-receipt.json"),
    JSON.stringify({ receiptId:valid.receiptId, receivedAtUtc:valid.receivedAtUtc }));
  // Retain byte-level snapshot in private E:-backed storage, never in the
  // shareable evidence package. Recovery compares every durable field.
  const snapshot = snapshotReceipt(valid.receiptId);
  writeFileSync(join(root, "private", "g8-receipt-snapshot.json"),
    JSON.stringify(snapshot), { flag:"wx", mode:0o600 });
  const replay = await send(bearer);
  assert.equal(replay.status, 202);
  assert.equal(replay.receiptId, valid.receiptId);
  assert.equal(replay.duplicate, true);
  const unsupported = await send(bearer,body,"unapproved");
  assert.deepEqual([unsupported.status,unsupported.code],[422,"unsupported_tracking_profile"]);
  const spoofed = await send(bearer,
    body.replace('"externalSchemaVersion":','"organizationId":"foreign","externalSchemaVersion":'));
  assert.equal(spoofed.status,422);
  const malformed = await send(bearer, "{");
  assert.equal(malformed.status,400);
  const oversized = await send(bearer," ".repeat(16385));
  assert.equal(oversized.status,413);
} else if (mode === "outage") {
  const offline = await send(bearer);
  assert.deepEqual([offline.status,offline.code],[503,"dependency_unavailable"]);
  assert.ok(offline.retry, "Database outage must include retry guidance");
} else {
  const resumed = await send(bearer);
  assert.equal(resumed.status,202);
  assert.ok(resumed.receiptId);
  const first = JSON.parse(readFileSync(join(root, "evidence", "g8-ingress-receipt.json"), "utf8"));
  assert.equal(resumed.receiptId, first.receiptId);
  assert.equal(resumed.receivedAtUtc, first.receivedAtUtc);
  assert.equal(resumed.duplicate, true);
  const initial = JSON.parse(readFileSync(
    join(root, "private", "g8-receipt-snapshot.json"), "utf8"));
  const recovered = snapshotReceipt(resumed.receiptId);
  assert.deepEqual(recovered, initial,
    "PostgreSQL recovery must preserve exact raw bytes, canonical digest, receipt and Pending state");
  writeFileSync(join(root, "evidence", "g8-restart-integrity.json"),
    JSON.stringify({
      sourceSha: process.env.HARBOROPS_SOURCE_SHA,
      runId: readFileSync(join(root, "evidence", "run-id.txt"), "utf8").trim(),
      receiptId: resumed.receiptId, status:"PASS",
      rawBytesUnchanged:true, canonicalDigestUnchanged:true,
      canonicalEventUnchanged:true, pendingWorkUnchanged:true
    }), { flag:"wx" });
}
console.log("G8 source-bound ingress " + mode + ": PASS");
