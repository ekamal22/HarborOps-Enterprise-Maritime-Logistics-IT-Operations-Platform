// G16 independent HTTP probe against a disposable probe API (verification only).
// Usage: node http-probe.mjs <outfile.json>   Env: PROBE_USER_PW_FILE
import { readFileSync, writeFileSync } from "node:fs";
import { execFileSync } from "node:child_process";

const BASE = "http://127.0.0.1:18086";
const ORIGIN = "http://127.0.0.1:18086";
const PW = readFileSync(process.env.PROBE_USER_PW_FILE, "utf8").trim();
const out = [];
const sql = q => execFileSync("docker", ["exec", "-i", "ho-probe-pg", "psql", "-X", "-U", "harborops",
  "-d", "harborops", "-Atq", "-c", q], { encoding: "utf8" }).trim();
const record = (id, name, expected, actual, pass, note = "") => {
  out.push({ id, name, expected, actual, pass, note });
  console.log(`${pass ? "PASS" : "FAIL"} ${id} ${name} expected=${expected} actual=${actual} ${note}`);
};
const sleep = ms => new Promise(r => setTimeout(r, ms));

class Client {
  jar = new Map();
  store(res) {
    for (const c of res.headers.getSetCookie?.() ?? []) {
      const [pair] = c.split(";"); const i = pair.indexOf("=");
      const k = pair.slice(0, i), v = pair.slice(i + 1);
      if (/expires=Thu, 01 Jan 1970|max-age=0/i.test(c) || v === "") this.jar.delete(k); else this.jar.set(k, v);
    }
  }
  cookie() { return [...this.jar].map(([k, v]) => `${k}=${v}`).join("; "); }
  async raw(method, path, { body, csrf = false, origin = ORIGIN, timeout = 20000 } = {}) {
    const headers = { cookie: this.cookie() };
    if (csrf) {
      const r = await fetch(BASE + "/api/v1/auth/csrf", { headers: { cookie: this.cookie() } });
      this.store(r);
      headers["x-harborops-csrf"] = (await r.json()).token;
      headers.cookie = this.cookie();
    }
    if (origin) headers.origin = origin;
    if (body !== undefined) headers["content-type"] = "application/json";
    const res = await fetch(BASE + path, { method, headers, signal: AbortSignal.timeout(timeout),
      body: body === undefined ? undefined : JSON.stringify(body) });
    this.store(res);
    const text = await res.text();
    let json = null; try { json = JSON.parse(text); } catch { /* not json */ }
    return { status: res.status, json, text };
  }
  get(p, o) { return this.raw("GET", p, o); }
  mut(m, p, body) { return this.raw(m, p, { body, csrf: true }); }
}
async function login(email) {
  const c = new Client();
  const r = await c.mut("POST", "/api/v1/auth/login", { email, password: PW });
  if (r.status !== 200) throw new Error(`login ${email} -> ${r.status} ${r.text.slice(0, 120)}`);
  return c;
}
const orgId = slug => sql(`select id from organizations where slug='${slug}'`);
const userId = e => sql(`select id from users where email='${e}'`);
const select = (c, org) => c.mut("PUT", "/api/v1/auth/session/organization", { organizationId: org });

const A = orgId("probe-org"), B = orgId("probe-org-b");
const em = n => `${n}@example.test`;
const admin = await login(em("probe-admin"));
await select(admin, A);
async function member(org, name, role) {
  await select(admin, org);
  const u = userId(em(name));
  const r = await admin.mut("PUT", `/api/v1/organizations/${org}/memberships/${u}`, { role, status: "Active" });
  if (![200, 201].includes(r.status)) throw new Error(`member ${name} ${role} -> ${r.status} ${r.text.slice(0, 100)}`);
}
for (const [n, role] of [["probe-aud", "Auditor"], ["probe-mgr", "Manager"], ["probe-ana", "Analyst"],
  ["probe-sup", "Support"], ["probe-rev1", "Auditor"], ["probe-rev2", "Auditor"], ["probe-rev3", "Auditor"],
  ["probe-rev4", "Auditor"], ["probe-multi", "Auditor"], ["probe-mix", "Auditor"]]) await member(A, n, role);
await member(B, "probe-multi", "Auditor");
await member(B, "probe-mix", "Analyst");
await select(admin, A);

const sessions = {};
for (const n of ["probe-aud", "probe-mgr", "probe-ana", "probe-sup", "probe-multi", "probe-mix",
  "probe-rev1", "probe-rev2", "probe-rev3", "probe-rev4"]) {
  sessions[n] = await login(em(n)); await select(sessions[n], A);
}
const L = o => `/api/v1/organizations/${o}/audit-events`;

