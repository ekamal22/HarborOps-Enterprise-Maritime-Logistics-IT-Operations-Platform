import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const dir=process.argv[2];
assert.ok(dir && fs.existsSync(dir),"G6 evidence directory required");
const at=p=>path.join(dir,p),read=p=>fs.readFileSync(at(p),"utf8");
const sha=read("g6-source-sha.txt").trim();
assert.match(sha,/^[0-9a-f]{40}$/);
assert.equal(read("m6-source-sha.txt").trim(),sha);
const rows=read("g6-stages.tsv").trim().split(/\r?\n/).map(l=>l.split("\t"));
const mandatory=[
  "storage-before","decision-unit","schema-index","shipment-schema","assignment-schema",
  "coverage-source","preflight","postgres-image","deps","web","docs","stack",
  "accounts","organizations","session","setup-fixtures","fixtures",
  "m4-fixtures","m5-fixtures","browsers","shipment-outage","reference-outage",
  "maritime-outage","recovery","scan","dotnet","snapshot","teardown","storage-after"
];
for(const name of mandatory){
 const matches=rows.filter(x=>x[0]===name);
 assert.equal(matches.length,1,"Missing/duplicate G6 stage "+name);
 assert.equal(matches[0][1],sha,"Mismatched stage SHA "+name);
 assert.equal(matches[0][2],"0","G6 stage failure "+name);
 assert.ok(fs.existsSync(at("g6-"+name+".log")),"Missing stage log "+name);
}
const teardown=JSON.parse(read("g6-teardown.json"));
for(const field of ["remainingContainers","remainingVolumes","remainingIntegrationContainers"])
 assert.equal(teardown[field],0,"Owned G6 Docker resources remain: "+field);
for(const phase of ["before","after"])
 assert.equal(JSON.parse(read("g6-storage-"+phase+".json")).sourceSha,sha);
const trxDir=at("trx");
const phases=["unit","contract","integration-fresh","integration-repeat"];
const testNames={};
for(const phase of phases){
 const file=path.join(trxDir,phase+".trx");
 assert.ok(fs.existsSync(file),"Missing TRX "+phase);
 const xml=fs.readFileSync(file,"utf8");
 const results=[...xml.matchAll(/<UnitTestResult\b([^>]*?)\/?>/g)].map(m=>({
   name:/testName="([^"]+)"/.exec(m[1])?.[1],
   outcome:/outcome="([^"]+)"/.exec(m[1])?.[1]
 }));
 assert.ok(results.length>0,"Empty TRX "+phase);
 assert.ok(results.every(x=>x.name&&x.outcome==="Passed"),
   "Failed, skipped or malformed TRX "+phase);
 testNames[phase]=new Set(results.map(x=>x.name));
}
assert.deepEqual([...testNames["integration-fresh"]].sort(),
  [...testNames["integration-repeat"]].sort(),"Fresh/repeat method sets differ");

const browser={};
for(const engine of ["chromium","firefox","webkit"]){
 const dirname=at("playwright");
 const files=fs.readdirSync(dirname).filter(f=>f.startsWith(engine+"-")&&f.endsWith(".json"));
 assert.ok(files.some(f=>f===engine+"-assignment.json"),"Missing G6 assignment browser JSON: "+engine);
 browser[engine]=new Set();
 function walk(suite){
   for(const spec of suite.specs||[]){
     for(const test of spec.tests||[]){
       if(test.results.some(x=>x.status!=="passed"))throw Error("Browser failure/skip: "+engine+"/"+spec.title);
       browser[engine].add(spec.title);
     }
   }
   for(const child of suite.suites||[])walk(child);
 }
 for(const f of files){
   const json=JSON.parse(fs.readFileSync(path.join(dirname,f),"utf8"));
   for(const suite of json.suites||[])walk(suite);
 }
}
const hasUnit=n=>[...testNames.unit].some(x=>x.includes(n));
const hasIntegration=n=>[...testNames["integration-fresh"]].some(x=>x.includes(n));
const hasBrowser=n=>["chromium","firefox","webkit"].every(engine=>[...browser[engine]].some(x=>x.includes(n)));
const decision=fs.readFileSync("docs/M6_LOCAL_CONTRACT_DECISION.md","utf8");
const accepted=/\*\*State:\*\*\s*Accepted/i.test(decision);
const snapshot=fs.existsSync(at("g6-snapshot.json"))?
  JSON.parse(read("g6-snapshot.json")):null;
const fullSnap=Boolean(snapshot&&
  (snapshot.outcome==="NOT APPLICABLE"&&snapshot.reason&&snapshot.discoveryPaths||
   snapshot.outcome==="PASS"&&snapshot.evidence));
const cases=[
  accepted,
  hasUnit("HalfOpenWindowsAllowAdjacencyButNotOverlap"),
  hasIntegration("ExclusionConstraintsAndDirectSqlGuards"),
  hasIntegration("ExclusionConstraintsAndDirectSqlGuards"),
  hasIntegration("RealForeignTenantParentFailsCompositeFk"),
  hasUnit("RouteAndKnownScheduleMustMatch"),
  hasIntegration("ExclusionConstraintsAndDirectSqlGuards"),
  hasIntegration("ExistingM5RowsSurviveM6UpgradeRollbackAndReapply"),
  hasIntegration("ExistingM5RowsSurviveM6UpgradeRollbackAndReapply"),
  fullSnap,
  hasIntegration("ContainerAndVoyageEndToEnd"),
  hasUnit("AssignmentPermissionsAreNarrow")&&hasIntegration("MalformedAndCrossOriginWrites"),
  false, // Requires live session/role revocation coverage beyond a static role matrix.
  hasIntegration("RealForeignTenantParentFailsCompositeFk"),
  hasIntegration("MalformedAndCrossOriginWritesCannotChangeAssignments"),
  hasIntegration("ContainerAndVoyageEndToEnd"),
  hasIntegration("ContainerAndVoyageEndToEnd"),
  hasIntegration("ExclusionConstraintsAndDirectSqlGuards"),
  false, // Deterministic concurrent writers and revocation races still need evidence.
  false, // Interruption/cancellation after in-flight DB writes still need evidence.
  hasBrowser("G6 Analyst attaches container and voyage"),
  hasBrowser("G6 organization switch invalidates"),
  hasIntegration("ExistingM5RowsSurviveM6UpgradeRollbackAndReapply"),
  false, // Effective security mutation proofs are required.
  true
];
assert.equal(cases.length,25);
const output=["# G6 Raw Evidence Mapping","","Source: "+sha,"",
  "Mapping is a preliminary index. Presence of a passing test name does not establish",
  "the full assertion coverage of a scenario without independent review.","",
  "| Scenario | Automated evidence mapping |",
  "| --- | --- |"];
const missing=[];
for(let i=0;i<cases.length;i++){
 const id="G6-S"+String(i+1).padStart(2,"0");
 const ok=cases[i];
 output.push("| "+id+" | "+(ok?"EVIDENCE PRESENT, REVIEW REQUIRED":"INCOMPLETE")+" |");
 if(!ok)missing.push(id);
}
fs.mkdirSync(at("reports"),{recursive:true});
fs.writeFileSync(at("reports/G6_EVIDENCE_INDEX.md"),output.join("\n")+"\n");
console.log("G6 mapped evidence: "+(25-missing.length)+"/25; missing "+missing.join(", "));
assert.deepEqual(missing,[],"G6 contract or scenario evidence remains incomplete");
console.log("G6 executable evidence mapped; independent review and acceptance still required");
