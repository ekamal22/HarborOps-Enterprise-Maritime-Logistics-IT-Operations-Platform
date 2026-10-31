# M3 Implementation and G3 Correctness Audit

**Audit date:** 2026-10-31  
**Audited branch:** `feat/m3-port-vessel-reference-data`  
**Source snapshot:** `051ea3b8d1322f76a8fa085ec3ba6bb1095c35f5` before this audit-only document commit  
**Review mode:** Read-only source and verification-harness inspection using the repository connector; ASP.NET Core DELETE binding behavior cross-checked with the Microsoft Minimal API documentation  
**Execution:** No .NET compiler, PostgreSQL, Compose, browser or image-scan run. No GitHub Actions triggered. No C: or E: workstation file operations.  
**Gate:** G3 OPEN. Not ready for acceptance or merge.

**Follow-up:** [M3 correctness remediation register](M3_CORRECTNESS_REMEDIATION.md) records subsequent source changes and remaining verification work. Findings here reflect the original audited snapshot.

## 1. Summary

M3 implements a substantial initial port/vessel reference-data slice on the feature branch, with scoped store methods, additive migration source, ten HTTP operations, React screens and test files. However, committed code is not verified code. The review found two immediate release blockers and multiple gaps between the mandatory G3-S01 through G3-S25 plan and executable assertions.

Severity meanings: **Blocker** means a high-confidence startup failure or a defect breaking a core flow. **High** means a major correctness/security/test-evidence gap that must be closed before G3. **Medium** means a narrower contract/state inconsistency that requires correction or an explicit signed-off decision. All claimed outcomes below describe source inspection, not observed test runs.

## 2. Implementation findings

