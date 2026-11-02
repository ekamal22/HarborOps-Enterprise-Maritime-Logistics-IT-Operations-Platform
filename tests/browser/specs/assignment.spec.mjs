import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";

const enabled = process.env.HARBOROPS_M6_FIXTURES === "1" &&
  Boolean(process.env.HARBOROPS_TEST_USER_PASSWORD);

// The select value changes before the server-side organization selection completes. Wait for the
// PUT so a following direct API call is made in the selected organization.
async function selectOrganization(page, label) {
  const saved = page.waitForResponse(r => r.request().method() === "PUT" &&
    new URL(r.url()).pathname === "/api/v1/auth/session/organization");
  await page.locator("#current-organization").selectOption({ label });
  expect((await saved).ok()).toBeTruthy();
  // The controlled select re-renders after the PUT resolves; WebKit can still report "" until then.
  await expect(page.locator("#current-organization")).not.toHaveValue("");
}

async function login(page, role = "analyst") {
  await page.goto("/");
  await page.getByLabel("Email").fill("m3-" + role + "@example.test");
  await page.getByLabel("Password", { exact: true }).fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Organization workspace" })).toBeVisible();
  await selectOrganization(page,
    "M3 North Terminal (" + (role === "analyst" ? "Analyst" : "Manager") + ")");
}
const section = page => page.getByRole("heading", { name: "Shipments", exact: true })
  .locator("xpath=ancestor::section[1]");
const assignment = (page, type) => page.getByRole("heading", { name: type + " assignments" })
  .locator("xpath=ancestor::div[contains(@class,'rounded')][1]");

