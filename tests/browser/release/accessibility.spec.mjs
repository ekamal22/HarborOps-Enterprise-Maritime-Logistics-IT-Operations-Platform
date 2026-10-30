import { test, expect } from "@playwright/test";

// Execute only at M24-04 after the complete product interface is implemented.
// Not part of the M0 browser suite or any G0-G23 acceptance gate.
test("final release color contrast", async ({ page }) => {
  await page.goto("/");
  const ratio = await page.getByText("Demo data only.", { exact: false }).evaluate(element => {
    const rgb = value => (value.match(/[\d.]+/g) ?? []).slice(0, 3).map(Number);
    const luminance = c => c.map(v => v / 255)
      .map(v => v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)
      .reduce((sum, value, i) => sum + value * [0.2126, 0.7152, 0.0722][i], 0);
    const foreground = luminance(rgb(getComputedStyle(element).color));
    const background = luminance(rgb(getComputedStyle(document.querySelector(".min-h-screen")).backgroundColor));
    return (Math.max(foreground, background) + 0.05) / (Math.min(foreground, background) + 0.05);
  });
  expect(ratio).toBeGreaterThanOrEqual(4.5);
});

test("final release keyboard operation and accessible naming", async ({ page }) => {
  await page.goto("/");
  const button = page.getByRole("button", { name: "Refresh status" });
  await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1);
  await expect(page.getByRole("heading", { level: 2, name: "System baseline" })).toBeVisible();
  await button.focus();
  await expect(button).toBeFocused();
  await expect(button).toHaveAccessibleName("Refresh status");
  await page.keyboard.press("Enter");
  await expect(page.getByRole("status")).toContainText("System baseline status loaded");
});

test("final release error announcement and recovery", async ({ page }) => {
  const endpoint = "**/api/v1/system/baseline";
  await page.route(endpoint, route => route.fulfill({
    status: 503,
    contentType: "application/json",
    headers: { "Access-Control-Allow-Origin": "http://localhost:5173" },
    body: JSON.stringify({ code: "dependency_unavailable" })
  }));
  await page.goto("/");
  await expect(page.getByRole("alert")).toContainText("HTTP 503");
  await page.unroute(endpoint);
  await page.getByRole("button", { name: "Refresh status" }).click();
  await expect(page.getByRole("status")).toContainText("System baseline status loaded");
});

test("final release small viewport usability", async ({ page }) => {
  for (const width of [1280, 640, 375, 320]) {
    await page.setViewportSize({ width, height: 812 });
    await page.goto("/");
    await expect(page.getByText("Synthetic demonstration")).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)).toBe(false);
    await expect(page.getByRole("button", { name: "Refresh status" })).toBeVisible();
  }
});
