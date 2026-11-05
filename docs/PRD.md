# HarborOps Product Requirements Document

**Current M9/G9 status (2026-11-05):** G9 was accepted with the mandatory [M9-01 transport-backlog operating condition](M9_LOCAL_CONTRACT_DECISION.md). M9 is integrated into `main` through PR #16 (`43fe326`) and PR #18 (`4684352`). The [G9 gate closure](M9_G9_GATE_CLOSURE.md) records the evidence review and residual limitations. No production or public deployment is authorized; sustained transport-event processing is held until M10 or another explicitly approved backlog mechanism is verified. Earlier planning or acceptance-pending language in historical subsections reflects the earlier project phase.

**Version:** 3.0 parallel delivery alignment  
**Date:** 2026-10-30  
**State:** Product direction confirmed; complete PRD acceptance pending  
**Product:** Enterprise Maritime Logistics & IT Operations Platform

## Authority and document boundaries

This PRD is the source of truth for product purpose, user outcomes, release scope, and prioritization. [Requirements](REQUIREMENTS.md) owns atomic functional and non-functional requirement IDs and the permission matrix; [Domain Model](DOMAIN_MODEL.md), [Security](SECURITY.md), and [Event Processing](EVENT_PROCESSING.md) own detailed invariants. [Roadmap](ROADMAP.md) owns milestone deliverables and exit gates. [Execution Graph](EXECUTION_GRAPH.md) shows the dependencies generated from [execution-graph.json](execution-graph.json). [Status](STATUS.md) alone owns observed implementation and acceptance state. [Decisions](DECISIONS.md) records architectural choices. Contradictions must be resolved through an explicit documentation change, not by silently treating one specification as implemented.

## Confirmed product decisions (2026-10-30)

The following product direction has been explicitly confirmed. These decisions govern roadmap interpretation, but do not by themselves constitute acceptance of an implementation milestone.

| ID | Confirmed direction | Release boundary |
| --- | --- | --- |
| PD-01 | Build both a credible portfolio-grade enterprise platform and a foundation capable of evolving toward real organizational adoption. | First prove coherent, functional, verified synthetic workflows. Production adoption, certifications, and operational integrations require separate evaluation. |
| PD-02 | Prioritize depth and correctness of the existing maritime operations scope over immediate expansion into additional logistics business functions. | Booking, commercial documentation, terminal operations, extended port/fleet management, billing, and customs workflows are future candidates, not M0-M24 requirements. |
| PD-03 | Evolve toward comprehensive IT operations, delivered incrementally. | M0-M24 includes platform health, observability, incidents, audit, diagnostics, root-cause/remediation, and recovery. Asset management, service requests, change management, formal service-level management, escalation automation, and broader operations capabilities are future candidates. |
| PD-04 | Deliver a premium enterprise interface with sophisticated dashboards, interactive route maps, polished navigation and operational workflows. | Premium analytics and route interfaces belong in M13-M14; overall interface polish is completed in M23, with accessibility testing reserved for final M24-04. Extensive end-user customization, configurable dashboards, and personalization are deferred. |
| PD-05 | Support multiple organizations and multiple users from the first business-functionality milestones. | M1 delivers authentication and session lifecycle; M2 delivers memberships, roles, organization isolation, and cross-organization negative tests. M0's public synthetic baseline is not a business-user interface. |
| PD-06 | Deliver a completely functional local deployment and pursue a public live demonstration when it can be provided without mandatory paid infrastructure. | Local functionality and verification are required. Public hosting is optional, subject to cost, security, availability, and service-limit checks; inability to host for free must not block release. |
| PD-07 | No additional individually nominated non-negotiable feature as of this review. | Existing documented Must/Should requirements and acceptance gates remain applicable; this does not waive them. |

Product strategy is settled at this level. Additional modules, paid infrastructure, real customer data, real-time commercial integrations, and production readiness are not implicitly approved.

## 1. Problem

Shipment progress, equipment assignments, exceptions, and technical incidents are often handled in disconnected workflows. An analyst needs an auditable view of a shipment, a manager needs explainable operational KPIs, and technical support needs evidence connecting application failures to service incidents. A standalone demonstration must show those functions connected through the same persisted domain state rather than through unrelated mock screens.

