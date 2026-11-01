# G4 Voyage and Container Reference Data: Verification Plan

**Milestone gate:** G4, after M4-01 through M4-04  
**Status:** Mandatory verification contract; expanded tests and local runner committed; the initial run at `7d0dd3e60049` passed decision-unit, preflight and PostgreSQL image build, then aborted in the .NET test build on two subsequently corrected xUnit analyzer errors. The second run at `fa46c86fd6e` built with zero warnings and passed 82 unit and 12 contract tests, but fresh and repeated integration failed at EF snapshot validation (86/87 each). The three missing voyage foreign-key indexes have been reconciled across live model, snapshot and SQL migration with an early `schema-index` guard; see [third correctness audit](M4_THIRD_CORRECTNESS_AUDIT.md). The complete G4 suite has not passed. See [M4 correctness audit](M4_CORRECTNESS_AUDIT.md) for the 25-scenario source review and remaining execution risks. G4 OPEN. See [implementation status](M4_IMPLEMENTATION_STATUS.md).  
**Source authority:** [M4 architecture](M4_ARCHITECTURE_DESIGN.md), [M4 implementation](M4_IMPLEMENTATION_PLAN.md), [D-003 proposal](M4_D003_CONTAINER_IDENTIFIER_PROPOSAL.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md), [M3 executed verification](M3_EXECUTED_VERIFICATION.md).  
**Entry:** G3 already accepted; M4-01 must resolve D-003 before relevant identifier code is implemented.

**Observed third G4 execution (2026-11-01):** At `bbb672a5b0ab094c1ae2c21d50ec5faf0101cbee`, decision tests, schema index, preflight, PostgreSQL image, clean .NET Release build, 82 unit, 12 contract and 87 fresh integration tests passed. Repeated integration failed one of 87 tests: `IdenticalContainerIdentifierIsAllowedInDifferentOrganizations` matched retained records from both runs due to a hardcoded identifier. A fresh identifier per invocation now preserves the cross-tenant semantics without stale-record interference. See [fourth correctness audit](M4_FOURTH_CORRECTNESS_AUDIT.md). The corrected source has not been rerun, browser/outage/recovery/scans remain unexecuted, D-003 is pending, G4 OPEN.

## 1. Gate rules

Every one of G4-S01 through G4-S25 is mandatory. The evidence matrix must identify named executed tests, commands, runtime environment, test counts, positive and negative assertions, exact tested SHA and any blockers. A compiled project, authored test or successful partial runner is not a PASS. No scenario may be skipped merely because its state seems unlikely.

All M4 integration and browser tests must run with actual ASP.NET Core 10 HTTP pipeline against disposable PostgreSQL 16, plus pure domain/unit tests where appropriate. Browser simulation may complement but not substitute for real API/session/role/outage tests. All controllable local files, Docker storage, browser profiles, caches and evidence must be on E:. Never use retained primary Docker volumes or real customer data. GitHub Actions are prohibited.

## 2. Scenario matrix

