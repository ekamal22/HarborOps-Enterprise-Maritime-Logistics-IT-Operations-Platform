# M13 Operational KPI Implementation Plan

**Current source-locked status (2026-11-09):** [Combined expanded M13/M15 run 42541135825](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) and [targeted run 42541135788](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135788) PASS at `6b05d84bf2f01741b182669c2dd17e39e63d9dda` (312 unit, 52 contract, 37 PostgreSQL and 27 browser). The newer independent M15 [42545005498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42545005498) passed source/DB, Chromium 7/7 and Firefox 7/7, but failed WebKit 1/7 because the expected live notice was absent. This latest M15 SHA and the independent M13 report-member SELECT RLS are not included in the green combined SHA. G13/G15 acceptance pending; no merge/deployment. [Combined evidence](M13_M15_COMBINED_VERIFICATION_20261109.md).

**Date:** 2026-11-08
**State:** Application source implemented; M13 independent targeted, scale, mutation and fault supplements and combined targeted/expanded technical checks executed. Comprehensive G13-S01 through S24 and independent S25 acceptance pending.
**Accepted baseline:** main eb529368a2447061fc1838cd3967b0ffedae669d
**Planning branch:** plan/m13-operational-kpi-dashboards-20261108
**Dependencies:** G11 and G12 accepted; [D-005 Option A approved](M13_D005_POLICY_APPROVAL_20261108.md) with safeguards. M13 application implementation was separately authorized and is authored on this branch; [actual source register](M13_IMPLEMENTATION_STATUS_20261109.md). Technical evidence and G13 acceptance remain pending. M15 may progress independently.

## 1. Scope, completion definition and decision gates

Outcome: an organization-scoped KPI dashboard with explainable throughput and delay figures, documented exact definitions, time windows, source freshness and drill pages that reconcile to underlying permitted source records.

Requirements: FR-012 and FR-002; NFR-007 and NFR-008. The accepted role matrix, M11 local rule contract, M12 search semantics, M16 audit and M9 operating restriction remain binding.

Canonical execution graph sequence:

| Work package | Depends on | Target deliverable | Exit evidence |
| --- | --- | --- | --- |
| M13-01 Finalize KPI catalog and cutoffs | Accepted G11; D-005 Option A approved | Reconcile approved m13-kpi-v1 policy to source-backed fixtures, Auditor grant and resource bounds | Recorded approval plus independently reconciled fixture arithmetic and remaining contract decisions |
| M13-02 Implement aggregate query services | Approved M13-01 and accepted G12 | Tenant-safe SQL/EF aggregate services, API, report/member snapshot, scoped authorization and measured query plans | Unit, contract, actual PostgreSQL/HTTP and real-role denial tests |
| M13-03 Build dashboard visualizations | Approved M13-01 | React cards, trends, coverage, filter state and snapshot-bound drill-through | Three-engine browser journeys and UI state controls |
| M13-04 Verify dashboard correctness | M13-02 and M13-03 | Complete source-locked G13-S01..S24 technical campaign and consolidated evidence | Exact reconciliation, security, migration, concurrency and failure evidence |
| G13 | All four packages | Independent S25 review and explicit milestone decision | Source-locked evidence package, separate policy and acceptance decisions |

M13-02 and M13-03 can be developed in parallel after the approved M13-01 contract is frozen, but shared API/DTO versions must match. No M13 branch is ready to merge before the separate G13 acceptance.

**Do not implement:** M14 maps, M15 notification delivery/badges, M17 platform telemetry, M18 incident workflows, predictive ETA, ARRI/DEPA backlog processing or unapproved changes to existing movement/source rules.

## 2. M13-01: policy, fixture and implementation contract

