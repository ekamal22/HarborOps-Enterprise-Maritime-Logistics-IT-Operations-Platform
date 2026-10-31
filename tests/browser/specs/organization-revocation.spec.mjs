import { test, expect } from "@playwright/test";

const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
const adminEmail = "m2-operator@example.test";
const otherAdminEmail = "m2-second-admin@example.test";

async function signIn(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Signed in as")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}

async function addRole(page, organization, userId, email, role) {
  await page.locator("#current-organization").selectOption({ label: organization });
  await expect(page.getByRole("heading", { name: "Memberships" })).toBeVisible();
  await page.getByLabel("Existing account ID").fill(userId);
  await page.locator("form").filter({
    has: page.getByRole("button", { name: "Add member" })
  }).locator("select").selectOption(role);
  await page.getByRole("button", { name: "Add member" }).click();
  await expect(page.getByRole("list").getByText(email)).toBeVisible();
}

test("membership switching and live revocation clear protected browser state", async ({ page, browser }, testInfo) => {
  test.setTimeout(120_000);
  test.skip(!password, "Disposable synthetic browser accounts are required");
  const projectName = testInfo.project.name;
  const memberEmail = "m2-browser-" + projectName + "@example.test";
  const memberName = "Synthetic Browser " +
    projectName[0].toUpperCase() + projectName.slice(1) + " Member";
  const adminContext = await browser.newContext();
  const otherContext = await browser.newContext();
  const admin = await adminContext.newPage();
  const other = await otherContext.newPage();
  try {
    await signIn(page, memberEmail);
    const session = await page.request.get("/api/v1/auth/session");
    expect(session.status()).toBe(200);
    const userId = (await session.json()).user.id;
    await expect(page.getByText("No active organization membership")).toBeVisible();

    await signIn(admin, adminEmail);
    await addRole(admin, "North Terminal (Administrator)", userId, memberEmail, "Analyst");
    await signIn(other, otherAdminEmail);
    await addRole(other, "East Terminal (Administrator)", userId, memberEmail, "Manager");

    await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));
    await expect(page.locator("#current-organization")).toContainText("North Terminal (Analyst)");
    await expect(page.locator("#current-organization")).toContainText("East Terminal (Manager)");
    await page.locator("#current-organization").selectOption({ label: "East Terminal (Manager)" });
    await expect(page.getByText("Slug: east-terminal")).toBeVisible();
    await expect(page.getByText("Role: Manager")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Memberships" })).toHaveCount(0);

    await page.locator("#current-organization").selectOption({ label: "North Terminal (Analyst)" });
    await expect(page.getByText("Slug: north-terminal")).toBeVisible();
    await expect(page.getByText("Role: Analyst")).toBeVisible();

    // The select value is local state, so wait for the server to persist the change
    // before the member's page is asked to observe it.
    await admin.getByLabel("Membership state for " + memberName).selectOption("Suspended");
    const saved = admin.waitForResponse(response =>
      response.request().method() === "PUT" &&
      response.url().includes("/api/v1/organizations/") &&
      response.url().endsWith("/memberships/" + userId));
    await admin.getByRole("listitem").filter({
      has: admin.getByText(memberEmail, { exact: true })
    }).getByRole("button", { name: "Save membership" }).click();
    expect((await saved).status()).toBe(200);
    await expect(admin.getByLabel("Membership state for " + memberName)).toHaveValue("Suspended");

    await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));
    await expect(page.getByText("Slug: north-terminal")).toHaveCount(0);
    await expect(page.locator("#current-organization")).not.toContainText("North Terminal (Analyst)");
    await expect(page.locator("#current-organization")).toContainText("East Terminal (Manager)");
    await page.locator("#current-organization").selectOption({ label: "East Terminal (Manager)" });
    await expect(page.getByText("Slug: east-terminal")).toBeVisible();
    await expect(page.getByText("Role: Manager")).toBeVisible();

    await page.reload();
    await expect(page.getByText("Slug: east-terminal")).toBeVisible();
  } finally {
    await adminContext.close();
    await otherContext.close();
  }
});
