import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");
const workerMigration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261104190000_TrackingWorker.cs");
const priorMigration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261103190000_TrackingInboxAndHistory.cs");
const worker = read("src/HarborOps.Infrastructure/Integrations/TrackingWorkStore.cs");
const admin = read("src/HarborOps.Infrastructure/Integrations/TrackingWorkAdminStore.cs");
const inbox = read("src/HarborOps.Infrastructure/Integrations/TrackingInboxStore.cs");
const server = read("src/HarborOps.Api/Integrations/TrackingWorkEndpoints.cs");

test("accepted M8 migrations remain present and untouched by M9 layering", () => {
    for (const piece of ["m8_guard_append_only", "m8_assert_complete_receipt",
        "m8_guard_work_item", "UQ_m8_receipt_key"]) {
        assert.ok(priorMigration.includes(piece), piece);
    }
    assert.match(workerMigration, /ALTER TABLE tracking_inbox_work_items/);
    assert.match(workerMigration, /DROP TRIGGER m8_work_guard/);
    assert.doesNotMatch(workerMigration, /DROP TABLE tracking_inbox_receipts/);
    assert.doesNotMatch(workerMigration, /DROP TABLE tracking_accepted_events/);
});

test("M9 only permits idempotent, fenced, finite worker outcomes", () => {
    for (const symbol of ["tracking_work_attempt_events", "tracking_processing_effects",
        "m9_guard_work_item", "DEFERRABLE INITIALLY DEFERRED", "m9_effect_terminal",
        "UQ_m9_redrive_generation", "m9_work_effect_required",
        "m9_validate_effect_reference", "m9_lock_active_organization"]) {
        assert.ok(workerMigration.includes(symbol), symbol);
    }
    for (const symbol of ["FOR UPDATE SKIP LOCKED", "lease_token=@token",
        "lease_epoch=@epoch", "lease_expires_at_utc>clock_timestamp()",
        "tracking_processing_effects", "NeedsReconciliation", "DeadLetter",
        "TrackingWorkPolicy.RetryDelay"]) {
        assert.ok(worker.includes(symbol), symbol);
    }
});

test("ingress counts all unresolved worker states and keeps replay before capacity", () => {
    assert.match(inbox, /state IN \('Pending','Leased','RetryWaiting','NeedsReconciliation'\)/);
    assert.ok(inbox.indexOf("if (originalCanonical is not null)") <
        inbox.indexOf("SELECT count(*) FROM tracking_inbox_work_items"));
});

test("operator queue requires live scope, authenticated CSRF and attributed redrive", () => {
    assert.match(server, /RequireAuthorization\(\)/);
    assert.match(server, /TrackingWorkRedrive/);
    assert.match(server, /ValidMutationAsync/);
    assert.match(admin, /m\.role='Manager'/);
    assert.match(admin, /FOR SHARE OF s,m,u/);
    assert.match(admin, /actor_user_id,redrive_reason/);
    assert.doesNotMatch(server, /raw_payload_utf8|canonical_payload_utf8|secret_digest/);
});
