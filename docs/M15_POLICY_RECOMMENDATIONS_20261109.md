# M15 Policy Recommendation Register

**Current G15 state (2026-11-12):** G15 and G15-S25 are **ACCEPTED** and merged into `main` through [PR #44](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/44) at `49934d793542ef43cfe587c54b16cfd5cce8e2f4`. Tested executable SHA `ebb28d2332ab6e7eb4264265db3ae35e8fbcbc35` passed [run #43178122017](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43178122017). Reviewer independence is an expressly disclosed acceptance exception, not an attested credential. G13 stays conditionally integrated; M15-P10 fail-closed and P12 no-deletion policies remain, delivery is default-off, and no deployment or final release is authorized. See [final decision](G15_S25_FINAL_ACCEPTANCE_20261112.md) and [post-merge reconciliation](G15_S25_POSTMERGE_ACCEPTANCE_RECONCILIATION_20261112.md).


**Final G13/G15 status, 2026-11-10:** [Source-locked run 42745334415](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED all 15 jobs at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543 passed TRX cases and 227/227 technical witnesses. The separate [policy decisions](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_POLICY_DECISIONS_20261110.md), [S25 reviews](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_REVIEW_MATRIX_20261110.md) and [formal decisions](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record **G13 NOT ACCEPTED; G15 NOT ACCEPTED**. G13 tenant storage quota/idle cleanup and exact negative mutation/HTTP-outage requirements remain. P10 is technically fulfilled; P12 deletion remains prohibited. Earlier numbers in this page are historical checkpoints, not the newest results. No main merge, deploy or activation.
**Current technical evidence (2026-11-10):** [15-job run 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`, including 541 passing TRX tests, no failures/skips, and 48 passing technical scenario contract checks. The separate 242-assertion audit has 224 technical witnesses present, 3 missing, 7 policy-review and 8 independent-review items. Full G13/G15 acceptance remains pending. [Current source-locked evidence](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_VERIFIED_EVIDENCE_20261110.md).

**Prepared:** 2026-11-09. **Latest evidence:** 2026-11-10. **Decision:** P01-P09, P11, P13-P14 approved as recommended; P10 approved conditionally on verified transactional recovery and accepted-M11 regressions; P12 provisional periods approved without deletion authority. All approval scope recorded here is distinct from G15 acceptance, a merge, or production activation.

| Decision | Recommended choice | Rationale | Approval |
| --- | --- | --- | --- |
| M15-P01 | Only immutable M11 Opened, Resolved and AutoResolved | No infrastructure source or unsupported event | APPROVED |
| M15-P02 | Active Analyst, Manager and Auditor with ViewShipmentData | No Administrator/Support business alerts | APPROVED |
| M15-P03 | Snapshot eligible recipient membership at committed fanout | No retroactive joiner backfill | APPROVED |
| M15-P04 | Validate live role, membership ID/version, user, organization and resource | Revocation and rejoin must not resurrect old alerts | APPROVED |
| M15-P05 | Unread, Read, Acknowledged terminal lifecycle | Personal state never changes M11 cases | APPROVED |
| M15-P06 | Unique event, recipient and context-bound replay key | Effect-once durable consequences | APPROVED |
| M15-P07 | Fixed evidence-safe titles and reason-free descriptions | No raw tracking, actor IDs or inferred ETA | APPROVED |
| M15-P08 | Default 25/max 50, 20-minute protected cursor, same-origin PATCH | Session-bound pagination and mutation | APPROVED |
| M15-P09 | M16 audit for acknowledgment atomically, not MarkRead | Protect audit consistency | APPROVED |
| M15-P10 | Atomic AFTER INSERT M11 history outbox trigger, fail-closed rollback | Availability tradeoff accepted, subject to successful recovery and M11 regression evidence | APPROVED WITH VERIFICATION CONDITION |
| M15-P11 | Opt-in bounded dispatcher, no historical backfill | Default-off production safety | APPROVED |
| M15-P12 | Provisional 90-day unread/read and 30-day acknowledged retention, cleanup OFF | Policy period accepted provisionally; legal hold and deletion authority remain unresolved | PERIOD APPROVED, DELETION NOT APPROVED |
| M15-P13 | Isolated M15 routes/UI; no M13 metric derivation from notifications | Prevent milestone crossover | APPROVED |
| M15-P14 | Archived/cancelled targets historical; live reauthorization; hide missing | Prevent existence oracle | APPROVED |

## Approval-critical decisions

**P10:** Approved with an explicit availability tradeoff. An outbox capture failure can roll back an accepted M11 history write. The source-locked G13/G15 campaign has passing rollback, process-restart, M11 regression and concurrency tests; independent conformance and P10 conditional acceptance review remain necessary before gate acceptance.

**P12:** Approved provisional periods do not authorize cleanup. Preserve immutable M11/M16 evidence, holds, replay history and undelivered intents. Cleanup stays OFF pending an explicit, legally informed disposal decision.

**P11:** Delivery remains default-off in production. Disposable verification may temporarily enable the dispatcher without changing operational defaults.

## Approved provisional G13 verification thresholds

Successor run [42552035939](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42552035939) measured p50 approximately 10,479 ms and a three-sample p95 approximately 10,540 ms on 170,000 shipments with about 450,006 materialized member rows. Three samples are insufficient for a concurrency or production SLA decision. Approved as an initial source-locked verification envelope: at 170,000 shipments (approximately 450,006 KPI members), p50 <= 15,000 ms and nearest-rank p95 <= 20,000 ms across at least 30 successful measured reports; request deadline 25 seconds; SQL statement timeout 15 seconds; member ceiling 500,000 (reject complete build if exceeded); report TTL 10 minutes; member page limit 100; and a separate four-principal concurrent request campaign. Store/report table and index bytes, elapsed CPU and memory, temp spill and cleanup backlog must be recorded; no arbitrary storage or permanent cleanup cap is approved until measured. If the envelope fails, G13-S20 fails, requiring explicit resolution rather than relaxing test expectations.

**Historical predecessor status (2026-11-10):** Approved policy decisions remain unchanged. The combined technical campaign passed at the frozen SHA, but three atomic G13 witnesses remain missing and full scenario dispositions are still blocked. Final permanent G13 storage/cleanup numerical decisions, conditional P10 closure, independent S25 reviews, milestone acceptance and merge remain pending. No production rollout or cleanup is authorized.
