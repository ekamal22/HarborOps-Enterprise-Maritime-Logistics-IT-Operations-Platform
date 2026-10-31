# M3 Second Correctness Audit and G3 Verification Review

**Audit date:** 2026-10-31  
**Source revision examined:** `5a2259671656bd0d2f73d1c2e0026be73e74b5ad`  
**Branch:** `feat/m3-port-vessel-reference-data`  
**Method:** Read-only review of the current GitHub source, approved M3 architecture, G3-S01 through G3-S25 test contract, API/domain/database code, browser implementation, test harness, and existing remediation records.  
**Execution:** No .NET build, PostgreSQL migrations, browser tests, Docker Compose, image scans, or GitHub Actions were executed. No workstation drive accessed.  
**Gate:** G3 OPEN. Acceptance and merge are blocked.

## Executive finding

The remediated M3 source includes material improvements over the first audit, including explicit bounded JSON parsing, independent mutation busy tracking, transaction-scoped reads, richer adversarial tests, deterministic reference fixtures, and a G3 evidence index. However, the newly added database trigger function contains a definite PostgreSQL SQL syntax error, so M3 migration execution cannot succeed unchanged. The G3 evidence checker also does not yet safely prove that each scenario passed on the final revision.

Severity is assigned to the potential impact. Certainty is identified separately: confirmed from source, high-confidence logic defect, coverage shortfall, or runtime unknown. This report does not treat authored test cases as executed results.

## Findings

