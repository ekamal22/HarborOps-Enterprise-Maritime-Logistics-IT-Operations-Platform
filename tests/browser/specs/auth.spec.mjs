import { test, expect } from "@playwright/test";

const password = process.env.HARBOROPS_TEST_USER_PASSWORD;
const email = "operator@example.test";

test("provisioned account signs in, survives reload and logs out", async ({ page }) => {
  test.skip(!password, "Synthetic account secret must be provided by isolated verification");
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Signed in as")).toBeVisible();
  await page.reload();
  await expect(page.getByText("Signed in as")).toBeVisible();
  const authenticated = await page.request.get("/api/v1/auth/session/guard");
  expect(authenticated.status()).toBe(200);
  await page.getByRole("button", { name: "Log out", exact: true }).click();
  await expect(page.getByRole("button", { name: "Sign in" })).toBeVisible();
  const rejected = await page.request.get("/api/v1/auth/session/guard");
  expect(rejected.status()).toBe(401);
});

test("incorrect credential is denied with a generic response", async ({ page }) => {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill("incorrect-password-long-enough");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("alert")).toContainText("Unable to sign in");
});
