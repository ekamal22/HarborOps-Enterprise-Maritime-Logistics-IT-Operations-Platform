# G12 Source-Locked Scenario Evidence Matrix

**Reviewed:** 2026-11-08 (local closure update)
**Frozen hosted application SHA:** `f56d017d9b2619be56fe533f73ba1d11f63974c0`
**Local verification SHAs:** `5dedbb70b4d2f4088f117d905e90d59f23a26915` (export retry fix plus focused tests, final run) and `3d450ac320b00538b106f2ad6d4b86c88661d7a4` (test-only supplement)
**Basis:** local execution against disposable PostgreSQL 16.15 servers with raw TRX retained on E:, plus the hosted run logs for items marked hosted. Not an independent review.
**Gate:** G12 acceptance pending. Dispositions below are traceability statements, not an acceptance decision.
**Totals:** BLOCKED 2, NOT APPLICABLE 1, NOT EXECUTED 1, PARTIAL 12, PASS 9

**Disposition key:** PASS = every required check of the scenario has executed local evidence. PARTIAL = executed subset passed, remainder NOT EXECUTED; the scenario is not closed and counts as not accepted. BLOCKED = needs an external or independent decision. NOT EXECUTED = no local execution. NOT APPLICABLE = justified.
**Evidence root (E:):** `E:\HarborOps-Verification\evidence\m11m12-local-01` (final run `final/`, amended G11 supplement `final-supplement/`, historical failed attempts under `raw/` and `logs/`, sealed by `MANIFEST.sha256`). See [local closure results](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md).

