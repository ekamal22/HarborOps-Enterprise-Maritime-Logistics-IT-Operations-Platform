import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const root = "src/HarborOps.Infrastructure/Persistence/";
const source = readFileSync(root + "HarborOpsDbContext.cs", "utf8").replaceAll("\r\n", "\n");
const snapshot = readFileSync(root + "Migrations/HarborOpsDbContextModelSnapshot.cs", "utf8");
const migration = readFileSync(root + "Migrations/20261101010000_VoyageContainerReferences.cs", "utf8");

for (const [property, column] of [
  ["VesselId", "vessel_id"],
  ["OriginPortId", "origin_port_id"],
  ["DestinationPortId", "destination_port_id"]
]) {
  const name = "IX_voyages_OrganizationId_" + property;
  const mapping = 'entity.HasIndex(x => new { x.OrganizationId, x.' +
    property + ' })\n                .HasDatabaseName("' + name + '");';
  assert.ok(source.includes(mapping), "Current model missing " + name);
  assert.ok(snapshot.includes(mapping), "Migration snapshot missing " + name);
  assert.ok(migration.includes('CREATE INDEX "' + name +
    '" ON voyages(organization_id,' + column + ');'),
    "SQL migration missing " + name);
  console.log("Index contract OK: " + name);
}
assert.ok(!source.includes("PendingModelChangesWarning"),
  "Do not suppress model drift in the current model");
assert.ok(!snapshot.includes("PendingModelChangesWarning"),
  "Do not suppress model drift in the snapshot");
