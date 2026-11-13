import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync } from "node:child_process";
import {verifyServiceState, isRefusedConnection} from "./g17-network-guards.mjs";

const project = process.env.HARBOROPS_G17_PROJECT;
const evidence = process.env.HARBOROPS_G17_EVIDENCE_DIR;
assert.match(process.env.HARBOROPS_SOURCE_SHA || "", /^[a-f0-9]{40}$/);
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
  grafana: ["telemetry_monitor"],
  dashboard_gateway: ["dashboard_access","telemetry_monitor"]
};
const observed = {};
const serviceIds = {};
const processStates = {};
let failedService = null;
let failureType = null;
const resultPath = path.join(evidence,"m17-network-results.json");
let outcome = "FAIL";
let dashboardListener = null;
const protectedPortsRefused = [];
let nonLoopbackRefused = 0;
try {
  for (const [service, want] of Object.entries(expected)) {
    // The migrator is a completed one-shot container, so ps -a is required.
    const id = docker([...compose, "ps", "-a", "-q", service]).split(/\s+/).filter(Boolean);
    assert.equal(id.length, 1, "Exactly one owned container is required for " + service);
    assert.match(id[0], /^[a-f0-9]{12,64}$/);
    serviceIds[service] = id[0];
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
    const state = JSON.parse(docker(["inspect","--format",
      "{{json .State}}",id[0]]));
    processStates[service] = {status:state.Status,exit_code:state.ExitCode,
      oom_killed:state.OOMKilled};
    failedService = service;
    verifyServiceState(service,state);
    failedService = null;
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
    const id=serviceIds[service];
    failedService=service;
    const state=JSON.parse(docker(["inspect","--format",
      "{{json .State}}",id]));
    processStates[service]={status:state.Status,exit_code:state.ExitCode,
      oom_killed:state.OOMKilled};
    verifyServiceState(service,state);
    failedService=null;
    const exposed=JSON.parse(docker(["inspect","--format",
      "{{json .HostConfig.PortBindings}}",id]));
    assert.equal(Object.keys(exposed||{}).filter(key=>
      (exposed[key]||[]).some(binding=>binding?.HostPort)).length,0,
      service+" exported an unauthenticated diagnostic host listener");
  }
  // Grafana itself is attached only to the internal monitoring network and must
  // publish nothing. The single forwarder owns the one loopback listener.
  const grafanaId=serviceIds.grafana;
  failedService="grafana";
  verifyServiceState("grafana",JSON.parse(docker(["inspect","--format",
    "{{json .State}}",grafanaId])));
  failedService=null;
  const grafanaBound=JSON.parse(docker(["inspect","--format",
    "{{json .HostConfig.PortBindings}}",grafanaId]));
  assert.equal(Object.values(grafanaBound||{}).flat().filter(Boolean).length,0,
    "Grafana must not publish a host port itself");
  const gatewayId=serviceIds.dashboard_gateway;
  const bound=JSON.parse(docker(["inspect","--format",
    "{{json .HostConfig.PortBindings}}",gatewayId]));
  const grafanaPorts=Object.values(bound||{}).flat().filter(Boolean);
  assert.equal(grafanaPorts.length,1);
  assert.equal(grafanaPorts[0].HostIp,"127.0.0.1");
  assert.equal(grafanaPorts[0].HostPort,"3301");
  const published=JSON.parse(docker(["inspect","--format",
    "{{json .NetworkSettings.Ports}}",gatewayId]));
  const live=(published["3000/tcp"]||[]).filter(Boolean);
  assert.equal(live.length,1,"Docker did not actually publish the loopback listener");
  assert.equal(live[0].HostIp,"127.0.0.1");
  assert.equal(docker(["network","inspect",project+"_dashboard_access","--format",
    "{{.Internal}}"]),"false","The forwarder network must be the only routable one");
  const members=docker(["network","inspect",project+"_dashboard_access","--format",
    "{{range $id,$c := .Containers}}{{$c.Name}} {{end}}"]).split(/\s+/).filter(Boolean);
  assert.deepEqual(members,[project+"-dashboard_gateway-1"],
    "Only the loopback forwarder may join the routable dashboard network");
  // Positive control: the dashboard is reachable on loopback (Grafana health).
  const health=await fetch("http://127.0.0.1:3301/api/health",{
    signal:AbortSignal.timeout(8000)});
  assert.equal(health.status,200,"Loopback dashboard listener is not serving Grafana");
  dashboardListener = {host_ip:"127.0.0.1",host_port:"3301",
    loopback_http_status:health.status,published_by:"dashboard_gateway",
    grafana_publishes_nothing:true,non_loopback_addresses_refused:0};
  // Negative control: the same port is not reachable via a non-loopback host address.
  for(const nic of Object.values(os.networkInterfaces()).flat()) {
    if(!nic||nic.internal||nic.family!=="IPv4"||nic.address.startsWith("169.254."))continue;
    try {
      const wire=await fetch("http://"+nic.address+":3301/api/health",{
        signal:AbortSignal.timeout(2500)});
      throw Error("Dashboard reachable on non-loopback address "+nic.address+
        " with status "+wire.status);
    } catch(error) {
      if(!(isRefusedConnection(error)||error?.name==="TimeoutError"||
        error?.cause?.code==="ETIMEDOUT"))throw error;
      nonLoopbackRefused++;
      dashboardListener.non_loopback_addresses_refused = nonLoopbackRefused;
    }
  }
  // Verify a network-untrusted HTTP client cannot reach the monitoring
  // endpoints via any published port. A reachable listener is a failure.
  for(const port of [4317,9464,9090,3200]) {
    try {
      const response=await fetch("http://127.0.0.1:"+port+"/",{
        signal:AbortSignal.timeout(1300)});
      throw Error("Untrusted host client reached protected monitoring port "+port+
        " with status "+response.status);
    } catch(error) {
      if(!isRefusedConnection(error)) throw error;
      protectedPortsRefused.push(port);
    }
  }
  for(const service of Object.keys(expected)) {
    const state=JSON.parse(docker(["inspect","--format",
      "{{json .State}}",serviceIds[service]]));
    processStates[service]={status:state.Status,exit_code:state.ExitCode,
      oom_killed:state.OOMKilled};
    failedService=service;
    verifyServiceState(service,state);
    failedService=null;
  }
  outcome = "PASS";
} catch(error) {
  failureType=error?.name || "Error";
  throw error;
} finally {
  fs.writeFileSync(resultPath,JSON.stringify({
    project,source_sha:process.env.HARBOROPS_SOURCE_SHA,
    result:outcome,expected,observed,process_states:processStates,
    protected_ports_refused:protectedPortsRefused,dashboard_listener:dashboardListener,
    failed_service:failedService,failure_type:failureType
  },null,2) + "\n",{flag:"wx"});
}
