# HarborOps Delivery Roadmap

**Version:** 3.0 parallel technical-prerequisite delivery baseline  
**Date:** 2026-10-30  
**State:** M0/G0 through M7/G7 are locally verified, explicitly accepted and integrated into `main` as of 2026-11-03. The twelve-rule M6 contract and G6-S10 snapshot limitation are documented in [gate closure](M6_G6_GATE_CLOSURE.md). G8-G24 remain open.

## Authority and rationale

The [PRD](PRD.md) defines confirmed product outcomes and release boundaries. This roadmap owns delivery scope, milestone prerequisites and gate conditions. [execution-graph.json](execution-graph.json) owns individual work-package IDs and dependency edges and generates [Execution Graph](EXECUTION_GRAPH.md). [Requirements](REQUIREMENTS.md) owns FR/NFR IDs and [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) owns detailed checks. [Status](STATUS.md) alone records actual implementation and acceptance evidence.

The previous seven milestones grouped too many unrelated deliverables together. Version 2.0 decomposes the same planned M0-M6 release into **25 narrower milestones (M0-M24), 101 scoped work packages, and 25 independent evidence gates**. The milestone count and scope remain unchanged. Version 3 replaces artificial gate-by-gate sequencing with a directed acyclic graph of actual technical prerequisites, without approving new features or implementation. No fixed calendar deadlines are imposed by this document.

## Confirmed product strategy

- Deliver a credible, cohesive enterprise demonstration that can evolve toward real deployment after separate requirements and security reviews.
- Complete maritime shipment, tracking, analytics and operations depth before expanding into bookings, customs, commercial logistics and terminal/fleet modules.
- Deliver the initial technical monitoring and incident workflow, with broader service/asset/change management treated as a future extension.
- Require polished premium-enterprise UX, with configurable personalized workspaces deferred.
- Require real multi-user and multi-organization boundaries from the first business-data milestone.
- Require fully functional zero-cost local deployment. Public hosting remains optional if no mandatory charges are necessary.
- No newly nominated non-negotiable features; existing requirements, negative tests and product acceptance criteria remain in force.

## Milestone contract and gate policy

Each milestone has one small, externally testable outcome, typically four work packages, and a single gate **G0 through G24**. A work package is not complete merely when its code is committed. The gate requires actual executed tests and a recorded revision, environment, fixtures, output, failures, security impact, and any outstanding work. A verified gate is **acceptance pending** until explicitly accepted. Only explicitly referenced prerequisite gates must be verified and accepted before their dependent implementation or integration. Milestone numbers and topic stages do not create additional dependencies. Independent work may proceed concurrently on isolated branches; final gate evidence and acceptance remain separate for each milestone.

Security, tenant authorization, data integrity, cost control, migration hygiene and documentation are applied when their related functionality first appears. M16/M20 harden and systematically reverify earlier security measures; they do not postpone basic security from M1 onward.


## Parallel execution and integration policy

The canonical work-package DAG is [execution-graph.json](execution-graph.json), rendered as the [Execution Graph](EXECUTION_GRAPH.md). The [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md) defines branch, database, API contract and verification safeguards. **A milestone number is a label, not a queue position.** P0-P5 are topic groups, not time phases.

- A **hard prerequisite** requires an accepted upstream G gate before dependent implementation begins.
- An **integration prerequisite** may appear on a later work package inside a milestone: earlier isolated design or contract work can proceed while that upstream gate is still open. The downstream package and milestone gate cannot close until the required integration is implemented and tested.
- **Independent work packages** may be implemented in parallel on distinct branches/worktrees and disposable E:-backed resources after their own prerequisites are satisfied.
- **Gate closure** requires the complete positive/negative/security/regression evidence on the integrated source revision and explicit acceptance. Verified but unaccepted gates are not substitutes for accepted prerequisites.
- **G24 release convergence** requires G0-G23 accepted, every cross-track contract integrated, a full final-system regression, M24-03 historical compatibility and M24-04 accessibility assessment. Early G19 recovery or G20 security review must be revalidated for later schema/features at this final gate.

