import { expect, test } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

const enabled = process.env.HARBOROPS_M5_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

/*
 * The audit-read limiter is per user (30 requests/minute). Each browser
 * scenario starts a fresh workspace, whose notification center fetches before
 * the scenario's individual routes can be installed. Intercept the incidental
 * first paint for mocked scenarios so three browser engines do not consume
 * real API quota. The separately named live scenario never installs mocks.
 * Scenario-specific routes take precedence over these earlier fallbacks.
 */
test.beforeEach(async ({ page }, info) => {
  // The notification workspace also mounts the M13 dashboard. Keep its
  // unrelated background report builds out of the notification scenarios,
  // including the live M15 delivery case. The KPI suite tests real builds.
  await page.route("**/analytics/overview*", route => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({
      policyVersion: "m13-kpi-v1",
      reportId: "e0000000-0000-4000-8000-000000000015",
      reportToken: "notification-suite-kpi-fixture",
      fromUtc: "2026-11-02T00:00:00Z",
      beforeUtc: "2026-11-09T00:00:00Z",
      evaluatedAtUtc: "2026-11-09T12:00:00Z",
      expiresAtUtc: "2026-11-09T12:10:00Z",
      memberCount: 0, bucket: "day", counts: []
    })
  }));
  if (info.title.includes("G15 live M11 event is delivered")) return;
  const empty = { items:[], nextCursor:null,
    snapshotAtUtc:"2026-11-08T18:00:00Z" };
  await page.route("**/organizations/*/notifications?*", route =>
    route.fulfill({ status:200, contentType:"application/json",
      body:JSON.stringify(empty) }));
  await page.route("**/organizations/*/notifications/unread-count", route =>
    route.fulfill({ status:200, contentType:"application/json",
      body:JSON.stringify({ unreadCount:0, asOfUtc:empty.snapshotAtUtc }) }));
});

async function signIn(page, role="Manager") {
  const root=process.env.HARBOROPS_VERIFICATION_ROOT;
  const filename=role==="Administrator" ? "m15-administrator.json" : "m15-manager.json";
  const privateState=root ? path.join(root,".private",filename) : null;
  if (process.env.HARBOROPS_M15_LIVE_FIXTURE === "1" &&
      !(privateState && fs.existsSync(privateState)))
    throw Error("Source-locked private browser session fixture missing");
  if (privateState && fs.existsSync(privateState)) {
    const state=JSON.parse(fs.readFileSync(privateState,"utf8"));
    const session=state.cookies.find(cookie=>cookie.name==="HarborOps.Session");
    expect(session?.value).toBeTruthy();
    await page.context().addCookies(state.cookies);
  } else {
    // Optional local smoke mode without a prepared disposable fixture.
    await page.goto("/");
    await page.getByLabel("Email").fill(role==="Administrator"
      ? "m3-admin-a@example.test" : "m3-manager@example.test");
    await page.getByLabel("Password",{exact:true})
      .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
    const login=page.waitForResponse(response=>
      response.url().endsWith("/api/v1/auth/login") &&
      response.request().method()==="POST");
    await page.getByRole("button",{name:"Sign in"}).click();
    const response=await login;
    expect(response.status(), "Disposable browser login HTTP status").toBe(200);
  }
  await page.goto("/");
  await expect(page.getByRole("heading",{name:"Organization workspace"}))
    .toBeVisible();
}

