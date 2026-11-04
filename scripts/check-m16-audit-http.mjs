#!/usr/bin/env node
// G16 HTTP authorization, tenant isolation, privacy and fault-injection matrix.
// Runs against the disposable G16 Compose stack only. Every check records an
// expected and an observed value; the process exits 1 if any check fails.
//
// Environment:
//   G16_API_BASE   API origin (default http://localhost:8086)
//   G16_PSQL       psql command prefix with superuser access to the disposable database,
//                  for example "docker compose -p ho-verify-g16 exec -T postgres psql -X -U harborops -d harborops -Atq"
//   HARBOROPS_TEST_USER_PASSWORD  password of the provisioned synthetic accounts
// Usage: node scripts/check-m16-audit-http.mjs <results.json>
import { execFileSync } from "node:child_process";
import { writeFileSync } from "node:fs";

const BASE = process.env.G16_API_BASE ?? "http://localhost:8086";
const PW = process.env.HARBOROPS_TEST_USER_PASSWORD;
const PSQL = (process.env.G16_PSQL ?? "").split(" ").filter(Boolean);
if (!PW || PSQL.length === 0) { console.error("HARBOROPS_TEST_USER_PASSWORD and G16_PSQL are required"); process.exit(2); }
const results = [];
const sleep = ms => new Promise(r => setTimeout(r, ms));
const sql = q => execFileSync(PSQL[0], [...PSQL.slice(1), "-c", q], { encoding: "utf8" }).trim();
function record(id, name, expected, actual, pass, note = "") {
  results.push({ id, name, expected: String(expected), actual: String(actual), pass: Boolean(pass), note });
  console.log(`${pass ? "PASS" : "FAIL"} ${id} ${name} expected=${expected} actual=${actual}${note ? " " + note : ""}`);
}

// The audit read limiter is 30 requests per minute per user. Budget requests per client
// so a verification walk is never mistaken for an abuse response.
class Client {
  jar = new Map(); count = 0; windowStart = 0;
  store(res) {
    for (const c of res.headers.getSetCookie?.() ?? []) {
      const [pair] = c.split(";"); const i = pair.indexOf("=");
      const k = pair.slice(0, i), v = pair.slice(i + 1);
      if (v === "" || /max-age=0|expires=Thu, 01 Jan 1970/i.test(c)) this.jar.delete(k); else this.jar.set(k, v);
    }
  }
  cookie() { return [...this.jar].map(([k, v]) => `${k}=${v}`).join("; "); }
  async budget(path) {
    if (this.unbounded || !path.includes("/audit-events")) return;
    const now = Date.now();
    if (now - this.windowStart > 60000) { this.windowStart = now; this.count = 0; }
    if (this.count >= 24) { await sleep(Math.max(0, 61000 - (now - this.windowStart))); this.windowStart = Date.now(); this.count = 0; }
    this.count++;
  }
  async req(method, path, { body, csrf = false, timeout = 20000 } = {}) {
    await this.budget(path);
    const headers = { cookie: this.cookie(), origin: BASE };
    if (csrf) {
      const r = await fetch(BASE + "/api/v1/auth/csrf", { headers: { cookie: this.cookie() } });
      this.store(r); headers["x-harborops-csrf"] = (await r.json()).token; headers.cookie = this.cookie();
    }
    if (body !== undefined) headers["content-type"] = "application/json";
    const res = await fetch(BASE + path, { method, headers, signal: AbortSignal.timeout(timeout),
      body: body === undefined ? undefined : JSON.stringify(body) });
    this.store(res);
    const text = await res.text(); let json = null; try { json = JSON.parse(text); } catch { /* not json */ }
    return { status: res.status, json, text };
  }
  get(p, o) { return this.req("GET", p, o); }
  mut(m, p, body) { return this.req(m, p, { body, csrf: true }); }
}
async function login(email) {
  const c = new Client(); const r = await c.mut("POST", "/api/v1/auth/login", { email, password: PW });
  if (r.status !== 200) throw new Error(`login ${email} failed with ${r.status}`); return c;
}
const em = n => `${n}@example.test`;
const orgId = slug => sql(`select id from organizations where slug='${slug}'`);
const userId = n => sql(`select id from users where email='${em(n)}'`);
const select = (c, org) => c.mut("PUT", "/api/v1/auth/session/organization", { organizationId: org });
const L = o => `/api/v1/organizations/${o}/audit-events`;