1. Apply the recorded [D-005 Option A approval](M13_D005_POLICY_APPROVAL_20261108.md) to source-backed fixtures, requirements, domain rules and permissions. Only the D-005 decision row is reconciled on this branch; the canonical execution graph and unrelated shared documents remain unchanged.
2. Decide strict on-time versus grace-tolerant recorded arrival. Keep distinct recorded event, schedule-unconfirmed and risk labels. Freeze every catalog entry's source class, eligibility, numerator, denominator, missing/stale behavior, archived/cancelled handling, rule version and output precision.
3. Prove whether a planned arrival recorded today is an applicable historical plan for a completed M5 workflow. Verify from accepted M5 migration and API update policy, including legacy upgrades. If not provable, choose an explicit unknown classification or an approved new plan-history fact before reporting historical accuracy.
4. Freeze filter set: required UTC half-open window or seven-complete-day default, maximum 366 days, origin/destination port, current voyage as of evaluation, optional business source, day/week bucket. Do not reuse M12 arbitrary text search in expensive aggregate paths without fresh index evidence.
5. Use the approved repeatable-read overview with bounded committed report members. Measure and finalize the provisional ten-minute TTL, retention, per-tenant/report limits and atomic refusal mode. Define owner-only and live-revocation protections. A live-only alternative requires a new policy decision.
6. Apply the approved typed permission: Analyst/Manager scoped allow, Auditor only with an explicit active scoped read-only grant, Administrator/Support deny. Choose and independently test its grant storage and revocation mechanism.
7. Freeze response DTOs and error codes with sample values for all K01-K14 and report-drill token field binding. Maintain M11/m13 policy version separation.
8. Build a deterministic test fixture manifest with one tenant A and one tenant B, IDs, timestamps relative to a captured database UTC reference, role assignments, accepted M5 status transitions, M6 association intervals, M10 revisions/corrections and M11 rule occurrences. Include exact numeric totals, source-row membership and graph bucket expectations, rather than counts only.

M13-01 exit: D-005 policy approval is recorded, but source-backed fixture membership, historical plan provenance, grant mechanism and measured limits still require closure. No M13-01 or G13 scenario may be marked PASS based on documentation alone.

## 3. M13-02: backend work breakdown

### 3.1 Domain and contract layer

Proposed new files:
- src/HarborOps.Domain/Reporting/OperationalKpiCatalog.cs with m13-kpi-v1 descriptors, source/denominator types, rule version validation and presentation precision rules.
- src/HarborOps.Domain/Reporting/OperationalKpiFilters.cs with strict paired UTC windows, allowlisted filter and bucket values, deterministic canonical normalization and SHA-256 digest.
- tests/HarborOps.UnitTests/OperationalKpiPolicyTests.cs for denominators, zero/null/rounding, first-event dedupe, 60-minute and one-microsecond cutoffs, scope filters and stale classifications.
- tests/HarborOps.ContractTests/OperationalKpiContractTests.cs for DTO shape, documented errors, token tampering/expiry and compatibility with accepted M12 search contract.

Treat published KPI codes as stable. Changing formulas after clients depend on them requires a version bump. Do not expose user-authored SQL, expression strings or arbitrary grouping columns.

### 3.2 Persistence and SQL

Proposed new files:
- src/HarborOps.Infrastructure/Reporting/OperationalKpiStore.cs for one-transaction result generation and member materialization.
- src/HarborOps.Infrastructure/Reporting/OperationalKpiReportStore.cs for stable scoped member paging, TTL and cleanup.
- One additive EF migration and model snapshot update, with final migration timestamp assigned only after M15 coordination.
- Corresponding read-model/entity mappings in HarborOpsDbContext.cs only for new derived report tables, plus index/migration tests. Existing 1-15 migrations remain untouched.

Build each metric from tenant-scoped source sets: M5 current shipment, immutable first Arrived/Completed/Cancelled histories, M6 current assignment intervals, M10 input version/projection freshness and M11 current rule checkpoints plus distinct occurrences. Use conditional aggregates and distinct keys, not unconditional shipment/history/exception multi-joins. Ensure filtered series equal the same member sets as headline counts.

A materialized report must atomically persist summary, member rows, policy/filter hash, UTC cutoffs and expiry. Keep member tables free of source payloads, Manager reasons or actor IDs. Enforce scoped foreign keys and uniqueness, retention and size controls. Avoid a long-held database transaction across browser HTTP calls. Reject over-limit reports without partial data. Never alter source entities to store a report total.

SQL privilege design: reproduce the accepted actual restricted harborops_api role and M11 protected read-scope/RLS behavior. Design an organization-scoped constrained read function or another security-reviewed path, with explicit GRANT/REVOKE; do not elevate the runtime role or modify M11 security policies for convenience. Validate direct SQL foreign-org and wrong-role access, not only endpoint denial.

### 3.3 Authentication and API

Proposed file: src/HarborOps.Api/Reporting/OperationalKpiEndpoints.cs. Register services/endpoints in Program.cs and dependency injection by minimal additive edits.

- GET /api/v1/organizations/{organizationId}/analytics/catalog: versioned allowlisted formulas and source limitations.
- GET /api/v1/organizations/{organizationId}/analytics/overview: normalized filters, exact integer metrics, denominators, status series, rule/input freshness, transaction as-of instant and protected report token.
- GET /api/v1/organizations/{organizationId}/analytics/reports/{reportId}/members: exact snapshot-member keyset paging with live reauthorization and session-bound protected token.

At the original 2026-11-08 planning checkpoint, the proposed analytics/overview route was not yet implemented. The subsequent M13 implementation contains the API route. Preserve its route namespace where compatible; confirm exact contract under M13-01.

