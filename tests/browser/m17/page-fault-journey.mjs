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
  const deadline=Date.now()+90000;
  let text="";
  while(Date.now()<deadline) {
   const body=await page.locator("body").innerText();
   if(body.includes("Active local alerts")&&
      body.includes("HarborOpsDatabaseUnavailable")) {
      text=body.slice(0,7000);break;
   }
   await page.waitForTimeout(3000);
  }
  assert.ok(text.includes("HarborOpsDatabaseUnavailable"),
    "Live local alert name must be rendered in the real browser dashboard");
  assert.ok(!/Bearer\s+\S+|@example\.test|password\s*[:=]/i.test(text),
    "Untrusted private material reached browser diagnostic display");
  result={source_sha:source,project,browser_engine:"chromium",
    dashboard_alert:"HarborOpsDatabaseUnavailable",
    visible_dom_text:text,rendered:true};
  fs.writeFileSync(path.join(root,"m17-page-fault-dashboard.json"),
    JSON.stringify(result,null,2)+"\n",{flag:"wx"});
 }
}finally{await browser.close();}
