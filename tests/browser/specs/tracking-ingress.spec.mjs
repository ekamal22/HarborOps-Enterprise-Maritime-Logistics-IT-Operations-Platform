import { test, expect } from "@playwright/test";

test("integration route is proxied and rejects requests without source credentials", async ({ page }) => {
  await page.goto("/");
  const response = await page.request.post("/integrations/v1/tracking-events", {
    headers: { "X-HarborOps-Tracking-Profile": "tnt-2.2-harborops-v1" },
    data: {
      sourceEventId: "browser-1",
      eventFamily: "Equipment",
      eventCode: "GTIN",
      classifier: "ACT",
      occurredAtUtc: "2026-11-02T10:00:00Z",
      equipmentReference: "SYN-BOX-ABCD",
      externalSchemaVersion: "2.2.0"
    }
  });
  expect(response.status()).toBe(401);
  expect(response.headers()["cache-control"]).toContain("no-store");
  const body = await response.json();
  expect(body.code).toBe("invalid_source_credentials");
  expect(body.correlationId).toBeTruthy();
});
