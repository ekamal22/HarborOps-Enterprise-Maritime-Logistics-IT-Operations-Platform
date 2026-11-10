import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M13_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

async function login(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}

function dashboard(page) {
  return page.getByRole("region", { name: "Operational KPI dashboard" });
}

test("M13 Manager sees scoped operational reporting and safe drill", async ({ page }) => {
  test.skip(!enabled, "Disposable M13 browser fixture required");
  await login(page, "m3-manager@example.test");
  const overviewResponse = page.waitForResponse(response =>
    response.url().includes("/analytics/overview") &&
    response.request().method() === "GET", { timeout: 30000 });
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  const response = await overviewResponse;
  expect(response.status(), "Real KPI reporting must be admitted").toBe(200);
  const d = dashboard(page);
  await expect(d.getByRole("heading", { name: "Operational KPIs" })).toBeVisible();
  await expect(d.getByText("Current workflow distribution")).toBeVisible();
  await expect(d.getByText("Assessment quality")).toBeVisible();
  await expect(d.getByText("Exception occurrence openings")).toBeVisible();
  await expect(d.getByText(/Current-state evaluation:/)).toBeVisible();
  await expect(d.getByText(/Snapshot expires:/)).toBeVisible();
  await expect(d.getByRole("button", { name: /Strict on-time recorded arrivals/ })).toBeVisible();
  await expect(d.getByRole("button", { name: /Within 60-minute tolerance/ })).toBeVisible();
  await expect(d.getByRole("button", { name: /Schedule overdue, unconfirmed/ })).toBeVisible();
  await expect(d.getByRole("button", { name: /Departure slippage risk/ })).toBeVisible();

  // A new report invalidates the old member set. The drill is scoped to its
  // originating report and current selected organization.
  await d.getByRole("button", { name: /Active shipments/ }).click();
  await expect(d.getByRole("region", { name: "KPI report members" })).toBeVisible();
  await d.getByRole("button", { name: "Refresh report" }).click();
  await expect(d.getByRole("region", { name: "KPI report members" })).toHaveCount(0);
  await expect(d.getByText(/Snapshot expires:/)).toBeVisible();

  // The default window is clear and can be restored after custom filtering.
  await d.getByLabel("From (UTC, inclusive)").fill("2026-11-01");
  await d.getByLabel("Before (UTC, exclusive)").fill("2026-11-09");
  await d.getByLabel("Trend bucket (UTC)").selectOption("week");
  const filteredResponse = page.waitForResponse(response => {
    const url = new URL(response.url());
    return url.pathname.endsWith("/analytics/overview") &&
      url.searchParams.get("fromUtc") === "2026-11-01T00:00:00Z" &&
      url.searchParams.get("beforeUtc") === "2026-11-09T00:00:00Z";
  }, { timeout: 30000 });
  await d.getByRole("button", { name: "Apply filters" }).click();
  const filtered = await filteredResponse;
  expect(filtered.status(), "The combined stack must serve the requested UTC report").toBe(200);
  // Verify both UTC boundaries without assuming an English month/day ordering.
  const displayedUtc = timestamp => new Intl.DateTimeFormat("en", {
    year: "numeric", month: "short", day: "2-digit",
    hour: "2-digit", minute: "2-digit", timeZone: "UTC"
  }).format(new Date(timestamp)) + " UTC";
  const period = d.getByText(/Period:/);
  await expect(period).toContainText(displayedUtc("2026-11-01T00:00:00Z"), { timeout: 30000 });
  await expect(period).toContainText(displayedUtc("2026-11-09T00:00:00Z"));
  await d.getByRole("button", { name: "Reset" }).click();
  await expect(d.getByLabel("From (UTC, inclusive)")).toHaveValue("");
});

test("M13 ungranted Auditor never sees report values or exported data", async ({ page }) => {
  test.skip(!enabled, "Disposable M13 browser fixture required");
  await login(page, "m4-auditor@example.test");
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Auditor)"
  });
  const d = dashboard(page);
  await expect(d.getByRole("heading", { name: "Operational KPIs" })).toBeVisible();
  await expect(d.getByText(/requires an active explicit read-only grant/i)).toBeVisible();
  await expect(d.getByText(/Current-state evaluation:/)).toHaveCount(0);
  await expect(d.getByRole("button", { name: /Active shipments/ })).toHaveCount(0);
});

