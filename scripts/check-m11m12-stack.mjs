#!/usr/bin/env node
// M11/M12 real-network and process-fault verification against the disposable Compose stack
// ho-verify-m11m12 (actual Nginx, Kestrel and PostgreSQL). Records an expected and an observed
// value for every check and exits 1 if any check fails. Never targets any other stack.
//
// Environment:
//   M11M12_WEB      Nginx origin (default http://localhost:5181)
//   M11M12_API      Direct API origin (default http://localhost:8091)
//   M11M12_PROJECT  Compose project (default ho-verify-m11m12)
//   HARBOROPS_TEST_USER_PASSWORD  password of the provisioned synthetic accounts
//   M11M12_CPUS     CPU limit applied during the CPU-bound case (default 0.02)
// Usage: node scripts/check-m11m12-stack.mjs <phase: prepare|g12|g11> <results.json>
import { execFileSync, spawn } from "node:child_process";
import { createHash } from "node:crypto";
import { writeFileSync } from "node:fs";
import net from "node:net";

const WEB = process.env.M11M12_WEB ?? "http://localhost:5181";
const API = process.env.M11M12_API ?? "http://localhost:8091";
const PROJECT = process.env.M11M12_PROJECT ?? "ho-verify-m11m12";
const PW = process.env.HARBOROPS_TEST_USER_PASSWORD;
const QUOTA = process.env.M11M12_CPU_QUOTA_US ?? "3000"; // microseconds of CPU per one second period
const phase = process.argv[2]; const out = process.argv[3];
if (!PW || !phase || !out || !PROJECT.startsWith("ho-verify-m11m12")) {
  console.error("password, phase, results path and the ho-verify-m11m12 project are required"); process.exit(2);
}
const PG = `${PROJECT}-postgres-1`, APIC = `${PROJECT}-api-1`, WEBC = `${PROJECT}-web-1`;
const results = [];
const sleep = ms => new Promise(r => setTimeout(r, ms));
const dk = (...a) => execFileSync("docker", a, { encoding: "utf8", env: { ...process.env, MSYS_NO_PATHCONV: "1" } }).trim();
const sql = q => dk("exec", PG, "psql", "-X", "-U", "harborops", "-d", "harborops", "-Atq", "-c", q);
const apiSql = q => dk("exec", PG, "psql", "-X", "-U", "harborops", "-d", "harborops", "-Atq", "-c", q);
function record(id, name, expected, actual, pass, note = "") {
  results.push({ id, name, expected: String(expected), actual: String(actual), pass: Boolean(pass), note });
  console.log(`${pass ? "PASS" : "FAIL"} ${id} ${name} expected=${expected} actual=${actual}${note ? " " + note : ""}`);
}
const within = (v, lo, hi) => v >= lo && v <= hi;
const NCPU = dk("info", "--format", "{{.NCPU}}");
// "docker update --cpus 0" does not remove a limit; set the full host CPU count instead.
const cpuRestore = () => dk("update", "--cpu-period", "100000", "--cpu-quota", String(Number(NCPU) * 100000), APIC);
const cpuThrottle = () => dk("update", "--cpu-period", "1000000", "--cpu-quota", QUOTA, APIC);
const only = (process.env.M11M12_ONLY ?? "").split(",").filter(Boolean);
const on = id => only.length === 0 || only.includes(id);

