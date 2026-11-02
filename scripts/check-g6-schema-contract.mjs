import assert from "node:assert/strict";
import fs from "node:fs";

const read = p => fs.readFileSync(p, "utf8").replaceAll("\r\n", "\n");
const model = read("src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs");
const snapshot = read("src/HarborOps.Infrastructure/Persistence/Migrations/HarborOpsDbContextModelSnapshot.cs");
const migration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261102080000_ShipmentAssignments.cs");
for (const name of ["ShipmentContainerAssignment","ShipmentVoyageAssignment","ConfigureAssignment",
  "HasAlternateKey(x => new { x.OrganizationId, x.Id })",
  "ViewShipmentAssignments"]) {
  const source = name === "ViewShipmentAssignments"
    ? read("src/HarborOps.Domain/Organizations/OrganizationPolicy.cs") : model;
  assert.ok(source.includes(name),"Missing current model/role: "+name);
  if (name !== "ViewShipmentAssignments")
    assert.ok(snapshot.includes(name),"Missing EF snapshot: "+name);
}
function region(source, start, end) {
  const a=source.indexOf(start), b=source.indexOf(end,a+1);
  assert.ok(a>=0 && b>a,"Missing schema block "+start);
  return source.slice(a,b);
}
assert.equal(
  region(model,"        modelBuilder.Ignore<AssignmentRecord>();",
    "        modelBuilder.Entity<ShipmentRecord>(entity =>"),
  region(snapshot,"        modelBuilder.Ignore<AssignmentRecord>();",
    "        modelBuilder.Entity<ShipmentRecord>(entity =>"),
  "Assignment EF model/snapshot entity mapping diverged"
);
assert.equal(
  region(model,"    private static void ConfigureAssignment",
    "    private static void ConfigureMaritime"),
  region(snapshot,"    private static void ConfigureAssignment",
    "    private static void ConfigureMaritime"),
  "Assignment shared model/snapshot mapping diverged"
);
const required=["CREATE EXTENSION IF NOT EXISTS btree_gist",
  "shipment_container_assignments","shipment_voyage_assignments",
  "ex_m6_container_overlap","ex_m6_voyage_overlap",
  "tstzrange(starts_at_utc,ends_at_utc,'[)')",
  "FK_m6_container_target","FK_m6_voyage_target","FK_m6_voyage_shipment",
  "m6_validate_assignment","m6_guard_parent","zz_m6_guard_shipment",
  "zz_m6_guard_container","zz_m6_guard_voyage"];
for(const term of required) assert.ok(migration.includes(term),"Migration missing "+term);
assert.ok(!migration.includes("DROP EXTENSION"),"Do not drop potentially shared btree_gist");
const names=[...fs.readdirSync("src/HarborOps.Infrastructure/Persistence/Migrations")
  .filter(f=>/^\d{14}_.*\.cs$/.test(f))];
assert.equal(new Set(names.map(x=>x.slice(0,14))).size,names.length,
  "Duplicate migration ID");
assert.ok(names.some(x=>x==="20261102080000_ShipmentAssignments.cs"));
console.log("M6 static schema, snapshot and migration source contract: PASS");
