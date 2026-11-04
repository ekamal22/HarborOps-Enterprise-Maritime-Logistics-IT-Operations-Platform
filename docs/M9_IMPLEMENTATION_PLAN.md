# M9 Implementation Plan: Idempotent Worker and Reconciliation

**Historical proposal snapshot:** This document was written before the M9 candidate source implementation. The corrected candidate is recorded in [M9 implementation status](M9_IMPLEMENTATION_STATUS.md) and the [follow-up source audit](M9_CORRECTNESS_AUDIT_20261104.md). Neither test execution nor G9 acceptance has occurred. All six M9-01 contract decisions remain acceptance pending. [Status](STATUS.md) records the authoritative observed milestone state.

**State:** Planning complete as a proposal. No M9 application, database, worker or verification code has been implemented. G9 acceptance pending.  
**Prepared:** 2026-11-04  
**Planning branch:** `plan/m9-idempotent-worker-reconciliation`  
**Accepted source baseline:** `main` `7b9e19d730e3a09c5e1184262460dfeac9319e3d`, G0-G8 accepted.  
**Graph owner:** [execution-graph.json](execution-graph.json), four work packages M9-01 to M9-04 and gate G9.  
**Design:** [M9 architecture](M9_ARCHITECTURE_DESIGN.md); [M9 local contract](M9_LOCAL_CONTRACT_PROPOSAL.md); [G9 matrix](M9_VERIFICATION_PLAN.md).

## Dependency and implementation sequence

```text
G8 accepted
    |
    v
M9-01  Freeze lease, state, reconciliation and backlog contract
    |                         |
    +-------------------------+
    |                         |
    v                         v
M9-02  Transactional claim   M9-03  Retry, dead letter and redrive
    |                         |
    +-------------------------+
                  |
                  v
M9-04  Crash, fencing, concurrency and recovery verification
                  |
                  v
                 G9  Independent evidence review, acceptance pending
                  |
                  v
                 M10  Eligible only after accepted G9
```

M9-02 and M9-03 may be developed in parallel **after** M9-01 freezes shared model/SQL/permission semantics. However, only one integration branch owns the EF migration, `TrackingInboxWorkItem` model, EF snapshot, state transition guard and worker DI registration. A separate branch may implement pure retry classification, policy unit tests and operator API contract tests without editing those shared files. Integrate before a single source-locked G9 execution.

## M9-01: Define worker state transitions and freeze local contract

**Precondition:** G8 accepted.  
**Inputs:** [M8/G8 closure](M8_G8_GATE_CLOSURE.md), [D-006 accepted proposal](M8_D006_DURABILITY_PROPOSAL.md), worker scaffold, M8 migration, existing organization and reference/assignment tables.

Deliverables:

1. Adopt or revise [M9 local contract proposal](M9_LOCAL_CONTRACT_PROPOSAL.md), especially the Pending-cap semantics, state transition diagram, lease token/epoch fencing, retry/redrive budgets, unknown reference handling, worker role privileges and required operator permissions. Any material change to D-006 terms must be recorded as a separate decision before coding.
2. Specify SQL migration and EF mapping before implementation. Pin the exact proposed state names and composite foreign keys. Define immutable event/receipt protection and the direct-SQL transition guard behavior.
3. Define one typed, tenant-scoped business-resolution effect per original receipt, not M10 projection writes. Map equipment to active tenant-owned containers and M6 time-valid assignments; explicitly resolve what `transportReference` can mean. Never infer a shipment from a mere token.
4. Define `ITrackingWorkClaimer`, `ITrackingWorkFinalizer`, `ITrackingReferenceResolver`, `ITrackingRetryPolicy`, `ITrackingWorkAdminStore` and worker configuration boundaries; choose DB clock and timeouts. Define exact sanitized queue status/error codes.
5. Define exact G9-S01 to G9-S25 pass/fail conditions, negative tests and controlled fault windows.

**Exit:** Versioned M9-01 contract accepted for implementation. G9 remains acceptance pending.

## M9-02: Additive schema and transactional worker claim/finalization

**Precondition:** Frozen M9-01.

