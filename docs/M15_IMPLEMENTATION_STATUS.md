# M15 Scoped In-App Notifications: Implementation Status

**G15 S25 update, 2026-11-12:** [Independent raw evidence disposition](G15_S25_INDEPENDENT_EVIDENCE_REVIEW_20261112.md) confirms technical PASS at `ebb28d2332ab6e7eb4264265db3ae35e8fbcbc35` (run 43178122017). Formal S25 unconditional acceptance remains pending reviewer-independence attestation. G15 conditional integration, P10/P12 restrictions, default-off delivery, and no deployment or deletion authorization remain unchanged. Earlier findings and failed runs are retained as historical evidence.

**G15 S25 evidence reaudit (2026-11-12):** [Current evidence disposition](G15_S25_EVIDENCE_REAUDIT_20261112.md) remains **NOT PASS** for unconditional closure. Four source-locked M11 witnesses have been added to the targeted test plan, the historical G13 witness has an explicit pending equivalence review, and the stronger complete-inventory checker has not yet been executed at a new source SHA. Existing conditional merges, operational restrictions and prior failed evidence are unchanged.

**Current integration state (2026-11-12):** G0-G12 and G16 are accepted and merged. G13 was conditionally accepted and merged via PR #38. G15 is conditionally accepted and merged via PR #41 at `47af1994b9958188666e5dde0aa6b5a536d7c55c`. Formal independent G15-S25 reviewer attestation was not established, and the procedural exception remains explicit. Dispatch defaults off, retention deletion and deployment are not authorized. See [G15 post-merge reconciliation](G15_POSTMERGE_RECONCILIATION_20261112.md).

**Final G13/G15 status, 2026-11-10:** [Source-locked run 42745334415](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED all 15 jobs at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543 passed TRX cases and 227/227 technical witnesses. The separate [policy decisions](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_POLICY_DECISIONS_20261110.md), [S25 reviews](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_REVIEW_MATRIX_20261110.md) and [formal decisions](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record **G13 NOT ACCEPTED; G15 NOT ACCEPTED**. G13 tenant storage quota/idle cleanup and exact negative mutation/HTTP-outage requirements remain. P10 is technically fulfilled; P12 deletion remains prohibited. Earlier numbers in this page are historical checkpoints, not the newest results. No main merge, deploy or activation.
**Historical 2026-11-10 predecessor source checkpoint:** [G13/G15 workflow 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed 15/15 jobs on `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` (541 tests passed, 0 failed/skipped). The separate atomic audit found 224 technical witnesses, 3 missing, 7 policy-review and 8 independent-review assertions; G13 and G15 acceptance remain pending. [Current detailed status](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_VERIFIED_EVIDENCE_20261110.md). Earlier source-specific reports below remain historical.

**Current checkpoint:** 2026-11-09 (Türkiye local date).  
**Implementation branch:** `feat/m15-scoped-notifications-20261108`  
**Integration PR:** [Draft PR #37](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/37), not merged.  
**Milestone disposition:** Latest standalone M15 technical candidate at `bbf4ba38c9742f4e78b9d184d5b60e8aabf6a04c` passed the full source, selected PostgreSQL and 21 three-engine browser executions in [run 42547260912](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42547260912). G15 acceptance pending; final successor combined technical verification has not run.

The authoritative executed test record, including the failed runs and immutable evidence links, is [M15_EXECUTED_VERIFICATION_20261109.md](M15_EXECUTED_VERIFICATION_20261109.md). It must be read alongside this status. Do not treat test source, corrected workflows or earlier run results as an accepted G15 gate.

## Implemented source work packages

| Package | Files and behavior | Disposition |
| --- | --- | --- |
| M15-01 | M11 immutable-history events Opened, Resolved and AutoResolved only. Fixed evidence-safe rule text, explicit role/recipient matrix, personal receipt lifecycle | Implemented as policy defaults. P01-P09/P11/P13-P14 approved; P10 conditionally approved and P12 provisional without deletion authority |
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
