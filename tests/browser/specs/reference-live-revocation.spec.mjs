import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M3_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);
const base = "http://localhost:5173";

async function login(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}

test("revocation removes management controls using real HTTP permissions", async ({ page, browser }, info) => {
  test.skip(!enabled, "Isolated G3 fixture required");
  test.setTimeout(120_000);
  const engine = info.project.name;
  const label = "M3 Revocation " + engine;
  await login(page, "m3-manager@example.test");
  await page.locator("#current-organization").selectOption({ label: label + " (Manager)" });
  await expect(page.getByRole("heading", { name: "Reference data" })).toBeVisible();
  const who = await page.request.get("/api/v1/auth/session");
  expect(who.status()).toBe(200);
  const managerId = (await who.json()).user.id;
  const mine = await page.request.get("/api/v1/me/organizations");
  const organization = (await mine.json()).organizations.find(x =>
    x.slug === "m3-revocation-" + engine);
  expect(organization).toBeTruthy();
  const root = "/api/v1/organizations/" + organization.id;

  const token = (await (await page.request.get("/api/v1/auth/csrf")).json()).token;
  const created = await page.request.post(root + "/ports", {
    headers: { Origin: base, "X-HarborOps-CSRF": token },
    data: { referenceCode: "REVOCATION-PORT", name: "Revocation protected port",
      countryCode: "US" }
  });
  expect(created.status()).toBe(201);
  await page.getByRole("button", { name: "Refresh references" }).click();
  await expect(page.getByText("Revocation protected port")).toBeVisible();

  const adminContext = await browser.newContext({ baseURL: base });
  const administrator = await adminContext.newPage();
  try {
    await login(administrator, "m3-admin-a@example.test");
    // The selection is stored server-side; wait for it before calling organization-scoped APIs.
    const selected = administrator.waitForResponse(response =>
      response.request().method() === "PUT" &&
      new URL(response.url()).pathname === "/api/v1/auth/session/organization");
    await administrator.locator("#current-organization")
      .selectOption({ label: label + " (Administrator)" });
    expect((await selected).status()).toBe(200);
    const roster = await administrator.request.get(root + "/memberships?limit=100");
    expect(roster.status()).toBe(200);
    const member = (await roster.json()).memberships.find(x => x.userId === managerId);
    expect(member?.role).toBe("Manager");
    let intercepted;
    let release;
    const queued = new Promise(resolve => { intercepted = resolve; });
    const permit = new Promise(resolve => { release = resolve; });
    await page.route("**/api/v1/organizations/*/ports?*", async route => {
      intercepted();
      await permit;
      await route.continue();
    });
    await page.getByRole("button", { name: "Refresh references" }).click();
    await queued;
    const csrf = (await (await administrator.request.get("/api/v1/auth/csrf")).json()).token;
    const downgraded = await administrator.request.put(
      root + "/memberships/" + managerId, {
        headers: { Origin: base, "X-HarborOps-CSRF": csrf },
        data: { role: "Support", status: "Active", expectedVersion: member.version }
      });
    expect(downgraded.status()).toBe(200);
    release();
    await expect(page.getByRole("heading", { name: "Reference data" })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Add port" })).toHaveCount(0);
    await expect(page.getByText("Revocation protected port")).toHaveCount(0);
    await page.unrouteAll({ behavior: "wait" });
    const csrfAgain = (await (await administrator.request.get("/api/v1/auth/csrf")).json()).token;
    const restored = await administrator.request.put(root + "/memberships/" + managerId, {
      headers: { Origin: base, "X-HarborOps-CSRF": csrfAgain },
      data: { role: "Manager", status: "Active", expectedVersion: member.version + 1 }
    });
    expect(restored.status()).toBe(200);
  } finally {
    await adminContext.close();
  }
});