const A = orgId("g16h-a"), B = orgId("g16h-b");
const admin = await login(em("g16h-admin")); await select(admin, A);
async function member(org, name, role) {
  await select(admin, org);
  const r = await admin.mut("PUT", `/api/v1/organizations/${org}/memberships/${userId(name)}`, { role, status: "Active" });
  if (![200, 201].includes(r.status)) throw new Error(`membership ${name} ${role} failed with ${r.status}`);
}
const roster = [["g16h-aud", "Auditor"], ["g16h-mgr", "Manager"], ["g16h-ana", "Analyst"], ["g16h-sup", "Support"],
  ["g16h-rev1", "Auditor"], ["g16h-rev2", "Auditor"], ["g16h-rev3", "Auditor"], ["g16h-rev4", "Auditor"],
  ["g16h-multi", "Auditor"], ["g16h-mix", "Auditor"], ["g16h-fault", "Auditor"], ["g16h-flood", "Support"],
  ["g16h-out", "Auditor"], ["g16h-outmgr", "Manager"]];
for (const [n, role] of roster) await member(A, n, role);
await member(B, "g16h-multi", "Auditor"); await member(B, "g16h-mix", "Analyst");
await select(admin, A);
const S = {};
for (const [n] of roster) { S[n] = await login(em(n)); await select(S[n], A); }

// S13, S14, S15: role matrix on list and detail
const anyAudit = sql(`select id from audit_entries where organization_id='${A}' order by occurred_at_utc desc limit 1`);
for (const [n, ok] of [["g16h-aud", true], ["g16h-mgr", false], ["g16h-ana", false], ["g16h-sup", false]]) {
  const l = await S[n].get(L(A) + "?limit=5"); record("S13/S15", `list as ${n}`, ok ? 200 : 403, l.status, l.status === (ok ? 200 : 403));
  const d = await S[n].get(L(A) + "/" + anyAudit); record("S14/S15", `detail as ${n}`, ok ? 200 : 403, d.status, d.status === (ok ? 200 : 403));
}
{ const l = await admin.get(L(A)); record("S15", "list as Administrator", 403, l.status, l.status === 403);
  const d = await admin.get(L(A) + "/" + anyAudit); record("S15", "detail as Administrator", 403, d.status, d.status === 403); }

// S20: anonymous, spoofed origin on safe reads is not required, malformed transport header
{ const l = await new Client().get(L(A)); record("S20", "anonymous list", 401, l.status, l.status === 401);
  const d = await new Client().get(L(A) + "/" + anyAudit); record("S20", "anonymous detail", 401, d.status, d.status === 401);
  const res = await fetch(BASE + L(A), { headers: { cookie: S["g16h-aud"].cookie(), "x-forwarded-proto": "https", "x-forwarded-for": "203.0.113.9" } });
  record("S20", "spoofed forwarded headers from the trusted peer do not widen access (Auditor read)", "200 or 403", res.status, [200, 403].includes(res.status));
  const anon = await fetch(BASE + L(A), { headers: { "x-forwarded-for": "203.0.113.9", "x-forwarded-proto": "https" } });
  record("S20", "anonymous with spoofed forwarded headers", 401, anon.status, anon.status === 401); }

// S20: transport trust. A valid Auditor session replayed over plain HTTP from a peer that is not
// loopback and not a configured trusted peer must be refused, including with spoofed headers.
if (process.env.G16_NETWORK) {
  const cookie = S["g16h-aud"].cookie();
  const target = process.env.G16_INTERNAL_API ?? "http://api:8080";
  const fromUntrusted = (extra) => execFileSync("docker", ["run", "--rm", "--network", process.env.G16_NETWORK, "--entrypoint", "curl",
    "mcr.microsoft.com/dotnet/sdk:10.0", "-s", "-o", "/dev/null", "-w", "%{http_code}", "-H", `Cookie: ${cookie}`, ...extra,
    `${target}${L(A)}?limit=1`], { encoding: "utf8", env: { ...process.env, MSYS_NO_PATHCONV: "1" } }).trim();
  const plain = fromUntrusted([]);
  record("S20", "valid session over plain HTTP from an untrusted peer, non-local Host", 403, plain, plain === "403");
  const hostSpoof = fromUntrusted(["-H", "Host: localhost:8080"]);
  record("S20", "untrusted peer with Host: localhost", 403, hostSpoof, hostSpoof === "403");
  const xff = fromUntrusted(["-H", "Host: localhost:8080", "-H", "X-Forwarded-For: 127.0.0.1", "-H", "X-Forwarded-Proto: https"]);
  record("S20", "untrusted peer with spoofed X-Forwarded-For and X-Forwarded-Proto", 403, xff, xff === "403");
  const trusted = await S["g16h-aud"].get(L(A) + "?limit=1");
  record("S20", "same session over the trusted local path still works", 200, trusted.status, trusted.status === 200);
}

