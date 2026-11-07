import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M12_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

async function login(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}

const shipments = page => page.getByRole("heading", { name: "Shipments", exact: true })
  .locator("xpath=ancestor::section[1]");

test("M12 advanced search keeps shipment draft intact", async ({ page }) => {
  test.skip(!enabled, "Disposable M12 fixture required");
  await login(page, "m3-manager@example.test");
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  const s = shipments(page);
  await expect(s.getByText("Advanced shipment search")).toBeVisible();
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await s.getByLabel("Shipment display name").fill("Unsubmitted local change");
  await expect(s.getByText("Unsaved changes")).toBeVisible();

  await s.locator("summary").filter({ hasText: "Advanced shipment search" }).click();
  await s.getByLabel("Current voyage UUID").fill("00000000-0000-0000-0000-000000000001");
  await s.getByRole("button", { name: "Apply advanced filters" }).click();
  await expect(s.getByText("No shipments match this filter.")).toBeVisible();
  await expect(s.getByLabel("Shipment display name")).toHaveValue("Unsubmitted local change");
  await s.getByRole("button", { name: "Clear advanced filters" }).click();
  await expect(s.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await expect(s.getByLabel("Shipment display name")).toHaveValue("Unsubmitted local change");
});

test("M12 authorized export returns a downloadable CSV", async ({ page }) => {
  test.skip(!enabled, "Disposable restricted-role M12 fixture required");
  await login(page, "m3-manager@example.test");
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  const s = shipments(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  const downloadEvent = page.waitForEvent("download");
  await s.getByRole("button", { name: "Export matching shipments as CSV" }).click();
  const download = await downloadEvent;
  expect(download.suggestedFilename()).toMatch(/^shipments-\d{8}T\d{6}Z\.csv$/);
  await expect(s.getByRole("alert")).toHaveCount(0);
});

test("M12 Auditor can search but cannot request export in the UI", async ({ page }) => {
  test.skip(!enabled, "Disposable M12 fixture required");
  await login(page, "m4-auditor@example.test");
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Auditor)"
  });
  const s = shipments(page);
  await expect(s.getByText("Advanced shipment search")).toBeVisible();
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await expect(s.getByRole("button", { name: "Export matching shipments as CSV" })).toHaveCount(0);
});
