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
