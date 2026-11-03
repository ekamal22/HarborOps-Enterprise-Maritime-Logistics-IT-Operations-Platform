#!/usr/bin/env node
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
const project = "ho-verify-g7";
const output = execFileSync("docker",
  ["compose", "--project-name", project, "config", "--format", "json"],
  { encoding:"utf8", timeout:30_000, maxBuffer:2 * 1024 * 1024 });
const config = JSON.parse(output);
assert.equal(config.name, project);
const actual = config.networks?.internal?.ipam?.config?.[0]?.subnet;
assert.equal(actual, "172.30.253.0/24", "G7 network cannot reuse retained stack");
assert.equal(config.services?.web?.networks?.internal?.ipv4_address, "172.30.253.10");
const environment = config.services?.api?.environment || {};
assert.equal(environment.Identity__TrustedLocalHttpPeers, "172.30.253.1,172.30.253.10");
assert.equal(environment.ReverseProxy__KnownProxies__0, "172.30.253.10");
for (const name of ["postgres","migrator","api","web"])
  assert.ok(config.services?.[name], "Missing G7 service: " + name);
console.log("Isolated G7 network and trusted proxy identity checked");
