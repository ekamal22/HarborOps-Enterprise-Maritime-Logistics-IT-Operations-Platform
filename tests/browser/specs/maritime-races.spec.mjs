import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M4_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);
async function login(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}
test("M4 delayed old-tenant container response never repaints new organization", async ({ page }) => {
  test.skip(!enabled, "Isolated M4 fixtures required");
  test.setTimeout(120_000);
  await login(page, "m3-manager@example.test");
  const select = page.locator("#current-organization");
  await select.selectOption({ label: "M3 North Terminal (Manager)" });
  const workspace = page.getByRole("heading", { name: "Voyages and containers" })
    .locator("xpath=ancestor::section");
  await workspace.getByRole("button", { name: "Containers", exact: true }).click();
  await expect(workspace.getByText("M4-BOX-NORTH")).toBeVisible();
  const organizations = (await (await page.request.get("/api/v1/me/organizations")).json()).organizations;
  const north = organizations.find(x => x.slug === "m3-north");
  let entered, release;
  const held = new Promise(resolve => { entered = resolve; });
  const permit = new Promise(resolve => { release = resolve; });
  await page.route("**/api/v1/organizations/*/containers?*", async route => {
    if (!route.request().url().includes(north.id)) return route.continue();
    entered();
    await permit;
    await route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ containers: [{
        id: crypto.randomUUID(), organizationId: north.id, referenceCode: "OLD-TENANT-M4",
        identifierKind: "Synthetic", containerIdentifier: "SYN-BOX-STALE01",
        equipmentType: "Dry20", recordStatus: "Active", version: 1
      }], nextAfter: null }) });
  });
  await workspace.getByRole("button", { name: "Refresh maritime records" }).click();
  await held;
  await select.selectOption({ label: "M3 East Terminal (Manager)" });
  release();
  await expect(page.getByText("OLD-TENANT-M4")).toHaveCount(0);
  await expect(page.getByText("M4-BOX-NORTH")).toHaveCount(0);
  const east = page.getByRole("heading", { name: "Voyages and containers" })
    .locator("xpath=ancestor::section");
  await east.getByRole("button", { name: "Containers", exact: true }).click();
  await expect(east.getByText("M4-BOX-EAST")).toBeVisible();
  await page.unrouteAll({ behavior: "wait" });
});
test("M4 reference rows clear on failed request and recover without cache", async ({ page }) => {
  test.skip(!enabled, "Isolated M4 fixtures required");
  await login(page, "m3-manager@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Manager)" });
  const section = page.getByRole("heading", { name: "Voyages and containers" })
    .locator("xpath=ancestor::section");
  await section.getByRole("button", { name: "Containers", exact: true }).click();
  await expect(section.getByText("M4-BOX-NORTH")).toBeVisible();
  await page.route("**/api/v1/organizations/*/containers?*", route => route.fulfill({
    status: 503, contentType: "application/json",
    body: JSON.stringify({ code: "dependency_unavailable",
      message: "Dependency temporarily unavailable", correlationId: "synthetic-m4-503" })
  }));
  await section.getByRole("button", { name: "Refresh maritime records" }).click();
  await expect(section.getByRole("alert")).toContainText("temporarily unavailable");
  await expect(section.getByText("M4-BOX-NORTH")).toHaveCount(0);
  await page.unrouteAll();
  await section.getByRole("button", { name: "Refresh maritime records" }).click();
  await expect(section.getByText("M4-BOX-NORTH")).toBeVisible();
});
test("M4 real Manager role revocation clears browser management controls", async ({ page, browser }, info) => {
  test.skip(!enabled, "Isolated M4 fixtures required");
  test.setTimeout(120_000);
  await login(page, "m3-manager@example.test");
  const organization = "M3 Revocation " + info.project.name;
  await page.locator("#current-organization")
    .selectOption({ label: organization + " (Manager)" });
  await expect(page.getByRole("heading", { name: "Voyages and containers" })).toBeVisible();
  const session = await page.request.get("/api/v1/auth/session");
  const userId = (await session.json()).user.id;
  const organizations = (await (await page.request.get("/api/v1/me/organizations")).json()).organizations;
  const org = organizations.find(x => x.slug === "m3-revocation-" + info.project.name);
  const root = "/api/v1/organizations/" + org.id;
  const adminContext = await browser.newContext({ baseURL: "http://localhost:5173" });
  try {
    const administrator = await adminContext.newPage();
    await login(administrator, "m3-admin-a@example.test");
    await administrator.locator("#current-organization")
      .selectOption({ label: organization + " (Administrator)" });
    const roster = await administrator.request.get(root + "/memberships?limit=100");
    const member = (await roster.json()).memberships.find(x => x.userId === userId);
    expect(member?.role).toBe("Manager");
    const csrf = (await (await administrator.request.get("/api/v1/auth/csrf")).json()).token;
    const changed = await administrator.request.put(root + "/memberships/" + userId, {
      headers: { Origin: "http://localhost:5173", "X-HarborOps-CSRF": csrf },
      data: { role: "Support", status: "Active", expectedVersion: member.version }
    });
    expect(changed.status()).toBe(200);
    await page.getByRole("button", { name: "Refresh maritime records" }).click();
    await expect(page.getByRole("heading", { name: "Voyages and containers" })).toHaveCount(0);
    const second = (await (await administrator.request.get("/api/v1/auth/csrf")).json()).token;
    const restored = await administrator.request.put(root + "/memberships/" + userId, {
      headers: { Origin: "http://localhost:5173", "X-HarborOps-CSRF": second },
      data: { role: "Manager", status: "Active", expectedVersion: member.version + 1 }
    });
    expect(restored.status()).toBe(200);
  } finally { await adminContext.close(); }
});
