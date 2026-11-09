# M13 Operational KPI Architecture Design

**Current source-locked status (2026-11-09):** [Combined expanded M13/M15 run 42541135825](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) and [targeted run 42541135788](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135788) PASS at `6b05d84bf2f01741b182669c2dd17e39e63d9dda` (312 unit, 52 contract, 37 PostgreSQL and 27 browser). The newer independent M15 [42545005498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42545005498) passed source/DB, Chromium 7/7 and Firefox 7/7, but failed WebKit 1/7 because the expected live notice was absent. This latest M15 SHA and the independent M13 report-member SELECT RLS are not included in the green combined SHA. G13/G15 acceptance pending; no merge/deployment. [Combined evidence](M13_M15_COMBINED_VERIFICATION_20261109.md).

**Date:** 2026-11-08
**Document scope:** Original 2026-11-08 design baseline. M13 is now implemented on its feature branch and has passed selected M13 and combined M13/M15 technical tests. This architecture document is not the acceptance ledger.
**Accepted source:** main eb529368a2447061fc1838cd3967b0ffedae669d
**Prerequisites:** G11 and G12 accepted; [D-005 Option A approved with safeguards](M13_D005_POLICY_APPROVAL_20261108.md). Implementation contracts and measurement remain open.
**Canonical work:** M13-01, M13-02, M13-03, M13-04 and G13.

## 1. Verified code and data inventory

The accepted repository is ASP.NET Core / EF Core / Npgsql with PostgreSQL migrations and a React/TypeScript frontend. Verified paths and contracts:

| Component | Accepted source and relevant fields | M13 ownership |
| --- | --- | --- |
| Workflow | src/HarborOps.Infrastructure/Shipments/ShipmentRecord.cs and ShipmentStore.cs; shipments, shipment_status_changes in 20261101180000_ShipmentLifecycle.cs | Read record_status, workflow_status, scheduled UTC values and immutable workflow transitions |
| Assignments | AssignmentRecord.cs and 20261102080000_ShipmentAssignments.cs; shipment_voyage_assignments starts_at_utc/ends_at_utc | Filter assigned voyage at report evaluation instant with tenant and interval bounds |
| Tracking | ShipmentTimelineStore.cs and 20261105180000_ShipmentTrackingProjections.cs; input revision/correction generation/policy_version/observed_state | Show quality/freshness only; no predictive ETA or physical movement |
| Exceptions | ShipmentExceptionPolicy.cs, ShipmentExceptionStore.cs and 20261107180000_ShipmentExceptions.cs | Use four accepted m11-rules-v1 conditions, occurrence IDs, evaluation checkpoints and current lifecycle |
| Search/export | ShipmentSearchPolicy.cs, ShipmentSearchStore.cs, ShipmentSearchEndpoints.cs | Reuse validated filter vocabulary where appropriate, but never reuse M12 cursor as a report snapshot |
| Roles | OrganizationPolicy.cs, OrganizationStore.cs, M1 protected-cookie CurrentSession and M16 audit | New typed dashboard permission with live selected-org checks; no global administrator bypass |
| UI | web/src/App.tsx, auth/AuthPanel.tsx, auth/OrganizationWorkspace.tsx, shipments/ShipmentWorkspace.tsx and shipmentSearchClient.ts | Add one scoped dashboard workspace and API client without redefining shared shell or notifications |

At planning time the OrganizationPermission enum has ViewShipmentData and ExportShipmentData, but **no dedicated dashboard permission** and **no per-Auditor dashboard grant storage**. The API contains no implemented analytics overview route; the entry in docs/API_CONTRACTS.md is a proposed resource map. M13 must not claim those already exist.

M11 exception SQL is protected by special restricted-reader read-scope controls and RLS on exception/evaluation/history tables. Do not assume an ordinary EF query under the API login can read them. A new report service must be validated against actual harborops_api permissions and either use a migration-owned constrained signed read function or an equivalently safe approved scoped SQL path. Avoid relaxing existing M11 SELECT/RLS to make aggregation easy.