class Client {
  jar = new Map(); constructor(base) { this.base = base; }
  store(res) {
    for (const c of res.headers.getSetCookie?.() ?? []) {
      const [pair] = c.split(";"); const i = pair.indexOf("=");
      const k = pair.slice(0, i), v = pair.slice(i + 1);
      if (v === "" || /max-age=0|expires=Thu, 01 Jan 1970/i.test(c)) this.jar.delete(k); else this.jar.set(k, v);
    }
  }
  cookie() { return [...this.jar].map(([k, v]) => `${k}=${v}`).join("; "); }
  async req(method, path, { body, csrf = false, timeout = 60000, raw = false } = {}) {
    const headers = { cookie: this.cookie(), origin: this.base };
    if (csrf) {
      const r = await fetch(this.base + "/api/v1/auth/csrf", { headers: { cookie: this.cookie() } });
      this.store(r); headers["x-harborops-csrf"] = (await r.json()).token; headers.cookie = this.cookie();
    }
    if (body !== undefined) headers["content-type"] = "application/json";
    const started = Date.now();
    const res = await fetch(this.base + path, { method, headers, signal: AbortSignal.timeout(timeout),
      body: body === undefined ? undefined : JSON.stringify(body) });
    this.store(res);
    const buffer = Buffer.from(await res.arrayBuffer());
    let json = null; if (!raw) { try { json = JSON.parse(buffer.toString("utf8")); } catch { /* not json */ } }
    return { status: res.status, json, buffer, headers: res.headers, ms: Date.now() - started, text: (res.headers.get("content-type") ?? "").includes("csv") ? "" : buffer.toString("utf8") };
  }
  get(p, o) { return this.req("GET", p, o); }
  mut(m, p, body) { return this.req(m, p, { body, csrf: true }); }
}
async function login(email, base = WEB) {
  const c = new Client(base); const r = await c.mut("POST", "/api/v1/auth/login", { email, password: PW });
  if (r.status !== 200) throw new Error(`login ${email} failed with ${r.status}`); return c;
}
const select = (c, org) => c.mut("PUT", "/api/v1/auth/session/organization", { organizationId: org });
const uid = email => sql(`select id from users where email='${email}'`);

// Raw TCP client for connection-level behaviour (no HTTP library cleanup).
function rawGet(port, path, cookie, { readBytes = null, pauseAfterHeaders = false, abortAfterMs = null, reset = true } = {}) {
  return new Promise(resolve => {
    const s = net.connect(port, "127.0.0.1"); const t0 = Date.now();
    let received = 0, head = "", status = 0, done = false; let headerEnd = -1;
    const finish = reason => { if (done) return; done = true; resolve({ received, status, ms: Date.now() - t0, reason, head }); };
    s.on("connect", () => s.write(`GET ${path} HTTP/1.1\r\nHost: localhost:${port}\r\nCookie: ${cookie}\r\nConnection: close\r\nAccept: text/csv\r\n\r\n`));
    s.on("data", d => {
      received += d.length;
      if (headerEnd < 0) { head += d.toString("latin1"); headerEnd = head.indexOf("\r\n\r\n");
        if (headerEnd >= 0) { status = Number(head.slice(9, 12)); if (pauseAfterHeaders) s.pause(); } }
      if (readBytes !== null && received >= readBytes) { reset ? s.resetAndDestroy() : s.destroy(); finish("aborted-after-bytes"); }
    });
    s.on("close", () => finish("closed")); s.on("error", () => finish("error"));
    if (abortAfterMs !== null) setTimeout(() => { if (!done) { reset ? s.resetAndDestroy() : s.destroy(); finish("aborted-after-time"); } }, abortAfterMs);
  });
}
// Background database lockers with a recognisable application name.
function locker(name, statement, seconds) {
  const p = spawn("docker", ["exec", "-e", `PGAPPNAME=${name}`, PG, "psql", "-X", "-U", "harborops", "-d", "harborops", "-v", "ON_ERROR_STOP=1", "-c",
    `BEGIN; ${statement}; SELECT pg_sleep(${seconds}); COMMIT;`], { stdio: "ignore", env: { ...process.env, MSYS_NO_PATHCONV: "1" } });
  return p;
}
const endLocker = name => sql(`select pg_terminate_backend(pid) from pg_stat_activity where application_name='${name}'`);
async function waitFor(fn, ms = 20000, step = 100) { const t = Date.now(); while (Date.now() - t < ms) { if (await fn()) return true; await sleep(step); } return false; }
const waiting = () => Number(sql("select count(*) from pg_stat_activity where usename='harborops_api' and wait_event_type='Lock'"));
const apiBackends = () => Number(sql("select count(*) from pg_stat_activity where usename='harborops_api'"));
const idleInTx = () => Number(sql("select count(*) from pg_stat_activity where usename='harborops_api' and state like 'idle in transaction%'"));
const audit = org => Number(sql(`select count(*) from audit_entries where organization_id='${org}' and event_type='shipment.export_prepared'`));

