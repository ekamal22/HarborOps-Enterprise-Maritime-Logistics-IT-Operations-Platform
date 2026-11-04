# M8 Second Audit Remediation

**Date:** 2026-11-04  
**Branch:** `feat/m8-durable-event-inbox`, draft PR #14  
**Baseline audit:** [Second correctness audit](M8_SECOND_CORRECTNESS_AUDIT.md), source `ef32252916c42f7604db0ed5739f3313739982f9`  
**Status:** Five findings patched in source. **Executable proof remains pending.** D-006 acceptance pending, G8 not executed and no merge to `main`.

| Finding | Source remediation | Required verification | Disposition |
| --- | --- | --- | --- |
| **B01, High** | Final Compose logs, teardown, exit and timestamps are captured **before** the shareable-evidence scan. The scanner covers all files and file bytes. A private archive is unpacked to E:, checked against every manifest SHA-256 and scanned again for actual private credentials and raw event bodies before promotion. Any unsafe or inconsistent archive is quarantined, with a separate finalization result recording its disposition. | Inject synthetic canaries into final Compose logs, screenshots and archived input; verify the stage rejects and publishes no archive. Check a clean full run and the recorded archive-safety log. | Source patched; no run |
| **B02, High** | The integration suite now includes a disposable PostgreSQL TCP proxy that intercepts an actual receipt work INSERT and cuts its connection either before forwarding COMMIT or after PostgreSQL emits the COMMIT ReadyForQuery response but before the client sees it. Both paths assert no HTTP 202, independently query durable database state and retry the stable event key. Both cases are mandatory in each G8 integration TRX run. | `RealPostgresCommitTransportLossNeverAcknowledgesAndReplayResolves` for both wire cut points, on fresh and repeated databases, with observable 503 and expected receipt triples. | Source patched; no run |
| **B03, Medium** | G8 roots are single-use. Each run receives a random ID, unique archive directory and immutable run-specific manifest/tarball names. Reusing evidence roots is refused. The reconciliation script selects the same source SHA and run ID, and requires the final archive-safety proof. | Repeat the **same source SHA** on two separate E:-backed `/m8` verification roots. Verify distinct manifests and archives remain byte-identical to their own recorded hashes and do not overwrite one another. | Source patched; no run |
| **B04, Medium** | The recovery probe stores original raw accepted payload bytes, canonical bytes/digest, receipt timestamp, event JSON, work UUID, Pending state, attempt count and work timestamps in private E:-backed storage. After PostgreSQL recovery it compares all these fields and emits a safe source/run-locked PASS marker with before/after SHA-256 values, but no raw bytes or original canonical digest. The G8-S19 summarizer requires that marker. | Execute real PostgreSQL stop/restart and verify all private before/after fields match, and that `g8-restart-integrity.json` contains no raw data or digest. | Source patched; no run |
| **B05, Medium** | The finalizer writes separate `g8-technical-status.json` and `g8-formal-gate-status.json` with source SHA/run ID. Technical PASS requires 23 technical scenarios, clean mandatory stage exits and no cleanup failure. Formal acceptance remains pending. The external `archives/<run-id>/g8-finalization-status.json` also reports the sanitized archive outcome without rewriting the sealed run. Exit 1 means technical or archive finalization failure; exit 2 means technically clean, archive-safe evidence with governance pending. | Run a clean G8 technical exercise and a deliberately failed stage. Confirm exit code, separate status records and unchanged G8-S01/S25 raw blocked records; no automatic acceptance. | Source patched; no run |

## Implementation and safety boundaries

The new `TrackingCommitSocketProxy` exists **only** under `tests/HarborOps.IntegrationTests`; it does not run in the deployed application. The receipt store and API retain their prior commit-before-202 and final authorization logic.

The E:-backed single-use runner is `scripts/verify-m8-local.sh`. Each execution must use a fresh dedicated verification root ending in `/m8`. A repeat run on the same revision must use **a different parent directory** with a new `/m8` root. Only one G8 Compose project can run at a time because it uses isolated fixed ports. Keep GitHub Actions disabled.

To reconcile an independently reviewed run, supply a review record with the actual `runId`, `sourceSha`, `technicalStatus`, archived evidence hashes and concrete D-006 and independent review references. See [G8 review procedure](M8_G8_RECONCILIATION.md). The utility never changes the original raw scenario report or grants G8 acceptance.

## Remaining proof and decision requirements

- Clean .NET Release build and fresh/repeated PostgreSQL integration suites including both new COMMIT cuts.
- All 25 G8 scenarios reconciled, 11 mutation variants killed, three browser engines, advisory/image scans, final archive redaction and complete teardown.
- Independent review of exact run hashes, followed by explicit D-006 and G8 acceptance.

**No runtime PASS is claimed. Protected `main` remains the accepted G0-G7 baseline; no GitHub Actions or application deployment were performed.**
