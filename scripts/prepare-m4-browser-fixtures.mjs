import assert from "node:assert/strict";

const base = "http://localhost:5173";
const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
assert.ok(password && password.length >= 20, "Disposable M4 credential is required");
class Session {
  jar = new Map();
  async request(method, path, data) {
    const headers = { Origin: base };
    if (this.jar.size)
      headers.Cookie = [...this.jar].map(([k,v]) => k + "=" + v).join("; ");
    if (method !== "GET") {
      const csrf = await this.request("GET", "/api/v1/auth/csrf");
      headers["X-HarborOps-CSRF"] = csrf.token;
      headers.Cookie = [...this.jar].map(([k,v]) => k + "=" + v).join("; ");
    }
    if (data !== undefined) headers["Content-Type"] = "application/json";
    const response = await fetch(base + path, {
      method, headers, ...(data === undefined ? {} : { body: JSON.stringify(data) })
    });
    for (const cookie of response.headers.getSetCookie()) {
      const match = cookie.match(/^([^=;]+)=([^;]*)/);
      if (match) this.jar.set(match[1], match[2]);
    }
    const raw = await response.text();
    const body = raw ? JSON.parse(raw) : null;
    if (!response.ok)
      throw new Error(method + " " + path + " returned " + response.status +
        " " + JSON.stringify(body).slice(0,150));
    return body;
  }
  async login(email) {
    await this.request("POST", "/api/v1/auth/login", { email, password });
  }
  async choose(id) {
    await this.request("PUT", "/api/v1/auth/session/organization", { organizationId: id });
  }
}
const manager = new Session();
await manager.login("m3-manager@example.test");
const orgs = (await manager.request("GET", "/api/v1/me/organizations")).organizations;
for (const slug of ["m3-north","m3-east"]) {
  const org = orgs.find(x => x.slug === slug && x.role === "Manager");
  assert.ok(org, "Missing M4 isolated test tenant " + slug);
  await manager.choose(org.id);
  const root = "/api/v1/organizations/" + org.id;
  const ports = (await manager.request("GET", root + "/ports?limit=100&status=Active")).ports;
  const vessels = (await manager.request("GET", root + "/vessels?limit=100&status=Active")).vessels;
  const portA = ports.find(x => x.referenceCode === "DEMO-PORT-1");
  const portB = ports.find(x => x.referenceCode === "DEMO-PORT-2");
  const vessel = vessels.find(x => x.referenceCode === "DEMO-SHIP-1");
  assert.ok(portA && portB && vessel, "Accepted M3 fixtures missing");
  const suffix = slug === "m3-north" ? "NORTH" : "EAST";
  const definitions = [
    ["containers", { referenceCode: "M4-BOX-"+suffix,
      identifierKind: "Synthetic", containerIdentifier: "SYN-BOX-M4"+suffix,
      equipmentType: "Dry20", displayName: "Synthetic " + suffix + " container" }],
    ["voyages", { referenceCode: "M4-VOYAGE-"+suffix,
      vesselId: vessel.id, originPortId: portA.id, destinationPortId: portB.id,
      plannedDepartureAtUtc: "2026-11-01T22:00:00Z",
      plannedArrivalAtUtc: "2026-11-02T06:00:00Z" }]
  ];
  for (const [kind, definition] of definitions) {
    const path = root + "/" + kind;
    let rows = (await manager.request("GET", path + "?recordStatus=All&limit=100"))[kind];
    let row = rows.find(x => x.referenceCode === definition.referenceCode);
    if (!row) {
      row = await manager.request("POST", path, definition);
      assert.equal(row.referenceCode, definition.referenceCode);
    }
    rows = (await manager.request("GET", path + "?recordStatus=All&limit=100"))[kind];
    const matches = rows.filter(x => x.referenceCode === definition.referenceCode);
    assert.equal(matches.length, 1, "M4 fixture must be idempotent");
    assert.equal(matches[0].id, row.id);
    assert.equal(matches[0].recordStatus, "Active");
  }
}
console.log("M4 two-tenant deterministic voyage and container fixtures: PASS");
