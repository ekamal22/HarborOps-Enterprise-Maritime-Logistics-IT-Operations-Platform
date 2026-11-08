# Product Scope

**Executed M11/M12 verification (2026-11-08):** [Source-locked verification reconciliation](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) records 340/340 passing full integration tests on an earlier SHA, 40/40 latest impacted tests, G11 19/19, G12 4/4, 36/36 three-engine browsers, 5/5 effective mutants, security checks and a verified populated 13-to-15 migration. Source build and 291/291 unit plus 46/46 contract tests passed on a later corrective SHA. The final combined regression run `42443065884` is still in progress; G11/G12 S25 review and acceptance remain pending. Earlier checkpoints below retain their historical meaning.

**Current verification policy (2026-11-08):** [M11/M12 verification operations](M11_M12_VERIFICATION_OPERATIONS_20261108.md) now requires manual, coordinated, source-locked workflows instead of automatic push-triggered campaigns. [Run-by-run progress](M11_M12_VERIFICATION_PROGRESS_20261108.md) is the current verification checkpoint; older reports and source-only notes below are historical. G11/G12 independent S25 review and acceptance remain pending. Protected `main`, the M9 transport hold, and deployment restrictions are unchanged.

**Current combined-source record (2026-11-08):** `feat/m11-m12-combined-integration` now includes the M11 and M12 source candidates and reconciliation of the shared API, EF migrations, role provisioning and React workspace. The [M11 local policy decision](M11_LOCAL_CONTRACT_DECISION_20261108.md) is recorded, with M11 lifecycle source fixes and unexecuted tests. Earlier parallel-only or M11-policy-pending statements in this document describe historical branch status, not the latest combined candidate. G11/G12 executable verification and independent acceptance remain pending. No protected-main merge, deployment or M9 hold release is authorized.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). M11 exception functionality and M12 shipment search/export will be tested together on a future reconciled candidate; product scope, independent policy decisions and the no-deployment boundary are unchanged.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records draft M11 PR #31 alongside draft M12 PR #32, with different additive migration IDs and an unresolved shared API/UI/role integration boundary. Both are source candidates, not merged or runtime-verified; G11-S01 BLOCKED, G11-S02..S25 NOT EXECUTED, G12 technical verification pending. Accepted `main` and the M9 transport hold remain unchanged.

**Current M11 source audit (2026-11-08):** [Sixth correctness audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) records correction of inactive severity and same-fingerprint metadata drift, plus typed due-work cursor keys. Tests are authored but unexecuted. G11-S01 BLOCKED, G11-S02 through S25 NOT EXECUTED, acceptance pending; M9 transport processing held and PR #31 unmerged.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). M11 exception functionality and M12 shipment search/export will be tested together on a future reconciled candidate; product scope, independent policy decisions and the no-deployment boundary are unchanged.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records independent unmerged source candidates in draft PRs #31 and #32. M11 policy approval is blocked, both technical gate campaigns are unexecuted, and acceptance pending. Protected `main` and the M9 transport hold are unchanged.

