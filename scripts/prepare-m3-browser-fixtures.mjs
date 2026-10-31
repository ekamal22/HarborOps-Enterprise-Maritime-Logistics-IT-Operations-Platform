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
  await owner.call("PUT", "/api/v1/organizations/" + row.id + "/memberships/" + managerId,
    { role: "Manager", status: "Active", expectedVersion: null });
  if (slug === "m3-north")
    await owner.call("PUT", "/api/v1/organizations/" + row.id + "/memberships/" + analystId,
      { role: "Analyst", status: "Active", expectedVersion: null });
}
const managerOrgs = await manager.organizations();
assert.ok(managerOrgs.some(o => o.slug === "m3-north" && o.role === "Manager"));
assert.ok(managerOrgs.some(o => o.slug === "m3-east" && o.role === "Manager"));
const analystOrgs = await analyst.organizations();
assert.ok(analystOrgs.some(o => o.slug === "m3-north" && o.role === "Analyst"));
assert.ok(!analystOrgs.some(o => o.slug === "m3-east"));
console.log("M3 browser fixture roles and two-organization isolation: PASS");
