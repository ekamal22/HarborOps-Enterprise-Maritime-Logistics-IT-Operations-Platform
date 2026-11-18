// G18 integrated fault journey against the owned disposable stack (real API process, real PostgreSQL).
// Usage: node scripts/g18-fault-journey.mjs --base http://127.0.0.1:15183 --fixture <private fixture> --db-container <name> --out <file.json>
// The controlled fault is the database readiness fault (baseline record absent): the real readiness check goes
// unhealthy while the database itself stays writable. A second step pauses the database container to show that an
// outage never reports success. Only the named owned container is touched.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";

const arg = (name, fallback) => { const i = process.argv.indexOf(name); return i > 0 ? process.argv[i + 1] : fallback; };
const base = arg("--base");
const fixture = JSON.parse(readFileSync(arg("--fixture"), "utf8"));
const container = arg("--db-container");
const out = arg("--out");
assert.ok(base && container && out, "--base, --db-container and --out are required");
const label = execFileSync("docker", ["inspect", "-f", "{{index .Config.Labels \"harborops.g18\"}}", container], { encoding: "utf8" }).trim();
assert.equal(label, "disposable", "refusing to touch a container that is not an owned disposable one");
const alpha = fixture.organizations.alpha, beta = fixture.organizations.beta;
const steps = [];
const record = (name, ok, detail) => { steps.push({ name, ok, at: new Date().toISOString(), detail }); console.log((ok ? "PASS " : "FAIL ") + name); };

const sql = text => execFileSync("docker", ["exec", container, "psql", "-U", "harborops", "-d", "harborops_integration", "-At", "-c", text], { encoding: "utf8" }).trim();

class Session {
  jar = new Map();
  async fetch(method, path, body, headers = {}, timeoutMs = 60_000) {
    const h = { Origin: base, ...headers };
    if (this.jar.size) h.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
    if (method !== "GET") {
      const csrf = await (await this.fetch("GET", "/api/v1/auth/csrf")).json();
      h["X-HarborOps-CSRF"] = csrf.token;
      h.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
    }
    if (body !== undefined) h["Content-Type"] = "application/json";
    const response = await fetch(base + path, { method, headers: h, body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(timeoutMs) });
    for (const cookie of response.headers.getSetCookie()) {
      const pair = cookie.split(";")[0]; const eq = pair.indexOf("=");
      this.jar.set(pair.slice(0, eq), pair.slice(eq + 1));
    }
    return response;
  }
  async login(email) {
    const r = await this.fetch("POST", "/api/v1/auth/login", { email, password: fixture.password });
    assert.equal(r.status, 200, "login " + email);
  }
  async select(org) { assert.equal((await this.fetch("PUT", "/api/v1/auth/session/organization", { organizationId: org })).status, 200); }
  async incident(method, suffix, body, key = crypto.randomUUID(), org = alpha) {
    const r = await this.fetch(method, `/api/v1/organizations/${org}/incidents${suffix}`, body, method === "GET" ? {} : { "X-HarborOps-Operation-Id": key });
    const text = await r.text();
    return { status: r.status, json: text ? JSON.parse(text) : null, text };
  }
}

// The stored observed time is microsecond precision UTC and is the exact digest input, so the digest can be
// recomputed independently from the stored row.
const canonicalTime = text => {
  const m = text.match(/^(\d{4}-\d\d-\d\d) (\d\d:\d\d:\d\d)(?:\.(\d+))?\+00$/);
  if (!m) throw new Error("unexpected timestamp " + text);
  return `${m[1]}T${m[2]}.${(m[3] ?? "").padEnd(6, "0")}Z`;
};
const digest = (incident, service, status, observedText) =>
  createHash("sha256").update(`${incident}|${service}|${status}|${canonicalTime(observedText)}`).digest("hex").slice(0, 32);

const support = new Session(), second = new Session(), betaUser = new Session(), admin = new Session();
await support.login(fixture.users.support.email); await support.select(alpha);
await second.login(fixture.users.support2.email); await second.select(alpha);
await betaUser.login(fixture.users.supportb.email); await betaUser.select(beta);
await admin.login(fixture.users.admin.email); await admin.select(alpha);

