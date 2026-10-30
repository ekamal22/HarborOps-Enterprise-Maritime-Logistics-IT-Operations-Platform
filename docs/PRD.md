# HarborOps Product Requirements Document

**Version:** 1.0 planning baseline  
**Date:** 2026-10-30  
**State:** Proposed, acceptance pending  
**Product:** Enterprise Maritime Logistics & IT Operations Platform

## Authority and document boundaries

This PRD is the source of truth for product purpose, user outcomes, release scope, and prioritization. [Requirements](REQUIREMENTS.md) owns atomic functional and non-functional requirement IDs and the permission matrix; [Domain Model](DOMAIN_MODEL.md), [Security](SECURITY.md), and [Event Processing](EVENT_PROCESSING.md) own detailed invariants. [Roadmap](ROADMAP.md) owns milestone deliverables and exit gates. [Execution Graph](EXECUTION_GRAPH.md) shows the dependencies generated from [execution-graph.json](execution-graph.json). [Status](STATUS.md) alone owns observed implementation and acceptance state. [Decisions](DECISIONS.md) records architectural choices. Contradictions must be resolved through an explicit documentation change, not by silently treating one specification as implemented.

## 1. Problem

Shipment progress, equipment assignments, exceptions, and technical incidents are often handled in disconnected workflows. An analyst needs an auditable view of a shipment, a manager needs explainable operational KPIs, and technical support needs evidence connecting application failures to service incidents. A standalone demonstration must show those functions connected through the same persisted domain state rather than through unrelated mock screens.

HarborOps is an independent demonstration product. It neither operates a shipping carrier nor represents a production port system.

## 2. Product vision and outcome

Deliver one cohesive, locally runnable, security-conscious maritime operations platform in which authorized users manage shipment lifecycles; durable tracking events drive timelines and explainable exceptions; analytics reconcile to source records; and technical failures are detected, investigated, and recovered without silently losing events.

The complete demonstration path is: create shipment -> assign container and voyage -> ingest tracking updates -> inspect timeline and delay -> receive correctly scoped alert -> reconcile dashboard -> inject technical fault -> create and investigate incident -> recover -> verify integrity. The detailed demonstration steps live in [Demo Scenarios](DEMO_SCENARIOS.md).

## 3. Target users and jobs

| Persona | Primary job | Success condition |
| --- | --- | --- |
| Operations Analyst | Create, edit, search, and investigate shipments in the assigned organization | Valid changes persist; unauthorized changes fail; status history is explainable |
| Operations Manager | Review delays, exceptions, and organization KPIs | Each figure can be drilled down to scoped records and documented definitions |
| Support Engineer | Diagnose outages and work service incidents | Health, logs, metrics, and incident evidence correlate without unrestricted customer-data access |
| System Administrator | Manage memberships, policies, and integration permissions | Administrative operations are explicitly scoped; platform role never implies blanket shipment access |
| Read-only Auditor | Examine permitted historical decisions and operational changes | Evidence is immutable or append-only where specified, access is scoped, and exports are controlled |

## 4. Release scope and priority

**Must-have for first complete release (M0 through M6):**

1. Reproducible no-charge local stack: React, ASP.NET Core, PostgreSQL, worker, migration, deterministic fixtures, health and structured logs (FR-015, FR-017; NFR-001, NFR-008).
2. Identity, session management, organizations, memberships, role/object authorization, and organization isolation (FR-001, FR-002; NFR-002, NFR-003).
3. Ports, vessels, voyages, containers, shipment lifecycle, assignments, validation, archive, and conflict protection (FR-003 through FR-005).
4. Versioned intake; durable inbox; immutable accepted event history; deduplication; bounded retries; corrections; deterministic projection; exception detection (FR-006 through FR-010; NFR-004, NFR-007).
5. Search, scoped export, timelines, KPI/dashboard drill-down, and auditable exceptions (FR-011 through FR-014).
6. Health, actionable diagnostics, incident lifecycle and recovery workflow (FR-015, FR-016).
7. Security regression, migration checks, backup/restore rehearsal, benchmark evidence, accessibility-critical paths, and a reproducible end-to-end demonstration (NFR-005 through NFR-012).

**Should-have within the planned release if the gates remain achievable:** licensed or offline-capable route/map context, authorized and deduplicated in-app exception alerts, and incident root-cause/remediation workflow (FR-018 through FR-020). These are planned M3/M4 work, not unconditionally dispensable if a demo scenario depends on them. Any deferral requires a change to the acceptance baseline.

**Optional and excluded from required local operation:** external tracking feeds, external SSO, permanent hosted deployment, and commercial integrations (FR-021, FR-022). Production freight booking, customs clearance, billing, live AIS guarantees, native mobile apps, multi-region HA, and unattended operational decision-making are out of scope.

## 5. Core product behavior

### 5.1 Shipment operations

An authenticated, authorized analyst selects an active organization and creates a shipment from validated reference records. Container and voyage assignments must respect temporal and ownership invariants. Edits use concurrency protection. Search and detail requests include the same server-side organization boundary; archive prevents casual deletion of referenced history. See FR-001 through FR-005 and [Domain Model](DOMAIN_MODEL.md).