Source modifications to implement:

- `src/HarborOps.Infrastructure/Persistence/Migrations/<new M9 timestamp>_TrackingWorker.cs`: additive work columns and initial Pending backfill; explicit guarded state constraints; replace M8 work guard only; add a deferred Processed-requires-effect completeness trigger; append-only attempt/effect tables and tenant-scoped FKs; eligible/expired indexes; safe empty rollback and blocked populated rollback.
- `src/HarborOps.Infrastructure/Integrations/TrackingInboxEntities.cs`, `HarborOpsDbContext.cs`, `HarborOpsDbContextModelSnapshot.cs`: strictly matching EF model with DB constraints; unchanged receipt/event/conflict immutability.
- `src/HarborOps.Infrastructure/Integrations/TrackingWorkStore.cs` (new): short organization-first claim transactions with `FOR UPDATE SKIP LOCKED`, expiring leases, generation/epoch/token checks, heartbeats, bounded row selection, fair polling, and atomic effect + processed-state finalization. Never hold a claim transaction while performing resolution.
- `src/HarborOps.Infrastructure/Integrations/TrackingReferenceResolver.cs` (new): deterministic scoped lookup against source event time and tenant assignments; 0/1/multiple result treatment without foreign organization fallbacks.
- `src/HarborOps.Infrastructure/Integrations/TrackingInboxStore.cs`: adjust the same-org M8 10,000-cap admission predicate to count all unresolved states, maintaining source-key replay-before-cap and organization-first lock order.
- `src/HarborOps.Worker/HarborOps.Worker.csproj`, `Program.cs`, `WorkerShell.cs` (replace idle host): add infrastructure dependency, worker DI, bounded poll loop, explicit required connection config, heartbeat renewal, graceful cancellation, safe JSON logs and fail-closed errors.
- `compose.yaml` and a new `compose.g9.verify.yaml`: opt-in worker startup, separate DB identity/secret configuration, isolated G9 verification project and clean teardown; no retained volume changes.

Implementation rules: use PostgreSQL time for lease predicates, command timeouts, optimistic fencing and atomic unique effect records. Ensure no stale worker can commit after another worker reclaims. Use unique `receipt_id` as the effect key and a versioned resolution policy; do not write shipment statuses or projections. Preserve accepted M8 API error shapes and the full M7/G7 authentication negative matrix.

**Exit:** Buildable migration, safe worker and deterministic effect-once processing code. Still no G9 acceptance.

## M9-03: Bounded retries, dead letter, reconciliation and scoped redrive

**Precondition:** Frozen M9-01. May proceed beside M9-02 in separate non-conflicting files.

Source modifications to implement:

- `src/HarborOps.Domain/Tracking/TrackingWorkPolicy.cs` (new): pure transition validator, typed transient/permanent/unknown reference classification, deterministic retry delay and jitter, max attempts, redrive limits, bounded error code vocabulary.
- `src/HarborOps.Application`: orchestration contracts for claim/finalize/admin actions, never a second shadow queue.
- `src/HarborOps.Infrastructure/Integrations/TrackingWorkAdminStore.cs` (new): scoped, paginated metadata queries and deterministic re-drive transaction. Require organization and exact receipt scopes with row locks and immutable append-only redrive event.
- `src/HarborOps.Api`: a small authenticated operator surface or scoped CLI that uses explicit `TrackingWorkRead` and `TrackingWorkRedrive` permissions, never default administrative business access. Suppress bearer, raw JSON and internal SQL errors.
- Structured observability counters and lag readings with bounded labels. Health/readiness reflects PostgreSQL loss, and worker shutdown avoids hidden pending tasks.

Unknown/ambiguous equipment/transport/shipment associations remain NeedsReconciliation, not dead-letter by default, and are rechecked only after an explicit authorized re-drive. Recoverable PostgreSQL failures use RetryWaiting until budget exhaustion, then DeadLetter. Already Processed work cannot be redriven. Redrive does not create another receipt or reset the immutable effect ledger.

**Exit:** Every accepted receipt has a safe, inspectable processing disposition; no unbounded retry or false terminal success.

