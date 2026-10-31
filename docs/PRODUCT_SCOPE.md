# Product Scope

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

## Planned M4 voyage/container reference scope

[M4 architecture](M4_ARCHITECTURE_DESIGN.md) adds organization-owned voyage route/schedule references derived from M3 vessels and ports, plus validated container identity records. The proposed ISO 6346/Synthetic split requires a separate [D-003 decision](M4_D003_CONTAINER_IDENTIFIER_PROPOSAL.md). [M4 implementation](M4_IMPLEMENTATION_PLAN.md) and [G4 verification](M4_VERIFICATION_PLAN.md) explicitly exclude bookings, M5 shipment CRUD, M6 assignments, tracking integration, registry-verified ownership and paid API dependencies. G4 is open and M4 code is not started.

## M3 reference-data module (verified, accepted and integrated)

The [M3 design](M3_ARCHITECTURE_DESIGN.md) separates accepted organization-owned port and vessel reference records from later M4 voyage/container and M5 shipment models. It validates external identifier syntax without asserting official registry verification. Implementation is integrated into `main`; all 25 G3 scenarios passed on `b72025d35d62d0a8a45b466986a0b3bd89eebadb` ([executed verification](M3_EXECUTED_VERIFICATION.md)). G3 was explicitly accepted on 2026-10-31. M4 remains planned, and no public deployment is implied.

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
