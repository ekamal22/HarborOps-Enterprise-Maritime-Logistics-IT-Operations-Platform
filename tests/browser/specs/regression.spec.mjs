import { test, expect } from "@playwright/test";

test("rapid refresh ends with current data and no unrelated network failures", async ({ page }) => {
  const external = [];
  const consoleErrors = [];
  const unexpectedFailures = [];

  page.on("request", request => {
    const hostname = new URL(request.url()).hostname;
    if (!["localhost", "127.0.0.1"].includes(hostname)) external.push(request.url());
  });
  page.on("console", message => {
    if (message.type() === "error") consoleErrors.push(message.text());
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