test("M13 Administrator has grant management but not KPI viewing", async ({ page }) => {
  test.skip(!enabled, "Disposable M13 browser fixture required");
  await login(page, "m3-admin-a@example.test");
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Administrator)"
  });
  await expect(page.getByRole("region", { name: "Operational KPI dashboard" })).toHaveCount(0);
  await expect(page.getByRole("region", { name: "KPI Auditor grants" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Save grant" })).toBeDisabled();
});


test("M13 empty report shows insufficient denominators without false risk trends", async ({ page }) => {
  test.skip(!enabled, "Disposable M13 browser fixture required");
  await login(page, "m3-manager@example.test");
  const now = "2026-11-09T12:00:00Z";
  await page.route("**/analytics/overview*", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({
      policyVersion: "m13-kpi-v1",
      reportId: "e0000000-0000-4000-8000-000000000001",
      reportToken: "disposable-fixture-report-token",
      fromUtc: "2026-11-02T00:00:00Z",
      beforeUtc: "2026-11-09T00:00:00Z",
      evaluatedAtUtc: now, expiresAtUtc: "2026-11-09T12:10:00Z",
      memberCount: 0, bucket: "day", originPortId: null,
      destinationPortId: null, voyageId: null, sourceKind: null, counts: []
    })
  }));
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  const d = dashboard(page);
  await expect(d.getByText(/Current-state evaluation:/)).toBeVisible();
  for (const label of [
    /Strict on-time recorded arrivals/,
    /Within 60-minute tolerance/,
    /Evaluator coverage/
  ]) {
    await expect(d.getByRole("button", { name: label })
      .getByText("Not enough data", { exact: true })).toBeVisible();
  }
  await expect(d.getByText("No recorded openings in this window.")).toBeVisible();
  await expect(d.getByText("Unarchived shipments only. Total 0.")).toBeVisible();
});

test("M13 failed report clears old values and a retry builds a fresh snapshot", async ({ page }) => {
  test.skip(!enabled, "Disposable M13 browser fixture required");
  await login(page, "m3-manager@example.test");
  let available = false;
  await page.route("**/analytics/overview*", route => route.fulfill({
    status: available ? 200 : 503, contentType: "application/json",
    body: JSON.stringify(available ? {
      policyVersion: "m13-kpi-v1",
      reportId: "e0000000-0000-4000-8000-000000000002",
      reportToken: "disposable-fixture-report-token",
      fromUtc: "2026-11-02T00:00:00Z",
      beforeUtc: "2026-11-09T00:00:00Z",
      evaluatedAtUtc: "2026-11-09T12:00:00Z",
      expiresAtUtc: "2026-11-09T12:10:00Z",
      memberCount: 0, bucket: "day", counts: []
    } : { code: "report_unavailable", message: "Unable to build report" })
  }));
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  const d = dashboard(page);
  await expect(d.getByRole("alert")).toBeVisible();
  await expect(d.getByText(/Current-state evaluation:/)).toHaveCount(0);
  available = true;
  const refresh = d.getByRole("button", { name: "Refresh report" });
  await expect(refresh).toBeEnabled();
  await refresh.click();
  await expect(d.getByRole("alert")).toHaveCount(0);
  await expect(d.getByText(/Current-state evaluation:/)).toBeVisible();
});


test("M13 ratio rendering uses exact member numerators and half-up percentages", async ({ page }) => {
  test.skip(!enabled, "Disposable M13 browser fixture required");
  await login(page, "m3-manager@example.test");
  await page.route("**/analytics/overview*", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({
      policyVersion: "m13-kpi-v1",
      reportId: "e0000000-0000-4000-8000-000000000006",
      reportToken: "disposable-fixture-report-token",
      fromUtc: "2026-11-02T00:00:00Z",
      beforeUtc: "2026-11-09T00:00:00Z",
      evaluatedAtUtc: "2026-11-09T12:00:00Z",
      expiresAtUtc: "2026-11-09T12:10:00Z",
      memberCount: 9, bucket: "day",
      counts: [
        { metricCode: "D05", dimension: "", bucketStartUtc: null, count: 3 },
        { metricCode: "K05", dimension: "", bucketStartUtc: null, count: 1 },
        { metricCode: "K06", dimension: "", bucketStartUtc: null, count: 2 },
        { metricCode: "D13", dimension: "", bucketStartUtc: null, count: 9 },
        { metricCode: "K13", dimension: "", bucketStartUtc: null, count: 8 },
        { metricCode: "K14", dimension: "", bucketStartUtc: null, count: 1 }
      ]
    })
  }));
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  const d = dashboard(page);
  await expect(d.getByRole("button", { name: /Strict on-time recorded arrivals/ })
    .getByText("33.33%", { exact: true })).toBeVisible();
  await expect(d.getByRole("button", { name: /Within 60-minute tolerance/ })
    .getByText("66.67%", { exact: true })).toBeVisible();
  await expect(d.getByRole("button", { name: /Evaluator coverage/ })
    .getByText("88.89%", { exact: true })).toBeVisible();
  await expect(d.getByText(/Partial assessment/).first()).toBeVisible();
});