HarborOps begins as an independent enterprise-grade demonstration platform, designed to be capable of evolution toward real organizational use. It neither operates a shipping carrier nor represents a deployed production port system.

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

**Must-have for first complete release (M0 through M24):**

1. Reproducible no-charge local stack: React, ASP.NET Core, PostgreSQL, worker, migration, deterministic fixtures, health and structured logs (FR-015, FR-017; NFR-001, NFR-008).
2. Identity, session management, organizations, memberships, role/object authorization, and organization isolation (FR-001, FR-002; NFR-002, NFR-003).
3. Ports, vessels, voyages, containers, shipment lifecycle, assignments, validation, archive, and conflict protection (FR-003 through FR-005).
4. Versioned intake; durable inbox; immutable accepted event history; deduplication; bounded retries; corrections; deterministic projection; exception detection (FR-006 through FR-010; NFR-004, NFR-007).
5. Search, scoped export, timelines, KPI/dashboard drill-down, and auditable exceptions (FR-011 through FR-014).
6. Health, actionable diagnostics, incident lifecycle and recovery workflow (FR-015, FR-016).
7. Security regression, migration checks, backup/restore rehearsal, benchmark evidence, accessibility-critical paths, and a reproducible end-to-end demonstration (NFR-005 through NFR-012).

**Should-have within the planned release if the gates remain achievable:** licensed or offline-capable route/map context, authorized and deduplicated in-app exception alerts, and incident root-cause/remediation workflow (FR-018 through FR-020). These are planned across M14, M15, and M18, not unconditionally dispensable when release gates or demonstration scenarios depend on them. Any deferral requires a change to the acceptance baseline.

**Optional and excluded from required local operation:** external tracking feeds, external SSO, a public hosted demonstration, permanent hosted deployment, and commercial integrations (FR-021, FR-022). Freight booking, customs clearance, billing, terminal operations, enterprise asset/service/change management, extensive personalization, live AIS guarantees, native mobile apps, multi-region HA, and unattended operational decision-making are out of scope for M0-M24. Candidate future features are not promised release deliverables.

## 5. Core product behavior

### 5.0 Identity and session lifecycle

The approved [M1 Authentication Design](M1_AUTHENTICATION_DESIGN.md) establishes controlled local accounts, Argon2id passwords, PostgreSQL-authoritative cookie sessions, expiry, logout, logout-all, self-disablement, antiforgery and login abuse controls. It requires a functional signed-in browser shell through same-origin `/api` routing, not public registration or an external identity provider. M1 protects only its own diagnostic/account routes; business-resource organization and role authorization begin in M2. Integration-source tokens begin in M7.

D-001 is accepted and M1 is implemented, G1 verified and explicitly accepted on 2026-10-31. PR #4 was merged into `main` at `8f9fc0d506a9e1dc1d4f5acab936622df988f413`. Requirements and test evidence remain governed by [Requirements](REQUIREMENTS.md), [G1 verification](M1_VERIFICATION_PLAN.md) and [Status](STATUS.md).

### 5.0.1 M2 organization membership and role boundary (implemented on `main`, G2 accepted)

The [M2 architecture](M2_ARCHITECTURE_DESIGN.md) defines additive organizations and memberships, a selected organization per authenticated server session, a fixed role-permission matrix and live server checks on each scoped request. A guessed organization ID is never sufficient authorization; administrators cannot automatically read other organizations' data. M2 implements restricted local bootstrap and scoped membership/profile management, not shipment CRUD. The implementation is integrated into `main` via PR #5; all mandatory G2 scenarios passed locally ([executed verification](M2_EXECUTED_VERIFICATION.md)) and G2 was accepted on 2026-10-31.

### 5.0.2 M3 port and vessel reference records (accepted and integrated)

M3 delivers the first accepted organization-owned maritime reference module after M2 authorization. The [M3 architecture](M3_ARCHITECTURE_DESIGN.md) defines port and vessel records, syntactically validated optional UN/LOCODE/IMO/MMSI fields, typed reference-data permissions and scoped React views. Source, schema and APIs are in `main`. The complete local G3 matrix passed on `b72025d35d62d0a8a45b466986a0b3bd89eebadb` ([executed verification](M3_EXECUTED_VERIFICATION.md)); G3 accepted on 2026-10-31. Official registry verification, external position tracking, and M4 voyage/container resources are not part of M3.

### 5.1 Shipment operations

