# G12 Independent Raw Evidence Review

**Review date:** 2026-11-08  
**Gate:** G12, shipment search and restricted CSV export  
**Reviewed technical source:** `5dedbb70b4d2f4088f117d905e90d59f23a26915`  
**Supplement, test-only:** `3d450ac320b00538b106f2ad6d4b86c88661d7a4`  
**Documentation snapshot:** `857105081ef57d40bf0bfe97c76212a2007e64e3`  
**Disposition:** **Independent evidence examination performed; G12-S25 NOT PASS. G12 acceptance pending.**

## Evidence authenticity and executed coverage

The submitted archive hashes to `35d43c916bc4242cafa5ecf232d437aee9e1ef7918855f3cc866de8453b69b4d`, exactly matching the transfer report. The embedded `MANIFEST.sha256` hashes to `7cadf6cb1edfd27ebaf7f65c357ace7496f1c331b3ee1d6d4fbb5abfbcfc12ba` and **all 277 listed files independently re-hash correctly**. The three derived records `READBACK.txt`, `trx-summary.json` and `safety-scan.json` are outside the manifest. Every TRX counter was independently compared against actual per-test outcomes; 40/40 combined existing and residual tests and 1/1 query-plan test PASS on the production-fix SHA. Earlier 8-case export revocation failures are preserved in raw TRX files and logs, followed by a 23/23 corrective residual pass.

## Defect F-M12-01 review

The source change in `ShipmentSearchStore.cs` catches PostgreSQL SQLSTATE 40001 through wrapped exceptions and retries at most three transaction attempts within one overall 25-second cancellation budget. The evidence records failures of the first attempt to fix the wrapped exception, then successful local revocation, session concurrency and export tests. The defect was a real availability and authorization-response defect, not an information disclosure: the failing paths returned 503 without CSV or prepared audit. The revised path was verified by 23/23 residual and 4/4 existing G12 integration tests on `5dedbb7`. This supports the specific remediation. Do not infer complete unrelated production behavior from it.

## G12-S20 measured plan verification

Independently parsed all **23 retained warm EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) plans** and compared execution times, buffer counts and shipment sequential-scan flags with `final/plans/summary.json`. **Zero discrepancies.** The 170,000-shipment dataset contains three tenants (120,000, 40,000 and 10,000), with observed maximum warm statement execution **49.345 ms**, below the predeclared 1,000 ms search and 3,000 ms export thresholds. Small-tenant statements stay below 2,500 buffers and avoid shipment sequential scans. Six large-tenant text/exact-5,000 export plans use sequential scans of the shipment heap (4,974 shared-hit buffers). The declared thresholds pass at this fixture scale, but these scans remain an explicit future scaling limitation. **G12-S20 technical evidence is sufficient for the tested representative volume**, not for untested million-row projections.

## Remaining G12 requirements

- **S17/S19:** Real 15-second database statement timeouts and 25-second in-process request cancellation were recorded. The HTTP 5,000-row response and SQL 10-MiB boundary were tested, with 5,001 rejected. **A real nginx reverse-proxy request path, 30-second external read-timeout envelope, CPU-bound deadline and TCP-level mid-download interruption were NOT executed.** Configuration inspection and an in-memory test server are not equivalent.
- **S09/S13/S14/S15/S16/S18:** The local reports show passed live-page, revocation, parity, Unicode/CSV, safe response-header, proof-rotation and audit-rollback cases. The prior 503 defect is fixed on the tested production SHA. Do not promote other HTTP role/filter cases without targeted proof.
- **S03-S08, S10, S12, S24:** The committed G12 matrix still marks significant filter variants, unauthorized existence probes, browser/raw security evidence and other components as PARTIAL. Preserve those outcomes.
- **S22:** Three-engine hosted browser testing passed on the earlier `f56d017` application SHA, but raw reports were not attached to the checked hosted run and no local browser rerun was performed on the export-retry SHA. Mark as earlier hosted evidence, not final local PASS.
- **S23:** The old 341/341 full integration run predates F-M12-01. Local change-impact coverage is strong, but a complete final-SHA full suite or an expressly approved, documented change-impact waiver remains required.
- **S01:** M12-P01..P16 were selected as implementation defaults; formal gate policy approval is still pending. The Q2 historical snapshot was explicitly N/A for a known repository-owned production snapshot, while populated 13-to-15 migration preservation passed.

## Review determination

**G12-S25: REVIEWED WITH OPEN MANDATORY FINDINGS, NOT PASS.** The local package, defect remediation and query-plan results are internally consistent. Formal G12 acceptance is not recommended until the real proxy/stream-cancellation boundary is tested or explicitly waived, the remaining PARTIAL/NOT EXECUTED matrix items are closed with evidence, final-source regression coverage is reconciled, and S01 is formally decided.

No protected-main merge, deployment, production migration, GitHub Actions run or M9 hold modification is authorized by this review.

## Subsequent remediation

The findings above were addressed by the closure campaign recorded in [local closure results](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md). This review is retained as written for the first package; an independent re-review of the closure package is pending.
