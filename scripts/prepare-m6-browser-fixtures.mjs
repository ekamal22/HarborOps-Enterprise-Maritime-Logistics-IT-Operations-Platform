import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
const base="http://127.0.0.1:5173";
const password=process.env.HARBOROPS_TEST_USER_PASSWORD;
assert.ok(password && password.length>=20,"G6 fixture password missing");
const cookies=new Map();
async function request(method,path,data){
  const headers={Origin:base};
  if(cookies.size)headers.Cookie=[...cookies].map(([k,v])=>k+"="+v).join("; ");
  if(method!=="GET"){
    const csrf=await request("GET","/api/v1/auth/csrf");
    headers["X-HarborOps-CSRF"]=csrf.token;
    headers.Cookie=[...cookies].map(([k,v])=>k+"="+v).join("; ");
  }
  if(data!==undefined)headers["Content-Type"]="application/json";
  const response=await fetch(base+path,{method,headers,
    ...(data===undefined?{}:{body:JSON.stringify(data)})});
  for(const value of response.headers.getSetCookie()){
    const [part]=value.split(";");
    const index=part.indexOf("=");
    if(index>0)cookies.set(part.slice(0,index),part.slice(index+1));
  }
  const body=await response.json().catch(()=>null);
  assert.ok(response.ok,method+" "+path+" "+response.status+" "+JSON.stringify(body).slice(0,250));
  return body;
}
await request("POST","/api/v1/auth/login",{email:"m3-manager@example.test",password});
const orgs=(await request("GET","/api/v1/me/organizations")).organizations;
const org=orgs.find(x=>x.slug==="m3-north"&&x.role==="Manager");
assert.ok(org,"M6 North fixture missing");
await request("PUT","/api/v1/auth/session/organization",{organizationId:org.id});

const root=process.env.HARBOROPS_VERIFICATION_ROOT;
const sha=process.env.HARBOROPS_SOURCE_SHA;
assert.ok(root && /^(?:[Ee]:[\\/]|\/e\/)/.test(root),"G6 fixture evidence must reside on E:");
assert.match(sha||"",/^[0-9a-f]{40}$/);
const g5=JSON.parse(fs.readFileSync(path.join(root,"evidence","g5-fixtures.json"),"utf8"));
assert.equal(g5.sourceSha,sha);
assert.equal(g5.organizations.length,2,"G6 requires two fixture tenants");
const roles=["Administrator","Manager","Analyst","Auditor","Support"];
assert.deepEqual(Object.values(g5.organizations.find(x=>x.slug==="m3-north").verifiedRoles).sort(),
  [...roles].sort(),"G6 requires five verified role memberships");

