import assert from "node:assert/strict";
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
const url="/api/v1/organizations/"+org.id+"/containers";
for(const engine of ["CHROMIUM","FIREFOX","WEBKIT"]){
  for(const [purpose,code,identifier] of [
    ["BOX","G6-BOX-"+engine,"SYN-BOX-G6"+engine],
    ["CONFLICT","G6-CONFLICT-"+engine,"SYN-BOX-G6CONFLICT"+engine],
    ["ISOLATE","G6-ISOLATE-"+engine,"SYN-BOX-G6ISOLATE"+engine]
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
