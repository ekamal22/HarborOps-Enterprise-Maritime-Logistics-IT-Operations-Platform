import { test, expect } from "@playwright/test";

const ready = page => expect(page.getByText("Synthetic demonstration")).toBeVisible();

test("rapid refresh ends on current response without unrelated network requests", async ({ page }) => {
  const external = [];
  const unexpectedConsole = [];
  const unexpectedFailures = [];

  page.on("request", request => {
    const hostname = new URL(request.url()).hostname;
    if (!["localhost", "127.0.0.1"].includes(hostname)) external.push(request.url());
  });
  page.on("console", message => {
    if (message.type() === "error") unexpectedConsole.push(message.text());
  });
  page.on("requestfailed", request => {
    const failure = request.failure()?.errorText ?? "";
    // Aborted superseded refresh requests are intentional.
    if (!/abort|cancel/i.test(failure)) unexpectedFailures.push(failure);
  });

  await page.goto("/");
  await ready(page);
  const button = page.getByRole("button", { name: "Refresh status" });
  for (let i = 0; i < 15; i++) await button.click();
  await ready(page);
  expect(external).toEqual([]);
  expect(unexpectedConsole).toEqual([]);
  expect(unexpectedFailures).toEqual([]);
});

test("all supported viewport widths remain usable without horizontal overflow", async ({ page }) => {
  for (const width of [1280, 640, 375, 320]) {
    await page.setViewportSize({ width, height: 812 });
    await page.goto("/");
    await ready(page);
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth),
      `Horizontal overflow at ${width}px`).toBe(false);
    await expect(page.getByRole("button", { name: "Refresh status" })).toBeVisible();
  }
});

test("small footer text meets the WCAG AA contrast threshold", async ({ page }) => {
  await page.goto("/");
  const result = await page.getByText("Demo data only.", { exact: false }).evaluate(element => {
    const toRgb = text => {
      const components = text.match(/[\d.]+/g)?.slice(0, 3).map(Number);
      if (!components || components.length !== 3) throw Error("Unexpected CSS color");
      return components;
    };
    const luminosity = components => components
      .map(value => value / 255)
      .map(value => value <= 0.04045 ? value / 12.92 : Math.pow((value + 0.055) / 1.055, 2.4))
      .reduce((sum, component, index) => sum + component * [0.2126, 0.7152, 0.0722][index], 0);
    const foreground = luminosity(toRgb(getComputedStyle(element).color));
    const background = luminosity(toRgb(getComputedStyle(document.querySelector(".min-h-screen")).backgroundColor));
    const ratio = (Math.max(foreground, background) + 0.05) / (Math.min(foreground, background) + 0.05);
    return { foreground: getComputedStyle(element).color, background, ratio };
  });
  expect(result.ratio, `Footer contrast: ${JSON.stringify(result)}`).toBeGreaterThanOrEqual(4.5);
});

test("headings and status announcements have distinct roles", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1);
  await expect(page.getByRole("heading", { level: 2, name: "System baseline" })).toBeVisible();
  await ready(page);
  await expect(page.getByRole("status")).toContainText("System baseline status loaded");
  expect(await page.locator('[aria-live="polite"] [role="status"]').count()).toBe(0);
  const button = page.getByRole("button", { name: "Refresh status" });
  await button.focus();
  await expect(button).toBeFocused();
});