| ID | Severity | Certainty | Location and finding | Corrective acceptance condition |
| --- | --- | --- | --- | --- |
| M3-B01 | **Blocker** | **Confirmed SQL syntax** | `src/HarborOps.Infrastructure/Persistence/Migrations/20261031110000_PortVesselReferences.cs:69-82` declares `CREATE FUNCTION ... LANGUAGE plpgsql AS $` and closes with `$;`. PostgreSQL dollar-quoted literal delimiters must be complete and paired, e.g. `$$ ... $$`. A standalone `$` is invalid. | Fix both delimiters. Run fresh and historical migrations, trigger negative writes, repeat migration, rollback check, and schema-drift test on disposable PostgreSQL. |
| M3-B02 | High | Confirmed evidence weakness | `scripts/assert-g3-evidence.mjs:14-33,93-106` recursively reads all TRX files already in `evidence/trx`; a prior PASS may satisfy the checker even if a matching case was skipped or absent in the newest run. It reads an SHA string but does not enforce the SHA against all evidence files. | Use per-run isolated evidence directories, record start/end timestamps and SHA, bind each test result to the final run, reject absent/skipped/stale cases, and require both fresh and repeated integration passes. |
| M3-B03 | High | Confirmed evidence weakness | `scripts/assert-g3-evidence.mjs:54-57` treats any nonempty `g3-<stage>.log` as stage evidence; it does not independently validate stage exit code, scenario assertions, build counts, scanner reports, or source SHA. The local runner stops on nonzero exits while running, but the indexer cannot safely judge an independently supplied evidence directory. | Save explicit machine-readable exit statuses and stage provenance; reject nonzero, unknown or stale statuses. Confirm scanner result files and image labels, not just log existence. |
| M3-B04 | High | Coverage shortfall | `scripts/assert-g3-evidence.mjs:58-84` lists minimum named test fragments, not all mandatory G3 assertions. For example S10 reuses the anonymous/wrong-role test name; S20 maps record races but not concurrent administrative revocation; S23 maps simulated browser error responses instead of real revocation. `PRESENT` is therefore not equivalent to G3 PASS. | Make the G3 matrix requirement-level, with explicit test IDs, run IDs, negative assertions and scenario completion criteria. Do not mark a scenario complete based on only one representative fragment. |
| M3-B05 | High | Confirmed test-design gap | `tests/browser/specs/reference-races.spec.mjs:59-79,113-132` injects synthetic 503 and 403 through `page.route`. Those checks can prove frontend handling but do not prove that PostgreSQL outage, role revocation, or organization suspension causes the correct live backend response. | Add a controlled real backend revocation and outage/recovery test with actual database and HTTP interactions, and a browser assertion of protected-state clearing. Retain mocked tests as complementary UI tests. |
| M3-B06 | High | Confirmed test-design gap | `tests/HarborOps.IntegrationTests/ReferenceConcurrencyTests.cs` tests simultaneous record mutations/collisions; it does not deterministically establish that a queued M2 membership downgrade or removal wins before a competing M3 write. The attempted controlled interleaving test is not in the committed source. | Add deterministic lock-ordered, independent-connection membership and organization change races and assert final database state. Check session/account security-version changes under concurrent writes. |
| M3-B07 | High | Confirmed UI-state gap | `web/src/references/ReferenceDataWorkspace.tsx:67-76,123-129` clears rows on a 403/401 but leaves the parent `OrganizationWorkspace` selection and `canManage` role unchanged. Management controls remain visible after an access-denied response, despite their API calls being denied. | On access revocation or expiration, notify the parent and invalidate the selected role/business workspace immediately. Assert the create/edit/archive controls disappear and no old detail content remains. |
| M3-B08 | Medium | High-confidence logic defect | `web/src/references/ReferenceDataWorkspace.tsx:92-106` still guards `setBusy(false)` for Load More using `serial === generation.current`. If a visibility-triggered reload changes the generation before Load More settles, the `finally` branch does not reset busy state, potentially leaving pagination and mutation controls disabled. | Use request-specific pagination tracking and unconditional safe cleanup for the current mounted context. Add delayed-page/visibility-refresh browser test. |
| M3-B09 | Medium | Confirmed validation limitation | `scripts/verify-m3-local.sh:15-28` verifies an operator-provided VHDX exists on E: but cannot establish it is the active Docker Desktop/WSL data backing file. | Validate Docker Desktop storage configuration against actual active backing; fail closed if active path cannot be established. Record the resolved path without changing primary data. |
| M3-B10 | Medium | Confirmed operational gap | `scripts/verify-m3-local.sh:36,73-105` logs an abort in a trap but does not call a guarded disposable-stack teardown. Early failures can leave the disposable Compose project/containers. | Add explicit, scoped, failure-safe cleanup with project identity checks and retain logs. Never operate on the primary stack. |
| M3-B11 | Medium | Coverage shortfall | Browser tests include basic port lifecycle, vessel creation, pagination, reload, and synthetic 403/503/race cases. Full cross-browser vessel edit/archive/restore, stale-version conflict UI, and server-driven role-change transitions are still not comprehensively asserted. | Complete role-aware and state-transition coverage in Chromium, Firefox and WebKit, including real server responses. |
| M3-B12 | Medium | Verification unknown | M3 migration and current EF snapshot, added trigger definitions, hard database constraints, rollback, source build, and runtime startup have never been executed on the final source. Source-level mapping looks intentional but cannot replace EF model-diff and PostgreSQL runtime evidence. | Execute clean .NET 10 Release build, `HasPendingModelChanges`, migration/upgrade/constraint suites, and startup/HTTP smoke before accepting G3. |

## Previous audit comparison

| First audit issue | Second-pass assessment |
| --- | --- |
| A01 DELETE binding | Source-level correction present: all six M3 mutations now read bounded JSON directly from `HttpContext` after transport validation. Runtime startup still not executed. |
| A02 mutation busy state | Source-level correction present for create/edit/archive/restore. Load More has a distinct remaining stale-generation busy-state issue (B08). |
| A03 G3 executed evidence | Evidence index and stage logs authored, but provenance, completeness and independent exit validation are deficient (B02-B04). No executed G3 evidence. |
| A04 migration and DB constraints | Historical-upgrade and direct-SQL tests authored. New M3 trigger SQL is syntactically invalid and blocks their intended execution (B01). |
| A05 role matrix | Support/Auditor and session-state tests authored; not executed. |
| A06 fixture repeatability | Browser reference fixture script creates two-tenant data and compares repeated snapshots; not executed. |
| A07 security races | True foreign objects and record concurrency added; deterministic concurrent M2 administrative change remains missing (B06). |
| A08 browser races | Mock delayed-response, synthetic 403/503 and logout scenarios added; real backend negative states remain missing (B05/B07). |
| A09 storage safety | Declared VHDX path and stage evidence added, but active backing remains unproven (B09). |
| A10 transition consistency | Archive through DELETE and restore through PATCH are now constrained in source; not executed. |
| A11 database immutability | Trigger source added, but cannot execute until the SQL syntax is fixed (B01). |
| A12 malformed body handling | Explicit JSON parsing and size checks added; runtime tests not executed. |
| A13 stale planning docs | Current documentation links to the original audit and remediation register, and marks G3 open. |
| A14 contract breadth | New vessel, pagination and negative HTTP tests authored; complete three-browser and server-driven authorization behavior remain open. |
| A15 read authorization race | Transaction-scoped organization/session/user share locks and current-security-version check present; interleaving proof not executed. |
| A16 write security version | Current session and account security versions compared in transactional write path; deterministic reset/disable race still unexecuted. |

