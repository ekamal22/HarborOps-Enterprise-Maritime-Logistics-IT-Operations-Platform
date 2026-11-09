# M15 Policy Recommendation Register

**Prepared:** 2026-11-09. **Decision:** P01-P09, P11, P13-P14 approved as recommended; P10 approved conditionally on verified transactional recovery and accepted-M11 regressions; P12 provisional periods approved without deletion authority. All approval scope recorded here is distinct from G15 acceptance, a merge, or production activation.

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

**P10:** Approved with an explicit availability tradeoff. An outbox capture failure can roll back an accepted M11 history write. Full targeted rollback, restart, latency, M11 audit and concurrency verification remains mandatory before gate acceptance.

**P12:** Approved provisional periods do not authorize cleanup. Preserve immutable M11/M16 evidence, holds, replay history and undelivered intents. Cleanup stays OFF pending an explicit, legally informed disposal decision.

**P11:** Delivery remains default-off in production. Disposable verification may temporarily enable the dispatcher without changing operational defaults.

## Approved provisional G13 verification thresholds

Successor run [42552035939](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42552035939) measured p50 approximately 10,479 ms and a three-sample p95 approximately 10,540 ms on 170,000 shipments with about 450,006 materialized member rows. Three samples are insufficient for a concurrency or production SLA decision. Approved as an initial source-locked verification envelope: at 170,000 shipments (approximately 450,006 KPI members), p50 <= 15,000 ms and nearest-rank p95 <= 20,000 ms across at least 30 successful measured reports; request deadline 25 seconds; SQL statement timeout 15 seconds; member ceiling 500,000 (reject complete build if exceeded); report TTL 10 minutes; member page limit 100; and a separate four-principal concurrent request campaign. Store/report table and index bytes, elapsed CPU and memory, temp spill and cleanup backlog must be recorded; no arbitrary storage or permanent cleanup cap is approved until measured. If the envelope fails, G13-S20 fails, requiring explicit resolution rather than relaxing test expectations.

**Status:** The stated policy decisions and provisional G13 thresholds are approved. Technical conformance, final storage/cleanup numerical decisions, S25 independent evidence reviews, milestone acceptance and merge remain pending. No production rollout or cleanup is authorized.
