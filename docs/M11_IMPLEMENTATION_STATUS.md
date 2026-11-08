# M11 Source Implementation Status

**Current verification policy (2026-11-08):** [M11/M12 verification operations](M11_M12_VERIFICATION_OPERATIONS_20261108.md) now requires manual, coordinated, source-locked workflows instead of automatic push-triggered campaigns. [Run-by-run progress](M11_M12_VERIFICATION_PROGRESS_20261108.md) is the current verification checkpoint; older reports and source-only notes below are historical. G11/G12 independent S25 review and acceptance remain pending. Protected `main`, the M9 transport hold, and deployment restrictions are unchanged.

**Verification reconciliation, 2026-11-08:** The [M11/M12 source-locked evidence register](M11_M12_VERIFICATION_PROGRESS_20261108.md) records the prior complete 338/338 integration run, 291/291 unit, 46/46 contract, 19/19 G11, 4/4 G12, 36/36 browser tests, five valid security mutants killed and successful runtime-image scans on their recorded source commits. The expanded 340-case full integration run, populated 13-to-15 migration test and final source lock were pending at this checkpoint. Earlier 18/18, 36/36 and source-only statements below are historical. [M12 selected contract](M12_LOCAL_CONTRACT_DECISION_20261108.md) and [Q2 applicability](M12_Q2_HISTORICAL_SNAPSHOT_APPLICABILITY_20261108.md) are recorded. Independent G11/G12 S25 review, raw evidence package and gate acceptance remain pending, with `main`, the M9 operating hold and deployment boundary unchanged.

