# M11 Third Correctness Audit and Source Remediation

**Review date:** 2026-11-08
**Feature branch:** `feat/m11-delay-exception-lifecycle`
**Accepted baseline:** `main` `6ead1d076946dd96b53401265a31efc35c4a666c`
**Starting candidate:** `cfb723b60964fbd1d62762a80d7c97c5df3aa79c`
**Third-pass source checkpoint:** `c36a950a5d19e890d12c5045725bc94e6d85553c`
**Earlier reviews:** [first audit](M11_CORRECTNESS_AUDIT_20261107.md), [second audit](M11_SECOND_CORRECTNESS_AUDIT_20261107.md)
**Decision:** Source defects corrected as described below. Compilation and runtime correctness not verified. G11 acceptance pending.

## Scope and evidence standard

The third review compared provisional M11 SQL authorization, signed proof serialization, deterministic rules, database conflict behavior, list cursors, request validation, server-side role grants, response types, UI and test source against accepted M5 shipment state, M9 processing restrictions, M10 projection contracts, M16 audit persistence and the G11 specification.

This review used repository source and static cross-file inspection only. It did not execute `dotnet`, PostgreSQL, Docker, browser tests, migration rollback or a security mutation campaign. The presence of a test in source does not constitute an executed PASS.

## Third-pass findings

| ID | Severity | Source finding | Source remediation | Executable evidence |
| --- | --- | --- | --- | --- |
| F11-025 | High | SQL nullable required inputs could null-propagate through evaluation HMAC text construction. A NULL expected signature compared with a NULL supplied signature could bypass the intended cryptographic rejection path; a synthetic inactive evaluation could be written without a valid proof. | Both SECURITY DEFINER operations explicitly reject NULL required fields, NULL signatures and malformed signature digests before constructing/verifying proofs. Added direct restricted-role negative SQL tests. | NOT EXECUTED |
| F11-026 | Medium | The scoped exception API ordered by UUID even though the proposed M11-P09 presentation contract requires Conflict, RecordedArrivalLate, ArrivalScheduleOverdue, then DepartureSlippageRisk. Pages could present lower-ranked rules first. | Implemented stable rank-plus-ID ordering in the server-side organization-scoped query, with the protected next cursor using the identical composite order. Added an HTTP integration scenario crossing a page boundary. | NOT EXECUTED |
| F11-027 | Medium | Concurrent first evaluations may race on occurrence and per-rule evaluation unique keys. A 23505 unique violation was not handled by the serializable retry logic and could surface as HTTP 500. | Retry only known M11 occurrence or evaluation unique-key races alongside serialization/deadlock conflicts, with bounded retry exhaustion returning Conflict. Added a two-connection restricted-role concurrency test. | NOT EXECUTED |
| F11-028 | Medium | Human disposition transaction serializations/deadlocks (SQLSTATE 40001/40P01) were not mapped and could produce an unclassified HTTP 500. | Map these transactional collisions to a truthful 409 without claiming an action was committed. | NOT EXECUTED |
| F11-029 | High | Evaluation HMAC encoded UTC evidence/threshold/due timestamps only to milliseconds, while PostgreSQL persisted microseconds. A sub-millisecond modification could preserve the valid signature text. | Raised the evaluation signature domain to `m11-eval-v2`; both .NET and PostgreSQL now serialize signed timestamps as canonical Unix microseconds, including negative epochs. Added deterministic microsecond tests. Manager action domain `m11-action-v1` is unchanged. | NOT EXECUTED |
| F11-030 | Medium | The M11 static contract checker referenced an undefined variable (`roles`) instead of the provisioner source (`grants`), preventing the source check from completing. | Corrected the identifier and strengthened assertions for NULL authority checks, ordered keyset pagination, known concurrency collisions and microsecond signing. | Script itself NOT EXECUTED |

## Reconciled candidate contracts

