# M9 Architecture: Idempotent Worker and Reconciliation

**Final G9 status (2026-11-05):** G9 accepted with the mandatory [M9-01 transport-backlog condition](M9_LOCAL_CONTRACT_DECISION.md); planning PR #16 and implementation PR #18 merged into `main` as `43fe326` and `4684352`. See [G9 gate closure](M9_G9_GATE_CLOSURE.md) and [evidence review](M9_G9_INDEPENDENT_EVIDENCE_REVIEW.md). Historical source-locked result classifications and earlier proposals below remain unaltered. No production deployment is authorized.

**Subsequent contract and evidence:** This document remains a historical design proposal. Its six local choices were approved with a mandatory [transport-backlog operating condition](M9_LOCAL_CONTRACT_DECISION.md) on 2026-11-05, and the source-locked [G9 campaign](M9_EXECUTED_VERIFICATION.md) plus [browser supplement](M9_BROWSER_SUPPLEMENT.md) passed. Formal G9-S25 review and acceptance are still pending.

**Historical proposal snapshot:** This document was written before the M9 candidate source implementation. The corrected candidate is recorded in [M9 implementation status](M9_IMPLEMENTATION_STATUS.md) and the [follow-up source audit](M9_CORRECTNESS_AUDIT_20261104.md). Neither test execution nor G9 acceptance has occurred. All six M9-01 contract decisions remain acceptance pending. [Status](STATUS.md) records the authoritative observed milestone state.

**Status:** Proposed architecture and implementation contract, not implemented or verified. M9/G9 acceptance pending.  
**Prepared:** 2026-11-04  
**Accepted source baseline:** `main` at `7b9e19d730e3a09c5e1184262460dfeac9319e3d`; G0-G8 accepted.  
**Graph:** M9-01 after G8, M9-02 and M9-03 after M9-01, M9-04 after both, G9 after all four. No graph change.  
**Requirements:** FR-007, FR-008, NFR-004.  
**Related:** [M9 implementation plan](M9_IMPLEMENTATION_PLAN.md), [G9 verification matrix](M9_VERIFICATION_PLAN.md), [M9 local contract proposal](M9_LOCAL_CONTRACT_PROPOSAL.md), [M8/G8 closure](M8_G8_GATE_CLOSURE.md).

## 1. Actual accepted baseline and invariant boundaries

M8 created the authoritative `tracking_inbox_receipts` and `tracking_accepted_events` immutable ledgers, the one-to-one `tracking_inbox_work_items` row and bounded `tracking_receipt_conflicts` metadata. Every accepted event creates exactly one receipt/event/work triple, and a duplicate request preserves the original receipt. The accepted M8 schema restricts work to `state='Pending'` and `attempt_count=0`; `m8_guard_work_item` rejects every update or delete. The ingress uses an organization row lock for transactional duplicate and capacity decisions, currently counting `state='Pending'` up to 10,000 per organization. The G8 gate explicitly requires M9 to replace the work-only constraint and guard through a new migration while retaining receipt/event immutability.

The existing worker project contains only `Program.cs` and `WorkerShell.cs`, with `Host.CreateApplicationBuilder` and an idle `BackgroundService`. Its project file references the application project but not infrastructure. The default `compose.yaml` runs PostgreSQL, migrator, API and web, not a worker. M9 must wire infrastructure, worker configuration and a separately managed worker service, not presume that work already runs.

The M7 `Equipment` variants GTIN/GTOT carry validated `equipmentReference`, while `Transport` variants ARRI/DEPA carry syntactically validated `transportReference`. The container table has a tenant-scoped `container_identifier`; voyage references and their meaning are distinct from generic transport identifiers. Never infer a transport-to-voyage match without an explicitly agreed mapping. M6 shipment-container and shipment-voyage assignments use tenant-owned, half-open time ranges. They are the supported base for deterministic reference reconciliation, not a license to invent shipment updates.

## 2. Scope and explicit exclusions