test("G15 live M11 event is delivered and navigable from the real notification API",
  async ({ page }) => {
  test.skip(!enabled || process.env.HARBOROPS_M15_LIVE_FIXTURE !== "1",
    "Disposable opt-in M15 delivery fixture required");
  const root = process.env.HARBOROPS_VERIFICATION_ROOT;
  const fixture = JSON.parse(fs.readFileSync(
    path.join(root,"m15-browser","live-fixture.json"),"utf8"));
  expect(fixture.sourceSha).toBe(process.env.HARBOROPS_SOURCE_SHA);
  const liveHttpStatuses = [];
  page.on("response", response => {
    if (response.url().includes("/organizations/" + fixture.organizationId +
      "/notifications")) liveHttpStatuses.push(response.status());
  });
  await signIn(page);
  await page.locator("#current-organization")
    .selectOption({ label:"M3 North Terminal (Manager)" });
  const center = page.getByRole("region",{name:"In-app shipment notifications"});
  await expect(center).toBeVisible();
  const liveNotice = center.locator("li").filter({hasText:fixture.title});
  try {
    await expect(liveNotice).toHaveCount(1);
  } catch (failure) {
    // Report request outcomes without logging response bodies, cookies, or
    // private fixture credentials. This distinguishes actual access failures
    // from pagination and rendering bugs on the final browser engine.
    const errors = await center.getByRole("alert").allTextContents();
    const content = await center.locator("li p.font-medium").allTextContents();
    throw new Error("Live M15 notification not visible. HTTP statuses: " +
      liveHttpStatuses.join(",") + "; UI errors: " + errors.join(" | ") +
      "; notification count: " + content.length, { cause:failure });
  }
  await expect(liveNotice.getByText(fixture.title)).toBeVisible();
  await expect(liveNotice.getByText("Unread",{exact:false})).toBeVisible();
  await liveNotice.getByRole("button",{name:"Open shipment exception"}).click();
  const panel = page.getByRole("region",{name:"Shipment exceptions"});
  await expect(panel).toBeVisible();
  await expect(panel.getByText(
    "Planned arrival is overdue; physical arrival is unconfirmed",
    {exact:false}).first()).toBeVisible();
});

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

test("G15 capped unread count shows 1000+ and an exact count never shows a plus", async ({ page }) => {
  test.skip(!enabled, "Disposable organization fixtures required");
  const notice = {
    id:"cddddd11-1111-4111-8111-111111111112",
    shipmentId:"cddddd22-2222-4222-8222-222222222223",
    exceptionId:"cddddd33-3333-4333-8333-333333333334",
    eventType:"ExceptionOpened",ruleCode:"ArrivalScheduleOverdue",
    title:"Planned arrival overdue; physical arrival unconfirmed",
    eventLabel:"Opened",createdAtUtc:"2026-11-08T17:00:00Z",
    readAtUtc:null,acknowledgedAtUtc:null,version:1
  };
  await signIn(page);
  const option = label => page.locator("#current-organization option")
    .filter({hasText:label}).getAttribute("value");
  const north = await option("M3 North Terminal (Manager)");
  const east = await option("M3 East Terminal (Manager)");
  expect(north).toBeTruthy();
  expect(east).toBeTruthy();
  const feed = JSON.stringify({items:[notice],nextCursor:null,
    snapshotAtUtc:"2026-11-08T17:01:00Z"});
  for (const [org, count] of [[north, {unreadCount:1000,unreadCountCapped:true}],
                              [east, {unreadCount:25,unreadCountCapped:false}]]) {
    await page.route("**/organizations/" + org + "/notifications?*", route =>
      route.fulfill({status:200,contentType:"application/json",body:feed}));
    await page.route("**/organizations/" + org + "/notifications/unread-count", route =>
      route.fulfill({status:200,contentType:"application/json",
        body:JSON.stringify({...count,asOfUtc:"2026-11-08T17:01:00Z"})}));
  }
  const center = page.getByRole("region",{name:"In-app shipment notifications"});
  await page.locator("#current-organization")
    .selectOption({label:"M3 North Terminal (Manager)"});
  await expect(center.getByText("(1000+ unread)",{exact:false})).toBeVisible();
  await page.locator("#current-organization")
    .selectOption({label:"M3 East Terminal (Manager)"});
  await expect(center.getByText("(25 unread)",{exact:false})).toBeVisible();
  await expect(center.getByText("25+",{exact:false})).toHaveCount(0);
  await expect(center.getByText("1000+",{exact:false})).toHaveCount(0);
});

