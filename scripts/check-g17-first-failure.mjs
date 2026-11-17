// S25 finding A: the first 5xx burst after a process starts must be counted in EVERY operation family, from real
// exported Prometheus observations. Fresh API, collector and Prometheus processes are created on the owned
// disposable stack, the zero baseline of every family must become visible in Prometheus first, and then real
// 503 responses are produced through a stopped PostgreSQL in each family that has a real fault path. The failure
// counter, the eligible-request floor, the failure fraction and the reference alert are then read back.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import {execFileSync, spawnSync} from "node:child_process";
import {FAMILIES, classifyFamily, familyFaultPlan, evaluateFirstFailure, syntheticBearer} from "./g17-first-failure-evidence.mjs";

const project = process.env.HARBOROPS_G17_PROJECT;
const source = process.env.HARBOROPS_SOURCE_SHA;
const evidence = process.env.HARBOROPS_G17_EVIDENCE_DIR;
const password = process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
assert.match(project || "", /^ho-verify-m17-[a-z0-9-]+$/);
assert.match(source || "", /^[a-f0-9]{40}$/);
assert.ok(evidence && fs.existsSync(evidence) && password);
const withWorker = process.env.HARBOROPS_G17_VERIFY_WORKER === "true";
const compose = ["compose", "-p", project, "-f", "compose.m17.verify.yaml",
  ...(withWorker ? ["-f", "compose.m9.worker.yaml"] : []),
  "-f", "compose.m17.observability.override.yaml",
  ...(withWorker ? ["-f", "compose.m17.worker.override.yaml"] : []),
  "--profile", "observability", ...(withWorker ? ["--profile", "worker"] : [])];
const dockerCompose = (parts, timeout = 180000, input) => spawnSync("docker", [...compose, ...parts],
  {encoding: "utf8", timeout, maxBuffer: 4 * 1024 * 1024, input});
const cmd = (parts, timeout = 180000) => {
  const run = dockerCompose(parts, timeout);
  assert.equal(run.status, 0, "docker compose " + parts.slice(0, 2).join(" ") + " failed");
  return run.stdout.trim();
};
const api = "http://127.0.0.1:18080";
const proxy = "http://127.0.0.1:3301/api/datasources/proxy/uid/harborops-local-metrics/api/v1/";
const basic = "Basic " + Buffer.from("local_operator:" + password).toString("base64");
const reportFile = path.join(evidence, "m17-first-failure-results.json");
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
const nonce = crypto.randomBytes(5).toString("hex");
const checks = [];
let stopped = false, error = null;

async function stage(name, fn) {
  try { const data = await fn(); checks.push({name, status: "PASS", data}); return data; }
  catch (e) {
    checks.push({name, status: "FAIL", error_type: e?.name || "Error", error_message: String(e?.message || "").slice(0, 400)});
    throw e;
  }
}
async function prom(expression) {
  const url = new URL(proxy + "query");
  url.searchParams.set("query", expression);
  const response = await fetch(url, {headers: {authorization: basic}, signal: AbortSignal.timeout(13000)});
  assert.equal(response.status, 200);
  const body = await response.json();
  assert.equal(body.status, "success");
  return body.data.result;
}
async function poll(label, predicate, ms) {
  const end = Date.now() + ms;
  let last = null;
  while (Date.now() < end) {
    try { const value = await predicate(); if (value) return value; } catch (e) { last = e; }
    await wait(3000);
  }
  throw new Error("Timed out before " + label + " was observed" + (last ? " (" + String(last.message).slice(0, 160) + ")" : ""));
}

