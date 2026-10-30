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

## Out of scope for initial release

- Commercial vessel positioning feeds and proprietary carrier system connections.
- Actual container bookings, customs declarations, freight invoicing, or financial settlement.
- Claims of certified DCSA interoperability or standards compliance without independent validation.
- Real shipping line branding, production operational data, or customer personal information.
- Multi-region high availability and permanent enterprise-scale cloud hosting.
- Fully automated operational decisions with no human confirmation.
- Native mobile applications and general-purpose ERP replacement.

## Verified baseline and workspace

The initial M0 local engineering baseline has been verified on the separate implementation branch against code revision `4fb34eb`; G0 acceptance remains pending. No shipment, tracking, multi-tenant business access, service desk or production deployment is thereby approved. See [Status](STATUS.md). Development storage is constrained by [Local Storage Policy](LOCAL_STORAGE_POLICY.md): E-drive persistence, with C-drive short-lived system temporary use permitted only when unavoidable.

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
