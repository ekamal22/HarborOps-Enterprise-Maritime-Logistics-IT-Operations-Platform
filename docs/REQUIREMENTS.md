# Requirements

**Authority:** This document owns atomic FR/NFR identifiers and the permission matrix. The [Product Requirements Document](PRD.md) owns product outcomes and release boundaries. [Roadmap](ROADMAP.md) assigns milestone gates, [execution-graph.json](execution-graph.json) connects work packages to requirement IDs, and [Status](STATUS.md) owns delivery evidence.

## Requirement status

Requirements describe the intended complete product. G0 is accepted; the M1 authentication design and D-001 are approved, but FR-001 is not implemented or verified. A requirement is considered delivered only after its tests and acceptance evidence appear in [Status](STATUS.md).

## Observed baseline and storage boundary

M0 baseline functionality is implemented on `main`, verified at `fec7429415fede121db8a4a34f0df72cd4fac609` by [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) and **G0 accepted** on 2026-10-31. The synthetic status endpoint, fresh PostgreSQL migration/seed, Compose startup and fault recovery are covered. This does **not** satisfy later shipment, tenant authorization, tracking or incident requirements. See [Status](STATUS.md) and [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md).

All future requirements must comply with [Local Storage Policy](LOCAL_STORAGE_POLICY.md): controllable persistent project storage on E:, with unavoidable short-lived Windows temporary writes on C: permitted only as an exception.

## Functional requirements

| ID | Priority | Requirement | Evidence expected |
| --- | --- | --- | --- |
| FR-001 | Must | Controlled local account sign-in using Argon2id; PostgreSQL-authoritative protected-cookie sessions; expiry, logout, logout-all, account disablement, session revocation, CSRF defense and server-enforced protected endpoints | [M1 G1 verification](M1_VERIFICATION_PLAN.md): credential, lifecycle, browser and negative integration tests |
| FR-002 | Must | Enforce organization-scoped read/write access for every resource | Cross-organization denial tests |
| FR-003 | Must | Manage ports, vessels, and voyages with validation | API integration tests |
| FR-004 | Must | Create, edit, search, and archive shipments | CRUD and concurrency tests |
| FR-005 | Must | Assign containers to shipments and voyages with temporal constraints | Domain invariant tests |
| FR-006 | Must | Store inbound tracking events immutably with provenance | Persistence and audit tests |
| FR-007 | Must | Reject malformed events and quarantine invalid/unknown variants | Contract and negative tests |
| FR-008 | Must | Process duplicate event deliveries without duplicate business effects | Replay/idempotency tests |
| FR-009 | Must | Build a shipment timeline and current status from accepted events | Projection correctness tests |
| FR-010 | Must | Detect delays and state conflicts using explainable rules | Rule examples and edge-case tests |
| FR-011 | Must | Search shipments by identifiers, status, voyage, port, and date | Filtering and pagination tests |
| FR-012 | Must | Display aggregate operational KPIs and underlying rows | Reconciliation tests |
| FR-013 | Must | Export only the caller's authorized filtered dataset | Export authorization tests |
| FR-014 | Must | Record sensitive actions in append-only audit records | Audit visibility tests |
| FR-015 | Must | Expose readiness, liveness, and dependency health checks | Failure injection tests |
| FR-016 | Must | Manage incident tickets through defined lifecycle states | State transition tests |
| FR-017 | Must | Provide a deterministic synthetic demonstration dataset | Reproducibility tests |
| FR-018 | Should | Show route and location context on an interactive map | UI tests and licensing review |
| FR-019 | Should | Send in-app alerts for shipment and infrastructure exceptions | Notification matrix tests |
| FR-020 | Should | Track incident root cause and associated remediation actions | Incident workflow tests |
| FR-021 | Could | Add an optional adapter for permitted external tracking APIs | Sandbox contract tests |
| FR-022 | Could | Add external SSO and enterprise identity mapping | Integration/security review |

## M1 identity requirement allocation

[The accepted M1 design](M1_AUTHENTICATION_DESIGN.md) specifies `FR-001` with `NFR-002` deny-by-default authentication and `NFR-003` protection of credentials, cookies, keys and logs. M1 implements who is authenticated, not which organizations or business records that person may access. Cross-organization authorization and two-organization fixtures remain `FR-002` in M2. Integration source authentication is an M7 concern. Human browser sessions use protected cookies, not browser JWT/refresh-token storage. D-001 is accepted; G1 remains open and unverified.

## Non-functional requirements

| ID | Area | Proposed target and verification |
| --- | --- | --- |
| NFR-001 | Cost | All essential workflows operate locally without payment details or paid subscriptions |
| NFR-002 | Security | All authenticated resources pass deny-by-default and cross-organization test matrix |
| NFR-003 | Confidentiality | No credentials, tokens, sensitive payloads, or private customer data in logs |
| NFR-004 | Reliability | Event replay is safe after process restart and repeated delivery |
| NFR-005 | Observability | User-facing failures can be correlated with structured logs, metrics, and traces |
| NFR-006 | API stability | Public endpoints versioned; errors use a consistent structured envelope |
| NFR-007 | Time handling | UTC at rest; no ambiguous local timestamp comparisons |
| NFR-008 | Quality | Required test suites pass from a clean database and reproducible local setup |
| NFR-009 | Final-release accessibility | Keyboard navigation, accessible names, visible focus, contrast, zoom, error announcements, responsive use and screen-reader workflows; all tested only in M24-04 before G24 |
| NFR-010 | Performance | For the defined reference dataset, p95 shipment search below 500 ms in local baseline |
| NFR-011 | Recovery | Restore procedure demonstrated on a disposable environment with integrity checks |
| NFR-012 | Operational safety | Fault simulations never affect external or production resources |

Performance targets are engineering objectives, not measured outcomes. Test hardware, data volume, warm-up, network conditions, and sampling must be recorded with each result.

## Authorization matrix

| Operation | Analyst | Manager | Support | Administrator | Auditor |
| --- | --- | --- | --- | --- | --- |
| View scoped shipments | Yes | Yes | Explicit grant only | Explicit grant only | Read-only grant |
| Modify scoped shipments | Yes | Yes | No | No by default | No |
| View organization dashboards | Yes | Yes | No by default | No by default | Read-only grant |
| Manage user roles | No | No | No | Yes, within administrative scope | No |
| View technical telemetry | No by default | Limited | Yes | Yes | Explicit grant |
| Manage incidents | No | Limited | Yes | Yes | Read-only grant |
| Export business data | Scoped | Scoped | No by default | No by default | Approved scoped export |

Every operation is denied without an authenticated subject, required permission, and matching organization scope. The matrix is the starting authorization policy, subject to a versioned review.

## Reporting definitions

- Active shipment: not archived, cancelled, or completed at the query cutoff.
- On-time arrival: actual arrival timestamp is no later than planned arrival, for completed eligible shipments.
- At risk: a rule-identified shipment with an unconfirmed future delay indicator.
- Delayed: latest accepted projected or actual milestone exceeds the plan under documented tolerance rules.
- Incident count: tickets that are not resolved or closed at query time.
- KPI denominators, exclusions, and query windows must accompany displayed results.
