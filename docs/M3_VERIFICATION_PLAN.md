# G3 Ports and Vessels Reference Data: Verification Plan

**State:** Proposed mandatory G3 test specification; not executed  
**Gate:** G3, following M3-01 through M3-04  
**Prerequisites:** Accepted G2, approved [M3 architecture](M3_ARCHITECTURE_DESIGN.md) and completed [M3 implementation](M3_IMPLEMENTATION_PLAN.md)  
**Acceptance:** All mandatory scenarios pass on one final implementation revision, evidence recorded; then G3 is verified with acceptance pending. Explicit acceptance is separate.  
**Authority:** [Roadmap](ROADMAP.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Requirements](REQUIREMENTS.md), [M2 G2 executed record](M2_EXECUTED_VERIFICATION.md).

## 1. Local execution policy

No GitHub Actions workflow may be dispatched, rerun, triggered through a push or used as the G3 verification environment. Perform all G3 tests on a local E:-backed disposable environment. This plan is a contract for future tests, not evidence of executed results.

Before testing, validate the current branch/SHA and clean worktree; verify E: for `TEMP`, `TMP`, `TMPDIR`, NuGet/npm cache, `DOTNET_CLI_HOME`, Playwright browser path, repository/worktrees, evidence, Docker Desktop/WSL image store and Compose volumes. Do not modify C: or existing primary database/volumes. Use a throwaway PostgreSQL database with the established test-database name guard and a disposable Compose project, separate from the primary `harborops` project. Local .NET 10 SDK containers are acceptable when their data is on E:.

Required evidence stages: Release build, unit, contract, fresh PostgreSQL integration, seeded upgrade/repeated PostgreSQL integration, web unit/build, complete Chromium/Firefox/WebKit browser flow, startup/Compose and database outage/recovery, image revision labels, image vulnerability scan, dependency review, document integrity and full regression for accepted M0/G0, M1/G1 and M2/G2. Do not weaken existing tests or substitute unit tests for DB/HTTP assertions.

## 2. Synthetic fixtures

Provision two active synthetic organizations A and B, two independent authenticated sessions for a user who belongs to both, plus role-specific active users for Analyst, Manager, Support, Administrator and Auditor. Include a revoked member, a suspended member and a suspended organization. Use distinct synthetic identifiers. At least one Manager and Analyst must be a member of both organizations with different permissions or membership states as required by the scenario.

In each tenant create:

- Two active ports with distinct immutable local codes; at least one has a syntactically valid UN/LOCODE and matching country code, one has no external code.
- One archived port; a cross-tenant port whose UUID is known to the test caller; an invalid UN/LOCODE and country-mismatch candidate.
- Two vessels, one with checksum-valid seven-digit IMO and another with a nine-digit MMSI including a leading zero; one name-only vessel, one archived vessel, and one known foreign vessel UUID.
- Duplicate code/external-ID attempts, same-named vessels, malformed characters, long strings and cross-tenant reuse of the same external identifier and local code.
- A database seeded at M0-M2 state before applying the M3 migration, preserving legacy `bootstrap_records`, users, credentials, sessions, organizations and memberships.

All fixtures must be synthetic or clearly user-provided, never labeled officially registered or externally verified. Keep test credentials and issued cookie/token values out of logs and Git. Stable fixture seed/expected counts must be recorded.

## 3. Mandatory G3-S01 through G3-S25 scenario matrix

