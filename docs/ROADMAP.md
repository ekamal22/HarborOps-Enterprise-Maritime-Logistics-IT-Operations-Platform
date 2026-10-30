# HarborOps Delivery Roadmap

**Version:** 1.0 planning baseline  
**Date:** 2026-10-30  
**State:** Proposed, acceptance pending

## Purpose and authority

This roadmap owns the approved planning sequence, milestone scopes, prerequisites, and exit-gate definitions for M0-M6. [PRD](PRD.md) owns product intent and release scope; [Requirements](REQUIREMENTS.md) owns FR/NFR IDs; [execution-graph.json](execution-graph.json) owns the executable work-package dependency data, rendered in [Execution Graph](EXECUTION_GRAPH.md); [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) owns detailed release checks; [Status](STATUS.md) alone owns actual implementation, verification, and acceptance state. [Implementation Plan](IMPLEMENTATION_PLAN.md) is supporting historical implementation guidance, not an independent schedule or authority.

Week estimates are indicative relative to actual available development time, not calendar commitments. Milestone ordering is mandatory at gate transitions, but independent work packages inside a milestone may run in parallel when graph prerequisites permit.

## Delivery principles

1. Each milestone ends with reproducible, recorded verification and a review gate. Code existence does not satisfy a gate.
2. Core security starts in M1; M4 expands hardening, telemetry, and incident management.
3. Avoid mandatory paid infrastructure, proprietary tracking feeds, and unnecessary distributed services.
4. Preserve organization boundaries and event integrity in all subsequently introduced features.
5. No milestone is accepted by implication. A passing gate becomes **acceptance pending** until acceptance is explicitly confirmed.
6. An unmet gate blocks promotion of dependent milestone implementation, except explicit read-only discovery or a separately documented change in dependency policy.
7. Maintain requirement -> work-package -> acceptance-check -> evidence traceability, using stable IDs.

## Milestone overview

| Milestone | Indicative phase | Delivery outcome | Gate |
| --- | --- | --- | --- |
| M0 Engineering Baseline | Week 1 | Repeatable frontend/API/PostgreSQL foundation, migration, seed, health, baseline verification | G0 |
| M1 Core Shipment Operations | Weeks 1-2 | Scoped identity, reference data, shipments, assignments, working browser flow | G1 |
| M2 Tracking Integrity | Weeks 3-4 | Versioned durable event processing, worker recovery, timelines, exceptions | G2 |
| M3 Operational Analytics | Weeks 5-6 | Scoped search/export, reconciled dashboards, maps, exception experience | G3 |
| M4 Security and IT Operations | Weeks 7-8 | Auditing, hardening, observability, incident lifecycle and fault diagnosis | G4 |
| M5 Reliability and Deployment | Weeks 9-10 | Migration and restore rehearsal, benchmarks, security review, optional guarded hosting | G5 |
| M6 Release Demonstration | Weeks 11-12 | Accessible UX, deterministic D01-D06 demos, evidence, documentation and release assessment | G6 |

## M0: Engineering baseline

**Entry:** Existing documentation decisions ADR-001 through ADR-006; no prior runtime assumed.

**Deliver:** .NET solution and API, React/Vite frontend, PostgreSQL EF Core migration, deterministic bootstrap seed, JSON logging, correlation IDs, liveness/readiness, one-shot migrator, Docker Compose, test projects, local setup/validation scripts.

**Gate G0 requires:** Clean .NET restore/build/unit/contract/integration execution; React tests and build; PostgreSQL migration and repeatable seed; Compose validation and clean startup; browser -> API -> database observation; liveness versus readiness under a controlled database outage; secret and dependency review; documented exact commands and environment. Reference [M0 Verification](M0_VERIFICATION.md) for existing feature-branch evidence.

**Current evidence:** Source exists on `feat/m0-engineering-baseline`; full runtime validation has not occurred. G0 is open. Nothing in M1 should be marked implemented by this fact.

## M1: Identity, organizations, and shipment operations

**Entry:** G0 verified; D-001 session/integration authentication approach and D-003 identifier policy decided.

**Deliver:** Organization memberships, session lifecycle and role enforcement; ports, vessels, voyages, containers, shipments and assignment schema; validated CRUD, archive and optimistic concurrency; scoped list/detail/search; browser create/edit/link flow and deterministic two-organization fixtures.

**Gate G1 requires:** Authorized shipment flow works via browser and API; invalid and conflicting writes leave no partial state; unauthenticated, wrong-role, disabled membership, and foreign-organization calls deny retrieval, mutation, filter, and export attempts; migrations and isolation integration suites pass. See FR-001 through FR-005 and acceptance sections A/D.

## M2: Durable tracking events and projections

**Entry:** G1 verified; D-002 pinned DCSA subset and D-006 durability/reconciliation design decided.