test("G15 notification center never grants administrator shipment visibility", async ({page}) => {
  test.skip(!enabled,"Disposable organization fixtures required");
  await signIn(page,"Administrator");
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
  // The successful MarkRead refresh must release its own busy lock.
  await expect(center.getByRole("button",{name:"Acknowledge notification"}))
    .toBeEnabled();
  await center.getByRole("button",{name:"Acknowledge notification"}).click();
  await expect(center.getByRole("button",{name:"Acknowledge notification"}))
    .toHaveCount(0);
  await expect(center.locator("li p").filter({hasText:/\|\s*Acknowledged$/})).toBeVisible();
});

test("G15 rejected notification refresh drops protected data and organization selection",async({page})=>{
  test.skip(!enabled,"Disposable organization fixtures required");
  await signIn(page);
  const north=await page.locator("#current-organization option")
    .filter({hasText:"M3 North Terminal (Manager)"}).getAttribute("value");
  expect(north).toBeTruthy();
  let revoked=false;
  // Simulate a real membership removal as well as the notification denial.
  // The authoritative organization refresh must not select a revoked tenant.
  await page.route("**/api/v1/me/organizations",route=>
    revoked ? route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({organizations:[]})}) : route.continue());
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
  // The initial organization selection starts its own notification refresh.
  // Verify that the authorized request has settled before revoking access,
  // so this test exercises the next refresh rather than racing the first one.
  const refresh= center.getByRole("button",{name:"Refresh notifications"});
  await expect(refresh).toBeEnabled();
  revoked=true;
  await refresh.click();
  await expect(center).toHaveCount(0);
  await expect(page.locator("#current-organization")).toHaveCount(0);
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


test("G15 a cancelled pagination result cannot leave Load more disabled", async ({ page }) => {
  test.skip(!enabled, "Disposable organization fixtures required");
  await signIn(page);
  const north = await page.locator("#current-organization option")
    .filter({ hasText:"M3 North Terminal (Manager)" }).getAttribute("value");
  expect(north).toBeTruthy();
  const notices = Array.from({ length:25 }, (_, index) => ({
    id:"d0000000-0000-4000-8000-"+String(index+1).padStart(12,"0"),
    shipmentId:"d1000000-0000-4000-8000-000000000001",
    exceptionId:"d2000000-0000-4000-8000-000000000001",
    eventType:"ExceptionOpened", ruleCode:"ArrivalScheduleOverdue",
    title:"Planned arrival overdue; physical arrival unconfirmed",
    eventLabel:"Opened", createdAtUtc:"2026-11-08T18:00:00Z",
    readAtUtc:null, acknowledgedAtUtc:null, state:"Unread",version:1
  }));
  let releasePage;
  await page.route("**/organizations/"+north+"/notifications?*", async route => {
    const query = new URL(route.request().url()).searchParams;
    if (query.has("cursor"))
      await new Promise(resolve => { releasePage = resolve; });
    return route.fulfill({ status:200, contentType:"application/json",
      body:JSON.stringify({ items:query.has("cursor") ? [] : notices,
        nextCursor:query.has("cursor") ? null : "synthetic-page-token",
        snapshotAtUtc:"2026-11-08T18:01:00Z" }) });
  });
  await page.route("**/organizations/"+north+"/notifications/unread-count",
    route => route.fulfill({ status:200,contentType:"application/json",
      body:JSON.stringify({ unreadCount:25, asOfUtc:"2026-11-08T18:01:00Z" }) }));
  await page.locator("#current-organization")
    .selectOption({ label:"M3 North Terminal (Manager)" });
  const center=page.getByRole("region",{name:"In-app shipment notifications"});
  const more=center.getByRole("button",{name:"Load more notifications"});
  await expect(more).toBeEnabled();
  await more.click();
  await expect.poll(() => typeof releasePage).toBe("function");
  await center.getByRole("button",{name:"Refresh notifications"}).click();
  releasePage();
  await expect(more).toBeEnabled();
});


