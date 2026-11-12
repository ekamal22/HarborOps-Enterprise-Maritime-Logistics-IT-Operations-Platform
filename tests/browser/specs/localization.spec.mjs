import { expect, test } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

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

test("authenticated language changes preserve scoped operational workspaces and the session", async ({ page }) => {
  test.skip(!process.env.HARBOROPS_TEST_USER_PASSWORD,
    "Isolated provisioned account credentials are required");
  const root = process.env.HARBOROPS_VERIFICATION_ROOT;
  const fixture = root ? path.join(root, ".private", "m15-manager.json") : null;
  if (process.env.HARBOROPS_M15_LIVE_FIXTURE === "1") {
    expect(fixture && fs.existsSync(fixture),
      "The disposable source-locked manager session fixture must exist").toBeTruthy();
  }
  if (fixture && fs.existsSync(fixture)) {
    const state = JSON.parse(fs.readFileSync(fixture, "utf8"));
    await page.context().addCookies(state.cookies);
  } else {
    await page.goto("/");
    await page.getByLabel("Email").fill("m3-manager@example.test");
    await page.getByLabel("Password", { exact: true })
      .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
  }
  await page.goto("/");
  await expect(page.getByText("Signed in as")).toBeVisible();
  const organizations = page.locator("#current-organization");
  await expect(organizations).toBeVisible();
  await organizations.selectOption({ label: "M3 North Terminal (Manager)" });
  await expect.poll(() => organizations.inputValue(), {
    message: "Authenticated organization selection should finish before changing language"
  }).not.toBe("");
  const selectedOrganization = await organizations.inputValue();
  await expect(page.getByRole("region", { name: "Operational KPI dashboard" })).toBeVisible();
  await expect(page.getByRole("region", { name: "In-app shipment notifications" })).toBeVisible();

  await page.getByRole("combobox", { name: "Select language" }).selectOption("tr-TR");
  await expect(page.getByText("Oturum açan kullanıcı:")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Kuruluş çalışma alanı" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Operasyonel KPI panosu" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Uygulama içi sevkiyat bildirimleri" })).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow, "Authenticated Turkish workspace horizontal overflow").toBeLessThanOrEqual(2);
  await expect(organizations).toHaveValue(selectedOrganization);

  await page.getByRole("combobox", { name: "Dil seçin" }).selectOption("en");
  await expect(page.getByText("Signed in as")).toBeVisible();
  await expect(page.getByRole("region", { name: "Operational KPI dashboard" })).toBeVisible();
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

test("language choice synchronizes across open tabs without a server mutation", async ({ browser, baseURL }) => {
  const context = await browser.newContext({
    baseURL: baseURL || process.env.HARBOROPS_BROWSER_BASE_URL || "http://localhost:5173",
    locale: "en-US"
  });
  try {
    const first = await context.newPage();
    const second = await context.newPage();
    const writes = [];
    for (const page of [first, second])
      page.on("request", request => {
        if (["POST", "PUT", "PATCH", "DELETE"].includes(request.method()))
          writes.push(request.url());
      });
    await first.goto("/");
    await second.goto("/");
    await first.getByRole("combobox", { name: "Select language" }).selectOption("tr-TR");
    await expect(second.getByRole("combobox", { name: "Dil seçin" })).toHaveValue("tr-TR");
    await expect(second.locator("html")).toHaveAttribute("lang", "tr-TR");
    await second.getByRole("combobox", { name: "Dil seçin" }).selectOption("en");
    await expect(first.getByRole("combobox", { name: "Select language" })).toHaveValue("en");
    await expect(first.locator("html")).toHaveAttribute("lang", "en");
    expect(writes).toEqual([]);
  } finally {
    await context.close();
  }
});


test("Turkish layouts remain contained and core controls have accessible names", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("combobox", { name: "Select language" }).selectOption("tr-TR");
  for (const viewport of [
    { width: 375, height: 812 }, { width: 768, height: 1024 },
    { width: 1440, height: 900 }
  ]) {
    await page.setViewportSize(viewport);
    await expect(page.locator("html")).toHaveAttribute("lang", "tr-TR");
    await expect(page.getByRole("heading", {
      name: "Deniz taşımacılığı operasyonlarına genel bakış"
    })).toBeVisible();
    await expect(page.getByRole("combobox", { name: "Dil seçin" })).toBeVisible();
    await expect(page.getByLabel("E-posta")).toBeVisible();
    await expect(page.getByLabel("Şifre", { exact: true })).toBeVisible();
    const audit = await page.evaluate(() => {
      const overflow = document.documentElement.scrollWidth -
        document.documentElement.clientWidth;
      const elements = [...document.querySelectorAll("input, select, textarea, button")];
      const unlabelled = elements.filter(element => {
        if (!element.getClientRects().length || element.disabled) return false;
        const aria = element.getAttribute("aria-label") ||
          element.getAttribute("aria-labelledby");
        const labels = "labels" in element ? element.labels : null;
        return !aria && !labels?.length && !element.textContent?.trim();
      });
      return { overflow, unlabelled: unlabelled.map(el => el.outerHTML.slice(0, 160)) };
    });
    expect(audit.overflow, "Unexpected horizontal overflow at " + viewport.width + " px")
      .toBeLessThanOrEqual(2);
    expect(audit.unlabelled, "Unnamed interactive controls at " + viewport.width + " px")
      .toEqual([]);
  }
  const language = page.getByRole("combobox", { name: "Dil seçin" });
  await language.focus();
  await expect(language).toBeFocused();
});



async function managerKpiDashboard(page) {
  const root = process.env.HARBOROPS_VERIFICATION_ROOT;
  const fixture = root ? path.join(root, ".private", "m15-manager.json") : null;
  expect(fixture && fs.existsSync(fixture),
    "Source-locked disposable manager authentication required").toBeTruthy();
  const state = JSON.parse(fs.readFileSync(fixture, "utf8"));
  await page.context().addCookies(state.cookies);
  await page.goto("/");
  await expect(page.getByText("Signed in as")).toBeVisible();
  const organizations = page.locator("#current-organization");
  await organizations.selectOption({ label: "M3 North Terminal (Manager)" });
  await expect.poll(() => organizations.inputValue()).not.toBe("");
  await page.getByRole("combobox", { name: "Select language" }).selectOption("tr-TR");
  const dashboard = page.getByRole("region", { name: "Operasyonel KPI panosu" });
  await expect(dashboard).toBeVisible();
  return dashboard;
}

test("current G13 tenant reporting-capacity rejection is explained in Turkish", async ({ page }) => {
  test.skip(process.env.HARBOROPS_M15_LIVE_FIXTURE !== "1",
    "Isolated authenticated G13 browser fixture required");
  await page.route("**/analytics/overview*", route => route.fulfill({
    status: 429, contentType: "application/json",
    body: JSON.stringify({
      code: "report_tenant_capacity_exceeded",
      message: "The organization's reporting capacity is currently in use"
    })
  }));
  const dashboard = await managerKpiDashboard(page);
  await expect(dashboard.getByRole("alert")).toContainText(
    "Kuruluşun raporlama kapasitesi şu anda kullanımda");
  await expect(dashboard.getByText("KPI report members")).toHaveCount(0);
  await expect(dashboard.getByText("Aktif sevkiyatlar")).toHaveCount(0);
});

test("G13 percentages keep numeric meaning and Turkish presentation", async ({ page }) => {
  test.skip(process.env.HARBOROPS_M15_LIVE_FIXTURE !== "1",
    "Isolated authenticated G13 browser fixture required");
  await page.route("**/analytics/overview*", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({
      policyVersion: "m13-kpi-v1",
      reportId: "e0000000-0000-4000-8000-000000000091",
      reportToken: "disposable-turkish-percentage-fixture",
      fromUtc: "2026-11-02T00:00:00Z",
      beforeUtc: "2026-11-09T00:00:00Z",
      evaluatedAtUtc: "2026-11-09T12:00:00Z",
      expiresAtUtc: "2026-11-09T12:10:00Z",
      memberCount: 9, bucket: "day",
      counts: [
        { metricCode: "D05", dimension: "", bucketStartUtc: null, count: 3 },
        { metricCode: "K05", dimension: "", bucketStartUtc: null, count: 1 },
        { metricCode: "K06", dimension: "", bucketStartUtc: null, count: 2 },
        { metricCode: "D13", dimension: "", bucketStartUtc: null, count: 9 },
        { metricCode: "K13", dimension: "", bucketStartUtc: null, count: 8 },
        { metricCode: "K14", dimension: "", bucketStartUtc: null, count: 1 }
      ]
    })
  }));
  const dashboard = await managerKpiDashboard(page);
  await expect(dashboard.getByText("%33,33", { exact: true })).toBeVisible();
  await expect(dashboard.getByText("%66,67", { exact: true })).toBeVisible();
  await expect(dashboard.getByText("%88,89", { exact: true })).toBeVisible();
});

