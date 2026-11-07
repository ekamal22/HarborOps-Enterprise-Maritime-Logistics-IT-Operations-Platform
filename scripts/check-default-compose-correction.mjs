#!/usr/bin/env node
// Default-stack correction smoke check against a disposable ho-verify-fm* Compose project only.
// COMPOSE_FILE layers the repository compose.yaml, compose.m9.worker.yaml and a private port/subnet
// override kept outside the repository. HARBOROPS_M10_CORRECTION_PROOF_KEY is read from the
// environment and is never printed, written to evidence or passed on a command line.
// It proves that the default configuration supplies the correction signing key to the privileged
// migrator and the HTTP API only, and that an authorized Manager correction then signs, passes the
// database verifier, persists the correction and writes its M16 audit entry.
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash, randomBytes, randomUUID } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const project = process.env.HARBOROPS_FM_PROJECT || "";
assert.match(project, /^ho-verify-fm[a-z0-9-]{0,16}$/, "Invalid default-stack Compose project name");
const outDir = process.argv[2];
assert.match(outDir || "", /^(?:[Ee]:[\\/]|\/e\/)/, "An E: evidence directory is required");
mkdirSync(outDir, { recursive: true });
const proofKey = process.env.HARBOROPS_M10_CORRECTION_PROOF_KEY;
assert.ok(proofKey && Buffer.from(proofKey, "base64").length === 32, "A 32-byte base64 proof key is required");
const base = process.env.HARBOROPS_FM_BASE || "http://127.0.0.1:18091";
// Mutations require a same-origin Origin header, so requests present the API address they use.
const origin = process.env.HARBOROPS_FM_ORIGIN || base;
const results = [];
const record = (id, ok, detail = "") => {
  results.push({ id, status: ok ? "PASS" : "FAIL", detail });
  console.log(`${ok ? "PASS" : "FAIL"} ${id}${detail ? " - " + detail : ""}`);
};
async function check(id, fn) {
  try {
    const detail = await fn();
    record(id, true, typeof detail === "string" ? detail : "");
  } catch (error) {
    record(id, false, String(error?.message ?? error).split(proofKey).join("[redacted]").replace(/\s+/g, " ").slice(0, 400));
  }
}
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const compose = (args, { input, timeout = 180_000 } = {}) =>
  spawnSync("docker", ["compose", "-p", project, "--profile", "worker", ...args],
    { encoding: "utf8", input, timeout, env: process.env, maxBuffer: 64 * 1024 * 1024 });
function psql(sql) {
  const run = compose(["exec", "-T", "postgres", "psql", "-U", "harborops", "-d", "harborops", "-At", "-c", sql], { timeout: 60_000 });
  return { code: run.status, out: (run.stdout || "").trim(), err: (run.stderr || "").trim() };
}
const scalar = sql => {
  const result = psql(sql);
  assert.equal(result.code, 0, "psql failed: " + result.err.slice(0, 200));
  return result.out;
};
async function waitFor(label, fn, timeoutMs = 90_000, stepMs = 1000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const value = await fn();
    if (value) return value;
    await sleep(stepMs);
  }
  throw new Error("Timed out waiting for " + label);
}

class Session {
  jar = new Map();
  headers(extra = {}) {
    const headers = { Origin: origin, ...extra };
    if (this.jar.size) headers.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
    return headers;
  }
  capture(response) {
    for (const cookie of response.headers.getSetCookie?.() ?? []) {
      const [pair] = cookie.split(";");
      const index = pair.indexOf("=");
      this.jar.set(pair.slice(0, index), pair.slice(index + 1));
    }
  }
  async csrfToken() {
    const response = await fetch(base + "/api/v1/auth/csrf", { headers: this.headers(), signal: AbortSignal.timeout(15_000) });
    this.capture(response);
    return (await response.json()).token;
  }
  async send(method, path, body, { csrf = true, headers = {} } = {}) {
    // The antiforgery cookie is set by the token request, so fetch the token before building headers.
    const token = csrf ? await this.csrfToken() : null;
    const requestHeaders = this.headers(headers);
    if (token) requestHeaders["X-HarborOps-CSRF"] = token;
    if (body !== undefined) requestHeaders["Content-Type"] = "application/json";
    const response = await fetch(base + path, {
      method, headers: requestHeaders, body: body === undefined ? undefined : JSON.stringify(body),
      redirect: "manual", signal: AbortSignal.timeout(30_000)
    });
    this.capture(response);
    const text = await response.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* non-JSON body */ }
    return { status: response.status, json, text };
  }
}

