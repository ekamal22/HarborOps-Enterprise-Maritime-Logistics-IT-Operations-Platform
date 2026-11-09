import { expect, test } from "@playwright/test";

test("language selection persists without submitting a sign-in form", async ({ page }) => {
  const mutations = [];
  page.on("request", request => {
    if (["POST", "PUT", "PATCH", "DELETE"].includes(request.method()))
      mutations.push(request.url());
  });
  await page.goto("/");
  const language = page.getByRole("combobox", { name: "Select language" });
  await expect(language).toHaveValue("en");
  await page.getByLabel("Email").fill("locale-check@example.test");
  await language.selectOption("tr-TR");
  await expect(page.locator("html")).toHaveAttribute("lang", "tr-TR");
  await expect(page.getByRole("button", { name: "Giriş yap", exact: true })).toBeVisible();
  await expect(page.getByLabel("E-posta")).toHaveValue("locale-check@example.test");
  expect(mutations).toEqual([]);
  await page.reload();
  await expect(page.getByRole("combobox", { name: "Dil seçin" })).toHaveValue("tr-TR");
  await expect(page.getByRole("button", { name: "Giriş yap", exact: true })).toBeVisible();
  await page.getByRole("combobox", { name: "Dil seçin" }).selectOption("en");
  await expect(page.locator("html")).toHaveAttribute("lang", "en");
  await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeVisible();
  expect(mutations).toEqual([]);
});

test.describe("first-visit browser language", () => {
  test.use({ locale: "tr-TR" });
  test("Turkish browser preference selects Turkish before sign-in", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByRole("combobox", { name: "Dil seçin" })).toHaveValue("tr-TR");
    await expect(page.locator("html")).toHaveAttribute("lang", "tr-TR");
    await expect(page.getByRole("heading", { name: "Deniz taşımacılığı operasyonlarına genel bakış" }))
      .toBeVisible();
  });
});

test("authenticated language changes preserve the session and organization workspace", async ({ page }) => {
  test.skip(!process.env.HARBOROPS_TEST_USER_PASSWORD,
    "Isolated provisioned account credentials are required");
  await page.goto("/");
  await page.getByLabel("Email").fill("operator@example.test");
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByText("Signed in as")).toBeVisible();
  await page.getByRole("combobox", { name: "Select language" }).selectOption("tr-TR");
  await expect(page.getByText("Oturum açan kullanıcı:")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Kuruluş çalışma alanı" })).toBeVisible();
  await page.getByRole("combobox", { name: "Dil seçin" }).selectOption("en");
  await expect(page.getByText("Signed in as")).toBeVisible();
  const session = await page.request.get("/api/v1/auth/session/guard");
  expect(session.status()).toBe(200);
});

test("stable authentication errors remain understandable after switching languages", async ({ page }) => {
  await page.route("**/api/v1/auth/session", route =>
    route.fulfill({ status: 401, contentType: "application/json",
      body: JSON.stringify({ code: "authentication_required" }) }));
  await page.route("**/api/v1/auth/csrf", route =>
    route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ token: "localization-check" }) }));
  let submitted = null;
  await page.route("**/api/v1/auth/login", route => {
    submitted = route.request().postDataJSON();
    return route.fulfill({ status: 401, contentType: "application/json",
      body: JSON.stringify({ code: "invalid_credentials", message: "Unexpected service wording" }) });
  });
  await page.goto("/");
  await page.getByRole("combobox", { name: "Select language" }).selectOption("tr-TR");
  await page.getByLabel("E-posta").fill("locale-check@example.test");
  await page.getByLabel("Şifre", { exact: true }).fill("wrong-test-password");
  await page.getByRole("button", { name: "Giriş yap", exact: true }).click();
  await expect(page.getByText("Bu kimlik bilgileriyle giriş yapılamadı")).toBeVisible();
  expect(submitted).toEqual({ email: "locale-check@example.test", password: "wrong-test-password" });
  await page.getByRole("combobox", { name: "Dil seçin" }).selectOption("en");
  await expect(page.getByText("Unable to sign in with these credentials")).toBeVisible();
});

test.describe("local session time is not displayed as UTC", () => {
  test.use({ timezoneId: "Europe/Istanbul" });
  test("session expiry renders in browser time without changing the instant", async ({ page }) => {
    await page.route("**/api/v1/auth/session", route =>
      route.fulfill({ status: 200, contentType: "application/json",
        body: JSON.stringify({
          user: { id: "00000000-0000-0000-0000-000000000001",
            email: "localized@example.test", displayName: "Test User" },
          session: { expiresAtUtc: "2026-11-09T11:30:00Z" }
        }) }));
    await page.goto("/");
    await page.getByRole("combobox", { name: "Select language" }).selectOption("tr-TR");
    await expect(page.getByText(/Oturum bitişi \(yerel saat\):.*14[:.]30/)).toBeVisible();
  });
});
