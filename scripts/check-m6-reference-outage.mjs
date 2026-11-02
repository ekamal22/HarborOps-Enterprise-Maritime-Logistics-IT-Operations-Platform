#!/usr/bin/env node
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";

const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
assert.ok(password && password.length >= 20, "Synthetic E:-drive credential is required");
const project = "ho-verify-g6";
const base = "http://127.0.0.1:5173";
const docker = (...args) => execFileSync("docker", args, {
  encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 30000
}).trim();
// An E:-confined DOCKER_CONFIG has no named contexts, so accept the pinned Docker Desktop pipe.
assert.ok(docker("context", "show") === "desktop-linux" ||
  process.env.DOCKER_HOST === "npipe:////./pipe/dockerDesktopLinuxEngine");
assert.equal(docker("info", "--format", "{{.OSType}}|{{.OperatingSystem}}|{{.Name}}"),
  "linux|Docker Desktop|docker-desktop");
assert.equal(JSON.parse(docker("compose", "-p", project, "config", "--format", "json")).name, project);
for (const service of ["postgres", "api"]) {
  const id = docker("compose", "-p", project, "ps", "-q", service);
  assert.ok(id, "Disposable service must exist: " + service);
  assert.equal(docker("inspect", "--format",
    '{{index .Config.Labels "com.docker.compose.project"}}', id), project);
}
class Client {
  constructor() { this.jar = new Map(); }
  async request(route, method = "GET", data) {
    const headers = {};
    if (this.jar.size)
      headers.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
    if (method !== "GET") {
      headers.Origin = base;
      const token = await this.request("/api/v1/auth/csrf");
      assert.equal(token.status, 200);
      headers["X-HarborOps-CSRF"] = token.body.token;
      if (this.jar.size)
        headers.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
    }
    if (data !== undefined) headers["Content-Type"] = "application/json";
    const response = await fetch(base + route, {
      method, headers, body: data === undefined ? undefined : JSON.stringify(data),
      signal: AbortSignal.timeout(15000)
    });
    for (const cookie of response.headers.getSetCookie()) {
      const segment = cookie.split(";", 1)[0], pos = segment.indexOf("=");
      if (pos > 0) this.jar.set(segment.slice(0, pos), segment.slice(pos + 1));
    }
    const raw = await response.text();
    let body;
    try { body = JSON.parse(raw); } catch { body = raw; }
    return { status: response.status, body, raw };
  }
}
const manager = new Client();
const admin = new Client();
async function login(client, email) {
  const result = await client.request("/api/v1/auth/login", "POST", { email, password });
  assert.equal(result.status, 200);
  return result.body;
}
async function ready() {
  for (let n = 0; n < 35; n++) {
    try {
      if ((await manager.request("/health/ready")).status === 200) return;
    } catch {}
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  throw new Error("PostgreSQL failed to return to a ready state");
}
function sanitized(result, code) {
  assert.equal(result.status, 503);
  assert.equal(result.body?.code, code);
  assert.ok(result.raw.length < 512);
  assert.doesNotMatch(result.raw, /Npgsql|PostgresException|password|SELECT\s+.*FROM\s+/i);
}
await ready();
await login(manager, "m3-manager@example.test");
await login(admin, "m3-admin-a@example.test");
const list = await manager.request("/api/v1/me/organizations");
assert.equal(list.status, 200);
const org = list.body.organizations.find(x => x.slug === "m3-north");
assert.ok(org);
assert.equal((await manager.request("/api/v1/auth/session/organization", "PUT",
  { organizationId: org.id })).status, 200);
assert.equal((await admin.request("/api/v1/auth/session/organization", "PUT",
  { organizationId: org.id })).status, 200);
const resource = "/api/v1/organizations/" + org.id + "/ports";
const before = await manager.request(resource);
assert.equal(before.status, 200);
assert.ok(before.body.ports.some(x => x.referenceCode === "DEMO-PORT-1"));
let stopped = false;
try {
  stopped = true;
  docker("compose", "-p", project, "stop", "postgres");
  sanitized(await manager.request(resource), "dependency_unavailable");
} finally {
  if (stopped) docker("compose", "-p", project, "start", "postgres");
}
await ready();
let recovered = false;
for (let attempt = 0; attempt < 30; attempt++) {
  const result = await manager.request(resource);
  if (result.status === 200) {
    assert.ok(result.body.ports.some(x => x.referenceCode === "DEMO-PORT-1"));
    recovered = true;
    break;
  }
  sanitized(result, "dependency_unavailable");
  await new Promise(resolve => setTimeout(resolve, 1000));
}
assert.ok(recovered, "M3 references must recover after PostgreSQL restart");
const roster = await admin.request("/api/v1/organizations/" + org.id + "/memberships?limit=100");
assert.equal(roster.status, 200);
const managerUser = roster.body.memberships.find(x => x.email === "m3-manager@example.test");
assert.ok(managerUser && managerUser.role === "Manager");
const revoked = await admin.request(
  "/api/v1/organizations/" + org.id + "/memberships/" + managerUser.userId,
  "PUT", { role: "Support", status: "Active", expectedVersion: managerUser.version });
assert.equal(revoked.status, 200);
const denied = await manager.request(resource);
assert.equal(denied.status, 403);
assert.equal(denied.body?.code, "access_denied");
const logs = docker("compose", "-p", project, "logs", "--no-color", "api");
assert.ok(!logs.includes(password), "Credential must not appear in logs");
console.log("M3 live PostgreSQL outage/recovery and real role downgrade: PASS");