- **Authority:** A database login cannot acquire evaluation or disposition authority through nullable signature arguments. Required fields are non-NULL, short-lived and cryptographically bound. M11 evaluation uses domain `m11-eval-v2` and exact database-storable microsecond timestamp units; M11 Manager disposition retains `m11-action-v1`.
- **Presentation:** The four rules can coexist. Server pages and cursors use proposed M11-P09 precedence without deleting lower-priority occurrences.
- **Concurrency:** Evaluation retry is bounded to SQLSTATE 40001, 40P01 or known M11 unique constraints; other integrity violations remain errors. Manager disposition collisions return 409, and audit/replay insertion remains in one SQL transaction.
- **Tests:** [M11 runtime tests](../tests/HarborOps.IntegrationTests/M11ExceptionRuntimeTests.cs), [proof unit tests](../tests/HarborOps.UnitTests/M11ProofSignerTests.cs) and [source-contract checker](../scripts/check-m11-contract.mjs) contain the additional assertions, but none was run in this review.

## Outstanding correctness blockers

1. **Executable integration gate:** M11 migration 14 has not been applied to either a clean or a populated accepted M10/G16 database in this audit. Effective grants, SECURITY DEFINER behavior, HMAC byte parity, nullable-input attacks, PostgreSQL row-valued keyset syntax and concurrent unique-race retry need real SQL execution.
2. **G11-S01:** Proposed M11-P01 through M11-P12 are not explicitly approved. Rule grace, recorded lateness, disposition of historic late-arrival conditions, precedence and user-role evidence policies must be decided before the gate.
3. **G11-S02 through G11-S24:** Build, all runtime/security/browser tests, relevant accepted-milestone regressions, performance, restart/outage exercises, three-engine browser checks, source-lock, mutations and image/dependency scans are NOT EXECUTED.
4. **G11-S25:** Independent raw-evidence reconciliation, discrepancy resolution and an explicit acceptance decision are NOT EXECUTED.
5. **Conflict-specific provenance:** An accepted M10 projection digest and revision do not by themselves provide every conflict-causing container and source event. Full lineage is not demonstrated.
6. **History access:** Only an explicitly truncated 500-entry history view exists, not full-history paging/export. Do not claim complete history retrieval.
7. **Reader trust:** Inherited broad SQL SELECT grants used by the API remain a residual defense-in-depth concern. API organization authorization does not itself establish database row-level security.
8. **Operational behavior:** Repeated no-op scans, sorted cursor stability under concurrent writes, nonce pruning, multiple replicas and evaluator restart have not been load-tested. The opt-in evaluator remains disabled by default.
9. **Parallel integration:** The M11 draft PR is isolated from M12 and documentation-planning PR #30. Resolve overlapping changes and run cross-milestone verification on the intended integration head before merging.

## Verification sequence

Use an E:-backed clean checkout and disposable local PostgreSQL database. Freeze the feature SHA; run .NET 10 build/test and source-contract/graph/planning checks first. Apply migration 14 to a fresh database and separately to a populated G10/G16 baseline. Test restricted SQL HMAC nulls and negative Unix timestamps, authority revocation, audit failure atomicity, concurrent insertion, 409 recovery, rule precedence across cursor pages, real M10 projector refresh, and M9 transport isolation. Follow with browser fixtures, relevant accepted regressions and independent source-locked G11 evidence review. Preserve failed attempts. The focused `scripts/verify-g11-local.sh` is a starting point, not a complete gate campaign.

## Preserved boundaries

Protected `main` and accepted M0-M10/G0-G10 plus M16/G16 are unchanged. The M9 transport-event processing hold remains; ARRI/DEPA stay parked, the 10,000 unresolved-work cap remains binding and M10 ETA is null. No GitHub Actions, merge, deployment, release, public hosting or destructive cleanup was performed.

**Third-pass verdict:** Confirmed source defects corrected. Runtime proof and independent G11 acceptance remain pending.
