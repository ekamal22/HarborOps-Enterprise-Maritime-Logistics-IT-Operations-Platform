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

No item is accepted by implication. See [Status](STATUS.md) for the current ledger. Independently numbered gates may be verified and accepted in parallel when their explicit prerequisites are accepted. Milestone numbering alone is not an acceptance dependency. See [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md).


## Independent gate verification and parallel integration

Each gate G0-G24 retains its own full mandatory security, authorization, database, HTTP, browser, failure/recovery and documentation checks. The [Execution Graph](EXECUTION_GRAPH.md) defines which accepted gates a given work package needs. A verified-but-unaccepted gate cannot be relied upon for dependent implementation or final integration. Different milestones may close in a nonnumeric order when their actual dependencies have been satisfied.

Cross-workstream integration must rerun affected previous gates' regression cases and all changed domain/API/migration/authorization contracts against the final integrated SHA. Independent branch PASS records alone do not prove the merged application. G19 recovery and G20 security outcomes require later change-specific regression checks. **G24 requires accepted G0-G23**, a complete current-system verification and final M24-04 accessibility evidence. The historical snapshot checkpoints at G6, G12, G18 and G24 remain mandatory at their named gate, not based on calendar or numeric percent. See [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md).

## Final-release-only accessibility gate

**All dedicated accessibility testing is scheduled exclusively for M24-04, the final work package before G24.** M0-M23 require functional and security verification appropriate to their features, but have no accessibility audit or accessibility acceptance tests. This includes automated contrast, keyboard/focus, accessible names, screen-reader behavior, zoom, responsive accessibility checks, and WCAG conformance tests. M23 handles interface polish without an accessibility gate. M24-04 executes the complete accessibility test suite against the finished interface, records failures and fixes, and reruns the suite before final release acceptance. The requirement is deferred, not cancelled.

## Historical-volume checks at quarterly milestones

**G0 requires fresh, disposable database migration/seed and runtime recovery tests, not compatibility testing against an existing local Docker volume.** Quarter-mile checkpoints are M6/G6 (Q1), M12/G12 (Q2), M18/G18 (Q3) and M24-03/G24 (Q4). Each runs isolated snapshot compatibility only when a representative historical volume or backup exists, otherwise records NOT APPLICABLE. No test may alter the only copy of a preserved dataset. M19 retains the full backup, historical migration and restore rehearsal.

Before a storage-format or breaking database change is deployed against existing data, test that particular upgrade on a disposable copy regardless of checkpoint timing. That rollout safeguard does not block ordinary M0 CI.

## G4 exit criteria (verified and accepted)

G4 was explicitly accepted on 2026-11-01 following independent raw-evidence review and D-003 Option B approval. The [G4 executed verification record](M4_EXECUTED_VERIFICATION.md) documents completion of all 25 scenario requirements: tenant, role, migration, temporal/identity constraints, archival uniqueness, concurrent parent/revocation cases, three browsers, M0-M3 regressions, outage/recovery and security scans. M5 shipment CRUD, M6 assignments, formal accessibility and historical-volume compatibility remain separate milestones.

## G5 exit contract (verified and accepted)

All [G5-S01 through G5-S25](M5_VERIFICATION_PLAN.md) were independently reviewed on the final tested M5 source and explicitly accepted on 2026-11-02, including the separately approved six-rule local contract. See [G5 gate closure](M5_G5_GATE_CLOSURE.md). M6 is eligible to plan because G4 and G5 are accepted.

## G6 exit contract (verified and accepted)

The [G6-S01 through G6-S25 matrix](M6_VERIFICATION_PLAN.md) covers database temporal exclusions, scoped tenant FKs, authorization, concurrency, three browsers, M0-M5 regressions and the Q1 historical snapshot. G6-S02 through G6-S25 were technically verified at the source-locked SHA. The [twelve-rule M6 contract](M6_LOCAL_CONTRACT_DECISION.md) and G6 were explicitly accepted on 2026-11-02, closing G6-S01 after execution, and M6 merged into `main`. The original exit status and limited snapshot representativeness are preserved in [G6 closure](M6_G6_GATE_CLOSURE.md).

## Recorded M0 verification state

