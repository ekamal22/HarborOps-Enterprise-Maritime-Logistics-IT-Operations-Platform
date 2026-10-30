# Implementation Plan

## Roadmap intent

Deliver a functional, secure maritime logistics prototype through incremental, independently testable milestones. The twelve-week projection assumes sustained development time and can be revised when implementation evidence changes.

## Dependencies

    M0 Baseline
       -> M1 Core Operations
          -> M2 Tracking Events
             -> M3 Analytics and UX
                -> M4 Security and Service Operations
                   -> M5 Cloud and Reliability
                      -> M6 Demonstration Release

Security begins in M1 and continues throughout; M4 is a hardening and service-desk milestone, not the first security milestone.

## M0: Engineering baseline (Week 1)

### Deliverables
- Repository layout, .NET solution, React app scaffold, Docker Compose.
- Developer setup instructions, format/lint/test commands.
- PostgreSQL schema blueprint and migration baseline.
- Health check endpoints and structured logging baseline.
- Deterministic seed design and pinned major dependencies.
- Architecture and security decision closure for first implementation.

### Exit gate
- Clean setup reproducible from a documented local environment.
- Frontend and backend start; minimal API/database health checks pass.
- CI or equivalent repeatable local verification passes.
- No credentials committed.

## M1: Identity and Shipment Operations (Weeks 1–2)

### Deliverables
- Organizations, membership roles, authentication, authorization policies.
- Port, vessel, voyage, container, and shipment domain model.
- Shipment creation/edit/search/detail and container assignment.
- Validations, optimistic concurrency for edits, migrations.
- Two-organization test data and isolation tests.

### Exit gate
- Complete shipment lifecycle through API and browser.
- Cross-organization and wrong-role operations consistently denied.
- Clean migration and integration suites pass.

## M2: Event Tracking (Weeks 3–4)

### Deliverables
- Versioned intake schema and simulator.
- Durable event inbox, immutable event history, idempotency key constraints.
- Background worker, retries, quarantine, reconciliation records.
- Timeline projection, ETA/status updates, late-event processing.
- Adapter mapping specification pinned to one DCSA Track & Trace release.

### Exit gate
- Deterministic end-to-end tracking demonstration.
- Duplicate, late, unknown, corrected, and forged-event cases pass.
- Worker restart does not duplicate or lose accepted effects.

## M3: Operations Analytics and Interface (Weeks 5–6)

### Deliverables
- Shipment list, filters, route context, timeline, status detail.
- KPI calculations with documented denominators and cutoff times.
- Delay exception queue, dashboard drill-through, authorized CSV export.
- Responsive and keyboard-accessible UI patterns.
- Map implementation with compliant tile usage or local fallback.

### Exit gate
- Analyst can explain a dashboard KPI through linked underlying records.
- At-risk/delayed labels agree with accepted events and rule fixtures.
- Usability and end-to-end tests pass; MVP demonstration runs locally.

## M4: Security Hardening and IT Service Desk (Weeks 7–8)

### Deliverables
- Comprehensive permission tests, sessions, audit records, abuse limits.
- Service incident data model, state machine, root-cause notes.
- Health endpoints, logs, telemetry, metrics panels, basic alerting.
- Documented triage, incident recovery, and corrective action workflow.

### Exit gate
- Security matrix and threat tests pass.
- Demonstrated diagnostic journey from injected failure to resolved incident.
- No sensitive information in telemetry or incident exports.

## M5: Cloud Demonstration and Verification (Weeks 9–10)

### Deliverables
- Optional Azure hosted demonstration with explicit budget controls.
- Repeatable infrastructure configuration and shutdown procedure.
- Backup/restore rehearsal and worker/database failure exercises.
- Load/latency benchmark and dependency/security review.
- Fresh and historical migration rehearsal.

### Exit gate
- Full system operates in a declared deployment profile or documented local-equivalent fallback.
- Recovery and cost guardrails verified.
- No unresolved blocker-class defects or unsupported quality claims.

## M6: Release Polish (Weeks 11–12)

### Deliverables
- Cohesive visual language, error/empty/loading states.
- Final deterministic fixtures and complete demo guide.
- Updated architecture, API, security, operations, and contributor docs.
- Release notes, known limitations, evidence register.

### Exit gate
- Acceptance Criteria document passes without undocumented exceptions.
- All visible functionality has verified runtime evidence.
- No stale documentation or mismatched status claims.

## Task completion rule

Every work package must specify implementation path, tests, migration implications, documentation updates, expected failure behavior, and acceptance evidence. Do not close a task because source code exists; require a verified behavior outcome.

## Scope control

New features are evaluated against security risk, test cost, maintainability, data licensing, hosting cost, and impact on the roadmap. Explicitly defer noncritical complexity rather than jeopardizing core verification.
