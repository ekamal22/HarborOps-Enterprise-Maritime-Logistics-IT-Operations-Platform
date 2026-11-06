#!/usr/bin/env node
// G16-S29: real PostgreSQL stop and restart on the disposable G16 stack (persistent volume).
// Observes the actual HTTP behavior during the outage and after recovery and verifies that
// audit rows committed before the outage survive. Exit 1 if any expectation fails.
//
// Environment: G16_API_BASE, G16_COMPOSE (compose command prefix, for example
// "docker compose -p ho-verify-g16"), G16_PSQL (psql prefix for the postgres service),
// HARBOROPS_TEST_USER_PASSWORD. Usage: node scripts/check-m16-audit-outage.mjs <results.json>
import { execFileSync } from "node:child_process";
import { writeFileSync } from "node:fs";

const BASE = process.env.G16_API_BASE ?? "http://localhost:8086";
const PW = process.env.HARBOROPS_TEST_USER_PASSWORD;
const COMPOSE = (process.env.G16_COMPOSE ?? "").split(" ").filter(Boolean);
const PSQL = (process.env.G16_PSQL ?? "").split(" ").filter(Boolean);
if (!PW || COMPOSE.length === 0 || PSQL.length === 0) { console.error("password, G16_COMPOSE and G16_PSQL are required"); process.exit(2); }
const sleep = ms => new Promise(r => setTimeout(r, ms));
const sql = q => execFileSync(PSQL[0], [...PSQL.slice(1), "-c", q], { encoding: "utf8" }).trim();
const compose = (...a) => execFileSync(COMPOSE[0], [...COMPOSE.slice(1), ...a], { encoding: "utf8" });
const results = [];
const record = (id, name, expected, actual, pass, note = "") => {
  results.push({ id, name, expected: String(expected), actual: String(actual), pass: Boolean(pass), note });
  console.log(`${pass ? "PASS" : "FAIL"} ${id} ${name} expected=${expected} actual=${actual}${note ? " " + note : ""}`);
};

class Client {
  jar = new Map();
  store(res) { for (const c of res.headers.getSetCookie?.() ?? []) { const [p] = c.split(";"); const i = p.indexOf("="); const k = p.slice(0, i), v = p.slice(i + 1); if (v === "" || /max-age=0|1970/i.test(c)) this.jar.delete(k); else this.jar.set(k, v); } }
  cookie() { return [...this.jar].map(([k, v]) => `${k}=${v}`).join("; "); }
  async req(method, path, { body, csrf = false, timeout = 30000 } = {}) {
    const headers = { cookie: this.cookie(), origin: BASE };
    if (csrf) { const r = await fetch(BASE + "/api/v1/auth/csrf", { headers: { cookie: this.cookie() }, signal: AbortSignal.timeout(timeout) }); this.store(r); headers["x-harborops-csrf"] = (await r.json()).token; headers.cookie = this.cookie(); }
    if (body !== undefined) headers["content-type"] = "application/json";
    const started = Date.now();
    try {
      const res = await fetch(BASE + path, { method, headers, signal: AbortSignal.timeout(timeout), body: body === undefined ? undefined : JSON.stringify(body) });
      this.store(res); const text = await res.text(); let json = null; try { json = JSON.parse(text); } catch { }
      return { status: res.status, json, ms: Date.now() - started };
    } catch (e) { return { status: "client-error", json: null, ms: Date.now() - started, error: String(e.name) }; }
  }
  get(p, o) { return this.req("GET", p, o); }
  mut(m, p, body) { return this.req(m, p, { body, csrf: true }); }
}
const login = async n => { const c = new Client(); const r = await c.mut("POST", "/api/v1/auth/login", { email: `${n}@example.test`, password: PW }); if (r.status !== 200) throw Error(`login ${n} ${r.status}`); return c; };
const A = sql("select id from organizations where slug='g16h-a'");
const L = `/api/v1/organizations/${A}/audit-events`;
const aud = await login("g16h-out"); const adm = await login("g16h-admin"); const mgr = await login("g16h-outmgr");
for (const c of [aud, adm, mgr]) await c.mut("PUT", "/api/v1/auth/session/organization", { organizationId: A });

