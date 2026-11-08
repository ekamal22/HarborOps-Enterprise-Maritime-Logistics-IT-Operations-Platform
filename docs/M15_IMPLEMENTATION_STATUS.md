# M15 Scoped In-App Notifications: Source Implementation Status

**Checkpoint:** 2026-11-08  
**Source branch:** `feat/m15-scoped-notifications-20261108`  
**Planning baseline:** accepted `main` `eb529368a2447061fc1838cd3967b0ffedae669d`, incorporating the M15 planning documents through `6d62c7287307cc702719e1eb40bd5fd88cb9b61c`.  
**Status:** Source implementation candidate created, compilation/runtime verification not executed, policy approval pending, G15 NOT EXECUTED and NOT ACCEPTED. Not merged or deployed.

## Source deliveries

| Slice | Current feature-branch source | Observed status |
| --- | --- | --- |
| M15-01 event/recipient rules | `docs/M15_ARCHITECTURE_DESIGN.md`, `NotificationText`, schema constraints and recipient role filter | Proposed defaults encoded; M15-P01..P14 awaiting approval |
| M15-02 persistence | `20261109010000_M15ScopedNotifications.cs`, `NotificationStore.cs`, `AuditRoleProvisioner.cs` | Additive SQL-only migration candidate, scoped API-role grants, signed M11 read-scope reuse, atomic outbox and receipt writes |
| M15-02 delivery | `NotificationDeliveryService.cs`, `Program.cs` | Separate API-hosted dispatcher, opt-in with `M15:EnableDelivery`; default off |
| M15-03 endpoints | `NotificationEndpoints.cs`, `Program.cs` | GET list/unread-count/detail, PATCH MarkRead/Acknowledge, live organization guard, CSRF, replay key and protected paging |
| M15-03 React | `web/src/notifications/notificationClient.ts`, `NotificationCenter.tsx`, `OrganizationWorkspace.tsx`, `ShipmentWorkspace.tsx`, `ShipmentExceptionPanel.tsx` | Notification center integrated with selected organization and authorized shipment/exception refetch navigation |
| M15-04 verification source | `NotificationTextTests.cs`, `M15NotificationRuntimeTests.cs`, `notifications.spec.mjs`, `scripts/check-m15-contract.mjs` | Test and invariant source authored only, **not executed**; G15-S01..S25 remain open |
| Cross-milestone compatibility | MigrationSnapshotTests, M10/M11/M12 and several accepted historical-upgrade integration test sources | Current-HEAD migration count updated to 16 without changing historical checkpoint counts |

## Implementation decisions currently encoded but not approved

- Only immutable M11 Opened, Resolved and AutoResolved history rows write M15 outbox intents. Normal Reevaluated, InputUnavailable and Acknowledged rows do not fan out.
- Delivery recipient membership is resolved from current Active organization/user/membership at fanout. Eligible roles are Analyst, Manager and Auditor. Administrator and Support have no implicit shipment-notification access.
- Each delivery snapshots membership ID/version; source event UUID plus user ID is unique. Read/count/detail/mutation use live M11-signed session read scope and a separate M15 row-level entitlement function.
- MarkRead is a personal state transition with no central audit. Acknowledge is a personal state transition with one mandatory transaction-coupled M16 `notification.receipt_acknowledged` audit event. Neither changes M11 exception state.
- The outbox trigger is in the same transaction as M11 immutable history; trigger failure rolls back the M11 change. No out-of-transaction notification loss is silently accepted.
- Notification titles use fixed, evidence-safe rule vocabulary. The frontend does not claim physical movement, predictive ETA or user-disposition reason.
- Dispatcher is opt-in and independent. It uses bounded transaction-scoped SKIP LOCKED rows, not an external queue or long-lived lease. This is a **source divergence from the earlier proposed fenced-lease dispatcher**, requiring policy reconciliation before implementation acceptance. A crash rolls back the unfinished batch and a committed batch is unique by source history/recipient.
- No retention delete job is enabled or implemented while M15-P12 remains pending. The provisional migration identifier must be coordinated with M13 before combined integration.
- The read-scope key reuses the accepted M10/M11 session-bound signed read authority rather than introducing an unapproved separate M15 secret. This is a **source divergence from the architecture proposal**; a targeted security review and explicit decision are required.
- The current branch does not enable the M9 ARRI/DEPA worker, M10 projector or M11 evaluator. The M9 10,000 unresolved-work cap and null ETA remain unchanged.

## Correctness and security work remaining

1. **Compile and run** source-contract checks, `.NET 10` warnings-as-errors build, frontend typecheck/build and new unit tests on a source-locked revision. This execution environment cannot clone the repository or run `dotnet`; no command result has been inferred.
2. **Run real PostgreSQL** fresh/populated migrations, restricted `harborops_api` and `harborops_worker` grants/RLS tests, SQL signed-scope forgery attempts, role/session revocation interleavings, collision/idempotency and same-transaction M16 audit rollback.
3. **Exercise fanout failure and performance**: concurrent instances, source-to-intent trigger failures, DB outage, cancellation, transaction restart, foreign membership, archived target, recipient counts and the measured EXPLAIN plan. Bound and monitor large-recipient fanout before rollout.
4. **Run browser tests** in Chromium, Firefox and WebKit with separate two-tenant, five-role fixtures; confirm async org switches, stale response disposal, authorized shipment/exception deep-link and CSRF updates.
5. **Reconcile M13** API/shell integration, identity permission changes, SQL role provisioner changes, EF snapshot and additive migration order against the independent M13 branch. No M13 owned code or documents were changed here.
6. **Resolve policies and complete G15**. All G15-S01 through S25 require source-locked evidence, independent S25 review and a separate explicit gate acceptance. Authored tests or code are not PASS evidence.

## Repository rules and release limits

All feature-branch commits must contain `[skip ci]`. No GitHub Actions usage, production database migration, merge, release, deployment, or protected-branch change is authorized by this checkpoint. The living docs note the M15 source candidate without changing accepted G0-G12/G16 status or the canonical execution graph. M13 policy and dashboard results remain separate from notification delivery state.