| ID | Severity | Finding, evidence and expected correction |
| --- | --- | --- |
| M3-A01 | Blocker | **DELETE request body binding.** `src/HarborOps.Api/References/ReferenceEndpoints.cs` maps `DELETE /ports/{portId}` and `DELETE /vessels/{vesselId}` with unannotated `JsonElement request` parameters (near lines 130 and 182). ASP.NET Core Minimal APIs do not implicitly bind DELETE request bodies. The endpoint may fail to initialize. Use explicit `[FromBody]` binding or read the request body after transport checks. Add a real application-startup test plus successful DELETE contract tests. See https://learn.microsoft.com/en-gb/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0 |
| M3-A02 | Blocker | **React stays busy after successful mutations.** `web/src/references/ReferenceDataWorkspace.tsx` `write` and `changeStatus` save `serial = generation.current`, then call `await reload()`. `reload` increments `generation.current`. Their `finally` blocks only call `setBusy(false)` when the old serial still matches, so a successful create/update/archive/restore leaves `busy=true` and disables controls. Separate mutation completion state from fetch generation and test repeated sequential operations in the same mounted workspace. |
| M3-A03 | High | **No executed end-to-end G3 record.** `scripts/verify-m3-local.sh` chains existing local stages and writes an SHA file but neither produces a G3-S01 to G3-S25 ledger nor validates each named scenario. `docs/M3_EXECUTED_VERIFICATION.md` is absent. A source commit or script success must not be promoted to G3 PASS. Implement a scenario-to-test manifest, capture commands, SHA, counts, fixtures and statuses, and run every mandatory stage on the final source revision. |
| M3-A04 | High | **Historical M2-to-M3 upgrade and direct constraint tests are absent.** `MigrationSnapshotTests.cs` checks ordering and snapshot; `ReferenceDataHttpTests.cs` calls `MigrateAsync` and checks one collision. No dedicated pre-M3 account/session/org preservation, repeated upgrade or direct SQL check/FK violation matrix has been found. Test M0-M2 seeded upgrade to M3 in a disposable database and verify all legacy records, selected sessions, negative DB writes, indices and repeatability. |
| M3-A05 | High | **Role matrix incomplete at real HTTP level.** `ReferenceBoundaryMatrixTests.cs` uses Manager, Analyst and Administrator for the ten verbs. It never provisions actual Support or Auditor callers for the M3 endpoint matrix; expired/disabled sessions are also not fully covered. Add every five-role/endpoint/verb pairing, plus session-state permutations. |
| M3-A06 | High | **Reference fixture determinism missing.** `scripts/prepare-m3-browser-fixtures.mjs` creates memberships in two organizations, but creates no prescribed deterministic port/vessel records and cannot be safely rerun against existing memberships with `expectedVersion:null`. Integration tests rely on random UUIDs. Implement isolated seeded reference fixtures with stable IDs/expected counts and a clean repeatability test for G3-S07. |
| M3-A07 | High | **G3 adversarial concurrency and foreign-object coverage incomplete.** Current tests cover concurrent port edits, one actual foreign port GET, foreign organization routes and later role revocation. They do not demonstrate foreign vessel CRUD substitution, port/vessel foreign PATCH/archive/restore, concurrent update/archive/restore, revocation during an in-flight write, organization suspension during access, or simultaneous duplicate creates. Add real concurrent contexts and post-commit row assertions. |
| M3-A08 | High | **Browser race tests are not implemented.** `tests/browser/specs/references.spec.mjs` has three cases: Manager workflow, Analyst visibility and an ordinary tenant switch. It does not delay tenant A's response until after switching to B, change role or suspend the organization mid-fetch, test sign-out during a fetch, or verify sanitized 503/recovery state. These are mandatory G3-S23 cases. |
| M3-A09 | High | **E: and disposal checks insufficiently proved.** `scripts/verify-m3-local.sh` reuses the G2 E: path guard and a core-dump override, but `docker info` prints the runtime root without verifying the actual Docker Desktop/WSL backing VHD location. The script does not assert each G3 test's evidence path, pass/fail and non-skipped counts. Preserve all G2 guards and add explicit Docker backing, executable tools and skip-detection checks before running destructive disposable stack stages. |
| M3-A10 | Medium | **Archive transition can be bypassed using PATCH.** `ReferenceStore.WritePortAsync` and `WriteVesselAsync` accept any `status` in `Active|Archived` for PATCH and increment version even for unchanged status. Repeating a PATCH to `Archived` on an already archived row succeeds with the next version, while DELETE explicitly rejects an already archived record. Define and enforce the same state machine for PATCH, DELETE and restore, then test both resources. |
| M3-A11 | Medium | **Model constraints do not fully enforce immutability.** The public EF setters and DB CHECK constraints do not prevent direct SQL changes to `organization_id`, `reference_code` or creation timestamp after insertion. HTTP allowlists protect normal clients but are not database-level immutability. The G3-S06 direct-write wording should either be satisfied with database safeguards and tests or explicitly revised with an approved boundary decision. |
| M3-A12 | Medium | **Malformed-body envelope depends on framework binding.** The handlers receive a `JsonElement` before invoking custom validation. Bad JSON, absent content type or body-size failures may return the framework's 400/415 without the standardized `invalid_request` code, `correlationId` and `no-store` controls. Exercise malformed body/content type and oversized body through the full HTTP pipeline, then normalize response/error behavior without weakening antiforgery. |
| M3-A13 | Medium | **M3 plan references remain inconsistent.** The feature branch status and implementation register describe authored source, but the architecture document still identifies the design as not implemented and retains a pre-implementation approval request. Align planning language with the actual source state while continuing to label runtime verification and G3 acceptance as outstanding. |
| M3-A14 | Medium | **Test/contract breadth remains narrow.** `ReferenceDataHttpTests.cs` completes the port lifecycle and vessel creation/lookup but not complete vessel update/archive/restore and state races. Browser cases omit reload persistence, paging, stale-version recovery and full negative error UI. Complete the contract and browser matrices; do not count authored but unexecuted cases as PASS. |
| M3-A15 | Medium | **Read-time permission refresh can return an empty success under a scope-change race.** `ReferenceStore.ListPortsAsync` and `ListVesselsAsync` call `ResolveAsync`, then execute a second query with fresh predicates. If the membership is revoked between them, the second query can return no rows with HTTP 200 rather than the prescribed denial. Reproduce the interleaving and ensure the API returns a sanitized authorization error on revocation, without exposing any row from the previous scope. |
| M3-A16 | Medium | **Session security-version check is missing from the M3 transactional revalidation.** `ReferenceStore.LockManagerAsync` checks selected org, expiry and revoked-at, and the current user status but does not compare the session and user's security versions after locking, as M1 middleware does before dispatch. Prove reset/disable races using independent database sessions and enforce the same authoritative session invariants inside the write transaction. |

**No defect has been marked fixed by this audit.** These are findings and required verifications, not modifications to M3 implementation code.

## 3. G3-S01 through G3-S25 source-coverage review

Every scenario below is **NOT RUN** in this audit. Coverage describes the existence of an appropriate assertion, not an executed result.

