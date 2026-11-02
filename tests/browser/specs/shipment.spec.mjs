import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M5_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);
async function login(page, email) {
  await page.goto("/");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
}
const sectionOf = page => page.getByRole("heading", { name: "Shipments", exact: true })
  .locator("xpath=ancestor::section[1]");
test("M5 Analyst creates and advances a scoped shipment", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(120_000);
  await login(page, "m3-analyst@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Analyst)" });
  const s = sectionOf(page);
  await expect(s.getByRole("button", { name: "Create shipment" })).toBeEnabled();
  await s.getByRole("button", { name: "Create shipment" }).click();
  const suffix = randomUUID().replaceAll("-", "").slice(0, 10).toUpperCase();
  const code = "M5-" + suffix;
  await s.getByLabel("Shipment reference code").fill(code);
  await s.getByLabel("Shipment display name").fill("Synthetic M5 " + suffix);
  await s.getByLabel("Origin port").selectOption({ index: 1 });
  await s.getByLabel("Destination port").selectOption({ index: 2 });
  await s.getByRole("button", { name: "Save new shipment" }).click();
  await expect(s.getByRole("button",{name:"Close detail"})).toHaveCount(0);
  await s.getByLabel("Search shipments").fill(code);
  await expect(s.getByText(code, { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await expect(s.getByText("Manual status history")).toBeVisible();
  await s.getByLabel("Shipment workflow").selectOption("Booked");
  await s.getByRole("button", { name: "Save shipment changes" }).click();
  await expect(s.getByRole("button",{name:"Close detail"})).toHaveCount(0);
  await s.getByLabel("Search shipments").fill(code);
  await expect(s.getByText("Booked", { exact: false })).toBeVisible();
  page.once("dialog", dialog => dialog.accept());
  await s.getByRole("button", { name: "Archive shipment" }).first().click();
  await expect(s.getByText(code, { exact: true })).toHaveCount(0);
  await s.getByLabel("Shipment record status").selectOption("Archived");
  await expect(s.getByText(code, { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "Restore shipment" }).first().click();
  await s.getByLabel("Shipment record status").selectOption("Active");
  await expect(s.getByText(code, { exact: true })).toBeVisible();
});
test("M5 Auditor shipment view does not grant M4 management", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  await login(page, "m4-auditor@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Auditor)" });
  const s = sectionOf(page);
  await expect(s).toBeVisible();
  await expect(s.getByRole("button", { name: "Create shipment" })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Voyages and containers" })).toHaveCount(0);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await expect(s.getByRole("button", { name: "Save shipment changes" })).toHaveCount(0);
  await expect(s.getByText(/DEMO-PORT-1/)).toBeVisible();
  await expect(s.getByText(/DEMO-PORT-2/)).toBeVisible();
});
test("M5 tenant change invalidates prior shipment list", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  await login(page, "m3-manager@example.test");
  const select = page.locator("#current-organization");
  await select.selectOption({ label: "M3 North Terminal (Manager)" });
  const s = sectionOf(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await select.selectOption({ label: "M3 East Terminal (Manager)" });
  const east = sectionOf(page);
  await east.getByLabel("Search shipments").fill("M5-DEMO-EAST");
  await expect(east.getByText("M5-DEMO-EAST", { exact: true })).toBeVisible();
  await expect(east.getByText("M5-DEMO-NORTH", { exact: true })).toHaveCount(0);
});

test("M5 Manager advances terminal workflows and retains manual history", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  await login(page,"m3-manager@example.test");
  await page.locator("#current-organization").selectOption({label:"M3 North Terminal (Manager)"});
  const s=sectionOf(page);
  const code="M5-TERM-"+randomUUID().slice(0,8).toUpperCase();
  await expect(s.getByRole("button",{name:"Create shipment"})).toBeEnabled();
  await s.getByRole("button",{name:"Create shipment"}).click();
  await s.getByLabel("Shipment reference code").fill(code);
  await s.getByLabel("Origin port").selectOption({index:1});
  await s.getByLabel("Destination port").selectOption({index:2});
  await s.getByRole("button",{name:"Save new shipment"}).click();
  await expect(s.getByRole("button",{name:"Close detail"})).toHaveCount(0);
  await s.getByLabel("Search shipments").fill(code);
  for(const state of ["Booked","InTransit","Arrived","Completed"]){
    await expect(s.getByText(code,{exact:true})).toBeVisible();
    await s.getByRole("button",{name:"View shipment"}).first().click();
    await s.getByLabel("Shipment workflow").selectOption(state);
    await s.getByRole("button",{name:"Save shipment changes"}).click();
    await expect(s.getByRole("button",{name:"Close detail"})).toHaveCount(0);
  }
  await s.getByRole("button",{name:"View shipment"}).first().click();
  await expect(s.getByRole("button",{name:"Save shipment changes"})).toBeVisible();
  await expect(s.getByLabel("Shipment workflow").locator("option")).toHaveCount(1);
  await expect(s.getByText(/v5: Arrived to Completed/)).toBeVisible();
  await s.getByRole("button",{name:"Close detail"}).click();
  const cancelCode="M5-CAN-"+randomUUID().slice(0,8).toUpperCase();
  await s.getByRole("button",{name:"Create shipment"}).click();
  await s.getByLabel("Shipment reference code").fill(cancelCode);
  await s.getByLabel("Origin port").selectOption({index:1});
  await s.getByLabel("Destination port").selectOption({index:2});
  await s.getByRole("button",{name:"Save new shipment"}).click();
  await expect(s.getByRole("button",{name:"Close detail"})).toHaveCount(0);
  await s.getByLabel("Search shipments").fill(cancelCode);
  await s.getByRole("button",{name:"View shipment"}).first().click();
  await s.getByLabel("Shipment workflow").selectOption("Cancelled");
  await s.getByRole("button",{name:"Save shipment changes"}).click();
  await expect(s.getByRole("button",{name:"Close detail"})).toHaveCount(0);
  await s.getByRole("button",{name:"View shipment"}).first().click();
  await expect(s.getByLabel("Shipment workflow").locator("option")).toHaveCount(1);
});
test("M5 failed parent catalog retries without suppressing shipment list", async ({ page }) => {
  test.skip(!enabled,"Disposable M5 fixture required");
  await login(page,"m3-manager@example.test");
  let attempts=0;
  let recovered=false;
  await page.route("**/api/v1/organizations/*/ports?*",async route=>{
    attempts++;
    if(!recovered) await route.fulfill({status:503,contentType:"application/json",
      body:JSON.stringify({code:"dependency_unavailable",message:"Database temporarily unavailable"})});
    else await route.continue();
  });
  await page.locator("#current-organization").selectOption({label:"M3 North Terminal (Manager)"});
  const s=sectionOf(page);
  await expect(s.getByText("M5-DEMO-NORTH",{exact:true})).toBeVisible();
  await expect(s.getByRole("button",{name:"Retry port catalog"})).toBeVisible();
  await expect(s.getByRole("button",{name:"Create shipment"})).toBeDisabled();
  recovered=true;
  await s.getByRole("button",{name:"Retry port catalog"}).click();
  await expect(s.getByRole("button",{name:"Create shipment"})).toBeEnabled();
  await expect(s.getByRole("button",{name:"Retry port catalog"})).toHaveCount(0);
  expect(attempts).toBeGreaterThanOrEqual(2);
});
test("M5 stale edit displays conflict and retains entered values",async ({page,context})=>{
  test.skip(!enabled,"Disposable M5 fixture required");
  await login(page,"m3-manager@example.test");
  await page.locator("#current-organization").selectOption({label:"M3 North Terminal (Manager)"});
  const first=sectionOf(page);
  const code="M5-CONF-"+randomUUID().slice(0,8).toUpperCase();
  await expect(first.getByRole("button",{name:"Create shipment"})).toBeEnabled();
  await first.getByRole("button",{name:"Create shipment"}).click();
  await first.getByLabel("Shipment reference code").fill(code);
  await first.getByLabel("Origin port").selectOption({index:1});
  await first.getByLabel("Destination port").selectOption({index:2});
  await first.getByRole("button",{name:"Save new shipment"}).click();
  await expect(first.getByRole("button",{name:"Close detail"})).toHaveCount(0);
  await first.getByLabel("Search shipments").fill(code);
  await first.getByRole("button",{name:"View shipment"}).first().click();
  const secondPage=await context.newPage();
  try{
    await secondPage.goto("/");
    if(await secondPage.getByLabel("Email").count()) await login(secondPage,"m3-manager@example.test");
    await secondPage.locator("#current-organization").selectOption({label:"M3 North Terminal (Manager)"});
    const second=sectionOf(secondPage);
    await second.getByLabel("Search shipments").fill(code);
    await second.getByRole("button",{name:"View shipment"}).first().click();
    await first.getByLabel("Shipment display name").fill("First writer");
    await first.getByRole("button",{name:"Save shipment changes"}).click();
    await expect(first.getByRole("button",{name:"Close detail"})).toHaveCount(0);
    await second.getByLabel("Shipment display name").fill("Preserved second edit");
    await second.getByRole("button",{name:"Save shipment changes"}).click();
    await expect(second.getByRole("button",{name:"Load current version"})).toBeVisible();
    await expect(second.getByLabel("Shipment display name")).toHaveValue("Preserved second edit");
    await second.getByRole("button",{name:"Load current version"}).click();
    await second.getByRole("button",{name:"Save shipment changes"}).click();
    await expect(second.getByRole("button",{name:"Close detail"})).toHaveCount(0);
    await expect(second.getByText(code,{exact:true})).toBeVisible();
  }finally{await secondPage.close();}
});
test("M5 deferred shipment response cannot cross organization switch",async ({page})=>{
  test.skip(!enabled,"Disposable M5 fixture required");
  await login(page,"m3-manager@example.test");
  const selection=page.locator("#current-organization");
  const northId=await selection.locator("option",{hasText:"M3 North Terminal (Manager)"})
    .getAttribute("value");
  expect(northId).toBeTruthy();
  let release;
  const held=new Promise(resolve=>{release=resolve;});
  let waiting;
  const entered=new Promise(resolve=>{waiting=resolve;});
  let intercepted=false;
  await page.route("**/api/v1/organizations/*/shipments?*",async route=>{
    if(!intercepted && route.request().url().includes("/"+northId+"/shipments?") &&
      route.request().method()==="GET"){
      intercepted=true;waiting();
      await held;
    }
    await route.continue();
  });
  try{
    await selection.selectOption({label:"M3 North Terminal (Manager)"});
    await Promise.race([entered,new Promise((_,reject)=>setTimeout(()=>reject(Error("Held read not observed")),7000))]);
    await selection.selectOption({label:"M3 East Terminal (Manager)"});
  }finally{release?.();}
  const east=sectionOf(page);
  await east.getByLabel("Search shipments").fill("M5-DEMO-EAST");
  await expect(east.getByText("M5-DEMO-EAST",{exact:true})).toBeVisible();
  await expect(east.getByText("M5-DEMO-NORTH",{exact:true})).toHaveCount(0);
});