test("M13 expired report drill shows no stale member data and refresh resets the drill", async ({ page }) => {
  test.skip(!enabled, "Disposable M13 browser fixture required");
  await login(page, "m3-manager@example.test");
  await page.route("**/analytics/overview*", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({
      policyVersion: "m13-kpi-v1",
      reportId: "e0000000-0000-4000-8000-000000000008",
      reportToken: "disposable-fixture-expired-token",
      fromUtc: "2026-11-02T00:00:00Z",
      beforeUtc: "2026-11-09T00:00:00Z",
      evaluatedAtUtc: "2026-11-09T12:00:00Z",
      expiresAtUtc: "2026-11-09T12:10:00Z",
      memberCount: 1, bucket: "day",
      counts: [{metricCode:"K01",dimension:"",bucketStartUtc:null,count:1}]
    })
  }));
  await page.route("**/analytics/reports/*/members?*", route => route.fulfill({
    status: 409, contentType: "application/json",
    body: JSON.stringify({code:"report_expired",message:"Report snapshot expired"})
  }));
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  const d = dashboard(page);
  await expect(d.getByRole("button", {name:/Active shipments/})).toBeVisible();
  await d.getByRole("button", {name:/Active shipments/}).click();
  const drill = d.getByRole("region", {name:"KPI report members"});
  await expect(drill.getByRole("alert")).toBeVisible();
  await expect(drill.locator("tbody tr")).toHaveCount(0);
  await d.getByRole("button", {name:"Refresh report"}).click();
  await expect(drill).toHaveCount(0);
});

test("M13 switching organization discards the previous organization's report", async ({ page }) => {
  test.skip(!enabled, "Disposable M13 browser fixture required");
  await login(page, "m3-manager@example.test");
  const options = page.locator("#current-organization option");
  const north = await options.filter({hasText:"M3 North Terminal (Manager)"})
    .getAttribute("value");
  const east = await options.filter({hasText:"M3 East Terminal (Manager)"})
    .getAttribute("value");
  expect(north).toBeTruthy();
  expect(east).toBeTruthy();
  await page.route("**/analytics/overview*", route => {
    const isNorth = route.request().url().includes("/organizations/" + north + "/");
    return route.fulfill({
      status:200,contentType:"application/json",
      body:JSON.stringify({
        policyVersion:"m13-kpi-v1",
        reportId:isNorth ? "e0000000-0000-4000-8000-000000000009"
          : "e0000000-0000-4000-8000-000000000010",
        reportToken:"disposable-fixture-report-token",
        fromUtc:"2026-11-02T00:00:00Z",
        beforeUtc:"2026-11-09T00:00:00Z",
        evaluatedAtUtc:"2026-11-09T12:00:00Z",
        expiresAtUtc:"2026-11-09T12:10:00Z",
        memberCount:isNorth?17:0,bucket:"day",
        counts:isNorth?[{metricCode:"K01",dimension:"",bucketStartUtc:null,count:17}]:[]
      })
    });
  });
  await page.locator("#current-organization").selectOption({
    label:"M3 North Terminal (Manager)"
  });
  let d = dashboard(page);
  await expect(d.getByRole("button",{name:/Active shipments/})
    .getByText("17",{exact:true})).toBeVisible();
  await page.locator("#current-organization").selectOption({
    label:"M3 East Terminal (Manager)"
  });
  d = dashboard(page);
  await expect(d.getByRole("button",{name:/Active shipments/})
    .getByText("0",{exact:true})).toBeVisible();
  await expect(d.getByRole("button",{name:/Active shipments/})
    .getByText("17",{exact:true})).toHaveCount(0);
});
