# Requirements

## Requirement status

M0 has source implementations supporting parts of FR-015 (liveness/readiness), FR-017 (deterministic bootstrap metadata only), NFR-001 (local-first configuration), and NFR-006 (initial structured errors). The M0 behavior was runtime verified on 2026-10-30 (see [M0 Verification](M0_VERIFICATION.md)); acceptance is pending and these do not satisfy their full later-milestone acceptance gates. The business requirements remain proposed.

## Functional requirements

| ID | Priority | Requirement | Evidence expected |
| --- | --- | --- | --- |
| FR-001 | Must | Authenticate users and terminate sessions securely | Session and auth integration tests |
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
| NFR-009 | Accessibility | Keyboard navigation, accessible names, visible focus, and usable high-contrast states |
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
