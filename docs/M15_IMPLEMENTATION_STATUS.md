# M15 Scoped In-App Notifications: Implementation Status

**Current checkpoint:** 2026-11-09 (Türkiye local date).  
**Implementation branch:** `feat/m15-scoped-notifications-20261108`  
**Integration PR:** [Draft PR #37](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/37), not merged.  
**Milestone disposition:** Source implementation candidate. On frozen `b1e79c8`, source and PostgreSQL passed, live M11-to-M15 delivery passed, and Chromium passed 5/6 tests. One receipt-transition test timed out due to a UI mutation-lock defect corrected in later source. Firefox and WebKit not reached. G15 acceptance pending.

The authoritative executed test record, including the failed runs and immutable evidence links, is [M15_EXECUTED_VERIFICATION_20261109.md](M15_EXECUTED_VERIFICATION_20261109.md). It must be read alongside this status. Do not treat test source, corrected workflows or earlier run results as an accepted G15 gate.

## Implemented source work packages

| Package | Files and behavior | Disposition |
| --- | --- | --- |
| M15-01 | M11 immutable-history events Opened, Resolved and AutoResolved only. Fixed evidence-safe rule text, explicit role/recipient matrix, personal receipt lifecycle | Implemented as policy defaults. P01-P14 acceptance pending |
| M15-02 persistence | SQL-only `20261109010000_M15ScopedNotifications`, `NotificationStore.cs`, `AuditRoleProvisioner.cs` | Immutable outbox trigger, version-bound recipient rows, RLS and restricted SQL grants; additive migration upgrade and rollback exercised on previous source |
| M15-02 fanout | `NotificationDeliveryService.cs`, signed `m15_dispatch_allowed` proof, bounded SKIP LOCKED batches, sanitized retry/backoff and quarantine counters | Implemented. Dispatcher defaults off through `M15:EnableDelivery` |
| M15-03 API | `NotificationEndpoints.cs`, `Program.cs` | Personal list, protected 20-minute paging cursor, unread count, detail and CSRF-protected PATCH; current selected-org session and membership checks |
| M15-03 UI | `web/src/notifications/*`, `OrganizationWorkspace.tsx`, `ShipmentWorkspace.tsx`, `ShipmentExceptionPanel.tsx` | Feed, count, filters, receipt state, restricted roles, authorized shipment/exception navigation and revocation handling |
| M15-04 tests | `NotificationTextTests.cs`, `M15NotificationRuntimeTests.cs`, `M15NotificationBoundaryTests.cs`, `notifications.spec.mjs`, `check-m15-contract.mjs`, branch-local GitHub workflow | Executable source, database and browser verification campaign created; full G15-S01..25 not executed |

## Executed results, frozen source boundary

At source SHA `51920dcb25cbe02109683852d4dc217dbf104c71`, [Actions run 42539950914](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539950914) passed the warning-as-error .NET build, all 299 unit and 46 contract tests, all 26 targeted PostgreSQL tests and all five M15 security/fault-injection cases. The real M11-to-M15 delivery fixture also passed. The browser job FAILED: two Chromium tests passed, four were unable to complete sign-in after the 20-attempt source IP rate limit was exhausted by fixture preparation and per-test authentication. Firefox and WebKit were not reached. No gate-wide PASS is claimed.

Later source revisions replace repeated Playwright logins with private, short-lived browser state copied from the real fixture sessions, with cleanup and no credential artifact uploads. These fixes require a source-locked rerun.

The connected Actions dispatch service is at its task limit. The branch-local GitHub Actions workflow remains manually dispatchable; the latest source cannot be called verified until the source, database and all three browser jobs pass on the same frozen SHA and the raw evidence is reviewed.

## Latest browser failure and application repair

[Actions run 42541604417](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541604417) on frozen `b1e79c826de71daea195250c00b46dcd7ab9a1a6` passed source/build checks and PostgreSQL integration. The live dispatcher fixture passed, and Chromium passed 5 of 6 scenarios. The receipt test timed out on its MarkRead -> Acknowledge sequence; Firefox and WebKit did not run.

In `NotificationCenter.tsx`, receipt mutation cleanup guarded `setBusyId(null)` on the pre-refresh request serial. Successful receipt refresh increments that serial, leaving `busyId` set and the Acknowledge button disabled. The source now releases the lock for the matching notification ID while the component remains mounted; Playwright explicitly asserts Acknowledge becomes enabled. Fix SHA: `464ac5728c4cc210eec4239e0bbcdc10d3786a58`. This update is **not yet reverified**. G15 remains pending.

## Implemented design choices requiring policy review

- History-to-intent trigger is transaction-coupled: a failed outbox insert fails the M11 history transaction. A rolled-back event has no persisted intent.
- Fanout obtains one repeatable-read transaction, source-history uniqueness and one receipt per active eligible recipient. Up to ten events per transaction and at most 1,000 eligible recipients per event are attempted. Failures back off and quarantine after bounded attempts. There is no external push or email.
- Eligibility is Analyst, Manager or Auditor with active business read permission. Administrator and Support do not receive shipment notifications by role alone.
- Recipient rows retain the membership ID/version at first committed fanout; read/count/mutation verify current identity, selected organization and permission. Old rows are hidden after membership version change.
- Personal `Unread -> Read -> Acknowledged` is separate from M11 exception disposition. Acknowledge implies read. A successful personal acknowledgment generates one transaction-coupled M16 audit event. Failed required audit rolls back the receipt.
- Idempotency replay records the exact first committed receipt version and timestamps. A later update cannot rewrite an earlier replay response.
- SQL read authority reuses accepted M11 signed proof rather than a separate M15 read key. SQL dispatcher authorization uses a separate purpose-bound HMAC value derived from the configured protected key. Both decisions require security/policy review.
- Dispatcher uses transaction-scoped row locks rather than persistent lease epochs, a documented design divergence requiring explicit reconciliation.
- No historical backfill, mass recipient truncation or automatic retention deletion is enabled. P12 online retention and legal-hold behavior remain acceptance pending; immutable M11/M16 evidence must not be deleted.
- M9 ARRI/DEPA processing stays held, 10,000-item unresolved cap remains, M10 projector stays default-off, ETA remains null and M11 evaluator stays opt-in.

## Independent M13 implementation coordination

At this checkpoint, M13's independent branch was `e5a7f70219bd9ad5aa5f4eccc0d894fffcda2127`. A separate integration verification branch exists at `6b05d84bf2f01741b182669c2dd17e39e63d9dda`, with independent gate decisions. M13 now has a distinct KPI migration `20261109020000_M13OperationalKpis` and application implementation. A combined branch must reconcile `Program.cs`, `AuditRoleProvisioner.cs`, `OrganizationWorkspace.tsx`, `MigrationSnapshotTests.cs`, `M11M12UpgradeTests.cs`, permissions and migration order. M15's `20261109010000` and M13's `20261109020000` do not by themselves prove compatible upgrades. Preserve separate M13 dashboard authorization and KPI source truth; M15 personal receipts must not change KPI measures.

## Additional unverified source improvements after the frozen run

The current M15 source now includes a Compose-controlled `HARBOROPS_M15_ENABLE_DELIVERY` setting with default `false`. The isolated verification job alone sets it to `true` to exercise the hosted dispatcher. It now runs the separate M15 notification fault-injection boundary suite after the older targeted integration tests, and prepares a live browser fixture: M5 Draft shipment -> authorized M11 evaluation -> committed M15 outbox -> hosted fanout -> scoped API -> shipment-exception navigation in Chromium, Firefox and WebKit. Browser mocks still separately cover receipt transitions, organization revocation and inaccessible targets. No operational workloads are enabled by these changes.

The SQL security tests verified invalid HMAC read proof rejection and `harborops_worker` denial in the five-case PostgreSQL boundary suite at `51920dcb`. The repeated browser authentication was subsequently replaced by source-locked, private, reusable fixtures with verified cookie attributes. Those fixture changes remain unexecuted. The Actions dispatch connector has a usage-limit blocker.

## Exit checklist

- [x] M15 source candidate and end-to-end event/data/API/UI path committed on separate branch
- [x] Previous-source .NET 10 build, 299 unit and 46 contract tests passed
- [x] Frozen-source 26 PostgreSQL tests and five M15 boundary cases passed
- [x] Historical run failures and corrections disclosed with source identities
- [ ] Latest private-browser-session harness executed on a final frozen SHA
- [ ] Chromium, Firefox and WebKit browser scenarios executed and passing, not skipped
- [ ] Source-locked recovery, malicious scope/replay/cursor and security mutation evidence reconciled against G15-S01..S24
- [ ] M13 combined migration, API and shell reconciliation completed on a shared tested integration SHA
- [ ] M15-P01..P14 policy dispositions approved and G15-S01 no longer blocked
- [ ] G15-S25 independent evidence review, separate acceptance decision and release authorization

Keep PR #37 in draft. No GitHub merge, production migration, deployment, or release is authorized by this status. All commits in this workstream retain `[skip ci]`.
