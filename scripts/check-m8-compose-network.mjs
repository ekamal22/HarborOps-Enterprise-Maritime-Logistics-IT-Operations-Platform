#!/usr/bin/env node
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
const project = "ho-verify-g8";
const output = execFileSync("docker",
  ["compose", "--project-name", project, "config", "--format", "json"],
  { encoding:"utf8", timeout:30_000, maxBuffer:2 * 1024 * 1024 });
const config = JSON.parse(output);
assert.equal(config.name, project);
const expectedPorts = {postgres:55438,api:8088,web:5178};
for(const [service,port] of Object.entries(expectedPorts)){
  const declared = config.services?.[service]?.ports ?? [];
  assert.equal(declared.length,1, service + " must expose exactly one test-only port");
  assert.equal(String(declared[0].published), String(port));
  assert.equal(declared[0].host_ip,"127.0.0.1");
}
const actual = config.networks?.internal?.ipam?.config?.[0]?.subnet;
assert.equal(actual, "172.30.252.0/24", "G8 network cannot reuse retained stack");
assert.equal(config.services?.web?.networks?.internal?.ipv4_address, "172.30.252.10");
const environment = config.services?.api?.environment || {};
assert.equal(environment.Identity__TrustedLocalHttpPeers, "172.30.252.1,172.30.252.10");
assert.equal(environment.ReverseProxy__KnownProxies__0, "172.30.252.10");
for (const name of ["postgres","migrator","api","web"])
  assert.ok(config.services?.[name], "Missing G8 service: " + name);
console.log("Isolated G8 network and trusted proxy identity checked");