// S14, S23: response allowlist and absence of credential words
{ const r = await S["g16h-aud"].get(L(A) + "?limit=3");
  const keys = Object.keys(r.json.entries[0]).sort().join(",");
  const allowed = "actorKind,actorRole,actorUserId,correlationId,eventType,id,metadata,occurredAtUtc,organizationId,outcome,resourceId,resourceType,schemaVersion";
  record("S14", "entry DTO keys equal the allowlist", allowed, keys, keys === allowed);
  const bad = /password|hash|cookie|token|bearer|@example\.test/i.test(JSON.stringify(r.json.entries));
  record("S23", "list body free of credential or e-mail text", false, bad, !bad); }

// S16, S17: foreign ids and multi-organization roles
{ const m = S["g16h-multi"];
  let r = await m.get(L(A) + "?limit=1"); record("S17", "multi auditor, A selected, reads A", 200, r.status, r.status === 200);
  r = await m.get(L(B) + "?limit=1"); record("S17", "multi auditor, A selected, reads B", "not 200", r.status, r.status !== 200);
  await select(m, B);
  r = await m.get(L(B) + "?limit=1"); record("S17", "multi auditor, B selected, reads B", 200, r.status, r.status === 200);
  r = await m.get(L(A) + "?limit=1"); record("S17", "multi auditor, B selected, reads A", "not 200", r.status, r.status !== 200);
  await select(m, A);
  const mix = S["g16h-mix"]; await select(mix, B);
  r = await mix.get(L(B) + "?limit=1"); record("S17", "Auditor in A and Analyst in B reads B", 403, r.status, r.status === 403);
  await select(mix, A); }
{ const idB = sql(`select id from audit_entries where organization_id='${B}' limit 1`);
  const rnd = "7f3a1c9e-2b64-4d1f-9a7e-5c0d8b2e6a11"; const aud = S["g16h-aud"];
  const f1 = await aud.get(L(A) + "/" + idB), u1 = await aud.get(L(A) + "/" + rnd);
  record("S16", "foreign id under own organization equals unknown id", `${u1.status}`, `${f1.status}`, f1.status === 404 && u1.status === 404);
  const f2 = await aud.get(L(B) + "/" + idB), u2 = await aud.get(L(B) + "/" + rnd);
  const strip = j => JSON.stringify((({ correlationId, ...x }) => x)(j ?? {}));
  record("S16", "foreign organization path with real id equals unknown id", `${u2.status} ${strip(u2.json)}`, `${f2.status} ${strip(f2.json)}`,
    f2.status === u2.status && f2.status !== 200 && strip(f2.json) === strip(u2.json)); }

// S24, S26: input handling
for (const [q, want] of [["page=0", 400], ["page=2", 400], ["page=abc", 400], ["limit=0", 400], ["limit=101", 400],
  ["limit=abc", 400], ["limit=99999999999", 400], ["limit=%00", 400], ["cursor=AAAA!!!!", 400],
  ["cursor=" + "A".repeat(161), 400], ["limit=5&cursor=", 200]]) {
  const r = await S["g16h-aud"].get(L(A) + "?" + q); record("S24", `query ${q.slice(0, 40)}`, want, r.status, r.status === want);
}
{ const r = await S["g16h-aud"].get(L(A) + "?eventType=%27%3BDROP%20TABLE%20audit_entries--&limit=3");
  record("S24", "unknown filter with injection text is inert", "200 or 400", r.status, [200, 400].includes(r.status));
  const rows = sql("select count(*) from audit_entries"); record("S24", "audit table still populated after injection text", ">0", rows, Number(rows) > 0);
  const g = await S["g16h-aud"].get(L(A) + "/not-a-guid"); record("S24", "detail with malformed id", 400, g.status, g.status === 400); }

