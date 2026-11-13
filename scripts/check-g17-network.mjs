import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const project = process.env.HARBOROPS_G17_PROJECT;
const evidence = process.env.HARBOROPS_G17_EVIDENCE_DIR;
assert.match(project || "", /^ho-verify-m17-[a-z0-9-]+$/);
assert.match(evidence || "", /^[Ee]:[\\/]/);
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
  outcome = "PASS";
} finally {
  fs.writeFileSync(resultPath,JSON.stringify({
    project,source_sha:process.env.HARBOROPS_SOURCE_SHA,
    result:outcome,expected,observed
  },null,2) + "\n",{flag:"wx"});
}