M9 delivers (a) durable worker state transitions and claim/reclaim with expiring leases; (b) finite retries with deterministic backoff and recorded dead-letter/reconciliation outcomes; (c) tenant-safe, idempotent event-to-reference resolution with one durable effect per accepted receipt; (d) bounded manual re-drive with authorization and audit; and (e) crash, replay, concurrency and restoration verification. Accepted original events and HTTP receipt semantics remain unchanged.

**M9 does not** compute shipment status/ETA projections, update `shipment_status_changes`, expose a tracking timeline, send notifications, implement M11 exceptions or provide production backup policy. M10 owns ordered event timelines, projections and rebuild. A G9 success can mean `Processed` with an immutable resolution record, not that a business projection or delivery has occurred. Unknown or ambiguous references must not be recorded as processed.

## 3. State machine and local contract

Proposed states: `Pending`, `Leased`, `RetryWaiting`, `NeedsReconciliation`, `Processed`, `DeadLetter`.

| Current state | Permitted transition | Guard |
| --- | --- | --- |
| Pending or due RetryWaiting | Leased | Atomic worker claim with unique token, incremented epoch and attempt count; lease expiry in database time |
| Leased | Leased | Heartbeat extends active lease only if current token, epoch and not expired |
| Leased | Processed | Same transaction commits immutable resolution effect and fenced work completion |
| Leased | RetryWaiting | Classified transient error, attempts below configured maximum; next eligible time persisted |
| Leased | NeedsReconciliation | Syntactically valid event with unknown/ambiguous or currently unresolvable tenant-local reference; no false association |
| Leased | DeadLetter | Confirmed permanent processor error or exhausted retry budget with bounded reason |
| Expired Leased | Leased with new owner, or DeadLetter | A recovery claim atomically replaces the token, increments epoch and attempt count, and records LeaseRecovered; if budget is exhausted it records DeadLetter. A stale lease cannot heartbeat or finalize. |
| NeedsReconciliation or DeadLetter | Pending | Authorized, audited explicit re-drive with new generation; no original event rewrite or automatic infinite retries |
| Processed | none | Terminal, duplicate claim, response or retry cannot create another effect |

`attempt_count` counts claim starts within the current generation; a redrive increments `redrive_generation` and resets the per-generation attempt count, while immutable attempt events retain the entire history. `lease_epoch` increments each time ownership changes. `lease_token` is a random UUID; fencing requires **both** token and epoch in every renewal and completion predicate. No claim lock is held across the full processing operation. Use PostgreSQL `clock_timestamp()` for the lease boundary to avoid host time drift; inject `TimeProvider` for test-driven scheduling decisions.

Recommended starting configuration, to be frozen in [local contract](M9_LOCAL_CONTRACT_PROPOSAL.md): lease 60 seconds, heartbeat at 20 seconds, poll delay 2 seconds, maximum 5 attempts per generation, exponential retry starting at 5 seconds and capped at 15 minutes, deterministic bounded jitter up to 20%, a small batch (up to 16) and a configurable concurrency ceiling of 4. All values are local demonstrator defaults, not benchmarks or new service-level promises. A configuration outside defined safe ranges fails at startup, not silently clamps.

## 4. Additive PostgreSQL schema, permissions and capacity

Create an M9-only EF migration after the accepted M8 migration, without editing historical migrations.