M6/G6 and M7/G7 are complete and integrated. M8-01 inbox contract design, M16 audit foundations, M17 observability, M19 recovery, M20-01 security review and M23-01 design-system definition are independently eligible under their work-package prerequisites. M8 requires the separate D-006 decision before applicable durable intake implementation. Eligibility never implies acceptance.

## Milestone register

| Milestone | Bounded delivery outcome | Prerequisite | Exit gate |
| --- | --- | --- | --- |
| **M0 Engineering baseline verification** | Verify the existing source-only foundation against real runtime evidence | Existing source assessment | G0 |
| **M1 Authentication and session lifecycle** | Secure login, session expiry, logout and account disablement | G0 accepted; D-001 resolved before applicable implementation | G1 |
| **M2 Organizations and role isolation** | Explicit memberships, roles and organization-scoped access | G1 accepted | G2 |
| **M3 Ports and vessels reference data** | Validated, queryable and scoped port/vessel reference records | G2 accepted | G3 |
| **M4 Voyages and containers reference data** | Validated voyage schedules and container identity models | G3 accepted; D-003 resolved before applicable implementation | G4 |
| **M5 Shipment lifecycle and concurrency** | Create, edit, search and archive organizational shipments | G3 accepted | G5 |
| **M6 Container and voyage assignments** | Temporal and organization-safe shipment assignments | G4 accepted, G5 accepted | G6 |
| **M7 Tracking contracts and source authentication** | Versioned and authenticated event interface | G3 accepted; D-002 accepted 2026-11-03 | G7 |
| **M8 Durable event inbox and immutable history** | Transactional receipt persistence and accepted event provenance | G7 accepted, G6 accepted; D-006 resolved before applicable implementation | G8 |
| **M9 Idempotent worker and reconciliation** | At-least-once processing with effect-once business outcomes | G8 accepted | G9 |
| **M10 Event timeline and projection rebuild** | Ordered history, current status and explicit correction handling | G9 accepted | G10 |
| **M11 Delay rules and shipment exceptions** | Explainable at-risk, delay and conflict detection | G10 accepted | G11 |
| **M12 Shipment search and authorized export** | Scoped filtering, detail navigation and CSV output | G5 accepted | G12 |
| **M13 Operational KPI definitions and dashboards** | Reconciled, drillable throughput and delay metrics | G11 accepted, G12 accepted; D-005 resolved before applicable implementation | G13 |
| **M14 Route mapping and geographic context** | Licensed and clearly labeled simulated route visualization | G4 accepted; D-004 resolved before applicable implementation | G14 |
| **M15 Scoped in-app notifications** | Authorized event and incident exception messaging | G11 accepted | G15 |
| **M16 Audit logging and access hardening** | Append-only evidence and strengthened authorization | G3 accepted | G16 |
| **M17 Observability and service health** | Structured metrics, traces and meaningful operational alerts | G3 accepted | G17 |
| **M18 IT incident lifecycle and remediation** | Actionable incident triage, notes, and closure evidence | G17 accepted, G16 accepted | G18 |
| **M19 Database recovery and migration rehearsal** | Verified fresh/historical upgrades and isolated restore | G3 accepted; D-008 resolved before applicable implementation | G19 |
| **M20 Security assurance and dependency review** | Adversarial regression and high-severity issue closure | G3 accepted, G15 accepted, G18 accepted, G19 accepted | G20 |
| **M21 Performance and capacity benchmarks** | Measured latency and bounded resource behavior | G9 accepted, G12 accepted, G13 accepted | G21 |
| **M22 Deployment portability and cost controls** | Complete local profile and optional guarded public demo | G19 accepted, G20 accepted, G21 accepted; D-007 resolved before applicable implementation | G22 |
| **M23 Interface polish and workflow consistency** | Polished enterprise interface with complete functional workflows | G3 accepted, G12 accepted, G13 accepted, G14 accepted, G15 accepted, G18 accepted | G23 |
| **M24 Final release validation and acceptance** | Complete release scenarios, final accessibility audit and acceptance evidence | G22 accepted, G23 accepted | G24 |

