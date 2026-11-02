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
 "coverage-source","preflight","postgres-image","build","deps","web","docs","dotnet",
 "mutation","snapshot","stack","accounts","organizations","session","setup-fixtures","fixtures",
 "m4-fixtures","m5-fixtures","m6-fixtures","browsers",
 "assignment-outage","shipment-outage","reference-outage","maritime-outage",
 "recovery","scan","teardown","storage-after"
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
 ["integration-fresh",fresh,146],["integration-repeat",repeat,146]];
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
 "G6 logout disposes allocation views and context projections",
 "G6 conflicting container allocation shows an actionable error and preserves inputs",
 "G6 deferred allocation response cannot cross organization switch",
 "G6 live membership downgrade invalidates the open allocation workspace",
 "G6 shipment switch discards prior contextual links and late responses"
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
 assert.ok(names.size>=57,"Accepted G5 browser baseline plus ten M6 tests missing for "+engine);
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
const fixture=JSON.parse(read("g6-fixtures.json"));
assert.equal(fixture.sourceSha,sha,"Fixture source SHA mismatch");
assert.equal(fixture.tenantCount,2,"G6 requires two-tenant allocation fixtures");
assert.equal(fixture.replayVerified,true,"G6 fixture replay is not idempotent");
assert.deepEqual([...fixture.expectedRoles].sort(),
  ["Administrator","Manager","Analyst","Auditor","Support"].sort());
assert.equal(fixture.organizations.length,2);
assert.notEqual(fixture.organizations[0].organizationId,
  fixture.organizations[1].organizationId);
assert.notEqual(fixture.organizations[0].shipmentId,
  fixture.organizations[1].shipmentId);
for(const org of fixture.organizations){
  assert.equal(org.containerIds.length,2);
  assert.notEqual(org.containerIds[0],org.containerIds[1]);
  assert.equal(org.links.length,3);
  assert.deepEqual(org.links.map(x=>x.end===null),[false,true,true]);
  assert.deepEqual(org.links.map(x=>x.kind),
    ["container-assignments","container-assignments","voyage-assignments"]);
  assert.ok(org.links.every(x=>x.id&&x.targetId&&x.version===1));
  assert.ok(org.route.originPortId!==org.route.destinationPortId);
}
const fixtureDisposition=true;
const snapshot=JSON.parse(read("g6-snapshot.json"));
assert.equal(snapshot.sourceSha,sha);
const snapshotDisposition=
  (snapshot.outcome==="NOT APPLICABLE" &&
   Array.isArray(snapshot.discoveryPaths)&&snapshot.discoveryPaths.length>0 &&
   snapshot.reason?.length>20 && !snapshot.candidates?.length) ||
  (snapshot.outcome==="PASS" && snapshot.evidence?.before &&
   snapshot.evidence?.after && snapshot.evidence.m6Exclusions===2 &&
   JSON.stringify(snapshot.evidence.before)===JSON.stringify(snapshot.evidence.after)) ||
  (snapshot.outcome==="PASS" && snapshot.sourceUnmodified===true && snapshot.sourceVolume?.name &&
   snapshot.evidence?.m6Exclusions===2 &&
   Object.keys(snapshot.evidence.before?.tables??{}).length>0 &&
   Object.keys(snapshot.evidence.before.tables).length===snapshot.evidence.after?.preservedTables &&
   snapshot.evidence.addedTables?.includes("shipment_container_assignments"));
