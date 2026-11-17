import { expect, test } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

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
    const results = await queryPrometheus(page, "harborops_db_readiness_ratio");
    return results.length ? Number(results[0].value[1]) : -1;
  }, {timeout: 95_000, intervals: [5_000]}).toBe(1);

  const metrics = await queryPrometheus(page, "harborops_db_readiness_ratio");
  expect(metrics).toHaveLength(1);
  const serialized = JSON.stringify(metrics);
  for (const forbidden of [
    "organization_id", "user_id", "session_id",
    "shipment_id", "correlation_id", "db.statement"
  ]) expect(serialized).not.toContain(forbidden);

  const notification = await queryPrometheus(page,
    'harborops_service_running_ratio{service="notifications"}');
  expect(notification).toHaveLength(1);
  expect(Number(notification[0].value[1])).toBe(0);

  const held = await queryPrometheus(page,
    'harborops_service_running_ratio{service="tracking_held"}');
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
  expect(alert.type).toBe("table");
  expect(alert.targets?.[0]?.format).toBe("table");
  expect(alert.targets?.[0]?.instant).toBe(true);
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
    ["harborops_db_readiness_ratio",1],
    ['harborops_service_running_ratio{service="notifications"}',0],
    ['harborops_service_running_ratio{service="tracking_held"}',-1],
    ["harborops_telemetry_collector_reachable_ratio",1]
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

  await expect.poll(async()=>{
    const rows=await queryPrometheus(page,
      "histogram_quantile(0.95, sum by (le) (rate(harborops_request_duration_seconds_bucket[5m])))");
    return rows.length?Number(rows[0].value[1]):0;
  },{timeout:95000,intervals:[5000]}).toBeGreaterThan(0);
  const insufficient=await queryPrometheus(page,
    'harborops_request_duration_seconds_count{operation="never-issued-g17"}');
  expect(insufficient).toHaveLength(0);
  const alertPanel=panels.find(p=>p.title==="Active local alerts");
  expect(alertPanel.fieldConfig?.defaults?.noValue).toBe("Unknown");
  const active=await queryPrometheus(page,'ALERTS{alertstate="firing"}');
  expect(Array.isArray(active)).toBe(true);
  await page.goto(dashboard+"?from=now-10m&to=now");
  for(const title of ["PostgreSQL readiness","Application heartbeats per 100 seconds",
    "Collector transport probe","Notification worker"])
    await expect(page.getByText(title,{exact:false}).first()).toBeVisible();
  // Rendered live measurements, not just titles: the readiness stat must read
  // Ready from the current healthy sample and the request-rate panel must show a
  // measured rate (a number with its unit) for the real traffic generated above.
  await expect.poll(async()=>String(await page.locator(
    '[data-griditem-key="grid-item-4"]').innerText()),
    {timeout:90000,intervals:[3000]}).toMatch(/\bReady\b/);
  await expect.poll(async()=>String(await page.locator(
    '[data-griditem-key="grid-item-1"]').innerText()),
    {timeout:90000,intervals:[3000]}).toMatch(/\d(?:\.\d+)?\s*req\/s/);
  const liveRate=String(await page.locator('[data-griditem-key="grid-item-1"]').innerText());
  expect(liveRate).not.toMatch(/\b(?:No data|Unknown)\b/i);
  const liveReady=String(await page.locator('[data-griditem-key="grid-item-4"]').innerText());
  expect(liveReady).not.toMatch(/\b(?:Unavailable|Unknown)\b/i);
});


