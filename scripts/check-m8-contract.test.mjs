import test from "node:test";
import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";

const read = path => readFileSync(path, "utf8");
const endpoint = read("src/HarborOps.Api/Integrations/TrackingEndpoints.cs");
const inbox = read("src/HarborOps.Infrastructure/Integrations/TrackingInboxStore.cs");
const migration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261103190000_TrackingInboxAndHistory.cs");
const model = read("src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs");
const snapshot = read("src/HarborOps.Infrastructure/Persistence/Migrations/HarborOpsDbContextModelSnapshot.cs");

test("M8 keeps a durable post-commit acknowledgment boundary", () => {
  for (const token of ["ReceiveAsync", "Status202Accepted", "source_event_collision",
    "receipt_unavailable", "intake_capacity_exceeded", "source_access_denied"]) {
    assert.ok(endpoint.includes(token), token);
  }
  for (const token of ["BeginTransactionAsync", "FOR UPDATE", "ReauthorizeAsync",
    "CryptographicOperations.FixedTimeEquals", "CommitAsync(ct)",
    "tracking_inbox_receipts", "tracking_accepted_events", "tracking_inbox_work_items",
    "TrackingReceiptOutcome.Received", "TrackingReceiptOutcome.Replayed"]) {
    assert.ok(inbox.includes(token), token);
  }
  assert.ok(inbox.lastIndexOf("CommitAsync(ct)") <
    inbox.lastIndexOf("TrackingReceiptOutcome.Received"),
    "Returning Received before commit is prohibited");
  assert.ok(endpoint.includes("TrackingEventPolicy.Parse"));
  assert.ok(endpoint.includes("Bind(source.Id, source.OrganizationId)"));
});

test("M8 schema enforces uniqueness, tenant scope, append-only and transaction completeness", () => {
  for (const token of [
    "AK_m8_integration_credential_scope", "UQ_m8_receipt_key",
    "FK_m8_receipt_source", "FK_m8_receipt_credential",
    "FK_m8_event_receipt", "FK_m8_work_receipt",
    "FK_m8_conflict_receipt", "CREATE CONSTRAINT TRIGGER m8_receipt_complete",
    "DEFERRABLE INITIALLY DEFERRED", "m8_guard_append_only",
    "m8_guard_work_item", "ON DELETE RESTRICT",
    "CK_m8_event_payload", "CK_m8_receipt_digest"]) {
    assert.ok(migration.includes(token), token);
  }
  assert.ok(migration.includes("DROP TABLE tracking_inbox_receipts"));
  assert.equal((migration.match(/CREATE TABLE tracking_/g) || []).length, 4);
});

test("EF model and snapshot agree on all M8 mapping definitions", () => {
  const section = source => {
    const start = source.indexOf("        modelBuilder.Entity<TrackingInboxReceipt>(entity =>");
    const end = source.indexOf("        modelBuilder.Entity<BootstrapRecord>(entity =>", start);
    assert.ok(start >= 0 && end > start);
    return source.slice(start, end);
  };
  assert.equal(section(model), section(snapshot));
  assert.ok(model.includes("DbSet<TrackingInboxReceipt>"));
  assert.ok(model.includes("DbSet<TrackingAcceptedEvent>"));
  assert.ok(model.includes("DbSet<TrackingInboxWorkItem>"));
  assert.ok(model.includes("DbSet<TrackingReceiptConflict>"));
});

test("M8 implementation has pure canonical policy and executable regression definitions", () => {
  const policy = read("src/HarborOps.Domain/Tracking/TrackingReceiptPolicy.cs");
  for (const marker of ["m8-canonical-v1", "Utf8JsonWriter", "SHA256.HashData",
    "OccurredAtUtc.UtcDateTime", "Canonicalize"]) assert.ok(policy.includes(marker));
  for (const path of [
    "tests/HarborOps.UnitTests/TrackingReceiptPolicyTests.cs",
    "tests/HarborOps.IntegrationTests/TrackingInboxHttpTests.cs",
    "tests/HarborOps.IntegrationTests/TrackingInboxSqlTests.cs",
    "tests/HarborOps.IntegrationTests/TrackingInboxMigrationTests.cs"
  ]) assert.ok(existsSync(path), path);
});

test("D-006 and G8 acceptance references preserve the original planned gate", () => {
  const decision = read("docs/M8_D006_DURABILITY_PROPOSAL.md");
  const gate = read("docs/M8_VERIFICATION_PLAN.md");
  const status = read("docs/M8_PLANNING_STATUS.md");
  assert.match(decision, /Accepted on 2026-11-04/i);
  assert.match(decision, /M8_G8_GATE_CLOSURE.md/);
  const closure = read("docs/M8_G8_GATE_CLOSURE.md");
  const review = read("docs/M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md");
  assert.match(closure, /G8 accepted/i);
  assert.match(review, /Independent evidence review PASS/i);
  assert.match(gate, /not executed/i);
  assert.match(status, /G8.*not executed/i);
  for (let i = 1; i <= 25; i++) {
    const marker = "G8-S" + String(i).padStart(2, "0");
    assert.ok(gate.includes(marker), marker);
  }
});
