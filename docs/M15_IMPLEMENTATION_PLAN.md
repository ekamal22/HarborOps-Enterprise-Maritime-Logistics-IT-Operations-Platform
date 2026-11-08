# M15 Scoped In-App Notifications: Implementation Plan

**State:** Implementation-ready sequence proposed, planning only, acceptance pending.  
**Source baseline:** main eb529368a2447061fc1838cd3967b0ffedae669d, verified 2026-11-08.  
**Prerequisite:** G11 accepted and merged. **Separate track:** M13 planning and delivery.  
**Canonical graph:** M15-01 -> M15-02 and M15-03 -> M15-04 -> G15. No graph rewrite proposed.

## 1. Scope freeze and approval prerequisites

M15 addresses FR-019 in-app scoped exception notifications and FR-002 authorization without implementing infrastructure monitoring, external transport, M13 dashboards, a new ETA, M17/M18 incident integrations or additional M9 processing. Freeze M15-P01 through P14 in M15_PLANNING_STATUS.md before runtime implementation. Agree the failure coupling of the additive M11 history trigger, membership-version policy, recipient inclusion of Auditor, retention and audit details. Unapproved policy questions keep G15-S01 blocked.

G11 already approved the M11-P01..P12/P10b rules and verifies the four source rule codes, immutable history, live selected-org session checks, recurrence and two distinct SQL roles. Do not revise these contracts or re-label exceptions as physical-vessel facts.

The current stack uses .NET 10, EF Core/PostgreSQL, Npgsql, ASP.NET protected cookie sessions and a React/Vite same-origin browser. Source names below are proposed additions, not implemented files.

## 2. Cross-track ownership before source changes

| Surface | M15 ownership | Reserved/shared boundary |
| --- | --- | --- |
| Notification domain and persistence | New src/HarborOps.Domain/Notifications and src/HarborOps.Infrastructure/Notifications modules; owned M15 tables and functions | M11 immutable history and signed evaluator stay owned by accepted M11; no edits to its original migration or functions |
| Notification API | New src/HarborOps.Api/Notifications/NotificationEndpoints.cs and service registration proposal | src/HarborOps.Api/Program.cs is shared. Integrate once against combined approved M13/M15 runtime revision |
| Permissions | Reuse current OrganizationPolicy.ViewShipmentData and current membership matrix as initial contract | Any new OrganizationPermission member is shared and requires explicit M13/security reconciliation; avoid for first M15 slice |
| Database schema | One additive M15 migration and scoped SQL grants, RLS/functions | EF DbContext, model snapshot, migration timestamps, bootstrap/provisioner shared with M13. Allocate ID only after branch ordering decision |
| Browser | New web/src/notifications/NotificationCenter.tsx, notificationClient.ts and isolated tests | web/src/auth/OrganizationWorkspace.tsx, web/src/shipments/ShipmentWorkspace.tsx, web/src/App.tsx, common styles and shared routes are integration-owned |
| Verification | M15-only unit/contract/integration/browser specs, local runner and raw evidence | G13 evidence and M13 KPI tests stay independent. Later combined-SHA regression ownership is shared |

Do not modify any M13-owned document or the M13 branch. Do not resolve an interface conflict by changing accepted G11 behavior or pushing a migration onto the other branch.

## 3. M15-01: Freeze recipient, event and duplicate contracts

1. Record explicit dispositions for event catalog, recipients, version-bound revocation, notification receipt semantics, outbox/fanout, partial failure rule, retention, audit and polling. Generate a signed-off local contract table, not an implicit approval.
2. Define the source mapping against actual immutable M11 shipment_exception_history actions and parent shipment_exceptions rule codes. Generate event fixtures for new Opened, recurrence Opened after prior AutoResolved, Manager Resolved and AutoResolved. Do not emit on ordinary Reevaluated, InputUnavailable, or repeated source evaluation.
3. Specify precise recipient SQL using organizations, users and organization_memberships with Active statuses, role parsed using accepted OrganizationPolicy, shipment ownership and permitted M11 read semantics. Capture membership row ID and version on delivery.
4. Lock unique keys: source history UUID for one outbox intent, (outbox ID, recipient user ID) for one personal row, and (org, recipient, replay key) for a personal mutation. Decide stale conflict behavior, same-key reply, actor/session binding and expiry.
5. Approve bound text templates per rule type and event type. Never embed historical M11 free-form reasons, raw evidence payloads, sensitive actor fields or unverified physical movement claims.
6. Produce implementation-owned API schemas and DB threat model, including the distinction between in-app personal Acknowledge and M11 Manager Acknowledge. Produce two-tenant and five-role fixtures with version changes, archived shipments and revoked sessions.