**Deliver:** Authenticated versioned ingress, strict payload validation and receipt persistence; source-scoped deduplication and immutable accepted events; transactional worker processing, bounded retries and dead-letter/reconciliation; late/correction-aware projection, timeline, deterministic ETA/status and exception rule fixtures; event security tests.

**Gate G2 requires:** New event visibility is durable; duplicates produce one business effect; conflicting IDs, forged sources, malformed and unknown events are handled explicitly; restart and database failure do not cause silent loss; recomputed projections match fixtures. See FR-006 through FR-010 and acceptance section B.

## M3: Operational analytics and user experience

**Entry:** G2 verified; D-004 tile/license decision and D-005 KPI tolerances finalized.

**Deliver:** Scoped shipment search/filter, view and bounded CSV export; explainable KPI aggregates with drill-through and denominator definition; route/map context with compliant fallback; exception triage and authorized, deduplicated in-app notifications; responsive, keyboard-usable operational views.

**Gate G3 requires:** KPIs reconcile with persisted fixture rows; projected at-risk/delayed states match rule tests; maps and exports obey tenant scope; simulated source is labeled; browser workflows and accessibility-critical checks pass. See FR-011 through FR-013 and FR-018/FR-019, acceptance section C.

## M4: Security hardening, observability, and service desk

**Entry:** G3 verified.

**Deliver:** Security/session/regression matrix and append-only audit records; rate limits and sensitive-log redaction; request and worker metrics, traces, health panels and alerts; incident ticket model, lifecycle, investigation evidence, root-cause and remediation actions; controlled outage-to-recovery journey.

**Gate G4 requires:** Threat and authorization matrix passes; no sensitive values leak to logs, metrics, or exports; injected failure is detected and represented by an incident; recovery and closeout evidence correlate with the original signal. See FR-014 through FR-016 and FR-020, acceptance sections D/E.

## M5: Reliability rehearsal and deployment boundary

**Entry:** G4 verified; D-007 budget/hosting policy and D-008 recovery targets decided before relevant work.

**Deliver:** Isolated backup and restoration validation, fresh/historical migration rehearsal, worker and database failure drills; measured reference load/performance; dependency and security review; documented local-full-stack deployment profile. Cloud hosting remains optional and cannot be required for acceptance if the fully verified local-equivalent profile meets the product requirement.

**Gate G5 requires:** Restore has validated counts, relationships, migration state, and sample API behavior; failure recovery is verified; performance evidence includes dataset, hardware, load and latency distribution; security findings are resolved or explicitly block; no unapproved paid services. See NFR-010 through NFR-012 and acceptance section F.

## M6: Release demonstration and product closure

**Entry:** G5 verified.

**Deliver:** Cohesive accessible UI with correct empty/error/permission states; deterministic complete fixtures; repeatable D01-D06 journey; documentation, operational runbooks, status ledger, evidence index, limitations and release readiness decision.

**Gate G6 requires:** All applicable release checks in [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) pass; six demo scenarios have recorded results, all required test layers pass, no critical/high security blocker remains, and documentation reflects exactly what was observed. Release becomes acceptance pending until explicitly accepted.

## Cross-cutting tracks

| Track | Starts | Continues through | Rule |
| --- | --- | --- | --- |
| Security and tenant isolation | M1 | M6 | No new business endpoint without negative authorization tests |
| Database migrations and recoverability | M0 | M6 | Rehearse before treating schema and recovery as verified |
| Traceability and evidence | M0 | M6 | Record revision, command, environment, output, and outstanding tests at each gate |
| Synthetic data and provenance | M0 | M6 | No real customer feeds; simulated values visibly labeled |
| Documentation and decisions | M0 | M6 | Keep one authoritative location per concern and resolve contradictions explicitly |
| Cost control | M0 | M6 | Essential workflows never require a paid account |

## Dependencies and change management

The binding graph of work packages and explicit gate dependencies is [execution-graph.json](execution-graph.json). Render it with `node scripts/render-execution-graph.mjs`; inspect its [readable view](EXECUTION_GRAPH.md). Before starting a graph node, confirm all its `depends_on` nodes have evidence that meets their gate requirements. A gate node cannot be closed by source inspection, skipped tests, a successful commit, or an unapproved waiver.

To change priority or sequence, revise the PRD if product scope changes, this roadmap if milestones or gates change, and the graph data if work packages or dependencies change. Then update requirement mappings, acceptance checks, and [Status](STATUS.md) as appropriate. A blocker or intentional deferral must be recorded, not silently removed. Existing accepted architecture decisions remain active until deliberately revised in [Decisions](DECISIONS.md).

## Next actionable work

Complete M0 verification against the existing feature branch. Follow the graph from M0-01 through G0 and record actual evidence in the dedicated M0 register. No M1 implementation promotion until G0 is verified.
