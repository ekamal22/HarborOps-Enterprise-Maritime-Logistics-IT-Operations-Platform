#!/usr/bin/env node
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const dir = process.argv[2];
assert.ok(dir && fs.existsSync(dir),"Current G5 evidence directory required");
const at = p=>path.join(dir,p);
const read=p=>fs.readFileSync(at(p),"utf8");
const sha=read("g5-source-sha.txt").trim();
assert.match(sha,/^[0-9a-f]{40}$/);
assert.equal(read("m5-source-sha.txt").trim(),sha);
assert.equal(execFileSync("git",["rev-parse","HEAD"],{encoding:"utf8"}).trim(),sha);
const started=Date.parse(read("g5-start-utc.txt").trim());
assert.ok(Number.isFinite(started) && started<=Date.now() &&
  Date.now()-started < 24*3600*1000,"Invalid G5 source execution timestamp");
const fresh=p=>fs.existsSync(at(p)) && fs.statSync(at(p)).size>0 &&
  fs.statSync(at(p)).mtimeMs >= started-2000;
assert.ok(!fs.existsSync(at("g5-aborted.txt")),"Aborted run cannot satisfy G5");

const requiredStages=[
  "storage-before","decision-unit","schema-index","shipment-schema","preflight","postgres-image",
  "dotnet","deps","web","docs","stack","accounts","organizations","session",
  "setup-fixtures","fixtures","m4-fixtures","m5-fixtures","browsers",
  "reference-outage","maritime-outage","shipment-outage","recovery","scan","teardown","storage-after"
];
const ledger=read("g5-stages.tsv").trim().split(/\r?\n/).map(line=>line.split("\t"));
for(const name of requiredStages){
  const rows=ledger.filter(row=>row[0]===name);
  assert.equal(rows.length,1,"Missing or duplicate G5 stage "+name);
  assert.equal(rows[0][1],sha,"Wrong stage source "+name);
  assert.equal(rows[0][2],"0","Failed required stage "+name);
  assert.ok(fresh("g5-"+name+".log"),"Missing current G5 stage log "+name);
}
const cleanup=JSON.parse(read("g5-teardown.json"));
assert.equal(cleanup.sourceSha,sha);
for(const property of ["remainingContainers","remainingVolumes","remainingIntegrationContainers"])
  assert.equal(cleanup[property],0,"G5 teardown left resources: "+property);
assert.ok(fresh("g5-docker-services.log"),"Missing raw G5 service logs");
const before=JSON.parse(read("g5-storage-before.json"));
const after=JSON.parse(read("g5-storage-after.json"));
for(const entry of [before,after]){
  assert.equal(entry.sourceSha,sha);
  assert.equal(entry.drives?.length,2);
  for(const key of ["TEMP","TMP","TMPDIR","NPM_CONFIG_CACHE","PLAYWRIGHT_BROWSERS_PATH",
    "NUGET_PACKAGES","DOTNET_CLI_HOME"])
    assert.match(entry.controlledPaths[key].replaceAll("\\","/"),/^(?:[Ee]:\/|\/e\/)/);
}
const fixture=JSON.parse(read("g5-fixtures.json"));
assert.equal(fixture.sourceSha,sha);
assert.equal(fixture.idempotent,true);
assert.equal(fixture.organizations.length,2);
assert.equal(new Set(fixture.organizations.map(x=>x.organizationId)).size,2);
const north=fixture.organizations.find(x=>x.slug==="m3-north");
assert.equal(Object.keys(north.verifiedRoles||{}).length,5);
for(const org of fixture.organizations){
  const rows=org.shipments;
  assert.ok(rows.length>=4 && rows.some(x=>x.recordStatus==="Archived") &&
    rows.some(x=>x.workflowStatus==="Booked") &&
    rows.some(x=>x.workflowStatus==="InTransit"));
  assert.equal(new Set(rows.map(x=>x.id)).size,rows.length);
}
const shared=fixture.organizations.map(x=>x.shipments.find(y=>y.referenceCode==="M5-SHARED-REF"));
assert.ok(shared.every(Boolean));
assert.notEqual(shared[0].id,shared[1].id);
assert.notEqual(shared[0].externalReference,shared[1].externalReference);

