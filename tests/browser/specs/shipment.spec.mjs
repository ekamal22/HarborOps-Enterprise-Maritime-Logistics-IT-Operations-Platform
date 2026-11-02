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