An authenticated, authorized analyst selects an active organization and creates a shipment from validated reference records. Container and voyage assignments must respect temporal and ownership invariants. Edits use concurrency protection. Search and detail requests include the same server-side organization boundary; archive prevents casual deletion of referenced history. See FR-001 through FR-005 and [Domain Model](DOMAIN_MODEL.md).

### 5.1.1 Accepted M5 shipment lifecycle

The [M5 architecture](M5_ARCHITECTURE_DESIGN.md) and [G5 plan](M5_VERIFICATION_PLAN.md) specify operator-entered shipment progress, scoped CRUD, status history, concurrency and archival. This differs from tracking-derived movement and from container/voyage assignments in M6. M5 was implemented and accepted on 2026-11-02; its manual status history is separate from tracking-derived movement and [M6 assignments](M6_G6_GATE_CLOSURE.md). See [G5 closure](M5_G5_GATE_CLOSURE.md).

### 5.2 Event lifecycle

An entitled source submits a bounded, versioned event. The system durably records receipt, authenticates and validates it, resolves identity, and applies at most one business effect per source-specific event key. Unknown, conflicting, malformed, corrected, late, and reordered events have inspectable outcomes. Accepted history is immutable and projections are reproducibly rebuildable. A worker restart or database outage must not silently discard accepted receipts. See FR-006 through FR-010 and [Event Processing](EVENT_PROCESSING.md).

The accepted [M7 implementation](M7_ARCHITECTURE_DESIGN.md) covers source validation and authentication, while M8's accepted [durable inbox](M8_G8_GATE_CLOSURE.md) acknowledges receipt only after PostgreSQL commit. M9's candidate worker, retry and reconciliation implementation passed [technical G9 verification](M9_EXECUTED_VERIFICATION.md) and [browser regressions](M9_BROWSER_SUPPLEMENT.md). The six [M9-01 contract choices](M9_LOCAL_CONTRACT_DECISION.md) were approved on 2026-11-05 with a mandatory hold on sustained transport-event processing until M10 or another explicitly approved mechanism manages the unresolved backlog. Formal G9-S25 review and gate acceptance are pending. M10 still owns projections.

### 5.3 Analytics, map, and notifications

An authorized user can search, filter, and export only visible records. KPI numerators, denominators, exclusions, time windows, and source rows are accessible through drill-through. Simulated positions and inferred ETA values are labeled. Exception alerts are deduplicated and delivered only to authorized recipients for the affected organization and resource. See FR-011 through FR-013 and FR-018, FR-019.

### 5.4 Technical operations

Liveness reports process availability; readiness independently reports dependency state. Support can correlate redacted diagnostic evidence with incidents, move incidents through approved states, document root cause and recovery, and verify successful restoration. The incident workflow never grants unrestricted business-record access. See FR-014 through FR-016, FR-020.

## 6. Interface and accessibility expectations

The web application must meet the confirmed premium-enterprise visual and interaction target: polished operational navigation, clear information hierarchy, sophisticated but explainable dashboards, and interactive map/route context where licensed and feasible. It must provide recognizable loading, empty, ready, denied, conflict, and degraded-service states. Primary flows include sign-in/organization selection, shipment list/detail/edit, container/voyage linking, event timeline, exception queue, operational dashboard, service-health view, and incident management. These views are planned across the smaller milestone boundaries in [Roadmap](ROADMAP.md); the M0 interface is only a database-backed synthetic status view.

Keyboard navigation, visible focus, accessible form names and errors, sensible color contrast, bounded tables, zoom and responsive usability remain required for the completed release (NFR-009). Their **formal testing and acceptance are scheduled only for M24-04**, after interface polish and all functional milestones. Earlier milestones test functional correctness and security, not accessibility. Never signal authorization solely by hiding a button; denied calls must fail on the server.

## 7. Security, data, and architecture constraints

- All business data is organization scoped. Privileged technical roles are not business-data bypass roles.
- Use PostgreSQL as the transactional source of truth with explicit migrations and transactional invariants.
- Use a modular .NET monolith with a separate event-processing worker, not speculative microservices.
- Keep essential development, testing, and demonstrations local and free. No credit card or paid feed is a prerequisite. A public live demo is desirable only when sustainable without mandatory charges; the fully functional local profile remains the release gate.
- Enforce the [Local Storage Policy](LOCAL_STORAGE_POLICY.md): repository, project caches, builds, container data and verification artifacts belong on E:, not C:. Only unavoidable short-lived system temporary use of C: is permitted.
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