const nonce = randomBytes(4).toString("hex");
const accountPassword = randomBytes(24).toString("hex");
const emails = Object.fromEntries(["admin", "manager", "analyst"].map(role => [role, `fm-${role}-${nonce}@example.test`]));
const slug = "fm-stack-" + nonce;
let organizationId = null;
const sessions = {};

await check("default-compose-resolves-key-for-migrator-and-api-only", async () => {
  const config = compose(["config", "--format", "json"]);
  assert.equal(config.status, 0, "compose config failed");
  const services = JSON.parse(config.stdout).services;
  const holders = Object.entries(services)
    .filter(([, service]) => (service.environment ?? {}).M10CorrectionProof__Key !== undefined).map(([name]) => name).sort();
  assert.deepEqual(holders, ["api", "migrator"], "only the migrator and the API may receive the key");
  for (const name of holders) assert.equal(services[name].environment.M10CorrectionProof__Key, proofKey, name + " must receive the same configured key");
  for (const [name, service] of Object.entries(services)) {
    const args = JSON.stringify(service.build?.args ?? {});
    assert.ok(!args.includes(proofKey), name + " build arguments must not carry the key");
    assert.ok(!JSON.stringify(service.command ?? []).includes(proofKey), name + " command must not carry the key");
  }
  assert.equal(services.api.environment.ConnectionStrings__HarborOps.includes("Username=harborops_api;"), true, "API keeps the restricted login");
  assert.equal(services.migrator.environment.ConnectionStrings__HarborOps.includes("Username=harborops;"), true, "migrator keeps the privileged login");
  return "migrator and api only; build arguments and commands carry no key";
});

await check("stack-services-ready", async () => {
  const ready = await waitFor("api readiness", async () => {
    try { return (await fetch(base + "/health/ready", { signal: AbortSignal.timeout(5_000) })).status === 200; }
    catch { return false; }
  }, 120_000, 2000);
  assert.ok(ready, "api /health/ready never returned 200");
  const worker = await waitFor("worker healthy", () => {
    const state = compose(["ps", "worker", "--format", "{{.State}} {{.Health}}"]).stdout.trim();
    return state === "running healthy" ? state : null;
  }, 90_000, 3000);
  return "api ready, worker " + worker;
});

await check("runtime-key-holders-match-configuration", async () => {
  const names = service => compose(["exec", "-T", service, "sh", "-c", "env | cut -d= -f1"]).stdout.split(/\s+/);
  assert.ok(names("api").includes("M10CorrectionProof__Key"), "running api must hold the key");
  assert.ok(!names("worker").includes("M10CorrectionProof__Key"), "running worker must not hold the key");
  assert.ok(!names("postgres").includes("M10CorrectionProof__Key"), "postgres must not hold the key");
  const migrator = compose(["ps", "-a", "migrator", "--format", "{{.State}} {{.ExitCode}}"]).stdout.trim();
  assert.equal(migrator, "exited 0", "migrator must complete successfully with the key");
  return "api holds the key; worker and postgres do not; migrator exited 0";
});

await check("database-verifier-matches-supplied-key-without-disclosure", async () => {
  assert.equal(scalar("SELECT count(*) FROM m10_correction_proof_keys"), "1");
  assert.equal(scalar("SELECT octet_length(secret) FROM m10_correction_proof_keys"), "32");
  assert.equal(scalar("SELECT encode(secret,'base64') FROM m10_correction_proof_keys") === proofKey, true, "verifier must equal the supplied key");
  return "one 32-byte verifier, equal to the supplied key (not printed)";
});

