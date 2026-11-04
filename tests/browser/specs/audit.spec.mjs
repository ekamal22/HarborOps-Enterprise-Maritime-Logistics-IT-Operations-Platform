import { test, expect } from "@playwright/test";

const password = process.env.HARBOROPS_TEST_USER_PASSWORD;

async function login(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  const response = page.waitForResponse(value =>
    value.request().method() === "POST" &&
    new URL(value.url()).pathname === "/api/v1/auth/login");
  await page.getByRole("button", { name: "Sign in" }).click();
  expect((await response).status()).toBe(200);
  await expect(page.getByRole("heading", { name: "Organization workspace" }))
    .toBeVisible({ timeout: 30_000 });
}

async function selectOrganization(page, organizationId, label) {
  // Wait for the persisted selection response, not just the UI select change.
  // Otherwise the following API request can race the selection PUT.
  const selected = page.waitForResponse(response =>
    response.request().method() === "PUT" &&
    new URL(response.url()).pathname === "/api/v1/auth/session/organization");
  await page.locator("#current-organization")
    .selectOption({ label }).catch(async () => {
      await page.locator("#current-organization").selectOption(organizationId);
    });
  expect((await selected).status()).toBe(200);
}

test("auditor may read only the selected tenant's audit entries", async ({ page }) => {
  test.setTimeout(90_000);
  test.skip(!password, "Disposable synthetic user password required");
  await login(page, "m4-auditor@example.test");
  const organizations = await (await page.request.get("/api/v1/me/organizations")).json();
  const north = organizations.organizations.find(x => x.slug === "m3-north");
  const east = organizations.organizations.find(x => x.slug === "m3-east");
  expect(north?.role).toBe("Auditor");
  expect(east?.role).toBe("Auditor");
  await selectOrganization(page, north.id, "M3 North Terminal (Auditor)");
  const root = "/api/v1/organizations/" + north.id + "/audit-events";
  const response = await page.request.get(root + "?limit=25");
  expect(response.status()).toBe(200);
  expect(response.headers()["cache-control"]).toContain("no-store");
  const result = await response.json();
  expect(result.entries.length).toBeGreaterThan(0);
  for (const entry of result.entries) {
    expect(entry.organizationId).toBe(north.id);
    expect(entry.actorSessionId).toBeUndefined();
    expect(JSON.stringify(entry.metadata)).not.toContain("password");
  }
  const first = await page.request.get(root + "/" + result.entries[0].id);
  expect(first.status()).toBe(200);
  expect((await first.json()).organizationId).toBe(north.id);
  const foreign = await page.request.get(
    "/api/v1/organizations/" + east.id + "/audit-events");
  expect(foreign.status()).toBe(404);
  expect((await page.request.get(root + "?limit=101")).status()).toBe(400);
});

test("administrator is not entitled to audit reads", async ({ page }) => {
  test.setTimeout(60_000);
  test.skip(!password, "Disposable synthetic user password required");
  await login(page, "m3-admin-a@example.test");
  const organizations = await (await page.request.get("/api/v1/me/organizations")).json();
  const north = organizations.organizations.find(x => x.slug === "m3-north");
  expect(north?.role).toBe("Administrator");
  await selectOrganization(page, north.id, "M3 North Terminal (Administrator)");
  const blocked = await page.request.get(
    "/api/v1/organizations/" + north.id + "/audit-events");
  expect(blocked.status()).toBe(403);
  expect(blocked.headers()["cache-control"]).toContain("no-store");
  const denied = await blocked.json();
  expect(denied.code).toBe("access_denied");
});

test("anonymous audit discovery returns an authentication challenge", async ({ page }) => {
  await page.goto("/");
  const response = await page.request.get(
    "/api/v1/organizations/00000000-0000-4000-8000-000000000001/audit-events");
  expect(response.status()).toBe(401);
  expect(response.headers()["cache-control"]).toContain("no-store");
});
