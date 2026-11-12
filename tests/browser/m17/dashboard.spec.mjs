import { expect, test } from "@playwright/test";

test("M17 private dashboard requires login and renders all diagnostic panels", async ({ page }) => {
  const password = process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
  if (!password) throw new Error("Set the disposable M17 Grafana password before browser verification");

  await page.goto("/login");
  await expect(page.locator('input[name="user"]')).toBeVisible();
  await page.locator('input[name="user"]').fill("local_operator");
  await page.locator('input[name="password"]').fill(password);
  await page.locator('button[type="submit"]').click();
  await page.goto("/d/harborops-m17/local-service-diagnostics");

  await expect(page.getByText("Local Service Diagnostics")).toBeVisible();
  for (const panel of [
    "API request rate", "API 5xx rate", "API p95 latency",
    "PostgreSQL readiness", "Notification worker",
    "Telemetry heartbeat sample age", "Bounded sampling drops", "Active local alerts"
  ]) {
    await expect(page.getByText(panel, { exact: false }).first()).toBeVisible();
  }
  // Data accuracy must additionally be reconciled with raw collector samples.
});

test("M17 panels are not available anonymously", async ({ browser }) => {
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto("/d/harborops-m17/local-service-diagnostics");
  await expect(page.locator('input[name="password"]')).toBeVisible();
  await context.close();
});