## 2. Bounded architecture and dataflow

1. Authenticated session selects one organization. A GET validates the actual current server session, security version, org Active status, membership Active state, typed dashboard permission and canonical filters.
2. A reporting service obtains one REPEATABLE READ PostgreSQL snapshot and one database UTC evaluation instant. It reads M5 shipment current records and first status transitions, tenant-matched M6 voyage intervals, M10 freshness inputs and authorized M11 current evaluation/occurrence facts. No M9 parked transport work is converted into a movement fact.
3. Use one canonical per-shipment eligibility relation for each metric family; avoid joins that multiply shipments by status history or multiple exception occurrences. Distinct shipment count and occurrence count remain separately defined.
4. Evaluate the approved m13-kpi-v1 truth table. Materialize minimal membership rows and result totals in the same committed transaction when precise cross-request drill parity is required. Protect an opaque report handle as a short-lived actor/session/org/filter/version-bound token.
5. The overview response contains counts, exact denominators, unknown/excluded reasons, UTC windows, policy versions, freshness and snapshot metadata. The frontend never calculates a metric independently from unpaged M12 data.
6. Drill endpoint reauthorizes every request, reads only the report's stored member set and returns a deterministic keyset page. A downstream link to M12 shipment detail or M11 exception detail requires fresh authorization and can display newer state with a visible date distinction.
7. Report member deletion is bounded by TTL and quotas; database outages fail closed. No background evaluation, projector reconfiguration, transport processing or notification delivery is triggered by a read.

D-005 approves the versioned KPI and exact-reconciliation policy. Storage, performance and security implementation remain unbuilt, and their behavior cannot be claimed until verified.

## 3. Read and computation model

Recommend a dedicated ReportQueryService and scoped ReportStore under a new Infrastructure/Reporting namespace, a pure Domain/Reporting KPI catalog and response validation, and Api/Reporting/OperationalKpiEndpoints.cs. Register only the dedicated endpoint/service and permission in Program.cs and dependency injection. Do not alter the M11 evaluator or M12 search/export execution semantics.

### 3.1 Base row eligibility and SQL shapes

The following are design sketches, **not existing SQL functions or executed queries**:

- Current work: FROM shipments s WHERE s.organization_id=@organizationId. Current active = s.record_status='Active' AND s.workflow_status NOT IN ('Completed','Cancelled'). All current workflow statuses count one unarchived row each.
- Period completion: FROM shipment_status_changes h JOIN shipments s ON s.organization_id=h.organization_id AND s.id=h.shipment_id WHERE h.organization_id=@organizationId AND h.to_status='Completed' AND h.changed_at_utc >= @fromUtc AND h.changed_at_utc < @beforeUtc. Select first Completed per shipment using GROUP BY h.organization_id,h.shipment_id or DISTINCT ON with deterministic tie-break. Do not multiply by later history entries.
- Current voyage: LEFT JOIN LATERAL (SELECT a.voyage_id FROM shipment_voyage_assignments a WHERE a.organization_id=s.organization_id AND a.shipment_id=s.id AND a.starts_at_utc <= @evaluatedAtUtc AND (a.ends_at_utc IS NULL OR a.ends_at_utc > @evaluatedAtUtc) ORDER BY a.starts_at_utc DESC,a.id DESC LIMIT 1) v ON true. Preserve existing M6 exclusion and tenant invariants.
- Current exception signal: choose one authoritative row or evaluation checkpoint for (organization,shipment,rule), with accepted M11 lifecycle/recurrence handling. Distinct shipment IDs for K07-K10; occurrence IDs for K11-K12. A resolved active RecordedArrivalLate remains factual late even after a Manager closes the case. Unknown/Stale and evaluator lag are visible separately.
- Fresh tracking: JOIN shipment_tracking_input_versions and shipment_tracking_correction_heads on both org and shipment, compare them to projection input_revision and correction_generation, require policy_version matching the accepted tracking policy. No projection or mismatched revisions is unknown/stale rather than conflict-free.
- Arrival comparator: first M5 Arrived transition against proven applicable planned_arrival_at_utc. Prove edit restrictions and historical reference integrity; never reassign an amended present-day plan to an earlier event without evidence.

