# Acceptance Criteria

**Authority:** This document defines detailed product and milestone validation checks. [Roadmap](ROADMAP.md) owns the M0-M24 gate boundaries, [Execution Graph](EXECUTION_GRAPH.md) owns prerequisite relationships, and [Status](STATUS.md) records observed outcomes. None of these planning documents by itself proves a check has passed.

## Status convention

- Planned: requirements documented, no implementation evidence.
- In progress: changes exist, verification incomplete.
- Implemented: functionality exists but may lack complete verification.
- Verified: documented acceptance checks passed against a specified revision.
- Accepted: verified and explicitly approved.
- Blocked: a prerequisite or mandatory check is failing.
- Acceptance pending: verified evidence is available, explicit acceptance not recorded.

No item is accepted by implication. See STATUS.md for the current ledger.

## Final-release-only accessibility gate

**All dedicated accessibility testing is scheduled exclusively for M24-04, the final work package before G24.** M0-M23 require functional and security verification appropriate to their features, but have no accessibility audit or accessibility acceptance tests. This includes automated contrast, keyboard/focus, accessible names, screen-reader behavior, zoom, responsive accessibility checks, and WCAG conformance tests. M23 handles interface polish without an accessibility gate. M24-04 executes the complete accessibility test suite against the finished interface, records failures and fixes, and reruns the suite before final release acceptance. The requirement is deferred, not cancelled.

## Historical-volume checks at quarterly milestones

**G0 requires fresh, disposable database migration/seed and runtime recovery tests, not compatibility testing against an existing local Docker volume.** Quarter-mile checkpoints are M6/G6 (Q1), M12/G12 (Q2), M18/G18 (Q3) and M24-03/G24 (Q4). Each runs isolated snapshot compatibility only when a representative historical volume or backup exists, otherwise records NOT APPLICABLE. No test may alter the only copy of a preserved dataset. M19 retains the full backup, historical migration and restore rehearsal.

Before a storage-format or breaking database change is deployed against existing data, test that particular upgrade on a disposable copy regardless of checkpoint timing. That rollout safeguard does not block ordinary M0 CI.

## Recorded M0 verification state