// S13 and S15 role matrix on the list route; detail route as well
const anyAudit = sql(`select id from audit_entries where organization_id='${A}' order by occurred_at_utc desc limit 1`);
for (const [n, expectOk] of [["probe-aud", true], ["probe-mgr", false], ["probe-ana", false], ["probe-sup", false]]) {
  const r = await sessions[n].get(L(A) + "?limit=5");
  record("S13/S15", `list as ${n}`, expectOk ? 200 : "403", r.status, expectOk ? r.status === 200 : r.status === 403);
  const d = await sessions[n].get(L(A) + "/" + anyAudit);
  record("S14/S15", `detail as ${n}`, expectOk ? 200 : "403", d.status, expectOk ? d.status === 200 : d.status === 403);
}
{ const r = await admin.get(L(A)); record("S15", "list as Administrator", 403, r.status, r.status === 403);
  const d = await admin.get(L(A) + "/" + anyAudit); record("S15", "detail as Administrator", 403, d.status, d.status === 403); }
{ const r = await new Client().get(L(A)); record("S20", "anonymous list", 401, r.status, r.status === 401);
  const d = await new Client().get(L(A) + "/" + anyAudit); record("S20", "anonymous detail", 401, d.status, d.status === 401); }

// S14 DTO allowlist
{ const r = await sessions["probe-aud"].get(L(A) + "?limit=3");
  const keys = Object.keys(r.json.entries[0]).sort().join(",");
  const allowed = "actorKind,actorRole,actorUserId,correlationId,eventType,id,metadata,occurredAtUtc,organizationId,outcome,resourceId,resourceType,schemaVersion";
  record("S14", "response DTO keys", allowed, keys, keys === allowed);
  const tooMuch = /password|hash|cookie|token|email/i.test(JSON.stringify(r.json));
  record("S23", "list body free of credential/email words", false, tooMuch, !tooMuch); }

// S16/S17 multi-organization and foreign ids
{ const m = sessions["probe-multi"];
  let r = await m.get(L(A) + "?limit=1"); record("S17", "multi auditor selected A reads A", 200, r.status, r.status === 200);
  r = await m.get(L(B) + "?limit=1"); record("S17", "multi auditor selected A reads B", "not 200", r.status, r.status !== 200, r.json?.code);
  await select(m, B);
  r = await m.get(L(B) + "?limit=1"); record("S17", "multi auditor selected B reads B", 200, r.status, r.status === 200);
  r = await m.get(L(A) + "?limit=1"); record("S17", "multi auditor selected B reads A", "not 200", r.status, r.status !== 200, r.json?.code);
  const mix = sessions["probe-mix"]; await select(mix, B);
  r = await mix.get(L(B) + "?limit=1"); record("S17", "Auditor in A, Analyst in B, selected B reads B", 403, r.status, r.status === 403);
  await select(mix, A); await select(m, A); }
{ const idB = sql(`select id from audit_entries where organization_id='${B}' limit 1`);
  const rnd = "7f3a1c9e-2b64-4d1f-9a7e-5c0d8b2e6a11";
  const aud = sessions["probe-aud"];
  const foreignUnderA = await aud.get(L(A) + "/" + idB);
  const unknownUnderA = await aud.get(L(A) + "/" + rnd);
  record("S16", "foreign id under own org vs unknown id (status)", unknownUnderA.status, foreignUnderA.status,
    foreignUnderA.status === unknownUnderA.status && foreignUnderA.status === 404);
  const foreignUnderB = await aud.get(L(B) + "/" + idB);
  const unknownUnderB = await aud.get(L(B) + "/" + rnd);
  record("S16", "foreign org path with real id vs unknown id (status)", unknownUnderB.status, foreignUnderB.status,
    foreignUnderB.status === unknownUnderB.status && foreignUnderB.status !== 200,
    JSON.stringify([foreignUnderB.json?.code, unknownUnderB.json?.code]));
  const sameBody = (({ correlationId, ...a }) => JSON.stringify(a))(foreignUnderB.json ?? {}) ===
    (({ correlationId, ...a }) => JSON.stringify(a))(unknownUnderB.json ?? {});
  record("S16", "foreign vs unknown id body identical", true, sameBody, sameBody); }