A subsequent isolated cold-database test reproduced a tracked-row recovery failure on the historical M0 code, invalidating the earlier complete cold-state verification claim. The corrective changes are tracked in [hardening PR #3](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/3). The separate [M0 implementation register](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m0-engineering-baseline/docs/M0_VERIFICATION.md) reports the G0 engineering baseline checks executed on 2026-10-30 against code revision `4fb34eb`, with 1 unit, 10 contract, 5 PostgreSQL integration and 6 frontend tests passed, plus local Compose, browser and database outage/recovery evidence.

**G0 verified and accepted.** The complete runtime, tests and image hardening passed GitHub Actions run #2 at the recorded M0 source revision. Explicit G0 acceptance was confirmed on 2026-10-31. G0 acceptance does not automatically close other gates. G1, G2 and G3 were independently verified and accepted on 2026-10-31; G4 was independently reviewed and accepted on 2026-11-01. G7 was accepted 2026-11-03 after 25 reconciled passes and independent supplement review; G8-G24 remain open. The historical PostgreSQL upstream `gosu` scan reported one critical and 25 high findings. The hardened PostgreSQL, API and web images passed high/critical scans against immutable running image IDs in run #2. Changes to these images require rescanning. Hosted deployment, additional browser coverage and full database backup/restore are not M0 results.

All future work must honor [Local Storage Policy](LOCAL_STORAGE_POLICY.md): controllable persistent project storage on E:, allowing only unavoidable short-lived Windows temporary use on C:.

## G1 detailed authentication gate (verified and accepted)

The selected D-001 architecture is [documented](ADR_D001_BROWSER_SESSIONS.md), and [M1 design](M1_AUTHENTICATION_DESIGN.md) and [verification plan](M1_VERIFICATION_PLAN.md) are approved. **G1 verification passed and was explicitly accepted on 2026-10-31; PR #4 merged into `main` at `8f9fc0d506a9e1dc1d4f5acab936622df988f413`.** See the [execution record](M1_IMPLEMENTATION_VERIFICATION.md). Before G1 verification, execute positive and negative tests for local provisioning, secure Argon2id verification, unknown/disabled accounts, session idle and absolute expiry, logout, logout-all, forced password-change/disable revocation, cookie replay rejection, protected API 401 without redirects, CSRF and foreign-origin denial, login throttling, database outage 503, persistent encryption keys across API restart, same-origin proxy and absence of credentials in logs.

Run fresh disposable PostgreSQL migration tests plus a disposable **M0-seeded-to-M1** additive migration, not a retained local volume. Execute functional Chromium/Firefox/WebKit browser tests, release build, dependency/image scanning and complete M0 regression checks. G1 initially advanced to `acceptance pending` after passing executed verification. Explicit acceptance was subsequently recorded on 2026-10-31; design approval alone never constituted G1 verification. Full accessibility testing remains exclusively M24-04.

## G2 completed organization authorization verification (accepted and merged)

The detailed [M2 architecture](M2_ARCHITECTURE_DESIGN.md), [implementation sequence](M2_IMPLEMENTATION_PLAN.md) and [G2 verification specification](M2_VERIFICATION_PLAN.md) require two-organization real-database fixtures, valid foreign organization and membership identifiers, a five-role permission matrix, explicit server-session organization selection, denied privilege escalation, suspension/revocation and last-active-Administrator concurrency tests. The verification must also preserve accepted M0 and M1 behavior.

M2 source and the full G2 regression suite are integrated into `main` via [PR #5](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/5) at `1df638d5300191f6c2c6c9d201fd5f4c4ab9e562`. All 25 G2-S01 through G2-S25 scenarios passed in the complete local run on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1` ([executed verification](M2_EXECUTED_VERIFICATION.md)), using disposable resources on E:. G2 was explicitly accepted on 2026-10-31. G3 has subsequently passed and been explicitly accepted on 2026-10-31.

## Granular milestone verification gates (v2)

The detailed product release checks below remain in force. The [Roadmap](ROADMAP.md) divides their implementation into 25 narrower gates. A milestone gate is verified only after the corresponding graph work packages have verifiable evidence for the checks below, including negative/failure conditions.

| Gate | Milestone | Required executed evidence |
| --- | --- | --- |
| **G0** | M0 Engineering baseline verification | Clean .NET and web builds, unit/contract/integration tests, PostgreSQL repeatable migration, Compose/browser status path, readiness outage and hygiene evidence |
| **G1** | M1 Authentication and session lifecycle | FR-001: real PostgreSQL Argon2id credentials, cookies, CSRF, login/logout/logout-all, expiry, account disablement, immediate revocation, three-browser functional flow, clean migration, restart/outage, rate limits and redacted security evidence. [Full G1 plan](M1_VERIFICATION_PLAN.md). |
| **G2** | M2 Organizations and role isolation | Two-organization membership scope, role denials, disabled membership, session-scoped organization selection, foreign-ID denial, last-admin concurrency and M0/M1 regressions; [G2 local plan](M2_VERIFICATION_PLAN.md) |
| **G3** | M3 Ports and vessels reference data | Port/vessel identifiers and uniqueness, tenant/role-scoped CRUD, cross-tenant and race denials, migration preservation and three-browser lookup, plus full M0-M2 regression; [G3 local plan](M3_VERIFICATION_PLAN.md). Executed and passed locally on `b72025d35d62d0a8a45b466986a0b3bd89eebadb` ([record](M3_EXECUTED_VERIFICATION.md)); G3 explicitly accepted on 2026-10-31 and integrated into `main`. |
| **G4** | M4 Voyages and containers reference data | Execute all [G4-S01 through G4-S25](M4_VERIFICATION_PLAN.md): approved D-003, time/identity constraints, live tenant/role/race matrix, M3 upgrade preservation, three browsers, M0-M3 regressions, E:-only evidence, explicit acceptance |
| **G5** | M5 Shipment lifecycle and concurrency | [G5-S01 to G5-S25](M5_VERIFICATION_PLAN.md): status and identity, tenant FKs, archived references, history atomicity, concurrent writes, two tenants/five roles, three browsers, M0-M4 regressions, recovery and raw evidence review |
| **G6** | M6 Container and voyage assignments | All [G6-S01 to G6-S25](M6_VERIFICATION_PLAN.md): UTC half-open intervals, database overlap exclusions, scoped container/voyage associations, route and parent guards, race/role matrix, end-to-end three-browser flow, M0-M5 regressions and the Q1 snapshot check or documented NOT APPLICABLE; explicit acceptance |
| **G7** | M7 Tracking contracts and source authentication | Execute [G7-S01 to G7-S25](M7_VERIFICATION_PLAN.md): accepted D-002 source mapping, tenant-bound source credentials and revocation, strict payload/transport/Host security, non-durable 503 acknowledgment barrier, clean M6-to-M7 migration, accepted regressions and E:-backed evidence; explicitly accepted 2026-11-03 (see [G7 closure](M7_G7_GATE_CLOSURE.md)) |
| **G8** | M8 Durable event inbox and immutable history | Accepted implementation (23 technical scenarios PASS in the [executed verification record](M8_EXECUTED_VERIFICATION.md)), [G8-S01 through G8-S25](M8_VERIFICATION_PLAN.md): D-006 acceptance, source-locked atomic receipt+event+Pending commit, unique scoped replay, conflict 409, post-commit-only 202, locked final source reauthorization, restart/commit uncertainty and immutable SQL proof, source-locked E:-backed regressions and independent review. **G8 accepted 2026-11-04** following the separately documented D-006 Option A decision and independent sealed-evidence review. Original runner results remain preserved; see [gate closure](M8_G8_GATE_CLOSURE.md). |
| **G9** | M9 Idempotent worker and reconciliation | **Accepted 2026-11-05 and merged through [PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18) at `4684352`.** All six [M9-01 choices](M9_LOCAL_CONTRACT_DECISION.md) approved with mandatory transport-backlog condition. Technical evidence: 809/809 tests, 14/14 mutation families; supplemental browser evidence: 174/174 tests. [G9-S25 evidence review](M9_G9_INDEPENDENT_EVIDENCE_REVIEW.md) was accepted with disclosed limitations. Historical raw statuses remain unchanged, reconciled by the later [G9 closure](M9_G9_GATE_CLOSURE.md). No production deployment. |
| **G10** | M10 Event timeline and projection rebuild | Timeline ordering, late event, corrections and projection rebuild equality checks |
| **G11** | M11 Delay rules and shipment exceptions | Delay rule boundaries, exceptions lifecycle and repeat delivery behavior |
| **G12** | M12 Shipment search and authorized export | Scoped search/filter and safe bounded CSV export with foreign-user denial tests; quarterly snapshot check or NOT APPLICABLE |
| **G13** | M13 Operational KPI definitions and dashboards | Exact KPI denominator/window reconciliations and dashboard drill-through checks |
| **G14** | M14 Route mapping and geographic context | Map license/fallback, simulated labeling, functional interactions and organization scoping |
| **G15** | M15 Scoped in-app notifications | Notification recipient matrix, deduplication and correctly scoped navigation tests |
| **G16** | M16 Audit logging and access hardening | **Accepted 2026-11-06 and merged into `main` via PR #25 (`47ba55e2`).** Standalone evidence review checked 162/162 files and the S07/S08/S28 supplement was later reviewed as 6/6 consistent with a provenance caveat. Combined source `79318c6` passed 228 unit, 39 contract, 286 integration twice, 61 browser tests per engine after a same-SHA supplement, 18/18 targeted mutants, 74/74 HTTP, 14/14 outage and 20/20 stack checks; the accepted-M9 populated upgrade passed 12/12. The post-acceptance F001 extension at `8ba7727` passed 17/17 focused plus 286/286 full integration and an effective regression mutation. [G16 gate closure](M16_G16_GATE_CLOSURE.md) preserves the original failures and limitations. Acceptance does not authorize release or sustained transport processing. |
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