await check("provision-accounts-and-organization", async () => {
  for (const [role, email] of Object.entries(emails)) {
    const run = compose(["run", "--rm", "-T", "-e", "HARBOROPS_ALLOW_ACCOUNT_CLI=true", "api",
      "--account-cli", "provision", email, "Synthetic FM " + role], { input: accountPassword + "\n" });
    assert.equal(run.status, 0, "account provisioning failed for " + role);
  }
  const org = compose(["run", "--rm", "-T", "-e", "HARBOROPS_ALLOW_ORGANIZATION_CLI=true", "api",
    "--organization-cli", "bootstrap", slug, "FM Stack Organization", emails.admin]);
  assert.equal(org.status, 0, "organization bootstrap failed");
  organizationId = scalar(`SELECT id FROM organizations WHERE slug='${slug}'`);
  assert.match(organizationId, /^[0-9a-f-]{36}$/);
});

const login = async (role, email) => {
  const session = new Session();
  const result = await session.send("POST", "/api/v1/auth/login", { email, password: accountPassword });
  assert.equal(result.status, 200, role + " login " + result.status);
  const selected = await session.send("PUT", "/api/v1/auth/session/organization", { organizationId });
  assert.ok([200, 409].includes(selected.status), role + " select organization " + selected.status);
  sessions[role] = session;
};
await check("memberships-and-sessions", async () => {
  await login("admin", emails.admin);
  for (const role of ["manager", "analyst"]) {
    const userId = scalar(`SELECT id FROM users WHERE email='${emails[role]}'`);
    const put = await sessions.admin.send("PUT", `/api/v1/organizations/${organizationId}/memberships/${userId}`,
      { role: role[0].toUpperCase() + role.slice(1), status: "Active" });
    assert.equal(put.status, 200, "membership " + role + " " + put.status);
    await login(role, emails[role]);
  }
});

const root = () => `/api/v1/organizations/${organizationId}`;
let fixture = null;
await check("seed-maritime-fixture", async () => {
  const create = async (session, path, body) => {
    const result = await session.send("POST", root() + path, body);
    assert.equal(result.status, 201, path + " " + result.status);
    return result.json.id;
  };
  const tag = nonce.toUpperCase();
  const a = await create(sessions.manager, "/ports", { referenceCode: "FM-PA-" + tag, name: "FM Origin", countryCode: "US" });
  const b = await create(sessions.manager, "/ports", { referenceCode: "FM-PB-" + tag, name: "FM Destination", countryCode: "US" });
  const container = await create(sessions.manager, "/containers", {
    referenceCode: "FM-BOX-" + tag, identifierKind: "Synthetic", containerIdentifier: "SYN-BOX-FM" + tag, equipmentType: "Dry20" });
  const shipment = await create(sessions.analyst, "/shipments", { referenceCode: "FM-S1-" + tag, originPortId: a, destinationPortId: b });
  const assignment = await sessions.analyst.send("POST", `${root()}/shipments/${shipment}/container-assignments`,
    { containerId: container, startsAtUtc: "2026-11-03T09:00:00Z" });
  assert.equal(assignment.status, 201, "assignment " + assignment.status);
  fixture = { container, shipment, identifier: "SYN-BOX-FM" + tag };
});

let bearer = null;
await check("provision-source", async () => {
  const sourceId = randomUUID(), credentialId = randomUUID(), secret = randomBytes(32);
  const digest = createHash("sha256").update(secret).digest("hex");
  bearer = "hop_v1_" + credentialId.replaceAll("-", "") + "_" + secret.toString("base64url");
  assert.match(scalar(`BEGIN;
    INSERT INTO integration_sources (id,organization_id,source_code,display_name,status,capability,version,created_at_utc,updated_at_utc)
    VALUES ('${sourceId}','${organizationId}','fm_stack','Synthetic FM Source','Active','tracking_event_validate',1,now(),now());
    INSERT INTO integration_credentials (id,organization_id,source_id,secret_digest,key_version,not_before_utc,expires_at_utc,created_at_utc)
    VALUES ('${credentialId}','${organizationId}','${sourceId}',decode('${digest}','hex'),'v1',now(),now()+interval '90 days',now());
    COMMIT;`), /COMMIT/);
});