## Accessibility verification sequencing

All dedicated accessibility testing is deferred to **M24-04, the final work package before G24 and the release acceptance decision**. No early milestone, including M0 and M23, has a keyboard, focus, screen-reader, contrast, zoom, accessible-name, or WCAG acceptance gate. Earlier milestones still validate that their implemented behavior works and is secure, but do not perform accessibility certification or audits. M23 completes interface polish; M24-04 evaluates the finished experience and records any remediation and re-test evidence before G24 can close. Accessibility remains part of the final release scope, not a waived requirement.

## Quarter-mile reliability checkpoints

Historical Docker/PostgreSQL volume compatibility is **not an M0 acceptance requirement** and is **not repeated at every commit, branch change or CI run**. Clean-database initialization, migration, deterministic seed and outage/recovery remain mandatory for G0.

The 25-milestone roadmap retains four legacy numbered checkpoint gates as fixed obligations. With parallel execution, these milestone labels no longer imply actual 25/50/75-percent chronological progress; they are mandatory checkpoints at the named gate regardless of completion order:

| Checkpoint | Milestone | Verification |
| --- | --- | --- |
| Q1, 25% | M6 / G6 | Exercise current PostgreSQL image against a disposable copy of an older representative data snapshot |
| Q2, 50% | M12 / G12 | Repeat compatibility with the latest retained historical snapshot and migration path |
| Q3, 75% | M18 / G18 | Repeat the isolated snapshot boot/read and integrity spot checks |
| Q4, final | M24-03 / G24 | Final historical snapshot compatibility review before M24-04 accessibility testing and final release assessment |

At each checkpoint, **if no representative historical volume exists**, record `NOT APPLICABLE` with a reason; never fabricate a test result or create a dependency on a user's personal workstation volume. Use a disposable clone or restored test backup, never the only original E-drive data volume. Preserve source and snapshot identification, PostgreSQL major version, startup, migration and representative read results.

**Exception:** Before applying a breaking schema, PostgreSQL-major or storage-layout change to an existing retained dataset, validate that migration against a safe copy at that change's rollout. This is deployment safety, not an extra routine M0 gate.

M19 remains the mandatory, more comprehensive **backup/restore and historical schema migration rehearsal**, separate from these quarterly lightweight compatibility checkpoints. Quarter checkpoints are milestone-based, not an automatic quarterly schedule.

## Staged delivery plan

### P0: Foundation and maritime records

#### M0: Engineering baseline verification

**Scope:** Verify the existing source-only foundation against real runtime evidence.
**Entry:** Existing implementation reviewed without assuming runtime correctness.
**Required work packages:** M0-01 Assess existing M0 branch; M0-02 Build and test foundations; M0-03 Verify migration and seed; M0-04 Verify local stack and browser; M0-05 Verify failure and hygiene.
**Exit G0:** Verified by GitHub Actions run #2 at `fec7429415fede121db8a4a34f0df72cd4fac609` and explicitly accepted on 2026-10-31. M0 was merged into `main`. G1 was subsequently verified, accepted and merged into `main` via PR #4; G2 and G3 were verified locally and accepted on 2026-10-31. G5 and G6 were subsequently accepted on 2026-11-02, and G7 was accepted on 2026-11-03; G8-G24 remain open.
**Not covered by this milestone:** Shipment and tenant business functionality.

#### M1: Authentication and session lifecycle

