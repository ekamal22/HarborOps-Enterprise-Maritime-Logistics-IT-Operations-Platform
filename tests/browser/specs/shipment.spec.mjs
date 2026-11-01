import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M5_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);
async function login(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}
const sectionOf = page => page.getByRole("heading", { name: "Shipments", exact: true })
  .locator("xpath=ancestor::section[1]");
test("M5 Analyst creates and advances a scoped shipment", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(120_000);
  await login(page, "m3-analyst@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Analyst)" });
  const s = sectionOf(page);
  await expect(s.getByRole("button", { name: "Create shipment" })).toBeEnabled();
  await s.getByRole("button", { name: "Create shipment" }).click();
  const suffix = Math.random().toString(36).slice(2, 10).toUpperCase();
  const code = "M5-" + suffix;
  await s.getByLabel("Shipment reference code").fill(code);
  await s.getByLabel("Shipment display name").fill("Synthetic M5 " + suffix);
  await s.getByLabel("Origin port").selectOption({ index: 1 });
  await s.getByLabel("Destination port").selectOption({ index: 2 });
  await s.getByRole("button", { name: "Save new shipment" }).click();
  await s.getByLabel("Search shipments").fill(code);
  await expect(s.getByText(code, { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await expect(s.getByText("Manual status history")).toBeVisible();
  await s.getByLabel("Shipment workflow").selectOption("Booked");
  await s.getByRole("button", { name: "Save shipment changes" }).click();
  await s.getByLabel("Search shipments").fill(code);
  await expect(s.getByText("Booked", { exact: false })).toBeVisible();
  page.once("dialog", dialog => dialog.accept());
  await s.getByRole("button", { name: "Archive shipment" }).first().click();
  await expect(s.getByText(code, { exact: true })).toHaveCount(0);
  await s.getByLabel("Shipment record status").selectOption("Archived");
  await expect(s.getByText(code, { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "Restore shipment" }).first().click();
  await s.getByLabel("Shipment record status").selectOption("Active");
  await expect(s.getByText(code, { exact: true })).toBeVisible();
});
test("M5 Auditor shipment view does not grant M4 management", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  await login(page, "m4-auditor@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Auditor)" });
  const s = sectionOf(page);
  await expect(s).toBeVisible();
  await expect(s.getByRole("button", { name: "Create shipment" })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Voyages and containers" })).toHaveCount(0);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await expect(s.getByRole("button", { name: "Save shipment changes" })).toHaveCount(0);
});
test("M5 tenant change invalidates prior shipment list", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  await login(page, "m3-manager@example.test");
  const select = page.locator("#current-organization");
  await select.selectOption({ label: "M3 North Terminal (Manager)" });
  const s = sectionOf(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await select.selectOption({ label: "M3 East Terminal (Manager)" });
  const east = sectionOf(page);
  await east.getByLabel("Search shipments").fill("M5-DEMO-EAST");
  await expect(east.getByText("M5-DEMO-EAST", { exact: true })).toBeVisible();
  await expect(east.getByText("M5-DEMO-NORTH", { exact: true })).toHaveCount(0);
});