Add OrganizationPermission.ViewOperationalKpis without granting it to every Auditor. If an explicit grant table is needed, add the smallest scope-locked schema and grant/revoke handling under separate reviewed ownership. Reuse existing selected organization and protected-cookie session middleware. Require active org, user, membership, unchanged security version and read-only dashboard grant at transaction and subsequent member request. For direct detail, preserve M5/M11 per-resource authorization.

Negative contract: reject unknown or repeated query keys, wrong UTC offsets, oversized strings, foreign UUIDs, forged report tokens, another actor/session, wrong role, expired membership, suspended tenant, oversized report requests and invalid keyset. Sanitize 400/401/403/404/409/429/503. Every response no-store; no report-sensitive data in logs. Client cancellation must release DB activity, locks and report-build reservations.

### 3.4 Performance and reliability

First measure accepted baseline query plans and table sizes on a disposable copy. Then compare (a) raw scoped aggregation per request, (b) query decomposition with index-supported CTEs, and (c) compact report membership materialization. Record CPU, buffers, temp files, concurrency, snapshot rows written and cleanup, not just elapsed time.

Inspect existing indices before any new index: M5 has organization/status and organization/record-status indexes; M11 has organization/lifecycle and organization/shipment indexes; M6 has assignment lookup indexes. Candidate new event-window and exception-condition composite indices require EXPLAIN evidence and migration impact review. Do not assume accepted M12 large-tenant text scanning was fixed.

Choose a bounded database statement timeout, overall request budget, report size quota and expiry with the reverse proxy's 30-second read envelope in mind. On serialization failure use a tightly bounded retry, never a non-idempotent partial report; on timeout or audit/reporter storage outage fail closed with rollback. Measure two API instances if per-instance throttling is used and clearly document its scope. No writes to accepted business tables.

M13-02 exit: real PostgreSQL repeatable-read correctness, scope/permission denial, exact fixture output, stable report-member pagination, no partial committed reports, index plans and acceptable measured operating envelope.

## 4. M13-03: frontend work breakdown

Proposed new files:
- web/src/analytics/kpiClient.ts: typed endpoint contracts, query normalization, no-store requests, abort and report-expiry handling.
- web/src/analytics/OperationalDashboard.tsx: role-gated scoped workspace, KPI cards and status distribution, trends, date/bucket/port/voyage filters, unknown/coverage banner, as-of labels and drill controls.
- web/src/analytics/KpiDrillPanel.tsx: fixed report-members paging, authorized detail links, exact numerator reconciliation, empty/expired state and refreshed-data disclosure.
- browser test source under tests/browser/specs/operational-kpis.spec.mjs, with seeded fixtures rather than mocked-only arithmetic.

Mount under the existing selected-organization workspace, not a new top-level app shell. Preserve auth and org state ownership in AuthPanel and OrganizationWorkspace, M12 ShipmentWorkspace search/export, M11 ShipmentExceptionPanel and other accepted components. Do not allocate top-level nav, notification bell, badge, unread state or shared global store without coordinated M15 agreement.

Visual spec: a high-contrast, information-dense but readable enterprise overview; show numerator and denominator next to percent; use labels Recorded late, Schedule overdue (unconfirmed), Departure slippage risk and Equipment conflict instead of a misleading combined physical-delay label. Show count and source status for Unknown/Stale. All charts must provide visible bucket names and precise filtered count; clickable items lead to report members, then authorized shipment/exception detail.

Treat loading, zero data, zero denominator, partial coverage, permission denied, account revoked, expired report, request timeout, transient outage and report too large as distinct states. Clearing the selected org must abort requests and wipe all report references. Ignore stale responses after a later organization or filter selection. A drill page must never show a blended cross-report list.

M13-03 exit: scoped journey and navigation in Chromium, Firefox and WebKit; totals and bucket labels match backend response; browser session/org-switch, denial and error cases pass; no M15 overlap.

## 5. M13-04: verification, reconciliation and review

Follow docs/M13_VERIFICATION_PLAN.md exactly, with G13-S01..S25, a frozen candidate SHA, known fixtures, actual restricted PostgreSQL role, real API and browser tests, negative and fault coverage, source-level mutation kills and independently rechecked raw evidence.

