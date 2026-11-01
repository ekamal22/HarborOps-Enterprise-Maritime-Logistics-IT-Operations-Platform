import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const base = "http://localhost:5173";
const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
const root = process.env.HARBOROPS_VERIFICATION_ROOT;
const sha = process.env.HARBOROPS_SOURCE_SHA;
assert.ok(password && password.length >= 20 && root && sha, "G5 fixture environment required");

class Session {
  cookies = new Map();
  async call(method, url, body) {
    const headers = { Origin: base };
    if (this.cookies.size) headers.Cookie = [...this.cookies].map(([k,v]) => k + "=" + v).join("; ");
    if (method !== "GET") {
      const csrf = await this.call("GET", "/api/v1/auth/csrf");
      headers["X-HarborOps-CSRF"] = csrf.token;
      headers.Cookie = [...this.cookies].map(([k,v]) => k + "=" + v).join("; ");
    }
    if (body !== undefined) headers["Content-Type"] = "application/json";
    const response = await fetch(base + url, {
      method, headers, ...(body === undefined ? {} : { body: JSON.stringify(body) })
    });
    for (const cookie of response.headers.getSetCookie()) {
      const match = cookie.match(/^([^=;]+)=([^;]*)/);
      if (match) this.cookies.set(match[1], match[2]);
    }
    const raw = await response.text();
    const data = raw ? JSON.parse(raw) : null;
    if (!response.ok) throw Error(method + " " + url + " status=" + response.status +
      " " + JSON.stringify(data).slice(0, 180));
    return data;
  }
}
const manager = new Session();
await manager.call("POST", "/api/v1/auth/login", { email:"m3-manager@example.test", password });
const orgs = (await manager.call("GET", "/api/v1/me/organizations")).organizations;
const summary = [];
for (const slug of ["m3-north", "m3-east"]) {
  const org = orgs.find(row => row.slug === slug && row.role === "Manager");
  assert.ok(org, "Missing M5 organization " + slug);
  await manager.call("PUT", "/api/v1/auth/session/organization", { organizationId:org.id });
  const api = "/api/v1/organizations/" + org.id;
  const ports = (await manager.call("GET", api + "/ports?status=Active&limit=100")).ports;
  const from = ports.find(p => p.referenceCode === "DEMO-PORT-1");
  const to = ports.find(p => p.referenceCode === "DEMO-PORT-2");
  assert.ok(from && to && from.id !== to.id, "M3 route fixtures missing");
  const definitions = [
    { code:"M5-DEMO-" + (slug === "m3-north" ? "NORTH" : "EAST"), workflow:"Draft", archive:false },
    { code:"M5-SHARED-REF", workflow:"Booked", archive:false },
    { code:"M5-TRANSIT-" + (slug === "m3-north" ? "N" : "E"), workflow:"InTransit", archive:false },
    { code:"M5-ARCHIVE-" + (slug === "m3-north" ? "N" : "E"), workflow:"Draft", archive:true }
  ];
  async function prepare(definition) {
    const list = await manager.call("GET", api + "/shipments?recordStatus=All&q=" + definition.code + "&limit=100");
    const matches = list.shipments.filter(s => s.referenceCode === definition.code);
    assert.ok(matches.length <= 1, "Duplicate local reference");
    const external = "M5/" + slug.toUpperCase() + "/" + definition.code;
    let row = matches[0] || await manager.call("POST", api + "/shipments", {
      referenceCode:definition.code,externalReference:external,
      displayName:"Synthetic " + definition.code,
      originPortId:from.id,destinationPortId:to.id
    });
    if (definition.workflow === "Booked" || definition.workflow === "InTransit") {
      if (row.workflowStatus === "Draft")
        row = await manager.call("PATCH", api + "/shipments/" + row.id,
          { workflowStatus:"Booked", expectedVersion:row.version });
    }
    if (definition.workflow === "InTransit" && row.workflowStatus === "Booked")
      row = await manager.call("PATCH", api + "/shipments/" + row.id,
        { workflowStatus:"InTransit", expectedVersion:row.version });
    if (definition.archive && row.recordStatus === "Active") {
      await manager.call("DELETE", api + "/shipments/" + row.id, { expectedVersion:row.version });
      row = await manager.call("GET", api + "/shipments/" + row.id);
    }
    assert.equal(row.organizationId, org.id);
    assert.equal(row.originPortId, from.id);
    assert.equal(row.destinationPortId, to.id);
    assert.equal(row.externalReference, external);
    assert.equal(row.workflowStatus, definition.workflow);
    assert.equal(row.recordStatus, definition.archive ? "Archived" : "Active");
    const history = (await manager.call("GET", api + "/shipments/" + row.id + "/status-changes")).changes;
    const expected = definition.workflow === "InTransit" ? 3 :
      definition.workflow === "Booked" ? 2 : 1;
    assert.equal(history.length, expected, "Unexpected status history");
    return { id:row.id, version:row.version, referenceCode:row.referenceCode,
      externalReference:row.externalReference, recordStatus:row.recordStatus,
      workflowStatus:row.workflowStatus, history:history.length };
  }
  const first = [];
  for (const d of definitions) first.push(await prepare(d));
  const second = [];
  for (const d of definitions) second.push(await prepare(d));
  assert.deepEqual(second, first, "M5 fixture rerun changed durable identity/version/history");
  summary.push({ organizationId:org.id,slug,roles:["Administrator","Manager","Analyst","Auditor","Support"],
    shipments:first });
}
const shared = summary.map(x=>x.shipments.find(s=>s.referenceCode==="M5-SHARED-REF"));
assert.equal(shared.length,2);
assert.equal(shared[0].referenceCode, shared[1].referenceCode);
assert.notEqual(shared[0].externalReference, shared[1].externalReference);
assert.notEqual(shared[0].id, shared[1].id);
const artifact = { sourceSha:sha, verifiedAtUtc:new Date().toISOString(),
  organizations:summary, idempotent:true };
fs.writeFileSync(path.join(root,"evidence","g5-fixtures.json"),
  JSON.stringify(artifact, null, 2));
console.log("G5 two-tenant shipment lifecycle and archived fixtures: PASS");
