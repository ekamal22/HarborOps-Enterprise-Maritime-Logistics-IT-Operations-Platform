import { test, expect } from "@playwright/test";

test("baseline status loads from the API without unrelated network calls", async ({ page }) => {
  const external = [];
  page.on("request", request => {
    const hostname = new URL(request.url()).hostname;
    if (!["localhost", "127.0.0.1"].includes(hostname)) external.push(request.url());
  });
  await page.goto("/");
  await expect(page.getByText("System baseline", { exact: true })).toBeVisible();
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
  await expect(page.getByText("m0-synthetic-v1")).toBeVisible();
  expect(external).toEqual([]);
});

test("refresh reloads the database-backed status", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
  await page.getByText("Refresh status").click();
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
});

test("unavailable API response is displayed and later refresh recovers", async ({ page }) => {
  const api = "**/api/v1/system/baseline";
  await page.route(api, route => route.fulfill({
    status: 503,
    contentType: "application/json",
    headers: { "Access-Control-Allow-Origin": "http://localhost:5173" },
    body: JSON.stringify({ code: "dependency_unavailable", message: "Service unavailable" })
  }));
  await page.goto("/");
  await expect(page.getByText("Service unavailable", { exact: true })).toBeVisible();
  await expect(page.getByText("Service returned HTTP 503")).toBeVisible();
  await page.unroute(api);
  await page.getByText("Refresh status").click();
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
});