**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted on protected `main`. The M10 signed-correction remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) following S01/S25 independent review; see [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Earlier G10 pending statements are historical source-locked records. The M9 transport-processing hold remains mandatory. No deployment or release is authorized.

**Current milestone status (2026-11-07):** G0-G10 and G16 are accepted and merged into `main`. G10 correction authority and upgrade-safe migration 13 integrated via [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28). The earlier M9 transport-backlog operating condition remains mandatory, so sustained ARRI/DEPA processing is held until separately verified cap-safe behavior is approved. No deployment or release authorization.

**Supporting scope detail:** The [Product Requirements Document](PRD.md) is authoritative for product vision, users, priorities, and release boundaries. Preserve domain-specific detail here without introducing a competing scope baseline. Actual state is recorded in [Status](STATUS.md).

## Vision

HarborOps provides a single operational workspace for maritime shipment lifecycle management, tracking event visibility, exception investigation, operational reporting, and technical service reliability. It is a standalone platform using demonstration data and open specifications.

## Primary users

| Role | Responsibilities | Default access |
| --- | --- | --- |
| Operations Analyst | Register and monitor shipments, investigate delayed milestones | Read/write assigned organization's shipment operations |
| Operations Manager | Review aggregate KPIs, address escalations, oversee workflows | Operational oversight within authorized organization |
| System Administrator | Configure tenants, users, integrations, and technical policies | Scoped platform administration; no implicit business-data bypass |
| Support Engineer | Monitor platform health and manage service incidents | Diagnostic access with redaction; no unrestricted customer data |
| Read-only Auditor | Review recorded decisions, access activity, and changes | Read-only, explicitly granted scope |

## In scope

1. Organization-scoped user management with configurable roles.
2. Ports, vessels, voyages, shipments, containers, and associations.
3. Ordered, inspectable shipment and equipment tracking timelines.
4. Versioned event ingestion from a simulator and a documented external adapter interface.
5. Validated tracking states, idempotency, replay controls, and exception handling.
6. Container lookup, filters, saved views, and milestone details.
7. Aggregate operational metrics and drill-through investigations.
8. Location views using licensed basemap sources or local map assets.
9. Security-relevant audit history and per-organization authorization.
10. Infrastructure health, diagnostics, alert routing, and service incidents.
11. Repeatable local deployment with documented backup and restore.
12. Export of authorized data and reproducible demonstration scenarios.

## Approved M1 security baseline

The initial human sign-in implementation is a local-first server-managed cookie session with PostgreSQL authority and Argon2id. The approved [D-001 decision](ADR_D001_BROWSER_SESSIONS.md) and [M1 design](M1_AUTHENTICATION_DESIGN.md) include login, idle/absolute expiry, session revocation, logout, logout-all, disabled account handling and a functional protected React shell. No identity hosting bill is required. Organization role isolation was delivered in M2; integration tokens and external tracking adapters follow later.

This functionality is implemented, verified and accepted on `main` following G1 acceptance and PR #4 integration on 2026-10-31. See [executed evidence](M1_IMPLEMENTATION_VERIFICATION.md). No public self-registration, enterprise single sign-on, paid identity service, JWT refresh-token system or organization-access claims are included in M1. Accessibility is tested only at the final M24-04 release package.

## Accepted M2 organization authorization

M2 introduces organization memberships, five fixed roles, per-session selected organization context, scoped organization and membership APIs, live permission evaluation and last-administrator protection. All 25 G2 scenarios passed locally on the recorded revision; G2 was accepted on 2026-10-31 and merged via [PR #5](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/5). The [executed verification record](M2_EXECUTED_VERIFICATION.md) preserves the source SHA, fixtures, cases and operational evidence. Maritime port and vessel reference entities are implemented and accepted in M3.

## Accepted M4 voyage/container reference scope

[M4 architecture](M4_ARCHITECTURE_DESIGN.md) implements organization-owned voyage route and schedule references derived from M3 vessels and ports, plus validated container identity records. D-003 Option B, with ISO 6346:2022 freight category U and visibly tagged synthetic identifiers, was explicitly accepted on 2026-11-01. The [M4 executed verification](M4_EXECUTED_VERIFICATION.md) confirms G4 acceptance. M5 shipment CRUD, M6 assignments, booking, tracking integration, registry-verified ownership and paid dependencies are outside M4 scope.

## M3 reference-data module (verified, accepted and integrated)

The [M3 design](M3_ARCHITECTURE_DESIGN.md) delivers accepted organization-owned port/vessel reference records on which the accepted M4 voyage/container module depends. M3 was accepted on 2026-10-31 and M4 on 2026-11-01. Both are integrated into `main`. M5 shipment lifecycle and M6 assignments were subsequently verified and accepted on 2026-11-02; no public application hosting is implied.

## Accepted M5/M6 shipment and assignment boundary

M5/M6 provide accepted shipment lifecycle and assignments; M7 source validation, M8 durable receipts, M9 worker/reconciliation and M10 indexed event timeline/projection are accepted and integrated. M16 Auditor-scoped audit controls are also accepted. M10 corrections are signed and audit-backed. M10 does not release the parked-transport hold or compute ETA.

## Out of scope for initial release

- Commercial vessel positioning feeds and proprietary carrier system connections.
- Actual container bookings, customs declarations, freight invoicing, or financial settlement.
- Claims of certified DCSA interoperability or standards compliance without independent validation.
- Real shipping line branding, production operational data, or customer personal information.
- Multi-region high availability and permanent enterprise-scale cloud hosting.
- Fully automated operational decisions with no human confirmation.
- Native mobile applications and general-purpose ERP replacement.

## Verified baseline and workspace

The M0 engineering baseline is integrated into `main` and G0 was **accepted** on 2026-10-31 after successful [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) at source revision `fec7429415fede121db8a4a34f0df72cd4fac609`. Shipment, tracking, multi-tenant business access, service desk and production deployment remain outside the implemented M0 scope. Local persistent development storage remains subject to [Local Storage Policy](LOCAL_STORAGE_POLICY.md): E-drive persistence, with only unavoidable short-lived Windows temporary use on C:.

## Product principles

- Every operational record has an explicit organization boundary.
- Data lineage and processing decisions are inspectable.
- Historical events are preserved; projections may be recomputed.
- Unknown or contradictory events enter investigation queues rather than silently overwriting authoritative values.
- Simulation is unmistakably distinguishable from verified external data.
- Security enforcement occurs on the server and database access paths, not only in the browser.
- No paid subscription is mandatory for local feature development.
- Planned, implemented, and verified are distinct statuses.

## Key workflows

A. Create shipment -> assign containers -> link voyage -> record milestones -> monitor status.

B. Receive tracking event -> validate -> deduplicate -> persist -> project timeline -> evaluate exception -> notify authorized users.

C. Detect infrastructure fault -> emit health signal -> create incident -> investigate evidence -> recover -> document root cause.

D. Examine dashboard anomaly -> filter shipments -> review event trail -> export scoped report.

## Constraints and assumptions

- First deployment is single-region and low-scale, using synthetic data.
- Authentication uses local accounts for the baseline; external single sign-on is an optional extension.
- Time is recorded as UTC with explicit source offsets where available.
- A vessel location or ETA presented without a verified upstream feed is simulated or inferred and visibly labeled.
- Scope expansion requires an explicit decision record and a testable acceptance gate.
