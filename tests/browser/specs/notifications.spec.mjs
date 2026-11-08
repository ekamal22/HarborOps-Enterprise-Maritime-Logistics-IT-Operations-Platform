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


test("G15 personal receipt controls preserve a separate unread counter", async ({ page }) => {
  test.skip(!enabled,"Disposable organization fixtures required");
  await signIn(page);
  const north = await page.locator("#current-organization option")
    .filter({hasText:"M3 North Terminal (Manager)"}).getAttribute("value");
  expect(north).toBeTruthy();
  let item = {
    id:"ceeeee11-1111-4111-8111-111111111111",
    shipmentId:"ceeeee22-2222-4222-8222-222222222222",
    exceptionId:"ceeeee33-3333-4333-8333-333333333333",
    eventType:"ExceptionOpened",ruleCode:"ArrivalScheduleOverdue",
    title:"Planned arrival overdue; physical arrival unconfirmed",
    eventLabel:"Opened",createdAtUtc:"2026-11-08T18:00:00Z",
    readAtUtc:null,acknowledgedAtUtc:null,state:"Unread",version:1
  };
  const root="**/organizations/"+north+"/notifications";
  await page.route(root+"?*", route=>route.fulfill({
    status:200,contentType:"application/json",
    body:JSON.stringify({items:[item],nextCursor:null,
      snapshotAtUtc:"2026-11-08T18:01:00Z"})
  }));
  await page.route(root+"/unread-count",route=>route.fulfill({
    status:200,contentType:"application/json",
    body:JSON.stringify({unreadCount:item.readAtUtc?0:1,
      asOfUtc:"2026-11-08T18:01:00Z"})
  }));
  await page.route(root+"/"+item.id,route=>{
    if(route.request().method()!=="PATCH") return route.continue();
    const data=route.request().postDataJSON();
    expect(route.request().headers()["idempotency-key"]).toMatch(
      /^[0-9a-f-]{36}$/);
    expect(route.request().headers()["x-harborops-csrf"]).toBeTruthy();
    expect(data.expectedVersion).toBe(item.version);
    if(data.action==="MarkRead") item={...item,readAtUtc:"2026-11-08T18:02:00Z",
      state:"Read",version:item.version+1};
    else if(data.action==="Acknowledge") item={...item,
      readAtUtc:item.readAtUtc??"2026-11-08T18:02:00Z",
      acknowledgedAtUtc:"2026-11-08T18:03:00Z",state:"Acknowledged",
      version:item.version+1};
    else throw Error("unexpected action");
    return route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify(item)});
  });
  await page.locator("#current-organization")
    .selectOption({label:"M3 North Terminal (Manager)"});
  const center=page.getByRole("region",{name:"In-app shipment notifications"});
  await expect(center.getByText("1 unread",{exact:false})).toBeVisible();
  await center.getByRole("button",{name:"Mark read"}).click();
  await expect(center.getByRole("button",{name:"Mark read"})).toHaveCount(0);
  await expect(center.getByText("0 unread",{exact:false})).toBeVisible();
  await center.getByRole("button",{name:"Acknowledge notification"}).click();
  await expect(center.getByRole("button",{name:"Acknowledge notification"}))
    .toHaveCount(0);
  await expect(center.getByText("Acknowledged",{exact:false})).toBeVisible();
});

test("G15 rejected notification refresh drops protected data and organization selection",async({page})=>{
  test.skip(!enabled,"Disposable organization fixtures required");
  await signIn(page);
  const north=await page.locator("#current-organization option")
    .filter({hasText:"M3 North Terminal (Manager)"}).getAttribute("value");
  expect(north).toBeTruthy();
  let revoked=false;
  await page.route("**/organizations/"+north+"/notifications?*",route=>
    route.fulfill({
      status:revoked?403:200,contentType:"application/json",
      body:JSON.stringify(revoked?{code:"organization_access_revoked",
        message:"Access changed"}:{items:[],nextCursor:null,
        snapshotAtUtc:"2026-11-08T18:00:00Z"})
    }));
  await page.route("**/organizations/"+north+"/notifications/unread-count",route=>
    route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({unreadCount:0,asOfUtc:"2026-11-08T18:00:00Z"})}));
  await page.locator("#current-organization")
    .selectOption({label:"M3 North Terminal (Manager)"});
  const center=page.getByRole("region",{name:"In-app shipment notifications"});
  await expect(center).toBeVisible();
  revoked=true;
  await center.getByRole("button",{name:"Refresh notifications"}).click();
  await expect(center).toHaveCount(0);
});

test("G15 unavailable shipment navigation fails closed without exposing the target",async({page})=>{
  test.skip(!enabled,"Disposable organization fixtures required");
  await signIn(page);
  const north=await page.locator("#current-organization option")
    .filter({hasText:"M3 North Terminal (Manager)"}).getAttribute("value");
  expect(north).toBeTruthy();
  const shipment="cfdddd22-2222-4222-8222-222222222222";
  const item={
    id:"cfdddd11-1111-4111-8111-111111111111",
    shipmentId:shipment,exceptionId:"cfdddd33-3333-4333-8333-333333333333",
    eventType:"ExceptionAutoResolved",ruleCode:"RecordedArrivalLate",
    title:"Late arrival recorded in the manual workflow",
    eventLabel:"Closed by evaluation",createdAtUtc:"2026-11-08T18:00:00Z",
    readAtUtc:null,acknowledgedAtUtc:null,state:"Unread",version:1
  };
  await page.route("**/organizations/"+north+"/notifications?*",route=>
    route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({items:[item],nextCursor:null,
        snapshotAtUtc:"2026-11-08T18:01:00Z"})}));
  await page.route("**/organizations/"+north+"/notifications/unread-count",route=>
    route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({unreadCount:1,asOfUtc:"2026-11-08T18:01:00Z"})}));
  await page.route("**/organizations/"+north+"/shipments/"+shipment+"*",route=>
    route.fulfill({status:404,contentType:"application/json",
      body:JSON.stringify({code:"resource_not_found",message:"Not found"})}));
  await page.locator("#current-organization")
    .selectOption({label:"M3 North Terminal (Manager)"});
  const center=page.getByRole("region",{name:"In-app shipment notifications"});
  await expect(center).toBeVisible();
  await center.getByRole("button",{name:"Open shipment exception"}).click();
  const shipmentPanel=page.getByRole("heading",{name:"Shipments",exact:true})
    .locator("xpath=ancestor::section[1]");
  await expect(shipmentPanel.getByText(
    "The requested shipment or exception is no longer available.")).toBeVisible();
});
