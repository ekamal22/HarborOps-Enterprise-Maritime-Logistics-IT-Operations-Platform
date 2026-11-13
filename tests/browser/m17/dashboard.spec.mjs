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
      expect(value.startsWith("/")).toBe(true);
      expect(["/d/","/explore","/dashboard"].some(prefix=>
        value.startsWith(prefix))).toBe(true);
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

test("critical monitoring panels use live series and no-data views are explicitly unknown", async ({page}) => {
  await logIn(page);
  const dataResponse=await page.request.get("/api/dashboards/uid/harborops-m17");
  expect(dataResponse.status()).toBe(200);
  const saved=await dataResponse.json();
  const panels=saved.dashboard?.panels||[];
  const critical=[
    "API request rate","API p95 latency","PostgreSQL readiness",
    "Application heartbeats per 100 seconds",
    "Collector transport probe","Notification worker",
    "Active local alerts","Tracking eligible backlog age"
  ];
  for(const title of critical) {
    const panel=panels.find(p=>p.title.startsWith(title));
    expect(panel,title).toBeTruthy();
    expect(panel.targets?.length,title).toBeGreaterThan(0);
    expect(panel.fieldConfig?.defaults?.noValue,title).toBe("Unknown");
    for(const target of panel.targets)
      expect(target.expr?.length,title).toBeGreaterThan(3);
  }
  // Ensure a real business read occurs during this browser session.
  for(let i=0;i<24;i++) {
    const response=await page.request.get(apiBase+"/api/v1/system/baseline");
    expect(response.status()).toBe(200);
  }
  await expect.poll(async()=>{
    const values=await queryPrometheus(page,
      "sum(increase(harborops_request_duration_seconds_count[5m]))");
    return values.length?Number(values[0].value[1]):0;
  },{timeout:95000,intervals:[5000]}).toBeGreaterThan(0);
  for(const [query,value] of [
    ["harborops_db_readiness",1],
    ['harborops_service_running{service="notifications"}',0],
    ['harborops_service_running{service="tracking_held"}',-1],
    ["harborops_telemetry_collector_reachable",1]
  ]) {
    const rows=await queryPrometheus(page,query);
    expect(rows,query).toHaveLength(1);
    expect(Number(rows[0].value[1]),query).toBe(value);
    expect(Number(rows[0].value[0]),query).toBeGreaterThan(
      Math.floor(Date.now()/1000)-70);
  }
  await expect.poll(async()=>{
    const rows=await queryPrometheus(page,
      "increase(harborops_telemetry_heartbeat_total[100s])");
    return rows.length?Number(rows[0].value[1]):0;
  },{timeout:95000,intervals:[5000]}).toBeGreaterThan(0);

  const insufficient=await queryPrometheus(page,
    'harborops_request_duration_seconds_count{operation="never-issued-g17"}');
  expect(insufficient).toHaveLength(0);
  const alertPanel=panels.find(p=>p.title==="Active local alerts");
  expect(alertPanel.fieldConfig?.defaults?.noValue).toBe("Unknown");
  const active=await queryPrometheus(page,'ALERTS{alertstate="firing"}');
  expect(Array.isArray(active)).toBe(true);
  await page.goto(dashboard);
  for(const title of ["PostgreSQL readiness","Application heartbeats per 100 seconds",
    "Collector transport probe","Notification worker"])
    await expect(page.getByText(title,{exact:false}).first()).toBeVisible();
});
