# G17-S25 Final Independent Review Handoff Packet

**Recorded:** 2026-11-16
**Frozen source under review:** `7cc25dabc42b78debf6c9b200fd5209e45b0f56d` on branch `feat/harborops-g17-final-verification-60b4bf`
**Status:** **READY FOR INDEPENDENT S25 REVIEW. G17 NOT ACCEPTED; acceptance pending.**
**Superseded (2026-11-17):** use the [revision 2 packet](M17_G17_S25_FINAL_HANDOFF_PACKET_R2_20261117.md) for source `634053c4`. This packet is kept unchanged for source `7cc25dab`.
**Signatures:** none. No reviewer has signed S25-A01, S25-A02 or S25-A03. Nothing in this packet is an approval, and the contributor who implemented and tested these changes cannot supply the independent review.

This packet lets a separate reviewer inspect the real source and raw evidence without relying on summaries. Every figure can be re-derived from the artifacts named here.

## 1. What is being reviewed

- **Source:** the commit above. Documentation commits that follow it change no executable file; confirm with `git diff 7cc25dab..<docs head> --stat -- . ":(exclude)docs" ":(exclude)README.md"`, which must be empty.
- **Campaign:** local attempt **k4**, 49 of 49 stages, 72 of 72 technical witnesses, 3 independent assertions pending. [Full result](M17_G17_FINAL_SOURCE_LOCKED_RESULT_20261116.md).
- **Changes on top of the earlier review-branch source**, all listed with cause and evidence in the [correctness and test audit](M17_G17_FINAL_CORRECTNESS_AND_TEST_AUDIT_20261116.md): one product file (`src/HarborOps.Api/Health/DatabaseReadinessCheck.cs`), the diagnostic producer, a new hash-list verifier, three new test files, a preserved previous-producer fixture and the runner.
- **Not reviewed or changed:** accepted milestone branches, `main`, the M9 transport hold, M16 audit protections, tenant isolation, authentication, privacy rules, default-off observability and every approved threshold.

## 2. Evidence access (local, not published)

| Item | Where |
| --- | --- |
| Raw campaign evidence | `E:\HarborOps-Verification\g17-final\final-k4-evidence` and archive `final-k4-evidence-7cc25da.zip` (SHA-256 `3d5bc1964035e00e34b768f2edeb083853625b1377ec744bbd6e4abaaa2c658b`) |
| Manifest, ledger, run status | `manifest.json` `84695b01...`, `g17-technical-assertion-ledger.json` `bf431f2b...`, `run-status.json` `febcf923...` (full values in the result document) |
| Preserved failed attempts k1 to k3 | `final-k1-evidence`, `final-k2-evidence`, `final-k3-evidence` and the matching `-run.log` files |
| Mutation and negative-control outputs | `mutation-diag-hash-list-round1/2/3` files, `red-runner-order`, `repro-pwsh-red.log`, `readiness-red*.log`, `readiness-green.log` in the same folder |
| Real Start/Stop diagnostic fixture | `diag-real-fixture-1` in the same folder |
| Original r1c, r2 and r3 evidence | `docs/m17-s25-p09-controlled-protocol-20261115/results/` (committed, byte-identical to before this work) |
| Prior independent bundle and mutation evidence | `docs/m17-s25-mutation-evidence-20261115/` |

Reviewer should obtain the folder from the contributor through a private channel. It has not been pushed anywhere.

## 3. Reproducing the checks without trusting this packet

```bash
git checkout 7cc25dabc42b78debf6c9b200fd5209e45b0f56d
git status --porcelain                      # must be empty
node scripts/verify-g17-manifest.mjs "E:\HarborOps-Verification\g17-final\final-k4-evidence\manifest.json" 7cc25dabc42b78debf6c9b200fd5209e45b0f56d
node scripts/audit-g17-test-coverage.mjs --strict   # 25 scenarios, 75 clauses, 72 authored, 3 independent
node --test scripts/g17-diagnostic-hash-list.test.mjs scripts/g17-runner-order.test.mjs
node scripts/g17-diagnostic-hash-list.mjs "E:\HarborOps-Verification\g17-final\diag-real-fixture-1"
```

