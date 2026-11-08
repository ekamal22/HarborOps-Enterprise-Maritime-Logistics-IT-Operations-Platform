# G12 Source-Locked Scenario Evidence Matrix

**Reviewed:** 2026-11-08
**Frozen application SHA:** `f56d017d9b2619be56fe533f73ba1d11f63974c0`
**Basis:** GitHub run logs and source inspection. This is not an independently sealed raw evidence archive.
**Gate:** Acceptance pending. Individual EXECUTED CHECKS rows describe demonstrated tests, not a closed whole-scenario Sxx gate.

| Scenario | Evidence state | Demonstrated evidence | Remaining requirement |
| --- | --- | --- | --- |
| G12-S01 | TECHNICAL POLICY SELECTED | M12-P01..P16 selection recorded | Formal S01 approval and implemented contract reconciliation |
| G12-S02 | EXECUTED CHECKS | 291 unit, 46 contract, web and clean Release build | Final source-lock evidence manifest |
| G12-S03 | EXECUTED SUBSET | Search normalizes status/text and M5 regression | Full M5 case/null/archive/status matrix |
| G12-S04 | EXECUTED SUBSET | AdvancedFiltersAndProtectedCursorStayTenantScoped | Closed/future/duplicate M6 assignment variants |
| G12-S05 | EXECUTED SUBSET | Scoped origin/destination filter implementation | Wrong-tenant UUID and independence cases |
| G12-S06 | EXECUTED SUBSET | UtcWindowsAreHalfOpenAndBounded | HTTP exact edge and offset variants |
| G12-S07 | EXECUTED SUBSET | Input guard and filter fingerprint tests | Overlong encoded query, unknown/repeated parameter HTTP matrix |
| G12-S08 | EXECUTED SUBSET | CursorRequiresMatchingOrganizationAndFilters; AdvancedFiltersAndProtectedCursorStayTenantScoped | Expiry and actor/session/tamper/mutating page variants |
| G12-S09 | NOT FULLY EVIDENCED | Live stateless page policy recorded | Concurrent insert/update/archive between pages |
| G12-S10 | EXECUTED SUBSET | CombinedSearchAndExceptionRoutesPreserveM5ShipmentDetail | Reauthorization/archived detail transitions |
| G12-S11 | EXECUTED SUBSET | RestrictedRuntimeExportIsScopedAndAudited, role gates | Complete live five-role and cross-login matrix |
| G12-S12 | EXECUTED SUBSET | Restricted runtime export and cursor tenant checks | Foreign-org joins and cross-organization existence probes |
| G12-S13 | NOT FULLY EVIDENCED | Live scope check exists | In-flight revocation, suspension and organization switch |
| G12-S14 | EXECUTED SUBSET | AdvancedFiltersAndProtectedCursorStayTenantScoped; export role test | Search-to-CSV parity with stable shared filters |
| G12-S15 | EXECUTED SUBSET | CsvEscapesAndDefangsFormulas, fixed 12-column export | HTTP Unicode, CRLF and spreadsheet injection end-to-end |
| G12-S16 | EXECUTED SUBSET | FixedColumnsAndBoundedOutput, HTTP endpoints | All response headers and forbidden-field redaction |
| G12-S17 | EXECUTED SUBSET | 5001-row denial; blocked DB 15-second timeout without partial CSV | Actual 5000 row PASS, 10 MiB +/-1, CPU budget and 30-second proxy |
| G12-S18 | EXECUTED SUBSET | RestrictedRuntimeExportIsScopedAndAudited, proof forgery and SQL grants | All nine-field proof permutations, key rotation, audit outage/rollback and downgrade after export |
| G12-S19 | EXECUTED SUBSET | DatabaseReadLockTimesOutBeforeProxyBudgetWithoutExportOrPartialCsv | Browser abort, multiple API instances, concurrency/rate, 25/30-second full stack timing |
| G12-S20 | NOT EXECUTED | No measured representative EXPLAIN plan in verified tests/logs | Local EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) across realistic tenants, keyset and export filters |
| G12-S21 | NOT APPLICABLE FOR Q2 SNAPSHOT; UPGRADE EXECUTED | Q2 snapshot applicability record and populated 13-to-15 migration PASS | Reopen if an older external production snapshot is supplied |
| G12-S22 | EXECUTED CHECKS | 36/36 Chromium/Firefox/WebKit | Raw results and role/org-switch fixture verification |
| G12-S23 | EXECUTED CHECKS | 341/341 whole suite and 40/40 impacted | Source-lock and M9 hold applicability |
| G12-S24 | EXECUTED SUBSET | 5/5 focused effective mutants and security scans | Remaining export abuse mutations, raw scans, evidence-safety checks |
| G12-S25 | BLOCKED | GitHub job logs and TRX hash; no raw artifacts | Independent reparse, manifest validation, complete S01..S24 dispositions |

## Review outcome

All rows are traceability and sufficiency observations. No unresolved scenario is silently promoted to PASS. Source and log evidence alone does not fulfill the separate S25 independent raw-evidence review. Historical failures remain valid for their superseded source and are recorded in [run index](M11_M12_EXECUTED_RUN_INDEX_20261108.json).

See [coordinated readiness review](M11_M12_ACCEPTANCE_REVIEW_20261108.md) and [verification plan](M12_VERIFICATION_PLAN.md).