### 5.2 Event lifecycle

An entitled source submits a bounded, versioned event. The system durably records receipt, authenticates and validates it, resolves identity, and applies at most one business effect per source-specific event key. Unknown, conflicting, malformed, corrected, late, and reordered events have inspectable outcomes. Accepted history is immutable and projections are reproducibly rebuildable. A worker restart or database outage must not silently discard accepted receipts. See FR-006 through FR-010 and [Event Processing](EVENT_PROCESSING.md).

### 5.3 Analytics, map, and notifications

An authorized user can search, filter, and export only visible records. KPI numerators, denominators, exclusions, time windows, and source rows are accessible through drill-through. Simulated positions and inferred ETA values are labeled. Exception alerts are deduplicated and delivered only to authorized recipients for the affected organization and resource. See FR-011 through FR-013 and FR-018, FR-019.

### 5.4 Technical operations

Liveness reports process availability; readiness independently reports dependency state. Support can correlate redacted diagnostic evidence with incidents, move incidents through approved states, document root cause and recovery, and verify successful restoration. The incident workflow never grants unrestricted business-record access. See FR-014 through FR-016, FR-020.

## 6. Interface and accessibility expectations

The web application must provide recognizable loading, empty, ready, denied, conflict, and degraded-service states. Primary flows include sign-in/organization selection, shipment list/detail/edit, container/voyage linking, event timeline, exception queue, operational dashboard, service-health view, and incident management. These views are planned; the M0 interface is only a database-backed synthetic status view.

Keyboard navigation, visible focus, accessible form names and errors, sensible color contrast, bounded data tables, and responsive layouts are required at their applicable milestones (NFR-009). Never signal authorization solely by hiding a button; denied calls must fail on the server.

## 7. Security, data, and architecture constraints

- All business data is organization scoped. Privileged technical roles are not business-data bypass roles.
- Use PostgreSQL as the transactional source of truth with explicit migrations and transactional invariants.
- Use a modular .NET monolith with a separate event-processing worker, not speculative microservices.
- Keep essential development, testing, and demonstrations local and free. No credit card or paid feed is a prerequisite.
- Use deterministic synthetic fixtures; do not claim actual vessel location, predicted ETA precision, carrier affiliation, or standards certification.
- Keep secrets out of tracked files and diagnostic output. Respect third-party license and map-tile limits.
- UTC storage and time-aware rendering are mandatory. External deployment requires separate HTTPS, budget, access, and shutdown review.

## 8. Proposed measurable success criteria

These are targets, not observed results.

| Outcome | Initial acceptance measure |
| --- | --- |
| Reproducibility | Clean setup builds, migrates, seeds, and serves browser -> API -> PostgreSQL without paid dependencies |
| Tenant isolation | Negative authorization matrix denies cross-organization reads, mutations, searches, and exports |
| Event integrity | Duplicate, late, corrected, unknown, and replayed events preserve immutable history and effect-once projection |
| Explainability | Tested KPI sample reconciles exactly to underlying permitted records and rule fixtures |
| Operational recovery | Fault injection, incident evidence, restart/recovery, and integrity checks complete without silent loss |
| Performance | Common filtered shipment queries achieve proposed p95 below 500 ms in a declared local reference setup (NFR-010) |
| Product completeness | Demonstration scenarios D01-D06 meet documented pass conditions with recorded evidence |
| Delivery correctness | Required test layers pass; no unresolved blocker defects; source and documentation state agree |

Security, reliability, benchmark, and usability evidence must include the exact revision, environment, dataset and executed checks. No metric above is an achieved measurement until recorded in [Status](STATUS.md) and the corresponding evidence register.

## 9. Dependencies, risks, and decisions

| Risk or decision | Owner document | Required resolution |
| --- | --- | --- |
| Session and integration authentication (D-001) | [Decisions](DECISIONS.md) | Before M1 identity implementation |
| Container identity policy (D-003) | [Decisions](DECISIONS.md) | Before M1 domain validation |
| DCSA subset and durable queue design (D-002, D-006) | [Decisions](DECISIONS.md) | Before M2 ingress/worker |
| Tile and KPI policies (D-004, D-005) | [Decisions](DECISIONS.md) | Before M3 maps/KPIs |
| Hosting budget and backup objectives (D-007, D-008) | [Decisions](DECISIONS.md) | Before M5 relevant tasks |
| Invalid scope, inaccurate projections, sensitive telemetry, or data loss | [Security](SECURITY.md), [Event Processing](EVENT_PROCESSING.md) | Block verification/release until remedied |

## 10. Change control and approval

Product-scope changes require updating this PRD, affected atomic requirements, the [Roadmap](ROADMAP.md) and [execution graph](EXECUTION_GRAPH.md) when sequence changes, plus acceptance criteria and decisions where applicable. Status is updated only from implementation evidence. Proposed scope expansions do not become accepted requirements merely because a branch or issue exists.

**Current state:** The M0 foundation exists on an unmerged feature branch but has not passed full runtime verification. All M1-M6 product workflows remain planned. See [Status](STATUS.md) for the current evidence and [M0 Verification](M0_VERIFICATION.md) on the feature branch for its detailed test ledger.