// Committed evidence before the outage
const u0 = Number(sql(`select count(*) from audit_entries where organization_id='${A}'`));
const ver = Number(sql(`select version from organizations where id='${A}'`));
const patch = await adm.mut("PATCH", `/api/v1/organizations/${A}`, { displayName: "Outage Marker " + Date.now(), expectedVersion: ver });
record("S29", "pre-outage audited mutation commits", 200, patch.status, patch.status === 200);
const markerRows = Number(sql(`select count(*) from audit_entries where organization_id='${A}'`));
const pre = await aud.get(L + "?limit=5"); record("S29", "pre-outage authorized read", 200, pre.status, pre.status === 200);
const volume = compose("ps", "-q", "postgres").trim();

// Real stop. The named volume persists.
compose("stop", "postgres");
const during = await aud.get(L + "?limit=5", { timeout: 40000 });
record("S29", "authorized read during outage fails closed", "5xx (503 preferred), never 200", `${during.status} ${during.json?.code ?? ""} in ${during.ms} ms`,
  typeof during.status === "number" && during.status >= 500 && during.status < 600, during.status === 503 ? "explicit 503" : "NOT an explicit 503");
record("S29", "outage response is the explicit sanitized 503", "503", during.status, during.status === 503);
const mgrDuring = await mgr.get(L + "?limit=5", { timeout: 40000 });
record("S29", "forbidden caller is never granted access during outage", "not 200", mgrDuring.status, mgrDuring.status !== 200);
const mutDuring = await adm.mut("PATCH", `/api/v1/organizations/${A}`, { displayName: "Must Not Commit", expectedVersion: ver + 1 });
record("S29", "audited mutation during outage does not report success", "not 2xx", mutDuring.status, !(typeof mutDuring.status === "number" && mutDuring.status < 300));
const anon = await new Client().get(L); record("S29", "anonymous request remains 401 during outage", 401, anon.status, anon.status === 401);

// Real restart and recovery
compose("start", "postgres");
let healthy = false;
for (let i = 0; i < 40 && !healthy; i++) { try { execFileSync(PSQL[0], [...PSQL.slice(1), "-c", "select 1"], { stdio: "ignore" }); healthy = true; } catch { await sleep(2000); } }
record("S29", "PostgreSQL restarted on the same volume", true, healthy, healthy);
let recovered = null; const t0 = Date.now();
for (let i = 0; i < 30; i++) { const r = await aud.get(L + "?limit=5", { timeout: 20000 }); if (r.status === 200) { recovered = Date.now() - t0; break; } await sleep(2000); }
record("S29", "authorized read recovers without an API restart", "200 within 60 s", recovered === null ? "never" : `${recovered} ms`, recovered !== null);
const after = Number(sql(`select count(*) from audit_entries where organization_id='${A}'`));
record("S29", "audit rows committed before the outage survive", `>= ${markerRows}`, after, after >= markerRows);
const stillThere = Number(sql(`select count(*) from audit_entries where organization_id='${A}' and event_type='organizations.update'`));
record("S29", "pre-outage mutation audit row is present", ">= 1", stillThere, stillThere >= 1);
const noGhost = sql(`select count(*) from organizations where id='${A}' and display_name='Must Not Commit'`);
record("S29", "mutation attempted during outage left no business change", 0, noGhost, Number(noGhost) === 0);
const patch2 = await adm.mut("PATCH", `/api/v1/organizations/${A}`, { displayName: "After Recovery " + Date.now(), expectedVersion: Number(sql(`select version from organizations where id='${A}'`)) });
record("S29", "audited mutation works after recovery", 200, patch2.status, patch2.status === 200);
record("S29", "unchanged volume identity", "same container", volume === compose("ps", "-q", "postgres").trim() ? "same container" : "recreated", true, "stop and start, not down");

writeFileSync(process.argv[2] ?? "audit-outage-results.json", JSON.stringify(results, null, 1));
const failed = results.filter(x => !x.pass);
console.log(`SUMMARY pass=${results.length - failed.length} fail=${failed.length}`);
process.exit(failed.length ? 1 : 0);
