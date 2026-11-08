# G12 Source-Locked Verification Plan

**Closure campaign update (2026-11-08):** Independent raw-evidence reviews of the first local package were performed ([G11](M11_G11_INDEPENDENT_EVIDENCE_REVIEW_20261108.md), [G12](M12_G12_INDEPENDENT_EVIDENCE_REVIEW_20261108.md)) and returned S25 NOT PASS. The [closure campaign](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md) then ran the complete 388-case integration suite with no skips, 291 unit and 46 contract tests, measured query plans, an isolated Nginx, Kestrel and PostgreSQL stack (G11 process and database faults 12/12, G12 real network path 32/32), 48/48 three-engine browser tests, 17/17 effective mutants killed and clean dependency, source and image scans, and fixed two defects (export serialization retry, explicit UTC designator). The [G11](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12](M12_G12_EVIDENCE_MATRIX_20261108.md) matrices carry the dispositions (G11 PASS 23, BLOCKED 2; G12 PASS 22, NOT APPLICABLE 1, BLOCKED 2). A new independent re-review of the closure package, the formal S01 policy decisions and the signed M9 hold reconciliation remain pending, so acceptance is pending for both gates. Protected `main`, the M9 hold and all deployment restrictions are unchanged.

**Final M11/M12 verification status (2026-11-08):** The [completed source-locked coverage record](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) confirms **341/341 integration tests passed** on `f56d017d9b2619be56fe533f73ba1d11f63974c0`, alongside 40/40 impacted, G11 19/19, G12 4/4, 36/36 browsers, five effective mutants, and security/SQL checks. The [G11/G12 evidence sufficiency review](M11_M12_ACCEPTANCE_REVIEW_20261108.md) documents remaining targeted local scenarios, unavailable raw artifacts and separately blocked S25 reviews. **Both gates: acceptance pending.** Protected `main` and the M9 hold remain unchanged. Older checkpoint text below is preserved as history.

**Executed verification reconciliation (2026-11-08):** [M11/M12 source-locked evidence](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) establishes 340/340 earlier full integration, 40/40 updated impacted regressions including populated 13-to-15 migration, G11 19/19, G12 4/4, 36/36 browser tests, 5/5 killed mutants, restricted SQL isolation and security scans on individually identified commits. The updated source gate passed 291/291 unit and 46/46 contract tests. Final full rerun `42443065884` is still in progress. Gate matrices, independent S25 reviews, and acceptance remain pending. Historical source-only statements below are not current test results.

**Current verification policy (2026-11-08):** [M11/M12 verification operations](M11_M12_VERIFICATION_OPERATIONS_20261108.md) now requires manual, coordinated, source-locked workflows instead of automatic push-triggered campaigns. [Run-by-run progress](M11_M12_VERIFICATION_PROGRESS_20261108.md) is the current verification checkpoint; older reports and source-only notes below are historical. G11/G12 independent S25 review and acceptance remain pending. Protected `main`, the M9 transport hold, and deployment restrictions are unchanged.

**Verification reconciliation, 2026-11-08:** The [source-locked M11/M12 evidence register](M11_M12_VERIFICATION_PROGRESS_20261108.md) records 338/338 passing full integration tests on the previously tested source, 291/291 unit, 46/46 contract, 19/19 G11, 4/4 G12, 36/36 three-engine browsers, five effective mutants killed and runtime-image vulnerability checks. A newer expanded full integration run and populated 13-to-15 upgrade test were still in progress at this checkpoint. Older 18/18, 36/36 and source-only statements below are historical. [M12-P01..P16 local contract](M12_LOCAL_CONTRACT_DECISION_20261108.md) is selected for verification, not independent gate acceptance; [Q2 applicability](M12_Q2_HISTORICAL_SNAPSHOT_APPLICABILITY_20261108.md) is recorded separately. Independent G11/G12 evidence reviews and gate acceptance pending, with protected `main` and the M9 hold unchanged.

