# HarborOps Delivery Roadmap

**Version:** 2.0 granular delivery baseline  
**Date:** 2026-10-30  
**State:** Confirmed product direction; revised milestone boundaries proposed, acceptance pending.

## Authority and rationale

The [PRD](PRD.md) defines confirmed product outcomes and release boundaries. This roadmap owns delivery scope, milestone prerequisites and gate conditions. [execution-graph.json](execution-graph.json) owns individual work-package IDs and dependency edges and generates [Execution Graph](EXECUTION_GRAPH.md). [Requirements](REQUIREMENTS.md) owns FR/NFR IDs and [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) owns detailed checks. [Status](STATUS.md) alone records actual implementation and acceptance evidence.

The previous seven milestones grouped too many unrelated deliverables together. Version 2.0 decomposes the same planned M0-M6 release into **25 narrower milestones (M0-M24), 101 scoped work packages, and 25 independent evidence gates**. This is a sequencing refinement, not approval to expand the initial product scope. No fixed calendar deadlines are imposed by this document.

## Confirmed product strategy

- Deliver a credible, cohesive enterprise demonstration that can evolve toward real deployment after separate requirements and security reviews.
- Complete maritime shipment, tracking, analytics and operations depth before expanding into bookings, customs, commercial logistics and terminal/fleet modules.
- Deliver the initial technical monitoring and incident workflow, with broader service/asset/change management treated as a future extension.
- Require polished premium-enterprise UX, with configurable personalized workspaces deferred.
- Require real multi-user and multi-organization boundaries from the first business-data milestone.
- Require fully functional zero-cost local deployment. Public hosting remains optional if no mandatory charges are necessary.
- No newly nominated non-negotiable features; existing requirements, negative tests and product acceptance criteria remain in force.

## Milestone contract and gate policy

Each milestone has one small, externally testable outcome, typically four work packages, and a single gate **G0 through G24**. A work package is not complete merely when its code is committed. The gate requires actual executed tests and a recorded revision, environment, fixtures, output, failures, security impact, and any outstanding work. A verified gate is **acceptance pending** until explicitly accepted. Dependent implementation must not proceed until the preceding gate is verified, except read-only research and separately documented sequencing changes.

Security, tenant authorization, data integrity, cost control, migration hygiene and documentation are applied when their related functionality first appears. M16/M20 harden and systematically reverify earlier security measures; they do not postpone basic security from M1 onward.

## Milestone register

| Milestone | Bounded delivery outcome | Prerequisite | Exit gate |
| --- | --- | --- | --- |
| **M0 Engineering baseline verification** | Verify the existing source-only foundation against real runtime evidence | Existing source assessment | G0 |
| **M1 Authentication and session lifecycle** | Secure login, session expiry, logout and account disablement | G0 verified; D-001 closed | G1 |
| **M2 Organizations and role isolation** | Explicit memberships, roles and organization-scoped access | G1 verified | G2 |
| **M3 Ports and vessels reference data** | Validated, queryable and scoped port/vessel reference records | G2 verified | G3 |
| **M4 Voyages and containers reference data** | Validated voyage schedules and container identity models | G3 verified; D-003 closed | G4 |
| **M5 Shipment lifecycle and concurrency** | Create, edit, search and archive organizational shipments | G4 verified | G5 |
| **M6 Container and voyage assignments** | Temporal and organization-safe shipment assignments | G5 verified | G6 |
| **M7 Tracking contracts and source authentication** | Versioned and authenticated event interface | G6 verified; D-002 closed | G7 |
| **M8 Durable event inbox and immutable history** | Transactional receipt persistence and accepted event provenance | G7 verified; D-006 closed | G8 |
| **M9 Idempotent worker and reconciliation** | At-least-once processing with effect-once business outcomes | G8 verified | G9 |
| **M10 Event timeline and projection rebuild** | Ordered history, current status and explicit correction handling | G9 verified | G10 |
| **M11 Delay rules and shipment exceptions** | Explainable at-risk, delay and conflict detection | G10 verified | G11 |
| **M12 Shipment search and authorized export** | Scoped filtering, detail navigation and CSV output | G11 verified | G12 |
| **M13 Operational KPI definitions and dashboards** | Reconciled, drillable throughput and delay metrics | G12 verified; D-005 closed | G13 |
| **M14 Route mapping and geographic context** | Licensed, simulated and accessible route visualization | G13 verified; D-004 closed | G14 |
| **M15 Scoped in-app notifications** | Authorized event and incident exception messaging | G14 verified | G15 |
| **M16 Audit logging and access hardening** | Append-only evidence and strengthened authorization | G15 verified | G16 |
| **M17 Observability and service health** | Structured metrics, traces and meaningful operational alerts | G16 verified | G17 |
| **M18 IT incident lifecycle and remediation** | Actionable incident triage, notes, and closure evidence | G17 verified | G18 |
| **M19 Database recovery and migration rehearsal** | Verified fresh/historical upgrades and isolated restore | G18 verified; D-008 closed | G19 |
| **M20 Security assurance and dependency review** | Adversarial regression and high-severity issue closure | G19 verified | G20 |
| **M21 Performance and capacity benchmarks** | Measured latency and bounded resource behavior | G20 verified | G21 |
| **M22 Deployment portability and cost controls** | Complete local profile and optional guarded public demo | G21 verified; D-007 closed | G22 |
| **M23 Interface polish and accessibility** | Premium, consistent and accessible enterprise user experience | G22 verified | G23 |
| **M24 Release scenarios and evidence closure** | D01-D06 repeatability, documentation and release decision | G23 verified | G24 |

