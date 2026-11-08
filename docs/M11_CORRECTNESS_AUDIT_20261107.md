# M11 Implementation Correctness Audit and Remediation Register

**Historical review scope (post-acceptance notice):** This is a point-in-time M11 source audit. Findings, source SHAs and unexecuted-test statements below refer to the audit date and must not be treated as the current G11 gate state. M11/G11 was subsequently verified, formally accepted and merged through [PR #33](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/33). See the [G11 evidence matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [formal gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md). The mandatory M9 hold remains unchanged.
**Audit date:** 2026-11-07
**Branch:** `feat/m11-delay-exception-lifecycle`
**Accepted integration baseline:** protected `main` `6ead1d076946dd96b53401265a31efc35c4a666c`
**Initial source reviewed:** `148457be7122d60c1470d943bcb31de3a9c2c3b2`
**Source-remediation checkpoint:** `6d8e663b7b8bddab8533a92b3a6a03ed27c83f55`
**Disposition:** Source defects remediated where listed below. Runtime correctness **NOT VERIFIED**. G11 policy and acceptance pending. This is not gate evidence, approval to merge, or deployment authorization.

## Scope and methodology

Compared the candidate against accepted M5 shipment lifecycle, M9 processing restrictions, M10 indexed projection migration and store, M16 audit constraints, restricted-role provisioning, authenticated organization scope, React shipment workspaces, EF migration discoverability, unit and integration source, browser fixtures and verification scripts. Findings are from source and contract review. No .NET compilation, SQL migration, PostgreSQL login test, Docker/Compose execution, browser execution, mutation campaign or vulnerability scan was performed in this environment. A test that exists in source has not thereby passed.

## Corrected source findings

| ID | Severity | Finding and consequence | Source remediation | Status |
| --- | --- | --- | --- | --- |
| F11-001 | Critical | M11 referenced nonexistent M10 `shipment_tracking_input_versions.revision`, breaking evaluation, due scans and signed disposition source checks. | Corrected all references to accepted `input_revision`; added accepted-schema assertions to the M11 contract script. | Source corrected, runtime unverified |
| F11-002 | High | A terminal manual `Arrived` or `Completed` workflow without a separate Arrived history timestamp could be labeled schedule-overdue; terminal workflow could retain departure risk. | Terminal status now suppresses overdue and departure risk without fabricating a recorded arrival time. Added UTC policy tests. | Source corrected, runtime unverified |
| F11-003 | High | Automatic clearance resolved all occurrences, so the Manager `Resolve` action had no reachable normal transition. | Unacknowledged active conditions auto-resolve on clearance. Acknowledged, eligible cleared conditions remain inactive until a Manager reviews and resolves. Cancellation/archive auto-resolves to avoid stranded archived exceptions. Added lifecycle tests. | Source corrected, policy approval pending |
| F11-004 | High | Replay-key uniqueness was per actor and exception, allowing the same organization/key to gain a second meaning elsewhere. | One unique key per organization, with actor, session, exception, action, reason and expected version bound by the request digest. Concurrent unique collision maps to HTTP 409. Added collision test. | Source corrected, runtime unverified |
| F11-005 | High | Every signed evaluation permanently inserted a proof nonce without bounded pruning. | Added expiry index and bounded deletion of expired ephemeral nonces inside privileged signed operations, retaining in-window replay protection and denying direct API-role nonce access. | Source corrected, retention/load unverified |
| F11-006 | High | Sparse due work could cause repeated broad scans of shipments, and policy upgrades were invisible to due checkpoints. | Bounded indexed candidate scan with cursor advancement over not-due rows; per-rule policy version persisted and compared. | Source corrected, performance unverified |
| F11-007 | High | Snapshot read acquired a shared shipment lock then a later signed operation upgraded it, increasing competing-evaluator deadlock risk and diverging from organization-first locking. | Organization lock precedes shipment `FOR UPDATE`; signed operation follows the same order. | Source corrected, concurrency unverified |
| F11-008 | Medium | Exception source evidence omitted explicit rule and M10 input checkpoint context. | Bounded structured evidence now includes shipment/input revision, correction generation, rule policy and M10 projection digest where relevant, exposed through scoped detail. | Improved; conflict-specific container provenance remains open |
| F11-009 | Medium | Detail silently truncated history at 500 entries. | Fetches 501, exposes `historyTruncated` and warns when showing only the first 500. | Source corrected; history pagination not implemented |
| F11-010 | Low | One shared frontend reason input could apply an unrelated exception's draft. | Scoped reason drafts by exception ID. | Source corrected |
| F11-011 | Medium | Analyst responses included free-form Manager reasons and actor IDs. | Analyst detail redacts both; Manager and Auditor retain evidence under existing authorization. | Source corrected, role matrix unverified |
| F11-012 | Medium | Focused local runner could exit on failure before sealing available raw evidence and permitted ambiguous non-E execution paths. | E:-backed repository/workspace preflight, EXIT trap and failure-inclusive evidence manifest. | Source corrected, script execution unverified |
| F11-013 | Medium | Current-facing execution graph and planning checker still treated G10 as open and G6 as pending. | Reconciled generated graph starting point, generator and planning assertions to accepted G0-G10/G16. | Source corrected, planning script not executed |

No existing accepted migrations 1 through 13, M9 worker source, G10 gate closure or prior raw evidence have been rewritten.

## Residual correctness and security risks

1. **Critical: executable correctness unproven.** New migration 14 and `SECURITY DEFINER` SQL functions must be applied on clean and *populated* accepted-M10/G16 database copies. Execute all code with the restricted `harborops_api` login. SQL syntax, grants, transaction behavior, migration downgrade safeguards and genuine audit failure atomicity have not been demonstrated.
2. **High: M11-P01 through M11-P12 unapproved.** The 60-minute UTC grace/tolerance, rule identities, confidence labels, conditions, closure choices and read redaction are provisional candidate semantics. No local-policy selection is implied by implementation.
3. **High: G11 verification incomplete.** All G11-S01 to G11-S25 dispositions remain BLOCKED or NOT EXECUTED, including independent evidence reconciliation and explicit acceptance.
4. **Medium: conflict provenance incomplete.** M10 input revision, correction generation and digest are retained. The exact conflicting container(s), individual accepted source-event IDs and correction-aware trace required by proposed M11-P03 have not been demonstrated end to end. Do not represent an aggregate projection digest as physical arrival or a conflict-specific event identifier.
5. **Medium: schema-wide API SQL reader model.** Accepted role provisioning grants broad SELECT on business tables with HTTP-side tenant authorization. M11 retains this inherited trust model for exception read tables; no new row-level security or signed SQL read gateway is claimed. G11-S12 requires separate scrutiny of the direct restricted-role threat model and whether additional hardening is required.
6. **Medium: bounded UI history is not full-history retrieval.** Immutable history is retained, but the detail route currently reports truncation rather than providing a paged history export. Establish the approved history-access requirement before G11 acceptance.
7. **Medium: optional evaluator operating behavior unverified.** Candidate-page traversal, time-only due transitions, restart, multiple API replicas, nonce expiry pruning and sustained volume/backpressure require real measured tests.

These residuals prevent a recommendation that G11 is ready for acceptance.

## Source and verification entrypoints

- Domain policy: `src/HarborOps.Domain/Exceptions/ShipmentExceptionPolicy.cs`
- Persistence/migration: `src/HarborOps.Infrastructure/Exceptions/ShipmentExceptionStore.cs`, `src/HarborOps.Infrastructure/Persistence/Migrations/20261107180000_ShipmentExceptions.cs`
- Proof and grants: `src/HarborOps.Infrastructure/Exceptions/M11ProofSigner.cs`, `src/HarborOps.Api/Audit/AuditRoleProvisioner.cs`
- HTTP and optional evaluator: `src/HarborOps.Api/Exceptions/`
- Shipment workspace: `web/src/shipments/ShipmentExceptionPanel.tsx`
- Source tests: `tests/HarborOps.UnitTests/ShipmentExceptionPolicyTests.cs`, `tests/HarborOps.IntegrationTests/M11ExceptionRuntimeTests.cs`, `tests/browser/specs/shipment-exceptions.spec.mjs`
- Checks: `scripts/check-m11-contract.mjs`, `scripts/verify-g11-local.sh`
- Gate matrix: [G11-S01 through G11-S25](M11_VERIFICATION_PLAN.md)

Run the focused verifier only against a disposable local database, from an E:-backed checkout with E:-backed evidence/cache paths. Preserve failed attempts and exact SHA. Follow with complete change-impact regression, migration-retention, restricted-role, cursor, audit-failure, restart, browser (Chromium, Firefox, WebKit), mutation, vulnerability and independent raw-evidence checks. The focused script cannot by itself close G11.

## Acceptance and release boundary

M11 is a draft feature-branch implementation and may not be merged based on this source review. `main` remains accepted through G10 and G16 only. The M9 sustained transport-processing hold remains active: ARRI/DEPA remain parked, the 10,000 unresolved-work cap remains binding, and M10 ETA stays null. No GitHub Actions were triggered, and no merge, release, deployment or public hosting is authorized.

**Decision:** G11 acceptance pending.
