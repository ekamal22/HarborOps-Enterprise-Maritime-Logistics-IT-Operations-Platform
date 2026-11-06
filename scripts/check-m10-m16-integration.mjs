import assert from "node:assert/strict";
import fs from "node:fs";

const root = new URL("../", import.meta.url);
const read = path => fs.readFileSync(new URL(path, root), "utf8");
const program = read("src/HarborOps.Api/Program.cs");
const auth = read("src/HarborOps.Api/Authentication/AuthenticationSetup.cs");
const provision = read("src/HarborOps.Api/Audit/AuditRoleProvisioner.cs");
const migration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261105180000_ShipmentTrackingProjections.cs");
const combined = read("tests/HarborOps.IntegrationTests/CombinedRestrictedRoleTests.cs");
const migrationOrder = read("tests/HarborOps.IntegrationTests/MigrationSnapshotTests.cs");
const compose = read("compose.g10.verify.yaml");
const status = read("docs/STATUS.md");

assert.match(program, /app\.MapAuditEndpoints\(\)/);
assert.match(program, /app\.MapShipmentTimelineEndpoints\(\)/);
assert.match(program, /builder\.Services\.AddScoped<ShipmentTimelineStore>\(\)/);
assert.match(auth, /"audit-read"/);
assert.match(auth, /"m10-mutations"/);

const order = ["20261104180000_AuditEntries", "20261104190000_TrackingWorker",
  "20261105180000_ShipmentTrackingProjections"];
let last = -1;
for (const id of order) {
  const i = migrationOrder.indexOf(id);
  assert.ok(i > last, "Combined EF migration order must be monotonic: " + id);
  last = i;
}
assert.match(provision, /m16_record_audit_access/);
assert.match(provision, /m16_api_scope_work_item/);
assert.match(provision, /m16_api_scope_attempt/);
assert.match(provision, /DO \$m10_roles\$/);
assert.match(provision, /GRANT INSERT ON shipment_tracking_corrections/);
assert.match(provision, /GRANT UPDATE ON shipment_tracking_projections TO harborops_api/);
assert.ok(!/GRANT (?:INSERT|UPDATE|DELETE) ON shipment_tracking_(?:correction_heads|input_versions|timeline_entries) TO harborops_api/.test(provision));
assert.match(combined, /harborops_worker/);
assert.match(combined, /shipment_tracking_operation_replays/);
assert.match(migration, /m10_validate_correction\(\) RETURNS trigger\s+LANGUAGE plpgsql SECURITY DEFINER/);
assert.match(migration, /AFTER INSERT ON tracking_processing_effects/);
assert.match(compose, /AuditRoles__ApiPassword/);
assert.match(compose, /AuditRoles__WorkerPassword/);
assert.match(compose, /Username=harborops_api/);
assert.match(compose, /Username=harborops_worker/);
assert.match(status, /G16 accepted and merged/);
assert.match(status, /G10 acceptance pending/);
console.log("M10/M16 combined source reconciliation checks: PASS (not a runtime verification)");
