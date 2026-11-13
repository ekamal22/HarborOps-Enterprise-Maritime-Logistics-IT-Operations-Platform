import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const project = process.env.HARBOROPS_G17_PROJECT;
const evidence = process.env.HARBOROPS_G17_EVIDENCE_DIR;
const password = process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(project || "", /^ho-verify-m17-[a-z0-9-]+$/);
const hostedEvidence = process.env.GITHUB_ACTIONS === "true" &&
  process.env.HARBOROPS_G17_HOSTED === "true" &&
  process.env.RUNNER_TEMP &&
  path.resolve(process.env.HARBOROPS_G17_EVIDENCE_DIR || "").startsWith(
    path.resolve(process.env.RUNNER_TEMP) + path.sep);
assert.ok(/^[Ee]:[\\/]/.test(evidence || "") || hostedEvidence,
  "Evidence directory must be on E: or a uniquely owned hosted temp root");
assert.ok(password, "A disposable dashboard password is required");
const compose = [
  "compose.m17.verify.yaml", "compose.m9.worker.yaml",
  "compose.m17.observability.override.yaml", "compose.m17.worker.override.yaml"
];
const args = ["compose", "-p", project, ...compose.flatMap(f => ["-f", f]),
  "--profile", "observability", "--profile", "worker"];
const checks = [];
const reportPath = path.join(evidence, "m17-worker-results.json");
const started = Date.now();

function docker(extra) {
  const res = spawnSync("docker", [...args, ...extra], {
    encoding: "utf8", timeout: 30000, maxBuffer: 2 * 1024 * 1024
  });
  if (res.status !== 0)
    throw Error("Disposable worker command failed with exit " + res.status);
  return res.stdout.trim();
}
async function metric(expression) {
  const url = "http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-metrics/api/v1/query";
  const request = new URL(url);
  request.searchParams.set("query", expression);
  const response = await fetch(request, {
    headers: {
      authorization: "Basic " + Buffer.from("local_operator:" + password).toString("base64")
    }, signal: AbortSignal.timeout(15000)
  });
  assert.equal(response.status, 200, "Local metrics proxy was unavailable");
  const data = await response.json();
  assert.equal(data.status, "success");
  return data.data.result;
}
async function poll(label, callback, timeoutMs = 105000) {
  const stop = Date.now() + timeoutMs;
  while (Date.now() < stop) {
    try {
      const outcome = await callback();
      if (outcome) return outcome;
    } catch { /* metric export may not have started yet */ }
    await new Promise(resolve => setTimeout(resolve, 3000));
  }
  throw Error("No fresh matching observation: " + label);
}
async function check(name, callback) {
  try {
    const value = await callback();
    checks.push({name, status:"PASS", detail:value ?? null});
  } catch (error) {
    checks.push({name, status:"FAIL", error_type:error?.name || "Error",error_message:String(error?.message||"").slice(0,240)});
  }
}

try {
  await check("owned-worker-ready", async () => {
    // The container healthcheck first runs one interval after start, so a worker that was
    // started moments ago reports "running starting". Wait within a bound; a worker that
    // never becomes healthy still fails here.
    let instance = "";
    await poll("worker container healthy", async () => {
      instance = docker(["ps", "worker", "--format", "{{.State}} {{.Health}}"]);
      return instance === "running healthy";
    }, 150000);
    assert.equal(instance, "running healthy");
    docker(["exec", "-T", "worker", "dotnet", "HarborOps.Worker.dll", "--readiness"]);
    return "running healthy under restricted worker identity";
  });
  await check("real-worker-running-and-heartbeat", async () => {
    await poll("worker running", async () => {
      const result = await metric('harborops_service_running_ratio{service="tracking"}');
      return result.length === 1 && Number(result[0].value[1]) === 1;
    });
    await poll("worker advancing heartbeat", async () => {
      const result = await metric('increase(harborops_worker_heartbeat_total{worker="tracking"}[2m])');
      return result.length === 1 && Number(result[0].value[1]) > 0;
    });
    const samples = await metric('harborops_worker_backlog_age_seconds{worker="tracking"}');
    assert.equal(samples.length, 1);
    assert.ok(Number.isFinite(Number(samples[0].value[1])));
    return "fresh running state, heartbeat and finite backlog";
  });

  await check("fault-worker-stop-suppresses-stale-backlog", async () => {
    try {
      docker(["stop", "worker"]);
      // A stopped process may leave last-known gauge data in Prometheus.
      // The live heartbeat gate must therefore suppress the backlog alert.
      await poll("no fresh worker heartbeat", async () => {
        const r = await metric('increase(harborops_worker_heartbeat_total{worker="tracking"}[1m])');
        return r.length === 0 || Number(r[0].value[1]) === 0;
      }, 150000);
      const r = await metric('ALERTS{alertname="HarborOpsTrackingBacklogStale",alertstate="firing"}');
      assert.equal(r.length, 0);
      return "held worker did not raise active-backlog alert";
    } finally {
      // Restore even on failed assertions; only the owned worker is touched.
      docker(["start", "worker"]);
    }
  });
  await check("worker-recovers-after-fault", async () => {
    await poll("worker restarted", async () =>
      docker(["ps", "worker", "--format", "{{.State}} {{.Health}}"]) === "running healthy");
    await poll("worker heartbeat resumes", async () => {
      const r = await metric('increase(harborops_worker_heartbeat_total{worker="tracking"}[2m])');
      return r.length === 1 && Number(r[0].value[1]) > 0;
    });
    return "restricted worker healthy after restart";
  });
} finally {
  fs.writeFileSync(reportPath, JSON.stringify({
    source_sha: process.env.HARBOROPS_SOURCE_SHA,
    project, elapsed_seconds: (Date.now() - started) / 1000,
    checks, result: checks.length === 4 && checks.every(x => x.status === "PASS")
      ? "PASS" : "FAIL",
    disposition_fixtures: "RESTRICTED_INTEGRATION_TRX_REQUIRED"
  }, null, 2) + "\n", {flag:"wx"});
}
if (checks.length !== 4 || checks.some(x => x.status !== "PASS"))
  process.exitCode = 1;
