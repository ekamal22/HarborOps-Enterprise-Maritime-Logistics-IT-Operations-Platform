// Disposable G10 browser fixture: one North shipment with a resolvable container, two real accepted
// tracking events processed by the real restricted-role worker, and an authorized rebuild, so the
// browser journey shows genuine derived data instead of mocked responses.
// Requires the combined/G10 Compose stack (project in G16_PROJECT, COMPOSE_FILE exported by the runner).
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash, randomBytes, randomUUID } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const base = process.env.HARBOROPS_BROWSER_BASE_URL || "http://127.0.0.1:5173";
const ingestBase = process.env.G16_API_BASE || "http://127.0.0.1:8086";
const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
const project = process.env.G16_PROJECT;
assert.ok(password && password.length >= 20, "G10 fixture password missing");
assert.match(project || "", /^ho-verify-cg16-[a-z0-9]{1,12}$/, "G10 stack project missing");
const root = process.env.HARBOROPS_VERIFICATION_ROOT;
const sha = process.env.HARBOROPS_SOURCE_SHA;
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root), "G10 fixture evidence must reside on E:");
assert.match(sha || "", /^[0-9a-f]{40}$/);

const cookies = new Map();
async function request(method, url, data, extraHeaders = {}) {
  const headers = { Origin: base, ...extraHeaders };
  if (cookies.size) headers.Cookie = [...cookies].map(([k, v]) => k + "=" + v).join("; ");
  if (method !== "GET") {
    const csrf = await request("GET", "/api/v1/auth/csrf");
    headers["X-HarborOps-CSRF"] = csrf.token;
    headers.Cookie = [...cookies].map(([k, v]) => k + "=" + v).join("; ");
  }
  if (data !== undefined) headers["Content-Type"] = "application/json";
  const response = await fetch(base + url, { method, headers,
    ...(data === undefined ? {} : { body: JSON.stringify(data) }) });
  for (const value of response.headers.getSetCookie()) {
    const [part] = value.split(";");
    const index = part.indexOf("=");
    if (index > 0) cookies.set(part.slice(0, index), part.slice(index + 1));
  }
  const body = await response.json().catch(() => null);
  assert.ok(response.ok, method + " " + url + " " + response.status + " " + JSON.stringify(body).slice(0, 250));
  return body;
}
function psql(sql) {
  const run = spawnSync("docker", ["compose", "-p", project, "exec", "-T", "postgres", "psql", "-X",
    "-v", "ON_ERROR_STOP=1", "-U", "harborops", "-d", "harborops", "-Atq", "-c", sql],
  { encoding: "utf8", timeout: 60_000 });
  assert.equal(run.status, 0, "psql failed: " + (run.stderr || "").slice(0, 200));
  return (run.stdout || "").trim();
}
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

await request("POST", "/api/v1/auth/login", { email: "m3-manager@example.test", password });
const orgs = (await request("GET", "/api/v1/me/organizations")).organizations;
const org = orgs.find(x => x.slug === "m3-north" && x.role === "Manager");
assert.ok(org, "G10 North Manager fixture missing");
await request("PUT", "/api/v1/auth/session/organization", { organizationId: org.id });
const api = "/api/v1/organizations/" + org.id;

const voyage = (await request("GET", api + "/voyages?recordStatus=Active&limit=100")).voyages
  .find(x => x.voyageStatus === "Planned" && x.originPortId && x.destinationPortId);
assert.ok(voyage, "G10 requires a route-compatible planned voyage");
const identifier = "SYN-BOX-G10N";
const container = await request("POST", api + "/containers", {
  referenceCode: "G10-LIVE-BOX", identifierKind: "Synthetic", containerIdentifier: identifier, equipmentType: "Dry20" });
const shipment = await request("POST", api + "/shipments", {
  referenceCode: "G10-LIVE-NORTH", originPortId: voyage.originPortId, destinationPortId: voyage.destinationPortId });
await request("POST", api + "/shipments/" + shipment.id + "/container-assignments",
  { containerId: container.id, startsAtUtc: "2026-11-03T09:00:00Z" });

const sourceId = randomUUID(), credentialId = randomUUID(), secret = randomBytes(32);
const digest = createHash("sha256").update(secret).digest("hex");
const bearer = "hop_v1_" + credentialId.replaceAll("-", "") + "_" + secret.toString("base64url");
psql(`BEGIN;
  INSERT INTO integration_sources (id,organization_id,source_code,display_name,status,capability,version,created_at_utc,updated_at_utc)
  VALUES ('${sourceId}','${org.id}','g10_browser','Synthetic G10 Browser Source','Active','tracking_event_validate',1,now(),now());
  INSERT INTO integration_credentials (id,organization_id,source_id,secret_digest,key_version,not_before_utc,expires_at_utc,created_at_utc)
  VALUES ('${credentialId}','${org.id}','${sourceId}',decode('${digest}','hex'),'v1',now(),now()+interval '90 days',now());
  COMMIT;`);
const nonce = randomBytes(4).toString("hex");
for (const [key, code, at] of [["g10-browser-in-" + nonce, "GTIN", "2026-11-03T10:00:00Z"],
  ["g10-browser-out-" + nonce, "GTOT", "2026-11-03T11:00:00Z"]]) {
  const response = await fetch(ingestBase + "/integrations/v1/tracking-events", {
    method: "POST", signal: AbortSignal.timeout(30_000),
    headers: { "Content-Type": "application/json; charset=utf-8", Authorization: "Bearer " + bearer,
      "X-HarborOps-Tracking-Profile": "tnt-2.2-harborops-v1" },
    body: JSON.stringify({ sourceEventId: key, eventFamily: "Equipment", eventCode: code, classifier: "ACT",
      occurredAtUtc: at, equipmentReference: identifier, externalSchemaVersion: "2.2.0" }) });
  assert.equal(response.status, 202, "tracking ingest " + response.status);
}
let effects = "0";
for (let attempt = 0; attempt < 90 && effects !== "2"; attempt += 1) {
  effects = psql(`SELECT count(*) FROM tracking_processing_effects WHERE shipment_id='${shipment.id}'`);
  if (effects !== "2") await sleep(1000);
}
assert.equal(effects, "2", "the restricted worker did not resolve both events");
const rebuilt = await request("POST", api + "/shipments/" + shipment.id + "/tracking-projection/rebuild",
  undefined, { "Idempotency-Key": randomUUID() });
assert.ok(rebuilt.materializationRevision >= 1);

const evidenceDir = path.join(root, "evidence");
fs.mkdirSync(evidenceDir, { recursive: true });
fs.writeFileSync(path.join(evidenceDir, "g10-browser-fixture.json"), JSON.stringify({
  sourceSha: sha, organizationId: org.id, shipmentId: shipment.id, shipmentReference: "G10-LIVE-NORTH",
  containerIdentifier: identifier, effects: 2, materializationRevision: rebuilt.materializationRevision }, null, 2) + "\n");
console.log("G10 browser fixture ready: 2 resolved events, materialization revision " + rebuilt.materializationRevision);