| G3 scenario | Authored coverage | Missing proof or gap |
| --- | --- | --- |
| S01 Port domain | Partial | Code/UNLOCODE cases exist; full negative input and direct-DB matrix still needed |
| S02 Vessel domain | Partial | IMO/MMSI unit cases and creation exist; mutable cases, cross-tenant uniqueness and duplicate-name behavior need expansion |
| S03 Reference permissions | Partial | All five roles in unit policy; real HTTP Support/Auditor scenarios missing |
| S04 Fresh migration | Partial | Migration source, order and snapshot test exist; cold real-DB execution not recorded |
| S05 M2 upgrade | Missing | Seeded old-model-to-M3 data and session preservation rehearsal |
| S06 Database constraints | Partial | SQL constraints authored; only limited API duplicate collision tested |
| S07 Fixture ownership | Missing | No repeatable deterministic port/vessel seed and counts |
| S08 Anonymous/disabled/revoked | Partial | Anonymous tested, full expired/disabled/revoked endpoint matrix absent |
| S09 No selection | Partial | Missing selection checked for port list only |
| S10 Foreign organization | Authored | All ten verb/path substitutions appear, but are unexecuted |
| S11 Foreign port UUID | Partial | Actual foreign port GET/list denial, no full write-method matrix |
| S12 Foreign vessel UUID | Missing | Real foreign vessel UUID under selected tenant for all verbs |
| S13 Real HTTP roles | Partial | No Support/Auditor callers, and incomplete Manager positive contract matrix |
| S14 Membership/org changes | Partial | Membership revoke/suspend after request; missing role downgrade, account disable and suspended organization |
| S15 CSRF/hostile bodies | Partial | Missing CSRF token, invalid identifiers and limited input tests; foreign Origin, malformed JSON, ownership injection and redaction matrix absent |
| S16 Port lifecycle | Partial | Basic create/read/update/archive/restore; incomplete field immutability and response metadata checks |
| S17 Vessel lifecycle | Partial | Create/list/error cases, no full edit/archive/restore browser/API progression |
| S18 Identifier collision | Partial | Same-org local code, IMO and UN/LOCODE collision; missing MMSI, concurrent creation, cross-tenant permitted reuse and archived reservation |
| S19 Search/pagination | Partial | Two pages, bounds and literal wildcard; missing end cursor, filtered page stability and cross-tenant cursor assertions |
| S20 Mutation races | Partial | Two port updates; missing update/archive/restore and concurrent authorization changes |
| S21 Archive/restore | Partial | Port transition; vessel transition and repeated stale/status changes missing |
| S22 Browser flow | Partial | Three specs configured for three engines; paging, detail/reload persistence and complete vessel management not demonstrated |
| S23 Browser race | Missing | Delayed tenant A response, revocation/suspension, logout and 503 recovery |
| S24 M0-M2 regression | Wired, unexecuted | Existing suites called by local script; results and exact SHA not available |
| S25 Operational gate | Wired, unexecuted | Builds, dependency review, scans and docs commands present, but final evidence and complete scenario mapping absent |

## 4. Immediate remediation order

1. Fix A01 and A02. Add endpoint-startup and repeated successful UI mutation assertions first. Confirm an actual .NET 10 Release build and a production web build.
2. Validate the additive EF snapshot/migration using fresh PostgreSQL and a seeded M2-to-M3 rehearsal. Repair model drift and any failed CHECK or index definitions without rewriting accepted M0-M2 migrations.
3. Complete negative authorization over every port/vessel method for all five roles, valid foreign objects, missing context, revoked account/session, suspended organization, CSRF and Origin.
4. Implement deterministic synthetic reference fixtures and the outstanding G3 race/browser cases, including three browser engines, stale request ordering and outage/recovery.
5. Add precise version/archival transitions and align database immutability guarantees with the design.
6. Harden the local E:-only verification runner, map every G3 scenario to executable tests and required observed counts, then rerun the whole suite at one final source SHA.
7. Record genuine PASS/FAIL/BLOCKED/NOT RUN evidence in `docs/M3_EXECUTED_VERIFICATION.md` only after execution. Keep G3 open until every mandatory scenario passes; request explicit acceptance before merging.

## 5. Verification and merge boundary

The current environment lacks the E: workstation's Docker Desktop/WSL backing images, PostgreSQL and .NET 10 runtime. This audit did not run the application. Source inspection and published framework behavior establish some high-confidence defects; EF runtime behavior, test outcomes and resource confinement remain unproven until actual local execution.

GitHub Actions execution is prohibited. All fixes and future local-evidence commits must use `[skip ci]`. Do not merge M3 into `main`, mark G3 verified, infer a production-security approval or begin M4 from this report.