## 9. Granular milestone boundaries

The earlier M0-M6 delivery bundles are represented by 25 individually gated milestones, M0-M24. Version 3.0 makes workstreams parallel wherever genuine technical prerequisites allow it. This **does not change product scope, approval status, or executed evidence**. Numbering identifies accountability rather than implementation order.

| Delivery group | Milestone range | Workstream scope |
| --- | --- | --- |
| Foundation and maritime records | M0-M6 | Engineering foundation, sessions, organizations, ports/vessels, voyages/containers, shipment CRUD, shipment assignments |
| Tracking and decision rules | M7-M11 | Source contracts, durable inbox, worker, projections, exceptions |
| Operational workspaces | M12-M15 | Search/exports, reconciled dashboards, geographic context, notifications |
| Security and technical operations | M16-M18 | Append-only audit and hardening, monitoring, incident management |
| Reliability and delivery assurance | M19-M22 | Restoration, security review, performance, local deployment and optional hosting feasibility |
| Release finish | M23-M24 | Premium interface polish, accessibility, complete deterministic scenarios and evidence closure |

Each milestone is gated on real executed behavior, not merely the presence of source files. Detailed work-package IDs, dependency edges and verification expectations are authoritative in [execution-graph.json](execution-graph.json), represented in [Execution Graph](EXECUTION_GRAPH.md). Old v1 M1-M6 task IDs are superseded; [Roadmap](ROADMAP.md) documents the mapping. [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md) controls overlapping branches, migrations, shared contracts and integration verification.

## M4 maritime references (implemented and accepted)

M4 uses accepted M3 vessel/port UUIDs for scoped voyage routes and schedules with independently nullable UTC fields and explicit progression. Containers implement accepted D-003 Option B, using distinct ISO 6346:2022 freight category U and visibly synthetic `SYN-BOX-` identifiers. The [M4 architecture](M4_ARCHITECTURE_DESIGN.md), [implementation status](M4_IMPLEMENTATION_STATUS.md) and [executed G4 evidence](M4_EXECUTED_VERIFICATION.md) document the accepted module. Shipment lifecycle remains separate in M5 and assignment intervals remain M6.

## 10. Dependencies, risks, and decisions

| Risk or decision | Owner document | Required resolution |
| --- | --- | --- |
| Session and integration authentication (D-001) | [Decisions](DECISIONS.md) | Before M1 session implementation |
| Container identity policy (D-003) | [Accepted D-003 Option B](DECISIONS.md) | Approved 2026-11-01 and verified for M4 |
| DCSA subset and durable queue design (D-002, D-006) | [Decisions](DECISIONS.md) | Before M7 contracts / M8 inbox processing |
| Tile and KPI policies (D-004, D-005) | [Decisions](DECISIONS.md) | Before M14 maps / M13 KPIs |
| Hosting budget and backup objectives (D-007, D-008) | [Decisions](DECISIONS.md) | Before M22 deployment / M19 recovery |
| Invalid scope, inaccurate projections, sensitive telemetry, or data loss | [Security](SECURITY.md), [Event Processing](EVENT_PROCESSING.md) | Block verification/release until remedied |

## 11. Change control and approval

Product-scope changes beyond PD-01 through PD-07 require updating this PRD, affected atomic requirements, the [Roadmap](ROADMAP.md) and [execution graph](EXECUTION_GRAPH.md) when sequence changes, plus acceptance criteria and decisions where applicable. Status is updated only from implementation evidence. Proposed scope expansions do not become accepted requirements merely because a branch or issue exists.

**Current state:** M0-M8 are implemented, independently verified, explicitly accepted and integrated into `main`, including G7 acceptance on 2026-11-03 and G8 acceptance on 2026-11-04. [Status](STATUS.md) records the accepted evidence. M8/G8 and D-006 Option A were accepted on 2026-11-04 after independent source-locked evidence review, while G9-G24 and subsequent milestones remain open. See the [M8 implementation status](M8_IMPLEMENTATION_STATUS.md) and [G8 verification runbook](M8_G8_VERIFICATION_RUNBOOK.md). The source repository is public; application hosting is not approved.
