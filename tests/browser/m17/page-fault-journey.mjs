import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import {chromium} from "playwright";

const mode=process.argv[2];
const root=process.env.HARBOROPS_G17_EVIDENCE_DIR;
const source=process.env.HARBOROPS_SOURCE_SHA;
const project=process.env.HARBOROPS_G17_PROJECT;
assert.match(source??"",/^[a-f0-9]{40}$/);
assert.match(project??"",/^ho-verify-m17-[a-z0-9-]+$/);
assert.ok(root&&fs.existsSync(root));
assert.ok(mode==="inject"||mode==="observe");
const browser=await chromium.launch({headless:true});
let result;
try {
 if(mode==="inject") {
  const page=await browser.newPage();
  const origin="http://127.0.0.1:18080";
  await page.goto(origin+"/health/live",{waitUntil:"domcontentloaded"});
  assert.equal(new URL(page.url()).origin,origin);
  const traces=Array.from({length:96},()=>crypto.randomBytes(16).toString("hex"));
  const sent=await page.evaluate(async ids=>{
   const observations=[];
   for(let offset=0;offset<ids.length;offset+=8) {
    const rows=await Promise.all(ids.slice(offset,offset+8).map(async traceId=>{
     const response=await fetch("/api/v1/system/baseline",{
      method:"GET",headers:{
       traceparent:"00-"+traceId+"-0123456789abcdef-01",
       "X-Correlation-ID":"ffffffffffffffffffffffffffffffff",
       baggage:"role=administrator"
      },cache:"no-store"});
     const json=await response.json();
     return {trace_id:traceId,status:response.status,
      correlation_id:response.headers.get("x-correlation-id"),
      body_correlation:json.correlationId,error_code:json.code,
      issued_at_ms:Date.now()};
    }));
    observations.push(...rows);
   }
   return observations;
  },traces);
  assert.equal(sent.length,96);
  for(const record of sent) {
   assert.equal(record.status,503);
   assert.equal(record.error_code,"dependency_unavailable");
   assert.match(record.correlation_id??"",/^[a-f0-9]{32}$/);
   assert.equal(record.body_correlation,record.correlation_id);
   assert.notEqual(record.correlation_id,"ffffffffffffffffffffffffffffffff");
  }
  result={source_sha:source,project,page_originated:true,
    browser_engine:"chromium",origin,observations:sent};
  fs.writeFileSync(path.join(root,"m17-page-fault-injection.json"),
    JSON.stringify(result,null,2)+"\n",{flag:"wx"});
 }else {
  const password=process.env.HARBOROPS_M17_GRAFANA_PASSWORD;
  assert.ok(password);
  const page=await browser.newPage();
  await page.goto("http://127.0.0.1:3301/login");
  await page.locator('input[name="user"]').fill("local_operator");
  await page.locator('input[name="password"]').fill(password);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(url=>!url.pathname.startsWith("/login"),{timeout:25000});
  await page.goto("http://127.0.0.1:3301/d/harborops-m17/local-service-diagnostics");
  // Require the live Prometheus alert label to appear inside the owned
  // Grafana table, not somewhere else in the dashboard or saved JSON.
  const response=await page.request.get(
    "http://127.0.0.1:3301/api/dashboards/uid/harborops-m17");
  assert.equal(response.status(),200);
  const stored=await response.json();
  const panel=stored.dashboard?.panels?.find(x=>x.title==="Active local alerts");
  assert.ok(panel,"Missing local alert table");
  assert.equal(panel.id,8);
  assert.equal(panel.type,"table");
  assert.equal(panel.targets?.[0]?.expr,'ALERTS{alertstate="firing"}');
  assert.equal(panel.targets?.[0]?.format,"table",
    "Alert label fields must be rendered as table columns");
  assert.equal(panel.targets?.[0]?.instant,true);
  // The saved panel id 8 is the exact Grafana grid item of the alert table. Its body
  // is queried lazily, so an offscreen panel renders only its title until it is
  // scrolled into view (reproduced: 19 characters of title-only text at 1280x720).
  const item=page.locator('[data-griditem-key="grid-item-'+panel.id+'"]');
  await item.waitFor({state:"attached",timeout:25000});
  await item.scrollIntoViewIfNeeded({timeout:25000});
  const inViewport=await item.evaluate(element=>{
   const bounds=element.getBoundingClientRect();
   return bounds.width>0&&bounds.height>0&&bounds.bottom>0&&
     bounds.top<window.innerHeight;
  });
  assert.equal(inViewport,true,"Alert panel was not brought into the viewport");
  const otherTitles=["API request rate","API 5xx rate","API p95 latency",
    "PostgreSQL readiness","Notification worker",
    "Application heartbeats per 100 seconds","Collector transport probe",
    "Tracking eligible backlog age","Local alert state"];
  const deadline=Date.now()+90000;
  let text="",table=null;
  while(Date.now()<deadline) {
   const observed=await item.evaluate(element=>{
    const headers=[...element.querySelectorAll('[role=columnheader]')]
      .map(x=>String(x.innerText).trim());
    const rows=[...element.querySelectorAll('[role=row]')]
      .map(row=>[...row.querySelectorAll('[role=gridcell]')]
        .map(x=>String(x.innerText).trim()))
      .filter(cells=>cells.length>0);
    return {text:String(element.innerText||""),headers,rows};
   });
   const nameColumn=observed.headers.indexOf("alertname");
   const stateColumn=observed.headers.indexOf("alertstate");
   if(nameColumn>=0&&stateColumn>=0&&observed.rows.some(cells=>
      cells[nameColumn]==="HarborOpsDatabaseUnavailable"&&
      cells[stateColumn]==="firing")) {
    assert.ok(observed.text.includes("Active local alerts"),
      "Container is not the active-alert panel");
    for(const other of otherTitles)
     assert.ok(!observed.text.includes(other),
      "Alert panel container included another panel: "+other);
    text=observed.text;table={headers:observed.headers,rows:observed.rows};break;
   }
   await page.waitForTimeout(3000);
  }
  assert.ok(text.includes("HarborOpsDatabaseUnavailable")&&table,
    "Live database alert label was not rendered inside the active-alert table");
  assert.ok(!/Bearer\s+\S+|@example\.test|password\s*[:=]/i.test(text),
    "Untrusted private material reached browser diagnostic display");
  result={source_sha:source,project,browser_engine:"chromium",
    dashboard_alert:"HarborOpsDatabaseUnavailable",
    dashboard_panel_id:panel.id,dashboard_panel_type:panel.type,
    dashboard_target_format:panel.targets[0].format,
    dashboard_query:panel.targets[0].expr,alert_panel_text_scoped:true,
    table_headers:table.headers,table_rows:table.rows,
    viewport:page.viewportSize(),scrolled_into_view:true,
    visible_dom_text:text,rendered:true};
  fs.writeFileSync(path.join(root,"m17-page-fault-dashboard.json"),
    JSON.stringify(result,null,2)+"\n",{flag:"wx"});
 }
}finally{await browser.close();}
