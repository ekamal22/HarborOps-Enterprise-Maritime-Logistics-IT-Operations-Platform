import { test, expect } from "@playwright/test";

test("rapid refresh ends with current data and no unrelated network failures", async ({ page }) => {
  const external = [];
  const consoleErrors = [];
  const unexpectedFailures = [];
  const expectedAnonymousSessionResponses = [];

  page.on("request", request => {
    const hostname = new URL(request.url()).hostname;
    if (!["localhost", "127.0.0.1"].includes(hostname)) external.push(request.url());
  });
  page.on("response", response => {
    if (new URL(response.url()).pathname === "/api/v1/auth/session" && response.status() === 401)
      expectedAnonymousSessionResponses.push(response.url());
  });
  page.on("console", message => {
    if (message.type() !== "error") return;
    const expectedAnonymousResponse = message.text().includes("401 (Unauthorized)") &&
      (new URL(message.location().url || "http://localhost/").pathname === "/api/v1/auth/session" ||
       expectedAnonymousSessionResponses.length > 0);
    if (!expectedAnonymousResponse) consoleErrors.push(message.text());
  });
  page.on("requestfailed", request => {
    const failure = request.failure()?.errorText ?? "";
    if (!/abort|cancel/i.test(failure)) unexpectedFailures.push(failure);
  });

  await page.goto("/");
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
  const button = page.getByText("Refresh status");
  for (let i = 0; i < 15; i++) await button.click();
  await expect(page.getByText("Synthetic demonstration")).toBeVisible();
  expect(external).toEqual([]);
  expect(consoleErrors).toEqual([]);
  expect(unexpectedFailures).toEqual([]);
});
