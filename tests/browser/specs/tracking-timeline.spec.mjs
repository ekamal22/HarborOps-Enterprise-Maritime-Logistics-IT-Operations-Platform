import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M5_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

test("G10 shipment detail shows separate observed tracking and manual workflow", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-analyst@example.test");
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Analyst)" });
  const shipments = page.getByRole("heading", { name: "Shipments", exact: true })
    .locator("xpath=ancestor::section[1]");
  await shipments.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(shipments.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await shipments.getByRole("button", { name: "View shipment" }).first().click();
  const panel = shipments.getByRole("heading", { name: "Tracking observations" })
    .locator("xpath=ancestor::section[1]");
  await expect(panel).toBeVisible();
  await expect(panel.getByText("Derived equipment observations", { exact: false }))
    .toBeVisible();
  await expect(panel.getByText("No ETA can be inferred", { exact: false })).toBeVisible();
  await expect(shipments.getByText("Manual status history")).toBeVisible();
  await expect(panel.getByRole("button", { name: "Refresh tracking" })).toBeEnabled();
});

test("G10 tracking workspace disappears on organization selection change", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-manager@example.test");
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
  const selector = page.locator("#current-organization");
  await selector.selectOption({ label: "M3 North Terminal (Manager)" });
  const shipments = page.getByRole("heading", { name: "Shipments", exact: true })
    .locator("xpath=ancestor::section[1]");
  await shipments.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await shipments.getByRole("button", { name: "View shipment" }).first().click();
  await expect(shipments.getByRole("heading", { name: "Tracking observations" })).toBeVisible();
  await selector.selectOption({ label: "M3 East Terminal (Manager)" });
  await expect(shipments.getByRole("heading", { name: "Tracking observations" })).toHaveCount(0);
});

test("G10 stale timeline cursor response is visible and refresh restores the page", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-analyst@example.test");
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Analyst)" });
  const shipments = page.getByRole("heading", { name: "Shipments", exact: true })
    .locator("xpath=ancestor::section[1]");
  await shipments.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await shipments.getByRole("button", { name: "View shipment" }).first().click();
  const panel = shipments.getByRole("heading", { name: "Tracking observations" })
    .locator("xpath=ancestor::section[1]");
  await expect(panel).toBeVisible();

  let rejected = false;
  await page.route("**/api/v1/organizations/*/shipments/*/events?*", async route => {
    if (!rejected) {
      rejected = true;
      await route.fulfill({ status: 409, contentType: "application/json",
        body: JSON.stringify({ code: "stale_or_invalid_cursor",
          message: "Tracking history changed" }) });
    } else await route.continue();
  });
  await panel.getByRole("button", { name: "Refresh tracking" }).click();
  await expect(panel.getByRole("alert")).toContainText("Tracking history changed");
  await panel.getByRole("button", { name: "Refresh tracking" }).click();
  await expect(panel.getByRole("alert")).toHaveCount(0);
  expect(rejected).toBe(true);
});

test("G10 access revocation response clears previously visible observed state", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-analyst@example.test");
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Analyst)" });
  const shipments = page.getByRole("heading", { name: "Shipments", exact: true })
    .locator("xpath=ancestor::section[1]");
  await shipments.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await shipments.getByRole("button", { name: "View shipment" }).first().click();
  const panel = shipments.getByRole("heading", { name: "Tracking observations" })
    .locator("xpath=ancestor::section[1]");
  await expect(panel).toBeVisible();

  await page.route("**/api/v1/organizations/*/shipments/*/tracking-projection", route =>
    route.fulfill({ status: 403, contentType: "application/json",
      body: JSON.stringify({ code: "organization_access_revoked",
        message: "Access revoked" }) }));
  await panel.getByRole("button", { name: "Refresh tracking" }).click();
  await expect(panel.getByText(/Observed:\s*/)).toHaveCount(0);
});

const liveEnabled = enabled && process.env.HARBOROPS_M10_FIXTURES === "1";

test("G10 shipment shows real worker-resolved events in order and the materialized observation", async ({ page }) => {
  test.skip(!liveEnabled, "Disposable G10 live fixture required");
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-analyst@example.test");
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Analyst)" });
  const shipments = page.getByRole("heading", { name: "Shipments", exact: true })
    .locator("xpath=ancestor::section[1]");
  await shipments.getByLabel("Search shipments").fill("G10-LIVE-NORTH");
  await expect(shipments.getByText("G10-LIVE-NORTH", { exact: true })).toBeVisible();
  await shipments.getByRole("button", { name: "View shipment" }).first().click();
  const panel = shipments.getByRole("heading", { name: "Tracking observations" })
    .locator("xpath=ancestor::section[1]");
  await expect(panel).toBeVisible();
  await expect(panel.locator("p", { hasText: "Observed:" })).toContainText("EquipmentOut");
  await expect(panel.locator("p", { hasText: "Materialization:" })).toContainText(/Materialization: [1-9]/);
  await expect(panel.locator("p", { hasText: "Materialization:" })).not.toContainText("rebuild pending");
  const items = panel.locator("ol > li");
  await expect(items).toHaveCount(2);
  await expect(items.nth(0)).toContainText("GTIN");
  await expect(items.nth(0)).toContainText("g10-browser-in-");
  await expect(items.nth(1)).toContainText("GTOT");
  await expect(items.nth(1)).toContainText("g10-browser-out-");
  await expect(panel.getByText("No ETA can be inferred", { exact: false })).toBeVisible();
  await expect(panel.getByRole("button", { name: /rebuild|correct/i })).toHaveCount(0);
  await expect(shipments.getByText("Manual status history")).toBeVisible();
});
