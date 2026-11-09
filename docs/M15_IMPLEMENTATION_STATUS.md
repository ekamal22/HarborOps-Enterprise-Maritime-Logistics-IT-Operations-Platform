# M15 Scoped In-App Notifications: Implementation Status

**Current G13/G15 verification checkpoint (2026-11-09):** [Run 42567231512](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42567231512) **FAILED** at `0a32e807404ab13b3e80a74e8dc553b76ba69f98` with 13/15 successful jobs. The only substantive failure was a Firefox M15 browser revocation test clicking Refresh before its initial request settled; the dependent evidence ledger failed closed. The browser test now waits for the control to be enabled and asserts the revoked notification and organization UI disappear. New verification-branch source is **unexecuted**; independent S25 review, acceptance, merge and deployment remain pending. [Final campaign](G13_G15_FINAL_VERIFICATION_20261109.md).

**Current checkpoint:** 2026-11-09 (Türkiye local date).  
**Implementation branch:** `feat/m15-scoped-notifications-20261108`  
**Integration PR:** [Draft PR #37](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/37), not merged.  
**Milestone disposition:** Latest standalone M15 technical candidate at `bbf4ba38c9742f4e78b9d184d5b60e8aabf6a04c` passed the full source, selected PostgreSQL and 21 three-engine browser executions in [run 42547260912](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42547260912). G15 acceptance pending; final successor combined technical verification has not run.

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

On source SHA `82dac56bc00443cc022ecae0b38cd945d3db8f59`, [Actions run 42530770493](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42530770493) completed a warning-free .NET 10 build, 299/299 unit tests, 46/46 contract tests, 26/26 selected PostgreSQL integration tests, web build/test checks, M11/M15 source contracts and project documentation/graph checks. The browser job FAILED **before** executing Playwright because a required fixture evidence directory had not been created. This is a genuine failed campaign, not a full PASS.

Subsequent commits corrected browser fixture initialization and added exact committed-state idempotency replay snapshots and regression assertions. The current branch differs from the measured SHA. A new source-locked build/DB/browser campaign must validate these changes.

An earlier manual dispatch dependency reached its task limit. Later standalone run 42547260912 succeeded across all three jobs on the corrected SHA. Historical browser failures remain recorded, but there is no outstanding standalone browser failure on that verified SHA. Combined successor verification and G15 acceptance remain separate.

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

At an earlier integration checkpoint, M13's independent branch was `4e717bd45011b0a75b3d764b3f298bfcfb1d9056`. The newer branch head is `fe5d3396035012a4ccd97f16e9c244a78b9c4b3c`, whose optimized source has been reconciled in the successor. M13 now has a distinct KPI migration `20261109020000_M13OperationalKpis` and application implementation. A combined branch must reconcile `Program.cs`, `AuditRoleProvisioner.cs`, `OrganizationWorkspace.tsx`, `MigrationSnapshotTests.cs`, `M11M12UpgradeTests.cs`, permissions and migration order. M15's `20261109010000` and M13's `20261109020000` do not by themselves prove compatible upgrades. Preserve separate M13 dashboard authorization and KPI source truth; M15 personal receipts must not change KPI measures.

## Additional unverified source improvements after the frozen run

The current M15 source now includes a Compose-controlled `HARBOROPS_M15_ENABLE_DELIVERY` setting with default `false`. The isolated verification job alone sets it to `true` to exercise the hosted dispatcher. It now runs the separate M15 notification fault-injection boundary suite after the older targeted integration tests, and prepares a live browser fixture: M5 Draft shipment -> authorized M11 evaluation -> committed M15 outbox -> hosted fanout -> scoped API -> shipment-exception navigation in Chromium, Firefox and WebKit. Browser mocks still separately cover receipt transitions, organization revocation and inaccessible targets. No operational workloads are enabled by these changes.

The SQL security tests also now attempt an invalid HMAC read proof using real organization/recipient/session identifiers, and verify `harborops_worker` cannot read or dispatch M15 notification rows. All new tests are **authored, not executed** as of this checkpoint. The Actions dispatch connector reported a usage-limit blocker after the prior run; no newer success has been inferred.

## Exit checklist

- [x] M15 source candidate and end-to-end event/data/API/UI path committed on separate branch
- [x] Previous-source .NET 10 build, 299 unit and 46 contract tests passed
- [x] Previous-source 26 PostgreSQL tests passed including additive upgrade and restricted SQL roles
- [x] Historical run failures and corrections disclosed with source identities
- [x] Updated receipt/browser fixture source executed on standalone M15 SHA `bbf4ba38c9742f4e78b9d184d5b60e8aabf6a04c`
- [x] Chromium, Firefox and WebKit standalone M15 7/7 per engine, no skips, run 42547260912
- [ ] Source-locked recovery, malicious scope/replay/cursor and security mutation evidence reconciled against G15-S01..S24
- [ ] M13 combined migration, API and shell reconciliation completed on a shared tested integration SHA
- [ ] M15-P01..P14 policy dispositions approved and G15-S01 no longer blocked
- [ ] G15-S25 independent evidence review, separate acceptance decision and release authorization

Keep PR #37 in draft. No GitHub merge, production migration, deployment, or release is authorized by this status. All commits in this workstream retain `[skip ci]`.
