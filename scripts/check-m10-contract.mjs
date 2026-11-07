import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";

const read = path => fs.readFileSync(new URL("../" + path, import.meta.url), "utf8");
const migration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261105180000_ShipmentTrackingProjections.cs");
const authority = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261107120000_M10CorrectionAuthority.cs");
const store = read("src/HarborOps.Infrastructure/Shipments/ShipmentTimelineStore.cs");
const signer = read("src/HarborOps.Api/Shipments/M10CorrectionProofSigner.cs");
const roles = read("src/HarborOps.Api/Audit/AuditRoleProvisioner.cs");
const reducer = read("src/HarborOps.Domain/Tracking/TrackingTimelinePolicy.cs");
const endpoints = read("src/HarborOps.Api/Shipments/ShipmentTimelineEndpoints.cs");
const projector = read("src/HarborOps.Api/Shipments/TrackingProjectionHostedService.cs");
const grants = read("scripts/configure-m10-projector-db.sql");

// Migration 12 is an accepted, immutable historical migration. Databases that already executed it
// never re-run it, so a behavior change must be delivered by a later additive migration instead.
assert.equal(createHash("sha256").update(migration.replace(/\r\n/g, "\n")).digest("hex"),
  "c1fcc719c3231a370a4c6d17a1cccd354a4d10fb91e605facd2a394c699c0efc",
  "Migration 20261105180000_ShipmentTrackingProjections must stay byte-identical to the accepted version");
assert.ok(!/COALESCE\(organization_state/.test(migration), "The NULL-safe scope function belongs to migration 13");
assert.equal((migration.match(/protected override void Up\(/g) || []).length, 1);
assert.equal((migration.match(/protected override void Down\(/g) || []).length, 1);
assert.equal((migration.match(/migrationBuilder\.Sql\("""/g) || []).length, 2);
assert.equal((migration.match(/"""/g) || []).length, 4);
assert.ok(migration.trimEnd().endsWith("}"));
assert.match(migration, /ck_m10_correction_digest CHECK\s*\(correction_digest ~ '\^\[0-9a-f\]\{64\}\$'\)/);
assert.match(migration, /ck_m10_replay_digest CHECK \(request_digest ~ '\^\[0-9a-f\]\{64\}\$'\)/);
assert.match(migration, /CREATE INDEX ix_m10_timeline_keyset/);
assert.match(migration, /CREATE FUNCTION m10_validate_timeline_entry/);
assert.match(migration, /tr_m10_validate_timeline_entry/);
assert.match(migration, /AFTER INSERT ON tracking_processing_effects/);
assert.match(migration, /CREATE TRIGGER tr_m10_immutable_replays/);
assert.match(migration, /m10_lock_projector_scope\(p_org/);
assert.match(migration, /M10 retained history prevents downgrade/);
assert.match(store, /FROM shipment_tracking_timeline_entries e/);
assert.match(reducer, /CompareUuid\(a.SourceId, b.SourceId\)/);
assert.match(reducer, /CompareUuid\(a.ReceiptId, b.ReceiptId\)/);
assert.match(reducer, /CompareUuid\(a.EventId, b.EventId\)/);
assert.match(store, /LIMIT @take/);
assert.match(store, /ReadBoundedAsync/);
assert.match(migration, /current_input\.input_revision/);
assert.match(store, /restrictedProjector/);
assert.match(store, /DirtyAsync\(/);
assert.equal((endpoints.match(/store\.ReadBoundedAsync\(/g) || []).length, 2);
assert.ok(!endpoints.includes("inputDigest ="));
assert.match(projector, /m10_lock_projector_scope/);
assert.match(projector, /cursorOrganization = org;/);
assert.match(projector, /catch \(Exception failure\)/);
assert.match(grants, /GRANT EXECUTE ON FUNCTION m10_lock_projector_scope/);
assert.match(grants, /shipment_tracking_timeline_entries/);
assert.match(grants, /shipment_tracking_correction_heads/);
assert.match(projector, /shipment_tracking_correction_heads/);
assert.ok(!/GRANT UPDATE ON TABLE organizations|GRANT UPDATE ON TABLE shipments/.test(grants));
assert.match(authority, /CREATE OR REPLACE FUNCTION %1\$I\.m10_lock_projector_scope\(p_org uuid,p_shipment uuid\)/);
assert.match(authority, /RETURN COALESCE\(organization_state='Active' AND shipment_state='Active',false\);/);
assert.match(authority, /RETURN organization_state='Active' AND shipment_state='Active';/, "Down restores the original function body");
assert.match(authority, /REVOKE ALL ON FUNCTION m10_lock_projector_scope\(uuid,uuid\) FROM PUBLIC;/);
// The default Compose file supplies the correction signing key to the privileged migrator and the
// HTTP API only, with fail-fast interpolation and no hard-coded secret.
const defaultCompose = read("compose.yaml");
const keyLines = defaultCompose.match(/^\s+M10CorrectionProof__Key: .*$/gm) || [];
assert.equal(keyLines.length, 2, "migrator and api must each receive the correction signing key");
for (const line of keyLines)
  assert.match(line, /"\$\{HARBOROPS_M10_CORRECTION_PROOF_KEY:\?[^}]+\}"$/, "the key must use fail-fast interpolation");
for (const block of defaultCompose.split(/^  (?=[a-z]+:\r?\n)/m).filter(item => /^[a-z]+:\r?\n/.test(item))) {
  const name = block.slice(0, block.indexOf(":"));
  assert.equal(block.includes("M10CorrectionProof__Key"), ["migrator", "api"].includes(name), name + " key exposure");
}
assert.equal((defaultCompose.match(/HARBOROPS_M10_CORRECTION_PROOF_KEY/g) || []).length, 4, "the key variable appears only in the two fail-fast entries and their messages");
assert.match(authority, /CREATE FUNCTION m10_apply_signed_correction/);
assert.match(authority, /session_user <> 'harborops_api'/);
assert.match(authority, /p_signature IS DISTINCT FROM expected_signature/);
assert.match(authority, /m10_guard_correction_replay/);
assert.match(authority, /INSERT INTO public.audit_entries/);
assert.match(authority, /ERRCODE='PZ001'/);
assert.match(store, /SELECT m10_apply_signed_correction/);
assert.doesNotMatch(store, /INSERT INTO shipment_tracking_corrections/);
assert.match(signer, /HMACSHA256.HashData/);
assert.match(roles, /REVOKE INSERT ON shipment_tracking_corrections FROM harborops_api/);
assert.match(roles, /REVOKE ALL ON m10_correction_proof_keys/);
console.log("M10 source contract guard: PASS (static only)");
