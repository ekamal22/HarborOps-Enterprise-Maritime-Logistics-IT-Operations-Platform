import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M3_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

async function login(page) {
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-manager@example.test");
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}

test("delayed old-tenant port response never appears after organization switch", async ({ page }) => {
  test.skip(!enabled, "M3 isolated browser fixture required");
  test.setTimeout(120_000);
  await login(page);
  const selected = page.locator("#current-organization");
  await selected.selectOption({ label: "M3 North Terminal (Manager)" });
  await expect(page.getByText("Synthetic Primary Port North")).toBeVisible();

  const organizations = (await (await page.request.get("/api/v1/me/organizations")).json()).organizations;
  const north = organizations.find(o => o.slug === "m3-north");
  if (!north) throw new Error("Missing North fixture");
  let release;
  let intercepted;
  const held = new Promise(resolve => { intercepted = resolve; });
  const permit = new Promise(resolve => { release = resolve; });
  await page.route("**/api/v1/organizations/*/ports?*", async route => {
    if (!route.request().url().includes(north.id)) {
      await route.continue();
      return;
    }
    intercepted();
    await permit;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        ports: [{ id: crypto.randomUUID(), organizationId: north.id,
          referenceCode: "DELAYED-NORTH", name: "Delayed North Confidential",
          status: "Active", version: 1 }],
        nextAfter: null
      })
    });
  });

  await page.getByRole("button", { name: "Refresh references" }).click();
  await held;
  await selected.selectOption({ label: "M3 East Terminal (Manager)" });
  await expect(page.getByText("Synthetic Primary Port East")).toBeVisible();
  release();
  await expect(page.getByText("Delayed North Confidential")).toHaveCount(0);
  await expect(page.getByText("Synthetic Primary Port North")).toHaveCount(0);
  await page.unrouteAll({ behavior: "wait" });
});

test("reference list clears on dependency failure, then recovers without cached rows", async ({ page }) => {
  test.skip(!enabled, "M3 isolated browser fixture required");
  await login(page);
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Manager)" });
  await expect(page.getByText("Synthetic Primary Port North")).toBeVisible();
  await page.route("**/api/v1/organizations/*/ports?*", route => route.fulfill({
    status: 503,
    contentType: "application/json",
    body: JSON.stringify({
      code: "dependency_unavailable",
      message: "Dependency temporarily unavailable",
      correlationId: "synthetic-503"
    })
  }));
  await page.getByRole("button", { name: "Refresh references" }).click();
  await expect(page.getByRole("alert")).toContainText("temporarily unavailable");
  await expect(page.getByText("Synthetic Primary Port North")).toHaveCount(0);
  await page.unrouteAll();
  await page.getByRole("button", { name: "Refresh references" }).click();
  await expect(page.getByText("Synthetic Primary Port North")).toBeVisible();
});

test("logging out during a held reference request clears protected state", async ({ page }) => {
  test.skip(!enabled, "M3 isolated browser fixture required");
  test.setTimeout(120_000);
  await login(page);
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Manager)" });
  await expect(page.getByText("Synthetic Primary Port North")).toBeVisible();
  let intercepted;
  let release;
  const entered = new Promise(resolve => { intercepted = resolve; });
  const suspended = new Promise(resolve => { release = resolve; });
  await page.route("**/api/v1/organizations/*/ports?*", async route => {
    intercepted();
    await suspended;
    await route.fulfill({
      status: 200, contentType: "application/json",
      body: JSON.stringify({ ports: [{ id: crypto.randomUUID(),
        referenceCode: "DELAYED-SECRET", name: "Private after logout",
        status: "Active", version: 1 }], nextAfter: null })
    });
  });
  await page.getByRole("button", { name: "Refresh references" }).click();
  await entered;
  await page.getByRole("button", { name: "Log out", exact: true }).click();
  await expect(page.getByRole("button", { name: "Sign in" })).toBeVisible();
  release();
  await expect(page.getByText("Private after logout")).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Reference data" })).toHaveCount(0);
  await page.unrouteAll({ behavior: "wait" });
});

test("revocation during a reference refresh clears protected rows", async ({ page }) => {
  test.skip(!enabled, "M3 isolated browser fixture required");
  await login(page);
  await page.locator("#current-organization")
    .selectOption({ label: "M3 North Terminal (Manager)" });
  await expect(page.getByText("Synthetic Primary Port North")).toBeVisible();
  await page.route("**/api/v1/organizations/*/ports?*", route => route.fulfill({
    status: 403,
    contentType: "application/json",
    body: JSON.stringify({
      code: "organization_access_revoked",
      message: "Organization access is no longer available",
      correlationId: "synthetic-revocation"
    })
  }));
  await page.getByRole("button", { name: "Refresh references" }).click();
  await expect(page.getByRole("alert")).toContainText("access has changed");
  await expect(page.getByText("Synthetic Primary Port North")).toHaveCount(0);
  await page.unrouteAll();
});