const ORG_SLUG = "m12-net";
const orgId = () => sql(`select id from organizations where slug='${ORG_SLUG}'`);
const ADMIN = "m12net-admin@example.test", MGR = "m12net-manager@example.test", ANA = "m12net-analyst@example.test";

// ---------------------------------------------------------------- prepare
async function prepare() {
  const org = orgId();
  const admin = await login(ADMIN); await select(admin, org);
  for (const [email, role] of [[MGR, "Manager"], [ANA, "Analyst"]]) {
    const r = await admin.mut("PUT", `/api/v1/organizations/${org}/memberships/${uid(email)}`, { role, status: "Active" });
    if (![200, 201].includes(r.status)) throw new Error(`membership ${email} failed ${r.status}`);
  }
  const mgr = await login(MGR); await select(mgr, org);
  const base = `/api/v1/organizations/${org}`;
  const port = async code => (await mgr.mut("POST", base + "/ports", { referenceCode: code, name: code, countryCode: "US" })).json.id;
  const a = await port("NET-PORT-A"), b = await port("NET-PORT-B");
  const s = await mgr.mut("POST", base + "/shipments", { referenceCode: "NET-TEMPLATE-1", originPortId: a, destinationPortId: b });
  if (s.status !== 201) throw new Error("template shipment failed " + s.status);
  record("P00", "prepared organization, accounts and one template shipment", 1, sql(`select count(*) from shipments where organization_id='${org}'`),
    sql(`select count(*) from shipments where organization_id='${org}'`) === "1");
}

