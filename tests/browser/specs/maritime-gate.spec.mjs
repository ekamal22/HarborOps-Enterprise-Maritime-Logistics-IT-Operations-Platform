import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M4_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

async function login(page, email = "m3-manager@example.test") {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}

const sectionOf = page => page.getByRole("heading", {
  name: "Voyages and containers"
}).locator("xpath=ancestor::section");

test("M4 container pagination preserves tenant results across multiple pages", async ({ page }) => {
  test.skip(!enabled, "Disposable M4 fixtures required");
  test.setTimeout(180_000);
  await login(page);
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Manager)" });
  const section = sectionOf(page);
  await section.getByRole("button", { name: "Containers", exact: true }).click();
  const organizations = (await (await page.request.get("/api/v1/me/organizations")).json()).organizations;
  const organization = organizations.find(item => item.slug === "m3-north");
  expect(organization?.id).toBeTruthy();
  const token = (await (await page.request.get("/api/v1/auth/csrf")).json()).token;
  const prefix = "G4P" + Date.now().toString(36).toUpperCase();
  for (let i = 0; i < 27; i++) {
    const suffix = String(i).padStart(3, "0");
    const created = await page.request.post(
      "/api/v1/organizations/" + organization.id + "/containers", {
        headers: { Origin: "http://localhost:5173", "X-HarborOps-CSRF": token },
        data: {
          referenceCode: prefix + "-" + suffix,
          identifierKind: "Synthetic",
          containerIdentifier: "SYN-BOX-" + prefix + suffix,
          equipmentType: i % 2 ? "Reefer40" : "Dry20"
        }
      });
    expect(created.status()).toBe(201);
  }
  await section.getByLabel("Search containers").fill(prefix);
  await expect(section.locator("ul > li")).toHaveCount(25);
  await section.getByRole("button", { name: "Load more maritime records" }).click();
  await expect(section.locator("ul > li")).toHaveCount(27);
  const codes = await section.locator("ul > li > div:first-child > div:first-child").allTextContents();
  expect(new Set(codes).size).toBe(27);
  expect(codes.every(code => code.startsWith(prefix))).toBeTruthy();
  await expect(section.getByRole("button", { name: "Load more maritime records" })).toHaveCount(0);
});

test("M4 logging out during delayed maritime response never reveals stale data", async ({ page }) => {
  test.skip(!enabled, "Disposable M4 fixtures required");
  test.setTimeout(120_000);
  await login(page);
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Manager)" });
  const section = sectionOf(page);
  await section.getByRole("button", { name: "Containers", exact: true }).click();
  await expect(section.getByText("M4-BOX-NORTH")).toBeVisible();
  let arrived, release;
  const waiting = new Promise(resolve => { arrived = resolve; });
  const permit = new Promise(resolve => { release = resolve; });
  await page.route("**/api/v1/organizations/*/containers?*", async route => {
    arrived();
    await permit;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        containers: [{
          id: "11111111-1111-4111-8111-111111111111",
          organizationId: "11111111-1111-4111-8111-111111111112",
          referenceCode: "G4-LOGOUT-STALE",
          identifierKind: "Synthetic",
          containerIdentifier: "SYN-BOX-LOGOUTSTALE",
          equipmentType: "Dry20", recordStatus: "Active", version: 1
        }],
        nextAfter: null
      })
    });
  });
  await section.getByRole("button", { name: "Refresh maritime records" }).click();
  await waiting;
  await page.getByRole("button", { name: "Log out", exact: true }).click();
  await expect(page.getByRole("button", { name: "Sign in" })).toBeVisible();
  release();
  await expect(page.getByText("G4-LOGOUT-STALE")).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Voyages and containers" })).toHaveCount(0);
  await page.unrouteAll({ behavior: "wait" });
});

test("M4 invalid container identifier is rejected and the form recovers", async ({ page }) => {
  test.skip(!enabled, "Disposable M4 fixtures required");
  test.setTimeout(120_000);
  await login(page);
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Manager)" });
  const section = sectionOf(page);
  await section.getByRole("button", { name: "Containers", exact: true }).click();
  await section.getByRole("button", { name: "Add container" }).click();
  const nonce = Date.now().toString(36).toUpperCase();
  await section.getByLabel("Reference code").fill("G4-ISO-" + nonce);
  await section.getByLabel("Identifier kind").selectOption("ISO6346");
  await section.getByLabel("Container identifier").fill("CSQU3054382");
  await section.getByRole("button", { name: "Create record" }).click();
  await expect(section.getByRole("alert")).toBeVisible();
  await expect(section.getByText("CSQU3054382")).toHaveCount(0);
  await section.getByRole("button", { name: "Add container" }).click();
  await section.getByLabel("Reference code").fill("G4-ISO-" + nonce);
  await section.getByLabel("Identifier kind").selectOption("ISO6346");
  await section.getByLabel("Container identifier").fill("CSQU3054383");
  await section.getByRole("button", { name: "Create record" }).click();
  await expect(section.getByText("CSQU3054383")).toBeVisible();
});
