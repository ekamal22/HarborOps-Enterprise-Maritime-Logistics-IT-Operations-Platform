import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M5_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

async function openShipment(page, role) {
  await page.goto("/");
  await page.getByLabel("Email").fill(
    role === "Auditor" ? "m4-auditor@example.test" : `m3-${role.toLowerCase()}@example.test`);
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" }))
    .toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: `M3 North Terminal (${role})` });
  const shipment = page.getByRole("heading", { name: "Shipments", exact: true })
    .locator("xpath=ancestor::section[1]");
  await shipment.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(shipment.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await shipment.getByRole("button", { name: "View shipment" }).first().click();
  return shipment.getByRole("region", { name: "Shipment exceptions" });
}

test("G11 read-only Analyst sees uncertainty and cannot disposition exceptions", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Analyst");
  await expect(panel).toBeVisible();
  await expect(panel.getByText("no ETA is calculated", { exact: false })).toBeVisible();
  await expect(panel.getByRole("button", { name: "Reevaluate rules" })).toHaveCount(0);
  await expect(panel.getByRole("button", { name: "Acknowledge" })).toHaveCount(0);
  await expect(panel.getByRole("button", { name: "Resolve" })).toHaveCount(0);
});

test("G11 Manager sees scoped reevaluation control and status without claimed ETA", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Manager");
  await expect(panel).toBeVisible();
  await expect(panel.getByRole("button", { name: "Reevaluate rules" })).toBeEnabled();
  await expect(panel.getByText("These rule outcomes use manual workflow records", {
    exact: false })).toBeVisible();
  await expect(panel.getByRole("button", { name: "Refresh exceptions" })).toBeEnabled();
});

test("G11 exception panel cannot survive organization switch", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Manager");
  await expect(panel).toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: "M3 East Terminal (Manager)" });
  await expect(panel).toHaveCount(0);
});

test("G11 a revoked exception read clears stale results", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Analyst");
  await expect(panel).toBeVisible();
  await page.route("**/api/v1/organizations/*/shipments/*/exceptions?*", route =>
    route.fulfill({ status: 403, contentType: "application/json",
      body: JSON.stringify({ code: "organization_access_revoked",
        message: "Access revoked" }) }));
  await panel.getByRole("button", { name: "Refresh exceptions" }).click();
  await expect(page.getByRole("region", { name: "Shipment exceptions" })).toHaveCount(0);
});
