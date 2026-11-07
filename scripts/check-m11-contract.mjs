import assert from "node:assert/strict";
import fs from "node:fs";
import { execFileSync } from "node:child_process";

const read = path => fs.readFileSync(new URL("../" + path, import.meta.url), "utf8");
const migration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261107180000_ShipmentExceptions.cs");
const policy = read("src/HarborOps.Domain/Exceptions/ShipmentExceptionPolicy.cs");
const store = read("src/HarborOps.Infrastructure/Exceptions/ShipmentExceptionStore.cs");
const acceptedM10 = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261105180000_ShipmentTrackingProjections.cs");
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
assert.match(acceptedM10, /CREATE TABLE shipment_tracking_input_versions/);
assert.match(acceptedM10, /input_revision bigint NOT NULL/);
assert.match(migration, /SELECT input_revision FROM shipment_tracking_input_versions/);
assert.match(store, /v\.input_revision/);
assert.ok(!/\bv\.revision\b/.test(migration + store),
  "Accepted M10 input version column is input_revision");
assert.match(migration, /ix_m11_nonce_expiry/);
assert.match(migration, /LIMIT 64 FOR UPDATE SKIP LOCKED/);
assert.match(migration, /rule_policy_version/);
assert.match(migration, /IF stored_condition=p_condition AND previous_fp=p_fingerprint THEN[\s\S]*?evaluated_at_utc=clock_timestamp\(\)/);
assert.match(migration, /Exception reevaluation required/);
assert.match(migration, /Equipment reevaluation required/);
assert.match(migration, /p\.refreshed_at_utc <= e\.evaluated_at_utc/);
assert.match(policy, /AddClamped/);
assert.match(policy, /StrictlyAfter/);
assert.match(store, /DatabaseNowAsync/);
assert.ok(!proof.includes("DateTimeOffset.UtcNow"), "Proofs must use database UTC time");
assert.match(grants, /shipment_exception_replays FROM harborops_api/);
assert.match(migration, /p_signature IS NULL/);
assert.match(migration, /p_rule IS NULL OR p_condition IS NULL/);
assert.match(migration, /p_action IS NULL OR p_expected_version IS NULL/);
assert.match(store, /WHEN 'EquipmentConflict' THEN 1[\s\S]*WHEN 'RecordedArrivalLate' THEN 2[\s\S]*WHEN 'ArrivalScheduleOverdue' THEN 3/);
assert.match(store, /ORDER BY rule_priority,id LIMIT @limit/);
assert.match(store, /IsEvaluationRace/);
assert.match(store, /shipment_exception_evaluations_pkey/);
assert.match(store, /ex.SqlState is "PZ004" or "23505" or "40001" or "40P01"/);
assert.match(migration, /PRIMARY KEY \(organization_id,idempotency_key\)/);
assert.match(migration, /existing_state='Open' OR p_reason='shipment_not_eligible'/);
assert.match(policy, /input.WorkflowStatus == "InTransit"/);
assert.match(store, /candidate AS MATERIALIZED/);
assert.match(store, /LastOrganization/);
assert.match(store, /DateTimeOffset SnapshotAtUtc/);
assert.match(store, /first_detected_at_utc<=@snapshot::timestamptz/);
assert.match(store, /HistoryPageAsync/);
assert.match(store, /ORDER BY occurred_at_utc,id LIMIT @limit/);
assert.match(endpoints, /DateTimeOffset SnapshotAtUtc/);
assert.match(endpoints, /HistoryCursor/);
assert.match(endpoints, /\/exceptions\/\{exceptionId\}\/history/);
assert.match(web, /Load more history/);
const m11RuntimeTests = read("tests/HarborOps.IntegrationTests/M11ExceptionRuntimeTests.cs");
assert.match(m11RuntimeTests, /LongImmutableHistoryCanBePagedWithRoleRedaction/);
assert.match(m11RuntimeTests, /database-clock creation-cutoff window/);

assert.match(grants, /REVOKE ALL ON shipment_exception_proof_nonces/);
assert.ok(!/GRANT (INSERT|UPDATE|DELETE) ON shipment_exception/i.test(grants));
assert.match(migration, /audit_entries/);
assert.match(proof, /m11-eval-v2/);
assert.match(migration, /m11-eval-v2/);
assert.match(migration, /extract\(epoch from p_reference\)\*1000000/);
assert.match(proof, /Microseconds\(signal\.ReferenceAtUtc\)/);
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
