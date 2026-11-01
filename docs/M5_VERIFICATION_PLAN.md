# G5 Shipment Lifecycle: Verification Plan

**Milestone:** M5, shipment lifecycle and concurrency
**Status:** Proposed mandatory verification contract; no M5 tests have executed
**Entry:** M5-01 through M5-04 implemented, current branch SHA fixed, disposable E:-backed verification environment
**Authorization:** [M5 architecture](M5_ARCHITECTURE_DESIGN.md), [implementation plan](M5_IMPLEMENTATION_PLAN.md), [acceptance criteria](ACCEPTANCE_CRITERIA.md), [execution graph](execution-graph.json).
**Decision:** G5 remains OPEN and acceptance pending until all executable scenarios pass, raw evidence is independently reviewed and explicit acceptance is received.

## 1. Blocking rules

All cases G5-S01 to G5-S25 require direct observed evidence on one final implementation SHA, not merely authored tests. Preserve failures, retries, skipped cases, timestamps, exact databases, image IDs, scripts and per-engine results. Repeat the complete integration suite on the **same disposable database**, with a separately initialized fresh migration case and a seeded accepted M4 upgrade/rollback/reapply case.

M5 is independent of M4 in its **formal graph prerequisite** but is tested against accepted current `main`, which includes M4. No M5 verification or design may modify accepted M0-M4 migration history or broaden the existing maritime permissions. An independently developed M6 branch must get new combined regression before integration. Neither M6 assignment behavior nor event-derived shipment status is a G5 acceptance claim.

All controllable verification state, browser caches, Postgres data, logs and evidence must be on E:. C: may contain only unavoidable attributable short-lived temporary activity, cleaned safely and specifically after E: evidence preservation. No GitHub Actions are authorized.

## 2. G5 scenario matrix

| Case | Scope | Required direct observations |
| --- | --- | --- |
| **G5-S01** | Approved M5-local contract | Confirm the approved status, reference, port, UTC, role and archive rules match code/schema, without inventing a new global D-number |
| **G5-S02** | State machine | Exhaustive Draft, Booked, InTransit, Arrived, Completed, Cancelled transition matrix; reject terminal rollback and illegal cancellation; allow only stated transitions |
| **G5-S03** | Identity and dates | Normalize canonical local/external references, reject Unicode/control/oversized input, enforce explicit UTC/null windows and planned start before finish |
| **G5-S04** | Fresh migration | Apply accepted M0-M5 migrations in order on empty isolated PostgreSQL; prove no pending EF model changes, duplicate migration IDs or missing index/constraint |
| **G5-S05** | Historical M4 upgrade | Preserve seeded M0-M4 users, sessions, selected organization, ports, voyages, containers and migration metadata during M4-to-M5 upgrade, disposable rollback and reapply |
| **G5-S06** | SQL invariants | Reject direct SQL foreign-tenant port references, invalid enums/window/version, mutable identity, forbidden transition, DELETE and history tampering, with positive controls for each |
| **G5-S07** | Fixtures | Two organizations with five role types, matching local codes across tenants, distinct external IDs, seeded transitions, archived rows, and repeat setup without extra rows/version bumps |
| **G5-S08** | Authentication | Anonymous, revoked, disabled, expired idle, expired absolute, changed security-version and missing-session responses across all six routes |
| **G5-S09** | Selection | No selected organization, selection switched mid-request, deleted/suspended org, revoked membership and stale-session state all fail closed |
| **G5-S10** | Tenant access | List, detail, status history, create, PATCH, DELETE and restore reject foreign organization or guessed valid foreign shipment UUID without leaking existence |
| **G5-S11** | Parent port boundaries | Real existing foreign/archived/nonexistent M3 port UUIDs rejected on create; active local parents accepted; distinct origin/destination enforced |
| **G5-S12** | Role matrix | Analyst+Manager read/write; Auditor reads only through typed Shipment view; Administrator/Support deny by default; full method and role matrix, no inherited M4 privilege |
| **G5-S13** | Revocation interleavings | Queued reads/writes revalidate role, session, account security version, organization status and membership removal after lock acquisition; no-revocation control |
| **G5-S14** | HTTP and CSRF/Origin | Wrong content type, malformed/duplicate/oversized JSON, forged Origin, invalid CSRF, unexpected fields, invalid versions, sanitized errors and correlation IDs |
| **G5-S15** | Create and uniqueness | Create Draft with no M6 relationships; per-organization reference and external-reference reservation persists after archive; cross-tenant reuse allowed |
| **G5-S16** | Edit and transition | Mutable display/planned dates and legal status changes, closed status graph, no-op rejection, version increment once, immutable IDs/source/ports/external refs |
| **G5-S17** | Status history | Initial Draft and every real workflow transition append exactly one ordered immutable record in the same transaction; no fabricated entries for display edits or archive |
| **G5-S18** | Archive/restore | Soft archive with version guard, hidden by default, identifier remains reserved, exact-version single-field restore and rejection of edits while archived |
| **G5-S19** | Search/pagination | Tenant predicate before bounded cursor, active default, q/status combinations, valid cursor, no wildcard escapes, large datasets, archived filtering, no page cross-contamination |
| **G5-S20** | Write races | Same-version competing status edits, transition vs archive, restore vs edit, duplicate create and repeated retries commit only one winner and consistent history |
| **G5-S21** | Fault atomicity | Inject failure between shipment change and status-history write, crash/timeout and database failure; assert no partial status/history rows or phantom version |
| **G5-S22** | Chromium workflow | Authentication, tenant selection, Analyst/Manager CRUD, Auditor read-only, lifecycle transitions, archival, conflict/denial/retry, history, and M0-M4 regression |
| **G5-S23** | Firefox and WebKit, UI races | Same browser workflow and no skipped cases; delayed list resolution, tenant switch, logout, selected-tab click, concurrent edits and degraded reference picker |
| **G5-S24** | Accepted M0-M4 regressions | Fresh and repeated .NET suites, historical schema, M1 sessions, M2 org roles, M3 ports/vessels, M4 voyages/containers, and affected existing browser tests |
| **G5-S25** | Operational evidence | Clean Release build, stable DTO/API, dependency scan, PostgreSQL/API/web Trivy HIGH/CRITICAL zero, real DB outage/recovery, stage ledger, hashes, cleanup and exact SHA |

