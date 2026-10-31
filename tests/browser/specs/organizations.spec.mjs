import { test, expect } from "@playwright/test";

const email = "operator@example.test";
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
  await expect(page.getByText("operator@example.test")).toBeVisible();
  await page.reload();
  await expect(page.getByText("Slug: north-terminal")).toBeVisible();
  await page.locator("#current-organization").selectOption("");
  await expect(page.getByText("Slug: north-terminal")).toHaveCount(0);
  const context = await page.request.get("/api/v1/me/organization-context");
  expect(context.status()).toBe(200);
  expect((await context.json()).selected).toBeNull();
});

test("foreign tenant selection and last administrator disablement are denied", async ({ page }) => {
  test.skip(!password, "Disposable synthetic account is required");
  await signIn(page);
  await page.locator("#current-organization").selectOption({ label: "North Terminal (Administrator)" });
  const current = await page.request.get("/api/v1/me/organizations");
  const list = await current.json();
  const orgId = list.organizations[0].id;
  const foreign = await page.request.get("/api/v1/organizations/00000000-0000-4000-8000-000000000999");
  expect(foreign.status()).toBe(404);
  const profile = await page.request.get("/api/v1/organizations/" + orgId);
  expect(profile.status()).toBe(200);
  const account = page.getByLabel("Password confirmation to disable account");
  await account.fill(password);
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Disable my account" }).click();
  await expect(page.getByRole("alert")).toContainText("Assign another active administrator");
  await expect(page.getByText("Signed in as")).toBeVisible();
});