const reports={};
for(const phase of ["unit","contract","integration-fresh","integration-repeat"]){
  const name="trx/"+phase+".trx";
  assert.ok(fresh(name),"Missing independent .NET TRX phase "+phase);
  const xml=read(name);
  assert.match(xml,/<TestRun\b/);
  const list=[];
  for(const [tag] of xml.matchAll(/<UnitTestResult\b[^>]*>/g)){
    const result=/\boutcome="([^"]+)"/.exec(tag)?.[1];
    const name=/\btestName="([^"]+)"/.exec(tag)?.[1];
    assert.ok(name && result,"Incomplete TRX result");
    assert.equal(result,"Passed","Unexpected "+phase+" test "+name);
    list.push(name);
  }
  assert.ok(list.length>0,"No passed test outcomes in "+phase);
  reports[phase]=list;
}
assert.equal(reports["integration-fresh"].length,reports["integration-repeat"].length,
  "Fresh/repeat integration result totals differ");
assert.deepEqual([...reports["integration-fresh"]].sort(),
  [...reports["integration-repeat"]].sort(),"Fresh/repeat executed tests differ");
const testRows=(phase,name)=>{
  const expression=new RegExp("(?:^|\\.)"+name.replace(/[.*+?^${}()|[\]\\]/g,"\\$&")+"(?:\\(|$)");
  return reports[phase].filter(x=>expression.test(x));
};
const unit=(name,count=1)=>testRows("unit",name).length>=count;
const integration=(name,count=1)=>
  ["integration-fresh","integration-repeat"].every(phase=>testRows(phase,name).length>=count);

const browsers=[];
const browserRoot=at("playwright");
assert.ok(fs.existsSync(browserRoot));
for(const file of fs.readdirSync(browserRoot).filter(x=>x.endsWith(".json"))){
  const p=path.join(browserRoot,file);
  assert.ok(fs.statSync(p).mtimeMs>=started-2000);
  const report=JSON.parse(fs.readFileSync(p,"utf8"));
  for(const key of ["unexpected","skipped","interrupted"])
    assert.equal(report.stats?.[key]??0,0,"Browser report "+file+" has "+key);
  function visit(suites){
    for(const suite of suites||[]){
      for(const spec of suite.specs||[])
        for(const item of spec.tests||[]){
          assert.ok(item.projectName && item.results?.length,"Malformed browser test result");
          assert.ok(item.results.every(x=>x.status==="passed"),
            "Nonpassing browser test: "+spec.title);
          browsers.push({engine:item.projectName,title:spec.title});
        }
      visit(suite.suites);
    }
  }
  visit(report.suites);
}
const engines=["chromium","firefox","webkit"];
const browser=title=>engines.every(engine=>
  browsers.some(x=>x.title===title&&x.engine===engine));