// S24 malicious or oversized input
for (const [q, want] of [["page=0", 400], ["page=1001", 400], ["page=abc", 400], ["page=-1", 400], ["limit=0", 400],
  ["limit=101", 400], ["limit=abc", 400], ["limit=99999999999", 400], ["page=99999999999", 400],
  ["limit=5&limit=6", "400 or 200"], ["eventType=%27%3BDROP%20TABLE%20audit_entries--&limit=3", "ignored or 400"],
  ["limit=%00", 400], ["from=2020-01-01&to=bad&limit=3", "ignored or 400"]]) {
  const r = await sessions["probe-aud"].get(L(A) + "?" + q);
  const ok = typeof want === "number" ? r.status === want : [200, 400].includes(r.status);
  record("S24/S26", `query ${q}`, want, r.status, ok);
}
{ const r = await sessions["probe-aud"].get(L(A) + "/not-a-guid"); record("S24", "detail bad guid", 400, r.status, r.status === 400);
  const r2 = await sessions["probe-aud"].get("/api/v1/organizations/not-a-guid/audit-events"); record("S24", "bad org guid", 400, r2.status, r2.status === 400);
  const r3 = await sessions["probe-aud"].get(L(A) + "/"); record("S24", "list trailing slash", "200/404", r3.status, [200, 404].includes(r3.status)); }

// S27 write and export routes absent
for (const m of ["POST", "PUT", "PATCH", "DELETE"]) {
  for (const p of [L(A), L(A) + "/" + anyAudit]) {
    const r = await sessions["probe-aud"].mut(m, p, { x: 1 });
    record("S27", `${m} ${p.endsWith(anyAudit) ? "detail" : "list"}`, "404/405", r.status, [404, 405].includes(r.status));
  }
}
for (const p of [L(A) + "/export", L(A) + "/export.csv", "/api/v1/audit-events", "/api/v1/audit-events/export"]) {
  const r = await sessions["probe-aud"].get(p); record("S27", `GET ${p.replace(A, "{org}")}`, "not 200", r.status, r.status !== 200);
}

// S25 reads are themselves recorded, one row per successful read, no recursion
{ const before = Number(sql(`select count(*) from audit_entries where event_type='audit.read' and organization_id='${A}'`));
  await sessions["probe-aud"].get(L(A) + "?limit=1");
  const after = Number(sql(`select count(*) from audit_entries where event_type='audit.read' and organization_id='${A}'`));
  record("S25", "one audit.read row per successful list", before + 1, after, after === before + 1);
  const bd = Number(sql(`select count(*) from audit_entries where event_type='audit.read_denied' and organization_id='${A}'`));
  await sessions["probe-mgr"].get(L(A) + "?limit=1");
  const bd2 = Number(sql(`select count(*) from audit_entries where event_type='audit.read_denied' and organization_id='${A}'`));
  record("S25/S21", "denied read recorded once", bd + 1, bd2, bd2 === bd + 1);
  const rowdetail = sql(`select actor_kind||'|'||coalesce(actor_role,'<null>')||'|'||(actor_user_id is not null)||'|'||(actor_session_id is not null) from audit_entries where event_type='audit.read' order by occurred_at_utc desc limit 1`);
  record("S28", "audit.read row actor fields (kind|role|user|session)", "Human|Auditor|t|t", rowdetail, rowdetail === "Human|Auditor|true|true" || rowdetail === "Human|Auditor|t|t"); }

// S28 actor attribution of an HTTP mutation
{ await select(admin, A);
  const before = sql(`select count(*) from audit_entries`);
  const u = userId(em("probe-ana"));
  const cur = await admin.get(`/api/v1/organizations/${A}/memberships/${u}`);
  const ev = cur.json?.version ?? cur.json?.membership?.version ?? 1;
  const r = await admin.mut("PUT", `/api/v1/organizations/${A}/memberships/${u}`, { role: "Analyst", status: "Active", expectedVersion: ev });
  const row = sql(`select event_type||'|'||actor_kind||'|'||coalesce(actor_role,'<null>')||'|'||(actor_user_id='${userId(em("probe-admin"))}')||'|'||metadata::text from audit_entries where event_type like 'organization_memberships.%' order by occurred_at_utc desc limit 1`);
  record("S05/S28", `HTTP membership PUT (status ${r.status}) audit row`, "organization_memberships.update|Human|Administrator|true|changed fields", row, false,
    "informational: expected role and changed-field evidence per privacy policy"); }

// S26 pagination stability under concurrent audit-read appends
{ const aud = sessions["probe-aud"];
  const p1 = await aud.get(L(A) + "?limit=5&page=1");
  const p2 = await aud.get(L(A) + "?limit=5&page=2");
  const ids1 = p1.json.entries.map(e => e.id), ids2 = p2.json.entries.map(e => e.id);
  const overlap = ids1.filter(i => ids2.includes(i));
  record("S26", "page1/page2 disjoint (reads append rows between calls)", 0, overlap.length, overlap.length === 0, `overlap=${overlap.length}`); }

