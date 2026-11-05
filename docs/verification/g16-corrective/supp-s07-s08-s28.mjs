// Supplemental G16 probe on the final source revision (not a runner stage): S07, S08 and S28 operator identity.
// Env: G16_API_BASE, G16_PSQL, G16_COMPOSE, HARBOROPS_TEST_USER_PASSWORD. Usage: node supp-s07-s08-s28.mjs <out.json>
import { execFileSync } from "node:child_process";
import { writeFileSync } from "node:fs";
const BASE = process.env.G16_API_BASE ?? "http://localhost:8086", PW = process.env.HARBOROPS_TEST_USER_PASSWORD;
const PSQL = process.env.G16_PSQL.split(" "), COMPOSE = process.env.G16_COMPOSE.split(" ");
const sql = q => execFileSync(PSQL[0], [...PSQL.slice(1), "-c", q], { encoding: "utf8" }).trim();
const sleep = ms => new Promise(r => setTimeout(r, ms));
const out = [];
const rec = (id, name, expected, actual, pass) => { out.push({ id, name, expected: String(expected), actual: String(actual), pass: Boolean(pass) }); console.log(`${pass ? "PASS" : "FAIL"} ${id} ${name} expected=${expected} actual=${actual}`); };
class C {
  jar = new Map();
  store(r) { for (const c of r.headers.getSetCookie?.() ?? []) { const [p] = c.split(";"); const i = p.indexOf("="); const k = p.slice(0, i), v = p.slice(i + 1); if (v === "" || /max-age=0|1970/i.test(c)) this.jar.delete(k); else this.jar.set(k, v); } }
  cookie() { return [...this.jar].map(([k, v]) => `${k}=${v}`).join("; "); }
  async req(method, path, body) {
    const h = { cookie: this.cookie(), origin: BASE };
    const r0 = await fetch(BASE + "/api/v1/auth/csrf", { headers: { cookie: this.cookie() } }); this.store(r0); h["x-harborops-csrf"] = (await r0.json()).token; h.cookie = this.cookie();
    if (body !== undefined) h["content-type"] = "application/json";
    const res = await fetch(BASE + path, { method, headers: h, body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(15000) });
    this.store(res); const t = await res.text(); let j = null; try { j = JSON.parse(t); } catch { } return { status: res.status, json: j };
  }
}
const login = async n => { const c = new C(); const r = await c.req("POST", "/api/v1/auth/login", { email: `${n}@example.test`, password: PW }); if (r.status !== 200) throw Error("login " + n + " " + r.status); return c; };
const A = sql("select id from organizations where slug='g16h-a'");
const admin = await login("g16h-admin"), ana = await login("g16h-ana");
await admin.req("PUT", "/api/v1/auth/session/organization", { organizationId: A });
await ana.req("PUT", "/api/v1/auth/session/organization", { organizationId: A });
const upd = () => Number(sql(`select count(*) from audit_entries where event_type='organizations.update' and organization_id='${A}'`));
const ver = () => Number(sql(`select version from organizations where id='${A}'`));
const all = () => Number(sql("select count(*) from audit_entries"));
const patch = (c, v, n) => c.req("PATCH", `/api/v1/organizations/${A}`, { displayName: n, expectedVersion: v });

{ const a0 = all(), v0 = ver();
  const stale = await patch(admin, v0 + 5, "Stale"), empty = await patch(admin, v0, ""), forb = await patch(ana, v0, "Forbidden");
  const bad = await admin.req("PATCH", "/api/v1/organizations/not-a-guid", { displayName: "x", expectedVersion: 1 });
  rec("S07", `rejected mutations stale=${stale.status} empty=${empty.status} forbidden=${forb.status} badId=${bad.status}`, "4xx and no audit rows, version unchanged",
    `audit +${all() - a0}, version ${v0}->${ver()}`, all() === a0 && ver() === v0 && [stale, empty, forb, bad].every(r => r.status >= 400 && r.status < 500)); }
{ const u0 = upd(), v0 = ver(); let wins = 0, other = 0;
  for (let round = 0; round < 10; round++) {
    const v = ver(); const rs = await Promise.all(Array.from({ length: 8 }, (_, i) => patch(admin, v, `R${round}W${i}`)));
    for (const r of rs) { if (r.status === 200) wins++; else if (r.status !== 409) other++; }
  }
  rec("S08", `10 rounds x 8 parallel same-version updates (wins=${wins}, other=${other})`, "10 wins, 0 other", `${wins} wins, ${other} other`, wins === 10 && other === 0);
  rec("S08", "audit rows equal committed organization updates", ver() - v0, upd() - u0, upd() - u0 === ver() - v0); }
{ const u0 = upd(), v0 = ver(); let stop = false;
  const worker = async n => { while (!stop) { try { await patch(admin, ver(), `Kill${n}-${Date.now()}`); } catch { await sleep(50); } } };
  const ws = [1, 2, 3, 4].map(worker);
  await sleep(2500); execFileSync(COMPOSE[0], [...COMPOSE.slice(1), "kill", "api"]); await sleep(1500); stop = true; await Promise.allSettled(ws);
  execFileSync(COMPOSE[0], [...COMPOSE.slice(1), "start", "api"]);
  rec("S08", "API process killed mid-burst: committed updates equal audit rows", "equal and > 0", `versions +${ver() - v0}, audit +${upd() - u0}`, ver() - v0 === upd() - u0 && ver() - v0 > 0); }
{ const rows = sql("select string_agg(distinct actor_kind||':'||event_type, ',' order by actor_kind||':'||event_type) from audit_entries where event_type in ('users.insert','user_credentials.insert','organizations.insert','organization_memberships.insert') and actor_kind='OperatorCli'");
  rec("S28", "operator CLI provisioning and bootstrap are attributed to OperatorCli", "includes users.insert and organizations.insert", rows, /users\.insert/.test(rows) && /organizations\.insert/.test(rows));
  const bad = sql("select count(*) from audit_entries where actor_kind='OperatorCli' and (actor_user_id is not null or actor_session_id is not null)");
  rec("S28", "OperatorCli rows carry no human user or session", 0, bad, Number(bad) === 0); }
writeFileSync(process.argv[2], JSON.stringify(out, null, 1));
console.log(`SUMMARY pass=${out.filter(x => x.pass).length} fail=${out.filter(x => !x.pass).length}`);
process.exit(out.some(x => !x.pass) ? 1 : 0);