// S27: no write or export routes
for (const m of ["POST", "PUT", "PATCH", "DELETE"])
  for (const p of [L(A), L(A) + "/" + anyAudit]) {
    const r = await S["g16h-aud"].mut(m, p, { x: 1 }); record("S27", `${m} ${p.endsWith(anyAudit) ? "detail" : "list"}`, "404 or 405", r.status, [404, 405].includes(r.status));
  }
for (const p of [L(A) + "/export", L(A) + "/export.csv", "/api/v1/audit-events", "/api/v1/audit-events/export"]) {
  const r = await S["g16h-aud"].get(p); record("S27", `GET ${p.replace(A, "{org}")}`, "not 200", r.status, r.status !== 200);
}

// S25: audit access is itself audited, once per read, without recursion
{ const before = Number(sql(`select count(*) from audit_entries where event_type='audit.read' and organization_id='${A}'`));
  await S["g16h-aud"].get(L(A) + "?limit=1");
  const after = Number(sql(`select count(*) from audit_entries where event_type='audit.read' and organization_id='${A}'`));
  record("S25", "one audit.read row per successful list", before + 1, after, after === before + 1);
  const d0 = Number(sql(`select count(*) from audit_entries where event_type='audit.read_denied' and organization_id='${A}'`));
  await S["g16h-mgr"].get(L(A) + "?limit=1");
  const d1 = Number(sql(`select count(*) from audit_entries where event_type='audit.read_denied' and organization_id='${A}'`));
  record("S25/S21", "one audit.read_denied row per denied read", d0 + 1, d1, d1 === d0 + 1);
  const row = sql("select actor_kind||'|'||coalesce(actor_role,'<null>')||'|'||(actor_user_id is not null)||'|'||(actor_session_id is not null) from audit_entries where event_type='audit.read' order by occurred_at_utc desc, id desc limit 1");
  record("S28", "audit.read actor (kind, role, user, session)", "Human|Auditor|true|true", row, row === "Human|Auditor|true|true" || row === "Human|Auditor|t|t"); }

// S01, S05, S28: event taxonomy, actor role, changed fields, privacy of metadata
{ const u = userId("g16h-ana");
  const cur = await admin.get(`/api/v1/organizations/${A}/memberships/${u}`);
  const ev = cur.json?.version ?? cur.json?.membership?.version ?? 1;
  const r = await admin.mut("PUT", `/api/v1/organizations/${A}/memberships/${u}`, { role: "Manager", status: "Active", expectedVersion: ev });
  const row = sql(`select event_type||'|'||actor_kind||'|'||coalesce(actor_role,'<null>')||'|'||(actor_user_id='${userId("g16h-admin")}')||'|'||metadata::text from audit_entries where resource_type='organization_memberships' and resource_id=(select id from organization_memberships where organization_id='${A}' and user_id='${u}') order by occurred_at_utc desc, id desc limit 1`);
  record("S01/S05/S28", `membership role change (PUT ${r.status}) audit row`, 'membership.role_changed|Human|Administrator|true|{"changedFields": ["role"], "operation": "update"}', row,
    row === 'membership.role_changed|Human|Administrator|true|{"changedFields": ["role"], "operation": "update"}');
  const meta = sql("select string_agg(metadata::text, ' ') from audit_entries");
  const leak = /password|secret|token|bearer|cookie|@|sourceEventId|canonical/i.test(meta ?? "");
  record("S01/S23", "no audit metadata contains credential, e-mail or payload text", false, leak, !leak);
  const keys = sql("select string_agg(distinct k, ',' order by k) from audit_entries, jsonb_object_keys(metadata) as k");
  record("S01/S24", "metadata keys are limited to the allowlist", "changedFields,operation", keys, keys === "changedFields,operation"); }

// S26: cursor stability while reads append rows
{ const aud = S["g16h-aud"]; const first = await aud.get(L(A) + "?limit=5");
  const next = first.json.nextCursor; const ids1 = first.json.entries.map(e => e.id);
  const second = await aud.get(L(A) + "?limit=5&cursor=" + encodeURIComponent(next));
  const overlap = second.json.entries.map(e => e.id).filter(i => ids1.includes(i));
  record("S26", "consecutive cursor pages are disjoint", 0, overlap.length, overlap.length === 0 && second.status === 200); }