1. Extend `tracking_inbox_work_items` with `lease_token`, `lease_epoch`, `lease_expires_at_utc`, `available_at_utc`, `redrive_generation`, `last_error_code`, `completed_at_utc` and `resolution_kind`, with explicit null/state and nonnegative counter checks. Backfill all existing Pending rows to immediate eligibility without changing their IDs, organization, source, creation timestamp or accepted event. Widen `CK_m8_work_state` in the new migration and replace `m8_guard_work_item` with a state-transition guard that forbids key/tenant/created-at changes and deletes, and enforces legal transitions. Keep `m8_guard_append_only` and all receipt/event/conflict triggers untouched.
2. Add append-only `tracking_work_attempt_events`, with tenant-scoped receipt FK, generation/attempt/lease epoch, status code, bounded sanitized reason and recorded UTC timestamp. Add an immutable guard, unique stable event key and indexes for worker diagnosis. An expired-lease takeover is distinguishable from a heartbeat: it requires an already expired old lease and a new token, strictly higher epoch and attempt count. A heartbeat retains the current token and epoch while extending the deadline only before expiry. Never store raw source JSON, credentials, SQL errors or stack traces in the table.
3. Add append-only `tracking_processing_effects`, with primary unique `receipt_id`, organization/source scope, an explicit resolution kind, optional resolved tenant-owned container/voyage/shipment IDs only when unambiguous, and a versioned resolution policy identifier. Enforce composite tenant FKs for any resolved reference. Add a **DEFERRABLE INITIALLY DEFERRED** completeness constraint on a transition to Processed, requiring exactly one corresponding receipt effect at commit; an effect cannot be deleted or edited. One committed receipt can have at most one processing effect. This is M9's effect-once business resolution outcome and M10's provenance input, **not** an update to shipment state.
4. Add an eligible-work index ordered by organization, due time, receipt age and ID, plus an expired-lease index. Preserve the M8 unique receipt key and all immutable ledger indexes. Add guarded operator queue-read index as needed.
5. Concurrency invariant for the accepted 10,000-cap demonstrator: keep a bound across unresolved work, including Pending, Leased, RetryWaiting and NeedsReconciliation. Expand the M8 admission-count predicate in a coordinated M9 change under the same organization row lock, counting all unresolved nonterminal work, not merely literal Pending. This prevents claiming 10,000 rows from allowing another 10,000 to be admitted. Processed and DeadLetter no longer occupy the active-backlog ceiling, but remain durably inspectable. Replays and collisions are still resolved before capacity enforcement. This is an explicit M9 contract clarification requiring approval at M9-01, not an unnoticed M8 behavior change.
6. Lease/claim/status transitions that change active backlog must respect the M8 lock order: lock organization first, then work row; never hold a work row while waiting for the organization. Claim per organization in short transactions and use `FOR UPDATE SKIP LOCKED` for eligible rows. Do not lock source or credential merely to process a previously accepted event. For suspended organizations, retain accepted data and skip new business resolution until authorized reactivation.
7. A normal `Down()` must reject a populated M9 work history/effects or a non-M8-compatible work state. Only an empty/disposable M9 schema may be reversed. Do not delete, truncate or reset accepted M8 history to make downgrade work.

The M9 worker should use a separately configured, least-privileged PostgreSQL service identity, without general rights to rewrite immutable ledger tables. Validate DB permissions and direct SQL bypass attempts in G9. Credential injection and temporary test data stay on E:, with no secrets in committed files.

## 5. Claim, processing and effect-once transaction

### Claim transaction

Select one active organization at a time, respecting a deterministic fair traversal. Within a short PostgreSQL transaction acquire its organization row lock, then atomically select due Pending/RetryWaiting or expired Leased work `FOR UPDATE SKIP LOCKED` and update to Leased with fresh token, epoch, deadline and incremented attempt count. Record an immutable `Claimed` attempt event in the same transaction. If an expired prior claim exhausted its budget, dead-letter it under lock rather than permitting a sixth claim. Commit and return a bounded private work descriptor; do not expose raw event payload in logs.

This mechanism permits multiple worker processes across different organizations; same-organization admission and claim transactions serialize briefly without turning an entire processing operation into a long organization lock.

### Resolve outside the claim transaction

Read the immutable event by its receipt and organization/source scope. Verify that the leased worker still has a valid token before committing any effect. Resolve only exact, tenant-local references using accepted M3/M4/M6 domain tables and the event's time for half-open assignment intervals. A single unambiguous container/shipment link may be persisted as a resolution effect. For transport events, do not conflate an arbitrary `transportReference` with a voyage code: either freeze a defensible mapping with tests at M9-01 or park as NeedsReconciliation. Unknown references, archived/mismatched scopes, multiple candidate assignments and unresolvable mappings must not use a foreign-tenant fallback or invent a shipment ID.

### Finalize atomically