**Exit artifacts:** M15 local policy decision record, event/recipient truth table, DTO and error examples, SQL grants/RLS design, concurrency diagram and ownership signoff. G15-S01 still needs explicit approval.

## 4. M15-02: Persistence and durable fanout

Implementation order:

1. After M13 migration reservation coordination, add a new additive migration with m15_notification_outbox, m15_user_notifications and m15_notification_replays, keys, indexes and controlled SQL functions. Do not edit historical migrations, accepted M11 history tables or migration IDs. If EF maps notification rows, add models and snapshot changes only in a coordinated integration branch. If using SQL-only restricted functions, keep EF snapshot unchanged and explicitly test the choice.
2. Add a narrow AFTER INSERT trigger on M11 immutable history, inserting only allowlisted transition types and only small source identifiers. The history entry and outbox intent must commit or roll back together. Demonstrate the exact M11 transaction rollback outcome on deliberate trigger failure before approving this coupling. Preserve M11 locked source/tenant semantics and deadlock ordering.
3. Add notification fanout under a bounded PostgreSQL lease, SKIP LOCKED and fenced lease epoch. Acquire current user/membership/organization eligibility and persist all delivery rows plus intent completion in one transaction. Use unique delivery keys and predictable retries to handle duplicate dispatch, two simultaneous instances, restart and post-commit acknowledgment uncertainty. Expired leases must be claimable safely.
4. Keep dispatcher disabled by default in the planning implementation configuration. A later authorized rollout turns it on after migration, proof key provisioning and G15 checks. It is separate from the M9 worker, M10 projector and M11 evaluator; none may be automatically enabled as a side effect.
5. Implement fixed, scoped read/count/detail and mutation database functions or equivalently verified RLS. Harden grants through src/HarborOps.Api/Audit/AuditRoleProvisioner.cs because that provisioner currently grants SELECT on ALL TABLES to harborops_api before protected exceptions. Explicitly deny general API/worker access to notification outbox, replay, verifier and other recipients' rows. Recheck full session, selected org and membership inside the effective PostgreSQL operation.
6. Decide and provision an independent M15 scoped-proof key at migration time, restricted from DB-only impersonators, absent from web, worker, repository and evidence. Missing/mismatch/rotation behavior fails closed. No production key is made under this plan.
7. Add bounded retention/cleanup functions disabled until retention decision approval, proving that M11/M16 records cannot be touched. Do not implement hidden global deletion.

**Implementation candidate paths:** src/HarborOps.Domain/Notifications/NotificationPolicy.cs, src/HarborOps.Infrastructure/Notifications/NotificationStore.cs, src/HarborOps.Infrastructure/Notifications/NotificationDeliveryService.cs, src/HarborOps.Api/Notifications/NotificationEndpoints.cs, one coordinated additive migration in src/HarborOps.Infrastructure/Persistence/Migrations and focused additions to the role provisioner and composition root.

**Exit artifacts:** migration upgrade and rollback strategy, grants matrix, lease/replay guarantees, backend API contracts, failure recovery runbook, source-change impact report. No success can be claimed until G15 runs.

## 5. M15-03: User-facing notification workspace

1. Add isolated typed notificationClient.ts under web/src/notifications using the existing auth/organizationClient same-origin credential and CSRF helpers. GET feed/count/detail and PATCH MarkRead/Acknowledge must use no-store and handle abortable requests.
2. Build NotificationCenter.tsx with a feed, unread count, per-person read/ack state, honest M11 evidence labels, filter, keyset continuation, reload, retry, and clear denial states. Hide for unsupported roles as a UI convenience, not a security boundary.
3. Implement race guards so an old response from an earlier organization/session never replaces current protected data. Cancel outstanding list/detail/mutation/count calls on switch and unmount; close or reset the center on access change.
4. Define the parent-owned navigation callback with payload { shipmentId, exceptionId }, bound to selected org. A click must refetch authorized shipment and M11 detail through existing APIs, then select the exact item inside ShipmentWorkspace. If archived/unavailable, render a safe nonlink state and do not infer its existence from stale notification contents.
5. Agree with M13 the exact workspace mount point. M15 owns the notification center itself, but no independent edit to the shared OrganizationWorkspace, App, shared route state, styling contract or dashboard indicators may be merged before interface reconciliation.
6. Add unit/component-style tests and three-engine Playwright tests in tests/browser/specs/notifications.spec.mjs, using real same-origin role/session fixtures. No dedicated accessibility audit here; M24-04 owns that final audit.

**Exit artifacts:** tested standalone center, parent integration contract, role-switch/access-loss navigation cases, typed request/response examples and browser fixture plan.

