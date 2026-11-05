# M9-01 Local Contract Decision

**Decision date:** 2026-11-05  
**Status:** **All six choices approved with one mandatory operating condition.**  
**Scope:** M9 worker state, retries, backlog admission, reference resolution, permissions and deployment.  
**Basis:** Explicit approval of all six as implemented, with the transport-backlog condition, recorded in the project conversation on 2026-11-05.  
**Implementation:** [M9 draft PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18), tested source `0abeeb0e0eca3f639ef0036483f6cfc6e6e65440`.  
**Relationship to D-006:** This is an M9-local decision, not a global D-number. D-006 Option A remains accepted and its historical records are unchanged.

## Accepted choices

| # | Accepted contract | Impact |
| --- | --- | --- |
| 1 | Pending, Leased, RetryWaiting, NeedsReconciliation, Processed and DeadLetter; private lease token, strictly increasing epoch, database expiry and attempts at claim | A stale worker cannot finalize. Processed requires a receipt-unique immutable effect, with database-generated attempt evidence |
| 2 | 60-second lease, 20-second heartbeat, 2-second poll, five attempts, bounded exponential backoff (5 seconds to 15 minutes with deterministic 0 to 20 percent jitter), batch 16, concurrency 4 and at most three manual redrives | The attempt and redrive ceilings are fixed to PostgreSQL constraints; changing them later requires a reviewed migration and contract revision |
| 3 | The existing 10,000 per-organization unresolved-work ceiling counts Pending, Leased, RetryWaiting and NeedsReconciliation. Processed and DeadLetter release capacity. Canonical replay is still allowed under pressure | This intentionally tightens the accepted M8 admission behavior. The M8 receipt/event history, replay identity and durable-acknowledgment contract remain unchanged |
| 4 | Only equipment events with a unique active tenant container and one event-time-valid assignment can produce a receipt-unique resolution effect. Unknown, ambiguous, foreign, out-of-window and opaque transport references park in NeedsReconciliation | Every transport event parks in M9; M10 or a separately approved mechanism must define any future resolution. M9 does not write shipment timelines, status or projections |
| 5 | TrackingWorkRead for Analyst, Manager and Auditor; TrackingWorkRedrive for Manager only with authenticated active scope, CSRF protection, reason, audit evidence and a maximum of three generations | Administrator does not automatically receive queue access. The optional local management CLI is not part of the implemented contract |
| 6 | Separate least-privilege worker, PostgreSQL scheduler only, no broker, opt-in Compose, bounded failure handling, no processing of suspended organizations and durable receipts surviving source credential revocation | Default Compose does not start the worker. No production or public deployment is authorized by this decision |

## Mandatory transport-backlog operating condition

**Do not enable sustained transport-event processing in any deployed environment until M10, or a separately approved operational mechanism, demonstrably manages the transport events that M9 parks in NeedsReconciliation and prevents the unresolved backlog from exhausting the 10,000-item capacity.**

This is an operational enablement constraint, not permission to change M8 receipt semantics, automatically delete historical events, silently drop transport events or bypass admission safeguards. Controlled, isolated verification of transport-event parking remains permitted. If the backlog-management design is developed outside M10, it requires separate explicit approval and verification before sustained use.

The deployment/readiness checklist must carry this condition forward and assess its fulfillment before enabling sustained transport traffic. Approval of the six choices alone does not authorize production deployment, relax the backlog limit or imply G9 acceptance.

## Verification and remaining gates

- The source-locked G9 technical campaign on `0abeeb0e0eca3f639ef0036483f6cfc6e6e65440` reported 809 of 809 passing tests, 14 of 14 mutation families killed, and G9-S02 through G9-S24 technically passing, with browser evidence reported separately as 174 of 174 passing executions. See [executed verification](M9_EXECUTED_VERIFICATION.md), [browser supplement](M9_BROWSER_SUPPLEMENT.md) and [acceptance package](M9_G9_ACCEPTANCE_PACKAGE.md).
- **G9-S01 contract decision is now satisfied by this approval record.** The previously sealed attempt 08 raw scenario register must remain unchanged, where it correctly reported G9-S01 as BLOCKED at execution time. Record this later approval as a separate governance supplement rather than rewriting the raw result.
- **G9-S25 remains pending formal independent review and a dated reviewer decision.** Technical archive inspection and manifests do not themselves substitute for the required independent review record.
- **G9 acceptance remains pending.** No M9 merge into `main` is authorized by this decision.
- Merge preparation may proceed, with PR #16 as an ancestor of PR #18 and M16 shared-file/migration reconciliation handled separately. Merge only after formal G9 closure and explicit merge authorization.

**Disposition:** M9-01 contract approved with a mandatory operating condition. G9-S01 governance prerequisite satisfied. G9-S25, G9 acceptance and merge remain pending.
