import { expect, test } from "@playwright/test";

const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
const fixtures = process.env.HARBOROPS_M3_FIXTURES === "1";

async function login(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  const result = page.waitForResponse(response =>
    response.request().method() === "POST" &&
    new URL(response.url()).pathname === "/api/v1/auth/login");
  await page.getByRole("button", { name: "Sign in" }).click();
  expect((await result).status()).toBe(200);
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}

test("Manager port and vessel create, edit, search, archive and restore", async ({ page }) => {
  test.setTimeout(120_000);
  test.skip(!password || !fixtures, "Requires isolated M3 local verification fixtures");
  await login(page, "m3-manager@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Manager)" });
  await expect(page.getByRole("heading", { name: "Reference data" })).toBeVisible();
  const nonce = Math.random().toString(36).slice(2, 10).toUpperCase();
  const code = "PORT-" + nonce;
  await page.getByRole("button", { name: "Add port" }).click();
  await page.getByLabel("Reference code").fill(code);
  await page.getByLabel("Name", { exact: true }).fill("Synthetic Test Port " + nonce);
  await page.getByLabel("Country code").fill("US");
  await page.getByLabel("UN/LOCODE (optional)").fill("");
  await page.getByRole("button", { name: "Create reference" }).click();
  await expect(page.getByText("Synthetic Test Port " + nonce)).toBeVisible();
  await page.getByLabel("Search ports").fill(code);
  await expect(page.getByText("Synthetic Test Port " + nonce)).toBeVisible();
  await page.getByRole("button", { name: "View or edit" }).first().click();
  await page.getByLabel("Name", { exact: true }).fill("Updated Synthetic Port " + nonce);
  await page.getByRole("button", { name: "Save reference" }).click();
  await expect(page.getByText("Updated Synthetic Port " + nonce)).toBeVisible();
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Archive" }).first().click();
  await expect(page.getByText("Updated Synthetic Port " + nonce)).toHaveCount(0);
  await page.getByLabel("Reference status").selectOption("Archived");
  await expect(page.getByText("Updated Synthetic Port " + nonce)).toBeVisible();
  await page.getByRole("button", { name: "Restore" }).first().click();
  await page.getByLabel("Reference status").selectOption("Active");
  await expect(page.getByText("Updated Synthetic Port " + nonce)).toBeVisible();

  await page.getByRole("button", { name: "Vessels", exact: true }).click();
  await page.getByRole("button", { name: "Add vessel" }).click();
  await page.getByLabel("Reference code").fill("SHIP-" + nonce);
  await page.getByLabel("Name", { exact: true }).fill("Synthetic Test Vessel " + nonce);
  await page.getByLabel("IMO number (optional)").fill("");
  await page.getByLabel("MMSI (optional)").fill("");
  await page.getByLabel("Flag country (optional)").fill("BD");
  await page.getByRole("button", { name: "Create reference" }).click();
  await expect(page.getByText("Synthetic Test Vessel " + nonce)).toBeVisible();
});

test("Analyst can read M3 references but cannot create or edit", async ({ page }) => {
  test.setTimeout(90_000);
  test.skip(!password || !fixtures, "Requires isolated M3 local verification fixtures");
  await login(page, "m3-analyst@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Analyst)" });
  await expect(page.getByRole("heading", { name: "Reference data" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Add port" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Create reference" })).toHaveCount(0);
  const orgs = await page.request.get("/api/v1/me/organizations");
  const row = (await orgs.json()).organizations.find(o => o.slug === "m3-north");
  const data = await page.request.get("/api/v1/organizations/" + row.id + "/ports");
  expect(data.status()).toBe(200);
  expect((await data.json()).ports).toBeInstanceOf(Array);
});

test("Organization switch discards previous tenant reference rows", async ({ page }) => {
  test.setTimeout(90_000);
  test.skip(!password || !fixtures, "Requires isolated M3 local verification fixtures");
  await login(page, "m3-manager@example.test");
  const select = page.locator("#current-organization");
  await select.selectOption({ label: "M3 North Terminal (Manager)" });
  await expect(page.getByRole("heading", { name: "Reference data" })).toBeVisible();
  await select.selectOption({ label: "M3 East Terminal (Manager)" });
  await expect(page.getByText("M3 East Terminal", { exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Reference data" })).toBeVisible();
  const orgs = await page.request.get("/api/v1/me/organizations");
  const rows = (await orgs.json()).organizations;
  const a = rows.find(o => o.slug === "m3-north");
  const b = rows.find(o => o.slug === "m3-east");
  const denied = await page.request.get("/api/v1/organizations/" + a.id + "/ports");
  expect(denied.status()).toBe(404);
  const allowed = await page.request.get("/api/v1/organizations/" + b.id + "/ports");
  expect(allowed.status()).toBe(200);
});