A single SELECT with multiple correlated scalar subqueries over a large tenant may be unacceptable. Prefer narrow, index-supported materialized CTE source sets, one pass for workflow status, one for event-window transitions and one M11 current rule scan, all within the same transaction. Test CTE materialization and optimizer choices instead of assuming one query is faster.

### 3.2 Report snapshot persistence proposal

Possible additive tables, names provisional pending migration allocation:

- operational_kpi_reports: id UUID, organization_id UUID, actor_user_id UUID, session_id UUID, policy_version, filters_sha256, from_utc, before_utc, evaluated_at_utc, created_at_utc, expires_at_utc, source_versions_digest, summary_json bounded, status and row_count. Composite scope indices and FK to tenant/session where safe.
- operational_kpi_report_members: report_id, organization_id, metric_code, bucket_start_utc nullable, shipment_id, occurrence_id nullable, safe source_kind, source_version or fingerprint. Unique key includes metric/bucket/shipment/occurrence so distinct-count semantics are explicit. Composite tenant and report key, UUID keyset, no unrestricted free text.
- Optional evaluation-quality summary materialized alongside totals. Do not store detailed M11 human reasons, actors, raw payload or M16 audit material.
- Database-enforced TTL/size/owner checks and cleanup process, with a measured retention policy and no cascade into accepted business history. Storing the derived membership set does not replace or mutate accepted M5/M11 records.
- Refuse over-budget snapshots atomically rather than return a truncated but seemingly complete dashboard. Establish a measured large-tenant cap through G13-S20, with realistic request budgets and per-principal limits. Prefer tenant-safe rate limiting and single in-flight build; per-instance protection alone must be documented under multi-instance use.

A materialized-per-metric member relation can be expensive for a large organization. Compare it with a compact per-shipment bitset design and server-owned precomputed read-model as a benchmarked alternative. Approve one format and retention bound before implementation. No final table or migration ID is allocated here.

### 3.3 Transactions and concurrent data

Use PostgreSQL REPEATABLE READ for summary/member generation. Lock live organizations, auth_sessions, users and organization_memberships with appropriate shared row locks for the selected actor so a concurrent revocation cannot turn a completed build into a post-revocation authorized commit. Revalidate before writing a report; retry only identified serialization/concurrency errors within a finite budget and never serve a mixed-source partial result.

Cross-request drill reads do not hold the original transaction open. They read persisted snapshot members after a fresh live authorization check, bind the cursor to the protected report token and reject expiration or altered filter/session. A revoked user cannot continue a previously built report. Do not claim that downstream M12 details are historically frozen.

M11 reevaluation may commit between two report requests; each independently reports its evaluatedAtUtc and rule/input generation. A suspended organization fails even if a report member table still contains its old IDs. If the read model is eventually refreshed asynchronously, it must carry a documented source high-water mark and be excluded from m13-kpi-v1 until reconciled to exact source rules.

## 4. Proposed public API contract

Namespace reserved for M13: /api/v1/organizations/{organizationId}/analytics, no M15 route ownership. Use the current error envelope/correlation conventions and protected session cookie. Initial routes:

| Method | Route | Semantics |
| --- | --- | --- |
| GET | /overview | Bounded, freshly authorized report. Required paired fromUtc/beforeUtc or explicit defaultWindow=last7CompleteDays. Optional originPortId, destinationPortId, voyageId, recordSource and bounded bucket=day/week. Current statuses always labelled evaluated-at, regardless of event window |
| GET | /reports/{reportId}/members | Required opaque reportToken, metricCode, optional bucketStartUtc and protected cursor; limit 1..100. Returns IDs, safe source labels, and source-version hints from the frozen membership set |
| GET | /catalog | Versioned public-to-authenticated catalog of display labels, numerators, denominators, source classes, exclusion semantics and accuracy warnings. Authorize tenant selection if it includes tenant-specific data |

