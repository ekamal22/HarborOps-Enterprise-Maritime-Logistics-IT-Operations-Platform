# M8 Second Correctness Audit

**Audit date:** 2026-11-04  
**Audited implementation:** `ef32252916c42f7604db0ed5739f3313739982f9`  
**Branch:** `feat/m8-durable-event-inbox`, draft [PR #14](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/14)  
**Accepted baseline:** protected `main` at `47f030f9fa4b588d589f3ae082d03b3d5b94736a`  
**Review type:** Independent source inspection of the implementation and the first audit's remediation, not executable verification.  
**Disposition:** Correctness sign-off withheld. G8 not executed. D-006 acceptance pending. No source changes or accepted milestone merges performed in this audit.

## Overall conclusion

The M8 transactional receipt design remains a reasonable basis for the milestone. No confirmed new violation of commit-before-202, composite tenant/source replay isolation or final credential reauthorization was found by this source inspection. That statement is deliberately narrower than a runtime PASS: the .NET build, EF migrations, PostgreSQL interleavings, Compose runner, three browser engines, mutations and raw logs have not been executed in the available repository environment.

The first remediation introduced valuable tests and code corrections. It did not fully close the G8 evidence contract. Three new evidence-integrity weaknesses and two remaining verification coverage gaps are recorded below. None is accepted, verified or waived.

## Second-round findings

### M8-B01 | High | Packaged Compose logs bypass pre-package secret scanning

- **Source:** [G8 runner](../scripts/verify-m8-local.sh), lines approximately 72-103 and 299-304; [evidence checker](../scripts/check-m8-evidence-safety.mjs).
- **Confirmed control-flow defect:** The `evidence-safety` stage runs before the shell EXIT trap. The trap later appends `evidence/compose.log`, `cleanup.log`, `teardown.txt` and `ended-utc.txt`, then packages the entire evidence directory. These newly added files are never scanned by that earlier stage. In particular, any credential or request payload recorded in final Compose logs can be packaged without this specific redaction check inspecting it.
- **Additional gap:** The checker reads only selected textual extensions and does not inspect the final archive as an artifact. No actual credential disclosure has been observed because executable logs are unavailable.
- **Correction required:** Make collection, redaction and package scanning order explicit. Capture final logs before secret checks, run an archive-content check after packaging, use private credential canaries before deleting them, and fail closed without publishing an unsafe archive. Preserve the exact raw run and record independent sanitized supplements rather than overwriting it.
- **Gate:** G8-S21, G8-S25.

### M8-B02 | High | G8-S17 can be scored PASS without a real ambiguous database COMMIT

- **Source:** [fault-window test](../tests/HarborOps.IntegrationTests/TrackingInboxFailureWindowTests.cs), [commit hook](../src/HarborOps.Infrastructure/Integrations/TrackingReceiptCommitHook.cs), [G8 summary](../scripts/summarize-g8-evidence.mjs), [required matrix](M8_VERIFICATION_PLAN.md).
- **Confirmed mismatch:** `CommitUncertaintyNeverProducesSuccessAndReplayResolvesOutcome` injects a test exception either before `CommitAsync` or after it has completed. Those are useful deterministic boundary checks, but neither creates a database connection loss or uncertain acknowledgement **during** COMMIT. The corresponding G8-S17 requirement explicitly includes a COMMIT ambiguity and a database outage after insertion before commit. The summarizer currently treats the two hook variants and an initial-auth outage test as sufficient for S17.
- **Correction required:** Add a disposable PostgreSQL proxy/connection-disruption test that interrupts an actual commit round trip, preserving both possible database outcomes. Demonstrate no false 202 and same-key retry reconstruction for both, and require the additional raw evidence in S17's scoring. Distinguish fault-injection proof from real transport failure proof.
- **Gate:** G8-S17 and G8-S18.

### M8-B03 | Medium | A repeated run at the same source SHA can overwrite an archived run

- **Source:** [G8 runner](../scripts/verify-m8-local.sh), approximately lines 44-48 and 100-103; [review reconciliation](../scripts/reconcile-g8-review.mjs).
- **Confirmed control-flow defect:** The runner rotates a prior `evidence/` directory to `archives/g8-prior-...`, but writes final artifacts to fixed names `g8-<sourceSha>-manifest.sha256` and `g8-<sourceSha>-evidence.tar.gz`. Repeating verification on the same commit replaces those files without an exclusive-creation check, invalidating immutable review provenance for the earlier run.
- **Correction required:** Use a unique run identifier and immutable per-run archive/manifest names. Make the review explicitly select and bind the specific run identifier, start/end timestamps, source SHA and archive hashes; prohibit overwrites.
- **Gate:** G8-S25.

### M8-B04 | Medium | Restart test does not prove required byte and Pending-state continuity

- **Source:** [receipt restart integration test](../tests/HarborOps.IntegrationTests/TrackingInboxHttpTests.cs), `AcceptedReceiptSurvivesConnectionAndApiRestart`; [host recovery probe](../scripts/check-m8-ingress.mjs); [G8 summary](../scripts/summarize-g8-evidence.mjs).
- **Confirmed coverage gap:** The integration test checks replay's receipt UUID and the existence/count of event/work rows after a newly constructed application host. The host-level recovery script checks receipt UUID and received timestamp after database restart. Neither compares the stored raw accepted payload bytes and canonical digest before versus after PostgreSQL recovery, nor explicitly asserts that the same work row remains `Pending` with its original counters. G8-S19 requires all of these properties.
- **Correction required:** Snapshot the pre-restart row identities, canonical digest, raw bytes and initial work state, restart the disposable database and application path, then compare the exact persisted values and replay identity. Require this evidence for S19 PASS.
- **Gate:** G8-S19.

### M8-B05 | Medium | Technical run exit status is conflated with pending governance

- **Source:** [G8 summarizer](../scripts/summarize-g8-evidence.mjs), which marks S01/S25 blocked pending D-006 and independent review; [G8 runner](../scripts/verify-m8-local.sh), lines approximately 303-314.
- **Confirmed control-flow limitation:** The runner unconditionally exits 2 after its technical stages while D-006 or independent review remain pending. This correctly blocks formal G8 approval, but means an otherwise successful technical run is represented by a nonzero process status. It is easy to mistake an acceptance-pending outcome for an executable stage failure. The separate review script exists but does not change the original run's technical exit.
- **Correction required:** Preserve initial blocked S01/S25 results without changing history, but record a distinct machine-readable `technicalExecutionStatus` separate from `formalGateDisposition`. Make all true technical failures nonzero and pending governance visibly different. Do not auto-approve either gate.
- **Gate:** G8-S01, G8-S25.

## Source invariants reviewed without executable proof

- `TrackingInboxStore.ReceiveAsync` opens one read-committed transaction, serializes per-organization Pending admission, locks the organization, obtains final source/credential locks, rechecks bearer digest and credential validity **after** lock acquisition, and only returns `Received` after `CommitAsync` returns.
- Composite scoped FKs, unique event keys and appending-only history guards are present in the migration; populated-schema `Down()` raises a check violation before dropping durable tables.
- Duplicate canonical requests return original receipt UUID/time; distinct same-key requests record bounded first-collision metadata and return 409.
- Invalid `M8:PendingLimit` values are rejected during startup and the global initial-auth dependency handler adds `Retry-After` to tracking 503 responses.
- The previous audit's 11 mutation variants, 20-way concurrency, three lock-wait cases and 9,999/10,000/10,001 Pending case are authored. Their presence is not proof of a clean build or observed PostgreSQL behavior.

## Tactical decision: continue M8, but change the immediate work type

**Recommendation:** Retain M8/G8 as the primary critical-path workstream. Do not expand M8 scope, initiate M9 implementation or approve G8 now. First repair B01-B05, run a short local build/migration/test preflight, then execute G8 on isolated E:-backed resources, fix actual failures and complete D-006 plus independent review.

The [v3 execution graph](execution-graph.json) places G8 directly before M9, whose output enables M10, M11 and additional security/notification/dashboard/deployment dependencies. Delaying the M8 acceptance gate compounds the longest downstream event-processing path. By contrast, M12 shipment search/export can begin from accepted G5, M14 route mapping from G4, M16 audit logging, M17 observability and M19 recovery planning from G3. A separate non-conflicting M12 development lane provides the best near-term visible operational capability without weakening the G8 gate. Keep it on its own branch/Compose project/ports; do not merge M8 into protected `main` without explicit acceptance.

**Suggested allocation:** One source-locked E:-backed verifier for M8/G8 and one independent development lane for the M12 search contract and scoped search/export foundation. If only one development lane is available, finish M8 verification and decision first. M12 is preferable to adding more M8 features while verification is pending.

## Required action before another acceptance request

1. Resolve the evidence sequencing, real COMMIT ambiguity, archive immutability, full restart state proof and split technical/governance status findings.
2. Perform actual .NET 10 compilation and EF snapshot/catalog validation; run the existing G0-G7 regressions and new G8 tests twice against disposable PostgreSQL.
3. Run all 11 targeted mutants and all three Playwright browser engines; preserve and validate each source-specific evidence artifact on E:.
4. Obtain explicit D-006 decision and independent source/hash-bound evidence review. Request G8 acceptance only after the complete 25-case register is reconciled.

**Main remains unchanged. GitHub Actions must remain disabled. The initial remediation register is not retroactively considered verified.**
