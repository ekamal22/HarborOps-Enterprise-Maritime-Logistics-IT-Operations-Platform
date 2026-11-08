# M13 Operational KPI Planning Status

**Date:** 2026-11-08
**Milestone:** M13 Operational KPI Definitions and Dashboards
**Status:** D-005 OPTION A APPROVED WITH SAFEGUARDS; M13 application implementation NOT STARTED OR AUTHORIZED; G13 verification NOT EXECUTED and G13 NOT ACCEPTED.
**Accepted main verified:** eb529368a2447061fc1838cd3967b0ffedae669d
**Planning branch:** plan/m13-operational-kpi-dashboards-20261108
**Scope:** Only M13 planning documentation. No source implementation, EF migration, workflow execution, shared project documentation edits, merge, release or deployment.

## Published planning package

| Document | Purpose |
| --- | --- |
| [M13 KPI policy and original proposal](M13_D005_KPI_DECISION_PROPOSAL.md) | Approved m13-kpi-v1 definitions and original alternatives, with operational limits still requiring measured evidence |
| [D-005 policy approval](M13_D005_POLICY_APPROVAL_20261108.md) | Explicit Option A approval, historical-data safeguards, open measured limits and exclusive future Actions verification boundary |
| [M13 architecture design](M13_ARCHITECTURE_DESIGN.md) | Actual M5/M6/M10/M11/M12 source mapping, tenant-scoped PostgreSQL query/read authority, report snapshot and drill contract, API/UI ownership, privacy, query plans and migration strategy |
| [M13 implementation plan](M13_IMPLEMENTATION_PLAN.md) | Sequenced M13-01 through M13-04 deliverables, proposed files/contracts, source/DB/React tasks, risks and M15 handoff boundaries |
| [M13 verification plan](M13_VERIFICATION_PLAN.md) | Full G13-S01 through G13-S25 source-locked matrix, exact fixture reconciliation, actual restricted SQL, browsers, failures, 170k scale and independent S25 |

The current requirements/roadmap/execution graph already place M13 after accepted G11 and G12, with D-005 required before applicable implementation. These accepted dependencies are unchanged. The canonical living docs and generated execution graph have deliberately not been edited merely to signal planning.

## Repository inspection and source references

The verified main tree contains the accepted M5 shipment and workflow history stores, M6 assignment intervals, M10 tracking version and projection tables, M11 exception policy/evaluation/occurrence history, M12 live search/export and M16 audit boundaries. Relevant source includes:

- src/HarborOps.Infrastructure/Shipments/ShipmentRecord.cs, ShipmentStore.cs and ShipmentSearchStore.cs
- src/HarborOps.Infrastructure/Assignments/AssignmentRecord.cs and AssignmentStore.cs
- src/HarborOps.Domain/Exceptions/ShipmentExceptionPolicy.cs and src/HarborOps.Infrastructure/Exceptions/ShipmentExceptionStore.cs
- src/HarborOps.Infrastructure/Shipments/ShipmentTimelineStore.cs
- src/HarborOps.Domain/Organizations/OrganizationPolicy.cs and src/HarborOps.Infrastructure/Organizations/OrganizationStore.cs
- src/HarborOps.Api/Program.cs, Exceptions/ShipmentExceptionEndpoints.cs and Search/ShipmentSearchEndpoints.cs
- src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs and the accepted 15 migrations, ending at 20261107230000_M12ShipmentExportAudit
- web/src/App.tsx, web/src/auth/AuthPanel.tsx, web/src/auth/OrganizationWorkspace.tsx and web/src/shipments/ShipmentWorkspace.tsx
- docs/STATUS.md, ROADMAP.md, PRD.md, REQUIREMENTS.md, ACCEPTANCE_CRITERIA.md, DECISIONS.md, PARALLEL_DELIVERY_POLICY.md, ARCHITECTURE.md, DOMAIN_MODEL.md, API_CONTRACTS.md, SECURITY.md, TEST_STRATEGY.md, OPERATIONS.md, EXECUTION_GRAPH.md and the accepted G11/G12 decisions and closure.

The accepted G11/G12 closure explicitly preserves the M9 transport-event hold, default-off M10 projector and opt-in M11 evaluator. The accepted M12 cursor is a live search paging construct, not a source-consistent reporting snapshot. M11 four rule conditions require provenance and stale/unknown treatment. M11 exception data is protected by restricted SQL read-scope controls. These are implementation-critical constraints, not open permissions to change accepted source behavior.

## Work package readiness

