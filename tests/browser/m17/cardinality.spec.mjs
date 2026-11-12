import { test, expect } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

const api = process.env.HARBOROPS_G17_API_URL || "http://127.0.0.1:18080";
const url = "/api/datasources/proxy/uid/harborops-local-metrics/api/v1/query";

test("ten thousand hostile paths stay within the finite exported-series budget", async ({ page }) => {
  const password = process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
  if (!password) throw Error("Missing private disposable Grafana password");
  await page.goto("/login");
  await page.locator('input[name="user"]').fill("local_operator");
  await page.locator('input[name="password"]').fill(password);
  await page.locator('button[type="submit"]').click();

  // All traffic is unauthenticated GET against unmatched routes. No accepted
  // business state, tenant identity, retained record or user credential changes.
  const total = 10_000;
  const concurrency = 32;
  for (let index = 0; index < total; index += concurrency) {
    const batch = Array.from({length: Math.min(concurrency, total - index)}, (_, i) => {
      const id = index + i;
      return page.request.get(api + "/m17-unknown/" + id, {
        headers: {
          "X-Correlation-ID": "spoof" + id,
          "X-M17-Canary": "secret-canary-" + id,
          "X-Forwarded-Host": "fake" + id + ".example.test"
        },
        timeout: 10_000
      });
    });
    const responses = await Promise.all(batch);
    for (const response of responses) expect(response.status()).toBe(404);
  }

  async function metrics() {
    const response = await page.request.get(url, {
      params: {query: '{__name__=~"harborops_.*"}'}
    });
    expect(response.status()).toBe(200);
    const data = await response.json();
    expect(data.status).toBe("success");
    return data.data.result;
  }

  await expect.poll(async () => (await metrics()).length,
    {timeout: 90_000, intervals: [5_000]}).toBeGreaterThan(0);
  const samples = await metrics();
  expect(samples.length).toBeLessThanOrEqual(500);
  const forbiddenKeys = [
    "organization_id", "user_id", "session_id", "shipment_id",
    "correlation_id", "url_full", "db_statement", "cookie"
  ];
  for (const sample of samples) {
    for (const key of Object.keys(sample.metric)) {
      expect(forbiddenKeys).not.toContain(key);
      expect(key).not.toMatch(/tenant|credential|source_event|receipt|email/i);
    }
    const serialized = JSON.stringify(sample.metric);
    expect(serialized).not.toMatch(/secret-canary-|fake\d+\.example\.test|spoof\d+/);
  }

  if (process.env.HARBOROPS_G17_EVIDENCE_DIR) {
    fs.writeFileSync(path.join(process.env.HARBOROPS_G17_EVIDENCE_DIR,
      "m17-cardinality-raw.json"), JSON.stringify({
        checked: total,
        sample_series: samples.length,
        samples
      }, null, 2) + "\n", {flag:"wx"});
  }
});