## Staged delivery plan

### P0: Foundation and maritime records

#### M0: Engineering baseline verification

**Scope:** Verify the existing source-only foundation against real runtime evidence.
**Entry:** Existing implementation reviewed without assuming runtime correctness.
**Required work packages:** M0-01 Assess existing M0 branch; M0-02 Build and test foundations; M0-03 Verify migration and seed; M0-04 Verify local stack and browser; M0-05 Verify failure and hygiene.
**Exit G0:** All applicable tasks, negative tests, runtime evidence and documentation for M0 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Shipment and tenant business functionality.

#### M1: Authentication and session lifecycle

**Scope:** Secure login, session expiry, logout and account disablement.
**Entry:** G0 verified with a documented gate report. Resolve D-001 before applicable feature implementation.
**Required work packages:** M1-01 Lock authentication contract; M1-02 Build credential and session store; M1-03 Implement login and logout endpoints; M1-04 Verify session lifecycle end to end.
**Exit G1:** All applicable tasks, negative tests, runtime evidence and documentation for M1 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Multiple-organization permissions and maritime resources; these begin in M2 and M3.

#### M2: Organizations and role isolation

**Scope:** Explicit memberships, roles and organization-scoped access.
**Entry:** G1 verified with a documented gate report.
**Required work packages:** M2-01 Model memberships and role grants; M2-02 Enforce permission boundary; M2-03 Implement organization selection; M2-04 Verify tenant isolation.
**Exit G2:** All applicable tasks, negative tests, runtime evidence and documentation for M2 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Shipment domain CRUD and maritime reference data.

#### M3: Ports and vessels reference data

**Scope:** Validated, queryable and scoped port/vessel reference records.
**Entry:** G2 verified with a documented gate report.
**Required work packages:** M3-01 Specify port and vessel invariants; M3-02 Implement port persistence and endpoints; M3-03 Implement vessel persistence and endpoints; M3-04 Verify reference data browser flow.
**Exit G3:** All applicable tasks, negative tests, runtime evidence and documentation for M3 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Voyage, container, shipment and tracking persistence.

#### M4: Voyages and containers reference data

**Scope:** Validated voyage schedules and container identity models.
**Entry:** G3 verified with a documented gate report. Resolve D-003 before applicable feature implementation.
**Required work packages:** M4-01 Finalize container identifier policy; M4-02 Implement voyage persistence; M4-03 Implement container persistence; M4-04 Verify voyage and container workflows.
**Exit G4:** All applicable tasks, negative tests, runtime evidence and documentation for M4 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Shipment assignment and tracking events.

#### M5: Shipment lifecycle and concurrency