test("G6 Analyst attaches container and voyage and retains the shipment draft", async ({ page }, testInfo) => {
  test.skip(!enabled, "Disposable M6 fixture required");
  test.setTimeout(120_000);
  await login(page);
  const org = await page.locator("#current-organization").inputValue();
  const root = "/api/v1/organizations/" + org;
  const voyageResponse = await page.request.get(root + "/voyages?recordStatus=Active&limit=100");
  const voyageData = await voyageResponse.json();
  const voyage = (voyageData.voyages ?? []).find(x =>
    (x.voyageStatus === "Planned" || x.voyageStatus === "Underway") &&
    x.originPortId && x.destinationPortId);
  const containerResponse = await page.request.get(root + "/containers?recordStatus=Active&limit=100");
  const containerData = await containerResponse.json();
  const container = (containerData.containers ?? []).find(x =>
    x.referenceCode === "G6-BOX-" + testInfo.project.name.toUpperCase());
  expect(voyage, "M6 fixture needs route-compatible voyage").toBeTruthy();
  expect(container, "M6 fixture needs active container").toBeTruthy();

  const s = section(page);
  await s.getByRole("button", { name: "Create shipment" }).click();
  const code = "G6-" + randomUUID().replaceAll("-", "").slice(0, 10).toUpperCase();
  await s.getByLabel("Shipment reference code").fill(code);
  await s.getByLabel("Origin port").selectOption(voyage.originPortId);
  await s.getByLabel("Destination port").selectOption(voyage.destinationPortId);
  await s.getByRole("button", { name: "Save new shipment" }).click();
  await expect(s.getByRole("button", { name: "Close detail" })).toHaveCount(0);
  await s.getByLabel("Search shipments").fill(code);
  await expect(s.getByText(code, { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await expect(s.getByRole("heading", { name: "Shipment allocations" })).toBeVisible();

  const boxes = assignment(page, "Container");
  await boxes.getByLabel("Assign container").selectOption(container.id);
  await boxes.getByLabel("Start Container assignment UTC").fill("2035-01-01T10:00:00Z");
  await boxes.getByRole("button", { name: "Attach container" }).click();
  await expect(boxes.getByText(/From 2035-01-01T10:00:00/)).toBeVisible();

  const voyages = assignment(page, "Voyage");
  await voyages.getByLabel("Assign voyage").selectOption(voyage.id);
  await voyages.getByLabel("Start Voyage assignment UTC").fill("2035-01-01T10:00:00Z");
  await voyages.getByRole("button", { name: "Attach voyage" }).click();
  await expect(voyages.getByText(/From 2035-01-01T10:00:00/)).toBeVisible();

  await s.getByLabel("Shipment display name").fill("Unsaved G6 edit");
  await boxes.getByRole("button", { name: "Refresh assignments" }).click();
  await expect(s.getByLabel("Shipment display name")).toHaveValue("Unsaved G6 edit");

  await boxes.getByRole("button", { name: "Close assignment" }).click();
  await boxes.getByLabel("End Container assignment UTC", { exact: true }).fill("2035-01-01T11:00:00Z");
  await boxes.getByRole("button", { name: "Confirm closure" }).click();
  await expect(boxes.getByText(/to 2035-01-01T11:00:00/)).toBeVisible();

  await voyages.getByRole("button", { name: "Close assignment" }).click();
  await voyages.getByLabel("End Voyage assignment UTC", { exact: true }).fill("2035-01-01T11:00:00Z");
  await voyages.getByRole("button", { name: "Confirm closure" }).click();
  await expect(voyages.getByText(/to 2035-01-01T11:00:00/)).toBeVisible();

  // Half-open adjacency permits a new durable allocation immediately after closure.
  await boxes.getByLabel("Assign container").selectOption(container.id);
  await boxes.getByLabel("Start Container assignment UTC").fill("2035-01-01T11:00:00Z");
  await boxes.getByLabel("Optional end Container assignment UTC").fill("2035-01-01T12:00:00Z");
  await boxes.getByRole("button",{name:"Attach container"}).click();
  await expect(boxes.getByText(/From 2035-01-01T11:00:00/)).toBeVisible();
  await voyages.getByLabel("Assign voyage").selectOption(voyage.id);
  await voyages.getByLabel("Start Voyage assignment UTC").fill("2035-01-01T11:00:00Z");
  await voyages.getByLabel("Optional end Voyage assignment UTC").fill("2035-01-01T12:00:00Z");
  await voyages.getByRole("button",{name:"Attach voyage"}).click();
  await expect(voyages.getByText(/From 2035-01-01T11:00:00/)).toBeVisible();
  await expect(s.getByLabel("Shipment display name")).toHaveValue("Unsaved G6 edit");
});

test("G6 organization switch invalidates shipment allocation panels", async ({ page }) => {
  test.skip(!enabled, "Disposable M6 fixture required");
  await login(page, "manager");
  const selector = page.locator("#current-organization");
  const s = section(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH", { exact: true })).toBeVisible();
  await s.getByRole("button", { name: "View shipment" }).first().click();
  await expect(s.getByRole("heading", { name: "Shipment allocations" })).toBeVisible();
  await selector.selectOption({ label: "M3 East Terminal (Manager)" });
  await expect(section(page).getByRole("heading", { name: "Shipment allocations" })).toHaveCount(0);
  await expect(section(page).getByText("M5-DEMO-NORTH", { exact: true })).toHaveCount(0);
});


test("G6 Auditor reads scoped assignment histories without catalog or mutation access", async ({ page }) => {
  test.skip(!enabled,"Disposable M6 fixture required");
  await page.goto("/");
  await page.getByLabel("Email").fill("m4-auditor@example.test");
  await page.getByLabel("Password",{exact:true}).fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button",{name:"Sign in"}).click();
  await selectOrganization(page,"M3 North Terminal (Auditor)");
  const s=section(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await expect(s.getByText("M5-DEMO-NORTH",{exact:true})).toBeVisible();
  await s.getByRole("button",{name:"View shipment"}).first().click();
  await expect(s.getByRole("heading",{name:"Shipment allocations"})).toBeVisible();
  await expect(s.getByRole("button",{name:"Attach container"})).toHaveCount(0);
  await expect(s.getByRole("button",{name:"Attach voyage"})).toHaveCount(0);
  await expect(s.getByRole("button",{name:"Close assignment"})).toHaveCount(0);
  const org=await page.locator("#current-organization").inputValue();
  const denied=await page.request.get("/api/v1/organizations/"+org+"/containers?limit=25");
  expect(denied.status()).toBe(403);
  const shipments=await (await page.request.get(
    "/api/v1/organizations/"+org+"/shipments?q=M5-DEMO-NORTH")).json();
  const shipmentId=shipments.shipments[0].id;
  for(const kind of ["container-assignments","voyage-assignments"]){
    const assignmentList=await page.request.get(
      "/api/v1/organizations/"+org+"/shipments/"+shipmentId+"/"+kind);
    expect(assignmentList.status(),kind).toBe(200);
  }
});

test("G6 Support cannot open typed shipment assignment history", async ({ page }) => {
  test.skip(!enabled,"Disposable M6 fixture required");
  await page.goto("/");
  await page.getByLabel("Email").fill("m4-support@example.test");
  await page.getByLabel("Password",{exact:true}).fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
  await page.getByRole("button",{name:"Sign in"}).click();
  await selectOrganization(page,"M3 North Terminal (Support)");
  const org=await page.locator("#current-organization").inputValue();
  const response=await page.request.get("/api/v1/organizations/"+org+
    "/shipments/00000000-0000-0000-0000-000000000001/container-assignments");
  expect(response.status()).toBe(403);
  await expect(page.getByRole("heading",{name:"Shipment allocations"})).toHaveCount(0);
});

test("G6 tab return refresh preserves allocation input and shipment draft", async ({ page }) => {
  test.skip(!enabled,"Disposable M6 fixture required");
  await login(page,"manager");
  const s=section(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await s.getByRole("button",{name:"View shipment"}).first().click();
  const boxes=assignment(page,"Container");
  await expect(boxes.getByRole("button",{name:"Refresh assignments"})).toBeVisible();
  await boxes.getByLabel("Start Container assignment UTC").fill("2036-02-01T10:00:00Z");
  await s.getByLabel("Shipment display name").fill("Unsaved allocation draft");
  let count=0;
  await page.route("**/api/v1/organizations/*/shipments/*/container-assignments*",async route=>{
    if(route.request().method()==="GET")count++;
    await route.continue();
  });
  await page.evaluate(()=>{
    Object.defineProperty(document,"visibilityState",{configurable:true,value:"visible"});
    document.dispatchEvent(new Event("visibilitychange"));
  });
  await expect.poll(()=>count).toBeGreaterThan(0);
  await expect(boxes.getByLabel("Start Container assignment UTC"))
    .toHaveValue("2036-02-01T10:00:00Z");
  await expect(s.getByLabel("Shipment display name")).toHaveValue("Unsaved allocation draft");
});

test("G6 logout disposes allocation views and context projections", async ({ page }) => {
  test.skip(!enabled,"Disposable M6 fixture required");
  await login(page,"manager");
  const s=section(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await s.getByRole("button",{name:"View shipment"}).first().click();
  await expect(s.getByRole("heading",{name:"Contextual container and voyage intervals"}))
    .toBeVisible();
  await page.getByRole("button",{name:"Log out",exact:true}).click();
  await expect(page.getByLabel("Email")).toBeVisible();
  await expect(page.getByRole("heading",{name:"Shipment allocations"})).toHaveCount(0);
  await expect(page.getByText("M5-DEMO-NORTH",{exact:true})).toHaveCount(0);
});


test("G6 conflicting container allocation shows an actionable error and preserves inputs",
  async ({ page }, testInfo) => {
  test.skip(!enabled,"Disposable M6 fixture required");
  await login(page,"manager");
  const s=section(page);
  const code="G6-CONF-"+randomUUID().slice(0,8).toUpperCase();
  await s.getByRole("button",{name:"Create shipment"}).click();
  await s.getByLabel("Shipment reference code").fill(code);
  await s.getByLabel("Origin port").selectOption({index:1});
  await s.getByLabel("Destination port").selectOption({index:2});
  await s.getByRole("button",{name:"Save new shipment"}).click();
  await s.getByLabel("Search shipments").fill(code);
  await expect(s.getByText(code,{exact:true})).toBeVisible();
  await s.getByRole("button",{name:"View shipment"}).first().click();
  const boxes=assignment(page,"Container");
  const org=await page.locator("#current-organization").inputValue();
  const list=await page.request.get("/api/v1/organizations/"+org+
    "/containers?recordStatus=Active&limit=100");
  const data=await list.json();
  const target=data.containers.find(x=>x.referenceCode===
    "G6-CONFLICT-"+testInfo.project.name.toUpperCase());
  expect(target).toBeTruthy();
  await boxes.getByLabel("Assign container").selectOption(target.id);
  await boxes.getByLabel("Start Container assignment UTC").fill("2035-01-01T12:00:00Z");
  await boxes.getByRole("button",{name:"Attach container"}).click();
  await expect(boxes.getByText(/From 2035-01-01T12:00:00/)).toBeVisible();
  await boxes.getByLabel("Assign container").selectOption(target.id);
  await boxes.getByLabel("Start Container assignment UTC").fill("2035-01-01T12:30:00Z");
  await boxes.getByRole("button",{name:"Attach container"}).click();
  await expect(boxes.getByRole("alert")).toContainText("record changed");
  await expect(boxes.getByLabel("Start Container assignment UTC"))
    .toHaveValue("2035-01-01T12:30:00Z");
});

test("G6 deferred allocation response cannot cross organization switch",async ({page})=>{
  test.skip(!enabled,"Disposable M6 fixture required");
  await login(page,"manager");
  const selector=page.locator("#current-organization");
  const org=await selector.inputValue();
  const s=section(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await s.getByRole("button",{name:"View shipment"}).first().click();
  const boxes=assignment(page,"Container");
  await expect(boxes.getByRole("button",{name:"Refresh assignments"})).toBeVisible();
  let released;
  const held=new Promise(resolve=>{released=resolve;});
  let entered;
  const seen=new Promise(resolve=>{entered=resolve;});
  let once=false;
  await page.route("**/api/v1/organizations/*/shipments/*/container-assignments*",async route=>{
    if(!once && route.request().method()==="GET" &&
      route.request().url().includes("/"+org+"/")){
      once=true;entered();
      await held;
    }
    await route.continue();
  });
  try{
    await boxes.getByRole("button",{name:"Refresh assignments"}).click();
    await Promise.race([seen,new Promise((_,reject)=>
      setTimeout(()=>reject(Error("Delayed allocation read not observed")),8000))]);
    await selector.selectOption({label:"M3 East Terminal (Manager)"});
  }finally{released?.();}
  const east=section(page);
  await east.getByLabel("Search shipments").fill("M5-DEMO-EAST");
  await expect(east.getByText("M5-DEMO-EAST",{exact:true})).toBeVisible();
  await expect(east.getByText("M5-DEMO-NORTH",{exact:true})).toHaveCount(0);
});


test("G6 live membership downgrade invalidates the open allocation workspace",
  async ({page,browser}) => {
  test.skip(!enabled,"Disposable M6 fixture required");
  await login(page,"manager");
  const org=await page.locator("#current-organization").inputValue();
  const session=await (await page.request.get("/api/v1/auth/session")).json();
  const managerId=session.user?.id;
  expect(managerId).toBeTruthy();
  const s=section(page);
  await s.getByLabel("Search shipments").fill("M5-DEMO-NORTH");
  await s.getByRole("button",{name:"View shipment"}).first().click();
  await expect(s.getByRole("heading",{name:"Shipment allocations"})).toBeVisible();
  const adminContext=await browser.newContext();
  try{
    const adminPage=await adminContext.newPage();
    await adminPage.goto("/");
    await adminPage.getByLabel("Email").fill("m3-admin-a@example.test");
    await adminPage.getByLabel("Password",{exact:true})
      .fill(process.env.HARBOROPS_TEST_USER_PASSWORD);
    await adminPage.getByRole("button",{name:"Sign in"}).click();
    await selectOrganization(adminPage,"M3 North Terminal (Administrator)");
    const base="/api/v1/organizations/"+org+"/memberships/";
    const listing=await (await adminPage.request.get(
      "/api/v1/organizations/"+org+"/memberships?limit=100")).json();
    const member=listing.memberships.find(x=>x.userId===managerId);
    expect(member).toBeTruthy();
    async function setRole(role,version){
      const csrf=(await (await adminPage.request.get("/api/v1/auth/csrf")).json()).token;
      const result=await adminPage.request.put(base+managerId,{
        headers:{Origin:"http://localhost:5173","X-HarborOps-CSRF":csrf},
        data:{role,status:"Active",expectedVersion:version}
      });
      expect(result.status()).toBe(200);
      return result.json();
    }
    let changed=false;
    try{
      const update=await setRole("Support",member.version);
      changed=true;
      await s.getByRole("button",{name:"Refresh assignments"}).first().click();
      await expect(s.getByRole("heading",{name:"Shipment allocations"})).toHaveCount(0);
    }finally{
      if(changed){
        const listingAgain=await (await adminPage.request.get(
          "/api/v1/organizations/"+org+"/memberships?limit=100")).json();
        const current=listingAgain.memberships.find(x=>x.userId===managerId);
        if(current?.role==="Support")await setRole("Manager",current.version);
      }
    }
  }finally{await adminContext.close();}
});


test("G6 shipment switch discards prior contextual links and late responses",
  async ({page}, testInfo) => {
  test.skip(!enabled,"Disposable M6 fixture required");
  await login(page,"manager");
  const org=await page.locator("#current-organization").inputValue();
  const root="/api/v1/organizations/"+org;
  const voyageList=await (await page.request.get(root+
    "/voyages?recordStatus=Active&limit=100")).json();
  const voyage=voyageList.voyages.find(v=>v.voyageStatus==="Planned"&&
    v.originPortId&&v.destinationPortId);
  expect(voyage).toBeTruthy();
  const containerList=await (await page.request.get(root+
    "/containers?recordStatus=Active&limit=100")).json();
  const isolated=containerList.containers.find(c=>c.referenceCode===
    "G6-ISOLATE-"+testInfo.project.name.toUpperCase());
  expect(isolated).toBeTruthy();
  const s=section(page);
  async function createShipment(code) {
    await s.getByRole("button",{name:"Create shipment"}).click();
    await s.getByLabel("Shipment reference code").fill(code);
    await s.getByLabel("Origin port").selectOption(voyage.originPortId);
    await s.getByLabel("Destination port").selectOption(voyage.destinationPortId);
    await s.getByRole("button",{name:"Save new shipment"}).click();
    await s.getByLabel("Search shipments").fill(code);
    await expect(s.getByText(code,{exact:true})).toBeVisible();
  }
  const suffix=randomUUID().slice(0,8).toUpperCase();
  const first="G6-CTX-A-"+suffix, second="G6-CTX-B-"+suffix;
  await createShipment(first);
  await createShipment(second);
  await s.getByLabel("Search shipments").fill(first);
  await expect(s.getByText(first,{exact:true})).toBeVisible();
  await s.getByRole("button",{name:"View shipment"}).first().click();
  const container=assignment(page,"Container");
  await container.getByLabel("Assign container").selectOption(isolated.id);
  await container.getByLabel("Start Container assignment UTC").fill("2040-01-01T10:00:00Z");
  await container.getByRole("button",{name:"Attach container"}).click();
  await expect(container.getByText(/From 2040-01-01T10:00:00/)).toBeVisible();
  const voyagePanel=assignment(page,"Voyage");
  await voyagePanel.getByLabel("Assign voyage").selectOption(voyage.id);
  await voyagePanel.getByLabel("Start Voyage assignment UTC").fill("2040-01-01T10:00:00Z");
  await voyagePanel.getByRole("button",{name:"Attach voyage"}).click();
  const contextual=page.getByRole("heading",{name:"Contextual container and voyage intervals"})
    .locator("xpath=ancestor::div[contains(@class,'rounded')][1]");
  await expect(contextual.getByText(/G6-ISOLATE-/)).toBeVisible();

  let release;
  const gate=new Promise(resolve=>{release=resolve;});
  let seen;
  const entered=new Promise(resolve=>{seen=resolve;});
  let held=false;
  await page.route("**/container-assignments*",async route=>{
    if(!held&&route.request().method()==="GET"){
      held=true;seen();await gate;
    }
    await route.continue();
  });
  try {
    await container.getByRole("button",{name:"Refresh assignments"}).click();
    await Promise.race([entered,new Promise((_,reject)=>
      setTimeout(()=>reject(Error("Held source shipment request was not observed")),8000))]);
    await s.getByLabel("Search shipments").fill(second);
    await expect(s.getByText(second,{exact:true})).toBeVisible();
    await s.getByRole("button",{name:"View shipment"}).first().click();
    await expect(contextual.getByText(/G6-ISOLATE-/)).toHaveCount(0);
  }finally{release?.();}
  await expect(contextual.getByText(/G6-ISOLATE-/)).toHaveCount(0);
  await expect(contextual.getByText("No overlapping allocation intervals.")).toBeVisible();
});
