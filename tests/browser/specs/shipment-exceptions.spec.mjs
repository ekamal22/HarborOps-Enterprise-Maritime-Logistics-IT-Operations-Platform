import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M5_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

async function openShipment(page, role) {
  await page.goto("/");
  await page.getByLabel("Email").fill(
    role === "Auditor" ? "m4-auditor@example.test" : `m3-${role.toLowerCase()}@example.test`);
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" }))
    .toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: `M3 North Terminal (${role})` });
  const shipment = page.getByRole("heading", { name: "Shipments", exact: true })
    .locator("xpath=ancestor::section[1]");
  await shipment.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(shipment.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await shipment.getByRole("button", { name: "View shipment" }).first().click();
  return shipment.getByRole("region", { name: "Shipment exceptions" });
}

test("G11 read-only Analyst sees uncertainty and cannot disposition exceptions", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Analyst");
  await expect(panel).toBeVisible();
  await expect(panel.getByText("no ETA is calculated", { exact: false })).toBeVisible();
  await expect(panel.getByRole("button", { name: "Reevaluate rules" })).toHaveCount(0);
  await expect(panel.getByRole("button", { name: "Acknowledge" })).toHaveCount(0);
  await expect(panel.getByRole("button", { name: "Resolve" })).toHaveCount(0);
});

test("G11 Manager sees scoped reevaluation control and status without claimed ETA", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Manager");
  await expect(panel).toBeVisible();
  await expect(panel.getByRole("button", { name: "Reevaluate rules" })).toBeEnabled();
  await expect(panel.getByText("These rule outcomes use manual workflow records", {
    exact: false })).toBeVisible();
  await expect(panel.getByRole("button", { name: "Refresh exceptions" })).toBeEnabled();
});

test("G11 exception panel cannot survive organization switch", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Manager");
  await expect(panel).toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: "M3 East Terminal (Manager)" });
  await expect(panel).toHaveCount(0);
});

test("G11 a revoked exception read clears stale results", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Analyst");
  await expect(panel).toBeVisible();
  await page.route("**/api/v1/organizations/*/shipments/*/exceptions?*", route =>
    route.fulfill({ status: 403, contentType: "application/json",
      body: JSON.stringify({ code: "organization_access_revoked",
        message: "Access revoked" }) }));
  await panel.getByRole("button", { name: "Refresh exceptions" }).click();
  await expect(page.getByRole("region", { name: "Shipment exceptions" })).toHaveCount(0);
});


test("G11 history continuation displays additional immutable entries without duplicates", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Manager");
  const exceptionId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
  const firstId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
  const secondId = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";
  const record = {
    id: exceptionId, ruleCode: "ArrivalScheduleOverdue", occurrence: 1,
    condition: "Inactive", state: "Acknowledged", severity: "Warning",
    confidence: "ScheduleOnly", reasonCode: "scheduled_arrival_not_yet_overdue",
    lastEvaluatedAtUtc: "2026-11-08T12:00:00Z",
    evidence: { rulePolicyVersion: "m11-rules-v1",
      trackingInputRevision: 0, correctionGeneration: 0,
      trackingInputDigest: null }
  };
  await page.route("**/shipments/*/exceptions?*", route =>
    route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ items: [record], nextCursor: null }) }));
  await page.route("**/exceptions/" + exceptionId, route =>
    route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ exception: record, historyTruncated: true,
        nextHistoryCursor: "next-history-page",
        history: [{ id: firstId, action: "Opened",
          occurredAtUtc: "2026-11-08T11:00:00Z", reason: null }] }) }));
  await page.route("**/exceptions/" + exceptionId + "/history?*", route =>
    route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ nextCursor: null, items: [
        { id: secondId, action: "Reevaluated",
          occurredAtUtc: "2026-11-08T12:00:00Z", reason: null }] }) }));
  await panel.getByRole("button", { name: "Refresh exceptions" }).click();
  await panel.getByRole("button", { name: "View history" }).click();
  await expect(panel.getByText("Additional retained records are available", {
    exact: false })).toBeVisible();
  await panel.getByRole("button", { name: "Load more history" }).click();
  // Count the actual immutable history rows, not text matches that differ
  // between browser engines when ancestor elements share matching text.
  const historyItems = panel.locator("ol > li");
  await expect(historyItems).toHaveCount(2);
  await expect(historyItems.nth(0)).toContainText("Opened");
  await expect(historyItems.nth(1)).toContainText("Reevaluated");
  await expect(panel.getByRole("button", { name: "Load more history" })).toHaveCount(0);
});


test("G11 Resolve is unavailable for an unacknowledged inactive occurrence", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Manager");
  const record = {
    id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
    ruleCode: "ArrivalScheduleOverdue", occurrence: 1,
    condition: "Inactive", state: "Open", severity: "Info",
    confidence: "ScheduleOnly", reasonCode: "scheduled_arrival_not_yet_overdue",
    lastEvaluatedAtUtc: "2026-11-08T12:00:00Z"
  };
  await page.route("**/shipments/*/exceptions?*", route =>
    route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ items: [record], nextCursor: null }) }));
  await panel.getByRole("button", { name: "Refresh exceptions" }).click();
  await expect(panel.getByText("Inactive / Open")).toBeVisible();
  await expect(panel.getByRole("button", { name: "Resolve" })).toHaveCount(0);
  await expect(panel.getByRole("button", { name: "Acknowledge" })).toHaveCount(0);
});

test("G11 reviewed recorded arrival remains attributable after Manager resolution", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  const panel = await openShipment(page, "Manager");
  const record = {
    id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
    ruleCode: "RecordedArrivalLate", occurrence: 1,
    condition: "Active", state: "Acknowledged", severity: "High",
    confidence: "RecordedWorkflow", reasonCode: "recorded_arrival_after_grace",
    lastEvaluatedAtUtc: "2026-11-08T12:00:00Z"
  };
  await page.route("**/shipments/*/exceptions?*", route =>
    route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ items: [record], nextCursor: null }) }));
  await panel.getByRole("button", { name: "Refresh exceptions" }).click();
  await expect(panel.getByText("Active / Acknowledged")).toBeVisible();
  const resolve = panel.getByRole("button", { name: "Resolve" });
  await expect(resolve).toBeVisible();
  await panel.locator('input[id^="reason-"]').fill("Historical recorded lateness reviewed");
  await expect(resolve).toBeEnabled();

  record.state = "Resolved";
  await panel.getByRole("button", { name: "Refresh exceptions" }).click();
  await expect(panel.getByText("Active / Resolved")).toBeVisible();
  await expect(panel.getByText("Resolution closes this occurrence", { exact: false }))
    .toBeVisible();
  await expect(panel.getByRole("button", { name: "Resolve" })).toHaveCount(0);
});