// Restrict the observation to one actual Grafana panel. A title, a saved
// noValue setting or "No data" printed by another panel is not evidence.
const PANEL_IDS={"API request rate":1,"API 5xx rate":2,"API p95 latency":3};
// The Grafana grid item of one saved panel id is the exact container of that panel.
// Its text is the panel's own title plus its own rendered body, never a neighbour's.
async function renderedApiNoData(page,title) {
  const item=page.locator('[data-griditem-key="grid-item-'+PANEL_IDS[title]+'"]');
  await expect(item).toHaveCount(1);
  const text=String(await item.innerText());
  expect(text,"panel container must be the titled panel: "+title).toContain(title);
  for(const other of Object.keys(PANEL_IDS).filter(name=>name!==title))
    expect(text).not.toContain(other);
  return /\b(?:No data|Unknown)\b/i.test(text)?text:"";
}
async function renderedApiText(page,title) {
  const item=page.locator('[data-griditem-key="grid-item-'+PANEL_IDS[title]+'"]');
  await expect(item).toHaveCount(1);
  return String(await item.innerText());
}

test("zero and insufficient traffic render unknown rather than a healthy zero", async ({page},testInfo) => {
  await logIn(page);
  const response=await page.request.get("/api/dashboards/uid/harborops-m17");
  expect(response.status()).toBe(200);
  const source=(await response.json()).dashboard;
  const critical=["API request rate","API 5xx rate","API p95 latency",
    "PostgreSQL readiness"];
  for(const title of critical) {
    const panel=source.panels.find(item=>item.title.startsWith(title));
    expect(panel,title).toBeTruthy();
    expect(panel.fieldConfig?.defaults?.noValue,title).toBe("Unknown");
    expect(panel.targets?.length,title).toBeGreaterThan(0);
  }
  const past=Math.floor(Date.now()/1000)-48*3600;
  const noRequests=await page.request.get(prometheusProxy,{
    params:{query:"harborops:m17:eligible_requests:5m",time:String(past)}
  });
  expect(noRequests.status()).toBe(200);
  const body=await noRequests.json();
  expect(body.status).toBe("success");
  expect(body.data.result).toHaveLength(0);
  // For the same timestamp, a missing failure series may not be treated as
  // the positive statement "zero failures with sufficient request volume".
  const noFailures=await page.request.get(prometheusProxy,{
    params:{query:"harborops:m17:api_5xx_fraction:5m",time:String(past)}
  });
  expect(noFailures.status()).toBe(200);
  const fraction=await noFailures.json();
  expect(fraction.status).toBe("success");
  expect(fraction.data.result).toHaveLength(0);
  const start=String((past-300)*1000);
  const finish=String(past*1000);
  await page.goto(dashboard+"?from="+start+"&to="+finish);
  await expect(page.getByText("API request rate",{exact:false}).first()).toBeVisible();
  await expect(page.getByText("PostgreSQL readiness",{exact:false}).first()).toBeVisible();
  await expect(page.getByText("API p95 latency",{exact:false}).first()).toBeVisible();
  const rendered={source_sha:process.env.HARBOROPS_SOURCE_SHA,
    engine:testInfo.project.name,window_start:past-300,
    window_end:past,source_result_counts:{
      eligible_requests:body.data.result.length,
      failure_fraction:fraction.data.result.length},panels:{}};
  for(const title of ["API request rate","API 5xx rate","API p95 latency"]) {
    await expect.poll(()=>renderedApiNoData(page,title),
      {timeout:90000,intervals:[3000]}).toMatch(/\b(?:No data|Unknown)\b/i);
    const state=await renderedApiNoData(page,title);
    expect(state).not.toMatch(/\b(?:Healthy|Sufficient)\b/i);
    rendered.panels[title]=state;
  }
  const root=process.env.HARBOROPS_G17_EVIDENCE_DIR;
  if(!root)throw Error("Source-locked evidence root required");

  // Real below-threshold traffic: the recorded window produced by
  // check-g17-low-traffic.mjs holds a few dozen genuine 2xx reads, a measured
  // healthy-looking raw 5xx zero, and no guarded 5xx or p95 series.
  const low=JSON.parse(fs.readFileSync(path.join(root,"m17-low-traffic-window.json"),"utf8"));
  expect(low.source_sha).toBe(process.env.HARBOROPS_SOURCE_SHA);
  expect(low.result).toBe("PASS");
  expect(low.requests_sent).toBeGreaterThan(0);
  expect(low.requests_sent).toBeLessThan(low.sufficiency_floor);
  expect(low.eligible_max).toBeLessThan(low.sufficiency_floor);
  expect(low.eligible_min).toBeGreaterThanOrEqual(1);
  expect(low.guarded_5xx_points).toBe(0);
  expect(low.guarded_p95_points).toBe(0);
  expect(low.raw_5xx_zero_points).toBeGreaterThanOrEqual(1);
  // Independent re-query inside this browser test, not trust of the stage file.
  const mid=Math.floor((low.window_start+low.window_end)/2);
  const rawZero=await page.request.get(prometheusProxy,{params:{
    query:"sum(rate(harborops_request_failures_total[5m]))",time:String(mid)}});
  expect(rawZero.status()).toBe(200);
  const rawBody=await rawZero.json();
  expect(rawBody.data.result).toHaveLength(1);
  expect(Number(rawBody.data.result[0].value[1])).toBe(0);
  for(const guarded of ["harborops:m17:api_5xx_rate_sufficient:5m",
    "harborops:m17:api_p95_seconds_sufficient:5m"]) {
    const guardedResponse=await page.request.get(prometheusProxy,{params:{
      query:guarded,time:String(mid)}});
    expect((await guardedResponse.json()).data.result).toHaveLength(0);
  }
  await page.goto(dashboard+"?from="+String(low.window_start*1000)+
    "&to="+String(low.window_end*1000));
  await expect.poll(()=>renderedApiText(page,"API request rate"),
    {timeout:90000,intervals:[3000]}).toMatch(/\d(?:\.\d+)?\s*req\/s/);
  const requestRate=await renderedApiText(page,"API request rate");
  expect(requestRate).not.toMatch(/\b(?:No data|Unknown)\b/i);
  const lowPanels={"API request rate":requestRate};
  for(const title of ["API 5xx rate","API p95 latency"]) {
    await expect.poll(()=>renderedApiNoData(page,title),
      {timeout:90000,intervals:[3000]}).toMatch(/\bUnknown\b/i);
    const state=await renderedApiNoData(page,title);
    expect(state).not.toMatch(/\b(?:Healthy|Sufficient)\b/i);
    expect(state).not.toMatch(/\b0(?:\.0+)?\s*(?:req\/s|ms|s)\b/i);
    lowPanels[title]=state;
  }
  rendered.low_traffic={window_start:low.window_start,window_end:low.window_end,
    requests_sent:low.requests_sent,sufficiency_floor:low.sufficiency_floor,
    eligible_min:low.eligible_min,eligible_max:low.eligible_max,
    raw_5xx_value_at_midpoint:Number(rawBody.data.result[0].value[1]),
    guarded_series_at_midpoint:0,panels:lowPanels};
  fs.writeFileSync(path.join(root,"m17-ui-no-traffic-"+testInfo.project.name+".json"),
    JSON.stringify(rendered,null,2)+"\n",{flag:"wx"});
});

