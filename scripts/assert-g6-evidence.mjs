import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const dir=process.argv[2];
assert.ok(dir && fs.existsSync(dir),"G6 evidence directory required");
const at=x=>path.join(dir,x);
const read=x=>fs.readFileSync(at(x),"utf8");
const sha=read("g6-source-sha.txt").trim();
assert.match(sha,/^[a-f0-9]{40}$/);
assert.equal(read("m6-source-sha.txt").trim(),sha);
const lines=read("g6-stages.tsv").trim().split(/\r?\n/).map(x=>x.split("\t"));
const stages=[
 "storage-before","decision-unit","schema-index","shipment-schema","assignment-schema",
 "coverage-source","preflight","postgres-image","deps","web","docs","stack",
 "accounts","organizations","session","setup-fixtures","fixtures",
 "m4-fixtures","m5-fixtures","m6-fixtures","browsers",
 "assignment-outage","shipment-outage","reference-outage","maritime-outage",
 "recovery","scan","dotnet","mutation","snapshot","teardown","storage-after"
];
for(const stage of stages){
 const matching=lines.filter(row=>row[0]===stage);
 assert.equal(matching.length,1,"Missing or repeated G6 stage "+stage);
 assert.equal(matching[0][1],sha,"Wrong G6 source in "+stage);
 assert.equal(matching[0][2],"0","Failed executable stage "+stage);
 assert.ok(fs.statSync(at("g6-"+stage+".log")).size>0,"Empty stage log "+stage);
}
const teardown=JSON.parse(read("g6-teardown.json"));
assert.equal(teardown.sourceSha,sha);
for(const key of ["remainingContainers","remainingVolumes","remainingIntegrationContainers"])
 assert.equal(teardown[key],0,"Unreleased G6 resource "+key);
for(const phase of ["before","after"]){
 const item=JSON.parse(read("g6-storage-"+phase+".json"));
 assert.equal(item.sourceSha,sha,"Storage evidence refers to a different source");
}
const unit=new Set(),contract=new Set(),fresh=new Set(),repeat=new Set();
const suites=[["unit",unit,138],["contract",contract,12],
 ["integration-fresh",fresh,139],["integration-repeat",repeat,139]];
for(const [phase,names,minimum] of suites){
 const trx=read("trx/"+phase+".trx");
 const rows=[...trx.matchAll(/<UnitTestResult\b([^>]*?)\/?>/g)];
 assert.ok(rows.length>=minimum,"Missing accepted regression executions in "+phase);
 for(const match of rows){
   const name=/testName="([^"]+)"/.exec(match[1])?.[1];
   const result=/outcome="([^"]+)"/.exec(match[1])?.[1];
   assert.ok(name && result==="Passed","Failed, skipped, or unnamed "+phase+" test "+name);
   assert.ok(!names.has(name),"Duplicate TRX test result "+phase+" "+name);
   names.add(name);
 }
}
assert.deepEqual([...fresh].sort(),[...repeat].sort(),
 "Fresh and repeated PostgreSQL test inventories differ");
const has=(set,name)=>[...set].some(x=>x.includes(name));
const requires=(set,...names)=>names.every(n=>has(set,n));
const browser=new Map();
const expected=[
 "G6 Analyst attaches container and voyage and retains the shipment draft",
 "G6 organization switch invalidates shipment allocation panels",
 "G6 Auditor reads scoped assignment histories without catalog or mutation access",
 "G6 Support cannot open typed shipment assignment history",
 "G6 tab return refresh preserves allocation input and shipment draft",
 "G6 logout disposes allocation views and context projections"
];
function walk(suite,names){
 for(const spec of suite.specs||[]){
   for(const test of spec.tests||[]){
     assert.ok(Array.isArray(test.results)&&test.results.length>0,
       "Missing actual browser execution: "+spec.title);
     assert.ok(test.results.every(r=>r.status==="passed"),
       "Failed, skipped or interrupted browser: "+spec.title);
     assert.ok(!names.has(spec.title),"Duplicated browser spec "+spec.title);
     names.add(spec.title);
   }
 }
 for(const nested of suite.suites||[])walk(nested,names);
}
for(const engine of ["chromium","firefox","webkit"]){
 const folder=at("playwright");
 const reports=fs.readdirSync(folder).filter(x=>
   x.startsWith(engine+"-") && x.endsWith(".json"));
 assert.ok(reports.includes(engine+"-assignment.json"),
   "Missing engine-specific M6 allocation report "+engine);
 const names=new Set();
 for(const file of reports){
   const doc=JSON.parse(fs.readFileSync(path.join(folder,file),"utf8"));
   for(const suite of doc.suites||[])walk(suite,names);
 }
 assert.ok(names.size>=45,"Baseline browser tests missing for "+engine);
 for(const scenario of expected)
   assert.ok(names.has(scenario),"Missing required "+engine+" scenario: "+scenario);
 browser.set(engine,names);
}
const baseline=[...browser.values()][0];
for(const items of browser.values())
 assert.deepEqual([...items].sort(),[...baseline].sort(),
   "Inconsistent browser inventories");
