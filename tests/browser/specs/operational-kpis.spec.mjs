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
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
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
