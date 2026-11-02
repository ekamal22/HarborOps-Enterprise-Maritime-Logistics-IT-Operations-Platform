#!/usr/bin/env node
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";

const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
assert.ok(password && password.length >= 20, "Disposable M5 credential required");
const base = "http://127.0.0.1:5173";
const project = "ho-verify-g6";
const docker = (...args) => execFileSync("docker", args, {
  encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 30000
}).trim();
assert.ok(docker("context", "show") === "desktop-linux" ||
  process.env.DOCKER_HOST === "npipe:////./pipe/dockerDesktopLinuxEngine");
assert.equal(docker("info", "--format", "{{.OSType}}|{{.OperatingSystem}}|{{.Name}}"),
  "linux|Docker Desktop|docker-desktop");
assert.equal(JSON.parse(docker("compose", "-p", project, "config", "--format", "json")).name, project);
for (const service of ["postgres", "api"]) {
  const id = docker("compose", "-p", project, "ps", "-q", service);
  assert.ok(id, "Disposable service missing: " + service);
  assert.equal(docker("inspect", "--format",
    '{{index .Config.Labels "com.docker.compose.project"}}', id), project);
}
class Session {
  jar = new Map();
  async call(method, path, data) {
    const headers = {};
    if (this.jar.size) headers.Cookie = [...this.jar].map(([k,v]) => k + "=" + v).join("; ");
    if (method !== "GET") {
      headers.Origin = base;
      const csrf = await this.call("GET", "/api/v1/auth/csrf");
      assert.equal(csrf.status, 200);
      headers["X-HarborOps-CSRF"] = csrf.body.token;
      headers.Cookie = [...this.jar].map(([k,v]) => k + "=" + v).join("; ");
    }
    if (data !== undefined) headers["Content-Type"] = "application/json";
    const response = await fetch(base + path, {
      method, headers, body: data === undefined ? undefined : JSON.stringify(data),
      signal: AbortSignal.timeout(15000)
    });
    for (const cookie of response.headers.getSetCookie()) {
      const part = cookie.split(";", 1)[0];
      const position = part.indexOf("=");
      if (position > 0) this.jar.set(part.slice(0,position),part.slice(position+1));
    }
    const raw = await response.text();
    let body;
    try { body = JSON.parse(raw); } catch { body = raw; }
    return { status: response.status, raw, body };
  }
}
function sanitized(result) {
  assert.equal(result.status, 503);
  assert.equal(result.body?.code, "dependency_unavailable");
  assert.ok(result.raw.length < 512);
  assert.doesNotMatch(result.raw, /PostgresException|Npgsql|password|SELECT\s+.*FROM\s+/i);
}
const manager = new Session();
async function ready() {
  for (let i=0;i<35;i++) {
    try { if ((await manager.call("GET", "/health/ready")).status === 200) return; }
    catch {}
    await new Promise(resolve => setTimeout(resolve,1000));
  }
  throw new Error("Database readiness did not recover");
}
await ready();
assert.equal((await manager.call("POST", "/api/v1/auth/login",
  { email:"m3-manager@example.test", password })).status,200);
const orgs = await manager.call("GET","/api/v1/me/organizations");
const org = orgs.body.organizations.find(x=>x.slug==="m3-north"&&x.role==="Manager");
assert.ok(org, "M5 verification organization unavailable");
assert.equal((await manager.call("PUT","/api/v1/auth/session/organization",
  { organizationId:org.id })).status,200);
const root="/api/v1/organizations/"+org.id;
const ports = await manager.call("GET",root+"/ports?status=Active&limit=100");
const from=ports.body.ports.find(x=>x.referenceCode==="DEMO-PORT-1");
const to=ports.body.ports.find(x=>x.referenceCode==="DEMO-PORT-2");
assert.ok(from&&to);
const shipments=root+"/shipments";
let list=await manager.call("GET",shipments+"?q=G6-OUTAGE&recordStatus=All");
assert.equal(list.status,200);
let shipment=list.body.shipments.find(x=>x.referenceCode==="G6-OUTAGE");
if(!shipment){
  const created=await manager.call("POST",shipments,{
    referenceCode:"G6-OUTAGE",originPortId:from.id,destinationPortId:to.id
  });
  assert.equal(created.status,201);
  shipment=created.body;
}
const containers=await manager.call("GET",root+"/containers?recordStatus=Active&limit=100");
assert.equal(containers.status,200);
const target=containers.body.containers?.[0];
assert.ok(target,"M4 container fixture not found");
const assignments=shipments+"/"+shipment.id+"/container-assignments";
let before=await manager.call("GET",assignments);
assert.equal(before.status,200);
if(!before.body.assignments.some(x=>x.containerId===target.id)){
  const created=await manager.call("POST",assignments,{
    containerId:target.id,startsAtUtc:"2035-01-01T10:00:00Z"
  });
  assert.equal(created.status,201);
}
before=await manager.call("GET",assignments);
assert.equal(before.status,200);
const persisted=before.body.assignments.find(x=>x.containerId===target.id);
assert.ok(persisted,"M6 assignment fixture not found");
let stopped=false;
try {
  stopped=true;
  docker("compose","-p",project,"stop","postgres");
  sanitized(await manager.call("GET",assignments));
} finally {
  if(stopped)docker("compose","-p",project,"start","postgres");
}
await ready();
let recovered=false;
for(let i=0;i<30;i++){
  const now=await manager.call("GET",assignments);
  if(now.status===200){
    assert.equal(now.body.assignments.filter(x=>x.id===persisted.id).length,1);
    recovered=true;break;
  }
  sanitized(now);
  await new Promise(resolve=>setTimeout(resolve,1000));
}
assert.ok(recovered,"M6 assignment records did not recover");
const logs=docker("compose","-p",project,"logs","--no-color","api");
assert.ok(!logs.includes(password),"Raw credential appeared in API logs");
console.log("M6 PostgreSQL outage/recovery and tenant assignment persistence: PASS");