**Scope:** Create, edit, search and archive organizational shipments.
**Entry:** G4 verified with a documented gate report.
**Required work packages:** M5-01 Specify shipment state transitions; M5-02 Implement shipment schema and commands; M5-03 Implement optimistic concurrency; M5-04 Verify shipment CRUD browser journey.
**Exit G5:** All applicable tasks, negative tests, runtime evidence and documentation for M5 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Container/voyage assignment and event processing.

#### M6: Container and voyage assignments

**Scope:** Temporal and organization-safe shipment assignments.
**Entry:** G5 verified with a documented gate report.
**Required work packages:** M6-01 Define assignment invariants; M6-02 Implement shipment-container links; M6-03 Implement shipment-voyage links; M6-04 Verify complete allocation journey.
**Exit G6:** All applicable tasks, negative tests, runtime evidence and documentation for M6 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Tracking intake or analytics.

### P1: Event ingestion and decision rules

#### M7: Tracking contracts and source authentication

**Scope:** Versioned and authenticated event interface.
**Entry:** G6 verified with a documented gate report. Resolve D-002 before applicable feature implementation.
**Required work packages:** M7-01 Pin external subset and contract; M7-02 Implement source entitlements; M7-03 Implement strict payload validation; M7-04 Verify ingestion contract tests.
**Exit G7:** All applicable tasks, negative tests, runtime evidence and documentation for M7 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Inbox persistence and background processing.

#### M8: Durable event inbox and immutable history

**Scope:** Transactional receipt persistence and accepted event provenance.
**Entry:** G7 verified with a documented gate report. Resolve D-006 before applicable feature implementation.
**Required work packages:** M8-01 Specify inbox and retention states; M8-02 Implement receipt and event schema; M8-03 Implement intake transaction path; M8-04 Verify persistent ingestion behavior.
**Exit G8:** All applicable tasks, negative tests, runtime evidence and documentation for M8 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Worker retry scheduling and projected shipment state.

#### M9: Idempotent worker and reconciliation

**Scope:** At-least-once processing with effect-once business outcomes.
**Entry:** G8 verified with a documented gate report.
**Required work packages:** M9-01 Define worker state transitions; M9-02 Implement transactional worker claim; M9-03 Implement retry and dead-letter queue; M9-04 Verify interrupted-work recovery.
**Exit G9:** All applicable tasks, negative tests, runtime evidence and documentation for M9 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Shipment timelines, ETA rules and UI dashboards.

#### M10: Event timeline and projection rebuild

**Scope:** Ordered history, current status and explicit correction handling.
**Entry:** G9 verified with a documented gate report.
**Required work packages:** M10-01 Specify deterministic event ordering; M10-02 Implement accepted timeline API; M10-03 Implement versioned projections; M10-04 Verify projection rebuild.
**Exit G10:** All applicable tasks, negative tests, runtime evidence and documentation for M10 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Delay classification, notifications and dashboard metrics.

#### M11: Delay rules and shipment exceptions

**Scope:** Explainable at-risk, delay and conflict detection.
**Entry:** G10 verified with a documented gate report.
**Required work packages:** M11-01 Specify delay classification rules; M11-02 Implement exception evaluation; M11-03 Implement exception lifecycle; M11-04 Verify end-to-end exception flow.
**Exit G11:** All applicable tasks, negative tests, runtime evidence and documentation for M11 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Notifications and high-level KPI visualization.

### P2: Operational workspaces

#### M12: Shipment search and authorized export

**Scope:** Scoped filtering, detail navigation and CSV output.
**Entry:** G11 verified with a documented gate report.
**Required work packages:** M12-01 Define search contract and pagination; M12-02 Implement scoped search services; M12-03 Implement controlled CSV export; M12-04 Verify search/export UX.
**Exit G12:** All applicable tasks, negative tests, runtime evidence and documentation for M12 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** KPI aggregation, geographic visualization and notification routing.

#### M13: Operational KPI definitions and dashboards

**Scope:** Reconciled, drillable throughput and delay metrics.
**Entry:** G12 verified with a documented gate report. Resolve D-005 before applicable feature implementation.
**Required work packages:** M13-01 Finalize KPI catalog and cutoffs; M13-02 Implement aggregate query services; M13-03 Build dashboard visualizations; M13-04 Verify dashboard correctness.
**Exit G13:** All applicable tasks, negative tests, runtime evidence and documentation for M13 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Maps and notification delivery.

