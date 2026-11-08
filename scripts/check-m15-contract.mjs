import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = path => readFileSync(resolve(root,path),"utf8");
const migration = read("src/HarborOps.Infrastructure/Persistence/Migrations/20261109010000_M15ScopedNotifications.cs");
const store = read("src/HarborOps.Infrastructure/Notifications/NotificationStore.cs");
const api = read("src/HarborOps.Api/Notifications/NotificationEndpoints.cs");
const delivery = read("src/HarborOps.Api/Notifications/NotificationDeliveryService.cs");
const program = read("src/HarborOps.Api/Program.cs");
const grants = read("src/HarborOps.Api/Audit/AuditRoleProvisioner.cs");
const react = read("web/src/notifications/NotificationCenter.tsx");
const workspace = read("web/src/auth/OrganizationWorkspace.tsx");
const tests = read("tests/HarborOps.IntegrationTests/M15NotificationRuntimeTests.cs");
const gate = read("docs/M15_VERIFICATION_PLAN.md");
const status = read("docs/M15_PLANNING_STATUS.md");

for (const action of ["Opened","Resolved","AutoResolved"])
  assert.ok(migration.includes(action),"missing qualifying M11 event "+action);
for (const table of ["m15_notification_outbox","m15_user_notifications","m15_notification_replays"])
  assert.ok(migration.includes("CREATE TABLE "+table),"missing storage "+table);
for (const func of ["m15_capture_exception_history","m15_deliver_pending",
  "m15_can_read","m15_apply_personal_action"])
  assert.ok(migration.includes("CREATE FUNCTION "+func),"missing SQL boundary "+func);
assert.match(migration,/AFTER INSERT ON shipment_exception_history/);
assert.match(migration,/FOR UPDATE SKIP LOCKED/);
assert.match(migration,/PRIMARY KEY\(organization_id,recipient_user_id,idempotency_key\)/);
assert.match(migration,/uq_m15_event_recipient UNIQUE\(source_history_id,recipient_user_id\)/);
assert.match(migration,/CREATE POLICY m15_personal_read/);
assert.match(migration,/m11_read_allowed\(p_org\)/);
assert.match(migration,/m15_dispatch_allowed\(\)/);
assert.match(migration,/m15-dispatch-v1/);
assert.match(migration,/attempt_count integer NOT NULL/);
assert.match(migration,/quarantined_at_utc/);
assert.match(migration,/recipient_limit/);
assert.match(migration,/FOR SHARE OF a,u/);
assert.match(migration,/FOR SHARE;/);
assert.match(migration,/notification\.receipt_acknowledged/);
assert.match(migration,/result_read_at_utc timestamptz NOT NULL/);
assert.match(migration,/result_acknowledged_at_utc timestamptz/);
assert.match(migration,/old_replay\.result_read_at_utc,old_replay\.result_acknowledged_at_utc/);
assert.match(migration,/next_read_at,next_acknowledged_at/);
assert.match(store,/result_read_at_utc,/);
assert.match(api,/ReadAtUtc = result\.ReadAtUtc/);
assert.match(read("tests/HarborOps.IntegrationTests/M15NotificationRuntimeTests.cs"),
  /lateReplay\.Content\.ReadFromJsonAsync/);
assert.match(migration,/M15 downgrade would destroy delivery evidence/);
assert.match(grants,/REVOKE ALL ON m15_notification_outbox/);
assert.match(grants,/GRANT SELECT ON m15_user_notifications TO harborops_api/);
assert.match(grants,/GRANT EXECUTE ON FUNCTION m15_apply_personal_action/);
assert.match(store,/m11-read-v1/);
assert.match(store,/set_config\('harborops\.m11_read_scope'/);
assert.match(api,/ValidMutationAsync/);
assert.match(api,/Idempotency-Key/);
assert.match(api,/NotificationCursor\.v1/);
assert.match(api,/organization_selection_required/);
assert.match(program,/M15:EnableDelivery/);
assert.ok(!program.includes('GetValue<bool>("M15:EnableDelivery", true)'),
  "notification dispatcher must default off");
assert.match(program,/MapNotificationEndpoints/);
assert.match(delivery,/NotificationStore/);
assert.match(react,/Acknowledge notification/);
assert.match(workspace,/NotificationCenter/);
assert.match(tests,/RestrictedSqlLoginCannotEnumerateOutbox/);
for (let number=1;number<=25;number++)
  assert.ok(gate.includes("G15-S"+String(number).padStart(2,"0")),
    "missing mandatory G15 scenario "+number);
assert.match(status,/G15/);
console.log("M15 source-contract source checks passed. Runtime verification is separate.");

assert.match(read("compose.yaml"),/M15__EnableDelivery:.*HARBOROPS_M15_ENABLE_DELIVERY:-false/);
assert.match(read(".env.example"),/HARBOROPS_M15_ENABLE_DELIVERY=false/);
assert.match(read(".github/workflows/verify-m11-m12-combined.yml"),/FullyQualifiedName~M15NotificationBoundaryTests/);
