#!/usr/bin/env node
// G9 isolated-stack end-to-end check against the disposable ho-verify-g9 Compose project only.
// It provisions synthetic accounts, drives the real API, the real restricted-role worker and the real
// database, then records per-check results. It never touches any other Compose project or volume.
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash, randomBytes, randomUUID } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const project = "ho-verify-g9";
const composeFile = "compose.g9.verify.yaml";
const outDir = process.argv[2];
assert.match(outDir || "", /^(?:[Ee]:[\\/]|\/e\/)/, "An E: evidence directory is required");
mkdirSync(outDir, { recursive: true });
const workerPassword = process.env.HARBOROPS_WORKER_DB_PASSWORD;
assert.ok(workerPassword && process.env.HARBOROPS_DB_PASSWORD, "Disposable database passwords are required");
const base = "http://127.0.0.1:8089";
const origin = "http://127.0.0.1:8089";
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
    record(id, false, String(error?.message ?? error).split("\n")[0].slice(0, 300));
  }
}
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

function compose(args, { input, env = {}, timeout = 180_000 } = {}) {
  return spawnSync("docker", ["compose", "-f", composeFile, "-p", project, ...args],
    { encoding: "utf8", input, timeout, env: { ...process.env, ...env }, maxBuffer: 32 * 1024 * 1024 });
}
function psql(sql, { user = "harborops", password } = {}) {
  const args = ["exec", "-T", ...(password ? ["-e", "PGPASSWORD=" + password] : []), "postgres", "psql",
    ...(password ? ["-h", "postgres"] : []), "-U", user, "-d", "harborops", "-At", "-c", sql];
  const run = compose(args, { timeout: 60_000 });
  return { code: run.status, out: (run.stdout || "").trim(), err: (run.stderr || "").trim() };
}
const scalar = sql => {
  const result = psql(sql);
  assert.equal(result.code, 0, "psql failed: " + result.err.slice(0, 200));
  return result.out;
};
async function waitFor(label, fn, timeoutMs = 90_000, stepMs = 1000) {
  const deadline = Date.now() + timeoutMs;
  let last;
  while (Date.now() < deadline) {
    last = await fn();
    if (last) return last;
    await sleep(stepMs);
  }
  throw new Error("Timed out waiting for " + label);
}