## G3 scenario disposition at this review

The following statuses describe **source coverage only**. The execution status of every G3-S01 through G3-S25 scenario remains **NOT RUN** at this audit revision.

| Scenario | Source-level readiness limitation |
| --- | --- |
| S01 | Domain validation tests authored, runtime PASS absent |
| S02 | Vessel validation tests authored, runtime PASS absent |
| S03 | Five-role logic and HTTP test source authored, execution absent |
| S04 | Fresh migration blocked by B01 |
| S05 | Historical M2-to-M3 migration blocked by B01 |
| S06 | Trigger/constraint verification blocked by B01 |
| S07 | Repeatable synthetic fixture source authored, execution absent |
| S08 | Anonymous/invalidated-session tests authored, execution absent |
| S09 | Missing selection assertions authored, complete verb evidence absent |
| S10 | Foreign organization tests authored, map completeness issue B04 |
| S11 | Foreign port UUID tests authored, execution absent |
| S12 | Foreign vessel UUID tests authored, execution absent |
| S13 | Role matrix tests authored, execution absent |
| S14 | Membership and organization changes partly tested, deterministic races B06 |
| S15 | Malformed JSON/Origin/CSRF test source authored, execution absent |
| S16 | Port lifecycle tests authored, execution absent |
| S17 | Vessel lifecycle API tests authored, browser completeness gap B11 |
| S18 | Identifier collision/archival tests authored, execution absent |
| S19 | Cursor/filter/pagination tests authored, UI interruption defect B08 |
| S20 | Record races authored, authorization race absent B06 |
| S21 | Archive/restore tests authored, execution absent |
| S22 | Three-browser tests authored, execution absent and coverage B11 |
| S23 | Simulated browser 403/503 not a real backend revocation/outage proof B05/B07 |
| S24 | Local M0-M2 regression runner wired, execution absent |
| S25 | Runner, storage provenance, stage-evidence checks and scans unverified B02/B03/B09/B10 |

## Required repair order

1. Correct the invalid PostgreSQL function dollar delimiters (B01) and run isolated fresh plus M2-to-M3 migration tests.
2. Repair the G3 evidence index so stale, skipped, partial, wrong-SHA and failed stages cannot be promoted to `PRESENT` (B02-B04).
3. Repair parent/child permission invalidation and the remaining pagination busy-state race (B07-B08).
4. Add deterministic administrative-revocation versus reference-write tests, real backend 403/503 browser tests, and full vessel browser transitions (B05-B06/B11).
5. Prove the active E:-backed Docker storage image and implement guarded failure cleanup (B09-B10).
6. Run the entire M0-M3 local test and operational matrix against one final clean implementation SHA. Repeat the complete suite after the last fix.
7. Only after evidence passes, produce `docs/M3_EXECUTED_VERIFICATION.md` with commands, versions, observed results, risk review and acceptance pending. Do not mark G3 accepted or merge before explicit acceptance.

## State and constraints

No source defects were repaired by this audit report. The current `main` branch remains M0-M2 accepted. This feature branch remains unmerged. GitHub Actions are prohibited and no workflow runs were initiated by this audit. Changes to evidence or test scripts do not permit claims that G3 has passed.
