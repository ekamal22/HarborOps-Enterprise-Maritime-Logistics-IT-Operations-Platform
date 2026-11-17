import { test, expect } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const api = process.env.HARBOROPS_G17_API_URL || "http://127.0.0.1:18080";
const proxy = "/api/datasources/proxy/uid/harborops-local-metrics/api/v1";
const project = process.env.HARBOROPS_G17_PROJECT;

function bytesInMib(value) {
  const match = value.match(/^\s*(\d+(?:\.\d+)?)\s*(B|KiB|MiB|GiB|TiB)\b/i);
  if (!match) throw Error("Unrecognized Docker memory units");
  const scale = { B: 1 / (1024 * 1024), KiB: 1 / 1024, MiB: 1, GiB: 1024, TiB: 1024 * 1024 };
  return Number(match[1]) * scale[match[2]];
}
function memorySnapshot() {
  if (!project || !/^ho-verify-m17-[a-z0-9-]+$/.test(project))
    throw Error("A uniquely identified disposable verification project is required");
  const ids = execFileSync("docker", [
    "ps", "--filter", "label=com.docker.compose.project=" + project,
    "--filter", "label=com.docker.compose.service=api", "--format", "{{.ID}}"
  ], { encoding: "utf8", timeout: 10000 }).trim().split(/\s+/).filter(Boolean);
  if (ids.length !== 1) throw Error("Exactly one owned API container is required");
  const row = execFileSync("docker",
    ["stats", "--no-stream", "--format", "{{json .}}", ids[0]],
    { encoding: "utf8", timeout: 12000 }).trim();
  const stats = JSON.parse(row);
  const used = bytesInMib(stats.MemUsage.split("/")[0]);
  if (!Number.isFinite(used) || used < 0) throw Error("Invalid memory reading");
  return { epoch_seconds: Date.now() / 1000, service: "api", used_mib: used };
}

async function grafanaQuery(page, endpoint, params) {
  const response = await page.request.get(proxy + endpoint, { params });
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.status).toBe("success");
  // /query returns {result:[...]}; /series returns the label-set array itself.
  return Array.isArray(body.data) ? body.data : body.data.result;
}

test("ten thousand hostile HTTP requests remain within complete exported-series budget", async ({ page }) => {
  const password = process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
  if (!password) throw Error("Missing disposable Grafana password");
  await page.goto("/login");
  await page.locator('input[name="user"]').fill("local_operator");
  await page.locator('input[name="password"]').fill(password);
  await page.locator('button[type="submit"]').click();
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/);

  const measurements = [memorySnapshot()];
  const started = Math.floor(Date.now() / 1000) - 30;
  const count = 10000;
  const concurrency = 32;
  for (let offset = 0; offset < count; offset += concurrency) {
    const requests = Array.from({ length: Math.min(concurrency, count - offset) }, (_, i) => {
      const n = offset + i;
      return page.request.get(api + "/m17-unknown/" + n, {
        headers: {
          "X-Correlation-ID": "untrusted" + n,
          "X-M17-Canary": "confidential-test-" + n,
          "X-Forwarded-Host": "spoof" + n + ".example.test"
        },
        timeout: 12000
      });
    });
    const responses = await Promise.all(requests);
    for (const response of responses) expect(response.status()).toBe(404);
    if (offset % 992 === 0) measurements.push(memorySnapshot());
  }
  measurements.push(memorySnapshot());

  // Wait for both 15-second OTLP export and Prometheus scrape intervals.
  await expect.poll(async () => {
    const series = await grafanaQuery(page, "/query", {
      query: 'sum(increase(harborops_request_duration_seconds_count{operation="other",status_class="4xx"}[5m]))'
    });
    return series.length ? Number(series[0].value[1]) : 0;
  }, {timeout: 95000, intervals: [5000]}).toBeGreaterThan(0);

  const finished = Math.floor(Date.now() / 1000) + 30;
  // /series over the entire fault window catches transient and disappeared
  // labelsets that an instantaneous query would miss.
  const series = await grafanaQuery(page, "/series", {
    "match[]": '{__name__=~"harborops_.*"}',
    start: String(started),
    end: String(finished)
  });
  expect(series.length).toBeGreaterThan(0);
  // This is stricter than 500 per service: the whole isolated stack must be
  // at or below 500 distinct custom series, even if a worker is also enabled.
  expect(series.length).toBeLessThanOrEqual(500);
  for (const labels of series) {
    for (const key of Object.keys(labels)) {
      expect(key).not.toMatch(/tenant|credential|source_event|receipt|email|organization_id|user_id|shipment_id|correlation_id|cookie/i);
    }
    expect(JSON.stringify(labels)).not.toMatch(/confidential-test-|spoof\d+\.example\.test|untrusted\d+/);
  }

  const peak = Math.max(...measurements.map(x => x.used_mib));
  const baseline = measurements[0].used_mib;
  const report = {
    requested_paths: count, all_requests_returned_404: true,
    exported_series_across_window: series.length, series_budget: 500,
    epoch_start: started, epoch_end: finished, series, memory_samples: measurements,
    memory_api_baseline_mib: baseline, memory_api_peak_mib: peak,
    memory_api_delta_mib: peak - baseline,
    memory_overhead_disposition: "PAIRED_TELEMETRY_OFF_BASELINE_REQUIRED"
  };
  const evidence = process.env.HARBOROPS_G17_EVIDENCE_DIR;
  if (!evidence) throw Error("A source-locked evidence directory is required");
  fs.writeFileSync(path.join(evidence, "m17-cardinality-raw.json"),
    JSON.stringify(report, null, 2) + "\n", {flag:"wx"});
});
