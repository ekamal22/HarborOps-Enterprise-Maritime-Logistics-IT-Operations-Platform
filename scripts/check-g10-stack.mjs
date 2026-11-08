#!/usr/bin/env node
// M10/G16 isolated-stack end-to-end check against a disposable ho-verify-cg16* Compose project only.
// Derived from the accepted scripts/check-combined-stack.mjs (unchanged) with the twelve-migration count and the
// M10 checks added: real restricted worker feeding the timeline index, scoped read routes, Manager-only rebuild
// and correction under the restricted API role, the least-privilege projector identity, parked transport events
// and recovery of the read routes after a database outage. COMPOSE_FILE must layer the combined Compose files.
// It provisions synthetic accounts, drives the real API, the real restricted-role worker and the real
// database, then records per-check results. It never touches any other Compose project or volume.
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash, randomBytes, randomUUID } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const project = process.env.HARBOROPS_CG16_PROJECT || "ho-verify-cg16";
assert.match(project, /^ho-verify-cg16(-[a-z0-9]{1,12})?$/, "Invalid combined Compose project name");
const outDir = process.argv[2];
assert.match(outDir || "", /^(?:[Ee]:[\\/]|\/e\/)/, "An E: evidence directory is required");
mkdirSync(outDir, { recursive: true });
const workerPassword = process.env.HARBOROPS_WORKER_DB_PASSWORD;
const apiPassword = process.env.HARBOROPS_API_DB_PASSWORD;
assert.ok(workerPassword && apiPassword && process.env.HARBOROPS_DB_PASSWORD, "Disposable database passwords are required");
const base = "http://127.0.0.1:8086";
const origin = "http://127.0.0.1:8086";
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
    record(id, false, String(error?.message ?? error).replace(/\s+/g, " ").slice(0, 400));
  }
}
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

function compose(args, { input, env = {}, timeout = 180_000 } = {}) {
  return spawnSync("docker", ["compose", "-p", project, "--profile", "worker", ...args],
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
  .map(role => [role, `cg16-${role}-${nonce}@example.test`]));
const slug = "cg16-stack-" + nonce;
let organizationId = null;
const sessions = {};

await check("stack-services-ready", async () => {
  const ready = await fetch(base + "/health/ready", { signal: AbortSignal.timeout(15_000) });
  assert.equal(ready.status, 200, "api /health/ready returned " + ready.status);
  const worker = await waitFor("worker healthy", () => {
    const state = compose(["ps", "worker", "--format", "{{.State}} {{.Health}}"]).stdout.trim();
    return state === "running healthy" ? state : null;
  }, 90_000, 3000);
  assert.equal(worker, "running healthy", "worker state was " + worker);
  return "api ready, worker healthy";
});

await check("fresh-volume-migrations-applied", async () => {
  const row = scalar(`SELECT count(*) || '|' || bool_or("MigrationId" LIKE '%_TrackingWorker') FROM "__EFMigrationsHistory"`);
  const hasM11 = row === "14|true" || row === "15|true";
  const hasM12 = row === "15|true";
  assert.ok(row === "13|true" || hasM11,
    "expected the accepted 13-migration baseline, standalone M11 14 or integrated M11/M12 15; got " + row);
  const latest = hasM12 ? 6 : (hasM11 ? 5 : 4);
  const order = scalar(`SELECT string_agg(split_part("MigrationId",'_',2),',' ORDER BY "MigrationId") FROM (
    SELECT "MigrationId" FROM "__EFMigrationsHistory"
    ORDER BY "MigrationId" DESC LIMIT ${latest}) t`);
  const expected = "AuditEntries,TrackingWorker,ShipmentTrackingProjections,M10CorrectionAuthority" +
    (hasM11 ? ",ShipmentExceptions" : "") +
    (hasM12 ? ",M12ShipmentExportAudit" : "");
  assert.equal(order, expected,
    "Accepted migration order must be preserved through M11/M12 additions");
  return (hasM12 ? "fifteen" : (hasM11 ? "fourteen" : "thirteen")) +
    " migrations applied in order";
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
  resolved = await ingest("cg16-stack-resolved-" + nonce, fixture.identifier);
  unknown = await ingest("cg16-stack-unknown-" + nonce, "SYN-BOX-NOSUCH1");
  assert.equal(resolved.status, 202);
  assert.equal(unknown.status, 202);
  assert.equal(await settle(resolved.receipt, ["Processed"]), "Processed");
  assert.equal(await settle(unknown.receipt, ["NeedsReconciliation"]), "NeedsReconciliation");
  assert.equal(scalar(`SELECT count(*) FROM tracking_processing_effects WHERE receipt_id='${resolved.receipt}'`), "1");
  assert.equal(scalar(`SELECT count(*) FROM tracking_processing_effects WHERE receipt_id='${unknown.receipt}'`), "0");
  const kinds = scalar(`SELECT string_agg(event_kind,',' ORDER BY occurred_at_utc) FROM tracking_work_attempt_events WHERE receipt_id='${resolved.receipt}'`);
  assert.equal(kinds, "Claimed,Processed");
  const replay = await ingest("cg16-stack-resolved-" + nonce, fixture.identifier);
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
    const during = await ingest("cg16-stack-outage-" + nonce, fixture.identifier).catch(() => ({ status: 0 }));
    assert.notEqual(during.status, 202, "intake must not acknowledge while the database is down");
  } finally {
    assert.equal(compose(["start", "postgres"]).status, 0);
  }
  await waitFor("postgres healthy", () => compose(["ps", "postgres", "--format", "{{.Health}}"]).stdout.trim() === "healthy", 90_000, 2000);
  await waitFor("worker ready", () => probe() === 0, 90_000, 3000);
  const after = await waitFor("api intake restored", async () => {
    const r = await ingest("cg16-stack-after-outage-" + nonce, fixture.identifier).catch(() => ({ status: 0 }));
    return r.status === 202 ? r : null;
  }, 90_000, 3000);
  assert.equal(await settle(after.receipt, ["Processed"]), "Processed");
});