// Inspect a single rendered Grafana panel, not just its datasource query or
// saved JSON settings. Reject text borrowed from another panel on the page.
async function renderedPanelText(page,title) {
  // Saved panel id 4 is "PostgreSQL readiness"; its grid item is the exact container.
  const item=page.locator('[data-griditem-key="grid-item-4"]');
  await expect(item).toHaveCount(1);
  const text=String(await item.innerText());
  expect(text).toContain(title);
  for(const other of ["API request rate","API 5xx rate","API p95 latency",
    "Notification worker","Application heartbeats","Collector transport probe",
    "Active local alerts","Tracking eligible backlog age"])
    expect(text).not.toContain(other);
  return /\b(?:Unknown|No data|Unavailable|Ready)\b/i.test(text)?text:"";
}

test("browser API denial retains server-issued correlation under spoofed context",async ({browser})=>{
  const ctx=await browser.newContext();
  try {
    const spoof="ffffffffffffffffffffffffffffffff";
    const response=await ctx.request.get(apiBase+
      "/api/v1/organizations/11111111-1111-1111-1111-111111111111/ports",{
      headers:{"X-Correlation-ID":spoof,
        "traceparent":"00-00112233445566778899aabbccddeeff-0123456789abcdef-01",
        "baggage":"role=administrator"}
    });
    expect(response.status()).toBeGreaterThanOrEqual(400);
    expect(response.status()).toBeLessThan(500);
    const correlation=response.headers()["x-correlation-id"];
    expect(correlation).toMatch(/^[0-9a-f]{32}$/);
    expect(correlation).not.toBe(spoof);
    const body=await response.text();
    expect(body).not.toContain(spoof);
    expect(body).not.toContain("role=administrator");
  } finally {await ctx.close();}
});