let receipt = null;
await check("worker-processes-one-resolved-event", async () => {
  const response = await fetch(base + "/integrations/v1/tracking-events", {
    method: "POST", signal: AbortSignal.timeout(30_000),
    headers: { "Content-Type": "application/json; charset=utf-8", Authorization: "Bearer " + bearer, "X-HarborOps-Tracking-Profile": "tnt-2.2-harborops-v1" },
    body: JSON.stringify({ sourceEventId: "fm-stack-" + nonce, eventFamily: "Equipment", eventCode: "GTIN", classifier: "ACT",
      occurredAtUtc: "2026-11-03T10:00:00Z", equipmentReference: fixture.identifier, externalSchemaVersion: "2.2.0" })
  });
  assert.equal(response.status, 202);
  receipt = (await response.json()).receiptId;
  await waitFor("work processed", () => scalar(`SELECT state FROM tracking_inbox_work_items WHERE receipt_id='${receipt}'`) === "Processed", 90_000, 1000);
  assert.equal(scalar(`SELECT count(*) FROM shipment_tracking_timeline_entries WHERE receipt_id='${receipt}'`), "1");
});

await check("manager-correction-signs-verifies-persists-and-audits", async () => {
  const shipmentRoot = `${root()}/shipments/${fixture.shipment}`;
  const correctionPath = shipmentRoot + "/tracking-corrections";
  const withKey = key => ({ headers: { "Idempotency-Key": key } });
  const body = { targetReceiptId: receipt, replacementReceiptId: null, expectedGeneration: 0, reason: "Confirmed invalid first observation" };
  const denied = await sessions.analyst.send("POST", correctionPath, body, withKey(randomUUID()));
  assert.equal(denied.status, 403, "Analyst must not correct");
  assert.equal(scalar(`SELECT count(*) FROM shipment_tracking_corrections WHERE organization_id='${organizationId}'`), "0");
  const key = randomUUID();
  const applied = await sessions.manager.send("POST", correctionPath, body, withKey(key));
  assert.equal(applied.status, 200, "correction " + applied.status + " " + applied.text.slice(0, 120));
  assert.equal(JSON.stringify(applied.json).includes(proofKey), false);
  const replay = await sessions.manager.send("POST", correctionPath, body, withKey(key));
  assert.equal(replay.status, 200);
  assert.equal(replay.json.replayed, true);
  assert.equal(scalar(`SELECT count(*) FROM shipment_tracking_corrections WHERE organization_id='${organizationId}' AND shipment_id='${fixture.shipment}'`), "1");
  assert.equal(scalar(`SELECT count(*) FROM m10_correction_proof_nonces WHERE organization_id='${organizationId}'`), "1");
  assert.equal(scalar(`SELECT count(*) FROM audit_entries WHERE organization_id='${organizationId}' AND event_type='shipment.tracking_correction_created' AND outcome='Succeeded'`), "1");
  assert.equal(scalar(`SELECT count(*) FROM audit_entries WHERE organization_id='${organizationId}' AND event_type='shipment.tracking_correction_created' AND metadata::text ~* '(reason|proof|signature|nonce)'`), "0");
  return "Analyst denied; Manager correction applied once, replayed safely, nonce and audit persisted, no secret in audit metadata";
});

await check("service-logs-contain-no-key", async () => {
  const logs = compose(["logs", "--no-color", "--no-log-prefix"], { timeout: 120_000 });
  assert.equal(logs.status, 0);
  assert.equal((logs.stdout + logs.stderr).includes(proofKey), false, "logs must not contain the signing key");
});

writeFileSync(join(outDir, "check-default-compose-correction.json"), JSON.stringify({ project, results }, null, 2));
const failed = results.filter(r => r.status !== "PASS");
console.log(`${results.length - failed.length}/${results.length} checks passed`);
process.exit(failed.length ? 1 : 0);