test("G15 unread count cap and exact count remain clear in English and Turkish", async ({ page }) => {
  test.skip(process.env.HARBOROPS_M15_LIVE_FIXTURE !== "1",
    "Disposable authenticated notification fixture required");
  const root = process.env.HARBOROPS_VERIFICATION_ROOT;
  const fixture = root ? path.join(root, ".private", "m15-manager.json") : null;
  expect(fixture && fs.existsSync(fixture)).toBeTruthy();
  const state = JSON.parse(fs.readFileSync(fixture, "utf8"));
  await page.context().addCookies(state.cookies);
  await page.route("**/analytics/overview*", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({
      policyVersion: "m13-kpi-v1",
      reportId: "e0000000-0000-4000-8000-000000000094",
      reportToken: "notification-count-layout-fixture",
      fromUtc: "2026-11-02T00:00:00Z",
      beforeUtc: "2026-11-09T00:00:00Z",
      evaluatedAtUtc: "2026-11-09T12:00:00Z",
      expiresAtUtc: "2026-11-09T12:10:00Z",
      memberCount: 0, bucket: "day", counts: []
    })
  }));
  await page.route("**/organizations/*/notifications?*", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({ items: [], nextCursor: null,
      snapshotAtUtc: "2026-11-09T12:00:00Z" })
  }));
  let capped = true;
  await page.route("**/organizations/*/notifications/unread-count", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({
      unreadCount: capped ? 1000 : 25, unreadCountCapped: capped,
      asOfUtc: "2026-11-09T12:00:00Z"
    })
  }));
  await page.goto("/");
  await expect(page.getByText("Signed in as")).toBeVisible();
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  const english = page.getByRole("region", { name: "In-app shipment notifications" });
  await expect(english.getByRole("heading", { level: 4 }))
    .toContainText("(1000+ unread)");
  await page.getByRole("combobox", { name: "Select language" }).selectOption("tr-TR");
  const turkish = page.getByRole("region", { name: "Uygulama içi sevkiyat bildirimleri" });
  const heading = turkish.getByRole("heading", { level: 4 });
  await expect(heading).toContainText("(1.000+ okunmamış)");
  capped = false;
  await turkish.getByRole("button", { name: "Bildirimleri yenile" }).click();
  await expect(heading).toContainText("(25 okunmamış)");
  await expect(heading).not.toContainText("25+");
});