| Scenario | Required executable verification and key assertions |
| --- | --- |
| G3-S01 Port domain rules | Normalize and validate local code, name, country and optional UN/LOCODE; reject invalid spacing, 0/1 location digits, mismatched country, controls and oversize values; no claims of official port status |
| G3-S02 Vessel domain rules | Canonical seven-digit IMO including valid checksum and optional display prefix; malformed and invalid checksum rejected; nine-digit MMSI preserves leading zeros, rejects nondigits/zero-only; missing optional IDs, duplicate names and flag normalization |
| G3-S03 Reference permission matrix | Unit tests for five roles on both typed operations; Analyst read only, Manager read/write, Support/Administrator/Auditor denied; all previously accepted M2 organization/membership permissions unchanged |
| G3-S04 Fresh additive migration | Brand-new disposable PostgreSQL runs M0-M3 migration/seed in correct order; model snapshot matches entities, all migrations discovered, no pending drift |
| G3-S05 Existing M2 data upgrade | Disposable M0/M1/M2-seeded database migrates to M3; original seed, users, credential/session usability, memberships and selected org survive; repeated migration is safe |
| G3-S06 Database constraints | FK restrictions, required organization, immutable identifier rules, positive version, status/source/check constraints, partial unique external IDs and unique local codes reject invalid direct writes |
| G3-S07 Deterministic fixture ownership | Fixture generated twice from fixed seed yields same bounded records/counts; no live M0 startup seed pollution; tenant A/B independence and labels remain synthetic |
| G3-S08 Anonymous/disabled/revoked caller | Every new endpoint and verb denies anonymous/expired/disabled/revoked sessions with appropriate 401; CSRF presence never substitutes for authentication |
| G3-S09 Missing selected organization | Every list/detail/write method fails with 409 selection-required where applicable; selecting one active organization restores exactly that tenant's access |
| G3-S10 Foreign organization substitution | With A selected, every port/vessel endpoint and verb using valid B organization UUID returns indistinguishable 404, no B counts, names, external identifiers or writes |
| G3-S11 Foreign port UUID | An actual B port UUID under A route cannot be listed, read, patched, archived or restored, and is indistinguishable from a nonexistent UUID |
| G3-S12 Foreign vessel UUID | Same actual foreign-UUID denial for vessel paths, including archive and restore and guessing the same external identifier |
| G3-S13 Real HTTP role matrix | Every verb tested across five roles: Analyst GET only; Manager GET/create/PATCH/archive/restore; Support/Admin/Auditor denied; browser-hidden controls cannot substitute for API authorization |
| G3-S14 Membership/organization changes | Two sessions; removal, role downgrade, user disable, org suspension and revoked selection immediately deny the next request; no protected response uses a stale scope or cached grant |
| G3-S15 CSRF and hostile payloads | Every mutation denies missing/invalid CSRF or foreign Origin; invalid JSON, unknown fields, embedded ownership, path UUID, status, version, body size and query values fail safely without partial writes or SQL in response/log |
| G3-S16 Port create/read/update contract | Real HTTP POST/GET list/detail/PATCH/DELETE and restore, normalization, canonical response shape, server-owned fields, 201/200/204, version increments and stable timestamps |
| G3-S17 Vessel create/read/update contract | Same HTTP lifecycle for vessels, including optional IDs, name-only records, canonical IMO/MMSI and archived record retrieval |
| G3-S18 Identifier uniqueness and collision | Duplicate local codes, same-tenant non-null UN/LOCODE/IMO/MMSI and concurrent duplicate creates return safe 409; cross-tenant identical values remain independent; archive does not permit recycling |
| G3-S19 Search/pagination | Filter by status, code/name/identifier using escaped bounded input; default and maximum limits, stable ascending UUID cursor, no skips/duplicates at page boundaries, no scope leakage or unbounded response |
| G3-S20 Concurrent mutation races | Two updates or update/archive/restore at same expected version yield exactly one successful version transition; no lost writes or unauthorized mutation after concurrent membership change |
| G3-S21 Archive and restore | Only Manager can archive/restore; status/version precondition enforced, no hard delete, archived records stay identified, duplicate identifier remains reserved, stale repeated actions return conflict |
| G3-S22 Three-browser reference flow | Chromium, Firefox and WebKit: sign in, select organization, view ports and vessels, search, page, inspect, Manager creates/edits/archives/restores, reload retains selected context |
| G3-S23 Browser race and permission changes | Switch A to B with delayed A HTTP response; A data is immediately cleared and cannot reappear. Revoke role/suspend org/log out during fetch; clear protected state, show safe errors and no stale details |
| G3-S24 M0/M1/M2 regressions and recovery | All previously accepted .NET, HTTP, session, membership, three-browser and Compose tests remain green; simulated PostgreSQL outage returns sanitized 503, recovers, and revoked scope stays denied |
| G3-S25 Operational, security and documentation gate | Clean Release and web builds, dependency audit, exact running image revision labels, zero unresolved HIGH/CRITICAL image findings or documented blocker, no new paid service/public exposure, local E: evidence, documentation links/graph checks and correct G3 status |

A test may cover several scenarios, but each scenario must cite named executable tests and their observed outputs. Permission scenarios require both real database and actual HTTP-pipeline checks. Three-browser evidence is mandatory for the functional M3 workspace, not an accessibility audit.

## 4. Expected error and data exposure contract

