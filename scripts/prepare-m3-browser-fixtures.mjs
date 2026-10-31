import assert from "node:assert/strict";

const base = "http://localhost:5173";
const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
if (!password) throw new Error("Disposable M3 password is required");
const users = {
  a: "m3-admin-a@example.test",
  b: "m3-admin-b@example.test",
  manager: "m3-manager@example.test",
  analyst: "m3-analyst@example.test"
};

class Session {
  cookies = new Map();

  async call(method, path, data) {
    const headers = { Origin: base };
    if (this.cookies.size)
      headers.Cookie = [...this.cookies].map(([name, value]) => name + "=" + value).join("; ");
    if (data !== undefined) headers["Content-Type"] = "application/json";
    if (method !== "GET") {
      const csrf = await this.call("GET", "/api/v1/auth/csrf");
      headers["X-HarborOps-CSRF"] = csrf.token;
      if (this.cookies.size)
        headers.Cookie = [...this.cookies].map(([name, value]) => name + "=" + value).join("; ");
    }
    const response = await fetch(base + path, {
      method, headers, ...(data !== undefined ? { body: JSON.stringify(data) } : {})
    });
    for (const value of response.headers.getSetCookie()) {
      const match = value.match(/^([^=;]+)=([^;]*)/);
      if (match) this.cookies.set(match[1], match[2]);
    }
    const text = await response.text();
    let body;
    try { body = text.length ? JSON.parse(text) : null; }
    catch { body = text; }
    if (!response.ok)
      throw new Error(method + " " + path + ": " + response.status + " " +
        JSON.stringify(body).slice(0, 200));
    return body;
  }

  async login(email) {
    await this.call("POST", "/api/v1/auth/login", { email, password });
    return (await this.call("GET", "/api/v1/auth/session")).user.id;
  }

  async select(id) {
    await this.call("PUT", "/api/v1/auth/session/organization", { organizationId: id });
  }

  async organizations() {
    return (await this.call("GET", "/api/v1/me/organizations")).organizations;
  }
}

const manager = new Session();
const analyst = new Session();
const managerId = await manager.login(users.manager);
const analystId = await analyst.login(users.analyst);
for (const [ownerEmail, slug] of [
  [users.a, "m3-north"],
  [users.b, "m3-east"]
]) {
  const owner = new Session();
  await owner.login(ownerEmail);
  const row = (await owner.organizations()).find(o => o.slug === slug);
  assert.ok(row, "Missing isolated M3 organization " + slug);
  await owner.select(row.id);
  const root = "/api/v1/organizations/" + row.id;
  const roster = (await owner.call("GET", root + "/memberships?limit=100")).memberships;
  async function ensureMember(userId, role) {
    const existing = roster.find(m => m.userId === userId);
    if (existing?.status === "Active" && existing.role === role) return;
    await owner.call("PUT", root + "/memberships/" + userId,
      { role, status: "Active", expectedVersion: existing?.version ?? null });
  }
  await ensureMember(managerId, "Manager");
  if (slug === "m3-north") await ensureMember(analystId, "Analyst");
}
const managerOrgs = await manager.organizations();
assert.ok(managerOrgs.some(o => o.slug === "m3-north" && o.role === "Manager"));
assert.ok(managerOrgs.some(o => o.slug === "m3-east" && o.role === "Manager"));
const analystOrgs = await analyst.organizations();
assert.ok(analystOrgs.some(o => o.slug === "m3-north" && o.role === "Analyst"));
assert.ok(!analystOrgs.some(o => o.slug === "m3-east"));
const expected = [
  ["ports", [
    { referenceCode: "DEMO-PORT-1", name: "Synthetic Primary Port", countryCode: "US", unLocode: "USNYC" },
    { referenceCode: "DEMO-PORT-2", name: "Synthetic Secondary Port", countryCode: "GB" },
    { referenceCode: "DEMO-PORT-3", name: "Synthetic Archived Port", countryCode: "BD", archived: true }
  ]],
  ["vessels", [
    { referenceCode: "DEMO-SHIP-1", name: "Synthetic IMO Vessel", imoNumber: "9074729" },
    { referenceCode: "DEMO-SHIP-2", name: "Synthetic MMSI Vessel", mmsi: "012345678" },
    { referenceCode: "DEMO-SHIP-3", name: "Synthetic Archived Vessel", archived: true }
  ]]
];
async function ensureRecords(organizationId) {
  await manager.select(organizationId);
  const snapshot = {};
  for (const [kind, definitions] of expected) {
    const path = "/api/v1/organizations/" + organizationId + "/" + kind;
    let rows = (await manager.call("GET", path + "?status=All&limit=100"))[kind];
    for (const definition of definitions) {
      let row = rows.find(value => value.referenceCode === definition.referenceCode);
      if (!row) {
        const { archived, ...fields } = definition;
        const suffix = managerOrgs.find(o => o.id === organizationId).slug === "m3-north" ? "North" : "East";
        row = await manager.call("POST", path, { ...fields, name: fields.name + " " + suffix });
        if (archived) {
          await manager.call("DELETE", path + "/" + row.id, { expectedVersion: row.version });
        }
      }
    }
    rows = (await manager.call("GET", path + "?status=All&limit=100"))[kind];
    for (const definition of definitions) {
      const matches = rows.filter(row => row.referenceCode === definition.referenceCode);
      assert.equal(matches.length, 1, kind + " reference fixture must be unique");
      assert.equal(matches[0].status, definition.archived ? "Archived" : "Active");
    }
    snapshot[kind] = definitions.map(definition => {
      const row = rows.find(value => value.referenceCode === definition.referenceCode);
      return [row.id, row.referenceCode, row.version, row.status];
    });
  }
  return snapshot;
}
for (const slug of ["m3-north", "m3-east"]) {
  const org = managerOrgs.find(o => o.slug === slug);
  const first = await ensureRecords(org.id);
  const second = await ensureRecords(org.id);
  assert.deepEqual(second, first, "Repeated fixture preparation must not mutate records");
}
console.log("M3 deterministic reference fixtures, role memberships and two-organization isolation: PASS");