// Every declared shipment browser spec must have at least one result per engine.
const browserSource=fs.readFileSync("tests/browser/specs/shipment.spec.mjs","utf8");
const declared=[...browserSource.matchAll(/\btest\("([^"]+)"/g)].map(x=>x[1]);
assert.ok(declared.length>=7,"Missing shipment browser scenarios");
for(const name of declared)
  assert.ok(browser(name),"Missing browser engine/spec result "+name);
const scans=["postgres","api","web"].every(name=>{
  const file="trivy/"+name+".json";
  if(!fresh(file))return false;
  const scan=JSON.parse(read(file));
  return Boolean(scan.ArtifactName) && Array.isArray(scan.Results) &&
    scan.Results.every(entry=>
      !(entry.Vulnerabilities||[]).some(v=>["HIGH","CRITICAL"].includes(v.Severity)));
});
const decision=fs.readFileSync("docs/M5_LOCAL_CONTRACT_DECISION.md","utf8");
const accepted=/^\*\*State:\*\* Accepted\b/m.test(decision);

const cases=[
["S01",accepted&&unit("ShipmentPolicyTests.FullWorkflowMatrix",36)],
["S02",unit("ShipmentPolicyTests.FullWorkflowMatrix",36)&&unit("ShipmentPolicyTests.RejectUnknownStatus",3)],
["S03",unit("ShipmentPolicyTests.ReferencesAndUtcAreStrict")&&integration("ShipmentExtendedGateTests.InvalidJsonContentAndVersionMatrix")],
["S04",integration("MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel")&&integration("MigrationSnapshotTests.Migrations_AreDiscoveredInOrder")],
["S05",integration("ShipmentMigrationTests.SeededM4HistorySurvivesM5UpgradeRollbackAndReapply")],
["S06",integration("ShipmentSqlMatrixTests.CompositePortsAndImmutableFieldsRejectIndependentSqlMutations")],
["S07",fixture.organizations.length===2&&Boolean(north.verifiedRoles)],
["S08",integration("ShipmentBoundaryTests.AllShipmentRoutesRejectInvalidatedAuthentication",5)&&integration("ShipmentExtendedGateTests.AnonymousAllSixShipmentRoutesRejectAccess")],
["S09",integration("ShipmentExtendedGateTests.MissingSelectionAndForeignActualShipmentNeverDiscloseData")&&integration("ShipmentExtendedGateTests.QueuedShipmentReadRechecksEveryAuthorizationDependency",8)],
["S10",integration("ShipmentHttpTests.DuplicateArchivedReferenceAndCrossTenantIdentityStayScoped")&&integration("ShipmentExtendedGateTests.MissingSelectionAndForeignActualShipmentNeverDiscloseData")],
["S11",integration("ShipmentBoundaryTests.ForeignAndArchivedPortsRejectShipmentCreation")&&integration("ShipmentSqlMatrixTests.CompositePortsAndImmutableFieldsRejectIndependentSqlMutations")&&integration("ShipmentExtendedGateTests.SameParentAndUnknownPortsRejectCreation")],
["S12",integration("ShipmentExtendedGateTests.FullSixRouteFiveRolePermissions")&&unit("ShipmentPolicyTests.ShipmentRolesAreIndependentOfM4")],
["S13",integration("ShipmentExtendedGateTests.QueuedShipmentWriteRechecksEveryAuthorizationDependency",8)&&integration("ShipmentExtendedGateTests.QueuedShipmentReadRechecksEveryAuthorizationDependency",8)],
["S14",integration("ShipmentExtendedGateTests.InvalidJsonContentAndVersionMatrix")&&integration("ShipmentBoundaryTests.InvalidPayloadOriginAndCsrfAreRejected")],
["S15",integration("ShipmentSqlMatrixTests.ExternalIdentifiersReserveArchivedValuesButPermitCrossTenantReuse")&&integration("ShipmentExtendedGateTests.DuplicateConcurrentCreationCommitsExactlyOneShipment")],
["S16",integration("ShipmentHttpTests.AnalystCreatesTransitionsArchivesRestoresAndReadsImmutableHistory")&&integration("ShipmentSqlMatrixTests.StatusNeutralEditAndArchiveNeverFabricateHistory")],
["S17",integration("ShipmentSqlMatrixTests.StatusNeutralEditAndArchiveNeverFabricateHistory")&&integration("ShipmentBoundaryTests.FailedTransitionLeavesNoPartialHistory")],
["S18",integration("ShipmentHttpTests.AnalystCreatesTransitionsArchivesRestoresAndReadsImmutableHistory")&&integration("ShipmentExtendedGateTests.ConcurrentArchiveAndStatusEditProduceOneVersionWinner")&&integration("ShipmentExtendedGateTests.ArchivedRestoreAndStaleEditCannotBothCommit")],
["S19",integration("ShipmentBoundaryTests.PaginationAndFiltersStayTenantScoped")],
["S20",integration("ShipmentExtendedGateTests.ConcurrentArchiveAndStatusEditProduceOneVersionWinner")&&integration("ShipmentExtendedGateTests.DuplicateConcurrentCreationCommitsExactlyOneShipment")&&integration("ShipmentHttpTests.CompetingExpectedVersionUpdatesHaveOneWinnerAndOneHistoryRecord")&&integration("ShipmentExtendedGateTests.ArchivedRestoreAndStaleEditCannotBothCommit")&&integration("ShipmentExtendedGateTests.DuplicateArchiveAttemptsCannotBothCommit")],
["S21",integration("ShipmentBoundaryTests.FailedTransitionLeavesNoPartialHistory")],
["S22",browser("M5 Analyst creates and advances a scoped shipment")&&browser("M5 Manager advances terminal workflows and retains manual history")&&browser("M5 stale edit displays conflict and retains entered values")],
["S23",browser("M5 Auditor shipment view does not grant M4 management")&&browser("M5 tenant change invalidates prior shipment list")&&browser("M5 deferred shipment response cannot cross organization switch")&&browser("M5 failed parent catalog retries without suppressing shipment list")],
["S24",unit("MaritimePolicyTests.RoleGrantsStayWithinBusinessPermissions")&&integration("ReferenceDataHttpTests.ScopedPortAndVesselLifecycleAndRoleMatrix")&&
  reports["contract"].length>0&&browsers.length>=engines.length*declared.length],
["S25",scans&&requiredStages.every(stage=>ledger.some(row=>row[0]===stage&&row[2]==="0"))&&
  fresh("g5-docker-services.log")&&fresh("g5-teardown.json")&&fresh("g5-storage-after.json")]
];
const missing=cases.filter(([,ok])=>!ok).map(([id])=>"G5-"+id);
for(const [id,ok] of cases)
  console.log("G5-"+id+": "+(ok?"EVIDENCE PRESENT, REVIEW REQUIRED":"INCOMPLETE"));
assert.deepEqual(missing,[],"Missing required G5 evidence: "+missing.join(", "));
console.log("All 25 scenario mappings have executable evidence, independent review required.");
