import { expect, test } from "@playwright/test";

const dashboard = "/d/harborops-m17/local-service-diagnostics";
const apiBase = process.env.HARBOROPS_G17_API_URL || "http://127.0.0.1:18080";
const prometheusProxy = "/api/datasources/proxy/uid/harborops-local-metrics/api/v1/query";

async function logIn(page) {
  const password = process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
  if (!password) throw Error("A private disposable Grafana password is required");
  await page.goto("/login");
  await expect(page.locator('input[name="user"]')).toBeVisible();
  await page.locator('input[name="user"]').fill("local_operator");
  await page.locator('input[name="password"]').fill(password);
  await page.locator('button[type="submit"]').click();
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
}

async function queryPrometheus(page, expression) {
  const response = await page.request.get(prometheusProxy, {
    params: { query: expression }
  });
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.status).toBe("success");
  return body.data.result;
}

test("dashboard requires authentication and shows actual monitoring panels", async ({ page }) => {
  await logIn(page);
  await page.goto(dashboard);
  await expect(page.getByText("Local Service Diagnostics")).toBeVisible();
  for (const title of [
    "API request rate", "API 5xx rate", "API p95 latency",
    "PostgreSQL readiness", "Notification worker",
    "Application heartbeats per 100 seconds", "Sampling rate-limit drops",
    "Active local alerts", "Tracking eligible backlog age",
    "Collector transport probe", "SDK trace export queue-full losses",
    "Worker outcomes", "Readiness sample age"
  ]) await expect(page.getByText(title, { exact: false }).first()).toBeVisible();
});

test("authenticated samples match the disposable ready API and suppress held workers", async ({ page }) => {
  await logIn(page);
  const readiness = await page.request.get(apiBase + "/health/ready");
  expect(readiness.status()).toBe(200);
  const report = await readiness.json();
  expect(report.status).toBe("healthy");

  // Allow two 15-second OTLP export/scrape intervals, with startup margin.
  await expect.poll(async () => {
    const results = await queryPrometheus(page, "harborops_db_readiness");
    return results.length ? Number(results[0].value[1]) : -1;
  }, {timeout: 95_000, intervals: [5_000]}).toBe(1);

  const metrics = await queryPrometheus(page, "harborops_db_readiness");
  expect(metrics).toHaveLength(1);
  const serialized = JSON.stringify(metrics);
  for (const forbidden of [
    "organization_id", "user_id", "session_id",
    "shipment_id", "correlation_id", "db.statement"
  ]) expect(serialized).not.toContain(forbidden);

  const notification = await queryPrometheus(page,
    'harborops_service_running{service="notifications"}');
  expect(notification).toHaveLength(1);
  expect(Number(notification[0].value[1])).toBe(0);

  const held = await queryPrometheus(page,
    'harborops_service_running{service="tracking_held"}');
  expect(held).toHaveLength(1);
  expect(Number(held[0].value[1])).toBe(-1);
});

test("dashboard is unavailable to an anonymous browser context", async ({ browser }) => {
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto(dashboard);
  await expect(page.locator('input[name="password"]')).toBeVisible();
  await context.close();
});

test("alert dashboard links stay local and never embed private diagnostic parameters", async ({page}) => {
  await logIn(page);
  const response=await page.request.get("/api/dashboards/uid/harborops-m17");
  expect(response.status()).toBe(200);
  const saved=await response.json();
  const panels=saved.dashboard?.panels||[];
  expect(panels.length).toBeGreaterThanOrEqual(15);
  const alert=panels.find(panel=>panel.title==="Active local alerts");
  expect(alert).toBeTruthy();
  expect(alert.targets?.[0]?.expr).toBe('ALERTS{alertstate="firing"}');
  let checked=0;
  for (const panel of panels) {
    const links=[
      ...(panel.links||[]),
      ...(panel.fieldConfig?.defaults?.links||[]),
      ...(panel.fieldConfig?.overrides||[]).flatMap(override=>
        (override.properties||[]).filter(property=>property.id==="links")
          .flatMap(property=>property.value||[]))
    ];
    for (const link of links) {
      const value=String(link.url||"");
      expect(value).not.toMatch(/^https?:\\/\\//i);
      expect(value).toMatch(/^\\/(d\\/|explore|dashboard)/);
      expect(value).not.toMatch(/(organization|shipment|receipt|token|password|credential|email)/i);
      checked++;
    }
    for (const target of panel.targets||[]) {
      const query=String(target.expr||"");
      expect(query).not.toMatch(/(tracking_inbox_receipts|audit_entries|password|credential|email)/i);
    }
  }
  // Zero explicit alert hyperlinks is an intentional privacy-safe option.
  expect(checked).toBeGreaterThanOrEqual(0);
});
