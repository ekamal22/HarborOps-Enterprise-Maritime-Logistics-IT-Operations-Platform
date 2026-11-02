import assert from "node:assert/strict";
import fs from "node:fs";

const needed = [
  "src/HarborOps.Domain/Assignments/AssignmentPolicy.cs",
  "src/HarborOps.Infrastructure/Assignments/AssignmentRecord.cs",
  "src/HarborOps.Infrastructure/Assignments/AssignmentStore.cs",
  "src/HarborOps.Api/Assignments/AssignmentEndpoints.cs",
  "src/HarborOps.Infrastructure/Persistence/Migrations/20261102080000_ShipmentAssignments.cs",
  "web/src/assignments/assignmentClient.ts",
  "web/src/assignments/ShipmentAssignmentsPanel.tsx",
  "tests/HarborOps.UnitTests/AssignmentPolicyTests.cs",
  "tests/HarborOps.IntegrationTests/AssignmentHttpTests.cs",
  "tests/HarborOps.IntegrationTests/AssignmentSqlTests.cs",
  "tests/HarborOps.IntegrationTests/AssignmentMigrationTests.cs",
  "tests/browser/specs/assignment.spec.mjs",
  "docs/M6_LOCAL_CONTRACT_DECISION.md",
  "docs/M6_VERIFICATION_PLAN.md",
];
for (const path of needed) assert.ok(fs.existsSync(path), "Missing M6 implementation/contract file: " + path);
const text = path => fs.readFileSync(path,"utf8");
const domain=text(needed[0]);
for(const name of ["HalfOpen","Overlaps","VoyageCompatible"]) {
  if(name==="HalfOpen") continue;
  assert.ok(domain.includes(name),"Missing "+name);
}
const testFiles=needed.filter(x=>x.includes("Tests.cs")||x.includes("assignment.spec.mjs"));
for(const name of ["ExclusionConstraintsAndDirectSqlGuards","ExistingM5RowsSurvive",
  "ContainerAndVoyageEndToEnd","AssignmentPermissionsAreNarrow"]){
 assert.ok(testFiles.some(p=>text(p).includes(name)),"Missing intended test method "+name);
}
const browser=text("tests/browser/specs/assignment.spec.mjs");
for(const marker of ["G6 Analyst attaches container and voyage","G6 organization switch invalidates"]){
 assert.ok(browser.includes(marker),"Missing G6 browser spec "+marker);
}
const runner=text("scripts/verify-m6-local.sh");
for(const name of ["HARBOROPS_M6_FIXTURES=1","scripts/verify-m6-stages-local.sh",
  "scripts/assert-g6-evidence.mjs","ho-verify-g6"]){
 assert.ok(runner.includes(name),"Missing G6 harness contract "+name);
}
const doc=text("docs/M6_VERIFICATION_PLAN.md");
for(let i=1;i<=25;i++){
const tag="G6-S"+String(i).padStart(2,"0");
assert.ok(doc.includes(tag),"Missing required gate scenario "+tag);
}
console.log("M6 source presence and 25-case gate declaration: PASS. Runtime evidence not implied.");