**Scope:** Secure local account login, PostgreSQL-authoritative cookie session lifecycle, expiry, logout, logout-all, self-disablement and a functional authenticated browser shell. Full design in [M1 Authentication Design](M1_AUTHENTICATION_DESIGN.md).
**Entry:** G0 verified and accepted. D-001 accepted; see [D-001](ADR_D001_BROWSER_SESSIONS.md) and the [M1 Authentication Design](M1_AUTHENTICATION_DESIGN.md).
**Required work packages:** M1-01 Lock authentication contract and D-001; M1-02 Build additive identity/credential/session/event persistence; M1-03 Implement cookie auth, CSRF, login, logout and disablement endpoints; M1-04 Implement same-origin browser auth and execute [G1 verification](M1_VERIFICATION_PLAN.md).
**Exit G1:** Verified by [M1 verification evidence](M1_IMPLEMENTATION_VERIFICATION.md) and accepted on 2026-10-31. Merged via PR #4 at `8f9fc0d506a9e1dc1d4f5acab936622df988f413`. No production deployment approval is implied.
**Not covered by this milestone:** Organization membership and role isolation (M2), maritime resources (M3 onward), integration tokens (M7), public registration, external SSO, historical-volume checks and accessibility audit (M24-04). **M1 implemented, verified, accepted and merged into `main`.**

#### M2: Organizations and role isolation

**Scope:** Explicit memberships, roles and organization-scoped access. The [M2 architecture](M2_ARCHITECTURE_DESIGN.md) specifies organization/membership persistence, role grants, per-session organization selection and fail-closed tenant boundaries. The verified implementation is integrated into `main`.
**Entry:** G1 verified and accepted, with documented evidence and merged source.
**Required work packages:** M2-01 Model memberships and role grants; M2-02 Enforce permission boundary; M2-03 Implement organization selection; M2-04 Verify tenant isolation. See the [M2 implementation plan](M2_IMPLEMENTATION_PLAN.md) and [G2 verification plan](M2_VERIFICATION_PLAN.md).
**Verification:** All 25 scenarios of the [G2 verification plan](M2_VERIFICATION_PLAN.md) were executed locally on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1`. The [executed record](M2_EXECUTED_VERIFICATION.md) lists the environment, E: storage configuration, suite counts, per-scenario evidence, security scans and the failure history. Earlier GitHub runs are retained there as history only.
**Exit G2:** All 25 scenarios passed locally on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1`. G2 accepted on 2026-10-31; M2 merged via PR #5 at `1df638d5300191f6c2c6c9d201fd5f4c4ab9e562`. M3 subsequently passed complete local G3 verification, was explicitly accepted on 2026-10-31 and integrated into `main`.
**Not covered by this milestone:** Shipment domain CRUD and maritime reference data.

#### M3: Ports and vessels reference data

**Scope:** Validated, queryable and scoped port/vessel reference records.
**Entry:** G2 verified, explicitly accepted and merged, with [executed evidence](M2_EXECUTED_VERIFICATION.md).
**Required work packages:** M3-01 Specify port and vessel invariants; M3-02 Implement port persistence and endpoints; M3-03 Implement vessel persistence and endpoints; M3-04 Verify reference data browser flow. All four work packages are implemented and G3-S01 through G3-S25 passed locally on `b72025d35d62d0a8a45b466986a0b3bd89eebadb` ([executed verification](M3_EXECUTED_VERIFICATION.md)).
**Exit G3:** All 25 mandatory scenarios passed on the verified M3 source revision; G3 explicitly accepted on 2026-10-31 and M3 integrated into `main` by fast-forward. G4 was subsequently accepted on 2026-11-01.
**Not covered by this milestone:** Voyage, container, shipment and tracking persistence.

#### M4: Voyages and containers reference data

**Scope:** Validated organization-owned voyages and containers with explicit schedule/progress, checked container identity, tenant-safe persistence, versioned API and browser workflows.
**Entry:** G3 explicitly accepted. Resolve D-003 before implementing the identifier contract.
**Required work packages:** M4-01 finalize D-003 and freeze domain/API contracts; M4-02 voyage persistence/API; M4-03 container persistence/API in parallel after M4-01; M4-04 browser integration and G4. See [M4 architecture](M4_ARCHITECTURE_DESIGN.md) and [implementation plan](M4_IMPLEMENTATION_PLAN.md).
**Exit G4:** Accepted 2026-11-01 after independently reviewed [executed verification](M4_EXECUTED_VERIFICATION.md). All required executable tests passed on `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6`; the evidence stage returned 1 only because D-003 was pending at run time, and D-003 Option B was explicitly approved before G4 acceptance.
**Decision:** [D-003 Option B](DECISIONS.md) accepted on 2026-11-01. ISO 6346:2022 U-only and tagged synthetic identifiers; full policy in the [decision record](M4_D003_CONTAINER_IDENTIFIER_PROPOSAL.md).
**Status:** M4-01 through M4-04 completed and accepted; M4 fast-forward integrated into `main`. [Implementation status](M4_IMPLEMENTATION_STATUS.md) and [G4 executed evidence](M4_EXECUTED_VERIFICATION.md) preserve scope and acceptance details.
**Not covered by this milestone:** M5 shipments, M6 assignments, tracking events, external registry verification.

