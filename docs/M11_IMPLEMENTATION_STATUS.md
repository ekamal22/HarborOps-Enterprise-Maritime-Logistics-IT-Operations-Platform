# M11 Source Implementation Status

**Current source audit (2026-11-08):** [Third M11 correctness audit](M11_THIRD_CORRECTNESS_AUDIT_20261108.md) records corrected defects and unresolved verification, local-policy and security boundaries. Runtime execution is not verified and G11 acceptance pending. The accepted M9 transport hold is unchanged.

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

## Unexecuted checks and known limitations

- The current environment has no .NET 10 SDK, PostgreSQL, Docker or browser fixture runtime, and cannot download the public repository from GitHub. No compilation, database migration, contract tests, integration tests, browser tests or mutation campaign was executed here.
- G11-S01 is **BLOCKED** until local policy approval; G11-S02 through G11-S25 remain **NOT EXECUTED**. This source audit is not evidence of PASS. Source existence and static review are not evidence of PASS.
- Before G11 submission, run `scripts/verify-g11-local.sh` with disposable E:-backed storage and `HARBOROPS_TEST_DB`; extend it with the full G11 verification matrix, source-locked raw evidence, all required regressions, security mutation controls, image/dependency scans and independent S25 review.
- SQL semantics, transactional security, storage/backpressure limits, audit failure atomicity, true populated-M10/G16 migration safety and restart behavior require execution and independent security review. Conflict-specific container provenance and full history pagination remain incomplete.
- The proposed automatic lifecycle resolves a condition on confirmed clearance, while manual resolution requires a cleared condition. The precise disposition policy remains subject to M11-P10 approval.
- The signed evaluator shares the protected M10 verifier secret with a separate M11 message domain; key rotation and restricted SQL-login tests require real-stack verification.
- The background evaluator is opt-in and uses bounded keyset sweeps, but operational throughput, due-time accuracy under sustained load and failure recovery are not yet verified.

## Unchanged accepted constraints

The M9 sustained transport-event hold remains. ARRI/DEPA remain parked, the 10,000 unresolved-work-item cap is binding, and M10 ETA is null. M11 does not implement transport backlog relief, predicted ETA, M13 KPIs or M15 notifications. No GitHub Actions execution, release, public hosting, production deployment or merge is authorized.

**Disposition:** Implementation source candidate delivered on an isolated feature branch; executed correctness verification, policy approval, complete G11 verification and explicit acceptance pending.