## 3. Required test layers and evidence

| Layer | Minimum concrete work |
| --- | --- |
| Pure unit | Shipment policy transition matrix, statuses, UTC, canonical identities, archive rules and new typed roles |
| SQL integration | Fresh M0-M5, seeded M4 upgrade, M5 rollback/reapply, snapshot drift, direct SQL FKs/checks/triggers, archived uniqueness, status history tampering |
| HTTP integration | Six routes, all five roles, live selected org, invalid auth/session, foreign real IDs, CSRF/Origin, error codes, hostile bodies, pagination and outage |
| Concurrent integration | Observe lock queues and winning order for edit, duplicate POST, archive/restore, M3 parent archive and queued revocation; compare final persisted rows |
| Browser | Chromium, Firefox and WebKit with two tenants, five roles, live revocation, delayed reads, version conflict, archive, restore and state history |
| Operations | Disposable E:-backed Docker stack, rootless Postgres, image scans, dependency review, outage/recovery, teardown and C:/E: storage ledger |
| Security mutation | Remove tenant filter and post-lock revalidation in isolated test mutations; named tests must fail, then restore source and run the complete final suite |

### 3.1 Stage ledger and exit contract

Write `scripts/verify-m5-local.sh` with a stage ledger comparable to G4, but use a **separate** disposable Compose project and `E:\HarborOps-Verification\m5` or another explicitly E:-backed root so accepted G4 evidence cannot be overwritten. Stages should include SHA/clean-worktree preflight, migration-ID check, Postgres image, .NET build/unit/contract, fresh/repeat integration, npm locked installation and web build, documentation checks, stack, accounts/organizations/session, M5 fixtures, browsers, parent-port outage tests, shipment DB outage/recovery, image/dependency scans, cleanup and evidence validation.

Every stage must record one status per exact source SHA. A stage must be counted only after its process exits. Preserve live container logs before teardown, the complete original console stream, exceptions, TRX XML, per-engine Playwright JSON, traces on failures, SQL failures, security mutation proofs and hash manifest. The evidence validator must reject stale source, missing or skipped cases, wrong engine, wrong SHA, failed or absent stages, and a pending M5-local contract decision.

Preserve each prior run under its **own** source SHA, not the incoming revision. Fail closed on existing verification containers and volumes. If interrupted by power loss, do not claim the run survived unless its stage files, processes, final exit status and cleanup were checked.

## 4. Explicit gate closure procedure

1. Finish and commit M5 source without GitHub Actions.
2. Run the entire G5 matrix at one final SHA. If any stage fails, repair the underlying defect, perform another correctness audit and restart full verification at the new SHA.
3. Bundle raw evidence and generate SHA-256 file manifest and archive digest on E:.
4. Independently review evidence including SQL, role/tenant, concurrency, browsers and recovery, not only a summary report.
5. Record a truthful `docs/M5_EXECUTED_VERIFICATION.md` with exact counts and source SHA only **after execution**. No placeholder PASS data is allowed.
6. Request explicit G5 acceptance. Before then mark every unfinished required case `NOT RUN`, `FAILED`, `BLOCKED` or `EVIDENCE PRESENT, REVIEW REQUIRED`, not `ACCEPTED`.
7. Fast-forward merge to `main` only after approval and a clean base/branch comparison. Do not trigger GitHub Actions. Reconcile documentation without claiming an execution on a later documentation-only SHA.

**G5 outcome today:** Design and verification contract only. All 25 cases are NOT RUN and **acceptance pending**.