#### M5: Shipment lifecycle and concurrency

**Scope:** Create, edit, search and archive organizational shipments.
**Entry:** G3 accepted before respective dependent work packages.
**Required work packages:** M5-01 freeze domain, lifecycle and roles; M5-02 implement schema and tenant-scoped commands; M5-03 implement optimistic concurrency and revocation races (parallel after M5-01); M5-04 verify full three-browser CRUD. Assignments remain M6. See [M5 plan](M5_IMPLEMENTATION_PLAN.md).
**Exit G5:** Accepted on 2026-11-02, based on the complete six-rule local contract and independently reviewed run 13 source `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91`. All 25 scenarios closed, 601 test executions passed, all 26 executable stages passed, and Trivy HIGH/CRITICAL findings were zero. The two derived report corrections changed no raw evidence or tested source. See [gate closure](M5_G5_GATE_CLOSURE.md).
**Not covered by this milestone:** Container/voyage assignment and event processing.

#### M6: Container and voyage assignments

**Scope:** Temporal and organization-safe shipment assignments.
**Entry:** G4 accepted, G5 accepted before respective dependent work packages.
**Required work packages:** M6-01 define and approve assignment invariants, M6-02 implement shipment-container links and M6-03 implement shipment-voyage links in parallel after M6-01, M6-04 integrate the complete allocation journey. See [architecture](M6_ARCHITECTURE_DESIGN.md) and [implementation plan](M6_IMPLEMENTATION_PLAN.md).
**Exit G6:** Accepted and integrated into `main` on 2026-11-02. Executable G6-S02 through G6-S25 passed on source `a94d7cd406a93806527d28ba7d2ce2d6ba3307b6`; G6-S01 closed by explicit approval of the [twelve-rule contract](M6_LOCAL_CONTRACT_DECISION.md). The original evidence stage exit 1 is retained as historical fact. The M0-only snapshot limitation and populated M5 upgrade proof are documented in [G6 closure](M6_G6_GATE_CLOSURE.md).
**Q1 snapshot checkpoint:** Verify a disposable copy of a retained historical PostgreSQL volume if available; otherwise record NOT APPLICABLE. This is a milestone checkpoint, not a per-commit test.
**Not covered by this milestone:** Tracking intake or analytics.

### P1: Event ingestion and decision rules

#### M7: Tracking contracts and source authentication

**Scope:** Versioned and authenticated event interface.
**Entry:** G3 accepted before respective dependent work packages. D-002 is accepted (2026-11-03, Option A).
**Required work packages:** M7-01 Pin external subset and contract; M7-02 Implement source entitlements; M7-03 Implement strict payload validation; M7-04 Verify ingestion contract tests. [D-002 proposal](M7_D002_TRACKING_REFERENCE_PROPOSAL.md), [architecture](M7_ARCHITECTURE_DESIGN.md), [implementation plan](M7_IMPLEMENTATION_PLAN.md) and [G7 matrix](M7_VERIFICATION_PLAN.md) define the solution; the implementation is recorded in [M7 status](M7_IMPLEMENTATION_STATUS.md) and its local technical verification in [M7 executed verification](M7_EXECUTED_VERIFICATION.md); acceptance is pending.
**Exit G7:** Explicitly accepted 2026-11-03 after 25/25 reconciled scenarios, independent supplement review and D-002 Option A. Integrated via PR #10 and PR #11; original run and supplements are preserved separately in [G7 closure](M7_G7_GATE_CLOSURE.md).
**Not covered by this milestone:** Inbox persistence and background processing.