// ---------------------------------------------------------------- load (after the G11 phase: the evaluator sweeps every shipment)
async function load() {
  const org = orgId();
  const s = { json: { id: sql(`select id from shipments where organization_id='${org}' and reference_code='NET-TEMPLATE-1'`) } };
  // 5,000 worst-case rows: 160 four-byte characters, 80-character external reference, 32-character code.
  const emoji = "\u{1F600}".repeat(160);
  sql(`insert into shipments (id,organization_id,reference_code,external_reference,display_name,origin_port_id,destination_port_id,
        workflow_status,record_status,source_kind,version,created_at_utc,updated_at_utc,changed_by_user_id)
       select gen_random_uuid(),t.organization_id,rpad('NETW'||lpad(n::text,5,'0'),32,'X'),rpad('NETW'||lpad(n::text,5,'0'),80,'Z'),
        '${emoji}',t.origin_port_id,t.destination_port_id,'Draft','Active','Synthetic',1,now(),now(),t.changed_by_user_id
       from shipments t cross join generate_series(1,5000) n where t.id='${s.json.id}'`);
  sql("analyze shipments");
  record("P01", "loaded 5,000 worst-case rows", 5001, sql(`select count(*) from shipments where organization_id='${org}'`),
    sql(`select count(*) from shipments where organization_id='${org}'`) === "5001");
}
// ---------------------------------------------------------------- g12 network path
async function g12() {
  const org = orgId(); const EXPORT = `/api/v1/organizations/${org}/exports/shipments?q=NETW`;
  const mgr = await login(MGR); await select(mgr, org);
  const waitAudit = async n => waitFor(() => audit(org) >= n, 5000);

  // N01 real Nginx-to-Kestrel forwarding.
  const viaProxy = await mgr.get(EXPORT, { raw: true });
  const direct = new Client(API); direct.jar = new Map(mgr.jar);
  const viaApi = await direct.get(EXPORT, { raw: true });
  const sha = b => createHash("sha256").update(b).digest("hex");
  record("N01", "export via Nginx status and Server header", "200 / nginx", `${viaProxy.status} / ${viaProxy.headers.get("server")}`,
    viaProxy.status === 200 && /nginx/i.test(viaProxy.headers.get("server") ?? ""));
  record("N01", "Nginx body is byte-identical to the direct Kestrel body", "equal", sha(viaProxy.buffer) === sha(viaApi.buffer) ? "equal" : "different",
    viaProxy.buffer.length === viaApi.buffer.length && sha(viaProxy.buffer) === sha(viaApi.buffer), `bytes=${viaProxy.buffer.length}`);
  record("N01", "worst-case 5,000-row body size under the 10 MiB limit", "< 10485760", viaProxy.buffer.length, viaProxy.buffer.length < 10485760);
  record("N01", "proxy preserves security headers", "text/csv, nosniff, no-store",
    `${viaProxy.headers.get("content-type")}, ${viaProxy.headers.get("x-content-type-options")}, ${viaProxy.headers.get("cache-control")}`,
    /text\/csv/.test(viaProxy.headers.get("content-type") ?? "") && /^nosniff(, nosniff)*$/.test(viaProxy.headers.get("x-content-type-options") ?? "") && /no-store/.test(viaProxy.headers.get("cache-control") ?? ""),
    "Nginx and Kestrel each add X-Content-Type-Options, so the value is repeated (benign)");
  record("N01", "two exports prepared two audits", 2, audit(org) , audit(org) >= 2, "includes direct request");

  if (on("N02")) {
  // N02 15-second PostgreSQL statement timeout through the proxy.
  const l1 = locker("m12lock-table", "LOCK TABLE shipments IN ACCESS EXCLUSIVE MODE", 40);
  await sleep(1500);
  const a0 = audit(org);
  const t2 = await mgr.get(EXPORT, { raw: true });
  endLocker("m12lock-table"); l1.kill();
  record("N02", "statement timeout through Nginx", "503 in 14-20 s", `${t2.status} in ${(t2.ms / 1000).toFixed(2)} s`, t2.status === 503 && within(t2.ms / 1000, 14, 20));
  record("N02", "no CSV bytes and no audit after statement timeout", "json / 0", `${t2.headers.get("content-type")} / ${audit(org) - a0}`,
    !/csv/.test(t2.headers.get("content-type") ?? "") && audit(org) === a0);
  await sleep(1000);
  }

  if (on("N03")) {
  // N03 25-second application budget (inside the 30-second proxy read timeout).
  const uidM = uid(MGR);
  const la = locker("m12lock-org", `SELECT 1 FROM organizations WHERE id='${org}' FOR UPDATE`, 12);
  const lb = locker("m12lock-member", `SELECT 1 FROM organization_memberships WHERE organization_id='${org}' AND user_id='${uidM}' FOR UPDATE`, 45);
  await sleep(1500);
  const t3 = await mgr.get(EXPORT, { raw: true, timeout: 40000 });
  endLocker("m12lock-org"); endLocker("m12lock-member"); la.kill(); lb.kill();
  record("N03", "25 second budget through Nginx", "503 in 24-27 s", `${t3.status} in ${(t3.ms / 1000).toFixed(2)} s`, t3.status === 503 && within(t3.ms / 1000, 24, 27));
  record("N03", "budget response names the request budget, not the proxy", "request budget", t3.text.includes("request budget") ? "request budget" : t3.text.slice(0, 80), t3.text.includes("request budget"));
  record("N03", "no audit after budget expiry", 0, audit(org) - a0, audit(org) === a0);
  await sleep(1000);
  }

  if (on("N04")) {
  // N04 30-second proxy read timeout: the request is in flight (blocked on a database lock) when the upstream stalls.
  const pre = audit(org);
  const lstall = locker("m12lock-stall", `SELECT 1 FROM organizations WHERE id='${org}' FOR UPDATE`, 60);
  await sleep(1500);
  const stalled = mgr.get(EXPORT, { raw: true, timeout: 70000 });
  await waitFor(() => waiting() >= 1, 5000);
  dk("pause", APIC);
  const t4 = await stalled;
  dk("unpause", APIC);
  endLocker("m12lock-stall"); lstall.kill();
  record("N04", "stalled upstream is cut by the 30 second proxy read timeout", "504 in 29-32 s", `${t4.status} in ${(t4.ms / 1000).toFixed(2)} s`, t4.status === 504 && within(t4.ms / 1000, 29, 32));
  record("N04", "client received no CSV", "no csv", t4.headers.get("content-type"), !/csv/.test(t4.headers.get("content-type") ?? ""));
  await sleep(30000);
  const post4 = audit(org) - pre;
  record("N04", "after resume the abandoned request is bounded by the 25 s budget: no audit", 0, post4, post4 === 0, "the budget had already expired while the process was frozen");
  const lease = await mgr.get(EXPORT, { raw: true });
  record("N04", "export lease released after the stalled request", 200, lease.status, lease.status === 200);
  record("N04", "no leaked transaction or waiting backend after the stall", "0 / 0", `${idleInTx()} / ${waiting()}`, idleInTx() === 0 && waiting() === 0);
  }

  // N05 CPU-bound deadline: throttle the API container and sweep the CPU share so the export work crosses the
  // 25 second application budget and the 30 second proxy envelope.
  for (const quota of (process.env.M11M12_CPU_QUOTAS ?? "3000").split(",")) {
    dk("update", "--cpu-period", "1000000", "--cpu-quota", quota, APIC); await sleep(1000);
    const before5 = audit(org);
    const t5 = await mgr.get(EXPORT, { raw: true, timeout: 90000 });
    cpuRestore();
    const dt = t5.ms / 1000;
    const complete = t5.status === 200 && t5.buffer.length === viaApi.buffer.length && sha(t5.buffer) === sha(viaApi.buffer);
    const noCsv = t5.status !== 200 && !/csv/.test(t5.headers.get("content-type") ?? "");
    record("N05", `CPU-bound export with ${quota} microseconds of CPU per second`, "complete CSV or no CSV, answered within 32 s", `${t5.status} in ${dt.toFixed(2)} s, bytes ${t5.buffer.length}`,
      (complete || noCsv) && dt <= 32,
      `${t5.status === 503 ? "internal budget expired" : t5.status === 200 ? "completed" : "proxy cut"}; ${dt > 25 ? `end-to-end exceeded the 25 s application budget by ${(dt - 25).toFixed(1)} s (pipeline work outside the store budget)` : "within the 25 s budget"}`);
    record("N05", "audit follows the outcome", "0 on 503 or 504, 0 or 1 on 200", audit(org) - before5,
      t5.status === 200 ? audit(org) - before5 === 1 : audit(org) - before5 <= 1, t5.status === 200 ? "" : "a request cut by the proxy may still finish preparation after the client left");
    await waitFor(async () => (await fetch(API + "/health/ready").then(r => r.status).catch(() => 0)) === 200, 60000, 500);
    await sleep(35000 > 0 && t5.status !== 200 ? 20000 : 500);
  }
  record("N05", "API responsive after the throttle is removed", 200, (await fetch(API + "/health/ready")).status, (await fetch(API + "/health/ready")).status === 200);

  // N06 TCP-level interruption while the CSV is streaming.
  const cookie = mgr.cookie(); const before6 = audit(org); const backendsBefore = apiBackends();
  const partial = await rawGet(5181, EXPORT, cookie, { pauseAfterHeaders: true, abortAfterMs: 4000 });
  record("N06", "client reset while the response is paused after headers", "status 200, aborted", `${partial.status} ${partial.reason}`, partial.status === 200 && partial.reason === "aborted-after-time", `bytesReceived=${partial.received}`);
  const partial2 = await rawGet(5181, EXPORT, cookie, { readBytes: 65536 });
  record("N06", "client reset after 64 KiB of the body", "partial < full", `${partial2.received} < ${viaProxy.buffer.length}`, partial2.received < viaProxy.buffer.length && partial2.status === 200);
  await sleep(3000);
  const after6 = await mgr.get(EXPORT, { raw: true });
  record("N06", "lease and connections cleaned after both interruptions", "200, no leaked tx", `${after6.status}, idleInTx=${idleInTx()}, backends ${backendsBefore}->${apiBackends()}`,
    after6.status === 200 && idleInTx() === 0 && apiBackends() <= backendsBefore + 1);
  record("N06", "one preparation audit per started export (documented), none extra", "3 or fewer new", audit(org) - before6, audit(org) - before6 <= 3, "partial deliveries still audit preparation");
  record("N06", "complete body still equal after the faults", "equal", sha(after6.buffer) === sha(viaApi.buffer) ? "equal" : "different", sha(after6.buffer) === sha(viaApi.buffer));

  // N07 disconnect while the request waits on the database: cancellation must reach PostgreSQL.
  const lo = locker("m12lock-org2", `SELECT 1 FROM organizations WHERE id='${org}' FOR UPDATE`, 40);
  await sleep(1500);
  const base7 = audit(org);
  const pending = rawGet(5181, EXPORT, cookie, { abortAfterMs: 6000 });
  const sawWaiter = await waitFor(() => waiting() >= 1, 5000);
  const result7 = await pending;
  const cleared = await waitFor(() => waiting() === 0, 8000, 200);
  record("N07", "request was blocked on the database before the client left", ">= 1 waiter", sawWaiter ? "waiter seen" : "none", sawWaiter);
  record("N07", "client disconnect cancels the database wait through Nginx and Kestrel while the lock is still held", "0 waiters", cleared ? 0 : waiting(), cleared, `reason=${result7.reason}`);
  endLocker("m12lock-org2"); lo.kill();
  await sleep(1000);
  record("N07", "no audit and no leaked transaction after cancellation", "0 / 0", `${audit(org) - base7} / ${idleInTx()}`, audit(org) === base7 && idleInTx() === 0);
  const again7 = await mgr.get(EXPORT, { raw: true });
  record("N07", "lease released after the cancelled request", 200, again7.status, again7.status === 200);

  // N08 revocation through the real path.
  const rev = await login(ANA); await select(rev, org);
  const lr = locker("m12lock-org3", `SELECT 1 FROM organizations WHERE id='${org}' FOR UPDATE`, 20);
  await sleep(1500);
  const base8 = audit(org);
  const inflight = rev.get(EXPORT, { raw: true, timeout: 40000 });
  await waitFor(() => waiting() >= 1, 5000);
  sql(`update auth_sessions set revoked_at_utc=now() where user_id='${uid(ANA)}' and revoked_at_utc is null`);
  endLocker("m12lock-org3"); lr.kill();
  const r8 = await inflight;
  record("N08", "session revoked while queued behind Nginx", "403, no CSV, no audit", `${r8.status}, ${r8.headers.get("content-type")}, ${audit(org) - base8}`,
    r8.status === 403 && !/csv/.test(r8.headers.get("content-type") ?? "") && audit(org) === base8);
}