| Condition | Expected |
| --- | --- |
| Missing/invalid authenticated session | 401 JSON, never browser redirect or data |
| Selected organization absent | 409 `organization_selection_required` |
| Selected A but route B or foreign record | 404 `resource_not_found` without information about B |
| Valid own selection but insufficient reference permission | 403 `access_denied` |
| Missing CSRF, wrong Origin or unauthorized mutation transport | 403 `csrf_invalid` |
| Bad identifier or body/filter | 400 `invalid_request` |
| Duplicate code/ID, stale version, already-archived transition | 409 `conflict` |
| PostgreSQL service unavailable | Sanitized 503 `dependency_unavailable`; no stale cached business data |
| Successfully created/updated/archived | 201 / 200 / 204 respectively |

Every response retains the request correlation ID and bounded body. Logging must not reveal passwords, session cookies, antiforgery tokens, database connection strings or tenant-specific data from a denied request. Deliberately inject a foreign UUID that really exists to prove object-level authorization, not only a random nonexistent ID.

## 5. Regression, concurrency and migration detail

- Migration must preserve M0 bootstrap row, M1 account/security/session rows and M2 organizations/memberships, including the selected session organization. Include snapshot/no-pending migration and repeat-upgrade tests.
- Use two independently allocated PostgreSQL contexts for write races and measure success/failure and final database state. Optimistic version checks alone are insufficient if a membership is revoked between authorization and mutation; revalidate after locking in the same transaction.
- For search, use first, middle and terminal pages with deliberate duplicate names and varied UUID order. Test `after` values that are foreign, malformed, missing, and valid but outside a filtered result without revealing their existence.
- Simulate a delayed response from tenant A after selecting tenant B, revocation while a tab is open, database stop/start and API restart without stale UI leakage.
- If a test failure is caused by harness/setup, fix the harness with an assertion that still proves the product requirement. Never convert a required test to SKIP, remove a role from a matrix or relax authorization to obtain green results.
- After a correction affecting code or tests, rerun the entire mandatory G3 matrix at the final fixed SHA, not only the previously failing scenario.

## 6. Verification stages and evidence register

Create or extend an E:-confined local runner, preserving the strict storage preflight, disposable-only Compose guard and clean failure handling of `scripts/verify-m2-local.sh`. The final run must record at least:

| Stage | Required evidence |
| --- | --- |
| Preflight | OS, repo path and SHA, E: temp/cache/browser/Docker settings, disposable project/database isolation, versions of SDK/Node/Docker/PostgreSQL/Playwright/Trivy |
| Domain and contract | Named xUnit tests and counts; permission matrix, malformed input and HTTP contract results |
| Database fresh + upgraded | Migration IDs, fresh and M0-M2-to-M3 upgrades, repeat migration, constraints and seed preservation |
| Browser | Three engine names/versions, spec counts, role/tenant/race and UI state transition results |
| Operations | Compose startup, API restart, session restoration, database failure/recovery, sanitized provider errors and durable revocation |
| Security | Actual running images and SHA labels, advisories/high-critical findings, negative access probes and secret-log review |
| Documentation | `node scripts/render-execution-graph.mjs --check` and `node scripts/check-planning-docs.mjs` results on the final source |
| Gate record | PASS/FAIL/BLOCKED/NOT RUN for all G3-S01 to S25, evidence path and any residual risk |

The local runner captures stage logs and structured TRX/browser results on E:. `scripts/assert-g3-evidence.mjs` writes a source-revision-labeled coverage index that fails on missing named evidence; the index must still be reviewed against the full negative-case matrix. See [M3 correctness remediation](M3_CORRECTNESS_REMEDIATION.md).

Write `docs/M3_EXECUTED_VERIFICATION.md` only when there is real execution evidence, not as a prefilled PASS template. The evidence must distinguish prior M0-M2 accepted results from G3 runs on the new source SHA.

## 7. Closure and acceptance rule

G3 can be marked `verified locally; acceptance pending` only when every mandatory scenario has successful evidence at one final implementation SHA, with no unresolved security, data-integrity or isolation blocker, and synchronized repository documentation. A missing mandatory browser engine, scanner, E: storage confinement, migration proof or regression result blocks G3.

**G3 is not yet verified, accepted or merged. Source implementation is committed on a feature branch, but runtime execution remains outstanding.** This document defines what a future execution must prove. Explicit design approval precedes implementation; explicit G3 acceptance follows verification. No M4 implementation until the G3 gate is satisfied under the roadmap policy.
