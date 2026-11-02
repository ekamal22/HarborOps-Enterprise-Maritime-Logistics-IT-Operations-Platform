import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M6_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

async function login(page, role = "analyst") {
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-" + role + "@example.test");
  await page.getByLabel("Password", { exact: true }).fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (" + (role === "analyst" ? "Analyst" : "Manager") + ")" });
}
const section = page => page.getByRole("heading", { name: "Shipments", exact: true })
  .locator("xpath=ancestor::section[1]");
const assignment = (page, type) => page.getByRole("heading", { name: type + " assignments" })
  .locator("xpath=ancestor::div[contains(@class,'rounded')][1]");

test("G6 Analyst attaches container and voyage and retains the shipment draft", async ({ page }) => {
  test.skip(!enabled, "Disposable M6 fixture required");
  test.setTimeout(120_000);
  await login(page);
  const org = await page.locator("#current-organization").inputValue();
  const root = "/api/v1/organizations/" + org;
  const voyageResponse = await page.request.get(root + "/voyages?recordStatus=Active&limit=100");
  const voyageData = await voyageResponse.json();
  const voyage = (voyageData.voyages ?? []).find(x =>
    (x.voyageStatus === "Planned" || x.voyageStatus === "Underway") &&
    x.originPortId && x.destinationPortId);
  const containerResponse = await page.request.get(root + "/containers?recordStatus=Active&limit=100");
  const containerData = await containerResponse.json();
  const container = (containerData.containers ?? [])[0];
  expect(voyage, "M6 fixture needs route-compatible voyage").toBeTruthy();
  expect(container, "M6 fixture needs active container").toBeTruthy();

  const s = section(page);
  await s.getByRole("button", { name: "Create shipment" }).click();
  const code = "G6-" + randomUUID().replaceAll("-", "").slice(0, 10).toUpperCase();
  await s.getByLabel("Shipment reference code").fill(code);
  await s.getByLabel("Origin port").selectOption(voyage.originPortId);
  await s.getByLabel("Destination port").selectOption(voyage.destinationPortId);
  await s.getByRole("button", { name: "Save new shipment" }).click();
  await expect(s.getByRole("button", { name: "Close detail" })).toHaveCount(0);
  await s.getByLabel("Search shipments").fill(code);
  await expect(s.getByText(code, { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await expect(s.getByRole("heading", { name: "Shipment allocations" })).toBeVisible();

  const boxes = assignment(page, "Container");
  await boxes.getByLabel("Assign container").selectOption(container.id);
  await boxes.getByLabel("Start Container assignment UTC").fill("2026-11-10T10:00:00Z");
  await boxes.getByRole("button", { name: "Attach container" }).click();
  await expect(boxes.getByText(/From 2026-11-10T10:00:00/)).toBeVisible();

  const voyages = assignment(page, "Voyage");
  await voyages.getByLabel("Assign voyage").selectOption(voyage.id);
  await voyages.getByLabel("Start Voyage assignment UTC").fill("2026-11-10T10:00:00Z");
  await voyages.getByRole("button", { name: "Attach voyage" }).click();
  await expect(voyages.getByText(/From 2026-11-10T10:00:00/)).toBeVisible();

  await s.getByLabel("Shipment display name").fill("Unsaved G6 edit");
  await boxes.getByRole("button", { name: "Refresh assignments" }).click();
  await expect(s.getByLabel("Shipment display name")).toHaveValue("Unsaved G6 edit");

  await boxes.getByRole("button", { name: "Close assignment" }).click();
  await boxes.getByLabel("End Container assignment UTC").fill("2026-11-10T11:00:00Z");
  await boxes.getByRole("button", { name: "Confirm closure" }).click();
  await expect(boxes.getByText(/to 2026-11-10T11:00:00/)).toBeVisible();

  await voyages.getByRole("button", { name: "Close assignment" }).click();
  await voyages.getByLabel("End Voyage assignment UTC").fill("2026-11-10T11:00:00Z");
  await voyages.getByRole("button", { name: "Confirm closure" }).click();
  await expect(voyages.getByText(/to 2026-11-10T11:00:00/)).toBeVisible();
  await expect(s.getByLabel("Shipment display name")).toHaveValue("Unsaved G6 edit");
});

test("G6 organization switch invalidates shipment allocation panels", async ({ page }) => {
  test.skip(!enabled, "Disposable M6 fixture required");
  await login(page, "manager");
  const selector = page.locator("#current-organization");
  const s = section(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await expect(s.getByRole("heading", { name: "Shipment allocations" })).toBeVisible();
  await selector.selectOption({ label: "M3 East Terminal (Manager)" });
  await expect(section(page).getByRole("heading", { name: "Shipment allocations" })).toHaveCount(0);
  await expect(section(page).getByText("M5-DEMO-NORTH", { exact: true })).toHaveCount(0);
});