#### M8: Durable event inbox and immutable history

**Scope:** Transactional receipt persistence and accepted event provenance.
**Entry:** G7 accepted, G6 accepted before respective dependent work packages. Resolve D-006 before applicable feature implementation. G7 permits contract design; M8-02 and M8-03 additionally wait for G6.
**Required work packages:** M8-01 Specify inbox and retention states; M8-02 Implement receipt and event schema; M8-03 Implement intake transaction path; M8-04 Verify persistent ingestion behavior. [M8 architecture](M8_ARCHITECTURE_DESIGN.md), [D-006 proposal](M8_D006_DURABILITY_PROPOSAL.md), [implementation plan](M8_IMPLEMENTATION_PLAN.md), [G8 matrix](M8_VERIFICATION_PLAN.md) and [planning register](M8_PLANNING_STATUS.md) are drafted; D-006 acceptance pending, no implementation or executed G8 evidence.
**Exit G8:** All applicable tasks, negative tests, runtime evidence and documentation for M8 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Worker retry scheduling and projected shipment state.

#### M9: Idempotent worker and reconciliation

**Scope:** At-least-once processing with effect-once business outcomes.
**Entry:** G8 accepted before respective dependent work packages.
**Required work packages:** M9-01 Define worker state transitions; M9-02 Implement transactional worker claim; M9-03 Implement retry and dead-letter queue; M9-04 Verify interrupted-work recovery.
**Exit G9:** All applicable tasks, negative tests, runtime evidence and documentation for M9 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Shipment timelines, ETA rules and UI dashboards.

#### M10: Event timeline and projection rebuild

**Scope:** Ordered history, current status and explicit correction handling.
**Entry:** G9 accepted before respective dependent work packages.
**Required work packages:** M10-01 Specify deterministic event ordering; M10-02 Implement accepted timeline API; M10-03 Implement versioned projections; M10-04 Verify projection rebuild.
**Exit G10:** All applicable tasks, negative tests, runtime evidence and documentation for M10 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Delay classification, notifications and dashboard metrics.

#### M11: Delay rules and shipment exceptions

**Scope:** Explainable at-risk, delay and conflict detection.
**Entry:** G10 accepted before respective dependent work packages.
**Required work packages:** M11-01 Specify delay classification rules; M11-02 Implement exception evaluation; M11-03 Implement exception lifecycle; M11-04 Verify end-to-end exception flow.
**Exit G11:** All applicable tasks, negative tests, runtime evidence and documentation for M11 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Notifications and high-level KPI visualization.

### P2: Operational workspaces

#### M12: Shipment search and authorized export

**Scope:** Scoped filtering, detail navigation and CSV output.
**Entry:** G5 accepted before respective dependent work packages.
**Required work packages:** M12-01 Define search contract and pagination; M12-02 Implement scoped search services; M12-03 Implement controlled CSV export; M12-04 Verify search/export UX.
**Exit G12:** All applicable tasks, negative tests, runtime evidence and documentation for M12 recorded; acceptance pending until explicitly confirmed.
**Q2 snapshot checkpoint:** Verify a disposable copy of a retained historical PostgreSQL volume if available; otherwise record NOT APPLICABLE. This is a milestone checkpoint, not a per-commit test.
**Not covered by this milestone:** KPI aggregation, geographic visualization and notification routing.

#### M13: Operational KPI definitions and dashboards

**Scope:** Reconciled, drillable throughput and delay metrics.
**Entry:** G11 accepted, G12 accepted before respective dependent work packages. Resolve D-005 before applicable feature implementation. KPI definitions may begin after G11, but aggregate-query integration waits for G12.
**Required work packages:** M13-01 Finalize KPI catalog and cutoffs; M13-02 Implement aggregate query services; M13-03 Build dashboard visualizations; M13-04 Verify dashboard correctness.
**Exit G13:** All applicable tasks, negative tests, runtime evidence and documentation for M13 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Maps and notification delivery.

