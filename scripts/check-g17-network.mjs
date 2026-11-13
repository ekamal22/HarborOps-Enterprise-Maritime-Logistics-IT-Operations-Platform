import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const project = process.env.HARBOROPS_G17_PROJECT;
const evidence = process.env.HARBOROPS_G17_EVIDENCE_DIR;
assert.match(project || "", /^ho-verify-m17-[a-z0-9-]+$/);
const hostedEvidence = process.env.GITHUB_ACTIONS === "true" &&
  process.env.HARBOROPS_G17_HOSTED === "true" &&
  process.env.RUNNER_TEMP &&
  path.resolve(process.env.HARBOROPS_G17_EVIDENCE_DIR || "").startsWith(
    path.resolve(process.env.RUNNER_TEMP) + path.sep);
assert.ok(/^[Ee]:[\\/]/.test(evidence || "") || hostedEvidence,
  "Evidence directory must be on E: or a uniquely owned hosted temp root");
const compose = ["compose", "-p", project,
  "-f", "compose.m17.verify.yaml",
  "-f", "compose.m17.observability.override.yaml",
  "--profile", "observability"];
function docker(args) {
  return execFileSync("docker", args, {
    encoding: "utf8", timeout: 15000, maxBuffer: 3 * 1024 * 1024
  }).trim();
}
const expected = {
  postgres: ["internal"], migrator: ["internal"],
  api: ["internal", "telemetry_ingest"], web: ["internal"],
  otelcol: ["telemetry_ingest", "telemetry_monitor"],
  tempo: ["telemetry_monitor"], prometheus: ["telemetry_monitor"],
  grafana: ["telemetry_monitor"]
};
const observed = {};
const resultPath = path.join(evidence,"m17-network-results.json");
let outcome = "FAIL";
try {
  for (const [service, want] of Object.entries(expected)) {
    // The migrator is a completed one-shot container, so ps -a is required.
    const id = docker([...compose, "ps", "-a", "-q", service]).split(/\s+/).filter(Boolean);
    assert.equal(id.length, 1, "Exactly one owned container is required for " + service);
    const labels = JSON.parse(docker(["inspect","--format",
      "{{json .Config.Labels}}",id[0]]));
    assert.equal(labels["com.docker.compose.project"],project);
    assert.equal(labels["com.docker.compose.service"],service);
    const networks = JSON.parse(docker(["inspect","--format",
      "{{json .NetworkSettings.Networks}}",id[0]]));
    const names = Object.keys(networks).map(name => {
      assert.ok(name.startsWith(project + "_"), "Unrelated shared network found");
      return name.slice(project.length + 1);
    }).sort();
    assert.deepEqual(names,want.slice().sort(),"Unexpected network access for " + service);
    observed[service] = names;
  }
  for (const name of ["telemetry_ingest","telemetry_monitor"]) {
    const isPrivate = docker(["network","inspect",project + "_" + name,
      "--format","{{.Internal}}"]);
    assert.equal(isPrivate,"true","Telemetry network must not have external routing");
  }
  assert.ok(!observed.web.includes("telemetry_ingest"));
  assert.ok(!observed.web.includes("telemetry_monitor"));
  // Assert actual Docker host port bindings, not just declared YAML ports.
  for(const service of ["otelcol","tempo","prometheus"]) {
    const id=docker([...compose,"ps","-q",service]).trim();
    assert.match(id,/^[a-f0-9]+$/);
    const exposed=JSON.parse(docker(["inspect","--format",
      "{{json .HostConfig.PortBindings}}",id]));
    assert.equal(Object.keys(exposed||{}).filter(key=>
      (exposed[key]||[]).some(binding=>binding?.HostPort)).length,0,
      service+" exported an unauthenticated diagnostic host listener");
  }
  const grafanaId=docker([...compose,"ps","-q","grafana"]);
  const bound=JSON.parse(docker(["inspect","--format",
    "{{json .HostConfig.PortBindings}}",grafanaId]));
  const grafanaPorts=Object.values(bound||{}).flat().filter(Boolean);
  assert.equal(grafanaPorts.length,1);
  assert.equal(grafanaPorts[0].HostIp,"127.0.0.1");
  assert.equal(grafanaPorts[0].HostPort,"3301");
  // Verify a network-untrusted HTTP client cannot reach the monitoring
  // endpoints via any published port. A reachable listener is a failure.
  for(const port of [4317,9464,9090,3200]) {
    try {
      const response=await fetch("http://127.0.0.1:"+port+"/",{
        signal:AbortSignal.timeout(1300)});
      throw Error("Untrusted host client reached protected monitoring port "+port+
        " with status "+response.status);
    } catch(error) {
      if(!["TypeError","TimeoutError","AbortError"].includes(error?.name))
        throw error;
    }
  }
  outcome = "PASS";
} finally {
  fs.writeFileSync(resultPath,JSON.stringify({
    project,source_sha:process.env.HARBOROPS_SOURCE_SHA,
    result:outcome,expected,observed
  },null,2) + "\n",{flag:"wx"});
}