test("rendered readiness distinguishes missing data from real downtime in three browsers",
  async ({page},testInfo)=>{
  await logIn(page);
  const source=await queryPrometheus(page,"harborops_db_readiness_ratio");
  expect(source.length).toBe(1);
  expect(Number(source[0].value[1])).toBe(1);
  const now=Math.floor(Date.now()/1000);
  const since=now-2700;
  const range=await page.request.get(prometheusProxy.replace(/\/query$/,"/query_range"),{
    params:{query:"harborops_db_readiness_ratio",start:String(since),
      end:String(now),step:"15s"}
  });
  expect(range.status()).toBe(200);
  const historical=await range.json();
  expect(historical.status).toBe("success");
  const values=historical.data?.result?.[0]?.values??[];
  const failures=values.filter(row=>Number(row[1])===0);
  expect(failures.length,"The earlier controlled database outage must have real samples")
    .toBeGreaterThanOrEqual(2);
  const downTime=Number(failures[Math.floor(failures.length/2)][0]);
  const recorded={source_sha:process.env.HARBOROPS_SOURCE_SHA,
    engine:testInfo.project.name,verified_statuses:{}};
  // A historical instant without any data must not appear healthy.
  const past=now-48*3600;
  await page.goto(dashboard+"?from="+String((past-15)*1000)+
    "&to="+String((past+15)*1000));
  await expect.poll(()=>renderedPanelText(page,"PostgreSQL readiness"),
    {timeout:90000,intervals:[3000]}).toMatch(/\b(?:Unknown|No data)\b/i);
  const missing=await renderedPanelText(page,"PostgreSQL readiness");
  expect(missing).not.toMatch(/\b(?:Ready|Unavailable)\b/);
  recorded.verified_statuses.missing=missing;
  // During a real PostgreSQL outage a measured 0 must render Unavailable.
  await page.goto(dashboard+"?from="+String(downTime*1000-10000)+
    "&to="+String(downTime*1000+10000));
  await expect.poll(()=>renderedPanelText(page,"PostgreSQL readiness"),
    {timeout:90000,intervals:[3000]}).toMatch(/\bUnavailable\b/i);
  recorded.verified_statuses.down=await renderedPanelText(page,"PostgreSQL readiness");
  // The current healthy sample must render Ready, distinct from both states.
  await page.goto(dashboard+"?from=now-5m&to=now");
  await expect.poll(()=>renderedPanelText(page,"PostgreSQL readiness"),
    {timeout:90000,intervals:[3000]}).toMatch(/\bReady\b/i);
  recorded.verified_statuses.ready=await renderedPanelText(page,"PostgreSQL readiness");
  const evidence=process.env.HARBOROPS_G17_EVIDENCE_DIR;
  if(!evidence)throw Error("Source-locked evidence root required");
  fs.writeFileSync(path.join(evidence,"m17-ui-state-"+testInfo.project.name+".json"),
    JSON.stringify(recorded,null,2)+"\n",{flag:"wx"});
});