A subsequent isolated cold-database test reproduced a tracked-row recovery failure on the historical M0 code, invalidating the earlier complete cold-state verification claim. The corrective changes are tracked in [hardening PR #3](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/3). The separate [M0 implementation register](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m0-engineering-baseline/docs/M0_VERIFICATION.md) reports the G0 engineering baseline checks executed on 2026-10-30 against code revision `4fb34eb`, with 1 unit, 10 contract, 5 PostgreSQL integration and 6 frontend tests passed, plus local Compose, browser and database outage/recovery evidence.

**G0 verified by [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) at `fec7429415fede121db8a4a34f0df72cd4fac609`, acceptance pending.** This is not automatic acceptance and does not close any other G1-G24 gate. The historical PostgreSQL upstream `gosu` scan reported one critical and 25 high findings. The hardened PostgreSQL, API and web images passed high/critical scans against immutable running image IDs in run #2. Changes to these images require rescanning. Hosted deployment, additional browser coverage and full database backup/restore are not M0 results.

All future work must honor [Local Storage Policy](LOCAL_STORAGE_POLICY.md): controllable persistent project storage on E:, allowing only unavoidable short-lived Windows temporary use on C:.

## Granular milestone verification gates (v2)

The detailed product release checks below remain in force. The [Roadmap](ROADMAP.md) divides their implementation into 25 narrower gates. A milestone gate is verified only after the corresponding graph work packages have verifiable evidence for the checks below, including negative/failure conditions.

| Gate | Milestone | Required executed evidence |
| --- | --- | --- |
| **G0** | M0 Engineering baseline verification | Clean .NET and web builds, unit/contract/integration tests, PostgreSQL repeatable migration, Compose/browser status path, readiness outage and hygiene evidence |
| **G1** | M1 Authentication and session lifecycle | Login/logout, account-disable, expiry and invalid session negative tests |
| **G2** | M2 Organizations and role isolation | Two-organization membership scope, role denials, disabled membership and selected-organization tests |
| **G3** | M3 Ports and vessels reference data | Port/vessel identifier, validation, scoped CRUD and browser lookup tests |
| **G4** | M4 Voyages and containers reference data | Voyage scheduling and container identity/uniqueness tests with resolved D-003 |
| **G5** | M5 Shipment lifecycle and concurrency | Shipment lifecycle, archival, stale-write, transaction and cross-tenant denial tests |
| **G6** | M6 Container and voyage assignments | Temporal assignment/overlap prevention and end-to-end linked shipment flow; quarterly snapshot check or NOT APPLICABLE |
| **G7** | M7 Tracking contracts and source authentication | Pinned source schema, source authorization, oversized/forged/unsupported payload tests |
| **G8** | M8 Durable event inbox and immutable history | Durable receipt, collision, uniqueness and restart-preserved immutable history tests |
| **G9** | M9 Idempotent worker and reconciliation | Worker concurrency, bounded retry, dead-letter and crash-window effect-once tests |
| **G10** | M10 Event timeline and projection rebuild | Timeline ordering, late event, corrections and projection rebuild equality checks |
| **G11** | M11 Delay rules and shipment exceptions | Delay rule boundaries, exceptions lifecycle and repeat delivery behavior |
| **G12** | M12 Shipment search and authorized export | Scoped search/filter and safe bounded CSV export with foreign-user denial tests; quarterly snapshot check or NOT APPLICABLE |
| **G13** | M13 Operational KPI definitions and dashboards | Exact KPI denominator/window reconciliations and dashboard drill-through checks |
| **G14** | M14 Route mapping and geographic context | Map license/fallback, simulated labeling, functional interactions and organization scoping |
| **G15** | M15 Scoped in-app notifications | Notification recipient matrix, deduplication and correctly scoped navigation tests |
| **G16** | M16 Audit logging and access hardening | Audit immutability, redaction, abuse throttling and permissions regression |
| **G17** | M17 Observability and service health | Health/trace/log signal propagation with controlled outage detection and redaction |
| **G18** | M18 IT incident lifecycle and remediation | Incident lifecycle permissions, investigation evidence, remediation and verified closure; quarterly snapshot check or NOT APPLICABLE |
| **G19** | M19 Database recovery and migration rehearsal | Fresh/historical migrations, isolated backup restoration, row/integrity verification and recovery target evidence |
| **G20** | M20 Security assurance and dependency review | Security test matrix, advisory/secret findings and zero unresolved release-blocker vulnerabilities |
| **G21** | M21 Performance and capacity benchmarks | Versioned load fixture, measured p50/p95/p99, p95 target and recorded optimization limits |
| **G22** | M22 Deployment portability and cost controls | Full local deployment reproducibility, cost constraints, teardown and optional public-demo decision with no hosting mandate |
| **G23** | M23 Interface polish and workflow consistency | Visual polish, consistent workflows and functional error/recovery states; accessibility tests remain deferred |
| **G24** | M24 Final release validation and acceptance | D01-D06, final M24-04 accessibility audit with remediation/re-tests, complete evidence and release decision; quarterly snapshot check or NOT APPLICABLE |

Additional gate rules:

- No release gate can pass with a mandatory test silently skipped or an implementation-only claim.
- All tasks listed as prerequisites in [execution-graph.json](execution-graph.json) must be verified; a passing subset is insufficient.
- Every recorded check must identify source revision, test command, environment, data fixture, actual result, and unresolved failures or waivers.
- M14/M15 map and notification gates remain in the planned release. A deferral requires an explicit product and acceptance change before the gate can be closed.
- G22 requires a complete **local** deployment and spending safety assessment. Public hosting is not a release requirement.
- G24 is a release readiness checkpoint, not automatic acceptance; after verification the state remains **acceptance pending** until explicit approval.
- The former v1 G1-G6 definitions were superseded by the v2 graph. Historical evidence must be mapped by demonstrated behavior, not reused by ID alone.

## Product acceptance gates

### A. Shipment workflows
- [ ] An authorized analyst creates a shipment, assigns a container and voyage, edits with concurrency protection, and finds it through search.
- [ ] Invalid data is rejected with stable errors and no partial changes.
- [ ] Unauthorized users cannot retrieve or mutate the shipment across any route or export.

### B. Tracking integrity
- [ ] Inbound event is validated, durably recorded, processed, and visible in timeline.
- [ ] Duplicate, out-of-order, missing-reference, and correction scenarios preserve event integrity.
- [ ] Projection rebuild produces the expected deterministic current state.
- [ ] Failed processing is diagnosable and recoverable without silent event loss.

### C. Analytics
- [ ] Dashboard KPIs reconcile to their documented underlying records.
- [ ] Delay and at-risk states follow testable rule definitions.
- [ ] Filters, downloads, maps, and detail navigation honor organization permissions.
- [ ] Simulated positions and ETA calculations are labeled as such.

### D. Security
- [ ] Role/function/object authorization matrix passes with at least two organizations.
- [ ] Tokens/secrets are excluded from version control, logs, and exports.
- [ ] Session expiry, account disablement, suspicious ingress, and rate limiting are tested.
- [ ] Security findings are triaged; release blockers are resolved.

### E. IT operations
- [ ] Liveness/readiness distinguish process and dependency health.
- [ ] Monitoring detects a deliberate worker or database failure.
- [ ] Incident records contain detection, impact, investigation, mitigation, and verification.
- [ ] Audit records and metrics can be correlated without exposing sensitive content.

### F. Delivery and recoverability
- [ ] Local stack starts from a clean documented environment.
- [ ] Fresh and historical migrations pass.
- [ ] Database backup restoration succeeds on isolated test target.
- [ ] Automated unit, integration, contract, and browser suites pass.
- [ ] Performance data includes environment, dataset, command, and summary.
- [ ] Hosted deployment uses an explicitly constrained budget and shutdown plan.
- [ ] README, STATUS, API docs, diagrams, and operational runbooks describe actual behavior.

## Required evidence bundle

1. Exact repository revision and dependency lock state.
2. Environment information and deterministic data seed.
3. Commands and pass/fail results for all required test layers.
4. Security isolation matrix and relevant remediation records.
5. Demonstrated incident and recovery logs/screenshots with private data redacted.
6. Known limitations and deferred work.
7. Final status: acceptance pending or accepted, with date and scope.

## Release refusal conditions

A release must not be described as verified if core data can cross organization boundaries, accepted events disappear silently, backups have never been restored, essential tests fail, or the documentation misstates current delivery status.