test("M5 logout clears selected shipment details and cached rows",async ({page})=>{
  test.skip(!enabled,"Disposable M5 fixture required");
  await login(page,"m3-manager@example.test");
  await page.locator("#current-organization").selectOption({label:"M3 North Terminal (Manager)"});
  const s=sectionOf(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH",{exact:true})).toBeVisible();
  await s.getByRole("button",{name:"View shipment"}).first().click();
  await expect(s.getByText("Manual status history")).toBeVisible();
  await page.getByRole("button",{name:"Log out",exact:true}).click();
  await expect(page.getByLabel("Email")).toBeVisible();
  await expect(page.getByText("Manual status history")).toHaveCount(0);
  await expect(page.getByText("M5-DEMO-NORTH",{exact:true})).toHaveCount(0);
});

// ---- Tab visibility, draft preservation and protected-state invalidation -------------------
// Headless engines never change document.visibilityState on a tab switch (probe evidence is kept
// with the G5 evidence). The tests therefore bring a genuine second page to the front and then
// drive visibility the way the app reads it: the state property plus the browser event.
const apiOrigin = "http://localhost:5173";
const draftDeparture = "2026-12-01T00:00:00Z";
const draftArrival = "2026-12-05T00:00:00Z";
const departureLabel = "Planned departure UTC (ISO 8601)";
const arrivalLabel = "Planned arrival UTC (ISO 8601)";

async function api(request, method, path, data) {
  const token = (await (await request.get("/api/v1/auth/csrf")).json()).token;
  return request.fetch(path, {
    method, headers: { Origin: apiOrigin, "X-HarborOps-CSRF": token }, data
  });
}
async function organization(request, slug) {
  const list = (await (await request.get("/api/v1/me/organizations")).json()).organizations;
  const row = list.find(x => x.slug === slug);
  expect(row, "organization " + slug).toBeTruthy();
  return row;
}
async function createShipmentApi(request, org, code, name = "Base name") {
  const root = "/api/v1/organizations/" + org.id;
  const ports = (await (await request.get(root + "/ports?status=Active&limit=100")).json()).ports;
  const from = ports.find(p => p.referenceCode === "DEMO-PORT-1");
  const to = ports.find(p => p.referenceCode === "DEMO-PORT-2");
  expect(from && to).toBeTruthy();
  const created = await api(request, "POST", root + "/shipments", {
    referenceCode: code, displayName: name, originPortId: from.id, destinationPortId: to.id
  });
  expect(created.status()).toBe(201);
  return created.json();
}
async function setVisibility(page, state) {
  await page.evaluate(next => {
    Object.defineProperty(document, "visibilityState", { configurable: true, get: () => next });
    Object.defineProperty(document, "hidden", { configurable: true, get: () => next === "hidden" });
    document.dispatchEvent(new Event("visibilitychange"));
  }, state);
}
async function leaveTab(page, other) {
  await other.bringToFront();
  await setVisibility(page, "hidden");
}
function shipmentReads(page) {
  return page.waitForResponse(r => r.request().method() === "GET" &&
    /\/shipments(\?|\/[0-9a-f-]{36}$)/.test(new URL(r.url()).pathname + new URL(r.url()).search));
}
async function returnToTab(page) {
  const refreshed = shipmentReads(page);
  await page.bringToFront();
  await setVisibility(page, "visible");
  await refreshed;
}
async function openNorthManager(page) {
  await login(page, "m3-manager@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Manager)" });
  const s = sectionOf(page);
  await expect(s.getByRole("button", { name: "Create shipment" })).toBeEnabled();
  return s;
}
async function openShipment(s, code) {
  await s.getByLabel("Search shipments").fill(code);
  await expect(s.getByText(code, { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await expect(s.getByText("Manual status history")).toBeVisible();
  await expect(s.getByText(/DEMO-PORT-1/)).toBeVisible();
}
async function typeDraft(s, name = "Local draft name") {
  await s.getByLabel("Shipment display name").fill(name);
  await s.getByLabel(departureLabel).fill(draftDeparture);
  await s.getByLabel(arrivalLabel).fill(draftArrival);
  await expect(s.getByText("Unsaved changes")).toBeVisible();
}
async function expectDraft(s, name = "Local draft name") {
  await expect(s.getByLabel("Shipment display name")).toHaveValue(name);
  await expect(s.getByLabel(departureLabel)).toHaveValue(draftDeparture);
  await expect(s.getByLabel(arrivalLabel)).toHaveValue(draftArrival);
  await expect(s.getByText("Unsaved changes")).toBeVisible();
}
const unique = prefix => prefix + "-" + randomUUID().slice(0, 8).toUpperCase();

test("M5 unsaved shipment edits survive leaving and returning to the tab", async ({ page, context }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(90_000);
  const s = await openNorthManager(page);
  const north = await organization(page.request, "m3-north");
  const code = unique("M5-VIS");
  await createShipmentApi(page.request, north, code);
  await openShipment(s, code);
  await typeDraft(s);
  const other = await context.newPage();
  await other.goto("/");
  await leaveTab(page, other);
  await returnToTab(page);
  await expectDraft(s);
  await expect(s.getByRole("heading", { name: code, level: 5 })).toBeVisible();
  await expect(s.getByRole("button", { name: "Save shipment changes" })).toBeEnabled();
  await expect(s.getByRole("button", { name: "Load current version" })).toHaveCount(0);
  await other.close();
});

test("M5 unsaved edits survive a background refresh that is still in flight", async ({ page, context }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(90_000);
  const s = await openNorthManager(page);
  const north = await organization(page.request, "m3-north");
  const code = unique("M5-HOLD");
  await createShipmentApi(page.request, north, code);
  await openShipment(s, code);
  await typeDraft(s);
  const other = await context.newPage();
  await other.goto("/");
  await leaveTab(page, other);
  let hold = true;
  let release;
  const gate = new Promise(resolve => { release = resolve; });
  let heldRequests = 0;
  let entered;
  const firstHeld = new Promise(resolve => { entered = resolve; });
  await page.route("**/api/v1/organizations/*/shipments**", async route => {
    const url = new URL(route.request().url());
    const read = route.request().method() === "GET" &&
      /\/shipments(\/[0-9a-f-]{36})?$/.test(url.pathname);
    if (hold && read) { heldRequests++; entered(); await gate; }
    await route.continue();
  });
  await page.bringToFront();
  await setVisibility(page, "visible");
  await firstHeld;
  // While the refresh is held, the draft is untouched and still editable.
  await expectDraft(s);
  await s.getByLabel("Shipment display name").fill("Local draft name plus typing during refresh");
  hold = false;
  release();
  await expect(s.getByText("Updating list...")).toHaveCount(0);
  await expect(s.getByLabel("Shipment display name")).toHaveValue("Local draft name plus typing during refresh");
  await expect(s.getByLabel(departureLabel)).toHaveValue(draftDeparture);
  await expect(s.getByLabel(arrivalLabel)).toHaveValue(draftArrival);
  await expect(s.getByText("Unsaved changes")).toBeVisible();
  await expect(s.getByRole("heading", { name: code, level: 5 })).toBeVisible();
  expect(heldRequests).toBeGreaterThan(0);
  await other.close();
});

test("M5 server change while away keeps the local draft and merges only on request", async ({ page, context }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(120_000);
  const s = await openNorthManager(page);
  const north = await organization(page.request, "m3-north");
  const code = unique("M5-MERGE");
  const row = await createShipmentApi(page.request, north, code);
  const url = "/api/v1/organizations/" + north.id + "/shipments/" + row.id;
  await openShipment(s, code);
  await typeDraft(s);
  const other = await context.newPage();
  await other.goto("/");
  await leaveTab(page, other);
  // A concurrent writer changes the same display name and advances the workflow.
  const changed = await api(context.request, "PATCH", url, {
    displayName: "Server name", workflowStatus: "Booked", expectedVersion: 1 });
  expect(changed.status()).toBe(200);
  await returnToTab(page);
  const notice = s.getByRole("alert").filter({ hasText: "changed on the server" });
  await expect(notice).toBeVisible();
  // Nothing was silently replaced: draft, base workflow options and version are unchanged.
  await expectDraft(s);
  await expect(s.getByLabel("Shipment workflow").locator("option")).toHaveText(["Draft", "Booked", "Cancelled"]);
  await s.getByRole("button", { name: "Load current version" }).click();
  const merged = s.getByRole("alert").filter({ hasText: "Current version 2 loaded" });
  await expect(merged).toBeVisible();
  await expect(merged).toContainText("display name");
  await expect(merged).toContainText("also changed");
  // Edited fields keep the user's values; the untouched workflow follows the server.
  await expectDraft(s);
  await expect(s.getByLabel("Shipment workflow")).toHaveValue("Booked");
  await s.getByRole("button", { name: "Save shipment changes" }).click();
  await expect(s.getByRole("button", { name: "Close detail" })).toHaveCount(0);
  const saved = await (await context.request.get(url)).json();
  expect(saved.displayName).toBe("Local draft name");
  expect(saved.workflowStatus).toBe("Booked");
  expect(saved.version).toBe(3);
  expect(Date.parse(saved.plannedDepartureAtUtc)).toBe(Date.parse(draftDeparture));
  expect(Date.parse(saved.plannedArrivalAtUtc)).toBe(Date.parse(draftArrival));
  await other.close();
});

test("M5 stale save after a server change keeps the exact local draft", async ({ page, context }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(120_000);
  const s = await openNorthManager(page);
  const north = await organization(page.request, "m3-north");
  const code = unique("M5-STALE");
  const row = await createShipmentApi(page.request, north, code);
  const url = "/api/v1/organizations/" + north.id + "/shipments/" + row.id;
  await openShipment(s, code);
  await typeDraft(s);
  const other = await context.newPage();
  await other.goto("/");
  await leaveTab(page, other);
  const changed = await api(context.request, "PATCH", url, { displayName: "Server name", expectedVersion: 1 });
  expect(changed.status()).toBe(200);
  await returnToTab(page);
  await expect(s.getByRole("alert").filter({ hasText: "changed on the server" })).toBeVisible();
  // Saving without reviewing is rejected by the version check and must not discard the draft.
  await s.getByRole("button", { name: "Save shipment changes" }).click();
  await expect(s.getByRole("alert").filter({ hasText: "Your edits are preserved" })).toBeVisible();
  await expectDraft(s);
  const unchanged = await (await context.request.get(url)).json();
  expect(unchanged.displayName).toBe("Server name");
  expect(unchanged.version).toBe(2);
  // Reviewing the current version and saving again succeeds exactly once.
  await s.getByRole("button", { name: "Load current version" }).click();
  await expect(s.getByRole("alert").filter({ hasText: "Current version 2 loaded" })).toBeVisible();
  await expectDraft(s);
  await s.getByRole("button", { name: "Save shipment changes" }).click();
  await expect(s.getByRole("button", { name: "Close detail" })).toHaveCount(0);
  const final = await (await context.request.get(url)).json();
  expect(final.displayName).toBe("Local draft name");
  expect(final.version).toBe(3);
  const history = await (await context.request.get(url + "/status-changes")).json();
  expect(history.changes).toHaveLength(1);
  await other.close();
});

test("M5 organization switch drops the open shipment and its draft", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(90_000);
  const select = page.locator("#current-organization");
  const s = await openNorthManager(page);
  const north = await organization(page.request, "m3-north");
  const code = unique("M5-SWITCH");
  await createShipmentApi(page.request, north, code);
  await openShipment(s, code);
  await typeDraft(s, "North private draft");
  await select.selectOption({ label: "M3 East Terminal (Manager)" });
  const east = sectionOf(page);
  await expect(east.getByRole("button", { name: "Create shipment" })).toBeEnabled();
  await expect(page.getByText(code)).toHaveCount(0);
  await expect(page.locator("input[aria-label='Shipment display name']")).toHaveCount(0);
  await expect(page.locator("body")).not.toContainText("North private draft");
  await expect(east.getByText("Unsaved changes")).toHaveCount(0);
  // Returning to North starts from a clean workspace, not a restored draft.
  await select.selectOption({ label: "M3 North Terminal (Manager)" });
  const back = sectionOf(page);
  await expect(back.getByRole("button", { name: "Create shipment" })).toBeEnabled();
  await expect(back.getByRole("button", { name: "Close detail" })).toHaveCount(0);
  await expect(back.getByText("Unsaved changes")).toHaveCount(0);
  await expect(page.locator("body")).not.toContainText("North private draft");
});

test("M5 real access revocation removes the open shipment and its draft", async ({ page, browser, context }, info) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(150_000);
  const engine = info.project.name;
  const label = "M3 Revocation " + engine;
  const adminContext = await browser.newContext({ baseURL: apiOrigin });
  const administrator = await adminContext.newPage();
  let memberRoot = null;
  let managerId = null;
  const roster = async root => (await (await administrator.request.get(
    root + "/memberships?limit=100")).json()).memberships;
  const setRole = async (root, userId, role) => {
    const current = (await roster(root)).find(x => x.userId === userId);
    const res = await api(administrator.request, "PUT", root + "/memberships/" + userId,
      { role, status: "Active", expectedVersion: current.version });
    expect(res.status()).toBe(200);
  };
  try {
    await login(administrator, "m3-admin-a@example.test");
    const selected = administrator.waitForResponse(r => r.request().method() === "PUT" &&
      new URL(r.url()).pathname === "/api/v1/auth/session/organization");
    await administrator.locator("#current-organization").selectOption({ label: label + " (Administrator)" });
    expect((await selected).status()).toBe(200);
    const revocationOrg = await organization(administrator.request, "m3-revocation-" + engine);
    memberRoot = "/api/v1/organizations/" + revocationOrg.id;
    await login(page, "m3-manager@example.test");
    managerId = (await (await page.request.get("/api/v1/auth/session")).json()).user.id;
    // Earlier specs may have left this fixture member downgraded; start from Manager.
    await setRole(memberRoot, managerId, "Manager");
    await page.reload();
    await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
    await page.locator("#current-organization").selectOption({ label: label + " (Manager)" });
    const s = sectionOf(page);
    await expect(s.getByRole("button", { name: "Create shipment" }).or(
      s.getByText("Port selector unavailable"))).toBeVisible();
    // Fixture parents for this organization, then a shipment through the real API.
    const nonce = randomUUID().slice(0, 8).toUpperCase();
    const code = "M5-REV-" + nonce;
    const makePort = async suffix => {
      const created = await api(page.request, "POST", memberRoot + "/ports", {
        referenceCode: "REV" + suffix + nonce, name: "Revocation port " + suffix, countryCode: "US" });
      expect(created.status()).toBe(201);
      return (await created.json()).id;
    };
    const originId = await makePort("A");
    const destinationId = await makePort("B");
    const created = await api(page.request, "POST", memberRoot + "/shipments", {
      referenceCode: code, displayName: "Revocation shipment", originPortId: originId, destinationPortId: destinationId });
    expect(created.status()).toBe(201);
    await s.getByRole("button", { name: "Refresh shipments" }).click();
    await s.getByLabel("Search shipments").fill(code);
    await expect(s.getByText(code, { exact: true })).toBeVisible();
    await s.getByRole("button", { name: "View shipment" }).first().click();
    await expect(s.getByText("Manual status history")).toBeVisible();
    await s.getByLabel("Shipment display name").fill("Revoked draft");
    await expect(s.getByText("Unsaved changes")).toBeVisible();
    const other = await context.newPage();
    await other.goto("/");
    await leaveTab(page, other);
    // Real revocation of the shipment permission while the tab is away.
    await setRole(memberRoot, managerId, "Support");
    await page.bringToFront();
    await setVisibility(page, "visible");
    await expect(page.getByRole("heading", { name: "Shipments", exact: true })).toHaveCount(0);
    await expect(page.locator("input[aria-label='Shipment display name']")).toHaveCount(0);
    await expect(page.locator("body")).not.toContainText("Revoked draft");
    await expect(page.locator("body")).not.toContainText(code);
    const denied = await page.request.get(memberRoot + "/shipments");
    expect([403, 404]).toContain(denied.status());
    await other.close();
  } finally {
    if (memberRoot && managerId) {
      try { await setRole(memberRoot, managerId, "Manager"); } catch { /* fixture reset is best effort */ }
    }
    await adminContext.close();
  }
});

test("M5 logout after unsaved edits leaves no shipment state behind", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(90_000);
  const s = await openNorthManager(page);
  const north = await organization(page.request, "m3-north");
  const code = unique("M5-OUT");
  await createShipmentApi(page.request, north, code);
  await openShipment(s, code);
  await typeDraft(s, "Pre-logout private draft");
  await page.getByRole("button", { name: "Log out", exact: true }).click();
  await expect(page.getByLabel("Email")).toBeVisible();
  await expect(page.locator("body")).not.toContainText("Pre-logout private draft");
  await expect(page.locator("body")).not.toContainText(code);
  await login(page, "m3-manager@example.test");
  await page.locator("#current-organization").selectOption({ label: "M3 North Terminal (Manager)" });
  const again = sectionOf(page);
  await expect(again.getByRole("button", { name: "Create shipment" })).toBeEnabled();
  await expect(again.getByText("Unsaved changes")).toHaveCount(0);
  await expect(again.getByRole("button", { name: "Close detail" })).toHaveCount(0);
  await expect(page.locator("input[aria-label='Shipment display name']")).toHaveCount(0);
  await expect(page.locator("body")).not.toContainText("Pre-logout private draft");
});