// S18 revocation: each case must deny on the very next request
async function expectDenied(id, name, c) {
  const r = await c.get(L(A) + "?limit=1"); record(id, name, "not 200", r.status, r.status !== 200, r.json?.code ?? "");
}
{ const c = sessions["probe-rev1"]; let r = await c.get(L(A) + "?limit=1"); record("S18", "rev1 baseline", 200, r.status, r.status === 200);
  await select(admin, A); const u = userId(em("probe-rev1"));
  const cur = await admin.get(`/api/v1/organizations/${A}/memberships/${u}`); const ev = cur.json?.version ?? 1;
  const dg = await admin.mut("PUT", `/api/v1/organizations/${A}/memberships/${u}`, { role: "Analyst", status: "Active", expectedVersion: ev });
  await expectDenied("S18", `role downgraded to Analyst (PUT ${dg.status})`, c); }
{ const c = sessions["probe-rev2"]; const u = userId(em("probe-rev2"));
  const cur = await admin.get(`/api/v1/organizations/${A}/memberships/${u}`); const ev = cur.json?.version ?? 1;
  const rm = await admin.raw("DELETE", `/api/v1/organizations/${A}/memberships/${u}`, { body: { expectedVersion: ev }, csrf: true });
  await expectDenied("S18", `membership removed (DELETE ${rm.status})`, c); }
{ const c = sessions["probe-rev3"]; await c.mut("POST", "/api/v1/auth/logout-all");
  await expectDenied("S18", "session revoked (logout-all)", c); }
{ const c = sessions["probe-rev4"]; sql(`update users set status='Disabled', disabled_at_utc=now() where email='${em("probe-rev4")}'`);
  await expectDenied("S18", "account disabled", c); }
{ const c = sessions["probe-aud"]; sql(`update organizations set status='Suspended' where id='${A}'`);
  await expectDenied("S18", "organization suspended", c);
  sql(`update organizations set status='Active' where id='${A}'`);
  const r = await c.get(L(A) + "?limit=1"); record("S18", "organization reactivated, auditor reads again", 200, r.status, r.status === 200); }

// S22 and S29 fault injection on the disposable database
{ const aud = sessions["probe-aud"], mgr = sessions["probe-mgr"];
  sql(`revoke execute on function m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar) from harborops_api`);
  const okRead = await aud.get(L(A) + "?limit=1");
  record("S22", "audit-read record fails (grant removed): authorized read", "not 200 (fail closed)", okRead.status, okRead.status !== 200, okRead.json?.code ?? "");
  const denied = await mgr.get(L(A) + "?limit=1");
  record("S22", "audit-read record fails: forbidden read stays denied", 403, denied.status, denied.status === 403);
  sql(`grant execute on function m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar) to harborops_api`);
  const back = await aud.get(L(A) + "?limit=1"); record("S22", "restored grant, read works", 200, back.status, back.status === 200); }
{ const aud = sessions["probe-aud"];
  execFileSync("docker", ["pause", "ho-probe-pg"]);
  let r; try { r = await aud.get(L(A) + "?limit=1", { timeout: 12000 }); } catch (e) { r = { status: "client-timeout" }; }
  execFileSync("docker", ["unpause", "ho-probe-pg"]);
  record("S29", "DB paused: read does not succeed", "not 200", r.status, r.status !== 200);
  await sleep(3000);
  const back = await aud.get(L(A) + "?limit=1", { timeout: 20000 }); record("S29", "after recovery read works", 200, back.status, back.status === 200); }

// S26 rate limit last, it throttles the caller for a minute
{ const aud = sessions["probe-aud"]; await sleep(61000);
  const codes = {}; let first429 = null;
  for (let i = 1; i <= 140; i++) { const r = await aud.get(L(A) + "?limit=1"); codes[r.status] = (codes[r.status] ?? 0) + 1; if (r.status === 429 && !first429) first429 = i; }
  record("S26", "audit-read limiter (120 per minute per user)", "429 after 120", JSON.stringify(codes), (codes[429] ?? 0) > 0, `first429=${first429}`);
  const mgr = sessions["probe-mgr"]; const before = Number(sql(`select count(*) from audit_entries where event_type='audit.read_denied'`));
  const dc = {}; for (let i = 0; i < 150; i++) { const r = await mgr.get(L(A) + "?limit=1"); dc[r.status] = (dc[r.status] ?? 0) + 1; }
  const after = Number(sql(`select count(*) from audit_entries where event_type='audit.read_denied'`));
  record("S21/S26", "denial flood: statuses and denial rows written", "bounded", JSON.stringify(dc) + ` rows+${after - before}`, (dc[429] ?? 0) > 0, "rows written per minute are bounded only by the 120/min limiter"); }

writeFileSync(process.argv[2], JSON.stringify(out, null, 1));
console.log(`SUMMARY pass=${out.filter(x => x.pass).length} fail=${out.filter(x => !x.pass).length}`);