#### M14: Route mapping and geographic context

**Scope:** Licensed and clearly labeled simulated route visualization.
**Entry:** G4 accepted before respective dependent work packages. Resolve D-004 before applicable feature implementation.
**Required work packages:** M14-01 Resolve map licensing and fallback; M14-02 Implement route visualization model; M14-03 Build interactive map interface; M14-04 Verify mapped data security.
**Exit G14:** All applicable tasks, negative tests, runtime evidence and documentation for M14 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Notifications and general dashboard widgets.

#### M15: Scoped in-app notifications

**Scope:** Authorized event and incident exception messaging.
**Entry:** G11 accepted before respective dependent work packages.
**Required work packages:** M15-01 Define recipient and deduplication rules; M15-02 Implement notification persistence; M15-03 Implement notification interface; M15-04 Verify recipient isolation.
**Exit G15:** All applicable tasks, negative tests, runtime evidence and documentation for M15 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Infrastructure alerts and incident creation.

### P3: Security and technical operations

#### M16: Audit logging and access hardening

**Scope:** Append-only evidence and strengthened authorization.
**Entry:** G3 accepted before respective dependent work packages.
**Required work packages:** M16-01 Define audit schema and privacy; M16-02 Implement append-only audit entries; M16-03 Harden security boundaries; M16-04 Verify security and audit matrix.
**Exit G16:** All applicable tasks, negative tests, runtime evidence and documentation for M16 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Telemetry dashboards and IT incident lifecycle.

#### M17: Observability and service health

**Scope:** Structured metrics, traces and meaningful operational alerts.
**Entry:** G3 accepted before respective dependent work packages.
**Required work packages:** M17-01 Define telemetry signals and thresholds; M17-02 Implement health and trace propagation; M17-03 Implement monitoring dashboards and alerts; M17-04 Verify diagnostic journey.
**Exit G17:** All applicable tasks, negative tests, runtime evidence and documentation for M17 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Incident ticket workflows and recovery rehearsal.

#### M18: IT incident lifecycle and remediation

**Scope:** Actionable incident triage, notes, and closure evidence.
**Entry:** G17 accepted, G16 accepted before respective dependent work packages. Incident policy starts after G17; CRUD integration waits for G16.
**Required work packages:** M18-01 Define severity and state policy; M18-02 Implement incident CRUD and state controls; M18-03 Implement investigation and remediation; M18-04 Verify alert-to-incident workflow.
**Exit G18:** All applicable tasks, negative tests, runtime evidence and documentation for M18 recorded; acceptance pending until explicitly confirmed.
**Q3 snapshot checkpoint:** Verify a disposable copy of a retained historical PostgreSQL volume if available; otherwise record NOT APPLICABLE. This is a milestone checkpoint, not a per-commit test.
**Not covered by this milestone:** Backup restoration and release performance benchmarking.

### P4: Reliability and delivery assurance

#### M19: Database recovery and migration rehearsal

**Scope:** Verified fresh/historical upgrades and isolated restore.
**Entry:** G3 accepted before respective dependent work packages. Resolve D-008 before applicable feature implementation.
**Required work packages:** M19-01 Define recovery targets and safeguards; M19-02 Rehearse schema upgrade paths; M19-03 Implement backup and isolated restore; M19-04 Verify restored integrity.
**Exit G19:** All applicable tasks, negative tests, runtime evidence and documentation for M19 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Comprehensive security scanning and load benchmarks.

#### M20: Security assurance and dependency review

**Scope:** Adversarial regression and high-severity issue closure.
**Entry:** G3 accepted, G15 accepted, G18 accepted, G19 accepted before respective dependent work packages. M20-01 can begin after G3; adversarial regression waits for G15 and G18, dependency scanning for G19.
**Required work packages:** M20-01 Establish security review baseline; M20-02 Run permission and injection tests; M20-03 Run dependency and endpoint scans; M20-04 Remediate and reverify findings.
**Exit G20:** All applicable tasks, negative tests, runtime evidence and documentation for M20 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Performance benchmarking and hosted deployment.