**Historical hosted verification checkpoint:** [Executed M11/M12 run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records a successful .NET and frontend source gate, G11 18/18 scoped PostgreSQL tests, G12 2/2 exclusive PostgreSQL tests, 36/36 impacted integration regressions, and direct SQL isolation of exception, history and evaluation tables with forged-scope denial. Updated source/dependency and runtime-image security scans passed. The three-engine browser rerun passed 36/36 tests, with 12/12 each in Chromium, Firefox and WebKit, including M11 exception journeys, M12 search/export and targeted M5 regressions. G11-S08 conflict-specific provenance remains open; M12 local policy decisions and independent G11/G12 S25 reviews remain pending. No G11/G12 acceptance, merge or deployment is authorized. Historical source-only notes below are retained as previous checkpoints.

**Earlier hosted verification checkpoint (before SQL remediation):** [Hosted run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records successful corrected-source checks, G11 17/17 and G12 2/2 targeted PostgreSQL tests, dependency and runtime-image scans, and a confirmed **G11-S12 SQL read-isolation FAIL**. The current HTTP database login can read M11 exception rows without scoped-session authority. The browser and full integration campaigns remain in progress at the recorded checkpoint. Independent G11/G12 gate reviews and acceptance pending.

**Historical combined candidate checkpoint (before hosted verification):** The isolated `feat/m11-m12-combined-integration` branch has been assembled from accepted `main` and both feature sources. [M11 local policies](M11_LOCAL_CONTRACT_DECISION_20261108.md) are now recorded, including clearance through Unknown/Stale and reviewed historical late-arrival resolution. This supersedes earlier pending-policy and no-combined-source snapshots in this document. Source-only corrections and authored tests are not executed evidence. G11/G12 technical verification, independent S25 review and acceptance pending. Protected `main`, M9 operating hold and deployment boundary are unchanged.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). M11 remains an independent candidate on draft PR #31. The combined verification strategy is approved but no combined source has been implemented, compiled or tested and M11 policy dispositions and G11 acceptance remain pending.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records draft M11 PR #31 alongside draft M12 PR #32, with different additive migration IDs and an unresolved shared API/UI/role integration boundary. Both are source candidates, not merged or runtime-verified; G11-S01 BLOCKED, G11-S02..S25 NOT EXECUTED, G12 technical verification pending. Accepted `main` and the M9 transport hold remain unchanged.

**Historical M11 source audit (before hosted verification):** [Sixth correctness audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) documents informational severity on inactive outcomes, versioned rule-metadata reconciliation and typed due-work continuation. The source changes and new tests are unexecuted. G11-S01 BLOCKED, G11-S02 through S25 NOT EXECUTED, acceptance pending. The M9 transport-processing hold remains in force.

**Date:** 2026-11-07
**Branch:** `feat/m11-delay-exception-lifecycle`
**Based on:** accepted `main` `6ead1d076946dd96b53401265a31efc35c4a666c`
**State:** Source candidate implemented, audited and remediated on the isolated feature branch. Compilation, SQL execution and G11 are NOT VERIFIED. Local policies M11-P01 to P12 are proposed and acceptance pending. This record is not gate closure or merge/deployment authorization.

## Implemented source candidate

| Surface | Files | Review boundary |
| --- | --- | --- |
| Pure rule policy | `src/HarborOps.Domain/Exceptions/ShipmentExceptionPolicy.cs` | UTC schedule overdue, recorded workflow late arrival, departure-slippage risk, and fresh M10 equipment conflict; explicit unknown/stale confidence |
| Persistence and database operations | Migration `20261107180000_ShipmentExceptions.cs` and `src/HarborOps.Infrastructure/Exceptions/ShipmentExceptionStore.cs` | Additive fourteenth migration, tenant FKs, append-only history/replay, source revisions, SQL-signed evaluator and Manager disposition functions |
| Proof boundaries | `M11ProofSigner.cs`, `AuditRoleProvisioner.cs` | Domain-separated HMAC proof, live Manager/session SQL checks for actions, no direct API-table mutation grants, atomic M16 audit |
| API and background processing | `ShipmentExceptionEndpoints.cs`, `ShipmentExceptionEvaluationService.cs`, `Program.cs` | Tenant-scoped list/detail, protected cursor, Manager-only evaluate/acknowledge/resolve, bounded optional evaluator disabled by default |
| Frontend | `ShipmentExceptionPanel.tsx`, shipment workspace and client wiring | Read-only role views and Manager-only actions with clear uncertainty and no ETA claim |
| Verification source | `ShipmentExceptionPolicyTests.cs`, `M11ExceptionRuntimeTests.cs`, `shipment-exceptions.spec.mjs`, `check-m11-contract.mjs`, `verify-g11-local.sh` | Unit, restricted PostgreSQL/HTTP, three-engine browser candidates and E:-backed focused local runner; none executed in this review |

The accepted thirteen earlier migrations remain unchanged. Historical migration test assertions were updated only where they observe current HEAD; assertions at a historical rollback checkpoint retain the historical count. The accepted G10 stack checker recognizes either the original 13-migration state or M11's additive 14-migration candidate.

## Candidate rule semantics, not approved

The policy proposes a 60-minute UTC grace period for arrival and departure. `ArrivalScheduleOverdue` means scheduled arrival overdue **without confirmed physical arrival**. `RecordedArrivalLate` compares a manual M5 Arrived transition to a schedule, not external verification. `DepartureSlippageRisk` is a recorded manual schedule-risk heuristic, not ETA. `EquipmentConflict` requires a fresh M10 observed conflict. Missing schedule, unknown tracking and stale materialization must never be represented as confirmed transport delay.

Automatic reevaluation is opt-in via `HARBOROPS_M11_ENABLE_EVALUATOR=true`; default Compose remains off. Manager can explicitly reevaluate in the UI with current authority. This does not activate the separate M9 worker or parked transport events.

## Source remediation (not executable verification)

The [first correctness audit](M11_CORRECTNESS_AUDIT_20261107.md) identifies and documents fixes for the M10 input-version column mismatch, terminal workflow risk, Manager resolution after clearance, archived acknowledgement cleanup, organization-wide replay-key uniqueness, bounded expiring nonces, lock ordering, fixed-size due candidate pages with rule-policy checkpoints, typed evidence, role-specific redaction, truncated-history disclosure, E:-backed failure evidence and stale documentation checks. These are committed source changes, not passed runtime tests.

## Second-pass source corrections

The [second correctness audit](M11_SECOND_CORRECTNESS_AUDIT_20261107.md) records fixes for no-op evaluation checkpoints, expired time-bound dispositions, refreshed M10 projection authority, restricted replay-table reads, exhausted serializable retries, PostgreSQL-clock proof expiry, full-range UTC arithmetic, microsecond due boundaries, chunked-body validation and UI detail-response races. Unit and database integration test sources were expanded. No executable tests were run here. Complete conflict-event attribution, history pagination and inherited SQL reader scope remain unresolved.

## Third-pass source corrections

The [third correctness audit](M11_THIRD_CORRECTNESS_AUDIT_20261108.md) records fixes for nullable signed SQL inputs, scoped rule-priority list/cursor ordering, concurrent evaluation unique-key races, Manager action conflicts, canonical microsecond `m11-eval-v2` proof timestamps and the undefined source-contract checker identifier. Direct restricted-role SQL, cursor and concurrency regression test sources were added, but remain unexecuted. Outstanding conflict lineage, full history pagination and broad API SQL read-scope questions remain open.

## Fourth-pass source corrections

The [fourth audit](M11_FOURTH_CORRECTNESS_AUDIT_20261108.md) records the database-clock `SnapshotAtUtc` exception-list cutoff with cursor purpose v2, and the append-only history continuation using `(occurred_at_utc,id)`, separately protected history cursors, a bounded organization-scoped history route, Analyst redaction, frontend Load more history and synthetic 506-entry regression source. The initial 500-history view is now a preview, not a retrieval ceiling. Existing lifecycle status fields may change between stateless exception-list pages, and newly appended immutable history can appear while browsing. No executable verification is claimed.

## Fifth-pass correctness remediation

The [fifth audit](M11_FIFTH_CORRECTNESS_AUDIT_20261108.md) records a signed-evaluator recurrence fix: verified inactive Acknowledged occurrences are system-closed before a new Active occurrence opens. Unknown/Stale does not prove clearance. The UI restricts Resolve to Acknowledged/Inactive, and ActionAsync classifies only the replay primary-key unique violation as an expected uniqueness conflict. Integration and browser regression sources are committed but unexecuted. Recurrence across uncertainty and permanently active RecordedArrivalLate remain policy blockers.

## Sixth-pass correctness remediation

The [sixth source audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) documents corrected rule severity for inactive outcomes, a stronger no-op predicate that compares persisted metadata rather than condition/fingerprint alone, and explicit UUID typing of due-work keyset bounds. Pure rule unit and PostgreSQL integration test source were added. Build, SQL and browser verification remain NOT EXECUTED, as do policy approval and independent G11 review.

## Unexecuted checks and known limitations

- The current environment has no .NET 10 SDK, PostgreSQL, Docker or browser fixture runtime, and cannot download the public repository from GitHub. No compilation, database migration, contract tests, integration tests, browser tests or mutation campaign was executed here.
- G11-S01 is **BLOCKED** until local policy approval; G11-S02 through G11-S25 remain **NOT EXECUTED**. This source audit is not evidence of PASS. Source existence and static review are not evidence of PASS.
- Before G11 submission, run `scripts/verify-g11-local.sh` with disposable E:-backed storage and `HARBOROPS_TEST_DB`; extend it with the full G11 verification matrix, source-locked raw evidence, all required regressions, security mutation controls, image/dependency scans and independent S25 review.
- SQL semantics, transactional security, storage/backpressure limits, audit failure atomicity, true populated-M10/G16 migration safety and restart behavior require execution and independent security review. Conflict-specific container provenance remains incomplete. Full history pagination is now implemented as unexecuted source; runtime coverage, concurrent append correctness and role grants require verification.
- The proposed automatic lifecycle resolves a condition on confirmed clearance, while manual resolution requires a cleared condition. The precise disposition policy remains subject to M11-P10 approval.
- The signed evaluator shares the protected M10 verifier secret with a separate M11 message domain; key rotation and restricted SQL-login tests require real-stack verification.
- The background evaluator is opt-in and uses bounded keyset sweeps, but operational throughput, due-time accuracy under sustained load and failure recovery are not yet verified.

## Unchanged accepted constraints

The M9 sustained transport-event hold remains. ARRI/DEPA remain parked, the 10,000 unresolved-work-item cap is binding, and M10 ETA is null. M11 does not implement transport backlog relief, predicted ETA, M13 KPIs or M15 notifications. No GitHub Actions execution, release, public hosting, production deployment or merge is authorized.

**Disposition:** Implementation source candidate delivered on an isolated feature branch; executed correctness verification, policy approval, complete G11 verification and explicit acceptance pending.