test("M5 archive and restore leave a draft on another shipment untouched", async ({ page }) => {
  test.skip(!enabled, "Disposable M5 fixture required");
  test.setTimeout(120_000);
  const s = await openNorthManager(page);
  const north = await organization(page.request, "m3-north");
  const prefix = unique("M5-ARC");
  const draftCode = prefix + "-A";
  const archiveCode = prefix + "-B";
  const draftRow = await createShipmentApi(page.request, north, draftCode);
  await createShipmentApi(page.request, north, archiveCode);
  await s.getByLabel("Search shipments").fill(prefix);
  await expect(s.getByText(draftCode, { exact: true })).toBeVisible();
  await expect(s.getByText(archiveCode, { exact: true })).toBeVisible();
  const draftItem = s.getByRole("listitem").filter({ has: s.getByText(draftCode, { exact: true }) });
  const archiveItem = s.getByRole("listitem").filter({ has: s.getByText(archiveCode, { exact: true }) });
  await draftItem.getByRole("button", { name: "View shipment" }).click();
  await expect(s.getByText("Manual status history")).toBeVisible();
  await typeDraft(s);
  page.once("dialog", dialog => dialog.accept());
  await archiveItem.getByRole("button", { name: "Archive shipment" }).click();
  await expect(s.getByText(archiveCode, { exact: true })).toHaveCount(0);
  await expectDraft(s);
  await s.getByLabel("Shipment record status").selectOption("Archived");
  await expect(s.getByText(archiveCode, { exact: true })).toBeVisible();
  await s.getByRole("listitem").filter({ has: s.getByText(archiveCode, { exact: true }) })
    .getByRole("button", { name: "Restore shipment" }).click();
  await s.getByLabel("Shipment record status").selectOption("Active");
  await expect(s.getByText(archiveCode, { exact: true })).toBeVisible();
  await expectDraft(s);
  // A normal save still works and applies exactly the draft.
  await s.getByRole("button", { name: "Save shipment changes" }).click();
  await expect(s.getByRole("button", { name: "Close detail" })).toHaveCount(0);
  const url = "/api/v1/organizations/" + north.id + "/shipments/" + draftRow.id;
  const saved = await (await page.request.get(url)).json();
  expect(saved.displayName).toBe("Local draft name");
  expect(saved.version).toBe(2);
});