test("G15 transient notification error is visible and retry clears it", async ({ page }) => {
  test.skip(!enabled, "Disposable organization fixtures required");
  await signIn(page);
  const north = await page.locator("#current-organization option")
    .filter({ hasText: "M3 North Terminal (Manager)" }).getAttribute("value");
  expect(north).toBeTruthy();
  let failing = true;
  await page.route("**/organizations/" + north + "/notifications?*", route =>
    route.fulfill({ status: failing ? 503 : 200,
      contentType: "application/json",
      body: JSON.stringify(failing
        ? { code: "notification_authority_unavailable", message: "Temporarily unavailable" }
        : { items: [], nextCursor: null, snapshotAtUtc: "2026-11-09T12:00:00Z" })
    }));
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  const center = page.getByRole("region", { name: "In-app shipment notifications" });
  await expect(center.getByRole("alert")).toBeVisible();
  failing = false;
  const refresh = center.getByRole("button", { name: "Refresh notifications" });
  await expect(refresh).toBeEnabled();
  await refresh.click();
  await expect(center.getByRole("alert")).toHaveCount(0);
  await expect(center.getByText(/No notifications are available for this filter/))
    .toBeVisible();
});

test("G15 late response from old organization never appears after switching", async ({ page }) => {
  test.skip(!enabled, "Disposable organization fixtures required");
  await signIn(page);
  const north = await page.locator("#current-organization option")
    .filter({ hasText: "M3 North Terminal (Manager)" }).getAttribute("value");
  expect(north).toBeTruthy();
  let release;
  let began;
  const queued = new Promise(resolve => { began = resolve; });
  await page.route("**/organizations/" + north + "/notifications?*", async route => {
    began();
    await new Promise(resolve => { release = resolve; });
    await route.fulfill({
      status: 200, contentType: "application/json",
      body: JSON.stringify({
        items: [{
          id: "e0000000-0000-4000-8000-000000000003",
          shipmentId: "e0000000-0000-4000-8000-000000000004",
          exceptionId: "e0000000-0000-4000-8000-000000000005",
          eventType: "ExceptionOpened", ruleCode: "ArrivalScheduleOverdue",
          title: "Old organization private notification",
          eventLabel: "Opened", createdAtUtc: "2026-11-09T12:00:00Z",
          readAtUtc: null, acknowledgedAtUtc: null, state: "Unread", version: 1
        }],
        nextCursor: null, snapshotAtUtc: "2026-11-09T12:00:00Z"
      })
    }).catch(() => {});
  });
  await page.locator("#current-organization").selectOption({
    label: "M3 North Terminal (Manager)"
  });
  await queued;
  await page.locator("#current-organization").selectOption({
    label: "M3 East Terminal (Manager)"
  });
  release();
  await expect(page.getByText("Old organization private notification")).toHaveCount(0);
  const center = page.getByRole("region", { name: "In-app shipment notifications" });
  await expect(center).toBeVisible();
  await expect(center.getByText("Old organization private notification")).toHaveCount(0);
});


