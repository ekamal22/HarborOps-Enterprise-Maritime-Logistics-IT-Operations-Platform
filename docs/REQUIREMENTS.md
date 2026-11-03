# Requirements

**Authority:** This document owns atomic FR/NFR identifiers and the permission matrix. The [Product Requirements Document](PRD.md) owns product outcomes and release boundaries. [Roadmap](ROADMAP.md) assigns milestone gates, [execution-graph.json](execution-graph.json) connects work packages to requirement IDs, and [Status](STATUS.md) owns delivery evidence.

The [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md) and version 3.0 [Execution Graph](EXECUTION_GRAPH.md) govern technical prerequisites: separate workstreams may progress independently after named accepted gates, but all cross-module security, data integrity and verification requirements remain mandatory. Requirement IDs and actual completion status are unchanged by the dependency redesign.

## Requirement status

Requirements describe the intended complete product. G0 through G7 are accepted. FR-001 was implemented in M1 and FR-002 organization authorization in M2; both are integrated into `main` following explicit acceptance on 2026-10-31. A requirement is considered accepted only after its tests and acceptance evidence appear in [Status](STATUS.md) and the corresponding gate is explicitly accepted.

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

[The accepted M1 design](M1_AUTHENTICATION_DESIGN.md) addresses `FR-001`, `NFR-002` and `NFR-003` for identity, credentials, cookies, keys and logs. Organization and role authorization is `FR-002`, implemented and accepted in M2; integration source authentication was implemented and accepted in M7. Human browser sessions use protected cookies, not browser JWT/refresh-token storage. D-001/G1 and G2 are accepted. The [M2 architecture](M2_ARCHITECTURE_DESIGN.md) and [executed G2 verification](M2_EXECUTED_VERIFICATION.md) record the completed tenant-isolation implementation on `main`.

## M2 organization authorization allocation (implemented, verified and accepted)

FR-002 and NFR-002 were implemented in M2-01 through M2-04 with live server-side permission resolution, organization memberships and role grants, session-owned organization selection, scoped resource predicates and immediate revocation. The existing role matrix below remains policy for future business operations. M2 implements organization/profile and membership APIs; shipment CRUD, event ingestion, exports and dashboards remain later milestones. The work is merged into `main` via PR #5 and the complete [G2 matrix](M2_VERIFICATION_PLAN.md) passed locally ([executed evidence](M2_EXECUTED_VERIFICATION.md)).

## M3 port and vessel reference-data allocation (G3 accepted)

FR-003 covers accepted M3 port/vessel and M4 voyage reference records. The M4 voyage slice implements tenant-scoped route and UTC window invariants, with independent migration, API, security and browser verification. [G4 executed evidence](M4_EXECUTED_VERIFICATION.md) is the M4 completion authority. Official registry validation remains out of scope.

## M4 requirement allocation (implemented and G4 accepted)

FR-003's voyage slice was implemented under M4-02: same-organization vessel and port route, nullable planned/actual UTC windows, explicit voyage status and immutable reference identity. FR-005's container reference slice was implemented under M4-03, with D-003 Option B accepted on 2026-11-01. FR-017 synthetic fixtures and the relevant NFR authorization, error, UTC, repeatability and evidence requirements were exercised. Shipment assignment intervals remain M6, after separately accepted G5 plus G4. See [M4 executed verification](M4_EXECUTED_VERIFICATION.md).

## M5 FR-004 allocation (implemented and G5 accepted)

M5-01 through M5-04 implement organization-scoped shipment CRUD and exact-version concurrency under FR-004 and FR-002. The accepted six-rule M5 local contract gives Analyst/Manager read-write, Auditor narrowly typed read-only and no implicit Administrator/Support business access. The full G5 matrix passed 601 executed tests and was explicitly accepted on 2026-11-02. See [M5 closure](M5_G5_GATE_CLOSURE.md), [architecture](M5_ARCHITECTURE_DESIGN.md) and [verification](M5_EXECUTED_VERIFICATION.md).

## M6 FR-005 allocation (implemented, verified and accepted)

FR-005 shipment-container and shipment-voyage assignments were implemented under the accepted [twelve-rule contract](M6_LOCAL_CONTRACT_DECISION.md). M6 provides tenant-owned UTC half-open intervals, PostgreSQL overlap exclusions, versioned closure, parent guards, route constraints and scoped allocation history. All G6 executable checks passed on the tested source; contract approval subsequently closed G6-S01. M6/G6 were explicitly accepted and merged on 2026-11-02. See [G6 closure](M6_G6_GATE_CLOSURE.md). Durable event intake and processing remain M8-M10 and advanced export remains M12; M7 source validation is accepted.

## Accepted M7 FR-007 source contract allocation

M7-01 to M7-04 satisfy the **validation and source-authentication** portion of FR-007 and the applicable FR-002, NFR-002, NFR-003, NFR-006 and NFR-007 boundaries. [M7 architecture](M7_ARCHITECTURE_DESIGN.md) and [G7 matrix](M7_VERIFICATION_PLAN.md) define the intended source-tenant credential and bounded payload behavior. FR-006 immutable durable event history, FR-008 idempotent processing and accepted-event acknowledgment are **not** delivered in M7: M8 introduces receipt persistence under D-006, followed by M9 and M10. D-002 Option A and M7/G7 were accepted 2026-11-03 following source-locked evidence and independently reviewed supplements. See [G7 closure](M7_G7_GATE_CLOSURE.md).

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