const mutation=JSON.parse(read("g6-mutations.json"));
assert.equal(mutation.sourceSha,sha);
assert.equal(mutation.control?.status,"PASS");
for(const variant of ["tenant-read","post-lock","overlap","parent-guard"]){
 const proof=mutation.mutants?.find(x=>x.variant===variant);
 assert.equal(proof?.status,"KILLED","Missing effective mutation proof "+variant);
 assert.ok(proof.failedTests.some(x=>x.includes(proof.expectedFailure)),
   "Mutation failed for an unrelated reason: "+variant);
}
const snapshot=JSON.parse(read("g6-snapshot.json"));
assert.equal(snapshot.sourceSha,sha);
const snapshotDisposition=
  (snapshot.outcome==="NOT APPLICABLE" &&
   Array.isArray(snapshot.discoveryPaths)&&snapshot.discoveryPaths.length>0 &&
   snapshot.reason?.length>20 && !snapshot.candidates?.length) ||
  (snapshot.outcome==="PASS" && snapshot.evidence?.before &&
   snapshot.evidence?.after && snapshot.evidence.m6Exclusions===2 &&
   JSON.stringify(snapshot.evidence.before)===JSON.stringify(snapshot.evidence.after));
const contract=fs.readFileSync("docs/M6_LOCAL_CONTRACT_DECISION.md","utf8");
const contractApproved=/\*\*State:\*\*\s*Accepted\b/i.test(contract);
const assertChecks=[
 ["G6-S01",contractApproved,"Explicit twelve-rule M6 contract approval"],
 ["G6-S02",requires(unit,"HalfOpenWindowsAllowAdjacencyButNotOverlap","AcceptsExplicitUtc","RejectsNonUtcAndAmbiguousInstants")],
 ["G6-S03",requires(fresh,"ExclusionConstraintsAndDirectSqlGuardsProtectAllocationHistory")],
 ["G6-S04",requires(fresh,"ContainerAndVoyageEndToEndEnforceOverlapClosureAndParentGuards")],
 ["G6-S05",requires(fresh,"CompositeForeignKeysRejectCrossTenantParentsWithoutValidationTrigger")],
 ["G6-S06",requires(unit,"RouteAndKnownScheduleMustMatch")],
 ["G6-S07",requires(fresh,"ExclusionConstraintsAndDirectSqlGuardsProtectAllocationHistory")],
 ["G6-S08",requires(fresh,"ExistingM5RowsSurviveM6UpgradeRollbackAndReapply")],
 ["G6-S09",requires(fresh,"ExistingM5RowsSurviveM6UpgradeRollbackAndReapply")],
 ["G6-S10",snapshotDisposition,"Historical backup restore or documented absence"],
 ["G6-S11",fs.statSync(at("g6-m6-fixtures.log")).size>0],
 ["G6-S12",requires(fresh,"AuditorCanReadAssignmentsButCannotMutateOrReadMaritimeCatalog")&&requires(unit,"AssignmentPermissionsAreNarrow")],
 ["G6-S13",requires(fresh,"LiveMembershipDowngradeImmediatelyRevokesAssignmentAccess")],
 ["G6-S14",requires(fresh,"ForeignAssignmentIdsReturnNotFoundInsideSelectedTenant","RealForeignTenantParentFailsCompositeFk")],
 ["G6-S15",requires(fresh,"MalformedAndCrossOriginWritesCannotChangeAssignments")],
 ["G6-S16",requires(fresh,"ContainerAndVoyageEndToEndEnforceOverlapClosureAndParentGuards")],
 ["G6-S17",requires(fresh,"ContainerAndVoyageEndToEndEnforceOverlapClosureAndParentGuards")],
 ["G6-S18",requires(fresh,"QueuedAllocationAndParentArchiveCannotCommitInconsistentState","VoyageCancellationAndAllocationCannotBothCommit")],
 ["G6-S19",requires(fresh,"CompetingSameContainerWritesCommitAtMostOneAssignment","QueuedAllocationAndParentArchiveCannotCommitInconsistentState")],
 ["G6-S20",requires(fresh,"TerminatingBackendAfterAssignmentInsertRollsBackWithoutOrphanHistory")],
 ["G6-S21",expected.every(x=>browser.get("chromium").has(x))],
 ["G6-S22",expected.every(x=>browser.get("firefox").has(x)&&browser.get("webkit").has(x))],
 ["G6-S23",fresh.size>=139 && unit.size>=138 && contract.size===undefined],
 ["G6-S24",mutation.mutants?.length>=4],
 ["G6-S25",teardown.remainingContainers===0&&teardown.remainingVolumes===0]
];
// Correct the G6-S23 check: the original accepted contract suite is present in TRX.
assertChecks[22][1]=fresh.size>=139 && unit.size>=138 &&
  suites[1][1].size>=12;
assert.equal(assertChecks.length,25);
const report=["# G6 source-locked executed scenario index","","Source: "+sha,"",
  "Scenario evidence is pending independent review even when executable tests pass.","",
  "| Scenario | State | Supporting assertion |","| --- | --- | --- |"];
const incomplete=[];
for(const [name,ok,proof] of assertChecks){
 report.push("| "+name+" | "+(ok?"EVIDENCE PRESENT, REVIEW REQUIRED":"INCOMPLETE")+
   " | "+(proof||"Executed test and stage evidence required")+" |");
 if(!ok)incomplete.push(name);
}
fs.mkdirSync(at("reports"),{recursive:true});
fs.writeFileSync(at("reports/G6_EVIDENCE_INDEX.md"),report.join("\n")+"\n");
console.log("G6 scenarios with executed evidence: "+(25-incomplete.length)+"/25");
assert.deepEqual(incomplete,[],
 "G6 evidence remains incomplete: "+incomplete.join(", "));
console.log("G6 technical evidence complete; explicit independent review still required");
