import { test, expect } from "@playwright/test";

const email = "m2-operator@example.test";
const password = process.env.HARBOROPS_TEST_USER_PASSWORD;

async function signIn(page) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Signed in as")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}

test("active organization can be selected, reloaded, and cleared", async ({ page }) => {
  test.skip(!password, "Disposable synthetic account is required");
  await signIn(page);
  const options = page.locator("#current-organization");
  await expect(options).toContainText("North Terminal (Administrator)");
  await options.selectOption({ label: "North Terminal (Administrator)" });
  await expect(page.getByText("Slug: north-terminal")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Memberships" })).toBeVisible();
  await expect(page.getByRole("list").getByText("m2-operator@example.test")).toBeVisible();
  await page.reload();
  await expect(page.getByText("Slug: north-terminal")).toBeVisible();
  // Run the foreign-ID and last-Administrator checks in the same authenticated
  // browser session, avoiding redundant logins against the per-IP security limit.
  const current = await page.request.get("/api/v1/me/organizations");
  const list = await current.json();
  const orgId = list.organizations.find