Begin a short transaction, lock the organization first and the specific work row second. Check `state='Leased'`, token, epoch and unexpired lease against PostgreSQL time. For a resolved outcome, insert the immutable unique effect if absent and transition to Processed in the **same transaction**; add an immutable Completed event. For a transient or permanent outcome, transition to RetryWaiting, NeedsReconciliation or DeadLetter with a bounded error code and corresponding immutable event. If the lease expired or epoch changed, roll back without creating an effect. On ambiguous COMMIT acknowledgment, do not announce success; reconnect and read the persisted work/effect identity. Re-processing the same receipt must hit unique constraints and reconstruct the committed outcome rather than create duplicates.

Never call network destinations, enqueue external notifications or update an M10 projection inside this claim/finalize path. Future M10 handlers need their own receipt-key idempotency and transactional effect boundary.

## 6. Retry, failure classification and operations

Classify only known transient failures such as temporary PostgreSQL unavailability, lock timeout or explicitly recoverable local dependency failure as RetryWaiting. Use an exponential due-time schedule with deterministic bounded jitter from receipt ID, generation and attempt. Distinguish lease expiration from an application failure in attempt events. Permanent failures and exhausted attempts become inspectable DeadLetter. Syntactically valid but unknown or ambiguous tenant references go to NeedsReconciliation without a hot retry loop; a reference may be resolved later through an explicit operator action.

Provide a bounded, tenant-scoped operator view and explicit re-drive command using existing session/role permission checks. Define new explicit `TrackingWorkRead` and `TrackingWorkRedrive` permissions at M9-01 rather than assuming an organization Administrator has implicit access to business records. Restrict redrive to a specific receipt and organization; require reason, generate a new generation/epoch, apply a configured redrive ceiling and append an audit event. No bulk cross-tenant redrive or public anonymous queue endpoint. Reject a redrive of Processed or a currently leased row. The operator sees sanitized status/reason metadata and immutable receipt reference, never source bearer or full raw payload.

On graceful shutdown stop claiming, let bounded in-flight work finalize or relinquish on lease expiry. A database outage must not permanently lose a claimed item. Expose structured, credential-free counters for claimed, processed, transient retry, reconciled, dead letter, stale lease and lag, with organization IDs excluded from externally visible metric labels. Worker/DB readiness should fail meaningfully without a hot loop or unbounded log growth.

## 7. Topology, testing and evidence

Replace idle `WorkerShell` with a real hosted polling service only under the M9 feature branch. Add worker-to-infrastructure references, explicit DI registrations, startup configuration validation, separate worker service in an **isolated G9 Compose project**, least-privileged DB credentials, health/readiness where useful, graceful cancellation and an opt-in bounded `--drain` or test-run mode. The base Compose worker addition must not change default accepted M8 ingestion or run a worker during old G8 tests unless intentionally enabled.

All G9 verification must use disposable E:-backed PostgreSQL and isolated Compose ports/network; test fresh migration, populated M8 upgrade, empty rollback, append-only SQL attacks, two workers and multiple tenants, actual lease expiry, token fencing, crash at claim/effect/finalize boundaries, COMMIT ambiguity, duplicate replays, downtime/recovery, quota and M0-G8 regressions. Capture raw TRX, source SHA, mutations targeting missing fencing, missing unique effect and unlimited retries, browser/API regression evidence, supply-chain reports and a sealed hashed package. No GitHub Actions. See [G9 verification plan](M9_VERIFICATION_PLAN.md).

## 8. Material design decisions still to freeze

The [M9 local contract proposal](M9_LOCAL_CONTRACT_PROPOSAL.md) must resolve: exact state names and allowed re-drive transitions; active-backlog interpretation of the 10,000 Pending limit; equipment/transport-to-shipment association policy and unknown-reference disposition; bounded operator permissions; retry and lease defaults; and whether effect records contain a resolved shipment ID or a narrower typed reference. Record these before M9-02/M9-03 schema/API changes. This proposal is **not** D-006 reacceptance, G9 execution or authority to merge M9 code. Acceptance pending.
