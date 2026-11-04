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

const grants = read("scripts/m9-worker-role.sql");
const g9Compose = read("compose.g9.verify.yaml");
const workerCompose = read("compose.m9.worker.yaml");
const host = read("src/HarborOps.Worker/Program.cs");

test("restricted worker role reads ledger but cannot forge attempt rows", () => {
    assert.match(grants, /tracking_work_attempt_events, public\.tracking_processing_effects/);
    assert.match(grants, /REVOKE INSERT, UPDATE, DELETE ON public\.tracking_work_attempt_events/);
    assert.doesNotMatch(grants, /GRANT INSERT ON public\.tracking_work_attempt_events/);
    assert.match(workerMigration, /m9_log_work_transition/);
    assert.match(workerMigration, /SECURITY DEFINER SET search_path=pg_catalog/);
    assert.match(workerMigration, /m9_work_transition_audit AFTER UPDATE/);
});

test("verification compose is standalone with disjoint resources and real worker readiness", () => {
    assert.match(g9Compose, /^name: ho-verify-g9/m);
    assert.match(g9Compose, /127\.0\.0\.1:55439:5432/);
    assert.match(g9Compose, /127\.0\.0\.1:8089:8080/);
    assert.match(g9Compose, /127\.0\.0\.1:5179:80/);
    assert.match(g9Compose, /VITE_API_BASE_URL: "http:\/\/localhost:8089"/);
    assert.doesNotMatch(g9Compose, /127\.0\.0\.1:8080:8080/);
    assert.match(g9Compose, /172\.30\.249\.0\/24/);
    assert.match(g9Compose, /HarborOps\.Worker\.dll", "--readiness"/);
    assert.match(workerCompose, /HarborOps\.Worker\.dll", "--readiness"/);
    assert.match(host, /has_function_privilege\(current_user/);
});

test("manual redrive rechecks the session security epoch and schema pins its lock target", () => {
    assert.match(admin, /s\.security_version=u\.security_version/);
    assert.match(workerMigration, /schema_name text := current_schema\(\)/);
    assert.match(workerMigration, /%1\$I\.organizations/);
    assert.match(workerMigration, /CK_m9_effect_reference/);
});

test("development runner reports technical success and gate incompleteness separately", () => {
    const runner = read("scripts/verify-m9-local.sh");
    assert.doesNotMatch(runner, /^ {8}exit 2$/m);
    assert.match(runner, /DEVELOPMENT_STAGES=PASS/);
    assert.match(runner, /G9_ACCEPTANCE=PENDING/);
    assert.match(runner, /summarize-m9-trx\.mjs/);
    const summary = read("scripts/summarize-m9-trx.mjs");
    assert.match(summary, /notExecuted === 0/);
    assert.match(summary, /row\.failed === 0/);
});