const decision=fs.readFileSync("docs/M6_LOCAL_CONTRACT_DECISION.md","utf8");
const contractApproved=/\*\*State:\*\*\s*Accepted\b/i.test(decision);
const assertChecks=[
 ["G6-S01",contractApproved,"Explicit twelve-rule M6 contract approval"],
 ["G6-S02",requires(unit,"HalfOpenWindowsAllowAdjacencyButNotOverlap","AcceptsExplicitUtc","RejectsNonUtcAndAmbiguousInstants")],
 ["G6-S03",requires(fresh,"ExclusionConstraintsAndDirectSqlGuardsProtectAllocationHistory")],
 ["G6-S04",requires(fresh,"ContainerAndVoyageEndToEndEnforceOverlapClosureAndParentGuards","VoyageAllowsManyShipmentsAndSequentialReassignmentWithoutWorkflowChanges")],
 ["G6-S05",requires(fresh,"CompositeForeignKeysRejectCrossTenantParentsWithoutValidationTrigger","RealForeignTenantParentFailsCompositeFk")],
 ["G6-S06",requires(unit,"RouteAndKnownScheduleMustMatch")&&requires(fresh,"ReverseRouteAndKnownDisjointSchedulesRejectVoyageAssignment")],
 ["G6-S07",requires(fresh,"ExclusionConstraintsAndDirectSqlGuardsProtectAllocationHistory")],
 ["G6-S08",requires(fresh,"ExistingM5RowsSurviveM6UpgradeRollbackAndReapply")],
 ["G6-S09",requires(fresh,"ExistingM5RowsSurviveM6UpgradeRollbackAndReapply")],
 ["G6-S10",snapshotDisposition,"Historical backup restore or documented absence"],
 ["G6-S11",fixtureDisposition,"Two-tenant and five-role idempotent bounded/open container and voyage fixtures"],
 ["G6-S12",requires(fresh,"EveryAssignmentEndpointHonorsFiveRoleMatrixForBothKinds","AuditorCanReadAssignmentsButCannotMutateOrReadMaritimeCatalog")&&requires(unit,"AssignmentPermissionsAreNarrow")],
 ["G6-S13",requires(fresh,"LiveMembershipDowngradeImmediatelyRevokesAssignmentAccess","QueuedAssignmentReadRechecksEveryAuthorizationDependency","QueuedAssignmentWriteRechecksEveryAuthorizationDependency")],
 ["G6-S14",requires(fresh,"ForeignAssignmentIdsReturnNotFoundInsideSelectedTenant","RealForeignTenantParentFailsCompositeFk")],
 ["G6-S15",requires(fresh,"MalformedAndCrossOriginWritesCannotChangeAssignments","MalformedJsonHeadersVersionsAndMutationsFailClosedWithoutSideEffects")],
 ["G6-S16",requires(fresh,"ContainerAndVoyageEndToEndEnforceOverlapClosureAndParentGuards")],
 ["G6-S17",requires(fresh,"VoyageAllowsManyShipmentsAndSequentialReassignmentWithoutWorkflowChanges","ReverseRouteAndKnownDisjointSchedulesRejectVoyageAssignment")],
 ["G6-S18",requires(fresh,"QueuedAllocationAndParentArchiveCannotCommitInconsistentState","VoyageCancellationAndAllocationCannotBothCommit","DirectSqlVoyageCancellationAndShipmentCancellationCannotBypassOpenLinks")],
 ["G6-S19",requires(fresh,"CompetingSameContainerWritesCommitAtMostOneAssignment","CompetingVoyagesForOneShipmentCommitAtMostOneAssignment","QueuedAllocationAndParentArchiveCannotCommitInconsistentState","QueuedAssignmentWriteRechecksEveryAuthorizationDependency")],
 ["G6-S20",requires(fresh,"TerminatingBackendAfterAssignmentInsertRollsBackWithoutOrphanHistory","CancelledAfterWriteAssignmentClosureRollsBack","TimedOutAfterWriteAssignmentClosureRollsBack","LostResponseAfterAssignmentCommitDoesNotDuplicateClosure")],
 ["G6-S21",expected.every(x=>browser.get("chromium").has(x))],
 ["G6-S22",expected.every(x=>browser.get("firefox").has(x)&&browser.get("webkit").has(x))],
 ["G6-S23",fresh.size>=146 && repeat.size>=146 && unit.size>=138 && contract.size>=12],
 ["G6-S24",mutation.mutants?.length===4 &&
    requires(fresh,"OverlapMutationRequiresBehavioralExclusionRejection",
      "ParentGuardMutationRequiresBehavioralArchiveRejection")],
 ["G6-S25",teardown.remainingContainers===0&&teardown.remainingVolumes===0]
];
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