async function makeFixture(slug) {
  const tenant=orgs.find(x=>x.slug===slug&&x.role==="Manager");
  assert.ok(tenant,"Manager is not a member of "+slug);
  await request("PUT","/api/v1/auth/session/organization",{organizationId:tenant.id});
  const api="/api/v1/organizations/"+tenant.id;
  const voyages=(await request("GET",api+"/voyages?recordStatus=Active&limit=100")).voyages;
  const voyage=voyages.find(x=>x.voyageStatus==="Planned"&&
    x.originPortId && x.destinationPortId);
  assert.ok(voyage,"G6 requires a route-compatible planned voyage in "+slug);
  const code=slug==="m3-north"?"G6-FIX-NORTH":"G6-FIX-EAST";
  const read=async(url,key)=>(await request("GET",url+"?recordStatus=All&limit=100"))[key];
  const containers=[];
  for(let i=0;i<2;i++){
    const boxCode=code+"-BOX-"+(i+1);
    const url=api+"/containers";
    let box=(await read(url,"containers")).find(x=>x.referenceCode===boxCode);
    if(!box)box=await request("POST",url,{
      referenceCode:boxCode,identifierKind:"Synthetic",
      containerIdentifier:"SYN-BOX-"+(slug==="m3-north"?"G6N":"G6E")+(i+1),
      equipmentType:"Dry20"
    });
    assert.equal(box.recordStatus,"Active");
    containers.push(box);
  }
  const shipments=api+"/shipments";
  let shipment=(await read(shipments,"shipments")).find(x=>x.referenceCode===code);
  if(!shipment)shipment=await request("POST",shipments,{
    referenceCode:code,originPortId:voyage.originPortId,
    destinationPortId:voyage.destinationPortId
  });
  assert.equal(shipment.recordStatus,"Active");
  const expected=[
    ["container-assignments",containers[0].id,"containerId",
      "2023-01-01T10:00:00Z","2023-01-01T12:00:00Z"],
    ["container-assignments",containers[1].id,"containerId",
      "2045-01-01T10:00:00Z",null],
    ["voyage-assignments",voyage.id,"voyageId",
      "2045-01-01T10:00:00Z",null]
  ];
  const links=[];
  for(const [kind,id,field,start,end] of expected){
    const url=shipments+"/"+shipment.id+"/"+kind;
    let listing=(await request("GET",url)).assignments;
    let link=listing.find(x=>x[field]===id&&Date.parse(x.startsAtUtc)===Date.parse(start));
    if(!link){
      link=await request("POST",url,{[field]:id,startsAtUtc:start,endsAtUtc:end});
    }
    assert.equal(link.organizationId,tenant.id);
    assert.equal(link.shipmentId,shipment.id);
    assert.equal(link[field],id);
    assert.equal(Date.parse(link.startsAtUtc),Date.parse(start));
    assert.equal(link.endsAtUtc===null?null:Date.parse(link.endsAtUtc),
      end===null?null:Date.parse(end));
    links.push({kind,id:link.id,targetId:id,start,end,version:link.version});
  }
  assert.equal(new Set(links.map(x=>x.id)).size,3);
  return {slug,organizationId:tenant.id,shipmentId:shipment.id,
    route:{originPortId:voyage.originPortId,destinationPortId:voyage.destinationPortId},
    containerIds:containers.map(x=>x.id),voyageId:voyage.id,links};
}
const snapshot=[];
for(const slug of ["m3-north","m3-east"]){
  const first=await makeFixture(slug);
  const replay=await makeFixture(slug);
  assert.deepEqual(replay,first,"M6 fixture replay changed identity or version");
  snapshot.push(first);
}
assert.notEqual(snapshot[0].shipmentId,snapshot[1].shipmentId);
assert.notEqual(snapshot[0].containerIds[0],snapshot[1].containerIds[0]);
assert.deepEqual(snapshot[0].links.map(x=>x.end===null),[false,true,true]);
const output={sourceSha:sha,verifiedAtUtc:new Date().toISOString(),
  expectedRoles:roles,tenantCount:2,organizations:snapshot,
  replayVerified:true,expectedLinkKinds:["bounded-container","open-container","open-voyage"]};
fs.writeFileSync(path.join(root,"evidence","g6-fixtures.json"),JSON.stringify(output,null,2));

await request("PUT","/api/v1/auth/session/organization",{organizationId:org.id});
const url="/api/v1/organizations/"+org.id+"/containers";
for(const engine of ["CHROMIUM","FIREFOX","WEBKIT"]){
  // The synthetic identifier suffix is limited to 16 characters, so use a short engine tag.
  const tag={CHROMIUM:"CHR",FIREFOX:"FFX",WEBKIT:"WBK"}[engine];
  for(const [purpose,code,identifier] of [
    ["BOX","G6-BOX-"+engine,"SYN-BOX-G6BOX"+tag],
    ["CONFLICT","G6-CONFLICT-"+engine,"SYN-BOX-G6CONFLICT"+tag],
    ["ISOLATE","G6-ISOLATE-"+engine,"SYN-BOX-G6ISOLATE"+tag]
  ]){
    const rowsBefore=(await request("GET",url+"?recordStatus=All&limit=100")).containers;
    if(!rowsBefore.some(row=>row.referenceCode===code)){
      await request("POST",url,{
        referenceCode:code,identifierKind:"Synthetic",containerIdentifier:identifier,
        equipmentType:"Dry20",displayName:"G6 "+purpose+" "+engine
      });
    }
    const matches=(await request("GET",url+"?recordStatus=All&limit=100")).containers
      .filter(row=>row.referenceCode===code);
    assert.equal(matches.length,1,"Fixture must remain idempotent: "+code);
    assert.equal(matches[0].recordStatus,"Active");
  }
}
console.log("G6 per-engine isolated allocation, conflict and switch fixtures: PASS");
