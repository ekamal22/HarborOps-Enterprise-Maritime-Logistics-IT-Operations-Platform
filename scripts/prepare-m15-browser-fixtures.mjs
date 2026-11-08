import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const base = process.env.HARBOROPS_BROWSER_BASE_URL || "http://localhost:5173";
const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
const root = process.env.HARBOROPS_VERIFICATION_ROOT;
const sha = process.env.HARBOROPS_SOURCE_SHA;
assert.ok(password?.length >= 20 && root && /^[a-f0-9]{40}$/.test(sha ?? ""),
  "M15 disposable browser fixture environment required");

class Session {
  cookies = new Map();

  async request(method, url, payload) {
    const headers = { Origin: base };
    if (this.cookies.size)
      headers.Cookie = [...this.cookies].map(([k, v]) => k + "=" + v).join("; ");
    if (method !== "GET") {
      const csrf = await this.request("GET", "/api/v1/auth/csrf");
      headers["X-HarborOps-CSRF"] = csrf.token;
      headers.Cookie = [...this.cookies].map(([k, v]) => k + "=" + v).join("; ");
    }
    if (payload !== undefined) headers["Content-Type"] = "application/json";
    const response = await fetch(base + url, {
      method, headers, ...(payload === undefined ? {} : { body: JSON.stringify(payload) })
    });
    for (const value of response.headers.getSetCookie()) {
      const cookie = value.match(/^([^=;]+)=([^;]*)/);
      if (cookie) this.cookies.set(cookie[1], cookie[2]);
    }
    const raw = await response.text();
    let body;
    try { body = raw ? JSON.parse(raw) : null; }
    catch { body = raw; }
    if (!response.ok) throw Error(
      "M15 fixture " + method + " " + url + " failed, HTTP " + response.status +
      ", code " + (typeof body?.code === "string" ? body.code : "unavailable"));
    return body;
  }
}

const manager = new Session();
await manager.request("POST", "/api/v1/auth/login",
  { email: "m3-manager@example.test", password });
const organizations = (await manager.request("GET", "/api/v1/me/organizations")).organizations;
const north = organizations.find(o => o.slug === "m3-north" && o.role === "Manager");
assert.ok(north?.id, "Missing authorized M15 manager fixture");
await manager.request("PUT", "/api/v1/auth/session/organization",
  { organizationId: north.id });
const rootApi = "/api/v1/organizations/" + north.id;
const rows = (await manager.request("GET",
  rootApi + "/shipments?recordStatus=All&q=M5-DEMO-NORTH&limit=100")).shipments;
const shipment = rows.find(row => row.referenceCode === "M5-DEMO-NORTH");
assert.ok(shipment?.id && shipment.workflowStatus === "Draft",
  "M15 source shipment must be a fresh M5 Draft fixture");

const now = Date.now();
const utc = date => new Date(date).toISOString().replace(/\.\d{3}Z$/, "Z");
const scheduled = await manager.request("PATCH",
  rootApi + "/shipments/" + shipment.id, {
    expectedVersion: shipment.version,
    plannedDepartureAtUtc: utc(now - 86_400_000),
    plannedArrivalAtUtc: utc(now - 7_200_000)
  });
assert.ok(scheduled.version > shipment.version);
const booked = await manager.request("PATCH",
  rootApi + "/shipments/" + shipment.id, {
    expectedVersion: scheduled.version,
    workflowStatus: "Booked"
  });
assert.equal(booked.workflowStatus, "Booked");
await manager.request("POST",
  rootApi + "/shipments/" + shipment.id + "/exceptions/evaluate");

const exceptions = (await manager.request("GET",
  rootApi + "/shipments/" + shipment.id + "/exceptions")).items;
const overdue = exceptions.find(row => row.ruleCode === "ArrivalScheduleOverdue");
assert.ok(overdue?.id, "Manual M11 evaluation did not create overdue occurrence");

let notice;
for (let attempt = 0; attempt < 36; attempt++) {
  const page = await manager.request("GET",rootApi + "/notifications?state=All&limit=50");
  notice = page.items.find(item => item.shipmentId === shipment.id &&
    item.exceptionId === overdue.id && item.eventType === "ExceptionOpened");
  if (notice) break;
  await new Promise(resolve => setTimeout(resolve, 1000));
}
assert.ok(notice?.id, "Opt-in M15 dispatcher did not persist a scoped personal receipt");
assert.equal(notice.ruleCode, "ArrivalScheduleOverdue");
assert.equal(notice.state, "Unread");
assert.equal(notice.version, 1);
assert.match(notice.title, /unconfirmed/);
const count = await manager.request("GET",rootApi + "/notifications/unread-count");
assert.ok(count.unreadCount >= 1, "Expected a genuinely unread notification");

const evidence = {
  sourceSha: sha, organizationId: north.id, shipmentId: shipment.id,
  exceptionId: overdue.id, notificationId: notice.id, eventType: notice.eventType,
  ruleCode: notice.ruleCode, title: notice.title, createdAtUtc: notice.createdAtUtc,
  testScope: "disposable-m11-to-m15-to-browser"
};
const destination = path.join(root, "m15-browser", "live-fixture.json");
fs.mkdirSync(path.dirname(destination), { recursive: true });
fs.writeFileSync(destination, JSON.stringify(evidence, null, 2) + "\n");
console.log("M15 real M11 event, opt-in fanout and scoped API fixture: PASS");