## 6. M15-04: Recipient isolation, correctness and resilience

Execute G15-S01 through G15-S24 only after M15-01 decisions and M15-02/M15-03 implementation are complete. Required classes to add, subject to final project naming:

- tests/HarborOps.UnitTests/NotificationPolicyTests.cs for event mapping, receipt transitions, keys, limits and no-ETA text.
- tests/HarborOps.ContractTests/NotificationContractTests.cs for route/DTO, errors, headers, cookie/CSRF, cursor and rate policy.
- tests/HarborOps.IntegrationTests/NotificationSqlBoundaryTests.cs for restricted API/worker login, privilege, RLS/proof and tenant/recipient isolation.
- tests/HarborOps.IntegrationTests/NotificationDeliveryRuntimeTests.cs for PostgreSQL fanout, M11 trigger, replay, membership changes, repeatable migrations and failure recovery.
- tests/HarborOps.IntegrationTests/NotificationHttpTests.cs for actual M1/M2 roles, live membership/session changes, resource links and update conflicts.
- tests/browser/specs/notifications.spec.mjs for three browser engines and stale-response/revocation behaviors.
- scripts/verify-g15-local.sh for isolated E:-backed source-locked evidence, no GitHub Actions dispatch.

Run targeted checks while developing, then freeze one final combined M15 implementation SHA and execute the required G15 gate once, with affected accepted-milestone regressions and focused security mutants. Preserve failures, skipped cases and rerun provenance. G15-S25 must be an independent review of manifest and raw artifacts, not a developer self-certification. Separate explicit G15 acceptance is required after S01-S25 reconciliation.

## 7. Migration, rollout and compatibility checkpoints

- Fresh disposable database must apply the full accepted M0-M12/M16 schema then the newly coordinated M15 migration.
- Populated historical upgrade must preserve existing M11 exceptions, immutable history, M12 export audit and M16 audit records, and should prove that adding M15 does not change old outputs before a new qualifying history action. Existing accepted migration contents/hashes must be unchanged.
- M13 and M15 may both introduce EF mappings or SQL-only migrations. Do not pick a filename by current date alone. Compare pending M13 plan, reserve order, verify a single deterministic chain and regenerate snapshot only on the integration owner after both source trees are known.
- Schema installation, runtime dispatcher enablement and UI exposure must be separable and reversible under a later authorized rollout; preserve pending intents on restart. A data-bearing downgrade requires an explicit safe policy and must never silently destroy delivered records or accepted M11 evidence.
- The API/runtime uses existing protected cookies and live selected-organization scope. Key mismatch, unavailable audit and database outage result in safe errors; no retry may bypass current authorization.
- Capture query plans and measured latency at realistic data volume. The current main baseline has known M12 large-tenant sequential search behavior; do not claim a benchmark for M15 from M12 figures.
- G15 verification may run on exclusive disposable PostgreSQL servers where role provisioning changes cluster-wide role passwords. Never touch shared or retained servers or volumes. All persistent worktrees, caches, DB/Docker and evidence belong on E:.

## 8. M13 compatibility checklist and integration conflicts

Before either workstream implements shared changes, reconcile:

1. Namespaces and ownership of /api/v1/organizations/{organizationId}/notifications versus M13 KPI routes. Neither route may use the other's storage or permission as authority.
2. Shell component slot and parent navigation callback shape, including whether dashboard owns a badge. M13 dashboard notification indicators require a separate approved consumer contract.
3. Role/permission decisions. Initial M15 proposal reuses ViewShipmentData; M13 may choose independent ViewDashboard permissions. Do not add a new shared enum value without a combined review.
4. EF DbContext/snapshot and additive migration ordering, including shared AuditRoleProvisioner role grants and PostgreSQL function namespaces.
5. src/HarborOps.Api/Program.cs DI registration and hosted service flags, shared web CSS and organization selection/logout invalidation.
6. Retention and aggregation. Notification delivery/read state must never define M13 operational throughput, delays, incidents or exception counts.
7. Combined-source checks: merge candidate diff, app boot, migrations, five-role permissions, M11 notification trigger and M13 KPI query parity, concurrent org switch and independently identifiable G13 and G15 evidence on their final SHAs.

An integration branch, merge, deployment or shared-source modification requires a separate instruction. A planning PR is not that authorization.

## 9. Stop condition

Planning may be published only as M15-specific Markdown documentation. Do not create implementation files, run migrations/verification, touch governance files, edit M13 documents, dispatch workflows, merge, deploy or mark G15 accepted. Implementation begins only after approval of outstanding M15-P policies and explicit implementation authorization.