**Historical hosted verification checkpoint:** [Executed M11/M12 run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records a successful .NET and frontend source gate, G11 18/18 scoped PostgreSQL tests, G12 2/2 exclusive PostgreSQL tests, 36/36 impacted integration regressions, and direct SQL isolation of exception, history and evaluation tables with forged-scope denial. Updated source/dependency and runtime-image security scans passed. The three-engine browser rerun passed 36/36 tests, with 12/12 each in Chromium, Firefox and WebKit, including M11 exception journeys, M12 search/export and targeted M5 regressions. G11-S08 conflict-specific provenance remains open; M12 local policy decisions and independent G11/G12 S25 reviews remain pending. No G11/G12 acceptance, merge or deployment is authorized. Historical source-only notes below are retained as previous checkpoints.

**Historical combined-source checkpoint (before hosted verification):** M11 and M12 are assembled on `feat/m11-m12-combined-integration`; shared API, role provisioning, React and migration-count surfaces have been reconciled, and M11 lifecycle code and tests updated under the [recorded M11 policies](M11_LOCAL_CONTRACT_DECISION_20261108.md). Earlier descriptions of missing combined integration or pending M11 policy choices are superseded. G11/G12 executable security and browser evidence does not yet exist, M12 local policies are still pending, and no gate acceptance, protected-main merge, deployment or M9 hold release is authorized.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). Run future G12-S01..S25 against the same frozen combined SHA as G11 while keeping separate scenario outcomes. Verify 13-to-15 migration integration and M12's independent proof key. The login-changing restricted SQL test requires a dedicated disposable PostgreSQL server and HARBOROPS_G12_EXCLUSIVE_POSTGRES=1; otherwise record SKIPPED, not PASS. Share browser and regression fixtures only when G12 predicates are executed.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records M11 draft PR #31 and M12 draft PR #32 with different additive migration IDs, shared API/UI/role integration surfaces and no combined execution. G11-S01 is BLOCKED, G11-S02..S25 NOT EXECUTED and G12 verification pending. Accepted `main` and the M9 transport hold remain unchanged.

**State:** NOT EXECUTED. G12 acceptance pending. Applies only to final frozen M12 source SHA. G5 accepted; G11 is not an M12 prerequisite.

| ID | Required scenario | Expected proof |
| --- | --- | --- |
| S01 | Explicit M12-P01..P16 decision | Recorded policy disposition |
| S02 | Build/typecheck/contracts | .NET and web outputs, zero warnings |
| S03 | M5 identifier and status compatibility | Case, null, archive and workflow fixtures |
| S04 | M6 voyage assignment | Current, closed, future, foreign and duplicate |
| S05 | Origin/destination port filters | Matching and wrong-tenant port UUIDs |
| S06 | UTC windows | Equal, just before/after, null and offsets |
| S07 | Invalid and combined filters | Repeated, unknown, malformed, encoded query >4096, AND semantics |
| S08 | Cursor integrity and UUID paging | Replay, expiry, tamper, tenant/filter/user/session swaps |
| S09 | Concurrent writes and live pages | No incorrect snapshot claim |
| S10 | Search-to-M5 detail | Fresh authorization and archive semantics |
| S11 | Five-role search/export matrix | Analyst/Manager export, Auditor search only, Administrator/Support denied with real restricted SQL login |
| S12 | Tenant boundary in SQL and HTTP | Cross-org UUID/join/export denial |
| S13 | Revocation, session and org switch | In-flight and post-revocation behavior |
| S14 | Search and export parity | Identical stable fixture/filter subset |
| S15 | CSV injection and encoding | Formula, quotes, comma, newline, Unicode |
| S16 | Safe fields and response headers | Fixed 12-column schema, no-store |
| S17 | Row/byte/timeout limits | 5000, 5001, 10 MiB, 15-second statement timeout, 25-second internal deadline and 30-second proxy read timeout, no partial CSV |
| S18 | M16 signed export audit and restricted role | Nine-field HMAC substitution, expiry, key missing/mismatch/rotation, unreadable verifier table, replay, SQL forgery, audit outage, M12 downgrade refusal after audited exports |
| S19 | Outage/cancel/rate/concurrency | Real slow DB, 25-second search/export budget, 30-second proxy envelope, browser abort, safe no-partial-output failure |
| S20 | PostgreSQL plans | EXPLAIN actual cost for representative data |
| S21 | Q2 historical snapshot | Disposable clone test or evidenced NOT APPLICABLE |
| S22 | Three-engine browser | M12 search/export/role plus accepted M5 delayed search response across organization switch and in-flight draft background refresh, in Chromium/Firefox/WebKit |
| S23 | Accepted source regressions | M2/M5/M6/M10/M16, M9 hold |
| S24 | Effective security mutants and scans | Intended assertions kill defects, no unresolved high/critical |
| S25 | Independent raw-evidence review | SHA, artifacts, hashes, failure logs and residuals |

## Efficient tiers