No POST or mutation route is required for end-user reporting. Server-side materialization on GET is an internal bounded derived-storage operation with no user-controlled SQL expressions. Unknown query keys, duplicates, invalid UUIDs, excessive URL length and non-UTC bounds return 400 with no database scan. Never put raw actor IDs, shipment labels or report token contents in URLs if it would expose them in logs; use a compact protected opaque token and no-store policy. For long tokens, consider safe request headers instead of query strings while keeping GET idempotent.

**Illustrative overview DTO** (field naming proposal):

- reportId, reportToken, expiresAtUtc, evaluatedAtUtc, window {fromUtc,beforeUtc,bucket,partialBucket}, catalogVersion, m11RuleVersion, trackingPolicyVersion, sourceFreshness {evaluatedShipments,eligibleShipments,staleOrUnassessed}, filters {normalized values}, metrics [{code,sourceClass,numerator,denominator nullable,valuePct nullable,excluded,unknown,label,drillable}], series [{metricCode,bucketStartUtc,numerator,denominator nullable}], warnings [{code,message}], correlationId.
- Every ratio provides exact integer numerator and denominator. Zero denominator is represented by valuePct=null. No business rows for a denied caller.
- The filtered scope must apply identically to totals, series and membership; do not allow a metric to show filtered totals with unfiltered drill rows. Include a summary reconciliation digest to validate test fixtures.

**Illustrative members DTO:** reportId, metricCode, evaluatedAtUtc, catalogVersion, items [{shipmentId,exceptionId nullable,sourceKind,sourceVersion}], nextCursor, countFromOverview. The stable cursor is signed and tied to actor, session, organization, report, metric, bucket, filter and expiry. A deleted shipment link may return 404 after snapshot without exposing cross-tenant existence.

HTTP access: 401 unauthenticated; 403 forbidden role/revoked scoped grant; 404 foreign or nonexistent organization/resource without existence oracle; 409 expired or mismatched report token, or conflict requiring fresh overview; 400 invalid filters; 429 approved report capacity throttle; 503 dependency, report build timeout or temporarily unavailable authorization verifier. Use accepted sanitized error envelopes and distinguish client cancellation. Responses are Cache-Control: private, no-store and nosniff. Avoid automatic browser retries of expensive overview builds.

## 5. Authorization and database least privilege

The role model currently allows Analyst, Manager and Auditor to ViewShipmentData; Support and Administrator cannot. The requirements matrix allows dashboard viewing for Analyst and Manager, requires an explicit read-only grant for Auditor and denies Support/Administrator by default.

Introduce a separate OrganizationPermission.ViewOperationalKpis. Approved allowlist: Analyst true, Manager true, Auditor false unless a specifically scoped, currently active read-only grant exists, Support false, Administrator false. The existing one-role-per-membership schema contains no general resource grant table, so M13-01 must select and validate a narrow organization/user dashboard entitlement table or an equivalent existing policy facility if independently proven. Do not silently map all Auditors to true on the basis of shipment-read permission. A granted Auditor can see only sanitized read-only metrics and authorized underlying shipment/exception detail; no CSV export authorization is implied.

Validate scope at HTTP boundary, inside the report generation transaction and on every drill request. Enforce organization_id predicates in every join, application query and database report object. Test direct SQL as harborops_api: all M11 RLS policy paths, read-scope signed controls, table grants and possible SECURITY DEFINER functions. Never expose SQL helper keys, permit arbitrary organization selection via a token or grant direct table SELECT to evade M11 isolation.

The existing M16 audit routes are Auditor-only and unrelated to reporting. Optional safe audit of report generation may be separately proposed, but the report flow must not claim that M16 currently logs dashboard reads or that M12's export_prepared event proves report delivery.

## 6. Frontend ownership and UX