To rederive the ledger yourself, import `deriveWitnessChecks` and `checkPerAssertion` from `scripts/reconcile-g17-assertions.mjs` against a copy of the evidence folder (the script refuses to overwrite an existing ledger) and compare record by record with `g17-technical-assertion-ledger.json`. The contributor's own run of this comparison is recorded in `k4-independent-verification.json`; it is a convenience, not a substitute for yours.

## 4. Disposition of every requirement

Legend: **TECHNICAL PASS** means a source-locked executable witness passed in k4 and the ledger re-derives. It is not independently reviewed. **PENDING** means it needs a reviewer. No row is an acceptance.

| Requirement | Disposition at `7cc25dab` |
| --- | --- |
| G17-S01 to G17-S24, 72 atomic assertions | **TECHNICAL PASS, pending independent review** of the witness behavior (table in section 10) |
| G17-S25-A01 separate reviewer checks raw source, privacy, metrics and full cases | **PENDING** |
| G17-S25-A02 policy and procedural exceptions disclosed | **PENDING** (disclosures are in section 7; review and attestation are the reviewer's) |
| G17-S25-A03 reviewer issues explicit signed PASS or NOT PASS | **PENDING** |
| S25-F01 rendered rate, 5xx and p95 states (S18, S19) | **TECHNICAL PASS at k4**, pending review |
| S25-F02 page-originated correlated fault journey (S20) | **TECHNICAL PASS at k4**, pending review |
| S25-F03 participating-service log capture and canary control (S05, S13, S14) | **TECHNICAL PASS at k4**, pending review |
| S25-F04 hidden Playwright metadata records (S24) | **TECHNICAL PASS at k4** (manifest 54 of 54 verified), pending review |
| S25-F05 / P09 | **QUALIFIED PASS (policy decision recorded 2026-11-16).** k4: authorized +8.96%, baseline +3.95%, memory +13.97% against 10% and 15%; f6 FAIL and its unexplained burst preserved; optimization before G24 release readiness |
| S25-F06 procedural separation | **PENDING.** Requires a separate reviewer, or an explicitly recorded and approved independence exception. None is recorded |
| S25-F07 restart and cancellation witness (S15) | **TECHNICAL PASS at k4**, pending review |
| S25-F08 storage exhaustion with retained M8/M11/M15/M16 history (S22) | **TECHNICAL PASS at k4**, pending review |
| Diagnostic state hash producer | **CORRECTED and VERIFIED prospectively** (section 6). Historical r1c, r2 and r3 mismatch **preserved, not repaired** |
| Mutation survivors (11 classified non-material) and the 27 reconciled disputed records | Unchanged and still valid for the modules they covered. New and changed verifier code was separately tested (section 5) |
| New correctness finding F-FINAL-01 (readiness cancellation) | **FIXED, regression-tested, executed in k4**; pending review |
| Runner dependency ordering F-FINAL-03 | **FIXED, order-tested, k2 failure preserved**; pending review |
| `scripts/check-planning-docs.mjs` M11 assertion | **FAIL, pre-existing and unrelated** (section 7) |
| GitHub Actions at this source | **NOT RUN, not authorized.** Only local verification exists |
| Merge to `main`, deployment, release, G18 start | **NOT AUTHORIZED** |

## 5. Mutation and negative-control evidence

| Subject | Control | Result |
| --- | --- | --- |
| Verifier modules already covered | Strict exhaustive campaign of 805 candidates (earlier source) | 790 runtime kills, 4 load-time kills, 11 survivors classified non-material. Files changed since that campaign: only `g17-malformed-trace-evidence.mjs` and `check-g17-malformed-export.mjs` (Tempo window), already mutation-tested separately at 15 of 15 |
| New `g17-diagnostic-hash-list.mjs` | 22 single-fact mutants against its tests, three rounds | Round 1: 14 of 21 applicable mutants killed and 7 survived, because the tests asserted only "not ok". Tests were rewritten to pin each reason. Round 2 (rewritten tests): 21 of 21 applicable killed. Round 3 (final frozen files): 22 of 22 killed, control passes |
| Corrected diagnostic producer | Preserved previous producer run through the same verifier | Detected as exactly one state-file size and checksum mismatch |
| Readiness correction | Same tests against the unfixed code | 2 of 4 failed (the two cancellation cases), 2 controls passed. After the fix 4 of 4 passed |
| Runner ordering | Order test against the k2 runner text | 4 of 5 failed (the order and negative-control cases), then 5 of 5 pass on the fixed runner |
| Test harness | k1 reproduced through a pwsh-launched Node before the fix | 4 failures reproduced, then 0 |

## 6. Diagnostic checksum issue and the prospective fix

The independent review found that `diag-hashes.txt` in r1c, r2 and r3 disagrees with the final `diag-state.json` in each run, because the producer hashed the state and only then added `stopped_utc`. Recalculated from the committed files: r1c lists 332 bytes and the file is 373, r2 lists 330 and 371, r3 lists 330 and 371.

- **Preserved:** the three historical records are unchanged and a test pins their exact mismatch, so any later edit would be detected. They were not reconstructed.
- **Fixed:** `docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1` now writes the finalized state first and writes the hash list last without listing itself.
- **Verified:** by an independent Node verifier (`scripts/g17-diagnostic-hash-list.mjs`), by 12 regression tests including tampering, size, checksum and self-reference cases, by a preserved copy of the previous producer used as a negative control, and by a real Start then Stop run against a disposable project name on E: (`diag-real-fixture-1`, 8 files, verifier OK, sidecar removed).
- **Not claimed:** the new producer has not produced evidence for a P09 campaign, because none was run. Its first use will be the next controlled run, if one is ever authorized.

## 7. Procedural exceptions and limitations (for S25-A02)

1. **Four local attempts, three failed.** k1, k2 and k3 failed for the reasons in the result document; each is preserved. None changed a requirement.
2. **Implementer is also the verifier of this packet.** All local verification here was run by the contributor who made the corrections. That is why every row above says "pending independent review".
3. **Local only.** No hosted GitHub Actions run exists for `7cc25dab`. The previous hosted attempts failed at their own sources and are historical.
4. **Raw evidence is not in the repository.** It is large and unpublished by design; the manifest and hashes in the repository identify it exactly.
5. **A test name is imprecise.** `OwnFiveSecondLimitStillPublishesUnavailableWhenCallerWaits` ran 8 seconds, so the connection ended through the provider timeout of the test connection string and not through the check's own 5 second limit (audit observation O-2). The assertion it makes (no caller cancellation, so an unavailable sample is published) is correct. The name should be corrected with the next change that requires a rerun; renaming now would change the frozen source.
6. **Existing documentation check fails before and after this work.** `node scripts/check-planning-docs.mjs` fails its M11 assertion because it requires the exact phrase `G0-G12 and G16 are accepted` while `docs/STATUS.md` says `G0-G12 and M16/G16 are accepted and integrated`. It stops at that first assertion. Run in scratch copies that record every failing assertion instead of throwing (the repository script is unmodified), the check has four failing assertions at the review-branch start `9cbcc369` and the identical four at this work: the M11 phrase, `Roadmap contains stale or missing M8 status`, `Contributors contains stale or missing M8 status` and `M9 acceptance, evidence review, merge or backlog condition is missing`. This work therefore does not worsen it. All four are unrelated wording and status drift and were not remediated.
7. **P09 margins are thin** and the individual pair exceedances (authorized +25.71% in pair 1, baseline +15.76% in pair 2) are disclosed above, not waived.
8. **The sixth P09-bearing run is not a performance re-test.** No benchmark was repeated to obtain a result; k4 was the first attempt to reach that stage on this source.

## 8. Exactly what remains for S25

| ID | Needs | Cannot be supplied by |
| --- | --- | --- |
| **S25-A01** | A separate reviewer with access to the source commit and the raw evidence confirms the source, privacy boundaries, metrics, and that the full 72-case technical campaign is real and complete (check the table in section 10 against the raw files, spot-check negative controls) | the implementing contributor, this packet, the ledger alone, or a green run |
| **S25-A02** | The reviewer confirms that policy and procedural exceptions in section 7 and the P09 qualified decision are disclosed and acceptable, or records objections | the contributor |
| **S25-A03** | The reviewer issues an explicit signed PASS or NOT PASS recommendation before any G17 acceptance decision | the contributor, any automation, or this document |
| **S25-F06** | Either a genuinely separate signed review exists, or an independence exception is explicitly approved and recorded | this work |
| **G17 acceptance** | A distinct, recorded acceptance decision after S25 | this work |

Suggested reviewer procedure: (1) run section 3; (2) read the audit document's finding table and open the changed files; (3) walk section 10, opening the named witness and the raw artifact for each row, at least all rows of S05, S09, S13, S15, S20, S21 and S22, which carry the highest risk; (4) try one deliberate corruption of a copy of the evidence and confirm `verify-g17-manifest.mjs` rejects it; (5) record the PASS or NOT PASS outcome and any exceptions.

## 9. Boundaries that must stay true

M9 transport restrictions and the M16 audit protections are unchanged; the regression stage ran 34 accepted-milestone tests with M17 opted in against an unreachable collector and all passed. Telemetry stays default off. P09 numerical budgets are unchanged. Optimization is scheduled just before G24 release readiness unless a source-locked gate fails earlier, in which case the remedy is agreed first.

## 10. The 25 scenarios and 75 atomic assertions at `7cc25dab`

Derived by script from the recorded ledger, the coverage register and the assertion inventory, not edited by hand. Technical rows mean the named witness executed and passed in k4; none has been reviewed by anyone other than the contributor.

| Assertion | Required outcome | Witness (path :: case) | Raw artifact in the k4 evidence | Disposition |
| --- | --- | --- | --- | --- |
| **G17-S01** | **Source/contract conformity** | | | |
| G17-S01-A01 | all four work packages implemented | `scripts/g17-source-boundary.test.mjs` :: M17 four work packages exist with actual referenced production and verification boundaries | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S01-A02 | approved P01-P09 and exact instrument mapping match source | `scripts/g17-source-boundary.test.mjs` :: every approved M17 policy has a concrete source-side verification control | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S01-A03 | no unapproved default service, API or migration changes | `scripts/g17-source-boundary.test.mjs` :: source diff does not change the accepted default Compose or database migrations | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| **G17-S02** | **Metric schema and type** | | | |
| G17-S02-A01 | each metric has exact name/type/unit | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: EveryLocalInstrumentHasTheApprovedNameTypeAndUnit | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S02-A02 | no duplicate request counters | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: RequestCountIsDerivedOnlyFromDurationHistogramAndNotDuplicated | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S02-A03 | histogram produces nonzero latency samples | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: RealNonzeroRequestAndDatabaseDurationsReachMeterListeners | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S03** | **Existing health preservation** | | | |
| G17-S03-A01 | live stays responsive with DB down | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S03-A02 | ready goes unavailable on DB stop or absent seed within budget | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S03-A03 | existing JSON/status paths unchanged | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S04** | **Server-controlled correlation** | | | |
| G17-S04-A01 | response correlation generated on server | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S04-A02 | error and structured log scope use same value | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S04-A03 | client-supplied correlation cannot spoof it | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S05** | **Label allowlist** | | | |
| G17-S05-A01 | metric attributes contain only approved keys | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: CustomMetricLabelsRejectUntrustedHostsPathsAndBusinessIdentifiers | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S05-A02 | no user/tenant/request/SQL fields | `scripts/check-g17-export-privacy.mjs` :: owned-sanitized-service-logs | `m17-export-privacy-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S05-A03 | unknown route has stable fallback | `tests/HarborOps.IntegrationTests/M17TelemetryContractTests.cs` :: TenThousandAttackerChosenPathsUseOneBoundedOperationSeries | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S06** | **Cardinality and memory bound** | | | |
| G17-S06-A01 | 10,000 distinct routes/identifiers stay within approved active series cap | `tests/browser/m17/cardinality.spec.mjs` :: ten thousand hostile HTTP requests remain within complete exported-series budget | `m17-cardinality-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S06-A02 | overflow coalesces | `tests/HarborOps.IntegrationTests/M17TelemetryContractTests.cs` :: TenThousandAttackerChosenPathsUseOneBoundedOperationSeries | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S06-A03 | memory does not grow without bound | `scripts/check-g17-cardinality-memory.mjs` :: FOUR_TEN_THOUSAND_PATH_STRESS_WAVES | `m17-cardinality-memory-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S07** | **Valid context propagation** | | | |
| G17-S07-A01 | W3C valid trace context accepted without altering auth | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: ValidW3CTraceparentConnectsApiRequestAndBoundedDatabaseSpan | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S07-A02 | API -> safe DB work connected by trace | `tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs` :: HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S07-A03 | original audit correlation preserved | `tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs` :: OptedInTrackingAcceptancePreservesImmutableAuditCorrelation | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S08** | **Invalid context rejection** | | | |
| G17-S08-A01 | malformed traceparent/tracestate handled safely | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: MalformedTraceContextDoesNotTurnDependencyFailureIntoUnhandled500 | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S08-A02 | oversized baggage ignored | `scripts/check-g17-export-privacy.mjs` :: baggage:"secret=" | `m17-export-privacy-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S08-A03 | no raw header is exported, no 500 | `scripts/check-g17-malformed-export.mjs` :: malformed-context-export-safe | `m17-malformed-export-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S09** | **DB timing and outages** | | | |
| G17-S09-A01 | successful bounded DB operation emits duration/outcome | `tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs` :: HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S09-A02 | outage emits classified failure without secret SQL | `scripts/check-g17-database-fault.mjs` :: real-db-outage-keeps-live-and-errors-classified | `m17-database-fault-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S09-A03 | business error contract remains unchanged | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S10** | **Worker status and hold** | | | |
| G17-S10-A01 | worker that is intentionally disabled/held remains labelled disabled/held | `tests/browser/m17/dashboard.spec.mjs` :: authenticated samples match the disposable ready API and suppress held workers | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S10-A02 | enabled disposable worker emits correct attempts/outcomes | `tests/HarborOps.IntegrationTests/M17WorkerShellFunctionalTests.cs` :: ActualWorkerShellEmitsProcessedAndReconciliationDispositionsWithoutDuplicateEffects | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S10-A03 | no worker auto-start | `tests/HarborOps.IntegrationTests/M17WorkerShellFunctionalTests.cs` :: DefaultApiHostDoesNotRegisterTheHeldTrackingWorker | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S11** | **Asynchronous causal linking** | | | |
| G17-S11-A01 | authorized worker work links safely to source or documented correlation | `tests/HarborOps.IntegrationTests/M17RestrictedWorkerReaderTests.cs` :: RestrictedWorkerCanLinkOnlyOwnedReceiptAndCannotReadM16AuditOrModifyHistory | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S11-A02 | fabricated parent/tenant trace context rejected | `tests/HarborOps.IntegrationTests/M17CorrelationJourneyTests.cs` :: ForgedTenantAndW3CParentHeadersCannotControlDurableReceiptLink | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S11-A03 | replay/retry has unique bounded spans | `tests/HarborOps.IntegrationTests/M17ReplayRedriveSpanTests.cs` :: RealReplayAndAuthorizedRedriveEmitOneSpanPerWorkerAttemptNotPerHttpReplay | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S12** | **Bounded exporter failures** | | | |
| G17-S12-A01 | collector down does not change API response, transaction or readiness | `tests/HarborOps.IntegrationTests/M17ExporterOutageMutationTests.cs` :: UnreachableCollectorCannotChangeAuthorizedMutationOrConflictInvariants | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S12-A02 | export queue bounded/drops counted | `tests/HarborOps.IntegrationTests/M17ExporterQueuePressureTests.cs` :: QueueFullDropsAreCountedWithoutBlockingSpanProducers | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S12-A03 | exporter recoverable without manual data repair | `scripts/check-g17-collector-fault.mjs` :: collector-recovery-automatic | `m17-collector-fault-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S13** | **Security redaction** | | | |
| G17-S13-A01 | secrets, cookies, personal canaries and SQL values absent from logs, spans and metric labels | `scripts/check-g17-export-privacy.mjs` :: owned-sanitized-service-logs | `m17-export-privacy-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S13-A02 | sanitized errors still diagnose component and type | `scripts/check-g17-database-fault.mjs` :: database-alert-identifies-only-safe-component | `m17-database-fault-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S13-A03 | retained M16 audit is not exposed | `tests/HarborOps.IntegrationTests/M17RestrictedWorkerReaderTests.cs` :: RestrictedWorkerCanLinkOnlyOwnedReceiptAndCannotReadM16AuditOrModifyHistory | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S14** | **Tenant and authorization isolation** | | | |
| G17-S14-A01 | cross-organization request produces same denial as baseline | `tests/HarborOps.IntegrationTests/M17TenantIsolationTests.cs` :: OptedInTelemetryPreservesTenantDenialsAndAnalystWriteRevocation | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S14-A02 | telemetry does not disclose tenant detail | `scripts/check-g17-worker-journey.mjs` :: Prometheus exported private tenant/source/account identifiers | `m17-worker-journey-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S14-A03 | instrumentation cannot elevate a role | `tests/HarborOps.IntegrationTests/M17TenantIsolationTests.cs` :: OptedInTelemetryPreservesTenantDenialsAndAnalystWriteRevocation | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S15** | **Restart and cancellation** | | | |
| G17-S15-A01 | process restart and canceled request do not leak stale spans or requests | `scripts/check-g17-restart.mjs` :: actual_spans_inspected | `m17-restart-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S15-A02 | shutdown drain is bounded | `scripts/check-g17-restart.mjs` :: owned-api-shutdown-bounded | `m17-restart-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S15-A03 | no duplicated business effect or worker lease change | `scripts/check-g17-restart.mjs` :: restart-recovers-health-without-business-duplication | `m17-restart-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S16** | **Private stack exposure** | | | |
| G17-S16-A01 | default Compose contains no collector/dashboard | `scripts/check-m17-source.test.mjs` :: the default stack does not start or expose M17 components | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S16-A02 | optional dashboard bound loopback | `scripts/check-g17-network.mjs` :: dashboard_listener | `m17-network-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S16-A03 | collector not exposed externally, untrusted reads denied | `scripts/check-g17-network.mjs` :: Untrusted host client reached protected monitoring port | `m17-network-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S17** | **Alert triggers and clears** | | | |
| G17-S17-A01 | controlled 5xx/latency/readiness faults trigger configured alerts | `scripts/check-g17-alert-matrix.mjs` :: real-http-5xx-rate-fires-reference-alert | `m17-alert-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S17-A02 | threshold/window evidence exact | `monitoring/m17/alert-test.yaml` :: Readiness must not remain critical | `m17-promtool-test.txt` | TECHNICAL PASS (not independently reviewed) |
| G17-S17-A03 | clear/hysteresis works after recovery | `monitoring/m17/alert-test.yaml` :: Latency recovers below | `m17-promtool-test.txt` | TECHNICAL PASS (not independently reviewed) |
| **G17-S18** | **Low-traffic and disabled suppression** | | | |
| G17-S18-A01 | zero/low traffic is unknown/insufficient, not healthy | `tests/browser/m17/dashboard.spec.mjs` :: zero and insufficient traffic render unknown rather than a healthy zero | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S18-A02 | held workers cause no false failure alert | `scripts/check-g17-worker.mjs` :: fault-worker-stop-suppresses-stale-backlog | `m17-worker-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S18-A03 | cool-down deduplicates repeat alerts | `monitoring/m17/alert-test.yaml` :: S18-A03 repeat suppression | `m17-promtool-test.txt` | TECHNICAL PASS (not independently reviewed) |
| **G17-S19** | **Local dashboard rendering** | | | |
| G17-S19-A01 | critical panels show actual live measurement | `tests/browser/m17/dashboard.spec.mjs` :: critical monitoring panels use live series and no-data views are explicitly unknown | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S19-A02 | unknown/unavailable have explicit distinct states | `tests/browser/m17/dashboard.spec.mjs` :: rendered readiness distinguishes missing data from real downtime in three browsers | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S19-A03 | alert links only sanitized diagnostic evidence | `tests/browser/m17/dashboard.spec.mjs` :: alert dashboard links stay local and never embed private diagnostic parameters | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S20** | **End-to-end fault journey** | | | |
| G17-S20-A01 | browser/API fault yields server correlation | `scripts/check-g17-diagnostic-journey.mjs` :: page-originated-same-request | `m17-diagnostic-journey-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S20-A02 | trace to DB/worker or documented async bridge resolves | `scripts/check-g17-diagnostic-journey.mjs` :: real-trace-to-db | `m17-diagnostic-journey-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S20-A03 | dashboard alert names correct component without private payload | `scripts/check-g17-diagnostic-journey.mjs` :: same-fault-rendered-alert | `m17-diagnostic-journey-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S21** | **Performance overhead** | | | |
| G17-S21-A01 | paired workload measurement meets approved p95 and memory budgets | `scripts/check-g17-performance.mjs` :: measured_routes | `m17-performance-raw.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S21-A02 | exact environment/variance reported | `scripts/check-g17-performance.mjs` :: THREE_MATCHED_PAIRS | `m17-performance-raw.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S21-A03 | pressure does not block business | `scripts/check-g17-pressure-matrix.mjs` :: business-under-tenfold-pressure-and-exporter-outage | `m17-pressure-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S22** | **Storage and retention isolation** | | | |
| G17-S22-A01 | local telemetry capped and expires only its own disposable data | `scripts/check-g17-retention-matrix.mjs` :: m17-tempo-retention-results.json | `m17-retention-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S22-A02 | full store degrades observation safely | `scripts/check-g17-storage.mjs` :: full-telemetry-store | `m17-storage-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S22-A03 | M8/M11/M15/M16 retained data unchanged | `scripts/check-g17-storage.mjs` :: all-application-history-byte-identical | `m17-storage-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S23** | **Defaults and regression** | | | |
| G17-S23-A01 | M9/M10/M11/M13/M15 opt-in/hold semantics unchanged | `scripts/check-g17-regression-matrix.mjs` :: M13AdmissionControlAcceptanceTests | `g17-regression-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S23-A02 | accepted authorization/health/audit APIs preserved | `scripts/check-g17-regression-matrix.mjs` :: G15AuditAcceptanceTests | `g17-regression-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S23-A03 | changed source build, .NET regression and targeted browser suites pass | `scripts/verify-g17-manifest.mjs` :: Completed run has missing evidence or a failed stage | `run-status.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S24** | **Evidence integrity** | | | |
| G17-S24-A01 | every claimed assertion maps to real source-locked result | `scripts/reconcile-g17-assertions.test.mjs` :: rejects a missing assertion evidence row even when other suites pass | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S24-A02 | source hashes and manifests complete/no unlisted files | `scripts/check-g17-manifest.test.mjs` :: altered evidence cannot pass hash verification | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S24-A03 | negative control rejects tampering, skip and wrong SHA | `scripts/g17-adversarial-evidence.test.mjs` :: G17 verifier rejects tampering, skipped cases, duplicate results and wrong source SHA | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| **G17-S25** | **Independent review and gate recommendation** | | | |
| G17-S25-A01 | separate reviewer checks raw source, privacy, metrics and full cases | none: independent reviewer | none | PENDING independent S25 review |
| G17-S25-A02 | policy and procedural exceptions disclosed | none: independent reviewer | none | PENDING independent S25 review |
| G17-S25-A03 | reviewer issues explicit signed PASS/NOT PASS before G17 acceptance | none: independent reviewer | none | PENDING independent S25 review |

**G17 NOT ACCEPTED; acceptance pending.**