#### M21: Performance and capacity benchmarks

**Scope:** Measured latency and bounded resource behavior.
**Entry:** G9 accepted, G12 accepted, G13 accepted before respective dependent work packages. Benchmark plans start after G9; shipment queries wait for G12, final bottleneck closure for G13.
**Required work packages:** M21-01 Define reference benchmark workload; M21-02 Benchmark shipment search; M21-03 Benchmark event processing and exports; M21-04 Resolve bottlenecks and record limits.
**Exit G21:** All applicable tasks, negative tests, runtime evidence and documentation for M21 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Cloud hosting and final UX polish.

#### M22: Deployment portability and cost controls

**Scope:** Complete local profile and optional guarded public demo.
**Entry:** G19 accepted, G20 accepted, G21 accepted before respective dependent work packages. Resolve D-007 before applicable feature implementation. Planning may start after G19; hardening waits for G20, and the final runbook check waits for G21.
**Required work packages:** M22-01 Finalize deployment profile and budget decision; M22-02 Harden local runtime profile; M22-03 Validate optional demonstration policy; M22-04 Verify deploy and rollback runbook.
**Exit G22:** All applicable tasks, negative tests, runtime evidence and documentation for M22 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Optional public infrastructure is not a mandatory acceptance path.

### P5: Release finish

#### M23: Interface polish and workflow consistency

**Scope:** Polished enterprise interface with complete functional workflows. No accessibility testing is required in M23.
**Entry:** G3 accepted, G12 accepted, G13 accepted, G14 accepted, G15 accepted, G18 accepted before respective dependent work packages. Visual-system definition may start after G3; complete operational workflow polish waits for G12, G13, G14, G15 and G18.
**Required work packages:** M23-01 Define visual system and navigation; M23-02 Polish operational workflows; M23-03 Complete error and offline states; M23-04 Finish interface consistency review.
**Exit G23:** All applicable tasks, negative tests, runtime evidence and documentation for M23 recorded; acceptance pending until explicitly confirmed.
**Not covered by this milestone:** Release acceptance or claims of final product certification.

#### M24: Final release validation and acceptance

**Scope:** Complete release scenarios and then perform the only formal accessibility test pass as the final work package before G24. Include automated checks and manual screen-reader, keyboard, focus, labels, contrast, zoom, responsive layouts, and complete workflows.
**Entry:** G22 accepted, G23 accepted before respective dependent work packages.
**Required work packages:** M24-01 Freeze deterministic demonstration fixtures; M24-02 Run end-to-end scenario suite; M24-03 Audit docs and test evidence; M24-04 Run final accessibility tests and release assessment.
**Exit G24:** All applicable tasks, negative tests, runtime evidence and documentation for M24 recorded; acceptance pending until explicitly confirmed.
**Q4 snapshot checkpoint:** M24-03 records the final historical-volume compatibility check, if a representative snapshot exists; the last work package M24-04 remains the complete final accessibility audit.
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

## Workspace constraint

All local development, test, cache, build, container and evidence storage must comply with the [E-drive Local Storage Policy](LOCAL_STORAGE_POLICY.md). Do not add new persistent C-drive storage. Unavoidable brief Windows temporary activity is permitted. Verify actual disk paths when starting each milestone.

## Documentation integrity checks

After any milestone or requirement edit, regenerate the graph with `node scripts/render-execution-graph.mjs`; run `node scripts/render-execution-graph.mjs --check` and `node scripts/check-planning-docs.mjs`. These checks validate structural consistency and links, not application functionality or milestone acceptance.

## Immediate execution

M0-M7/G0-G7 are verified, explicitly accepted and integrated into `main`. Eligible next work includes M8-01 (D-006 pending before applicable implementation), M16, M17, M19 (D-008 pending), M20-01 and M23-01, subject to per-package prerequisites in the execution graph. The dependency graph remains unchanged. G8-G24 remain open. Public source visibility is separate from production-hosting acceptance.