class Session {
  jar = new Map();
  cookie() { return [...this.jar].map(([k, v]) => k + "=" + v).join("; "); }
  store(response) {
    for (const cookie of response.headers.getSetCookie?.() || []) {
      const pair = cookie.split(";")[0], index = pair.indexOf("=");
      if (index > 0) this.jar.set(pair.slice(0, index), pair.slice(index + 1));
    }
  }
  async token() {
    const headers = {Origin: api};
    if (this.jar.size) headers.Cookie = this.cookie();
    const response = await fetch(api + "/api/v1/auth/csrf", {headers, signal: AbortSignal.timeout(14000)});
    assert.equal(response.status, 200);
    this.store(response);
    return (await response.json()).token;
  }
  async send(method, route, body) {
    const headers = {Origin: api};
    if (method !== "GET") headers["X-HarborOps-CSRF"] = await this.token();
    if (this.jar.size) headers.Cookie = this.cookie();
    if (body !== undefined) headers["Content-Type"] = "application/json";
    const response = await fetch(api + route, {method, headers,
      body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(20000)});
    this.store(response);
    const text = await response.text();
    let json = null;
    try { json = JSON.parse(text); } catch { /* not JSON */ }
    return {status: response.status, json, text};
  }
}

// Every response this script causes is classified exactly like the API does, so the exported counter can be
// compared with an exact client-side count.
const clientCounts = Object.fromEntries(FAMILIES.map(f => [f, {requests: 0, server_errors: 0}]));
function note(route, status) {
  const family = classifyFamily(route);
  clientCounts[family].requests++;
  if (status >= 500 && status < 600) clientCounts[family].server_errors++;
}
async function plain(route, init = {}) {
  const response = await fetch(api + route, {signal: AbortSignal.timeout(25000), ...init});
  await response.arrayBuffer();
  note(route, response.status);
  return response;
}

const admin = new Session();
let organizationId = "";
let orgSlug = "";