class Session {
  jar = new Map();
  async send(method, path, body, { csrf = true, headers = {} } = {}) {
    const requestHeaders = { Origin: origin, ...headers };
    if (csrf) requestHeaders["X-HarborOps-CSRF"] = await this.csrfToken();
    if (this.jar.size) requestHeaders.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
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
  capture(response) {
    for (const cookie of response.headers.getSetCookie?.() ?? []) {
      const [pair] = cookie.split(";");
      const index = pair.indexOf("=");
      this.jar.set(pair.slice(0, index), pair.slice(index + 1));
    }
  }
  async csrfToken() {
    const requestHeaders = { Origin: origin };
    if (this.jar.size) requestHeaders.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
    const response = await fetch(base + "/api/v1/auth/csrf", { headers: requestHeaders, signal: AbortSignal.timeout(15_000) });
    this.capture(response);
    return (await response.json()).token;
  }
}

const nonce = randomBytes(4).toString("hex");
const accountPassword = randomBytes(24).toString("hex");
const emails = Object.fromEntries(["admin", "manager", "analyst", "auditor"]
  .map(role => [role, `g9-${role}-${nonce}@example.test`]));
const slug = "g9-stack-" + nonce;
let organizationId = null;
const sessions = {};

await check("stack-services-ready", async () => {
  const ready = await fetch(base + "/health/ready", { signal: AbortSignal.timeout(15_000) });
  assert.equal(ready.status, 200);
  const worker = await waitFor("worker healthy", () => {
    const state = compose(["ps", "worker", "--format", "{{.State}} {{.Health}}"]).stdout.trim();
    return state === "running healthy" ? state : null;
  }, 90_000, 3000);
  assert.equal(worker, "running healthy");
  return "api ready, worker healthy";
});

await check("provision-accounts-and-organization", async () => {
  for (const [role, email] of Object.entries(emails)) {
    const run = compose(["run", "--rm", "-T", "-e", "HARBOROPS_ALLOW_ACCOUNT_CLI=true", "api",
      "--account-cli", "provision", email, "Synthetic G9 " + role], { input: accountPassword + "\n" });
    assert.equal(run.status, 0, "account provisioning failed for " + role);
  }
  const org = compose(["run", "--rm", "-T", "-e", "HARBOROPS_ALLOW_ORGANIZATION_CLI=true", "api",
    "--organization-cli", "bootstrap", slug, "G9 Stack Organization", emails.admin]);
  assert.equal(org.status, 0, "organization bootstrap failed");
  organizationId = scalar(`SELECT id FROM organizations WHERE slug='${slug}'`);
  assert.match(organizationId, /^[0-9a-f-]{36}$/);
});

const login = async (role, email) => {
  const session = new Session();
  const result = await session.send("POST", "/api/v1/auth/login", { email, password: accountPassword });
  assert.equal(result.status, 200, role + " login " + result.status + " " + result.text.slice(0, 160));
  const selected = await session.send("PUT", "/api/v1/auth/session/organization", { organizationId });
  assert.ok([200, 409].includes(selected.status), role + " select organization " + selected.status);
  sessions[role] = session;
};

await check("memberships-and-sessions", async () => {
  await login("admin", emails.admin);
  for (const role of ["manager", "analyst", "auditor"]) {
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
  const a = await create(sessions.manager, "/ports", { referenceCode: "G9-PA-" + tag, name: "G9 Origin", countryCode: "US" });
  const b = await create(sessions.manager, "/ports", { referenceCode: "G9-PB-" + tag, name: "G9 Destination", countryCode: "US" });
  const container = await create(sessions.manager, "/containers", {
    referenceCode: "G9-BOX-" + tag, identifierKind: "Synthetic", containerIdentifier: "SYN-BOX-G9" + tag, equipmentType: "Dry20" });
  const shipment = await create(sessions.analyst, "/shipments", { referenceCode: "G9-S1-" + tag, originPortId: a, destinationPortId: b });
  const assignment = await sessions.analyst.send("POST", `${root()}/shipments/${shipment}/container-assignments`,
    { containerId: container, startsAtUtc: "2026-11-03T09:00:00Z" });
  assert.equal(assignment.status, 201, "assignment " + assignment.status);
  fixture = { container, shipment, identifier: "SYN-BOX-G9" + tag };
});

let bearer = null;
await check("provision-source", async () => {
  const sourceId = randomUUID(), credentialId = randomUUID(), secret = randomBytes(32);
  const digest = createHash("sha256").update(secret).digest("hex");
  bearer = "hop_v1_" + credentialId.replaceAll("-", "") + "_" + secret.toString("base64url");
  const sql = `BEGIN;
    INSERT INTO integration_sources (id,organization_id,source_code,display_name,status,capability,version,created_at_utc,updated_at_utc)
    VALUES ('${sourceId}','${organizationId}','g9_stack','Synthetic G9 Source','Active','tracking_event_validate',1,now(),now());
    INSERT INTO integration_credentials (id,organization_id,source_id,secret_digest,key_version,not_before_utc,expires_at_utc,created_at_utc)
    VALUES ('${credentialId}','${organizationId}','${sourceId}',decode('${digest}','hex'),'v1',now(),now()+interval '90 days',now());
    COMMIT;`;
  assert.match(scalar(sql), /COMMIT/);
});

async function ingest(key, equipment, occurred = "2026-11-03T10:00:00Z") {
  const response = await fetch(base + "/integrations/v1/tracking-events", {
    method: "POST", signal: AbortSignal.timeout(30_000),
    headers: { "Content-Type": "application/json; charset=utf-8", Authorization: "Bearer " + bearer,
      "X-HarborOps-Tracking-Profile": "tnt-2.2-harborops-v1" },
    body: JSON.stringify({ sourceEventId: key, eventFamily: "Equipment", eventCode: "GTIN", classifier: "ACT",
      occurredAtUtc: occurred, equipmentReference: equipment, externalSchemaVersion: "2.2.0" })
  });
  const json = await response.json().catch(() => null);
  return { status: response.status, receipt: json?.receiptId ?? null, duplicate: json?.duplicate ?? null };
}
const stateOf = receipt => scalar(`SELECT state FROM tracking_inbox_work_items WHERE receipt_id='${receipt}'`);
const settle = (receipt, states) => waitFor("work " + receipt, () => {
  const state = stateOf(receipt);
  return states.includes(state) ? state : null;
}, 90_000, 1000);

let resolved = null, unknown = null;
await check("e2e-worker-processes-resolved-and-unresolved-events", async () => {
  resolved = await ingest("g9-stack-resolved-" + nonce, fixture.identifier);
  unknown = await ingest("g9-stack-unknown-" + nonce, "SYN-BOX-NOSUCH1");
  assert.equal(resolved.status, 202);
  assert.equal(unknown.status, 202);
  assert.equal(await settle(resolved.receipt, ["Processed"]), "Processed");
  assert.equal(await settle(unknown.receipt, ["NeedsReconciliation"]), "NeedsReconciliation");
  assert.equal(scalar(`SELECT count(*) FROM tracking_processing_effects WHERE receipt_id='${resolved.receipt}'`), "1");
  assert.equal(scalar(`SELECT count(*) FROM tracking_processing_effects WHERE receipt_id='${unknown.receipt}'`), "0");
  const kinds = scalar(`SELECT string_agg(event_kind,',' ORDER BY occurred_at_utc) FROM tracking_work_attempt_events WHERE receipt_id='${resolved.receipt}'`);
  assert.equal(kinds, "Claimed,Processed");
  const replay = await ingest("g9-stack-resolved-" + nonce, fixture.identifier);
  assert.equal(replay.status, 202);
  assert.equal(replay.duplicate, true);
  await sleep(4000);
  assert.equal(scalar(`SELECT count(*) FROM tracking_processing_effects WHERE receipt_id='${resolved.receipt}'`), "1");
  return "restricted worker role resolved one event and parked one";
});

await check("operator-api-roles-and-redrive-ceiling", async () => {
  const path = `${root()}/tracking-work`;
  const unauthenticated = await new Session().send("GET", path, undefined, { csrf: false });
  assert.equal(unauthenticated.status, 401);
  const asSource = await fetch(base + path, { headers: { Authorization: "Bearer " + bearer }, signal: AbortSignal.timeout(15_000) });
  assert.ok([401, 403].includes(asSource.status), "integration principal must not use operator routes");
  const admin = await sessions.admin.send("GET", path, undefined, { csrf: false });
  assert.equal(admin.status, 403, "Administrator membership must not grant maritime queue access");
  for (const role of ["manager", "analyst", "auditor"]) {
    const read = await sessions[role].send("GET", `${path}/${unknown.receipt}`, undefined, { csrf: false });
    assert.equal(read.status, 200, role + " read");
    assert.equal(JSON.stringify(read.json).includes("rawPayload"), false);
  }
  for (const role of ["analyst", "auditor", "admin"]) {
    const denied = await sessions[role].send("POST", `${path}/${unknown.receipt}/redrive`, { reason: "operator investigation" });
    assert.equal(denied.status, 403, role + " redrive");
  }
  const foreign = await sessions.manager.send("GET", `/api/v1/organizations/${randomUUID()}/tracking-work/${unknown.receipt}`, undefined, { csrf: false });
  assert.notEqual(foreign.status, 200);
  const processed = await sessions.manager.send("POST", `${path}/${resolved.receipt}/redrive`, { reason: "attempt to redrive done" });
  assert.equal(processed.status, 409, "Processed work must never be redriven");
  for (let generation = 1; generation <= 3; generation += 1) {
    const accepted = await sessions.manager.send("POST", `${path}/${unknown.receipt}/redrive`, { reason: "reconciliation retry " + generation });
    assert.equal(accepted.status, 200, "redrive " + generation + " got " + accepted.status);
    assert.equal(await settle(unknown.receipt, ["NeedsReconciliation"]), "NeedsReconciliation");
  }
  const fourth = await sessions.manager.send("POST", `${path}/${unknown.receipt}/redrive`, { reason: "reconciliation retry 4" });
  assert.equal(fourth.status, 409);
  assert.equal(fourth.json.code, "redrive_limit");
  assert.equal(scalar(`SELECT count(*) FROM tracking_work_attempt_events WHERE receipt_id='${unknown.receipt}' AND event_kind='Redriven'`), "3");
  assert.equal(scalar(`SELECT count(*) FROM tracking_inbox_receipts WHERE id='${unknown.receipt}'`), "1");
});

await check("worker-role-least-privilege", async () => {
  const run = sql => psql(sql, { user: "harborops_worker", password: workerPassword });
  const denied = sqlText => {
    const result = run(sqlText);
    assert.notEqual(result.code, 0, "must fail: " + sqlText.slice(0, 50));
    assert.match(result.err, /permission denied|Worker role|violates|Invalid|ERROR/i);
  };
  assert.equal(run("SELECT count(*) FROM tracking_inbox_work_items").code, 0);
  for (const table of ["users", "user_credentials", "auth_sessions", "integration_credentials", "tracking_receipt_conflicts"]) {
    const result = run(`SELECT count(*) FROM ${table}`);
    assert.notEqual(result.code, 0, table + " must not be readable by the worker role");
    assert.match(result.err, /permission denied/);
  }
  denied(`INSERT INTO tracking_work_attempt_events (id,receipt_id,organization_id,source_id,generation,attempt_count,lease_epoch,event_kind) SELECT gen_random_uuid(),receipt_id,organization_id,source_id,0,0,0,'Claimed' FROM tracking_inbox_work_items LIMIT 1`);
  denied("UPDATE tracking_work_attempt_events SET event_kind='Processed'");
  denied("DELETE FROM tracking_inbox_work_items");
  denied("DELETE FROM tracking_processing_effects");
  denied("UPDATE tracking_inbox_work_items SET organization_id=organization_id");
  denied("UPDATE tracking_inbox_receipts SET correlation_id='x'");
  denied("UPDATE tracking_accepted_events SET classifier='x'");
  // Redrive bypass through the worker identity fails in the database guard, not only in the API.
  const bypass = run(`UPDATE tracking_inbox_work_items SET state='Pending',attempt_count=0,redrive_generation=redrive_generation+1,
    lease_epoch=lease_epoch+1,last_error_code=NULL,resolution_kind=NULL,completed_at_utc=NULL,updated_at_utc=clock_timestamp()
    WHERE receipt_id='${unknown.receipt}'`);
  assert.notEqual(bypass.code, 0);
  assert.equal(stateOf(unknown.receipt), "NeedsReconciliation");
  return "denied reads of identity and credential tables; forged attempts, deletes and redrive bypass rejected";
});

const probe = () => compose(["exec", "-T", "worker", "dotnet", "HarborOps.Worker.dll", "--readiness"], { timeout: 60_000 }).status;
await check("readiness-detects-missing-grant-and-recovers", async () => {
  assert.equal(probe(), 0);
  scalar("REVOKE UPDATE (state) ON public.tracking_inbox_work_items FROM harborops_worker");
  try {
    assert.equal(probe(), 1, "readiness must fail when an operational grant is missing");
  } finally {
    scalar("GRANT UPDATE (state) ON public.tracking_inbox_work_items TO harborops_worker");
  }
  assert.equal(probe(), 0);
  scalar("REVOKE INSERT ON public.tracking_processing_effects FROM harborops_worker");
  try { assert.equal(probe(), 1); } finally { scalar("GRANT INSERT ON public.tracking_processing_effects TO harborops_worker"); }
  assert.equal(probe(), 0);
});

await check("database-outage-and-recovery", async () => {
  assert.equal(compose(["stop", "postgres"]).status, 0);
  try {
    assert.equal(probe(), 1, "readiness must fail without the database");
    const state = compose(["ps", "worker", "--format", "{{.State}}"]).stdout.trim();
    assert.equal(state, "running", "the worker must survive a database outage");
    const during = await ingest("g9-stack-outage-" + nonce, fixture.identifier).catch(() => ({ status: 0 }));
    assert.notEqual(during.status, 202, "intake must not acknowledge while the database is down");
  } finally {
    assert.equal(compose(["start", "postgres"]).status, 0);
  }
  await waitFor("postgres healthy", () => compose(["ps", "postgres", "--format", "{{.Health}}"]).stdout.trim() === "healthy", 90_000, 2000);
  await waitFor("worker ready", () => probe() === 0, 90_000, 3000);
  const after = await waitFor("api intake restored", async () => {
    const r = await ingest("g9-stack-after-outage-" + nonce, fixture.identifier).catch(() => ({ status: 0 }));
    return r.status === 202 ? r : null;
  }, 90_000, 3000);
  assert.equal(await settle(after.receipt, ["Processed"]), "Processed");
});

await check("stopped-worker-leaves-pending-then-processes-once", async () => {
  assert.equal(compose(["stop", "worker"]).status, 0);
  let receipt;
  try {
    const accepted = await ingest("g9-stack-stopped-" + nonce, fixture.identifier);
    assert.equal(accepted.status, 202);
    receipt = accepted.receipt;
    await sleep(6000);
    assert.equal(stateOf(receipt), "Pending", "accepted work stays durable while no worker runs");
  } finally {
    assert.equal(compose(["start", "worker"]).status, 0);
  }
  assert.equal(await settle(receipt, ["Processed"]), "Processed");
  assert.equal(scalar(`SELECT count(*) FROM tracking_processing_effects WHERE receipt_id='${receipt}'`), "1");
});

await check("images-carry-source-revision-label", async () => {
  const sha = process.env.HARBOROPS_SOURCE_SHA;
  assert.match(sha || "", /^[0-9a-f]{40}$/);
  for (const service of ["postgres", "api", "web", "worker"]) {
    const id = compose(["images", "-q", service]).stdout.trim().split(/\s+/)[0];
    assert.ok(id, service + " image");
    const label = spawnSync("docker", ["image", "inspect", "--format", '{{index .Config.Labels "org.opencontainers.image.revision"}}', id],
      { encoding: "utf8" }).stdout.trim();
    assert.equal(label, sha, service + " revision label");
  }
});

await check("service-logs-contain-no-secrets", async () => {
  const logs = compose(["logs", "--no-color", "--no-log-prefix"], { timeout: 120_000 }).stdout;
  assert.ok(logs.length > 0);
  for (const secret of [accountPassword, workerPassword, process.env.HARBOROPS_DB_PASSWORD, bearer, bearer.split("_").pop()]) {
    assert.equal(logs.includes(secret), false, "a secret value appeared in service logs");
  }
  assert.doesNotMatch(logs, /hop_v1_[0-9a-f]{32}_/);
  assert.doesNotMatch(logs, /Password=/i);
  return `${logs.split("\n").length} log lines scanned`;
});

const failed = results.filter(x => x.status !== "PASS");
writeFileSync(join(outDir, "g9-stack-results.json"), JSON.stringify({ project, results }, null, 2));
console.log(`G9 stack checks: ${results.length - failed.length} passed, ${failed.length} failed`);
process.exit(failed.length === 0 ? 0 : 1);
