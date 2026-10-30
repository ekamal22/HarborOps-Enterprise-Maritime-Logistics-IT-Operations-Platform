import { test, expect } from "@playwright/test";

test("database-backed baseline renders without unexpected external calls", async ({ page }) => {
  const external = [];
  page.on("request", request => {
    const url = new URL(request.url());
    if (!["localhost", "127.0.0.1"].includes(url.hostname))
      external.push(url.origin);
  });

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "System baseline" })).toBeVisible();
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
  await expect(page.getByText("m0-synthetic-v1")).toBeVisible();
  await expect(page.getByText("HarborOps", { exact: true }).last()).toBeVisible();
  expect(external).toEqual([]);
});

test("keyboard refresh preserves valid and accessible status", async ({ page }) => {
  await page.goto("/");
  const button = page.getByRole("button", { name: "Refresh status" });
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
  await button.focus();
  await expect(button).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
  await expect(button).toHaveAccessibleName("Refresh status");
});

test("unavailable response is announced and refresh recovers", async ({ page }) => {
  const url = "**/api/v1/system/baseline";
  await page.route(url, route => route.fulfill({
    status: 503,
    contentType: "application/json",
    headers: { "Access-Control-Allow-Origin": "http://localhost:5173" },
    body: JSON.stringify({ code: "dependency_unavailable", message: "Service unavailable" })
  }));
  await page.goto("/");
  await expect(page.getByRole("alert")).toContainText("Service unavailable");
  await expect(page.getByRole("alert")).toContainText("HTTP 503");
  await page.unroute(url);
  await page.getByRole("button", { name: "Refresh status" }).click();
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
});

test("375px mobile layout has no horizontal overflow", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto("/");
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth > window.innerWidth);
  expect(overflow).toBe(false);
});
