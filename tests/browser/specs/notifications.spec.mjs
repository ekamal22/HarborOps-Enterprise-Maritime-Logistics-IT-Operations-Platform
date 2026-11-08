import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M5_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

async function signIn(page) {
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-manager@example.test");
  await page.getByLabel("Password", { exact: true })
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}

test("G15 scoped notification center displays neutral event and personal state", async ({ page }) => {
  test.skip(!enabled, "Disposable organization fixtures required");
  const id = "cddddd11-1111-4111-8111-111111111111";
  const notice = {
    id, shipmentId:"cddddd22-2222-4222-8222-222222222222",
    exceptionId:"cddddd33-3333-4333-8333-333333333333",
    eventType:"ExceptionOpened",ruleCode:"ArrivalScheduleOverdue",
    title:"Planned arrival overdue; physical arrival unconfirmed",
    eventLabel:"Opened",createdAtUtc:"2026-11-08T17:00:00Z",
    readAtUtc:null,acknowledgedAtUtc:null,version:1
  };
  await signIn(page);
  const north = await page.locator("#current-organization option")
    .filter({hasText:"M3 North Terminal (Manager)"}).getAttribute("value");
  expect(north).toBeTruthy();
  await page.route("**/organizations/" + north + "/notifications?*", route =>
    route.fulfill({
      status:200,contentType:"application/json",
      body:JSON.stringify({items:[notice],nextCursor:null,
        snapshotAtUtc:"2026-11-08T17:01:00Z"})
    }));
  await page.route("**/organizations/" + north + "/notifications/unread-count", route =>
    route.fulfill({
      status:200,contentType:"application/json",
      body:JSON.stringify({unreadCount:1,asOfUtc:"2026-11-08T17:01:00Z"})
    }));
  await page.locator("#current-organization")
    .selectOption({ label:"M3 North Terminal (Manager)" });
  const center = page.getByRole("region",{name:"In-app shipment notifications"});
  await expect(center).toBeVisible();
  await expect(center.getByText("Planned arrival overdue; physical arrival unconfirmed"))
    .toBeVisible();
  await expect(center.getByText("1 unread",{exact:false})).toBeVisible();
  await expect(center.getByRole("button",{name:"Acknowledge notification"}))
    .toBeVisible();
  await page.locator("#current-organization")
    .selectOption({label:"M3 East Terminal (Manager)"});
  await expect(center.getByText("Planned arrival overdue; physical arrival unconfirmed"))
    .toHaveCount(0);
});

test("G15 notification center never grants administrator shipment visibility", async ({page}) => {
  test.skip(!enabled,"Disposable organization fixtures required");
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-admin-a@example.test");
  await page.getByLabel("Password",{exact:true})
    .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button",{name:"Sign in"}).click();
  await page.locator("#current-organization")
    .selectOption({label:"M3 North Terminal (Administrator)"});
  await expect(page.getByRole("region",{name:"In-app shipment notifications"}))
    .toHaveCount(0);
});