test("G15 logout clears protected feed data before another authentication", async ({page}) => {
  test.skip(!enabled, "Disposable organization fixtures required");
  await signIn(page);
  const north = await page.locator("#current-organization option")
    .filter({hasText:"M3 North Terminal (Manager)"}).getAttribute("value");
  expect(north).toBeTruthy();
  await page.route("**/organizations/" + north + "/notifications?*", route =>
    route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({
        items:[{
          id:"e0000000-0000-4000-8000-000000000011",
          shipmentId:"e0000000-0000-4000-8000-000000000012",
          exceptionId:"e0000000-0000-4000-8000-000000000013",
          eventType:"ExceptionOpened",ruleCode:"ArrivalScheduleOverdue",
          title:"Private scoped notice for logout verification",
          eventLabel:"Opened",createdAtUtc:"2026-11-09T12:00:00Z",
          readAtUtc:null,acknowledgedAtUtc:null,state:"Unread",version:1
        }],
        nextCursor:null,snapshotAtUtc:"2026-11-09T12:01:00Z"
      })}));
  await page.locator("#current-organization").selectOption({
    label:"M3 North Terminal (Manager)"
  });
  const center=page.getByRole("region",{name:"In-app shipment notifications"});
  await expect(center.getByText("Private scoped notice for logout verification"))
    .toBeVisible();
  await page.getByRole("button",{name:"Log out",exact:true}).click();
  await expect(page.getByText("Private scoped notice for logout verification"))
    .toHaveCount(0);
  await expect(center).toHaveCount(0);
  await expect(page.getByLabel("Email")).toBeVisible();
});


test("G15 successful browser pagination appends distinct second-page receipts in order", async ({page}) => {
  test.skip(!enabled, "Disposable organization fixtures required");
  await signIn(page);
  const north = await page.locator("#current-organization option")
    .filter({hasText:"M3 North Terminal (Manager)"}).getAttribute("value");
  expect(north).toBeTruthy();
  const receipt = index => ({
    id:"a1000000-0000-4000-8000-"+String(index).padStart(12,"0"),
    shipmentId:"a2000000-0000-4000-8000-000000000001",
    exceptionId:"a3000000-0000-4000-8000-000000000001",
    eventType:"ExceptionOpened",ruleCode:"ArrivalScheduleOverdue",
    title:"Scoped pagination receipt "+String(index).padStart(2,"0"),
    eventLabel:"Opened",createdAtUtc:"2026-11-08T18:00:00Z",
    readAtUtc:null,acknowledgedAtUtc:null,state:"Unread",version:1
  });
  let seenSecondPage = 0;
  await page.route("**/organizations/"+north+"/notifications?*",route=>{
    const cursor=new URL(route.request().url()).searchParams.get("cursor");
    if(cursor) {
      expect(cursor).toBe("source-bound-next-page");
      seenSecondPage++;
    }
    const items=cursor ? [receipt(25),receipt(26),receipt(27)] :
      Array.from({length:25},(_,i)=>receipt(i+1));
    return route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({items,nextCursor:cursor?null:"source-bound-next-page",
        snapshotAtUtc:"2026-11-08T18:01:00Z"})});
  });
  await page.route("**/organizations/"+north+"/notifications/unread-count",route=>
    route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({unreadCount:27,unreadCountCapped:false,
        asOfUtc:"2026-11-08T18:01:00Z"})}));
  await page.locator("#current-organization")
    .selectOption({label:"M3 North Terminal (Manager)"});
  const center=page.getByRole("region",{name:"In-app shipment notifications"});
  await expect(center.locator("li")).toHaveCount(25);
  await center.getByRole("button",{name:"Load more notifications"}).click();
  await expect(center.locator("li")).toHaveCount(27);
  const titles=await center.locator("li p.font-medium").allTextContents();
  expect(titles).toEqual(Array.from({length:27},(_,i)=>
    "Scoped pagination receipt "+String(i+1).padStart(2,"0")));
  expect(seenSecondPage).toBe(1);
  await expect(center.getByRole("button",{name:"Load more notifications"})).toHaveCount(0);
});