| Work package | Status and next required action |
| --- | --- |
| M13-01 | **D-005 POLICY APPROVED**; fixture/source reconciliation, Auditor grant implementation and measured limits still outstanding |
| M13-02 | Architecture and SQL/API work breakdown prepared; **NOT STARTED**; dependent on approved M13-01, permission/SQL-read decisions and G12 accepted baseline |
| M13-03 | UX/React and API contract plan prepared; **NOT STARTED**; dependent on approved M13-01 and coordinated selected-organization navigation boundary |
| M13-04 | 25-scenario evidence matrix prepared; **NOT STARTED**; requires integrated M13-02 and M13-03 source |
| G13-S01 | **POLICY APPROVED, RECONCILIATION PENDING**; do not mark PASS solely from the approval |
| G13-S02..S25 | **NOT EXECUTED**, including independent S25; no runtime or policy PASS claimed |
| G13 | **NOT ACCEPTED**; no gate decision or implementation authorization implied by planning publication |

## Approved D-005 rules and remaining measurements

1. Use the approved fourteen-metric m13-kpi-v1 catalog; distinguish workflow-recorded arrival lateness, schedule overdue with arrival unconfirmed, forward-looking departure slippage risk, and equipment observation conflict.
2. Keep separately displayed strict-on-time and accepted M11 60-minute grace-tolerated arrival percentages. Only count source-proven historical plans; missing authoritative plan attribution is unknown, never fabricated.
3. Use UTC half-open windows, seven complete UTC days by default, maximum 366-day explicit range, rule-versioned deterministic fixtures and exact integer source rows with separately rounded percentages.
4. Use REPEATABLE READ overview with a bounded, short-lived server-owned membership snapshot, protected actor/session/org/filter-bound token and live authorization on every drill page. M12 search cannot substitute for this.
5. Report missing/unassessed M11 and stale M10 data visibly; do not count it as clear or predict ETA, nor process parked ARRI/DEPA receipts.
6. Introduce a typed dashboard-read permission and an explicitly granted read-only Auditor policy; do not widen Analyst/Manager, Support or Administrator rights by accident.
7. Treat 10-minute report TTL as a provisional target; finalize storage, quotas, retention, API budgets, indexes and performance thresholds from measurement. Historical arrival eligibility is source-proven or unknown and excluded.

The reference fixture is an approved test target, not executed evidence. Its exact source rows, members and denominators must still be independently reconciled and later tested through GitHub Actions alone.

## M15 coexistence and integration questions

M15 is planned independently on its own branch. M13 owns only reporting definitions, /analytics namespace, report storage and dashboard leaf UI. M15 owns notification recipients, deduplication, read/unread, delivery state and badges. KPI figures do not consume notification state.

**Shared decisions needed before implementation:** OrganizationPolicy permission additions and Auditor-grant representation; Program.cs/dependency injection registration; EF model snapshot and migration timestamp order after the accepted fifteen migrations; security-definer/grant/RLS reviews; selected-organization workspace navigation and state cleanup; API/UX contract coordination for any future notification-to-dashboard link; combined browser and restricted-role regressions on a unified candidate. Do not independently edit M15 documents or accept its proposed contracts.

## Open blockers and risk acceptance boundaries

- **Policy evidence:** D-005 Option A is approved; authoritative historical plan applicability and fixture membership remain to be proven, not inferred.
- **Security:** No currently accepted dedicated dashboard permission or Auditor dashboard grant store exists. M11 RLS/read-scope enforcement requires a carefully constrained report read path and real restricted-login tests.
- **Consistency:** The snapshot/member reconciliation approach is approved; encoding, storage/retention bounds and behavior under concurrent changes still require measured validation. Live M12 search cannot substitute for report-member snapshots.
- **Performance:** 170,000-row M12 big-tenant text search was accepted with sequential scans; M13 aggregate plans, quotas, cleanup and multi-instance bounds are unmeasured.
- **Parallel delivery:** M15 migration IDs, shared React navigation and DB privilege changes require a cross-branch contract agreement.
- **Acceptance:** G13 source, executed tests, independent raw evidence review and explicit approval do not exist yet.

## Handoff and recommended next decision

**Planning deliverable:** Approved Option A policy and implementation/verification specifications. Source fixtures, permission contracts, retention/capacity and M15 coordination still require closure before implementation authorization.

**Next action:** Reconcile approved formulas against source/fixture evidence, finalize Auditor-grant implementation, prove historical arrival-plan provenance and measure resource bounds. Coordinate M13/M15 migration, permissions and navigation. GitHub Actions is exclusively authorized for future M13 verification after separate application implementation authorization; no workflow may run in this documentation-only turn. No merge, deployment, production migration or G13 acceptance is authorized.
