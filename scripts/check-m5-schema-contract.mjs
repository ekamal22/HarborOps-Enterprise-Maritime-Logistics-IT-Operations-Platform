#!/usr/bin/env node
import assert from "node:assert/strict";
import fs from "node:fs";
const root = "src/HarborOps.Infrastructure/Persistence/";
const read = p => fs.readFileSync(root + p, "utf8").replaceAll("\r\n", "\n");
const live = read("HarborOpsDbContext.cs");
const snapshot = read("Migrations/HarborOpsDbContextModelSnapshot.cs");
const migration = read("Migrations/20261101180000_ShipmentLifecycle.cs");
const select = s => {
  const start = s.indexOf("modelBuilder.Entity<ShipmentRecord>");
  const end = s.indexOf("\n    }\n\n    private static void ConfigureMaritime", start);
  assert.ok(start >= 0 && end > start, "Shipment EF mappings missing");
  return s.slice(start, end).replace(/\s+/g, " ");
};
assert.equal(select(live), select(snapshot), "Shipment live model/snapshot differ");
for (const name of [
  "AK_shipments_OrganizationId_Id", "IX_shipments_OrganizationId_ReferenceCode",
  "IX_shipments_OrganizationId_ExternalReference",
  "IX_shipments_OrganizationId_RecordStatus_Id",
  "IX_shipments_OrganizationId_WorkflowStatus_Id",
  "IX_shipments_OrganizationId_OriginPortId",
  "IX_shipments_OrganizationId_DestinationPortId",
  "IX_shipment_status_changes_OrganizationId_ShipmentId_Version"
]) assert.ok(migration.includes('"' + name + '"'), "Migration missing index " + name);
for (const clause of [
  "FK_shipments_origin", "FK_shipments_destination",
  "FK_shipment_status_changes_shipment", "CREATE TRIGGER shipments_validate",
  "CREATE TRIGGER shipments_status_history", "CREATE TRIGGER status_history_immutable",
  "pg_trigger_depth()"
]) assert.ok(migration.includes(clause), "Migration missing invariant " + clause);
assert.ok(!live.includes("PendingModelChangesWarning") &&
  !snapshot.includes("PendingModelChangesWarning"), "EF drift suppression prohibited");
console.log("M5 EF model, snapshot and migration source contract: PASS");