test("G15 pending notification request displays and clears the loading state", async ({page}) => {
  test.skip(!enabled,"Disposable organization fixtures required");
  await signIn(page);
  const north=await page.locator("#current-organization option")
    .filter({hasText:"M3 North Terminal (Manager)"}).getAttribute("value");
  expect(north).toBeTruthy();
  await page.locator("#current-organization")
    .selectOption({label:"M3 North Terminal (Manager)"});
  const center=page.getByRole("region",{name:"In-app shipment notifications"});
  await expect(center.getByRole("button",{name:"Refresh notifications"})).toBeEnabled();
  let release;
  let started;
  const requestStarted=new Promise(resolve=>{started=resolve;});
  await page.route("**/organizations/"+north+"/notifications?*",async route=>{
    started();
    await new Promise(resolve=>{release=resolve;});
    await route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({items:[],nextCursor:null,
        snapshotAtUtc:"2026-11-08T18:01:00Z"})}).catch(()=>{});
  });
  await center.getByRole("button",{name:"Refresh notifications"}).click();
  await requestStarted;
  await expect(center.getByRole("status").getByText("Loading notifications...")).toBeVisible();
  release();
  await expect(center.getByRole("status")).toHaveCount(0);
  await expect(center.getByText("No notifications are available for this filter.",
    {exact:false})).toBeVisible();
});

test("G15 archived historical alert navigates to an authorized real archived shipment as read only", async ({page}) => {
  test.skip(!enabled,"Disposable organization fixtures required");
  await signIn(page);
  const north=await page.locator("#current-organization option")
    .filter({hasText:"M3 North Terminal (Manager)"}).getAttribute("value");
  expect(north).toBeTruthy();
  await page.locator("#current-organization")
    .selectOption({label:"M3 North Terminal (Manager)"});
  const archivedResponse=await page.request.get(
    "/api/v1/organizations/"+north+"/shipments?recordStatus=All&q=M5-ARCHIVE-N&limit=100");
  expect(archivedResponse.ok()).toBeTruthy();
  const archived=(await archivedResponse.json()).shipments.find(
    item=>item.referenceCode==="M5-ARCHIVE-N");
  expect(archived?.recordStatus).toBe("Archived");
  const notice={
    id:"a4000000-0000-4000-8000-000000000001",
    shipmentId:archived.id,
    exceptionId:"a5000000-0000-4000-8000-000000000001",
    eventType:"ExceptionAutoResolved",ruleCode:"ArrivalScheduleOverdue",
    title:"Historical archived shipment alert",eventLabel:"Closed by evaluation",
    createdAtUtc:"2026-11-08T18:00:00Z",readAtUtc:null,
    acknowledgedAtUtc:null,state:"Unread",version:1
  };
  await page.route("**/organizations/"+north+"/notifications?*",route=>
    route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({items:[notice],nextCursor:null,
        snapshotAtUtc:"2026-11-08T18:01:00Z"})}));
  await page.route("**/organizations/"+north+"/notifications/unread-count",route=>
    route.fulfill({status:200,contentType:"application/json",
      body:JSON.stringify({unreadCount:1,unreadCountCapped:false,
        asOfUtc:"2026-11-08T18:01:00Z"})}));
  const center=page.getByRole("region",{name:"In-app shipment notifications"});
  await center.getByRole("button",{name:"Refresh notifications"}).click();
  await expect(center.getByText("Historical archived shipment alert")).toBeVisible();
  await center.getByRole("button",{name:"Open shipment exception"}).click();
  const panel=page.getByRole("heading",{name:"Shipments",exact:true})
    .locator("xpath=ancestor::section[1]");
  await expect(panel.getByRole("heading",{name:"M5-ARCHIVE-N"})).toBeVisible();
  await expect(panel.getByLabel("Shipment workflow")).toBeDisabled();
  await expect(panel.getByRole("button",{name:"Save shipment changes"})).toHaveCount(0);
  // Current API still governs the archived target. The mock covers only the
  // historical personal alert; it cannot supply or grant shipment authority.
  const detail=await page.request.get(
    "/api/v1/organizations/"+north+"/shipments/"+archived.id);
  expect(detail.ok()).toBeTruthy();
  expect((await detail.json()).recordStatus).toBe("Archived");
});