// ---------------------------------------------------------------- g11 process and outage faults
async function g11() {
  const org = orgId();
  const mgr = await login(MGR); await select(mgr, org);
  const base = `/api/v1/organizations/${org}`;
  async function dueShipment(tag) {
    const ports = (await mgr.get(base + "/ports?limit=50")).json.ports;
    const s = await mgr.mut("POST", base + "/shipments", { referenceCode: tag, originPortId: ports[0].id, destinationPortId: ports[1].id });
    const id = s.json.id; const now = Date.now();
    const iso = h => new Date(now + h * 3600000).toISOString().replace(/\.\d+Z$/, "Z");
    let v = (await mgr.get(`${base}/shipments/${id}`)).json.version;
    await mgr.mut("PATCH", `${base}/shipments/${id}`, { expectedVersion: v, plannedDepartureAtUtc: iso(-30), plannedArrivalAtUtc: iso(-3) });
    v = (await mgr.get(`${base}/shipments/${id}`)).json.version;
    await mgr.mut("PATCH", `${base}/shipments/${id}`, { expectedVersion: v, workflowStatus: "Booked" });
    return id;
  }
  const counts = id => sql(`select (select count(*) from shipment_exceptions where shipment_id='${id}')||'/'||(select count(*) from shipment_exception_evaluations where shipment_id='${id}')||'/'||
    (select count(*) from shipment_exception_history h join shipment_exceptions e on e.id=h.exception_id where e.shipment_id='${id}' and h.action='Opened')`);
  // The hold fires when the evaluation transaction reaches the exception insert, after the earlier rules have written.
  // It is keyed by reference code and installed before the shipment exists, so no evaluation can race past it.
  const hold = tag => sql(`create or replace function m11_test_hold() returns trigger language plpgsql as $$ begin
      if exists (select 1 from shipments where id=new.shipment_id and reference_code=tg_argv[0]) then perform pg_sleep(25); end if; return new; end $$;
    drop trigger if exists m11_test_hold on shipment_exceptions;
    create trigger m11_test_hold before insert on shipment_exceptions for each row execute function m11_test_hold('${tag}')`);
  const unhold = () => sql("drop trigger if exists m11_test_hold on shipment_exceptions");
  const overdueState = id => sql(`select coalesce((select condition_state from shipment_exception_evaluations where shipment_id='${id}' and rule_code='ArrivalScheduleOverdue'),'none')`);
  const evaluatorBusy = id => Number(sql(`select count(*) from pg_stat_activity where usename='harborops_api' and state='active' and query ilike '%m11_apply_signed_evaluation%'`));
  const ready = async () => waitFor(async () => { try { return (await fetch(API + "/health/ready")).status === 200; } catch { return false; } }, 90000, 1000);
  const evaluatorOn = dk("exec", APIC, "printenv", "M11__EnableEvaluator");
  record("E00", "evaluator enabled in the stack for this phase", "true", evaluatorOn, evaluatorOn === "true");

  // E01 SIGKILL of the API process while the evaluation transaction is open (before commit).
  hold("NET-KILL-BEFORE");
  const s1 = await dueShipment("NET-KILL-BEFORE");
  const busy = await waitFor(() => evaluatorBusy(s1) >= 1, 60000, 200);
  record("E01", "evaluator transaction is open inside the signed function", ">= 1 active", busy ? "active" : "none", busy);
  dk("kill", "-s", "KILL", APIC);
  await sleep(30000); // longer than the 25 second hold: the abandoned backend must still not commit
  record("E01", "API killed before commit leaves no exception or history and the overdue rule is still not Active", "0 exceptions, 0 opened, Inactive",
    `${counts(s1)} / ${overdueState(s1)}`, counts(s1).startsWith("0/") && counts(s1).endsWith("/0") && overdueState(s1) !== "Active");
  unhold(); dk("start", APIC); record("E01", "API restarts ready", true, await ready(), await ready());
  const recovered = await waitFor(() => counts(s1) === "1/4/1", 90000, 1000);
  record("E01", "restarted evaluator completes exactly once", "1/4/1", counts(s1), recovered);

  // E02 SIGKILL immediately after the commit becomes visible.
  const s2 = await dueShipment("NET-KILL-AFTER");
  const committed = await waitFor(() => Number(sql(`select count(*) from shipment_exception_evaluations where shipment_id='${s2}'`)) === 4, 90000, 100);
  dk("kill", "-s", "KILL", APIC);
  record("E02", "commit observed, then API killed", "committed", committed ? "committed" : "not seen", committed);
  const stateAtKill = counts(s2);
  dk("start", APIC); await ready(); await sleep(40000);
  record("E02", "state is exactly one set after restart and 40 s of further scanning", "1/4/1", counts(s2), counts(s2) === "1/4/1" && stateAtKill === "1/4/1");

  // E03 PostgreSQL hard crash (SIGKILL) while an evaluation transaction is open.
  hold("NET-DB-CRASH");
  const s3 = await dueShipment("NET-DB-CRASH");
  await waitFor(() => evaluatorBusy(s3) >= 1, 60000, 200);
  dk("kill", "-s", "KILL", PG);
  await sleep(2000);
  const ro = await fetch(API + "/health/ready").then(r => r.status).catch(() => 0);
  record("E03", "API reports not ready while PostgreSQL is down", "!= 200", ro, ro !== 200);
  dk("start", PG);
  const dbUp = await waitFor(() => { try { sql("select 1"); return true; } catch { return false; } }, 90000, 1000);
  unhold();
  record("E03", "PostgreSQL crash recovery completes", true, dbUp, dbUp);
  await ready();
  const rec3 = await waitFor(() => counts(s3) === "1/4/1", 120000, 1000);
  record("E03", "after crash recovery the evaluation committed exactly once", "1/4/1", counts(s3), rec3);

  // E04 orderly PostgreSQL stop and start.
  hold("NET-DB-STOP");
  const s4 = await dueShipment("NET-DB-STOP");
  await waitFor(() => evaluatorBusy(s4) >= 1, 60000, 200);
  dk("stop", "-t", "5", PG);
  const down = await fetch(API + "/api/v1/auth/csrf").then(r => r.status).catch(() => 0);
  dk("start", PG);
  await waitFor(() => { try { sql("select 1"); return true; } catch { return false; } }, 90000, 1000);
  unhold(); await ready();
  const rec4 = await waitFor(() => counts(s4) === "1/4/1", 120000, 1000);
  record("E04", "orderly database stop and restart leaves one complete evaluation", "1/4/1", counts(s4), rec4, `csrf while down=${down}`);

  // E05 invariants across the whole run: no duplicates anywhere.
  record("E05", "no duplicate exceptions, histories or evaluations in the stack", "0 / 0 / 0",
    sql(`select (select count(*) from (select shipment_id,rule_code,occurrence from shipment_exceptions group by 1,2,3 having count(*)>1) x)||' / '||
      (select count(*) from (select exception_id,action,source_fingerprint from shipment_exception_history group by 1,2,3 having count(*)>1) y)||' / '||
      (select count(*) from (select shipment_id,rule_code from shipment_exception_evaluations group by 1,2 having count(*)>1) z)`),
    sql(`select (select count(*) from (select shipment_id,rule_code,occurrence from shipment_exceptions group by 1,2,3 having count(*)>1) x)||' / '||
      (select count(*) from (select exception_id,action,source_fingerprint from shipment_exception_history group by 1,2,3 having count(*)>1) y)||' / '||
      (select count(*) from (select shipment_id,rule_code from shipment_exception_evaluations group by 1,2 having count(*)>1) z)`) === "0 / 0 / 0");
}

try {
  if (phase === "prepare") await prepare(); else if (phase === "load") await load(); else if (phase === "g12") await g12(); else if (phase === "g11") await g11(); else throw new Error("unknown phase");
} catch (error) { record("ERR", "unhandled error", "none", String(error).slice(0, 300), false); }
writeFileSync(out, JSON.stringify({ phase, project: PROJECT, results }, null, 2));
const failed = results.filter(r => !r.pass).length;
console.log(`${results.length - failed}/${results.length} checks passed`);
process.exit(failed ? 1 : 0);