#### M14: Route mapping and geographic context

**Scope:** Licensed, simulated and accessible route visualization.
**Entry:** G13 verified with a documented gate report. Resolve D-004 before applicable feature implementation.
**Required work packages:** M14-01 Resolve map licensing and fallback; M14-02 Implement route visualization model; M14-03 Build interactive map interface; M14-04 Verify mapped data security.
**Exit G14:** All applicable tasks, negative tests, runtime evidence and documentation for M14 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Notifications and general dashboard widgets.

#### M15: Scoped in-app notifications

**Scope:** Authorized event and incident exception messaging.
**Entry:** G14 verified with a documented gate report.
**Required work packages:** M15-01 Define recipient and deduplication rules; M15-02 Implement notification persistence; M15-03 Implement notification interface; M15-04 Verify recipient isolation.
**Exit G15:** All applicable tasks, negative tests, runtime evidence and documentation for M15 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Infrastructure alerts and incident creation.

### P3: Security and technical operations

#### M16: Audit logging and access hardening

**Scope:** Append-only evidence and strengthened authorization.
**Entry:** G15 verified with a documented gate report.
**Required work packages:** M16-01 Define audit schema and privacy; M16-02 Implement append-only audit entries; M16-03 Harden security boundaries; M16-04 Verify security and audit matrix.
**Exit G16:** All applicable tasks, negative tests, runtime evidence and documentation for M16 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Telemetry dashboards and IT incident lifecycle.

#### M17: Observability and service health

**Scope:** Structured metrics, traces and meaningful operational alerts.
**Entry:** G16 verified with a documented gate report.
**Required work packages:** M17-01 Define telemetry signals and thresholds; M17-02 Implement health and trace propagation; M17-03 Implement monitoring dashboards and alerts; M17-04 Verify diagnostic journey.
**Exit G17:** All applicable tasks, negative tests, runtime evidence and documentation for M17 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Incident ticket workflows and recovery rehearsal.

#### M18: IT incident lifecycle and remediation

**Scope:** Actionable incident triage, notes, and closure evidence.
**Entry:** G17 verified with a documented gate report.
**Required work packages:** M18-01 Define severity and state policy; M18-02 Implement incident CRUD and state controls; M18-03 Implement investigation and remediation; M18-04 Verify alert-to-incident workflow.
**Exit G18:** All applicable tasks, negative tests, runtime evidence and documentation for M18 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Backup restoration and release performance benchmarking.

### P4: Reliability and delivery assurance

#### M19: Database recovery and migration rehearsal

**Scope:** Verified fresh/historical upgrades and isolated restore.
**Entry:** G18 verified with a documented gate report. Resolve D-008 before applicable feature implementation.
**Required work packages:** M19-01 Define recovery targets and safeguards; M19-02 Rehearse schema upgrade paths; M19-03 Implement backup and isolated restore; M19-04 Verify restored integrity.
**Exit G19:** All applicable tasks, negative tests, runtime evidence and documentation for M19 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Comprehensive security scanning and load benchmarks.

#### M20: Security assurance and dependency review

**Scope:** Adversarial regression and high-severity issue closure.
**Entry:** G19 verified with a documented gate report.
**Required work packages:** M20-01 Establish security review baseline; M20-02 Run permission and injection tests; M20-03 Run dependency and endpoint scans; M20-04 Remediate and reverify findings.
**Exit G20:** All applicable tasks, negative tests, runtime evidence and documentation for M20 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Performance benchmarking and hosted deployment.

#### M21: Performance and capacity benchmarks

**Scope:** Measured latency and bounded resource behavior.
**Entry:** G20 verified with a documented gate report.
**Required work packages:** M21-01 Define reference benchmark workload; M21-02 Benchmark shipment search; M21-03 Benchmark event processing and exports; M21-04 Resolve bottlenecks and record limits.
**Exit G21:** All applicable tasks, negative tests, runtime evidence and documentation for M21 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Cloud hosting and final UX polish.

