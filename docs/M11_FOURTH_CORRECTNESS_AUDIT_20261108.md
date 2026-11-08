# M11 Fourth Source Correctness Audit and Remediation

**Reviewed:** 2026-11-08
**Branch:** `feat/m11-delay-exception-lifecycle`
**Accepted baseline:** protected `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`
**Initial fourth-pass candidate:** `af65b7dbfc676f3dd9446cfb1aa7f5956492c4e7`
**Fourth-pass remediation checkpoint before this register:** `a72bbd106b4cda42a7e01401a1f768bda04b1226`
**Earlier audits:** [first](M11_CORRECTNESS_AUDIT_20261107.md), [second](M11_SECOND_CORRECTNESS_AUDIT_20261107.md), [third](M11_THIRD_CORRECTNESS_AUDIT_20261108.md)
**Disposition:** Confirmed source defects corrected; executable verification is NOT EXECUTED and G11 acceptance pending. This record is not merge, release or deployment authorization.

## Scope and verification limits

Inspected the accepted M5 recorded workflow, M9 processing hold, M10 projection lock ordering, M16 audit boundaries and M11 domain policy, SQL-signed evaluation/action functions, API requests, frontend state, cursors and test source. Cross-checked the candidate against its planning, security and G11 contracts.

The review was limited to repository-source inspection and deterministic source-contract checks. This environment has Node and Git, but does not have a local HarborOps checkout or the required .NET SDK, PostgreSQL server, Docker and browser fixture runtime. Repository access was through the GitHub connector; direct GitHub cloning from this environment could not resolve the host. No claim of compiled, migrated, executed SQL, passing browser tests or independent G11 evidence is made.

## Findings and corrections

| ID | Severity | Finding | Source correction | Evidence |
| --- | --- | --- | --- | --- |
| F11-031 | Medium | The protected exception-list cursor stored only the last exception ID and tenant/filter tuple. New occurrences could be admitted partway through pagination even though the architecture called for an initial browse cutoff. | Added a PostgreSQL-clock `SnapshotAtUtc` to protected `ExceptionCursor.v2`, returned it from the store, and enforced `first_detected_at_utc <= snapshot` on every page. Existing rule-precedence-plus-UUID ordering remains unchanged. Added a create-between-pages regression. | Authored; NOT EXECUTED |
| F11-032 | High | The exception detail capped immutable history at 500 entries and provided no way to retrieve the remainder, preventing full review of retained lifecycle evidence. | Added a tenant-scoped `HistoryPageAsync` using the immutable `(occurred_at_utc,id)` keyset, a separately protected 20-minute organization-and-exception-bound history cursor, and `GET /exceptions/{exceptionId}/history` with a bounded limit. Detail now supplies a continuation token when its initial 500-entry preview is truncated. | Authored; NOT EXECUTED |
| F11-033 | Medium | Read-only Analyst privacy redaction had been applied to the initial history preview but did not cover a history continuation route because that route did not exist. | Applied the same `reason` and `actorUserId` redaction to every history page after live organization authorization. Added synthetic 506-record pagination and role-boundary integration source. | Authored; NOT EXECUTED |
| F11-034 | Low | Long-history frontend display had no continuation state or protection against a late page response after detail selection changed. | Added an explicit Load more history action, guarded state updates by the selected detail request serial, deduplicated immutable entry IDs and invalidated outstanding requests on close/refresh. Added a browser fixture. | Authored; NOT EXECUTED |
| F11-035 | Medium | The source-checker and living documentation still expressed the 500-entry preview as a permanent limit and omitted creation-cutoff rules. | Extended the M11 source-contract assertions and reconciled the current API, security, architecture, operations and test documents. Preserved earlier audits and gate decisions as historical records. | Authored; checker NOT EXECUTED |

## Precision of the new cursor contracts

- Exception-list cursors exclude occurrences whose `first_detected_at_utc` is after the initial database-clock cutoff. **This is not a frozen PostgreSQL MVCC snapshot of mutable lifecycle fields**. State filters and displayed versions can change between HTTP requests. Pagination must be rerun after a lifecycle change if a transactionally consistent status list is required.
- Exception-history rows are append-only and ordered by `occurred_at_utc,id`. The initial detail includes up to 500 earliest rows, reports truncation and supplies `nextHistoryCursor`. The dedicated history route returns up to 100 additional rows and a further cursor when needed. Appended history can become visible during a long browse; this is not a time-frozen export.
- Both cursor types have separate DataProtection purposes, organization/exception or organization/filter binding, bounded token length, 20-minute expiry and failure on tampering. The API rechecks selected organization and permitted role on every request.
- History pagination does not change M16 audit tables, approved business-role grants, immutable source evidence or the accepted M9 worker.

## Remaining blockers

1. **G11-S01 BLOCKED** on proposed M11-P01 through M11-P12 decisions. In particular, recurrence for a previously acknowledged and cleared occurrence that becomes active before reviewed resolution remains a policy decision. Do not imply the implementation resolves all recurrence cases until this is decided.
2. **G11-S02 through G11-S24 NOT EXECUTED:** run .NET 10 compilation, Node checks, fresh and populated migration 14, real restricted database logins, HMAC NULL/precision attacks, signed source races, audit atomicity, keyset pagination including >500 records, frontend/browser fixtures, relevant accepted milestone regressions, sustained scan/restart/performance and security scans.
3. **G11-S25 NOT EXECUTED:** independent reconciliation of exact-source raw evidence and explicit acceptance remains required.
4. **Conflict-specific evidence lineage:** a projection digest and small container observation sample do not fully identify all conflicting source events and containers. Proposed P03 evidence completeness remains incomplete.
5. **Inherited broad API SQL read permissions:** the new endpoints enforce selected organization in application queries but direct restricted-role database SELECT does not provide independent row-level tenant segregation. Reassess this trust boundary before G11 authorization.
6. **History and list consistency:** immutable history is fully pageable as committed source, but consistency under concurrent inserts and role/session revocation needs executed tests. List creation cutoff does not freeze lifecycle updates.
7. **No source-locked runtime evidence:** the local focused verification runner is unexecuted here. It is not a substitute for the full G11 matrix or independent review.

## Verification plan

1. Freeze candidate SHA and clean E:-backed checkout, record tool versions, database snapshots and exact migration head.
2. Run `node scripts/check-m11-contract.mjs`, graph and planning checks, warning-as-error .NET build, focused unit tests and front-end typecheck.
3. Apply migration 14 on an empty and a populated accepted M10/G16 database. Verify PostgreSQL objects, SECURITY DEFINER grants, search path and downgrade protections.
4. Exercise the 25 G11 scenarios and new F11-031 through F11-035 regression cases. Run actual restricted-role HMAC, history privacy and concurrent snapshot behavior; preserve failures and reruns.
5. Confirm accepted M5/M9/M10/M16 regressions and unchanged M9 ARRI/DEPA hold, cap and null ETA. Run browser engines and relevant security/mutation scans.
6. Hash source-locked raw evidence, reconcile discrepancies independently, record local-policy decisions and seek explicit G11 gate acceptance.

## Preserved governance

This work remains on draft PR #31. Protected `main` is unchanged and G0-G10 plus G16 are the accepted baseline. No GitHub Actions were run or enabled, and no merge, deployment, release or destructive cleanup occurred. The M9 sustained transport hold remains in force: ARRI/DEPA are parked, the 10,000 unresolved-work cap remains binding and M10 ETA stays null.

**Result:** Fourth source-level remediation delivered. **G11 acceptance pending.**