await check("stopped-worker-leaves-pending-then-processes-once", async () => {
  assert.equal(compose(["stop", "worker"]).status, 0);
  let receipt;
  try {
    const accepted = await ingest("cg16-stack-stopped-" + nonce, fixture.identifier);
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

// ---- Combined M9/M16 checks (not part of the accepted M9 stack check) ----------------------------
const asApi = sqlText => psql(sqlText, { user: "harborops_api", password: apiPassword });
const asWorker = sqlText => psql(sqlText, { user: "harborops_worker", password: workerPassword });
const grants = role => scalar(`SELECT COALESCE(string_agg(table_name||':'||privilege_type, ',' ORDER BY table_name, privilege_type),'')
  FROM information_schema.role_table_grants WHERE grantee='${role}' AND table_schema='public'`);
const denies = (run, sqlText, pattern) => {
  const result = run(sqlText);
  assert.notEqual(result.code, 0, "must fail: " + sqlText.slice(0, 70));
  assert.match(result.err, pattern, "unexpected failure for " + sqlText.slice(0, 70) + ": " + result.err.slice(0, 160));
};

await check("combined-exact-runtime-grants-and-role-flags", async () => {
  assert.equal(grants("harborops_worker"), ["containers:SELECT", "organizations:SELECT", "shipment_container_assignments:SELECT",
    "shipments:SELECT", "tracking_accepted_events:SELECT", "tracking_inbox_receipts:SELECT", "tracking_inbox_work_items:SELECT",
    "tracking_processing_effects:INSERT", "tracking_processing_effects:SELECT", "tracking_work_attempt_events:SELECT"].join(","),
    "worker table grants must equal the accepted M9 set");
  const api = grants("harborops_api").split(",");
  for (const forbidden of ["audit_entries:INSERT", "audit_entries:UPDATE", "audit_entries:DELETE", "audit_entries:TRUNCATE",
    "tracking_processing_effects:INSERT", "tracking_processing_effects:UPDATE", "tracking_work_attempt_events:UPDATE",
    "tracking_work_attempt_events:DELETE", "tracking_inbox_receipts:UPDATE", "tracking_accepted_events:UPDATE",
    "users:DELETE", "auth_sessions:DELETE", "organizations:DELETE"])
    assert.ok(!api.includes(forbidden), "API role must not hold " + forbidden);
  for (const required of ["audit_entries:SELECT", "tracking_inbox_work_items:UPDATE", "tracking_work_attempt_events:INSERT"])
    assert.ok(api.includes(required), "API role must hold " + required);
  assert.equal(scalar(`SELECT count(*) FROM pg_roles WHERE rolname IN ('harborops_api','harborops_worker')
    AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)`), "0");
  assert.equal(scalar(`SELECT count(*) FROM pg_class c JOIN pg_roles r ON r.oid=c.relowner WHERE r.rolname IN ('harborops_api','harborops_worker')`), "0");
  assert.equal(scalar("SELECT count(*) FROM pg_trigger WHERE tgrelid='public.audit_entries'::regclass AND NOT tgisinternal AND tgenabled='O'"), "2");
  return "worker grants equal the accepted M9 set; API contract holds; no privileged flags or owned objects";
});

await check("api-and-worker-real-login-ddl-and-audit-denials", async () => {
  for (const run of [asApi, asWorker]) {
    denies(run, "UPDATE audit_entries SET outcome='Denied'", /permission denied/);
    denies(run, "DELETE FROM audit_entries", /permission denied/);
    denies(run, "TRUNCATE audit_entries", /permission denied/);
    denies(run, "INSERT INTO audit_entries DEFAULT VALUES", /permission denied/);
    denies(run, "ALTER TABLE audit_entries DISABLE TRIGGER ALL", /must be owner|permission denied/);
    denies(run, "DROP TRIGGER m16_audit_no_change ON audit_entries", /must be owner|permission denied/);
    denies(run, "CREATE TABLE cg16_unauthorized (id int)", /permission denied/);
    denies(run, "SET session_replication_role = replica", /permission denied/);
    denies(run, "INSERT INTO tracking_work_attempt_events (id) VALUES (gen_random_uuid())", run === asApi ? /may only record an attributed manual redrive/ : /permission denied/);
  }
  denies(asWorker, "SELECT count(*) FROM audit_entries", /permission denied/);
  denies(asWorker, "SELECT count(*) FROM integration_credentials", /permission denied/);
  denies(asWorker, "SELECT count(*) FROM integration_sources", /permission denied/);
  denies(asApi, "INSERT INTO tracking_processing_effects DEFAULT VALUES", /permission denied/);
  denies(asApi, "DELETE FROM tracking_inbox_work_items", /permission denied/);
  denies(asApi, "UPDATE tracking_work_attempt_events SET reason_code='x'", /permission denied/);
  assert.equal(asApi("SELECT count(*) FROM audit_entries").code, 0, "API role must read audit entries");
  return "DDL, trigger-disable, audit mutation and cross-privilege writes denied for both real identities";
});

await check("api-role-is-confined-to-manual-redrive", async () => {
  // Keep one item Pending: stop the worker so nothing claims it, then try the forbidden transitions
  // with the real restricted API login. The session_user based guards cannot be tested through SET ROLE.
  assert.equal(compose(["stop", "worker"]).status, 0);
  let receipt;
  try {
    const accepted = await ingest("cg16-stack-scope-" + nonce, fixture.identifier);
    assert.equal(accepted.status, 202);
    receipt = accepted.receipt;
    assert.equal(stateOf(receipt), "Pending");
    const before = scalar(`SELECT count(*) FROM tracking_work_attempt_events WHERE receipt_id='${receipt}'`);
    denies(asApi, `UPDATE tracking_inbox_work_items SET state='Leased',attempt_count=1,lease_epoch=lease_epoch+1,
      lease_token=gen_random_uuid(),lease_expires_at_utc=clock_timestamp()+interval '5 minutes',updated_at_utc=clock_timestamp()
      WHERE receipt_id='${receipt}'`, /limited to manual redrive/);
    for (const kind of ["Processed", "Claimed", "LeaseBudgetExhausted"])
      denies(asApi, `INSERT INTO tracking_work_attempt_events (id,receipt_id,organization_id,source_id,generation,attempt_count,lease_epoch,event_kind,occurred_at_utc)
        SELECT gen_random_uuid(),receipt_id,organization_id,source_id,0,1,1,'${kind}',clock_timestamp()
        FROM tracking_inbox_work_items WHERE receipt_id='${receipt}'`, /may only record an attributed manual redrive/);
    assert.equal(stateOf(receipt), "Pending");
    assert.equal(scalar(`SELECT count(*) FROM tracking_work_attempt_events WHERE receipt_id='${receipt}'`), before);
  } finally {
    assert.equal(compose(["start", "worker"]).status, 0);
  }
  assert.equal(await settle(receipt, ["Processed"]), "Processed");
  assert.equal(scalar(`SELECT count(*) FROM tracking_processing_effects WHERE receipt_id='${receipt}'`), "1");
  return "forged lease claim and forged worker attempt history rejected; worker then processed the item once";
});

await check("audit-and-tracking-work-coexist-with-role-boundaries", async () => {
  const audit = `${root()}/audit-events`;
  const list = await sessions.auditor.send("GET", audit + "?limit=100", undefined, { csrf: false });
  assert.equal(list.status, 200, "auditor audit list " + list.status);
  const text = JSON.stringify(list.json);
  assert.ok(text.includes("integration.tracking_accepted") && text.includes("IntegrationSource"),
    "accepted tracking events must be audited as the integration source");
  assert.ok(!text.includes(accountPassword) && !text.includes(bearer), "audit payload must not contain secrets");
  for (const role of ["admin", "manager", "analyst"]) {
    const denied = await sessions[role].send("GET", audit, undefined, { csrf: false });
    assert.equal(denied.status, 403, role + " must not read audit entries, got " + denied.status);
  }
  // The same Auditor reads tracking work (M9 policy) but cannot redrive it.
  const read = await sessions.auditor.send("GET", `${root()}/tracking-work/${unknown.receipt}`, undefined, { csrf: false });
  assert.equal(read.status, 200);
  const redrive = await sessions.auditor.send("POST", `${root()}/tracking-work/${unknown.receipt}/redrive`, { reason: "auditor must not redrive" });
  assert.equal(redrive.status, 403);
  // The migration-time actor attribution rows are scoped to this organization and carry no raw payload.
  assert.equal(Number(scalar(`SELECT count(*) FROM audit_entries WHERE organization_id='${organizationId}' AND event_type='integration.tracking_accepted'
    AND actor_kind='IntegrationSource' AND actor_user_id IS NULL AND actor_session_id IS NULL AND actor_role IS NULL`)) > 0, true);
  assert.equal(scalar(`SELECT count(*) FROM audit_entries WHERE metadata::text ~* '(sourceEventId|equipmentReference|SYN-BOX)'`), "0");
  return "Auditor-only audit read, M9 work read by Auditor, redrive denied, source attribution intact";
});

await check("manager-redrive-writes-immutable-attributed-attempt-under-restricted-api", async () => {
  const receipt = await ingest("cg16-stack-redrive-" + nonce, "SYN-BOX-NOSUCH2");
  assert.equal(receipt.status, 202);
  assert.equal(await settle(receipt.receipt, ["NeedsReconciliation"]), "NeedsReconciliation");
  const managerId = scalar(`SELECT id FROM users WHERE email='${emails.manager}'`);
  const before = scalar(`SELECT count(*) FROM tracking_work_attempt_events WHERE receipt_id='${receipt.receipt}'`);
  const deniedRedrive = await sessions.analyst.send("POST", `${root()}/tracking-work/${receipt.receipt}/redrive`, { reason: "analyst must not redrive" });
  assert.equal(deniedRedrive.status, 403);
  assert.equal(scalar(`SELECT count(*) FROM tracking_work_attempt_events WHERE receipt_id='${receipt.receipt}'`), before, "denied redrive wrote history");
  const ok = await sessions.manager.send("POST", `${root()}/tracking-work/${receipt.receipt}/redrive`, { reason: "operator reviewed reference data" });
  assert.equal(ok.status, 200, "manager redrive " + ok.status);
  assert.equal(scalar(`SELECT count(*) FROM tracking_work_attempt_events WHERE receipt_id='${receipt.receipt}' AND event_kind='Redriven' AND actor_user_id='${managerId}'`), "1");
  assert.equal(await settle(receipt.receipt, ["NeedsReconciliation"]), "NeedsReconciliation");
  // Immutability holds even for the owner role through the database guard.
  const mutate = psql(`UPDATE tracking_work_attempt_events SET reason_code='tamper' WHERE receipt_id='${receipt.receipt}'`);
  assert.notEqual(mutate.code, 0);
  assert.equal(scalar(`SELECT count(*) FROM tracking_work_attempt_events WHERE receipt_id='${receipt.receipt}' AND reason_code='tamper'`), "0");
  return "Manager redrive recorded one attributed Redriven row; Analyst redrive wrote nothing; history immutable";
});

let orgB = null;
// ---- M10 checks (added for G10; the accepted combined stack check has none) ---------------------------
const projectorPassword = randomBytes(24).toString("hex");
const asProjector = sqlText => psql(sqlText, { user: "harborops_projector", password: projectorPassword });
const shipmentRoot = () => `${root()}/shipments/${fixture.shipment}`;
const tableSet = text => new Set(text.split(",").filter(Boolean));

await check("m10-timeline-index-and-revision-from-real-restricted-worker", async () => {
  const effects = scalar(`SELECT count(*) FROM tracking_processing_effects WHERE organization_id='${organizationId}' AND shipment_id='${fixture.shipment}'`);
  assert.ok(Number(effects) >= 4, "expected several resolved effects, saw " + effects);
  assert.equal(scalar(`SELECT count(*) FROM shipment_tracking_timeline_entries WHERE organization_id='${organizationId}' AND shipment_id='${fixture.shipment}'`), effects);
  assert.equal(scalar(`SELECT input_revision||'|'||event_count FROM shipment_tracking_input_versions WHERE organization_id='${organizationId}' AND shipment_id='${fixture.shipment}'`), `${effects}|${effects}`);
  assert.equal(scalar(`SELECT count(*) FROM shipment_tracking_timeline_entries t LEFT JOIN tracking_processing_effects fx ON fx.receipt_id=t.receipt_id WHERE fx.receipt_id IS NULL`), "0");
  // The parked, unresolved event never reaches the derived index.
  assert.equal(scalar(`SELECT count(*) FROM shipment_tracking_timeline_entries WHERE receipt_id='${unknown.receipt}'`), "0");
  // Nothing materializes a projection until an authorized rebuild: the projector is off by default.
  assert.equal(scalar(`SELECT count(*) FROM shipment_tracking_projections WHERE organization_id='${organizationId}'`), "0");
  return `${effects} effects indexed with matching input revision; no automatic projection`;
});

await check("m10-transport-events-stay-parked-and-unindexed", async () => {
  const response = await fetch(base + "/integrations/v1/tracking-events", {
    method: "POST", signal: AbortSignal.timeout(30_000),
    headers: { "Content-Type": "application/json; charset=utf-8", Authorization: "Bearer " + bearer,
      "X-HarborOps-Tracking-Profile": "tnt-2.2-harborops-v1" },
    body: JSON.stringify({ sourceEventId: "g10-transport-" + nonce, eventFamily: "Transport", eventCode: "ARRI", classifier: "ACT",
      occurredAtUtc: "2026-11-03T10:00:00Z", transportReference: "SYN-TRANSPORT-" + nonce.toUpperCase(), externalSchemaVersion: "2.2.0" })
  });
  assert.equal(response.status, 202);
  const receipt = (await response.json()).receiptId;
  assert.equal(await settle(receipt, ["NeedsReconciliation"]), "NeedsReconciliation");
  assert.equal(scalar(`SELECT count(*) FROM tracking_processing_effects WHERE receipt_id='${receipt}'`), "0");
  assert.equal(scalar(`SELECT count(*) FROM shipment_tracking_timeline_entries WHERE receipt_id='${receipt}'`), "0");
  return "opaque transport event parked; M10 neither resolves nor indexes it";
});

await check("m10-read-routes-scoped-and-unauthenticated-denied", async () => {
  for (const path of ["/events", "/tracking-projection"]) {
    const anonymous = await new Session().send("GET", shipmentRoot() + path, undefined, { csrf: false });
    assert.equal(anonymous.status, 401, path + " unauthenticated");
    const asSource = await fetch(base + shipmentRoot() + path, { headers: { Authorization: "Bearer " + bearer }, signal: AbortSignal.timeout(15_000) });
    assert.ok([401, 403].includes(asSource.status), "integration principal must not use operator routes");
    const foreign = await sessions.manager.send("GET", `/api/v1/organizations/${randomUUID()}/shipments/${fixture.shipment}${path}`, undefined, { csrf: false });
    assert.notEqual(foreign.status, 200, path + " foreign organization");
  }
  const page = await sessions.analyst.send("GET", shipmentRoot() + "/events?limit=2", undefined, { csrf: false });
  assert.equal(page.status, 200);
  assert.equal(page.json.items.length, 2);
  assert.ok(page.json.nextCursor, "a bounded page must carry a cursor when more history exists");
  assert.equal(JSON.stringify(page.json).includes("rawPayload"), false);
  const next = await sessions.analyst.send("GET", shipmentRoot() + "/events?limit=2&cursor=" + encodeURIComponent(page.json.nextCursor), undefined, { csrf: false });
  assert.equal(next.status, 200);
  const tampered = await sessions.analyst.send("GET", shipmentRoot() + "/events?limit=2&cursor=" + encodeURIComponent(page.json.nextCursor.slice(0, -4) + "AAAA"), undefined, { csrf: false });
  assert.equal(tampered.status, 409, "a tampered cursor must be refused");
  const projection = await sessions.analyst.send("GET", shipmentRoot() + "/tracking-projection", undefined, { csrf: false });
  assert.equal(projection.status, 200);
  assert.equal(projection.json.materializationStale, true, "an unmaterialized shipment must report stale, not current");
  assert.equal(projection.json.etaUtc, null);
  return "scoped, cursor-protected reads; unmaterialized state reported as stale";
});

await check("m10-manager-rebuild-and-correction-under-restricted-api-role", async () => {
  const withKey = key => ({ headers: { "Idempotency-Key": key } });
  const rebuildPath = shipmentRoot() + "/tracking-projection/rebuild";
  assert.equal((await sessions.analyst.send("POST", rebuildPath, undefined, withKey(randomUUID()))).status, 403, "Analyst must not rebuild");
  assert.equal((await sessions.manager.send("POST", rebuildPath, undefined, {})).status, 400, "rebuild requires an Idempotency-Key");
  const key = randomUUID();
  const rebuilt = await sessions.manager.send("POST", rebuildPath, undefined, withKey(key));
  assert.equal(rebuilt.status, 200, "rebuild " + rebuilt.status + " " + rebuilt.text.slice(0, 120));
  const replay = await sessions.manager.send("POST", rebuildPath, undefined, withKey(key));
  assert.equal(replay.status, 200);
  assert.equal(replay.json.replayed, true);
  const projection = await sessions.analyst.send("GET", shipmentRoot() + "/tracking-projection", undefined, { csrf: false });
  assert.equal(projection.json.materializationStale, false);
  assert.equal(projection.json.observedTrackingState, "EquipmentIn");

  const revisionBefore = Number(scalar(`SELECT input_revision FROM shipment_tracking_input_versions WHERE organization_id='${organizationId}' AND shipment_id='${fixture.shipment}'`));
  const correctionPath = shipmentRoot() + "/tracking-corrections";
  const body = { targetReceiptId: resolved.receipt, replacementReceiptId: null, expectedGeneration: 0, reason: "Confirmed invalid first observation" };
  assert.equal((await sessions.analyst.send("POST", correctionPath, body, withKey(randomUUID()))).status, 403, "Analyst must not correct");
  const correctionKey = randomUUID();
  const applied = await sessions.manager.send("POST", correctionPath, body, withKey(correctionKey));
  assert.equal(applied.status, 200, "correction " + applied.status + " " + applied.text.slice(0, 120));
  const again = await sessions.manager.send("POST", correctionPath, body, withKey(correctionKey));
  assert.equal(again.json.replayed, true);
  const stale = await sessions.manager.send("POST", correctionPath, body, withKey(randomUUID()));
  assert.equal(stale.status, 409, "a stale expected generation must conflict");
  const collision = await sessions.manager.send("POST", correctionPath, { ...body, reason: "Different payload reusing the key" }, withKey(correctionKey));
  assert.equal(collision.status, 409, "idempotency key reuse with another payload must conflict");
  assert.equal(scalar(`SELECT count(*) FROM shipment_tracking_corrections WHERE organization_id='${organizationId}' AND shipment_id='${fixture.shipment}'`), "1");
  assert.equal(scalar(`SELECT generation FROM shipment_tracking_correction_heads WHERE organization_id='${organizationId}' AND shipment_id='${fixture.shipment}'`), "1");
  assert.equal(Number(scalar(`SELECT input_revision FROM shipment_tracking_input_versions WHERE organization_id='${organizationId}' AND shipment_id='${fixture.shipment}'`)), revisionBefore + 1);
  assert.equal(scalar(`SELECT count(*) FROM tracking_processing_effects WHERE receipt_id='${resolved.receipt}'`), "1", "the M9 effect is untouched by the correction");
  assert.equal(scalar("SELECT count(*) FROM audit_entries WHERE event_type='shipment.tracking_correction_created'"), "1");

  // The restricted API identity and even the owner session cannot rewrite retained M10 history.
  for (const sqlText of ["UPDATE shipment_tracking_corrections SET reason='Tampered after the fact'", "DELETE FROM shipment_tracking_operation_replays",
    "UPDATE shipment_tracking_correction_heads SET generation=0", "UPDATE shipment_tracking_input_versions SET input_revision=0",
    "INSERT INTO shipment_tracking_timeline_entries DEFAULT VALUES", "DELETE FROM shipment_tracking_timeline_entries", "TRUNCATE shipment_tracking_timeline_entries"])
    denies(asApi, sqlText, /permission denied/);
  denies(sqlText => psql(sqlText), "UPDATE shipment_tracking_corrections SET reason='Owner rewrite of history'", /M10 retained history is immutable/);
  denies(sqlText => psql(sqlText), "DELETE FROM shipment_tracking_operation_replays", /M10 retained history is immutable/);
  denies(sqlText => psql(sqlText), "UPDATE shipment_tracking_timeline_entries SET event_code='GTOT'", /immutable|append-only/i);
  return "Manager-only, idempotent, generation-checked correction and rebuild; history immutable for API and owner sessions";
});

await check("m10-exact-api-worker-grants-keep-m16-and-m9-restrictions", async () => {
  const api = tableSet(grants("harborops_api"));
  for (const need of ["shipment_tracking_operation_replays:INSERT", "shipment_tracking_projections:INSERT",
    "shipment_tracking_projections:UPDATE", "shipment_projection_applications:INSERT"])
    assert.ok(api.has(need), "API must hold " + need);
  for (const table of ["shipment_tracking_timeline_entries", "shipment_tracking_input_versions", "shipment_tracking_correction_heads"])
    for (const privilege of ["INSERT", "UPDATE", "DELETE", "TRUNCATE"])
      assert.ok(!api.has(table + ":" + privilege), `API must not hold ${privilege} on ${table}`);
  for (const table of ["shipment_tracking_corrections", "shipment_tracking_operation_replays", "shipment_projection_applications"])
    for (const privilege of ["UPDATE", "DELETE", "TRUNCATE"])
      assert.ok(!api.has(table + ":" + privilege), `API must not hold ${privilege} on ${table}`);
  for (const forbidden of ["audit_entries:INSERT", "audit_entries:UPDATE", "audit_entries:DELETE",
    "shipment_tracking_corrections:INSERT", "m10_correction_proof_keys:SELECT",
    "m10_correction_proof_nonces:SELECT", "tracking_processing_effects:INSERT",
    "tracking_work_attempt_events:UPDATE"])
    assert.ok(!api.has(forbidden), "API must not hold " + forbidden);
  const worker = grants("harborops_worker");
  assert.equal(/shipment_tracking_|shipment_projection_/.test(worker), false, "the worker holds no M10 table privilege");
  assert.equal(scalar("SELECT count(*) FROM pg_trigger WHERE tgrelid='public.audit_entries'::regclass AND NOT tgisinternal AND tgenabled='O'"), "2");
  assert.equal(scalar("SELECT count(*) FROM audit_entries WHERE event_type='shipment.tracking_correction_created'"), "1",
    "An authorized correction must atomically produce one M16 audit event");
  assert.equal(scalar("SELECT count(*) FROM audit_entries WHERE resource_type IN ('shipment_tracking_timeline_entries','shipment_tracking_input_versions')"), "0",
    "M10 derived evidence must not duplicate audit rows");
  return "API gained only the scoped M10 writes; worker and audit protections unchanged";
});

await check("m10-correction-authority-attacks-under-real-restricted-api-login", async () => {
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
  const managerUser = scalar(`SELECT id FROM users WHERE email='${emails.manager}'`);
  // The restricted login can read live identifiers; they must not be enough to correct.
  const readable = asApi(`SELECT id FROM auth_sessions WHERE user_id='${managerUser}' AND revoked_at_utc IS NULL ORDER BY issued_at_utc DESC LIMIT 1`);
  assert.equal(readable.code, 0, "API must read auth_sessions: " + readable.err.slice(0, 120));
  assert.match(readable.out, uuid, "a live Manager session id is readable by the API login");
  const stolenSession = readable.out;
  const effects = () => scalar(`SELECT (SELECT count(*) FROM shipment_tracking_corrections WHERE organization_id='${organizationId}')||'/'||
    (SELECT count(*) FROM audit_entries WHERE event_type='shipment.tracking_correction_created')||'/'||
    (SELECT count(*) FROM shipment_tracking_operation_replays WHERE organization_id='${organizationId}' AND operation_kind='Correction')||'/'||
    (SELECT count(*) FROM shipment_tracking_input_versions WHERE organization_id='${organizationId}' AND shipment_id='${fixture.shipment}' AND input_revision>0)`);
  const before = effects();
  // R04: direct INSERT is denied even for an otherwise valid actor and generation.
  denies(asApi, `INSERT INTO shipment_tracking_corrections (id,organization_id,shipment_id,target_receipt_id,replacement_receipt_id,generation,actor_user_id,reason,correlation_id,correction_digest)
    VALUES (gen_random_uuid(),'${organizationId}','${fixture.shipment}','${resolved.receipt}',NULL,2,'${managerUser}','Direct insert attempt','direct','${"d".repeat(64)}')`, /permission denied/);
  // R05 and R06: forged proofs built from readable identifiers are refused.
  const epoch = Math.floor(Date.now() / 1000);
  const call = (signature, expires = epoch + 30) => `SELECT m10_apply_signed_correction('${organizationId}','${fixture.shipment}','${resolved.receipt}',NULL,2,'${managerUser}','${stolenSession}',
    'Forged authority attempt','forge','${"e".repeat(64)}',gen_random_uuid(),gen_random_uuid(),${expires},'${signature}')`;
  denies(asApi, call("a".repeat(64)), /Correction authority denied/);
  denies(asApi, call(""), /Correction authority denied/);
  denies(asApi, call("a".repeat(64), epoch - 30), /Correction authority denied/);
  denies(asApi, call("a".repeat(64), epoch + 3600), /Correction authority denied/);
  denies(asWorker, call("a".repeat(64)), /permission denied/);
  // The verifier secret, the nonce store and replay forgery are closed to the API login.
  denies(asApi, "SELECT secret FROM m10_correction_proof_keys", /permission denied/);
  denies(asApi, "SELECT 1 FROM m10_correction_proof_nonces", /permission denied/);
  denies(asApi, `INSERT INTO shipment_tracking_operation_replays (organization_id,shipment_id,actor_user_id,idempotency_key,operation_kind,request_digest,response_revision,response_generation)
    VALUES ('${organizationId}','${fixture.shipment}','${managerUser}',gen_random_uuid(),'Correction','${"e".repeat(64)}',99,99)`, /Correction replay authority denied/);
  assert.equal(effects(), before, "denied attacks must leave no correction, audit, replay or revision effect");

  // R10: only the Auditor of this organization reads the correction event, without sensitive content.
  const audit = `${root()}/audit-events`;
  const list = await sessions.auditor.send("GET", audit + "?limit=100", undefined, { csrf: false });
  assert.equal(list.status, 200, "auditor audit list " + list.status);
  const rows = list.json.entries.filter(entry => entry.eventType === "shipment.tracking_correction_created");
  assert.equal(rows.length, 1, "exactly one correction audit entry is visible");
  const [entry] = rows;
  assert.equal(entry.organizationId, organizationId);
  assert.equal(entry.outcome, "Succeeded");
  assert.equal(entry.actorKind, "Human");
  assert.equal(entry.actorRole, "Manager");
  assert.equal(entry.actorUserId, managerUser);
  assert.equal(entry.resourceType, "shipment_tracking_corrections");
  assert.deepEqual(entry.metadata, { operation: "insert", changedFields: [] });
  const text = JSON.stringify(entry);
  for (const secret of ["Confirmed invalid first observation", stolenSession, "signature", "nonce"])
    assert.ok(!text.includes(secret), "audit entry must not expose " + secret.slice(0, 12));
  for (const role of ["admin", "manager", "analyst"]) {
    const denied = await sessions[role].send("GET", audit, undefined, { csrf: false });
    assert.equal(denied.status, 403, role + " must not read the correction audit entry, got " + denied.status);
  }
  return "direct INSERT, forged and stolen-identifier proofs, expired and far-future proofs, key, nonce and replay access all denied with no effect; Auditor reads one sanitized scoped entry";
});

await check("m10-projector-role-provisioned-and-confined", async () => {
  const script = readFileSync(new URL("./configure-m10-projector-db.sql", import.meta.url), "utf8");
  const apply = compose(["exec", "-T", "postgres", "psql", "-X", "-v", "ON_ERROR_STOP=1", "-U", "harborops", "-d", "harborops"], { input: script });
  assert.equal(apply.status, 0, "projector provisioning failed: " + (apply.stderr || "").slice(0, 200));
  assert.match(scalar(`ALTER ROLE harborops_projector PASSWORD '${projectorPassword}'`), /ALTER ROLE/);
  assert.equal(scalar(`SELECT count(*) FROM pg_roles WHERE rolname='harborops_projector' AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)`), "0");
  assert.equal(scalar(`SELECT count(*) FROM pg_class c JOIN pg_roles r ON r.oid=c.relowner WHERE r.rolname='harborops_projector'`), "0");
  assert.equal(asProjector(`SELECT m10_lock_projector_scope('${organizationId}','${fixture.shipment}')`).out, "t");
  assert.equal(scalar(`SELECT m10_lock_projector_scope('${organizationId}','${fixture.shipment}')`), "f", "only the projector session may use the lock function");
  assert.equal(asProjector(`SELECT m10_lock_projector_scope('${organizationId}','${randomUUID()}')`).out, "f");
  denies(asApi, `SELECT m10_lock_projector_scope('${organizationId}','${fixture.shipment}')`, /permission denied/);
  denies(asWorker, `SELECT m10_lock_projector_scope('${organizationId}','${fixture.shipment}')`, /permission denied/);
  for (const [sqlText, pattern] of [
    ["INSERT INTO tracking_processing_effects DEFAULT VALUES", /permission denied/],
    ["UPDATE tracking_accepted_events SET classifier='x'", /permission denied/],
    ["UPDATE tracking_inbox_receipts SET correlation_id='x'", /permission denied/],
    ["UPDATE shipments SET id=id", /permission denied/],
    ["UPDATE organizations SET id=id", /permission denied/],
    ["DELETE FROM shipment_tracking_projections", /permission denied/],
    ["UPDATE shipment_projection_applications SET policy_version='x'", /permission denied/],
    ["SELECT count(*) FROM audit_entries", /permission denied/],
    ["SELECT count(*) FROM tracking_inbox_work_items", /permission denied/],
    ["SELECT count(*) FROM integration_credentials", /permission denied/],
    ["SELECT count(*) FROM auth_sessions", /permission denied/],
    ["CREATE TABLE g10_unauthorized (id int)", /permission denied/],
    ["SET session_replication_role = replica", /permission denied/]])
    denies(asProjector, sqlText, pattern);
  const expected = ["organizations:SELECT", "shipment_projection_applications:INSERT", "shipment_projection_applications:SELECT",
    "shipment_tracking_correction_heads:SELECT", "shipment_tracking_corrections:SELECT", "shipment_tracking_input_versions:SELECT",
    "shipment_tracking_projections:INSERT", "shipment_tracking_projections:SELECT", "shipment_tracking_projections:UPDATE",
    "shipment_tracking_timeline_entries:SELECT", "shipments:SELECT", "tracking_accepted_events:SELECT",
    "tracking_inbox_receipts:SELECT", "tracking_processing_effects:SELECT"].sort();
  assert.deepEqual([...tableSet(grants("harborops_projector"))].sort(), expected, "projector table grants must equal the committed contract");
  // The provisioning script changed nothing for the other identities.
  assert.equal(/shipment_tracking_|shipment_projection_/.test(grants("harborops_worker")), false);
  return "least-privilege projector provisioned from the committed script and confined by real-login probes";
});

await check("m10-read-routes-recover-after-database-outage", async () => {
  const path = shipmentRoot() + "/events?limit=1";
  assert.equal((await sessions.analyst.send("GET", path, undefined, { csrf: false })).status, 200);
  assert.equal(compose(["stop", "postgres"]).status, 0);
  try {
    const during = await sessions.analyst.send("GET", path, undefined, { csrf: false }).catch(() => ({ status: 0 }));
    assert.notEqual(during.status, 200, "reads must not succeed without the database");
    assert.ok(during.status === 0 || during.status >= 500 || during.status === 401, "outage answered " + during.status);
  } finally {
    assert.equal(compose(["start", "postgres"]).status, 0);
  }
  await waitFor("postgres healthy", () => compose(["ps", "postgres", "--format", "{{.Health}}"]).stdout.trim() === "healthy", 90_000, 2000);
  const recovered = await waitFor("event route restored", async () => {
    const result = await sessions.analyst.send("GET", path, undefined, { csrf: false }).catch(() => ({ status: 0 }));
    return result.status === 200 ? result : null;
  }, 90_000, 3000);
  assert.equal(recovered.status, 200);
  await waitFor("worker ready", () => probe() === 0, 90_000, 3000);
  return "no stale success during the outage; routes and worker recovered";
});

await check("tenant-isolation-and-live-revocation", async () => {
  const slugB = "cg16-other-" + nonce;
  const adminB = `cg16-adminb-${nonce}@example.test`;
  const run = compose(["run", "--rm", "-T", "-e", "HARBOROPS_ALLOW_ACCOUNT_CLI=true", "api", "--account-cli", "provision", adminB, "Synthetic Other Admin"],
    { input: accountPassword + "\n" });
  assert.equal(run.status, 0, "second tenant admin provisioning failed");
  const bootstrap = compose(["run", "--rm", "-T", "-e", "HARBOROPS_ALLOW_ORGANIZATION_CLI=true", "api", "--organization-cli", "bootstrap", slugB, "Other Tenant", adminB]);
  assert.equal(bootstrap.status, 0, "second tenant bootstrap failed");
  orgB = scalar(`SELECT id FROM organizations WHERE slug='${slugB}'`);
  // Org A manager and auditor cannot see or select tenant B and learn nothing about it.
  for (const role of ["manager", "auditor"]) {
    const select = await sessions[role].send("PUT", "/api/v1/auth/session/organization", { organizationId: orgB });
    assert.ok([403, 404, 409].includes(select.status), role + " must not select a foreign tenant, got " + select.status);
    for (const path of [`/api/v1/organizations/${orgB}/tracking-work`, `/api/v1/organizations/${orgB}/audit-events`]) {
      const foreign = await sessions[role].send("GET", path, undefined, { csrf: false });
      assert.ok([403, 404].includes(foreign.status), role + " foreign " + path.split("/").pop() + " got " + foreign.status);
    }
  }
  // Live revocation: removing the membership denies the already authenticated session at once.
  const analystUser = scalar(`SELECT id FROM users WHERE email='${emails.analyst}'`);
  assert.equal((await sessions.analyst.send("GET", `${root()}/tracking-work`, undefined, { csrf: false })).status, 200);
  assert.equal(scalar(`UPDATE organization_memberships SET status='Removed',version=version+1,updated_at_utc=now()
    WHERE organization_id='${organizationId}' AND user_id='${analystUser}' RETURNING status`).startsWith("Removed"), true);
  const afterRemoval = await sessions.analyst.send("GET", `${root()}/tracking-work`, undefined, { csrf: false });
  assert.ok([401, 403].includes(afterRemoval.status), "removed member must be denied immediately, got " + afterRemoval.status);
  // Session revocation: logout-all invalidates the Manager and the Auditor sessions for both surfaces.
  for (const role of ["manager", "auditor"]) {
    const out = await sessions[role].send("POST", "/api/v1/auth/logout-all", {});
    assert.ok([200, 204].includes(out.status), role + " logout-all " + out.status);
    for (const path of [`${root()}/tracking-work`, `${root()}/audit-events`]) {
      const stale = await sessions[role].send("GET", path, undefined, { csrf: false });
      assert.equal(stale.status, 401, role + " stale session on " + path.split("/").pop() + " got " + stale.status);
    }
  }
  return "foreign tenant invisible, removed member and revoked sessions denied immediately on both surfaces";
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
  for (const secret of [accountPassword, workerPassword, apiPassword, process.env.HARBOROPS_DB_PASSWORD, bearer, bearer.split("_").pop()]) {
    assert.equal(logs.includes(secret), false, "a secret value appeared in service logs");
  }
  assert.doesNotMatch(logs, /hop_v1_[0-9a-f]{32}_/);
  assert.doesNotMatch(logs, /Password=/i);
  return `${logs.split("\n").length} log lines scanned`;
});

const failed = results.filter(x => x.status !== "PASS");
writeFileSync(join(outDir, "g10-stack-results.json"), JSON.stringify({ project, results }, null, 2));
console.log(`G10 stack checks: ${results.length - failed.length} passed, ${failed.length} failed`);
process.exit(failed.length === 0 ? 0 : 1);