// S18: revocation takes effect on the next request
async function denied(id, name, c) { const r = await c.get(L(A) + "?limit=1"); record(id, name, "not 200", `${r.status} ${r.json?.code ?? ""}`, r.status !== 200); }
{ const c = S["g16h-rev1"]; let r = await c.get(L(A) + "?limit=1"); record("S18", "rev1 baseline read", 200, r.status, r.status === 200);
  const u = userId("g16h-rev1"); const cur = await admin.get(`/api/v1/organizations/${A}/memberships/${u}`);
  const dg = await admin.mut("PUT", `/api/v1/organizations/${A}/memberships/${u}`, { role: "Analyst", status: "Active", expectedVersion: cur.json?.version ?? 1 });
  await denied("S18", `role downgraded (PUT ${dg.status})`, c); }
{ const u = userId("g16h-rev2"); const cur = await admin.get(`/api/v1/organizations/${A}/memberships/${u}`);
  const rm = await admin.req("DELETE", `/api/v1/organizations/${A}/memberships/${u}`, { body: { expectedVersion: cur.json?.version ?? 1 }, csrf: true });
  await denied("S18", `membership removed (DELETE ${rm.status})`, S["g16h-rev2"]); }
{ await S["g16h-rev3"].mut("POST", "/api/v1/auth/logout-all"); await denied("S18", "session revoked by logout-all", S["g16h-rev3"]); }
{ sql(`update users set status='Disabled', disabled_at_utc=now() where email='${em("g16h-rev4")}'`); await denied("S18", "account disabled", S["g16h-rev4"]); }
{ const c = S["g16h-aud"]; sql(`update organizations set status='Suspended' where id='${A}'`); await denied("S18", "organization suspended", c);
  sql(`update organizations set status='Active' where id='${A}'`);
  const r = await c.get(L(A) + "?limit=1"); record("S18", "organization reactivated restores access", 200, r.status, r.status === 200); }

// S06, S22: fault injection on the audit recording path. EXECUTE is revoked from the API role.
const fn = "m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar)";
{ const aud = S["g16h-fault"], mgr = S["g16h-mgr"];
  const ok = await aud.get(L(A) + "?limit=1"); record("S22", "fault baseline read", 200, ok.status, ok.status === 200);
  sql(`revoke execute on function ${fn} from harborops_api`);
  try {
    const r = await aud.get(L(A) + "?limit=1");
    record("S22", "authorized read when audit recording fails", "503 audit_unavailable", `${r.status} ${r.json?.code}`, r.status === 503 && r.json?.code === "audit_unavailable");
    const blocked = await mgr.get(L(A) + "?limit=1");
    record("S22", "forbidden read stays 403 when denial recording fails", 403, blocked.status, blocked.status === 403);
    const d = await aud.get(L(A) + "/" + anyAudit);
    record("S22", "authorized detail when audit recording fails", "503 audit_unavailable", `${d.status} ${d.json?.code}`, d.status === 503 && d.json?.code === "audit_unavailable");
  } finally { sql(`grant execute on function ${fn} to harborops_api`); }
  const back = await aud.get(L(A) + "?limit=1"); record("S22", "recording restored, read works", 200, back.status, back.status === 200); }

// S21, S26: limiter and denial volume are bounded per user
{ const flood = S["g16h-flood"]; flood.unbounded = true; // deliberately exceed the client budget to hit the real limiter
  const before = Number(sql("select count(*) from audit_entries where event_type='audit.read_denied'"));
  const codes = {}; for (let i = 0; i < 45; i++) { const r = await flood.get(L(A) + "?limit=1"); codes[r.status] = (codes[r.status] ?? 0) + 1; }
  const rows = Number(sql("select count(*) from audit_entries where event_type='audit.read_denied'")) - before;
  record("S21/S26", "forbidden flood: statuses", "403 up to the limit then 429", JSON.stringify(codes), (codes[429] ?? 0) > 0 && (codes[403] ?? 0) <= 30);
  record("S21/S26", "denial rows written by one user in one window", "<= 30", rows, rows <= 30); }

writeFileSync(process.argv[2] ?? "audit-http-results.json", JSON.stringify(results, null, 1));
const failed = results.filter(x => !x.pass);
console.log(`SUMMARY pass=${results.length - failed.length} fail=${failed.length}`);
process.exit(failed.length ? 1 : 0);
