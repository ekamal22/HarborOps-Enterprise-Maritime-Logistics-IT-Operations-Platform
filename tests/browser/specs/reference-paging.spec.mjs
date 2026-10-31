import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M3_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

test("scoped UI pagination loads beyond 25 results without duplicates", async ({ page }) => {
  test.skip(!enabled, "M3 isolated browser fixtures required");
  test.setTimeout(180_000);
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-manager@example.test");
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
  await page.locator("#current-organization")
    .selectOption({ label: "M3 Pagination Fixtures (Manager)" });
  await expect(page.getByRole("heading", { name: "Reference data" })).toBeVisible();
  const organizations = (await (await page.request.get("/api/v1/me/organizations")).json()).organizations;
  const org = organizations.find(o => o.slug === "m3-pagination");
  if (!org) throw new Error("Missing isolated pagination tenant");
  const prefix = "/api/v1/organizations/" + org.id + "/ports";
  const csrf = (await (await page.request.get("/api/v1/auth/csrf")).json()).token;
  const headers = { Origin: "http://localhost:5173", "X-HarborOps-CSRF": csrf };
  const nonce = Math.random().toString(36).slice(2, 10).toUpperCase();
  const created = [];
  try {
    for (let i = 0; i < 27; i++) {
      const response = await page.request.post(prefix, {
        headers,
        data: { referenceCode: "BATCH-" + nonce + "-" + String(i).padStart(2, "0"),
          name: "Synthetic Batch " + nonce + " Port " + i, countryCode: "US" }
      });
      expect(response.status()).toBe(201);
      created.push(await response.json());
    }
    await page.getByLabel("Search ports").fill(nonce);
    const workspace = page.getByRole("heading", { name: "Reference data" })
      .locator("xpath=ancestor::section");
    await expect(workspace.locator("ul > li")).toHaveCount(25);
    await expect(page.getByRole("button", { name: "Load more references" })).toBeVisible();
    await page.getByRole("button", { name: "Load more references" }).click();
    await expect(workspace.locator("ul > li")).toHaveCount(27);
    const codes = await workspace.locator("ul > li p").allTextContents();
    expect(new Set(codes).size).toBe(27);
    await expect(page.getByRole("button", { name: "Load more references" })).toHaveCount(0);
  } finally {
    for (const row of created) {
      const response = await page.request.delete(prefix + "/" + row.id, {
        headers, data: { expectedVersion: row.version }
      });
      expect(response.status()).toBe(204);
    }
  }
});