| ID | Area | Required observable positive and negative evidence |
| --- | --- | --- |
| **G4-S01** | D-003 decision and ISO parser | Approved, pinned identifier policy; documented chosen alternatives, standard edition and category scope; correct independent ISO 6346 checksum vectors; invalid checksum, format, length, category and presentation normalization rejected |
| **G4-S02** | Synthetic kind and equipment catalog | Kind-tagged synthetic code normalization, immutable classification, explicit synthetic UI label; wrong kind, ISO-looking synthetic, prohibited controls and invalid type rejected |
| **G4-S03** | Voyage domain invariants | Distinct origin/destination, required vessel/ports, reference code, nullable planned/actual times, planned/actual order, terminal states and forbidden transition matrix, including overnight UTC crossings |
| **G4-S04** | Fresh schema from M0 | Apply M0-M4 migrations in order against empty disposable PostgreSQL; verify tables, indexes, functions, composite FKs and model snapshot; no pending EF model changes |
| **G4-S05** | Historical M3-to-M4 upgrade | Seed real M1/M2/M3 users, sessions, memberships, ports and vessels; upgrade to M4 without changing existing rows; repeat migration, rollback to M3 and reapply; preserve auth selections and accepted M3 constraints |
| **G4-S06** | Direct SQL constraints and triggers | Reject cross-tenant voyage FK, matching route endpoints, invalid time/status combinations, invalid ISO checksum, wrong kind, duplicate code/identifier, direct immutable changes and invalid versions; no partial writes |
| **G4-S07** | Reproducible tenant fixtures | Two isolated organizations with Analyst/Manager/Support/Administrator/Auditor, deterministic voyage/container rows, repeated setup equals first, stable ids/codes/versions, no seed in migration or primary stack |
| **G4-S08** | Anonymous and invalidated sessions | Every GET/POST/PATCH/DELETE denies anonymous, disabled user, expired idle/absolute session, revoked cookie and wrong security version; no leakage |
| **G4-S09** | Missing or changed organization selection | All ten routes fail closed with no selected organization; old-context requests cannot retrieve or mutate after selected-org switch |
| **G4-S10** | Foreign organization substitution | Real foreign organization ID on every relevant method returns sanitized scope error, never foreign counts, existence hints or data |
| **G4-S11** | Foreign voyage UUID substitution | Existing voyage from organization B used as GET/PATCH/DELETE identifier within organization A is not found, without changing B row |
| **G4-S12** | Foreign container UUID substitution | Existing foreign container ID on list/detail/mutation attempts cannot be read, archived, restored, transferred or enumerated |
| **G4-S13** | Complete five-role HTTP matrix | Manager reads/writes and Analyst reads both resources; Analyst writes rejected; Support, Administrator and Auditor forbidden by default for both resources on all verbs; no permission escalation |
| **G4-S14** | Live membership/organization revocation | Admin downgrades/removes Manager, suspends org, account disables/security version changes while operations queue; blocked writes/read requests fail and final DB state remains unchanged |
| **G4-S15** | Hostile request/body and CSRF/Origin | Reject malformed/empty/oversized/deep/duplicate/unknown JSON, wrong types, extra immutable properties, malformed UUID/cursors, wildcard/search abuse, forged CSRF and foreign Origin; standardized error/correlation envelope |
| **G4-S16** | Voyage CRUD and schedule contracts | Create/list/detail/search/edit/archive/restore under own org, planned unknown values preserved, version monotonic, 201/200/204 and sanitized errors; no fabricated status/timestamp |
| **G4-S17** | Container CRUD and type contracts | ISO and synthetic kind create/list/detail/edit/archive/restore; canonical identifier never mutable; type/source-kind immutable and visible; no registry-verified assertion |
| **G4-S18** | Identifier reservation and uniqueness | Per-org unique local codes and ISO/synthetic identifiers across archived/active; parallel duplicates return conflict with exactly one row; same value in independent orgs allowed |
| **G4-S19** | Scoped search/filter/pagination | Limit 1..100/default 25, deterministic UUID cursor, exact status/kind/type filters, voyage port/vessel status filters, literal text wildcard handling, tenant-specific counts and stable multiple pages |
| **G4-S20** | Optimistic concurrency and parent archival races | Competing PATCH/DELETE, stale versions, org role changes, simultaneous M3 parent archival/M4 voyage creation; one valid serialized outcome, no partial write or dangling foreign FK |
| **G4-S21** | Archive vs voyage progress lifecycle | Archived immutable edits denied; restore only via explicit recordStatus and version; invalid voyage status transitions rejected; referenced records remain read-only historical identities |
| **G4-S22** | Three-browser Manager/Analyst workflows | Chromium, Firefox, WebKit: Manager voyages/container create/edit/progress/archive/restore and search/pagination; Analyst reads only; statuses, unknown time labels and synthetic flag correct |
| **G4-S23** | Browser tenant, session and dependency races | Hold old-tenant response while switching tenant, logout during request, real role revocation during refresh, real 503 followed by recovery, stale pagination and mutation busy cleanup; no protected stale rows or edit controls |
| **G4-S24** | Accepted M0-M3 regressions and integration | Complete impacted identity, organization, M3 reference, migration, role/tenant and browser regression suites; no changes to accepted APIs or auth semantics; if M5 schema is concurrently integrated, also run combined M4/M5 migration and shared-context tests |
| **G4-S25** | Security, operations, docs and evidence | Clean .NET 10 Release/web build, zero unresolved HIGH/CRITICAL scan findings within agreed scope, dependency audits, exact running image SHAs, E:-only active Docker backing, clean disposable teardown, full planning/doc link checks, no public/paid dependency, all stages tied to source SHA |

The scenario title is not sufficient evidence. When a scenario covers multiple roles, verbs, reference states or browsers, each required case must be observed or explicitly flagged FAIL/BLOCKED. The status of the complete scenario is the least successful mandatory subcase.

## 3. Required test suites and mechanics

Suggested unit suites:
- `VoyagePolicyTests`: route identity, empty/missing UTC values, status transitions, mixed planned/actual, same-port rejection.
- `ContainerIdentifierTests`: ISO checksum vectors, kind separation, synthetic prefix, normalization, equipment catalog.
- `OrganizationPolicyTests`: all five roles for two new read/write permissions.

Suggested real PostgreSQL and HTTP suites:
- `M4MigrationUpgradeTests`: M3 historical preservation, fresh/repeat/rollback, model drift and direct SQL constraints.
- `VoyageHttpTests`, `ContainerHttpTests`: every route with actual cookie/selected organization, status and version assertions.
- `M4BoundaryMatrixTests`: true foreign parent/record UUIDs, missing selection, role matrix, invalid session and CSRF/Origin.
- `M4ConcurrencyTests`: multiple independent connections, locks, membership downgrade, account security version, concurrent parent archive.
- `M4RecoveryTests`: real DB outage/recovery, sanitized 503 and absence of persisted partial writes.