Stages:
1. Source review, clean Release build with warnings treated as errors, unit/contract and web build. Cross-check M11/M12 accepted contracts. Stop on failures before expensive stages.
2. Ephemeral GitHub Actions runners with exclusive disposable PostgreSQL fresh and populated accepted-15-migration upgrades, real-role HTTP/SQL security, fixed fixture totals, equality cutoff, duplicate/correction and rule-history tests.
3. Concurrent update, tenant suspension, membership revocation, session logout, report expiration, cancellation, outage/restart and cleanup. Test no partial or stale leaks.
4. Real 170,000-row skewed-tenant query plan and report build budget; repeat for empty and medium tenant. Measure actual SQL and HTTP path including proxy envelope.
5. Three-engine browser flow including M5/M11/M12 regressions and M15-neutral navigation. Use independently isolated browser ports and profiles.
6. Freeze source and manifest exact commands, tool versions, DB snapshot, policy version, SQL plans, TRX/JUnit, browser reports, mutation provenance, failures and SHA-256. Independent S25 reviewer separately verifies raw evidence. Only then request explicit G13 acceptance.

No past G11/G12 PASS can be copied into G13 results. GitHub Actions is the **exclusive permitted environment for future M13 verification after separate M13 implementation authorization**. Do not start workflows now or substitute local test execution. Source review is not runtime performance evidence.

## 6. Parallel M15 interface and migration coordination

The M15 session independently owns notification delivery, deduplication, read/unread state, bell and recipient navigation. M13 owns analytics endpoints, reporting SQL, catalog policy, report snapshot storage and dashboard leaf components only.

Required coordination before shared-file edits:

| Shared surface | Risk | Proposed boundary |
| --- | --- | --- |
| OrganizationPolicy.cs | New dashboard permission or Auditor grant conflicts with notification read permission | Review policy changes jointly; never broaden unrelated grants |
| Program.cs and DependencyInjection.cs | Registration and endpoint ordering | Minimal additive blocks from each milestone; integrated test after rebasing |
| OrganizationWorkspace.tsx / App.tsx | Shared navigation, org-switch cleanup, state overlap | M13 child dashboard; M15 owns alert entry point; jointly agree nav slot |
| HarborOpsDbContext.cs / EF snapshot | Competing model changes | Migration coordinator assigns IDs and integrates snapshot only once |
| SQL grants, RLS, signed helpers | Cross-role permission leakage | Keep each module's functions and keys namespaced, restricted runtime proof |
| docs/API_CONTRACTS.md, ARCHITECTURE.md, SECURITY.md, ROADMAP.md, execution graph | Concurrent edits to living docs | Scope M13 planning docs now; integrate shared docs only in a separately authorized reconciliation |
| Browser fixtures | Shared organization/session fixture and selectors | Separate fixture seeds and names; rerun combined critical journeys after integration |
| Deep links | Notifications may target analytics | Proposal only, no assumed route/contract until both milestone owners agree |

M15 notification read/unread/delivery state is never an M13 numerator, denominator or freshness source. Do not infer an accepted M15 contract from its planning branch.

## 7. Risk register and exit criteria

| Risk | Impact | Mitigation and evidence |
| --- | --- | --- |
| Deviation from approved D-005 rules | Policy mismatch or misleading metrics | Enforce approved Option A, require source-proven arrival plans and demand a new versioned decision for formula changes |
| Missing historical plan values | False on-time percentages | Prove plan immutability or exclude unknown, add new plan-version storage only by approval |
| Stale/default-off M10 and M11 | False zero conflict or risk | Coverage and freshness warnings; no prediction; opt-in processing not enabled by reporting |
| M11 protected SQL read policy | Direct SQL leak or empty unreadable aggregate | Restricted-role and RLS testing, constrained signed report-read authority |
| Concurrent source changes | Aggregate and drill disagreement | MVCC transaction plus bounded report-member snapshot; session-bound cursor and reauth |
| Materialization cost at large tenant | Resource exhaustion and timeout | EXPLAIN, bounded quotas, alternative encoding, measured cleanup, no partial totals |
| Auditor implicit data access | Unauthorized business visibility | Dedicated typed permission with explicit read-only grant and five-role negatives |
| Parallel M15 migrations/UI | Broken migration or navigation | Shared contract coordination and source-locked combined acceptance work |
| API/proxy timeout | Partially served dashboards or stalled connections | Safe pre-response build, cancellation/rollback and Nginx/Kestrel fault tests |
| Historical checkpoint limitations | Unsupported upgrade confidence | Fresh and populated 15-to-next migration tests; retain accepted data untouched |

**Delivery status (2026-11-09):** D-005 Option A and application implementation are authorized. The [implementation register](M13_IMPLEMENTATION_STATUS_20261109.md) supersedes earlier proposed file names where implementations were consolidated. Real-role tests, scale/fault campaigns, source-locked evidence and independent review are pending. GitHub Actions is the exclusive verification environment. G13 acceptance, merge, release and deployment remain unauthorized.
