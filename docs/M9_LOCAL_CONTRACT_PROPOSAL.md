# M9 Local Processing and Reconciliation Contract Proposal

**State:** Proposed for M9-01 contract freeze; acceptance pending. This is not a new global D-number and does not reopen the accepted D-006 Option A decision.  
**Prepared:** 2026-11-04  
**Baseline:** G8 accepted; [G8 closure](M8_G8_GATE_CLOSURE.md).  
**Design:** [M9 architecture](M9_ARCHITECTURE_DESIGN.md).  
**Gate:** [G9 verification](M9_VERIFICATION_PLAN.md).

## Decision 1: State transitions and lease ownership

**Recommendation:** `Pending -> Leased -> Processed`, with `RetryWaiting`, `NeedsReconciliation` and `DeadLetter` as persisted alternatives. An active lease is identified by a private UUID token, strictly increasing lease epoch and a PostgreSQL-issued expiry. Only current, unexpired ownership may finalize a result. Attempts are incremented on claim, not at the point of failure.

An expired lease cannot update a receipt, replace the winner's result or renew an epoch it no longer holds. A takeover from expired Leased to new Leased is permitted only under the database lock with a fresh token, strictly higher epoch and incremented attempt count. Processed may commit only with exactly one immutable receipt-keyed effect in the same transaction, enforced by a deferred PostgreSQL completeness constraint. A stopped worker's abandoned lease remains durably visible until expiry and the next recovery sweep. Complete failure and retry attempts are recorded as append-only events. Processing effects have a unique receipt ID. `Processed` is terminal.

## Decision 2: Retry defaults

Proposed demonstrator defaults, validated at startup:

| Parameter | Proposed default | Invariant |
| --- | --- | --- |
| Lease lifetime | 60 seconds | Expiry enforced using database time |
| Lease renewal interval | 20 seconds | Renewal fails for stale token or epoch |
| Poll interval | 2 seconds | Bounded CPU and database polling; cancellation supported |
| Max attempts per generation | 5 | Exactly five claim starts permitted; no hidden sixth attempt |
| Retry base delay | 5 seconds | Nonnegative, explicit time bounds |
| Retry ceiling | 15 minutes | No overflow or unbounded postponement |
| Jitter | Deterministic, 0-20% | Stable hash of receipt, generation and attempt, independent of process randomness |
| Batch size | Up to 16 | Fair round-robin organization selection; no unbounded result materialization |
| Worker concurrency | Up to 4 per instance | Explicit global limit and cancellation |
| Manual redrives | Up to 3 generations | Operator decision and reason required, no unbounded reset |

A retry exhausted after the fifth failed or lost claim is DeadLetter. A malformed accepted-state mismatch is a permanent processing error, not permission to mutate immutable receipt/event history. PostgreSQL retryable errors are the only candidates for immediate operational retries, respecting the bounded schedule. Tests use injectable time controls and real PostgreSQL clock checks.

## Decision 3: Work capacity and admission

**Recommendation:** Preserve the accepted 10,000 unprocessed-item ceiling per organization, with a clarification that all `Pending`, `Leased`, `RetryWaiting` and `NeedsReconciliation` rows consume the same backlog budget. Only `Processed` and `DeadLetter` free it. A claimed but unacknowledged event remains unprocessed and must continue to consume capacity.

This requires updating the accepted M8 ingress count predicate in M9 alongside the work-state migration. Both ingress admission and terminal worker transitions must acquire the organization lock before checking/changing quota-relevant state. Identical source-key replays and 409 collisions still bypass new-key admission checks. This is a backward-compatible protective clarification of the accepted ceiling, but it changes the exact admission predicate and therefore **must** be explicitly frozen and regression-tested before implementation.

Alternative: Count only literal Pending work as M8 did, accepting that leased/retry items no longer count. **Rejected for planning** because rolling claims can admit more than the unresolved cap; not approved for implementation.

## Decision 4: Identity reconciliation and effect-once outcome

**Recommendation:** M9 only records one versioned, tenant-scoped, immutable processing effect for an event that can be assigned unambiguously to known references, including a shipment only when a valid tenant-local time-interval assignment proves it. The effect references the immutable accepted receipt and does not modify shipment workflow status, ETA, timeline or notifications.

- Equipment `GTIN/GTOT`: normalize and lookup `containers.container_identifier` within the source's organization; consider active/archived status and M6 half-open shipment-container assignments at `occurred_at_utc`. If no unique permissible association exists, record NeedsReconciliation, not a guessed shipment.
- Transport `ARRI/DEPA`: the M7 `transportReference` is a syntactically valid opaque transport identifier. It is **not** proven to be a HarborOps voyage reference. The default is NeedsReconciliation until M9-01 defines and tests an explicit reference mapping; do not silently equate fields.
- Cross-organization references, conflicting active assignment candidates, archived targets without a documented policy and out-of-window events are unresolved, not successful business effects.
- A successful effect is receipt-unique and type/version labeled, with constrained tenant-owned foreign keys. M10 later consumes the resolved event and separately handles deterministic ordering, projection and correction rules. M9 does not announce full shipment processing.

An alternative narrower policy may persist a unique resolved container/voyage ID without shipment ID when assignment is missing, but it must be explicitly chosen and tested to avoid implying that a receipt is bound to a shipment.

## Decision 5: Operations, permissions and redrive

**Recommendation:** Readable, tenant-scoped work state and dead-letter/reconciliation metadata are required. Add explicit permission names `TrackingWorkRead` and `TrackingWorkRedrive` to the existing permission matrix, not blanket business permissions for global administrative roles. Select permitted organization roles during M9-01. A redrive requires an authenticated scoped action, reason, bounded generation count and immutable attempt event. It may requeue only NeedsReconciliation/DeadLetter, never Processed or an active lease. Do not modify the accepted event or issue new publisher credentials.

A local management CLI may accompany the API for offline operation, but it must not bypass the same organization/role/scope checks or expose bearer, raw payload or connection secrets. Limit pages, redact error details and require direct-ID tenant guards.

## Decision 6: Failure handling and worker deployment

**Recommendation:** Separate, least-privileged `HarborOps.Worker` process using the existing .NET 10 host; PostgreSQL is the sole durable scheduler. No broker, external SaaS or new paid dependency. The default Compose configuration may add an opt-in worker service only after G9 runtime coverage; all verification uses a distinct `ho-verify-g9` project and new ports/subnet without touching existing retained resources. Docker/WSL and persistent test storage remain E:-backed. Retryability must not be inferred from exception message text; typed classifier and allowlist are required.

A source's credential revocation after a valid M8 receipt does not retroactively erase the accepted event. A suspended organization must not receive new processing side effects until resumed under authorized state. Sensitive data remains confined to the private database, protected temporary environment and source-locked evidence rules.

## Acceptance boundary

Approve or revise the six local choices as one versioned M9-01 contract before introducing M9 persistence migrations, worker state transitions and operator permissions. The source architecture and G9 plan remain proposals until that decision.

The following are not granted by this proposal: G9 acceptance, changes to D-006 historical retention, automatic reprocessing of DeadLetter records, M10 projections, internet-facing hosting, external incident notifications or access to another organization's events.

**Decision:** Acceptance pending.