Suggested browser suites:
- `voyages.spec.mjs`, `containers.spec.mjs`, `m4-races.spec.mjs`, `m4-permissions.spec.mjs`, each on Chromium, Firefox, WebKit; add required regression specs for M0-M3.
- Use separate synthetic sessions and disposable organizations to avoid login throttling interference. Hold/release server requests deterministically for race tests rather than depending on sleeps alone.

All tests use correct assertions for both response and final DB row state. Do not bypass failing tests, transform mandatory tests into SKIP, rely on mocked 403 alone for a real revocation scenario, or count stale TRX results. Avoid test code that assumes a fixed wall-clock timezone.

## 4. Migration security and negative requirements

Require explicit tests that a voyage cannot point to a vessel in A and a port in B even with a legitimate Manager in A. Both origin and destination composite FKs must use the same `organization_id` as the voyage, and reference IDs must be immutable after insertion. Direct SQL bypass tests must make genuine invalid writes on a disposable DB, not only assert that constraint names exist.

Tests for M3 parent archival must create an actual same-org voyage and concurrently archive its active vessel/port. The database may retain a voyage referencing a subsequently archived M3 reference, but an attempted new voyage with an already archived parent must be denied. Confirm both orders yield consistent results.

Container archival never frees its identifier for reuse in the same org. A label edit must not change the physical identifier, kind, type or provenance. The SQL checksum implementation must use independent vectors that do not merely invoke the same C# function.

## 5. HTTP expected results

| Condition | Status | Envelope |
| --- | --- | --- |
| Anonymous or expired session | 401 | Safe authentication code, no HTML redirect |
| Active session, no selected organization | 409 | `organization_selection_required` |
| Revoked membership / suspended organization | 403 | `organization_access_revoked` |
| Wrong role | 403 | `access_denied` |
| True foreign or nonexistent record UUID | 404 | `resource_not_found`, indistinguishable |
| Malformed JSON/UUID/filter/time/identifier | 400 | `invalid_request` |
| Stale expected version, illegal transition, duplicate identity | 409 | `conflict` or documented specific safe code |
| Successful create | 201 | New resource projection and Location |
| Successful update/restore | 200 | Updated projection with increased version |
| Successful soft archive | 204 | Empty body |
| Dependency unavailable | 503 | `dependency_unavailable` with no SQL/secret disclosure |

Every JSON error includes a safe `code`, `message` and `correlationId` using existing API conventions, and no cross-tenant details.

## 6. G4 local evidence pipeline

The implementation phase must author and execute `scripts/verify-m4-local.sh`, reusing the accepted M3 E:-only checks and strengthening evidence provenance, and `scripts/assert-g4-evidence.mjs`. Required captured stages:

1. Source SHA, clean tree, Windows E: worktree and active Docker Desktop WSL disk verified; pre-existing disposable resources refused.
2. Release .NET restore/build/unit/contract and production web build; migration model integrity check.
3. Fresh M0-M4 database and seeded M3-to-M4 historical upgrade, repeat and rollback.
4. M4 domain/API/SQL/role/security/concurrency integration tests, fresh plus repeated at the same final SHA.
5. Deterministic two-tenant fixtures and three-browser runs with original JSON evidence.
6. Actual database interruption and recovery with sanitized 503, preserved log and successful cleanup.
7. Image/dependency high/critical scans, current image identifiers and complete prior accepted M0-M3 regressions.
8. Strict index G4-S01..S25 mapped to actual test IDs, full stage exit statuses, no stale/skipped results and no ambiguous PASS labels.

Record under `E:\HarborOps-Verification\evidence\g4` or equivalent E:-backed explicit directory, with isolated run identity and SHA-256 inventory. Use a distinct disposable Compose project that cannot collide with the primary stack or M3 evidence. Never remove user-created Docker containers/volumes, and do not run GitHub Actions.

## 7. Closure and future change control

The source architecture and plan can be approved without accepting G4. M4 is complete only after every scenario passes on one final integrated revision, independently reviewed evidence is recorded in `docs/M4_EXECUTED_VERIFICATION.md`, D-003 is closed, and G4 receives explicit acceptance. Until then [Status](STATUS.md) must say M4 planned or in progress, not verified/accepted.

M6 cannot use M4 as an accepted prerequisite until G4 acceptance. Any concurrent M5 integration must reconcile migrations and rerun the combined test matrix, not reuse independent per-branch results. M6 historical-volume compatibility checkpoint, M19 full recovery and M24-04 accessibility remain at their originally designated milestones.

**No tests were executed by authoring this verification plan.** The proposed test names, commands and results are design specifications only.