Implement a new web/src/analytics/OperationalDashboard.tsx and analytics/kpiClient.ts. Mount as a child of the existing selected organization workspace in OrganizationWorkspace.tsx after shared navigation ownership is agreed. Do not add a top-level application router or overwrite authentication, selected-organization, M15 bell/badge, unread or notification state.

Suggested layout: contextual page heading and UTC as-of label; compact filter row; six workflow status segments; completion and strict-on-time cards with explicit fractions; separate unconfirmed schedule overdue, recorded late, departure risk and equipment conflict indicators; stacked exception-opening trends; evaluated/unknown coverage banner; a drill table with report-bound members and fresh detail links. Place operational caveats next to their corresponding number. No map tiles, route UI, incident chart or ETA prediction.

Loading/empty/error/stale/report-expired/revoked/zero-denominator states are distinct. On organization or role switch abort inflight fetches, clear all report/cursor/selected-row state and unmount the old tenant component. On revocation clear report state before showing denial. Retry creates a new overview rather than reusing an expired report. Protect against out-of-order request completions via request epoch and abort controller; preserve keyboard-semantic controls and focus but defer formal accessibility certification to M24-04.

## 7. Performance and reliability envelope

The accepted M12 evidence used 170,000 synthetic shipments; a large-tenant text search used a sequential heap scan and is not million-row scalable. M13 should not duplicate that unrestricted text search for each metric. Evaluate tenant-leading indices for event-window transitions, current status, M11 condition and evaluability, M6 current voyage, report expiry and report-members keyset. Examples for independent measurement, not allocated migrations: (organization_id,to_status,changed_at_utc,shipment_id) on shipment_status_changes; (organization_id,record_status,workflow_status,id) on shipments; (organization_id,rule_code,condition_state,shipment_id) on shipment_exceptions; a report membership index starting with (organization_id,report_id,metric_code,bucket_start_utc). Existing M5 and M11 indices must be inventoried before adding duplicates.

Capture EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) for exact parameterized SQL under the restricted runtime role at small, medium and 170k seeded volumes, skewed large tenant, empty tenant and concurrent update conditions. Log query shape and timings, not raw customer data. Report lock waits, heap/index buffers, sort and temp spills, snapshot write amplification and cleanup cost. Propose explicit p95 and memory thresholds for G13/S20 approval, not claim the M12 500ms search target is already a measured M13 dashboard objective.

Set short statement and overall request budgets below the current 30-second proxy read envelope; choose exact caps after performance experiments. Recheck per-instance vs multi-instance rate/lease semantics; if a multi-node environment is in scope, require database-backed or shared admission control. No retries after network response headers. On database outage, cancellation, serialization exhaustion or snapshot write failure, roll back report and members and return sanitized failure.

## 8. Migration, rebuild and future integrations

The accepted migration chain ends at 20261107230000_M12ShipmentExportAudit, fifteen ordered migrations. M13 and M15 may both need additive migrations and touch the EF model snapshot. Allocate final migration timestamps only after a shared migration-order agreement. Verify fresh schema and populated 15-to-next migration safe copies, tenant grants, snapshot hashes and Down safeguards. Never modify accepted migrations 1-15.

A future read model may accelerate reporting, but it must derive exclusively from versioned accepted source facts, be rebuildable and reconcile exactly at a named cutoff; no background job, cache or transport-backlog release is assumed in M13. M15 notifications may eventually link to an analytics drill view, but M15 recipient/delivery/read state is not a KPI source and no shared link contract is accepted yet.

## 9. Architectural decisions requiring closure

D-005 already approves the catalog, UTC boundaries, separate strict/tolerated arrival metrics, source safeguards and snapshot-to-drill consistency. Before M13-02: reconcile exact fixture membership and historical plans, finalize Auditor grant storage, measure and select snapshot-member encoding, expiry/quotas, restricted M11 SQL read authority, indexes and request budgets, and coordinate M15 migrations/navigation. Feature implementation still requires separate authorization. G13 requires executed evidence and a separate acceptance decision.
