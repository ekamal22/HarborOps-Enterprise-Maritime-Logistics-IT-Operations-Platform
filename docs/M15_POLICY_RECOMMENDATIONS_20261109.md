# M15 Policy Recommendation Register

**Prepared:** 2026-11-09. **Approval state:** All M15-P01 through P14 PENDING. These are recommendations, not approvals. Technical predecessor run 42555586856 PASSED; expanded eleven-job parallel candidate on `verify/g13-g15-parallel-closure-20261109` remains unexecuted. Neither technical result creates policy approval.

| Decision | Recommended choice | Rationale | Approval |
| --- | --- | --- | --- |
| M15-P01 | Only immutable M11 Opened, Resolved and AutoResolved | No infrastructure source or unsupported event | PENDING |
| M15-P02 | Active Analyst, Manager and Auditor with ViewShipmentData | No Administrator/Support business alerts | PENDING |
| M15-P03 | Snapshot eligible recipient membership at committed fanout | No retroactive joiner backfill | PENDING |
| M15-P04 | Validate live role, membership ID/version, user, organization and resource | Revocation and rejoin must not resurrect old alerts | PENDING |
| M15-P05 | Unread, Read, Acknowledged terminal lifecycle | Personal state never changes M11 cases | PENDING |
| M15-P06 | Unique event, recipient and context-bound replay key | Effect-once durable consequences | PENDING |
| M15-P07 | Fixed evidence-safe titles and reason-free descriptions | No raw tracking, actor IDs or inferred ETA | PENDING |
| M15-P08 | Default 25/max 50, 20-minute protected cursor, same-origin PATCH | Session-bound pagination and mutation | PENDING |
| M15-P09 | M16 audit for acknowledgment atomically, not MarkRead | Protect audit consistency | PENDING |
| M15-P10 | Atomic AFTER INSERT M11 history outbox trigger, fail-closed rollback | M11 coupling must be explicitly accepted | PENDING |
| M15-P11 | Opt-in bounded dispatcher, no historical backfill | Default-off production safety | PENDING |
| M15-P12 | Provisional 90-day unread/read and 30-day acknowledged retention, cleanup OFF | Legal hold and deletion authority remain unresolved | PENDING |
| M15-P13 | Isolated M15 routes/UI; no M13 metric derivation from notifications | Prevent milestone crossover | PENDING |
| M15-P14 | Archived/cancelled targets historical; live reauthorization; hide missing | Prevent existence oracle | PENDING |

## Approval-critical decisions

**P10:** The outbox creates a new transactional dependency within accepted M11 writes. Review rollback, locking, latency and audit failures before approving fail-closed behavior.

**P12:** Proposed retention windows do not authorize cleanup. Preserve immutable M11/M16 evidence, holds, replay history and undelivered intents. Cleanup stays OFF pending an explicit, legally informed disposal decision.

**P11:** Delivery remains default-off in production. Disposable verification may temporarily enable the dispatcher without changing operational defaults.

## G13 resource budget decision

Successor run [42552035939](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42552035939) measured p50 approximately 10,479 ms and a three-sample p95 approximately 10,540 ms on 170,000 shipments with about 450,006 materialized member rows. Three samples are insufficient for a concurrency or production SLA decision. Define approved p50/p95, timeout, storage, member cap, report TTL and cleanup budgets only after realistic multi-sample/concurrent runs. Existing 25-second request bounds and member caps are safeguards, not approved targets.

**Status:** G13-S01/S20 and G15-S01 BLOCKED pending the applicable explicit decisions; neither milestone is accepted, merged or released.