## M9-04: Integrated verification and controlled recovery

**Precondition:** M9-02 and M9-03 integrated, working tree clean, source SHA recorded.

Use an E:-backed isolated verification root with dedicated G9 Compose project, no default Compose resource reuse and no GitHub Actions.

Efficiency sequence:

1. Preflight clean source, documentation links/graph, script syntax, `.NET Release` build with warnings as errors, EF snapshot parity, pure state/lease/retry units, current dependency advisories and frontend build. Abort genuine failures before starting expensive containers.
2. Fresh full M0-M9 migration and a populated M8-to-M9 upgrade against disposable PostgreSQL. Test work-backfill, alternate key/tenant FKs, direct-SQL immutability, invalid transition and downgrade refusal; run a clean M8 regression first.
3. Two workers with independent processes and transactions; same receipt, cross-source, cross-organization, organization suspension, lease expiry/heartbeat, stale token, claim kill, lock waiting and cap-at-10,000 races. Use barriers to prove actual overlap, not sleep-based guesses.
4. Exhaust failure windows: after claim commit, during resolution, after effect insert before final commit, after final COMMIT but before worker acknowledgment, heartbeat expiry, database outage and restart. Check one immutable effect per receipt and no double work outcome.
5. Run strict retry budget and deterministic jitter tests, dead letter, NeedsReconciliation, scoped operator redrive with generation, incomplete mapping and malicious cross-tenant ID attempts. Include all accepted M0-G8 tests affected by new state/permissions/Compose.
6. Run all mandatory G9 targeted security mutations, browser/API regressions (three engines if operator UI or shared browser contracts are modified), advisory and exact image scans; do not skip security/negative cases to save time.
7. Capture raw TRX, PostgreSQL race witnesses, worker logs, browser results, mutation outcomes, image IDs, teardown, manifest and archive hashes. Independently review a source-locked sealed package. Then request explicit G9 acceptance. Do not update original evidence after sealing.

G9 is a technical and governance gate. A clean build and functioning worker alone do not establish its closure. A blocked or skipped required case remains BLOCKED or NOT EXECUTED until resolved or explicitly reviewed under the gate policy.

## Critical implementation invariants

| ID | Invariant | Guard/test |
| --- | --- | --- |
| W-01 | Original accepted receipt/event/collision history is immutable | SQL trigger attack tests on UPDATE/DELETE and original digest/bytes |
| W-02 | Every receipt retains exactly one work row | Composite FKs, primary key, M8 deferred completeness and migration tests |
| W-03 | Only one current lease may finalize | Token+epoch+expiry predicate, row locks and stale-worker kill test |
| W-04 | A committed business resolution effect occurs once | Unique receipt effect, same-transaction finalization, crash-before/after COMMIT |
| W-05 | Retry count and redrive are finite | Typed policy, attempt events, budget and redrive ceiling |
| W-06 | Tenant/resource identity is never guessed | Tenant FK and scoped M6 time-valid mapping; unknown -> reconciliation |
| W-07 | Unresolved backlog remains bounded at 10,000 | Organization lock, all active unresolved states counted, concurrent intake/worker test |
| W-08 | Terminal state cannot be silently reset | Transition guard, authorized redrive audit, Processed redrive forbidden |
| W-09 | Worker shutdown and loss do not lose accepted work | Lease expiry, restart, controlled cancellation, fail-closed DB outage |
| W-10 | G8 ingress and historical evidence stay intact | Full affected M0-G8 regression and no original migration edits |

## Cross-track coordination

M10 needs a stable processing-effect schema and a clear rule for NeedsReconciliation; M9 cannot secretly implement M10 projection writes. M12 may independently develop scoped search/export from accepted G5 with its own branch and no overlap in EF snapshot, shared database model or Compose ports. If two streams need the same EF snapshot or permission files, assign a single integration author and use a short integration window. Keep the accepted execution graph unchanged.

## Current disposition

Planning and architecture are committed, not M9 application code. The proposed local contract awaits an explicit M9-01 design decision. No G9 tests have been executed, no G9 acceptance exists, and `main` remains the accepted G0-G8 baseline.
