import assert from "node:assert/strict";

const base = "http://localhost:5173";
const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
assert.ok(password && password.length >= 20, "Disposable M5 credential required");
class Session {
  jar = new Map();
  async call(method, path, data) {
    const headers = { Origin: base };
    if (this.jar.size) headers.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
    if (method !== "GET") {
      const csrf = await this.call("GET", "/api/v1/auth/csrf");
      headers["X-HarborOps-CSRF"] = csrf.token;
      headers.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
    }
    if (data !== undefined) headers["Content-Type"] = "application/json";
    const response = await fetch(base + path, { method, headers,
      ...(data === undefined ? {} : { body: JSON.stringify(data) }) });
    for (const cookie of response.headers.getSetCookie()) {
      const match = cookie.match(/^([^=;]+)=([^;]*)/);
      if (match) this.jar.set(match[1], match[2]);
    }
    const raw = await response.text();
    const payload = raw ? JSON.parse(raw) : null;
    if (!response.ok) throw new Error(method + " " + path + " returned " + response.status +
      " " + JSON.stringify(payload).slice(0, 140));
    return payload;
  }
}
const manager = new Session();
await manager.call("POST", "/api/v1/auth/login", { email: "m3-manager@example.test", password });
const orgs = (await manager.call("GET", "/api/v1/me/organizations")).organizations;
for (const slug of ["m3-north", "m3-east"]) {
  const org = orgs.find(x => x.slug === slug && x.role === "Manager");
  assert.ok(org, "M5 tenant fixture missing: " + slug);
  await manager.call("PUT", "/api/v1/auth/session/organization", { organizationId: org.id });
  const root = "/api/v1/organizations/" + org.id;
  const ports = (await manager.call("GET", root + "/ports?status=Active&limit=100")).ports;
  const from = ports.find(x => x.referenceCode === "DEMO-PORT-1");
  const to = ports.find(x => x.referenceCode === "DEMO-PORT-2");
  assert.ok(from && to && from.id !== to.id, "M3 accepted parent ports missing");
  const code = slug === "m3-north" ? "M5-DEMO-NORTH" : "M5-DEMO-EAST";
  async function ensureShipment() {
    const page = await manager.call("GET", root + "/shipments?recordStatus=All&limit=100&q=" + code);
    let row = page.shipments.find(x => x.referenceCode === code);
    if (!row) row = await manager.call("POST", root + "/shipments", {
      referenceCode: code, externalReference: "M5/" + slug.toUpperCase(),
      displayName: "Synthetic " + slug + " shipment",
      originPortId: from.id, destinationPortId: to.id
    });
    assert.equal(row.organizationId, org.id);
    assert.equal(row.originPortId, from.id);
    assert.equal(row.destinationPortId, to.id);
    assert.equal(row.workflowStatus, "Draft");
    assert.equal(row.recordStatus, "Active");
    const rows = (await manager.call("GET", root + "/shipments?recordStatus=All&limit=100&q=" + code))
      .shipments.filter(x => x.referenceCode === code);
    assert.equal(rows.length, 1, "Shipment fixture duplicate");
    return [rows[0].id, rows[0].version, rows[0].referenceCode];
  }
  const first = await ensureShipment();
  const repeated = await ensureShipment();
  assert.deepEqual(repeated, first, "M5 fixture rerun changed identity or version");
}
console.log("M5 two-tenant shipment fixtures are stable");
