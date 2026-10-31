#!/usr/bin/env node
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";

const project = "ho-verify";
const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
assert.ok(password && password.length >= 20, "Disposable test credential is required");
const docker = (...args) => execFileSync("docker", args, { encoding: "utf8",
  stdio: ["ignore", "pipe", "pipe"], timeout: 30000 }).trim();
const host = process.env.DOCKER_HOST ||
  docker("context", "inspect", "--format", "{{ (index .Endpoints \"docker\").Host }}");
assert.match(host, /^(unix:\/\/|npipe:\/\/|tcp:\/\/(?:localhost|127\.0\.0\.1):)/,
  "Only the local Docker context is authorized");
const cfg = JSON.parse(docker("compose", "-p", project, "config", "--format", "json"));
assert.equal(cfg.name, project);
assert.ok(cfg.services.postgres && cfg.services.api);
for (const service of ["postgres", "api"]) {
  const id = docker("compose", "-p", project, "ps", "-q", service);
  assert.ok(id.length > 12, "Disposable service must be running: " + service);
  assert.equal(docker("inspect", "--format",
    "{{index .Config.Labels \"com.docker.compose.project\"}}", id), project);
}
const url = "http://127.0.0.1:5173";
class Client {
  constructor() { this.jar = new Map(); }
  async send(path, method = "GET", data, token) {
    const headers = {};
    if (this.jar.size) headers.Cookie = [...this.jar].map(([k, v]) => k + "=" + v).join("; ");
    if (method !== "GET") headers.Origin = url;
    if (token) headers["X-HarborOps-CSRF"] = token;
    if (data !== undefined) headers["Content-Type"] = "application/json";
    const response = await fetch(url + path, {
      method, headers, body: data === undefined ? undefined : JSON.stringify(data),
      signal: AbortSignal.timeout(15000)
    });
    for (const cookie of response.headers.getSetCookie()) {
      const segment = cookie.split(";", 1)[0], pos = segment.indexOf("=");
      if (pos > 0) this.jar.set(segment.slice(0, pos), segment.slice(pos + 1));
    }
    const raw = await response.text();
    let body = null;
    try { body = JSON.parse(raw); } catch { body = raw; }
    return { status: response.status, body, raw };
  }
  async mutate(path, method, data) {
    const csrf = await this.send("/api/v1/auth/csrf");
    assert.equal(csrf.status, 200);
    return this.send(path, method, data, csrf.body.token);
  }
}
const admin = new Client();
const member = new Client();
function safeError(result, expected) {
  assert.equal(result.status, expected, "Unexpected dependency response");
  assert.ok(result.raw.length < 512, "Error envelope exceeds limit");
  assert.doesNotMatch(result.raw, /Npgsql|PostgresException|password|session_cookie/i);
  assert.doesNotMatch(result.raw, /SELECT\s+\S+\s+FROM\s+/i);
}
async function ready() {
  for (let i = 0; i < 30; i++) {
    try {
      const response = await admin.send("/health/ready");
      if (response.status === 200) return;
    } catch { /* service reinitializing */ }
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  throw new Error("Disposable PostgreSQL did not recover");
}
await ready();
const loginAdmin = await admin.mutate("/api/v1/auth/login", "POST",
  { email: "m2-operator@example.test", password });
assert.equal(loginAdmin.status, 200);
const available = await admin.send("/api/v1/me/organizations");
assert.equal(available.status, 200);
const organization = available.body.organizations.find(x => x.slug === "north-terminal");
assert.ok(organization, "Synthetic organization is required");
assert.equal((await admin.mutate("/api/v1/auth/session/organization", "PUT",
  { organizationId: organization.id })).status, 200);
const loginMember = await member.mutate("/api/v1/auth/login", "POST",
  { email: "m2-outage@example.test", password });
assert.equal(loginMember.status, 200);
const memberId = loginMember.body.user.id;
assert.equal((await admin.mutate("/api/v1/organizations/" + organization.id +
  "/memberships/" + memberId, "PUT",
  { role: "Support", status: "Active", expectedVersion: null })).status, 200);
assert.equal((await member.mutate("/api/v1/auth/session/organization", "PUT",
  { organizationId: organization.id })).status, 200);
assert.equal((await member.send("/api/v1/organizations/" + organization.id)).status, 200);
assert.equal((await admin.mutate("/api/v1/organizations/" + organization.id +
  "/memberships/" + memberId, "DELETE", { expectedVersion: 1 })).status, 204);
safeError(await member.send("/api/v1/organizations/" + organization.id), 409);
let stopped = false;
try {
  stopped = true;
  docker("compose", "-p", project, "stop", "postgres");
  safeError(await admin.send("/api/v1/organizations/" + organization.id), 503);
  safeError(await admin.send("/api/v1/me/organizations"), 503);
} finally {
  if (stopped) docker("compose", "-p", project, "start", "postgres");
}
await ready();
// Database readiness and authenticated scope reads use different connections.
// During restart, a stale pooled connection may still fail after readiness
// succeeds. Only sanitized 503s are permissible until scope access recovers.
async function waitForScopedRecovery(organizationId) {
  const path = "/api/v1/organizations/" + organizationId;
  const attempts = 30;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    const result = await admin.send(path);
    if (result.status === 200) {
      assert.equal(result.body.id, organizationId,
        "Recovered request must preserve the selected organization");
      console.log("Scoped access recovered on attempt " + attempt + " of " + attempts +
        " after " + (attempt - 1) + " sanitized 503 response(s) from stale pooled connections");
      return;
    }
    safeError(result, 503);
    if (attempt < attempts) await new Promise(resolve => setTimeout(resolve, 1000));
  }
  assert.fail("Authenticated organization scope did not recover after database restart");
}
await waitForScopedRecovery(organization.id);
safeError(await member.mutate("/api/v1/auth/session/organization", "PUT",
  { organizationId: organization.id }), 404);
safeError(await member.send("/api/v1/organizations/" + organization.id), 409);
const serviceLog = docker("compose", "-p", project, "logs", "--no-color", "api");
assert.ok(!serviceLog.includes(password), "Synthetic authentication input must not appear in service logs");
console.log("G2 scoped PostgreSQL outage, sanitized 503, recovery and durable revocation: PASS");
