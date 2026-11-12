# Product Scope

**G13/G15 current checkpoint (2026-11-12):** [Run 43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445) is IN PROGRESS on source `c6d2dc86e30913b8d67a011131f9980422e4c197`; preflight, browser, crash recovery, G13 rollback/concurrency, G15 load/fanout and security checks had passed at inspection, with longer technical jobs and the final ledger unfinished. [Run 43120072016](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43120072016) ended FAILURE after 13 technical passes because G15 mutation `outbox-claim-skip-locked-removed` timed out and was INVALID. The fixture was changed to sleep once per transaction, still subject to source-locked verification. [Run 43093329982 attempt 2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982/attempts/2) passed all technical jobs but failed atomic assertion reconciliation. Results cannot be pooled across SHAs. Approved G13-S20 contract: 250,000 KPI members per report, four live reports per tenant, two per user and 1,000,000 total live members per tenant. **G13/G15 NOT ACCEPTED** pending the complete technical packet, independent S25 reviews and explicit gate decisions. No merge, deployment, Turkish integration, production activation or retention deletion. See [run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) and [2026-11-12 reconciliation](REPOWIDE_DOCUMENTATION_RECONCILIATION_20261112.md); older results are historical.

**2026-11-10 final G13/G15 review:** The [final source-locked run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED 15/15 jobs, 543 tests and 227/227 technical assertion witnesses at `15cabd8c542159753c927c2bd65b4346223e84f6`. The separate [formal gate decision](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) is G13 NOT ACCEPTED and G15 NOT ACCEPTED following [S25 reviews](G13_G15_REVIEW_MATRIX_20261110.md). G13 permanent tenant capacity/idle cleanup enforcement and exact G13/G15 mutation/HTTP-outage proof remain pending. P10 conformance is supported; P12 deletion remains disabled. All earlier dates and SHAs in this file describe historical checkpoints only. No merge or production authorization.
**Current G13/G15 evidence (2026-11-10):** [Verified campaign 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed all 15 jobs at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`, with 541 passed tests, zero failures/skips and intact artifacts. The assertion audit still has 3 missing technical witnesses, 7 policy-review and 8 independent-review items. G13/G15 acceptance and integration remain pending. See [the current evidence and policy boundary](G13_G15_VERIFIED_EVIDENCE_20261110.md).

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