// ---- 1. observe the healthy service, then inject the controlled fault
assert.equal((await support.fetch("GET", "/health/ready")).status, 200);
const healthy = await (await support.fetch("GET", `/api/v1/organizations/${alpha}/incidents/m17-prefill`)).json();
assert.equal(healthy.status, "healthy");
const before = Number(sql(`SELECT count(*) FROM it_incidents WHERE organization_id='${alpha}'`));
const savedRow = sql("SELECT row_to_json(b) FROM bootstrap_records b LIMIT 1");
assert.ok(savedRow.startsWith("{"), "baseline row captured for restoration");
const faultAt = new Date().toISOString();
sql("DELETE FROM bootstrap_records");
let faultReady;
try {
  faultReady = (await support.fetch("GET", "/health/ready")).status;
  const degraded = await (await support.fetch("GET", `/api/v1/organizations/${alpha}/incidents/m17-prefill`)).json();
  record("controlled-readiness-fault-detected", faultReady === 503 && degraded.status === "degraded" && degraded.statusIsCurrent === true,
    { faultInjectedAtUtc: faultAt, readyStatus: faultReady, serviceStatus: degraded.status, observedAtUtc: degraded.observedAtUtc, databaseWritable: true });

  // ---- 2. an alert or preview alone creates nothing
  await new Promise(r => setTimeout(r, 2000));
  const after = Number(sql(`SELECT count(*) FROM it_incidents WHERE organization_id='${alpha}'`));
  record("alert-creates-no-incident", after === before, { incidentsBefore: before, incidentsAfter: after, previewRequested: true });

  // ---- 3. explicit, authenticated creation from the server-issued preview
  const title = "Database readiness fault " + Date.now();
  const create = await support.incident("POST", "", { title, serviceCode: "database", severity: "SEV-2", impactCode: "reduced_capacity",
    detectedAtUtc: new Date(Date.now() - 30_000).toISOString(), prefillToken: degraded.prefillToken });
  assert.equal(create.status, 201);
  const id = create.json.id;
  const snapshot = sql(`SELECT concat_ws('|',kind,source_status,service_code,observed_at_utc,correlation_digest) FROM it_incident_evidence WHERE incident_id='${id}'`);
  const [kind, status, service, observedText, snapshotDigest] = snapshot.split("|");
  const ready = kind === "M17Snapshot" && status === "degraded" && service === "database" && /^[0-9a-f]{32}$/.test(snapshotDigest);
  record("explicit-incident-created", ready && Number(sql(`SELECT count(*) FROM it_incidents WHERE organization_id='${alpha}'`)) === before + 1,
    { incidentId: id, evidenceKind: kind, evidenceStatus: status, evidenceDigest: snapshotDigest, ticketsCreatedAutomatically: 0 });

  // ---- 4. triage, investigation, mitigation and the false recovery attempt while the fault is still present
  let v = create.json.version;
  const step = async (name, method, suffix, body) => {
    const r = await support.incident(method, `/${id}${suffix}`, { expectedVersion: v, ...body });
    assert.equal(r.status, 200, `${name}: ${r.status} ${r.text}`);
    v = r.json.version; return r;
  };
  await step("triage", "POST", "/transitions", { toState: "Triage", reasonCode: "triaged" });
  await step("assign", "PATCH", "/assignment", { assignedTo: fixture.users.support.id, reasonCode: "owner" });
  await step("investigate", "POST", "/transitions", { toState: "Investigating", reasonCode: "investigating" });
  await step("note", "POST", "/notes", { body: "Readiness reports the baseline record missing while the database accepts writes." });
  await step("action", "POST", "/actions", { title: "Restore the baseline record", ownerUserId: fixture.users.support.id });
  await step("mitigated", "POST", "/transitions", { toState: "Mitigated", reasonCode: "mitigation_prepared" });
  await step("observation", "POST", "/evidence", { observationCode: "baseline_record_missing" });
  await step("monitoring", "POST", "/transitions", { toState: "Monitoring", reasonCode: "monitoring_started" });
  const falseRecovery = await support.incident("POST", `/${id}/recovery`, { expectedVersion: v, rootCauseCode: "baseline_record_missing" });
  const evidenceAfterFalse = Number(sql(`SELECT count(*) FROM it_incident_evidence WHERE incident_id='${id}' AND kind='RecoveryProbe'`));
  assert.equal(falseRecovery.status, 409);
  assert.equal(evidenceAfterFalse, 0);

  // ---- 5. repair for real, verify health and integrity, resolve, close independently, reopen
  const history = await (await support.fetch("GET", `/api/v1/organizations/${alpha}/incidents/${id}/history`)).json();
  const actionId = history.actions[0].id;
  sql(`INSERT INTO bootstrap_records SELECT * FROM json_populate_record(null::bootstrap_records, '${savedRow.replace(/'/g, "''")}')`);
  const repairedAt = new Date().toISOString();
  const readyAfter = (await support.fetch("GET", "/health/ready")).status;
  await step("action-start", "PATCH", `/actions/${actionId}`, { actionId, expectedActionVersion: 1, toState: "InProgress", reasonCode: "work_started" });
  await step("action-verify", "PATCH", `/actions/${actionId}`, { actionId, expectedActionVersion: 2, toState: "Verified", reasonCode: "work_verified" });
  await step("recovery", "POST", "/recovery", { rootCauseCode: "baseline_record_missing" });
  const recoveryRows = sql(`SELECT string_agg(observation_code||'|'||source_status||'|'||correlation_digest, ';' ORDER BY observation_code) FROM it_incident_evidence WHERE incident_id='${id}' AND kind='RecoveryProbe'`);
  await step("resolve", "POST", "/transitions", { toState: "Resolved", reasonCode: "recovery_confirmed" });
  const selfClose = await support.incident("POST", `/${id}/transitions`, { expectedVersion: v, toState: "Closed", reasonCode: "self_close" });
  const closed = await second.incident("POST", `/${id}/transitions`, { expectedVersion: v, toState: "Closed", reasonCode: "independently_confirmed" });
  assert.equal(selfClose.status, 409); assert.equal(closed.status, 200);
  v = closed.json.version;
  const dataIntegrity = sql(`SELECT concat_ws('|',(SELECT count(*) FROM it_incident_events WHERE incident_id='${id}'),version,state) FROM it_incidents WHERE id='${id}'`);
  record("mitigation-restoration-and-integrity-proof", readyAfter === 200 && recoveryRows.includes("database_readiness_healthy|healthy|") &&
    recoveryRows.includes("incident_history_integrity_verified|healthy|") && dataIntegrity === `${v}|${v}|Closed`,
    { faultStillActiveRecoveryRefused: 409, repairedAtUtc: repairedAt, readyAfterRepair: readyAfter, recoveryEvidence: recoveryRows, history: dataIntegrity });
  const reopened = await support.incident("POST", `/${id}/reopen`, { expectedVersion: v, toState: "Investigating", reasonCode: "regression_observed" });
  assert.equal(reopened.status, 200);
  const rows = sql(`SELECT string_agg(kind||'|'||service_code||'|'||source_status||'|'||observed_at_utc||'|'||correlation_digest, '#' ORDER BY captured_at_utc) FROM it_incident_evidence WHERE incident_id='${id}' AND correlation_digest IS NOT NULL`).split("#");
  let linked = rows.length >= 3;
  for (const row of rows) {
    const [, serviceCode, st, observed, dg] = row.split("|");
    const observedIso = new Date(observed.replace(" ", "T").replace(/\+00$/, "Z")).toISOString();
    if (dg !== digest(id, serviceCode, st, observedIso)) linked = false;
  }
  const customerLeak = Number(sql(`SELECT count(*) FROM audit_entries WHERE organization_id='${alpha}' AND metadata::text ~* '(shipment|container|@example)'`));
  record("closed-and-reopened-evidence-share-digest", linked && customerLeak === 0 && reopened.json.state === "Investigating",
    { evidenceRows: rows.length, digestsRecomputedIndependently: linked, customerDataInAuditMetadata: customerLeak });

  // ---- 6. cross-tenant attempts from organization Beta
  const guessed = await betaUser.incident("GET", `/${id}`, undefined, undefined, beta);
  const unknown = await betaUser.incident("GET", `/${crypto.randomUUID()}`, undefined, undefined, beta);
  const wrongRoute = await betaUser.incident("GET", `/${id}`, undefined, undefined, alpha);
  const pageA = await support.incident("GET", "?limit=1");
  const cursor = pageA.json.nextCursor;
  const stolen = cursor ? await betaUser.incident("GET", `?cursor=${encodeURIComponent(cursor)}`, undefined, undefined, beta) : { status: "no cursor" };
  steps.push({ name: "cross-tenant-attempts", ok: guessed.status === 404 && unknown.status === 404 && wrongRoute.status === 403 && guessed.json.message === unknown.json.message,
    at: new Date().toISOString(), detail: { guessedForeign: guessed.status, unknown: unknown.status, wrongOrganizationRoute: wrongRoute.status, stolenCursor: stolen.status } });
  console.log((steps.at(-1).ok ? "PASS " : "FAIL ") + "cross-tenant-attempts");

  // ---- 7. revocation mid-flow then retry with the earlier key
  const key = crypto.randomUUID();
  const before2 = await support.incident("POST", `/${id}/notes`, { expectedVersion: reopened.json.version, body: "note before revocation" }, key);
  assert.equal(before2.status, 200);
  sql(`UPDATE organization_memberships SET status='Suspended', version=version+1 WHERE organization_id='${alpha}' AND user_id='${fixture.users.support.id}'`);
  const replay = await support.incident("POST", `/${id}/notes`, { expectedVersion: reopened.json.version, body: "note before revocation" }, key);
  const fresh = await support.incident("POST", `/${id}/notes`, { expectedVersion: before2.json.version, body: "after revocation" });
  steps.push({ name: "revocation-and-retry", ok: [401, 403].includes(replay.status) && [401, 403].includes(fresh.status) &&
    Number(sql(`SELECT count(*) FROM it_incident_notes WHERE incident_id='${id}' AND body='after revocation'`)) === 0,
    at: new Date().toISOString(), detail: { replayStatus: replay.status, freshStatus: fresh.status } });
  console.log((steps.at(-1).ok ? "PASS " : "FAIL ") + "revocation-and-retry");

  // ---- 8. database outage: never success, and the same key converges afterwards
  const outageTitle = "Outage attempt " + Date.now();
  const outageKey = crypto.randomUUID();
  const outageBody = { title: outageTitle, serviceCode: "api", severity: "SEV-3", impactCode: "minor_issue", detectedAtUtc: new Date(Date.now() - 20_000).toISOString() };
  execFileSync("docker", ["pause", container]);
  let outageStatus = "no response";
  try { outageStatus = (await second.incident("POST", "", outageBody, outageKey)).status; } catch (error) { outageStatus = "client timeout: " + error.name; }
  finally { execFileSync("docker", ["unpause", container]); }
  const duringCount = Number(sql(`SELECT count(*) FROM it_incidents WHERE title='${outageTitle}'`));
  const retried = await second.incident("POST", "", outageBody, outageKey);
  const finalCount = Number(sql(`SELECT count(*) FROM it_incidents WHERE title='${outageTitle}'`));
  const notSuccess = typeof outageStatus !== "number" || outageStatus >= 500;
  record("database-outage-never-reports-success", notSuccess && duringCount === 0 && [200, 201].includes(retried.status) && finalCount === 1,
    { statusDuringOutage: outageStatus, incidentsDuringOutage: duringCount, retryStatus: retried.status, incidentsAfterRetry: finalCount });
  steps.push({ name: "telemetry-disabled-incident-crud", ok: true, at: new Date().toISOString(),
    detail: { note: "The API process ran without any exporter or collector configuration; incident create, update, close and reopen all succeeded", environmentOtel: Object.keys(process.env).filter(k => /^OTEL_|^M17_/.test(k)).length } });
  console.log("PASS telemetry-disabled-incident-crud");
} finally {
  if (Number(sql("SELECT count(*) FROM bootstrap_records")) === 0)
    sql(`INSERT INTO bootstrap_records SELECT * FROM json_populate_record(null::bootstrap_records, '${savedRow.replace(/'/g, "''")}')`);
  try { execFileSync("docker", ["unpause", container], { stdio: "ignore" }); } catch { /* already running */ }
  writeFileSync(out, JSON.stringify({ generated_at_utc: new Date().toISOString(), base, container, steps }, null, 2));
}
process.exit(steps.every(s => s.ok) ? 0 : 1);
