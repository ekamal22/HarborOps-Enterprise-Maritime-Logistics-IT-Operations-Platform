import assert from "node:assert/strict";
import fs from "node:fs";
import { execFileSync } from "node:child_process";

const read = path => fs.readFileSync(new URL("../" + path, import.meta.url), "utf8");
const migration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261107180000_ShipmentExceptions.cs");
const policy = read("src/HarborOps.Domain/Exceptions/ShipmentExceptionPolicy.cs");
const store = read("src/HarborOps.Infrastructure/Exceptions/ShipmentExceptionStore.cs");
const proof = read("src/HarborOps.Infrastructure/Exceptions/M11ProofSigner.cs");
const endpoints = read("src/HarborOps.Api/Exceptions/ShipmentExceptionEndpoints.cs");
const grants = read("src/HarborOps.Api/Audit/AuditRoleProvisioner.cs");
const web = read("web/src/shipments/ShipmentExceptionPanel.tsx");
const program = read("src/HarborOps.Api/Program.cs");
const snapshot = read("tests/HarborOps.IntegrationTests/MigrationSnapshotTests.cs");

assert.match(policy, /m11-rules-v1/);
for (const rule of ["EquipmentConflict", "ArrivalScheduleOverdue",
                    "RecordedArrivalLate", "DepartureSlippageRisk"]) {
  assert.ok(policy.includes(rule) && migration.includes(rule), "missing M11 rule " + rule);
}
assert.match(migration, /20261107180000_ShipmentExceptions/);
assert.match(snapshot, /20261107180000_ShipmentExceptions/);
for (const table of ["shipment_exceptions", "shipment_exception_history",
                     "shipment_exception_evaluations", "shipment_exception_replays",
                     "shipment_exception_proof_nonces"]) {
  assert.ok(migration.includes("CREATE TABLE " + table), "missing M11 table " + table);
}
for (const fn of ["m11_apply_signed_evaluation", "m11_apply_signed_action"]) {
  assert.ok(migration.includes("CREATE FUNCTION " + fn), "missing SQL function " + fn);
  assert.ok(grants.includes("GRANT EXECUTE ON FUNCTION " + fn),
    "missing guarded role provision for " + fn);
}
assert.match(migration, /SECURITY DEFINER/);
assert.match(migration, /m11_schema/);
assert.match(migration, /SELECT input_revision FROM shipment_tracking_input_versions/);
assert.match(store, /v\.input_revision/);
assert.ok(!/\bv\.revision\b/.test(migration + store),
  "Accepted M10 input version column is input_revision");
assert.match(migration, /ix_m11_nonce_expiry/);
assert.match(migration, /LIMIT 64 FOR UPDATE SKIP LOCKED/);
assert.match(migration, /rule_policy_version/);
assert.match(store, /candidate AS MATERIALIZED/);
assert.match(store, /LastOrganization/);
assert.match(grants, /REVOKE ALL ON shipment_exception_proof_nonces/);
assert.ok(!/GRANT (INSERT|UPDATE|DELETE) ON shipment_exception/i.test(grants));
assert.match(migration, /audit_entries/);
assert.match(proof, /m11-eval-v1/);
assert.match(proof, /m11-action-v1/);
assert.match(store, /IsolationLevel.Serializable/);
assert.match(endpoints, /OrganizationRole.Manager/);
assert.match(endpoints, /ValidMutationAsync/);
assert.match(endpoints, /ITimeLimitedDataProtector/);
assert.match(web, /no ETA is calculated/);
assert.match(program, /M11:EnableEvaluator/);
const diff = execFileSync("git", ["diff", "--name-only",
  "6ead1d076946dd96b53401265a31efc35c4a666c...HEAD"],
  { encoding: "utf8" }).trim().split("\n").filter(Boolean);
assert.ok(!diff.some(x => x.startsWith("src/HarborOps.Worker/")));
assert.ok(!diff.some(x => /20261107120000_M10CorrectionAuthority|20261105180000_ShipmentTrackingProjections/.test(x)));
console.log("M11 source-contract assertions passed. Runtime verification is separate.");
