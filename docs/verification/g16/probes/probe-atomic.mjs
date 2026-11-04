// G16 S07 and S08 probe: audit/business atomic pairing under validation failure,
// concurrency and process interruption. Disposable DB and API only.
import { readFileSync, writeFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
const BASE = "http://127.0.0.1:18086", ORIGIN = BASE;
const PW = readFileSync(process.env.PROBE_USER_PW_FILE, "utf8").trim();
const out = [];
const sql = q => execFileSync("docker", ["exec", "-i", "ho-probe-pg", "psql", "-X", "-U", "harborops", "-d", "harborops", "-Atq", "-c", q], { encoding: "utf8" }).trim();
const rec = (id, name, expected, actual, pass) => { out.push({ id, name, expected, actual, pass }); console.log(`${pass ? "PASS" : "FAIL"} ${id} ${name} expected=${expected} actual=${actual}`); };
const sleep = ms => new Promise(r => setTimeout(r, ms));
class Client {
  jar = new Map();
  store(res) { for (const c of res.headers.getSetCookie?.() ?? []) { const [p] = c.split(";"); const i = p.indexOf("="); const k = p.slice(0, i), v = p.slice(i + 1); if (v === "" || /max-age=0|1970/i.test(c)) this.jar.delete(k); else this.jar.set(k, v); } }
  cookie() { return [...this.jar].map(([k, v]) => `${k}=${v}`).join("; "); }
  async req(method, path, body, csrf = true) {
    const h = { cookie: this.cookie(), origin: ORIGIN };
    if (csrf) { const r = await fetch(BASE + "/api/v1/auth/csrf", { headers: { cookie: this.cookie() } }); this.store(r); h["x-harborops-csrf"] = (await r.json()).token; h.cookie = this.cookie(); }
    if (body !== undefined) h["content-type"] = "application/json";
    const res = await fetch(BASE + path, { method, headers: h, body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(15000) });
    this.store(res); const t = await res.text(); let j = null; try { j = JSON.parse(t); } catch { }
    return { status: res.status, json: j };
  }
}
const login = async e => { const c = new Client(); const r = await c.req("POST", "/api/v1/auth/login", { email: e, password: PW }); if (r.status !== 200) throw Error("login " + r.status); return c; };
const A = sql("select id from organizations where slug='probe-org'");
const admin = await login("probe-admin@example.test"), ana = await login("probe-ana@example.test");
await admin.req("PUT", "/api/v1/auth/session/organization", { organizationId: A });
{ const u = sql("select id from users where email='probe-ana@example.test'");
  await admin.req("PUT", `/api/v1/organizations/${A}/memberships/${u}`, { role: "Analyst", status: "Active" });
  await ana.req("PUT", "/api/v1/auth/session/organization", { organizationId: A }); }
const upd = () => Number(sql(`select count(*) from audit_entries where event_type='organizations.update' and organization_id='${A}'`));
const ver = () => Number(sql(`select version from organizations where id='${A}'`));
const allAudit = () => Number(sql("select count(*) from audit_entries"));
const patch = (c, v, name) => c.req("PATCH", `/api/v1/organizations/${A}`, { displayName: name, expectedVersion: v });

// S07: rejected or invalid requests leave no business change and no success audit row
{ const a0 = allAudit(), v0 = ver();
  const stale = await patch(admin, v0 + 5, "Stale Version");
  const empty = await patch(admin, v0, "");
  const forb = await patch(ana, v0, "Analyst Attempt");
  const bad = await admin.req("PATCH", `/api/v1/organizations/not-a-guid`, { displayName: "x", expectedVersion: 1 });
  const a1 = allAudit();
  rec("S07", `rejected mutations stale=${stale.status} empty=${empty.status} forbidden=${forb.status} badId=${bad.status}`, "4xx and no audit rows", `audit rows +${a1 - a0}, version ${v0}->${ver()}`, a1 === a0 && ver() === v0 && [stale, empty, forb, bad].every(r => r.status >= 400 && r.status < 500)); }

// S08a: contended optimistic updates. Exactly one winner per version; audit rows equal committed updates
{ const u0 = upd(), v0 = ver(); let wins = 0, conflicts = 0, other = 0;
  for (let round = 0; round < 10; round++) {
    const v = ver();
    const rs = await Promise.all(Array.from({ length: 8 }, (_, i) => patch(admin, v, `Round ${round} Writer ${i}`)));
    for (const r of rs) { if (r.status === 200) wins++; else if (r.status === 409) conflicts++; else other++; }
  }
  rec("S08", `10 rounds x 8 parallel same-version updates: wins=${wins} conflicts=${conflicts} other=${other}`, "wins=10, no 5xx", `wins=${wins} other=${other}`, wins === 10 && other === 0);
  rec("S08", "audit rows equal committed organization updates", ver() - v0, upd() - u0, upd() - u0 === ver() - v0 && wins === ver() - v0); }

// S08b: kill the API process mid-burst; every committed update has its audit row and vice versa
{ const u0 = upd(), v0 = ver(); let ok = 0, fail = 0; let stop = false;
  const worker = async n => { while (!stop) { try { const v = ver(); const r = await patch(admin, v, `Kill ${n} ${Date.now()}`); if (r.status === 200) ok++; else fail++; } catch { fail++; await sleep(50); } } };
  const ws = [worker(1), worker(2), worker(3), worker(4)];
  await sleep(2500); execFileSync("docker", ["kill", "ho-probe-api"]); await sleep(1500); stop = true; await Promise.allSettled(ws);
  const dv = ver() - v0, du = upd() - u0;
  rec("S08", `API killed mid-burst (client saw ok=${ok} fail=${fail})`, "committed updates == audit rows", `versions +${dv}, audit +${du}`, dv === du && dv > 0);
  const orphan = sql(`select count(*) from audit_entries a where a.event_type='organizations.update' and a.organization_id='${A}' and a.occurred_at_utc > now() - interval '5 minutes'`);
  rec("S08", "no audit row without a committed change (same-transaction pairing)", "equal", `${dv} vs ${du}`, dv === du); }
writeFileSync(process.argv[2], JSON.stringify(out, null, 1));
console.log(`SUMMARY pass=${out.filter(x => x.pass).length} fail=${out.filter(x => !x.pass).length}`);