**Tier 1:** On the frozen candidate, `dotnet build HarborOps.sln --configuration Release`; targeted unit/contract suites; `npm --prefix web run build`; `node scripts/render-execution-graph.mjs --check`; `node scripts/check-planning-docs.mjs`. Stop on source defects before heavy tests.

**Tier 2:** Real scoped HTTP/PostgreSQL `ShipmentSearchHttpTests`, restricted API DB role, migration 14 on fresh and copied populated source, signer and key provisioning, audit function grants, replay index, missing/mismatched/rotated key, cross-role/revocation/SQL, failure atomicity, row and byte limits. The restricted-role test reprovisions global PostgreSQL logins, so it must run on an exclusive disposable PostgreSQL **server**, not merely an isolated database. Set `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1` only after verifying server isolation. Without the flag, the test reports SKIPPED and is not G12 PASS. The test collection disables same-assembly parallel execution; also prevent concurrency across other assemblies, runners and external processes.

**Tier 3:** One final source-locked G12 campaign, required relevant regressions, browser tests in Chromium/Firefox/WebKit using `HARBOROPS_M12_FIXTURES=1`, applicable security/mutation/image scans, measured query plans and Q2 historical check. Parallelize non-conflicting static checks only; isolate browser ports/profiles and mutable DB state.

Evidence on E: must include exact SHA, clean/dirty status, branch/parent/migration hash, config digest without secrets, commands, versions, exit codes, timing, TRX/JUnit, SQL/HTTP and browser captures, mutation effective-kill reasons, scan outputs, failures and SHA-256 manifest. Mark each S01..S25 PASS, FAIL, BLOCKED or NOT EXECUTED based only on executed artifacts. An independent reviewer reconciles S25; explicit G12 gate acceptance remains separate. Never run GitHub Actions or alter the only retained volume.

## Adversarial checks

Remove tenant predicates, the M6 association scope, cursor org/fingerprint binding, live role validation, export limit, CSV formula defense, export audit or failure rollback in disposable mutations and prove targeted assertions fail for their intended reason. Test API-login forged audit arguments and unauthorized audit-write SQL. No test execution has been claimed. See [source correctness audit](M12_CORRECTNESS_AUDIT_20261107.md) for the required evidence distinctions.

## Second-pass additions

The [2026-11-08 source audit](M12_CORRECTNESS_AUDIT_20261108.md) added tests for a full five-role HTTP matrix, nine forged/modified proof fields, oversized URLs, and non-destructive EF migration rollback/privilege-operation assertions, plus source guards for audit rollback and database/total export deadlines. G12 must validate these in an exclusive real PostgreSQL and HTTP campaign. Do not infer any new PASS from the source additions.

## Third-pass M5 browser compatibility checks

The [third-pass source findings](M12_CORRECTNESS_AUDIT_20261108.md#third-pass-m5-browser-regression-findings-2026-11-08) show that the M12 shipment workspace reads `/shipments/search`, whereas two accepted M5 browser tests still intercepted the original list URI. The `shipment.spec.mjs` deferred-response test and `shipment-draft.spec.mjs` unsaved-draft background refresh test were updated to intercept the route actually used by M12, without weakening their assertions. G12-S22 must execute both tests with `shipment-search.spec.mjs` against actual M5/M12 fixtures across all three engines. Their source correction does not constitute a passing browser result.

## Fourth-pass execution prerequisites

The [fourth-pass correctness audit](M12_CORRECTNESS_AUDIT_20261108_FOURTH_PASS.md) records the parameterized EF Core `ExecuteSqlAsync` change and 25-second total search/export budgets. Source inspection established both Nginx configurations use a 30-second API read timeout, but did not measure actual cancellation or HTTP response timing. S02 must compile the API with warnings-as-errors, and S17/S19 must exercise slow SQL, authentication middleware delay, store cancellation, timeout response, and the proxy path with actual timing and no partial CSV. S25 remains unexecuted.

## Fifth-pass isolation and cancellation coverage

The [fifth-pass source audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md) requires the restricted-role integration test to report SKIPPED unless `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1` is explicitly set for a dedicated disposable local PostgreSQL server. G12-S11/S18 must not be marked PASS if that test was skipped, even if the remainder of the integration suite succeeded. Capture the exact skip/run result, role grants, server identity and process isolation evidence. The CSV preparation path now checks cancellation during each row and immediately before/after UTF-8 encoding. G12-S17/S19 still require actual CPU-delay and database-delay observations, abort tests and a no-partial-CSV assertion. Do not assume the 25-second internal budget covers authentication middleware or network delivery.