| Scenario | Disposition | Executed evidence | Remaining requirement or caveat |
| --- | --- | --- | --- |
| G12-S01 | BLOCKED | M12-P01..P16 selected; per-instance lease and rate limit (P10), 45 second proof (P12), 5,000 rows and 10 MiB (P10) confirmed by executed tests. | Formal policy approval is an acceptance decision. |
| G12-S02 | PASS | Release build 0/0, 291/291 unit, 46/46 contract at `5dedbb7`. | Web build not rerun locally. |
| G12-S03 | PARTIAL | `AdvancedFiltersAndProtectedCursorStayTenantScoped` PASS locally; plan probe exercises status, record status and text filters. | Full M5 case/null/archive/status matrix NOT EXECUTED. |
| G12-S04 | PARTIAL | Voyage filter with an open assignment PASS; plan probe shows the assignment join plan. | Closed, future and duplicate M6 assignment variants NOT EXECUTED. |
| G12-S05 | PARTIAL | Origin/destination filters PASS; plan probe on 40 ports per tenant. | Wrong-tenant port UUID cases NOT EXECUTED. |
| G12-S06 | PARTIAL | `UtcWindowsAreHalfOpenAndBounded` and plan probe windows PASS. | HTTP exact-edge and offset variants NOT EXECUTED. |
| G12-S07 | PARTIAL | Input guard, repeated/unknown parameters, over-length text PASS in the existing test. | Over-length encoded query matrix NOT EXECUTED. |
| G12-S08 | PARTIAL | Actor swap, damaged and filter-mismatched cursor PASS; cross-tenant cursor refused in the live-page test. | Cursor expiry, session-swap and mutating-page variants NOT EXECUTED. |
| G12-S09 | PASS | `LivePagesReflectInsertUpdateAndArchiveBetweenPagesWithoutLeakingTenants`: a row inserted behind the cursor is not revisited, one inserted ahead is returned, an archived row ahead is excluded, a renamed row shows its live value, no row appears twice, no foreign-tenant row appears, a cursor from one organization is refused for another. Live-page semantics, not a snapshot, as documented. |  |
| G12-S10 | PARTIAL | `CombinedSearchAndExceptionRoutesPreserveM5ShipmentDetail` PASS locally. | Reauthorization and archived-detail transitions NOT EXECUTED. |
| G12-S11 | PASS | `RestrictedRuntimeExportIsScopedAndAudited` (Administrator, Auditor, Support, Analyst, Manager under the restricted login) and the 16-case revalidation theory. |  |
| G12-S12 | PARTIAL | Wrong-organization 404 and cross-organization cursor refusal PASS; small-tenant plans touch 300 buffers regardless of other tenants. | Foreign-org join and existence-probe equivalence NOT EXECUTED. |
| G12-S13 | PASS | `InFlightRequestsRevalidateAuthorityAfterTheOrganizationLockIsReleased` 16/16 (search and export x none, session revoked, member removed, role downgraded, organization suspended, account disabled, security version, organization switched). Initial run FAILED 7/8 export cases (503) and exposed defect F-M12-01; fixed in `5dedbb7`; all 16 PASS, denied cases 403/404 with zero audit rows. | Historical failing runs retained under `logs/` and `raw/`. |
| G12-S14 | PASS | Search-to-export parity over 260 rows with a shared filter: identical identifier sets. |  |
| G12-S15 | PASS | `CsvPreservesUnicodeUsesCrlfDefangsFormulasAndMatchesSearchResults`: UTF-8 without BOM, CRLF only, quoted fields round trip, accents/CJK/Arabic/emoji intact, `=`, `+`, `-`, `@`, leading-space and zero-width-prefixed formulas defanged. |  |
| G12-S16 | PASS | Same test: fixed 12-column header, `text/csv; charset=utf-8`, attachment filename pattern, `nosniff`, `private, no-store`, no organization id, session, password, email or CSRF text. |  |
| G12-S17 | PARTIAL | Exactly 5,000 worst-case rows accepted (4,870,215 bytes, 974 per row, 46% of 10 MiB) with audit byte count equal to the response; 5,001 refused 413 with no audit; SQL function accepts 10,485,760 bytes and refuses 10,485,761, 5,001 rows and 0 bytes; real 15 s statement timeout fired at 15.01 s; internal budget cancelled search and export at 25.01 s (body names the request budget); `proxy_read_timeout 30s` present. | 10 MiB cannot be reached over HTTP (maximum about 5 MB), so that limit is verified at the SQL boundary only. CPU-bound deadline and the real nginx proxy path NOT EXECUTED. |
| G12-S18 | PASS | Existing restricted test (nine tampered proof fields, replay, wrong role, key-table read denial, missing and mismatched runtime key) plus expired and not-yet-valid proofs, key rotation (old key refused, new key accepted), verifier key row removed, trigger-injected audit rejection (503, no audit, no CSV) and downgrade refusal after a retained export (`M12 audit history exists`, function intact). |  |
| G12-S19 | PARTIAL | Client abort mid-queue leaves no audit and releases the lease; discarded body keeps exactly one preparation audit (documented P12); same-instance concurrent export 429 `export_in_progress`; second instance independent; rate limit 429 per instance; 25 s budget. | TCP-level mid-download interruption and the real proxy path NOT EXECUTED (in-memory test server). No cross-instance aggregate limit exists (documented per-instance policy). |
| G12-S20 | PASS | `SearchAndExportPlansAreMeasuredOnRepresentativeMultiTenantData`: 170,000 shipments (120k, 40k, 10k), restricted runtime role, exact emitted SQL captured, 23 plans with `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` (cold and warm) in `final/plans/`. All queries other than free-text search on the largest tenant use indexes, at most 8.8 ms and 5,835 buffers; keyset page 2 and a 60,000-row-deep keyset 0.1 ms; export of exactly 5,000 rows 35.6 ms; 5,001-row refusal 3.9 ms; small-tenant queries use indexes and 300 buffers. Declared criteria met. | Finding: free-text search in the largest tenant (70% of the table) is a parallel sequential scan of the whole shipments heap (4,974 buffers, 32 to 49 ms). Cost grows with total table size for such tenants; no trigram index exists. Acceptable at 170k rows; see results record. |
| G12-S21 | NOT APPLICABLE | Q2 2026 snapshot not applicable (documented); populated 13-to-15 upgrade PASS locally (`M11M12UpgradeTests`). | Reopen if an external production snapshot is supplied. |
| G12-S22 | NOT EXECUTED | 36/36 three-engine browser run reported in the hosted log for `f56d017`. | Raw browser JSON not retained; browsers not rerun locally. |
| G12-S23 | PARTIAL | Hosted 341/341 and 40/40 at `f56d017`; local change-impact at `5dedbb7` (all M11/M12 classes, upgrade, unit, contract). | Full 341-case suite at `5dedbb7` NOT EXECUTED; one method changed. |
| G12-S24 | PARTIAL | Hosted 5/5 effective mutants and scans; local negative controls for revocation, proof and limits; evidence safety scan 0 hits. | Remaining export-abuse mutants and raw scan output NOT EXECUTED locally. |
| G12-S25 | BLOCKED | Local package sealed and read back. | Independent reviewer must reparse the raw evidence and issue a separate written decision. |

## Review outcome

PARTIAL rows are not promoted. G12-S20 now has measured plans. Independent S25 raw-evidence review is required before any acceptance decision. Historical failed attempts remain recorded in the [run index](M11_M12_EXECUTED_RUN_INDEX_20261108.json) and in the retained local logs. See the [acceptance review](M11_M12_ACCEPTANCE_REVIEW_20261108.md) and the [local closure results](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md).