#### M22: Deployment portability and cost controls

**Scope:** Complete local profile and optional guarded public demo.
**Entry:** G21 verified with a documented gate report. Resolve D-007 before applicable feature implementation.
**Required work packages:** M22-01 Finalize deployment profile and budget decision; M22-02 Harden local runtime profile; M22-03 Validate optional demonstration policy; M22-04 Verify deploy and rollback runbook.
**Exit G22:** All applicable tasks, negative tests, runtime evidence and documentation for M22 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Optional public infrastructure is not a mandatory acceptance path.

### P5: Release finish

#### M23: Interface polish and accessibility

**Scope:** Premium, consistent and accessible enterprise user experience.
**Entry:** G22 verified with a documented gate report.
**Required work packages:** M23-01 Define visual system and navigation; M23-02 Polish operational workflows; M23-03 Complete error and offline states; M23-04 Verify accessibility critical paths.
**Exit G23:** All applicable tasks, negative tests, runtime evidence and documentation for M23 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Release acceptance or claims of final product certification.

#### M24: Release scenarios and evidence closure

**Scope:** D01-D06 repeatability, documentation and release decision.
**Entry:** G23 verified with a documented gate report.
**Required work packages:** M24-01 Freeze deterministic demonstration fixtures; M24-02 Run end-to-end scenario suite; M24-03 Audit docs and test evidence; M24-04 Prepare release readiness assessment.
**Exit G24:** All applicable tasks, negative tests, runtime evidence and documentation for M24 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Public hosting and production accreditation; only documented local release readiness is required.

## Legacy milestone mapping

The revised identifiers are a **versioned replacement** for the earlier seven-milestone plan. Historical issue and task references using v1 identifiers are not automatically valid under v2. M0 task IDs M0-01 through M0-05 and gate G0 retain their original meaning.

| Previous milestone (v1) | New milestone boundaries (v2) |
| --- | --- |
| M0 Engineering Baseline | M0 |
| M1 Identity and Shipment Operations | M1-M6 |
| M2 Event Tracking | M7-M11 |
| M3 Analytics and User Interface | M12-M15 |
| M4 Security and IT Service Desk | M16-M18 |
| M5 Cloud and Reliability | M19-M22 |
| M6 Demonstration Release | M23-M24 |

**ID migration rule:** Previous M1-M6 node IDs and G1-G6 must be treated as superseded planning identifiers. The new work package IDs and gates are not equivalent to the old ones. When moving an issue or evidence reference to v2, match its deliverable and tests explicitly rather than copying an old ID. No completed evidence is inferred.

## Release scope boundary

The planned M14 map view and M15 in-app notifications remain in the release baseline, with licensed offline fallback and entitlement tests. A change to omit these requires an explicit PRD, acceptance, roadmap and graph revision. M22's public demonstration feasibility assessment does **not** require deploying to paid infrastructure. Enterprise asset management, service requests, change-management suites, commercial shipping feeds, public hosting, and advanced user customization remain future candidates, not additional G24 blockers.

## Gate transitions and evidence

1. Deliver one work package with a bounded output and a testable negative/failure case; parallelize only where the JSON dependency graph permits.
2. Update relevant security, schema, API, operations and decision records before calling the implementation complete.
3. Execute the specified tests, record exact revision/commands/fixture/environment and distinguish unrun from failed and passed checks.
4. Verify every dependency and applicable [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) before recording a gate as verified; retain an explicit acceptance-pending state if not yet accepted.
5. If a gate is blocked, record it in [Status](STATUS.md). Do not skip ahead to dependent functionality without an approved sequencing change.

## Documentation integrity checks

After any milestone or requirement edit, regenerate the graph with `node scripts/render-execution-graph.mjs`; run `node scripts/render-execution-graph.mjs --check` and `node scripts/check-planning-docs.mjs`. These checks validate structural consistency and links, not application functionality or milestone acceptance.

## Immediate execution

Work against the existing unmerged `feat/m0-engineering-baseline` source. Complete the M0 execution graph tasks and G0 with actual .NET, PostgreSQL, Compose, browser, dependency and failure checks before beginning M1 implementation. No current application feature has been accepted by this roadmap revision.
