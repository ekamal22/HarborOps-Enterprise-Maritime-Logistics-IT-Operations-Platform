import { test, expect } from "@playwright/test";

const email = "m2-operator@example.test";
const password = process.env.HARBOROPS_TEST_USER_PASSWORD;

async function signIn(page) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  const login = page.waitForResponse(response => response.request().method() === "POST" &&
    new URL(response.url()).pathname === "/api/v1/auth/login");
  await page.getByRole("button", { name: "Sign in" }).click();
  expect((await login).status()).toBe(200);
  // The server answers in milliseconds; rendering can lag in an engine that shares the machine
  // with parallel workers, so the post-login render has a longer bounded wait.
  await expect(page.getByText("Signed in as")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible({ timeout: 30_000 });
}

test("organization selection, reload, foreign identifier and last-admin denial", async ({ page }) => {
  test.setTimeout(90_000);
  test.skip(!password, "Disposable synthetic account is required");
  await signIn(page);
  const options = page.locator("#current-organization");
  await expect(options).toContainText("North Terminal (Administrator)");
  await options.selectOption({ label: "North Terminal (Administrator)" });
  await expect(page.getByText("Slug: north-terminal")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Memberships" })).toBeVisible();
  await expect(page.getByRole("list").getByText(email)).toBeVisible();
  await page.reload();
  await expect(page.getByText("Slug: north-terminal")).toBeVisible();

  const current = await page.request.get("/api/v1/me/organizations");
  expect(current.status()).toBe(200);
  const list = await current.json();
  const orgId = list.organizations.find(x => x.slug === "north-terminal").id;
  const foreign = await page.request.get(
    "/api/v1/organizations/00000000-0000-4000-8000-000000000999");
  expect(foreign.status()).toBe(404);
  const profile = await page.request.get("/api/v1/organizations/" + orgId);
  expect(profile.status()).toBe(200);

  await page.getByLabel("Password confirmation to disable account").fill(password);
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Disable my account" }).click();
  await expect(page.getByRole("alert")).toContainText("Assign another active administrator");
  await expect(page.getByText("Signed in as")).toBeVisible();

  // The interface hides organization data immediately; the server-side clear is asynchronous.
  const cleared = page.waitForResponse(response =>
    response.request().method() === "DELETE" &&
    new URL(response.url()).pathname === "/api/v1/auth/session/organization");
  await page.locator("#current-organization").selectOption("");
  expect((await cleared).status()).toBe(204);
  await expect(page.getByText("Slug: north-terminal")).toHaveCount(0);
  const context = await page.request.get("/api/v1/me/organization-context");
  expect(context.status()).toBe(200);
  expect((await context.json()).selected).toBeNull();
});
