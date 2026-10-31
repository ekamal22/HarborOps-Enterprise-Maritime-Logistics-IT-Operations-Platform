import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M4_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);
async function login(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  const response = page.waitForResponse(r =>
    r.request().method() === "POST" && new URL(r.url()).pathname === "/api/v1/auth/login");
  await page.getByRole("button", { name: "Sign in" }).click();
  expect((await response).status()).toBe(200);
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}
test("M4 Manager container lifecycle retains synthetic identity", async ({ page }) => {
  test.skip(!enabled, "Disposable M4 fixtures required");
  test.setTimeout(120_000);
  await login(page, "m3-manager@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Manager)" });
  await page.getByRole("button", { name: "Containers", exact: true }).click();
  const code = Math.random().toString(36).slice(2,10).toUpperCase();
  await page.getByRole("button", { name: "Add container" }).click();
  const section = page.getByRole("heading", { name: "Voyages and containers" }).locator("xpath=ancestor::section");
  await section.getByLabel("Reference code").fill("M4-" + code);
  await section.getByLabel("Identifier kind").selectOption("Synthetic");
  await section.getByLabel("Container identifier").fill("SYN-BOX-" + code);
  await section.getByLabel("Equipment type").selectOption("Reefer40");
  await section.getByLabel("Display name").fill("Synthetic M4 box");
  await section.getByRole("button", { name: "Create record" }).click();
  await expect(section.getByText("SYN-BOX-" + code)).toBeVisible();
  await section.getByLabel("Search containers").fill(code);
  await expect(section.getByText("SYN-BOX-" + code)).toBeVisible();
  await section.getByRole("button", { name: "View or edit" }).first().click();
  await section.getByLabel("Display name").fill("Updated synthetic box");
  await section.getByRole("button", { name: "Save changes" }).click();
  page.once("dialog", dialog => dialog.accept());
  await section.getByRole("button", { name: "Archive" }).first().click();
  await expect(section.getByText("SYN-BOX-" + code)).toHaveCount(0);
  await section.getByLabel("Record status").selectOption("Archived");
  await expect(section.getByText("SYN-BOX-" + code)).toBeVisible();
  await section.getByRole("button", { name: "Restore" }).first().click();
  await section.getByLabel("Record status").selectOption("Active");
  await expect(section.getByText("SYN-BOX-" + code)).toBeVisible();
});
test("M4 Analyst sees maritime records without mutation controls", async ({ page }) => {
  test.skip(!enabled, "Disposable M4 fixtures required");
  await login(page, "m3-analyst@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Analyst)" });
  const section = page.getByRole("heading", { name: "Voyages and containers" }).locator("xpath=ancestor::section");
  await expect(section.getByRole("button", { name: "Add voyage" })).toHaveCount(0);
  await section.getByRole("button", { name: "Containers", exact: true }).click();
  await expect(section.getByRole("button", { name: "Add container" })).toHaveCount(0);
  const orgs = (await (await page.request.get("/api/v1/me/organizations")).json()).organizations;
  const org = orgs.find(x => x.slug === "m3-north");
  const list = await page.request.get("/api/v1/organizations/" + org.id + "/containers");
  expect(list.status()).toBe(200);
});
test("M4 tenant switch drops former tenant maritime content", async ({ page }) => {
  test.skip(!enabled, "Disposable M4 fixtures required");
  await login(page, "m3-manager@example.test");
  const select = page.locator("#current-organization");
  await select.selectOption({ label: "M3 North Terminal (Manager)" });
  const section = page.getByRole("heading", { name: "Voyages and containers" }).locator("xpath=ancestor::section");
  await section.getByRole("button", { name: "Containers", exact: true }).click();
  await expect(section).toBeVisible();
  await select.selectOption({ label: "M3 East Terminal (Manager)" });
  const orgs = (await (await page.request.get("/api/v1/me/organizations")).json()).organizations;
  const north = orgs.find(o => o.slug === "m3-north");
  const east = orgs.find(o => o.slug === "m3-east");
  expect((await page.request.get("/api/v1/organizations/" + north.id + "/containers")).status()).toBe(404);
  expect((await page.request.get("/api/v1/organizations/" + east.id + "/containers")).status()).toBe(200);
});
