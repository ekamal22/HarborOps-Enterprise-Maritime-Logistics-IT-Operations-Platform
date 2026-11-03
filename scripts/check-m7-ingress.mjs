#!/usr/bin/env node
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";

const mode = process.argv[2] || "healthy";
assert.ok(["healthy", "outage", "recovered"].includes(mode), "Invalid G7 ingress stage");
const root = process.env.HARBOROPS_VERIFICATION_ROOT;
assert.match(root || "", /^(?:[Ee]:[\\/]|\/e\/)/);
const bearer = readFileSync(join(root, "private", "source-bearer.txt"), "utf8").trim();
assert.match(bearer, /^hop_v1_[0-9a-f]{32}_[A-Za-z0-9_-]{43}$/);
const url = "http://127.0.0.1:5173/integrations/v1/tracking-events";
const body = JSON.stringify({
  sourceEventId:"g7-synthetic-1", eventFamily:"Equipment", eventCode:"GTIN",
  classifier:"ACT", occurredAtUtc:"2026-11-02T10:00:00Z",
  equipmentReference:"SYN-BOX-G7SOURCE", externalSchemaVersion:"2.2.0"
});
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
  assert.equal(typeof parsed.code, "string");
  assert.ok(parsed.correlationId);
  assert.match(result.headers.get("cache-control") || "", /no-store/);
  return { status:result.status, code:parsed.code, retry:result.headers.get("retry-after") };
}
if (mode === "healthy") {
  const missing = await send(null);
  assert.deepEqual([missing.status,missing.code], [401,"invalid_source_credentials"]);
  const valid = await send(bearer);
  assert.deepEqual([valid.status,valid.code], [503,"intake_unavailable"]);
  assert.ok(valid.retry);
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
} else {
  const resumed = await send(bearer);
  assert.deepEqual([resumed.status,resumed.code],[503,"intake_unavailable"]);
}
console.log("G7 source-bound ingress " + mode + ": PASS");