try {
  await stage("provision-session-while-database-is-healthy", async () => {
    const email = "g17-first-failure-" + nonce + "@example.test";
    const secret = crypto.randomBytes(24).toString("base64url");
    const provision = dockerCompose(["run", "--rm", "-T", "-e", "HARBOROPS_ALLOW_ACCOUNT_CLI=true", "api",
      "--account-cli", "provision", email, "G17 First Failure"], 120000, secret + "\n");
    assert.equal(provision.status, 0, "account provisioning failed: " + String(provision.stderr || provision.stdout).slice(0, 400));
    orgSlug = "g17-first-failure-" + nonce;
    const bootstrap = dockerCompose(["run", "--rm", "-T", "-e", "HARBOROPS_ALLOW_ORGANIZATION_CLI=true", "api",
      "--organization-cli", "bootstrap", orgSlug, "G17 First Failure Organization", email], 120000);
    assert.equal(bootstrap.status, 0, "organization bootstrap failed: " + String(bootstrap.stderr || bootstrap.stdout).slice(0, 400));
    organizationId = cmd(["exec", "-T", "postgres", "psql", "-U", "harborops", "-d", "harborops", "-At",
      "-v", "ON_ERROR_STOP=1", "-c", "SELECT id FROM organizations WHERE slug='" + orgSlug + "'"]);
    assert.match(organizationId, /^[0-9a-f-]{36}$/);
    const login = await admin.send("POST", "/api/v1/auth/login", {email, password: secret});
    assert.equal(login.status, 200, "synthetic account must sign in");
    const selected = await admin.send("PUT", "/api/v1/auth/session/organization", {organizationId});
    assert.ok([200, 409].includes(selected.status));
    const ports = await admin.send("GET", "/api/v1/organizations/" + organizationId);
    assert.equal(ports.status, 200, "the authorized organization read must work before the fault");
    return {session: "authorized", organization_scoped_read: 200};
  });

  const fresh = await stage("fresh-api-collector-and-prometheus-processes", async () => {
    const before = cmd(["ps", "-q", "api"]);
    cmd(["up", "-d", "--force-recreate", "--no-deps", "otelcol", "prometheus", "api"], 240000);
    const after = cmd(["ps", "-q", "api"]);
    assert.notEqual(before, after, "the API container must be a new process");
    const started = Date.now();
    await poll("fresh API readiness", async () => (await fetch(api + "/health/ready", {signal: AbortSignal.timeout(5000)})).status === 200, 120000);
    // Prometheus is a new, empty store: nothing about the earlier process survives in it.
    await poll("fresh Prometheus answering", async () => {
      const up = await prom('up{job="m17_collector"}');
      return up.length === 1;
    }, 120000);
    const empty = await prom("harborops_request_failures_total");
    return {api_container_replaced: true, prometheus_series_before_baseline: empty.length,
      api_ready_after_seconds: Math.round((Date.now() - started) / 1000)};
  });

  await stage("zero-baseline-of-every-family-is-exported-before-any-failure", async () => {
    const started = Date.now();
    const seen = await poll("a zero 5xx baseline for every operation family in Prometheus", async () => {
      const rows = await prom('harborops_request_failures_total{status_class="5xx"}');
      const byFamily = Object.fromEntries(rows.map(r => [r.metric.operation, Number(r.value[1])]));
      return FAMILIES.every(f => byFamily[f] === 0) ? byFamily : null;
    }, 150000);
    const everySeries = await prom("harborops_request_failures_total");
    assert.equal(everySeries.length, FAMILIES.length, "exactly one 5xx series per operation family");
    const unknown = await prom("harborops:m17:api_5xx_rate_sufficient:5m");
    assert.equal(unknown.length, 0, "with no eligible traffic the dashboard 5xx rate must remain Unknown");
    return {families: seen, series_count: everySeries.length, baseline_visible_after_seconds: Math.round((Date.now() - started) / 1000),
      unknown_before_traffic: true, fresh_context: fresh.api_container_replaced};
  });

  await stage("healthy-traffic-in-every-family-meets-the-eligible-floor", async () => {
    // Prometheus cannot count the first sample of a series as an increase, and every non-5xx duration series of a
    // fresh Prometheus is new. A first wave therefore only creates the series, and the eligible count is judged on a
    // second wave. This is the same first-sample rule that the failure counter's zero baseline removes for 5xx.
    const wave = async (scale) => {
      const ok = [];
      for (let i = 0; i < 6 * scale; i++) ok.push(plain("/health/ready"));
      for (let i = 0; i < 6 * scale; i++) ok.push(plain("/api/v1/system/baseline"));
      for (let i = 0; i < 4 * scale; i++) ok.push(plain("/api/v1/auth/csrf", {headers: {Origin: api}}));
      for (let i = 0; i < 6 * scale; i++) {
        const response = await fetch(api + "/api/v1/organizations/" + organizationId,
          {headers: {Cookie: admin.cookie()}, signal: AbortSignal.timeout(20000)});
        await response.arrayBuffer();
        note("/api/v1/organizations/" + organizationId, response.status);
        assert.equal(response.status, 200);
      }
      for (let i = 0; i < 4 * scale; i++) ok.push(plain("/integrations/v1/tracking-events",
        {method: "POST", headers: {"Content-Type": "application/json"}, body: "{}"}));
      await Promise.all(ok);
    };
    await wave(1);
    await poll("a duration series for every family in Prometheus", async () => {
      const rows = await prom('count by (operation) (harborops_request_duration_seconds_count{status_class!="5xx"})');
      const seenFamilies = new Set(rows.map(r => r.metric.operation));
      return FAMILIES.filter(f => f !== "other").every(f => seenFamilies.has(f));
    }, 120000);
    await wave(5);
    const sufficient = await poll("sufficient healthy traffic exported", async () => {
      const rows = await prom("harborops:m17:eligible_requests:5m");
      return rows.length === 1 && Number(rows[0].value[1]) >= 100 ? Number(rows[0].value[1]) : null;
    }, 150000);
    const zero = await prom("harborops:m17:api_5xx_rate_sufficient:5m");
    assert.equal(zero.length, 1, "with 100 or more eligible requests the 5xx rate is a known value");
    assert.equal(Number(zero[0].value[1]), 0, "healthy traffic must show a zero failure rate, not a failure");
    const alert = await prom('ALERTS{alertname="HarborOpsElevatedApiFailureRate",alertstate="firing"}');
    assert.equal(alert.length, 0, "no false-positive alert from healthy traffic");
    return {eligible_requests_5m: sufficient, five_xx_rate: 0, false_positive_alert: false};
  });

  const plan = familyFaultPlan(organizationId);
  const outage = await stage("real-503-burst-in-each-family-through-a-stopped-database", async () => {
    cmd(["stop", "postgres"], 120000);
    stopped = true;
    await poll("readiness reporting the outage", async () => (await fetch(api + "/health/ready", {signal: AbortSignal.timeout(8000)})).status === 503, 120000);
    note("/health/ready", 503);
    const burst = {};
    for (const family of FAMILIES) {
      const step = plan[family];
      if (!step) { burst[family] = {real_fault_path: false}; continue; }
      let produced = 0;
      for (let offset = 0; offset < step.count; offset += 10) {
        const batch = await Promise.all(Array.from({length: Math.min(10, step.count - offset)}, async () => {
          const init = {method: step.method, headers: {...(step.headers || {})}, body: step.body};
          if (step.cookie) init.headers.Cookie = admin.cookie();
          if (step.randomBearer) init.headers.Authorization = "Bearer " + syntheticBearer(crypto.randomBytes);
          const response = await plain(step.route, init);
          assert.equal(response.status, 503, family + " must return the real dependency failure response");
          if (family === "tracking") assert.equal(response.headers.get("retry-after"), "60");
          return true;
        }));
        produced += batch.length;
      }
      burst[family] = {real_fault_path: true, route: step.routeLabel, responses_503: produced};
    }
    // The family with no real fault path: an unmatched URL is a 404, never a 5xx.
    const unmatched = await plain("/g17-first-failure-unmatched-" + nonce);
    assert.equal(unmatched.status, 404);
    burst.other.observed_status = 404;
    return burst;
  });

  await stage("exported-counters-equal-the-real-503-counts-per-family", async () => {
    let lastSeen = null;
    const observed = await poll("every family's exported 5xx count", async () => {
      const rows = await prom('harborops_request_failures_total{status_class="5xx"}');
      const byFamily = Object.fromEntries(rows.map(r => [r.metric.operation, Number(r.value[1])]));
      lastSeen = {exported: byFamily, client: Object.fromEntries(FAMILIES.map(f => [f, clientCounts[f].server_errors])), row_count: rows.length};
      return FAMILIES.every(f => byFamily[f] === clientCounts[f].server_errors) ? byFamily : null;
    }, 150000).catch(error => { throw new Error(error.message + " last observed " + JSON.stringify(lastSeen)); });
    const increases = {};
    for (const family of FAMILIES) {
      const rows = await prom('increase(harborops_request_failures_total{operation="' + family + '",status_class="5xx"}[5m])');
      increases[family] = rows.length === 1 ? Number(rows[0].value[1]) : null;
    }
    const labels = await fetch(proxy.replace("/api/v1/", "/api/v1/") + "series?" + new URLSearchParams({"match[]": "harborops_request_failures_total"}),
      {headers: {authorization: basic}, signal: AbortSignal.timeout(13000)});
    const series = (await labels.json()).data;
    const verdict = evaluateFirstFailure({exported: observed, client: clientCounts, increases, series});
    assert.deepEqual(verdict.problems, [], verdict.problems.join("; "));
    return {exported_5xx: observed, client_5xx: Object.fromEntries(FAMILIES.map(f => [f, clientCounts[f].server_errors])),
      prometheus_increase_5m: increases, series_count: series.length, label_names: verdict.label_names,
      families_with_real_fault: verdict.families_with_real_fault};
  });

  await stage("eligible-floor-fraction-and-reference-alert-from-the-first-burst", async () => {
    const measured = await poll("eligible floor, failure fraction and alert", async () => {
      const [eligible, fraction, sufficient, alert] = await Promise.all([
        prom("harborops:m17:eligible_requests:5m"), prom("harborops:m17:api_5xx_fraction:5m"),
        prom("harborops:m17:api_5xx_rate_sufficient:5m"),
        prom('ALERTS{alertname="HarborOpsElevatedApiFailureRate",alertstate="firing"}')]);
      if ([eligible, fraction, sufficient].some(r => r.length !== 1) || alert.length !== 1) return null;
      return {eligible_requests_5m: Number(eligible[0].value[1]), failure_fraction_5m: Number(fraction[0].value[1]),
        dashboard_rate: Number(sufficient[0].value[1]), alert: "firing"};
    }, 240000);
    assert.ok(measured.eligible_requests_5m >= 100, "approved 100 request minimum");
    assert.ok(measured.failure_fraction_5m > 0.01, "approved 1 percent trigger");
    assert.ok(measured.dashboard_rate > 0, "the dashboard series is no longer Unknown");
    const sample = JSON.parse(execFileSync("docker", [...compose, "ps", "--format", "json", "api"], {encoding: "utf8"}).split("\n")[0]);
    assert.ok(sample && sample.State === "running");
    return measured;
  });
} catch (e) { error = e; }
finally {
  if (stopped) {
    try { cmd(["start", "postgres"], 120000); stopped = false; }
    catch (restoreError) { error = error || restoreError; checks.push({name: "database-owned-restore", status: "FAIL"}); }
  }
}
if (!stopped) {
  try {
    await stage("recovery-restores-readiness-and-the-counters-stop-rising", async () => {
      await poll("API ready after PostgreSQL restart", async () => (await fetch(api + "/health/ready", {signal: AbortSignal.timeout(8000)})).status === 200, 150000);
      // Readiness polling during the restart can itself have produced 5xx responses, and the OTLP export plus the scrape
      // delay their exported count by up to about 30 seconds. Let that settle, then require two samples 35 seconds
      // apart to be equal: no new failures after the database is back.
      await wait(40000);
      const first = await prom('sum(harborops_request_failures_total{status_class="5xx"})');
      await wait(35000);
      const second = await prom('sum(harborops_request_failures_total{status_class="5xx"})');
      assert.equal(first.length, 1);
      assert.equal(second.length, 1);
      assert.equal(Number(second[0].value[1]), Number(first[0].value[1]), "counters rose after recovery without new failures");
      return {readiness: "healthy", counter_after_recovery: Number(second[0].value[1]),
        hysteresis_and_clear_evidence: "promtool first-wave recovery case and the live alert matrix"};
    });
  } catch (e) { error = error || e; }
}
const required = ["provision-session-while-database-is-healthy", "fresh-api-collector-and-prometheus-processes",
  "zero-baseline-of-every-family-is-exported-before-any-failure", "healthy-traffic-in-every-family-meets-the-eligible-floor",
  "real-503-burst-in-each-family-through-a-stopped-database", "exported-counters-equal-the-real-503-counts-per-family",
  "eligible-floor-fraction-and-reference-alert-from-the-first-burst", "recovery-restores-readiness-and-the-counters-stop-rising"];
const success = !error && !stopped && checks.length === required.length &&
  checks.every((c, i) => c.status === "PASS" && c.name === required[i]);
fs.writeFileSync(reportFile, JSON.stringify({source_sha: source, project, result: success ? "PASS" : "FAIL", checks,
  families: FAMILIES, client_counts: clientCounts, database_restored: !stopped,
  source_environment: "OWNED_DISPOSABLE_STACK_ONLY", accepted_assertions: 0,
  failure_type: error?.name ?? null, failure_message: error ? String(error.message).slice(0, 400) : null}, null, 2) + "\n", {flag: "wx"});
if (!success) throw new Error("G17 first-failure baseline verification failed; raw evidence retained");
