import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
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

assert.match(read("scripts/prepare-m15-browser-fixtures.mjs"),
  /M15 live scoped receipt and private browser authentication prepared: PASS/);
assert.match(read("scripts/prepare-m15-browser-fixtures.mjs"),/storageState\(\)/);
assert.match(read("tests/browser/specs/notifications.spec.mjs"),
  /page\.context\(\)\.addCookies\(state\.cookies\)/);
assert.match(read("tests/browser/specs/notifications.spec.mjs"),/G15 live M11 event is delivered/);

assert.match(react,/pagingAbort\.current\?\.abort\(\)/);
assert.match(react,/pagingAbort\.current === controller/);
assert.match(read("tests/browser/specs/notifications.spec.mjs"),
  /G15 a cancelled pagination result cannot leave Load more disabled/);
assert.match(read("tests/browser/specs/notifications.spec.mjs"),
  /center\.locator\("li p"\)\.filter\(\{hasText:/);

const browserSpec = read("tests/browser/specs/notifications.spec.mjs");
// Faked scenarios must intercept first-paint reads before loading the workspace.
// A real hosted M11 to M15 browser check must never use that fallback.
assert.match(browserSpec,/test\.beforeEach\(async \(\{ page \}, info\) =>/);
assert.match(browserSpec,/if \(info\.title\.includes\("G15 live M11 event is delivered"\)\) return/);
assert.match(browserSpec,/page\.route\("\*\*\/organizations\/\*\/notifications\?\*"/);
assert.match(browserSpec,/page\.on\("response"/);
assert.match(browserSpec,/liveHttpStatuses\.push\(response\.status\(\)\)/);
assert.ok(
  browserSpec.indexOf('test("G15 live M11 event is delivered') <
  browserSpec.indexOf('test("G15 scoped notification center displays'),
  "real browser verification must run before mocked scenarios"
);

// Delivery remains off in application defaults and ordinary CI workflows.
// The dedicated, source-locked verification workflow may enable it only in
// its disposable browser fixture, which owns and destroys its own stack.
// Only these exact source-locked workflows may enable delivery, and only in
// a disposable browser job with automatic teardown.
const disposableBrowserBranches = new Map([
  ["verify-g13-g15-post-c3.yml", "feature/g13-post-c3-remediation-20261111"],
  ["verify-g15-post-g13.yml", "integration/g15-post-g13-remediation-20261112"],
  ["verify-g15-s25-remediation.yml", "feature/g15-s25-complete-remediation-20261112"],
]);
for (const name of readdirSync(resolve(root,".github/workflows"))) {
  const workflow = read(".github/workflows/" + name);
  const expectedBranch = disposableBrowserBranches.get(name);
  if (expectedBranch === undefined) {
    assert.doesNotMatch(workflow, /HARBOROPS_M15_ENABLE_DELIVERY=true/,
      name + " must not enable notification delivery");
    continue;
  }
  const start = workflow.indexOf("\n  m15-browser:\n");
  const end = workflow.indexOf("\n  scale:\n", start);
  assert.ok(start !== -1 && end > start,
    "Disposable browser job must have an isolated source-locked definition");
  const browser = workflow.slice(start, end);
  const otherJobs = workflow.slice(0, start) + workflow.slice(end);
  assert.match(browser, /HARBOROPS_M15_ENABLE_DELIVERY=true/);
  assert.doesNotMatch(otherJobs, /HARBOROPS_M15_ENABLE_DELIVERY=true/,
    "Only the disposable browser job may enable notification delivery");
  for (const required of [
    "HARBOROPS_M15_LIVE_FIXTURE=1",
    'project="ho-verify-m15-${GITHUB_RUN_ID}"',
    'bash scripts/smoke-compose.sh --project "$project" --confirm-disposable',
    'docker compose -p "$project" down --volumes --remove-orphans',
    "trap cleanup EXIT",
    "persist-credentials: false",
  ]) {
    assert.ok(browser.includes(required),
      "Disposable browser job lacks isolation guard: " + required);
  }
  assert.ok(
    workflow.includes("on:\n  push:\n    branches:\n      - " + expectedBranch + "\n"),
    "Disposable browser workflow must run only on its approved source branch"
  );
  assert.match(workflow, /permissions:\n  contents: read/);
  assert.match(browser, /needs: source/,
    "Live browser verification must depend on the successful preflight");
}
