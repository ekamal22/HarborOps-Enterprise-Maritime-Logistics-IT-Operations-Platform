# G17-S25 Local Source-Locked Verification Result (attempt f7)

**Recorded:** 2026-11-14. **Gate:** G17 NOT ACCEPTED. Independent G17-S25 A01, A02 and A03 remain pending. No GitHub Actions workflow was dispatched, rerun or otherwise used for any part of this work, and nothing was pushed.

**Tested source:** `5b1dcc41d5b19dcbacd66de38193dc5020964647` on local branch `feat/harborops-g17-s25-audit-6439ae`, eight commits above the review branch head `7b3d3e87a62c03ba6a7ce7d8d92b93bf7c3c78a7`. Documentation commits made after the run change no executable source. The protected main baseline `b490fb8cfc879cdf05cb1f7484b99f6a9b264ed3` is unchanged.

**Method:** the complete campaign ran locally through `scripts/run-g17-local.ps1` (PowerShell 7) with `-StartDisposableStack -FaultProbe -VerifyDisposableWorker -RequireFullG17 -StopOwnedContainers`, project `ho-verify-m17-audit-f7`, on a clean worktree with disposable credentials generated locally for this run. The machine was otherwise idle during the run. Started 2026-11-14T17:36:35Z, finished 18:14:25Z.

## Result

| Item | Value |
| --- | --- |
| Run state | COMPLETE, failure type none |
| Stages | 49 of 49 PASS, exit code 0 each |
| .NET | unit 319/319, contract 60/60, M17 integration 55/55, accepted-milestone regressions 34/34, no skipped or not-executed case |
| Node source and negative controls | 222 cases, 0 skipped, 0 failed |
| Playwright | dashboard 24/24 (3 engines), cardinality 1/1, 0 skipped, 0 flaky, 0 retried; both hidden `.last-run.json` records present and `passed` |
| Technical ledger | 72/72 TECHNICAL PASS, 3 independent S25 clauses PENDING, 0 errors |
| Manifest | 54 files indexed and hash-verified, 0 missing expected artifacts, 0 binary files, `gate_acceptance` NOT_ACCEPTED, 0 assertions reviewed |
| Evidence directory | `E:\HarborOps-Verification\g17-s25-audit\final-f7-evidence` (55 files including `manifest.json`) |
| Manifest SHA-256 | `46df381bfd026babba3ceca34eb0b1ba6539996e50c5aa6d46de6cc277653bf1` |
| Archive | `final-f7-evidence-5b1dcc4.zip`, SHA-256 `77c6b26f5c12337f32de2ed606b1a94f91e41971441779ed943a6825b668df34` |

Independent verification after the run: `node scripts/verify-g17-manifest.mjs <manifest> 5b1dcc41d5b19dcbacd66de38193dc5020964647` printed a complete, source-matching index.

## P09 overhead, disclosed in full

The approved methodology and budgets were not changed: pooled p95 over three matched pairs (10 percent budget) and mean paired peak memory (15 percent budget).

| Measure | f7 |
| --- | --- |
| Pooled p95, baseline route | -1.9 percent |
| Pooled p95, authorized route | **+9.3 percent** (0.7 point of margin) |
| Mean paired peak memory | **+13.79 percent** (1.2 points of margin) |
| Pair 1 | baseline -7.67, authorized +5.23, memory +13.19 |
| Pair 2 | baseline -12.34, authorized +7.96, memory +13.60 |
| Pair 3 | baseline **+17.54**, authorized **+11.10**, memory +14.59 |

**Individual outliers:** pair 3 exceeds the individual 10 percent p95 figure on both routes (baseline +17.54, authorized +11.10). The approved evaluation is the pooled rule, under which the gate passes. The historical third-pair outlier of +22.69 percent in the earlier local campaign is preserved in its own record and is not rewritten. The measurement is noisy and the margins are thin: attempt f6 on the same source failed this same gate (below).

## Every attempt is disclosed

Seven full-campaign attempts were made. Six failed; all failed evidence is preserved unmodified under `E:\HarborOps-Verification\g17-s25-audit\final-f<N>-evidence`. A later pass does not erase an earlier failure.

| Attempt | Source | Stages passed | Failed stage and cause | Disposition |
| --- | --- | --- | --- | --- |
| f1 | `c954edec` | 26 | New low-traffic stage: fewer than four below-threshold points on a fresh stack | Stage now waits for six recorded points (`90aac1c2`) |
| f2 | `90aac1c2` | 21 | Compose up: fixed subnets still held by the stopped f1 stack | Operational. The runner only stops containers; each failed project is now removed with `compose down` before relaunch. No source change |
| f3 | `90aac1c2` | 29 | PostgreSQL outage stage: page-originated injection exceeded 110 s with the database stopped | Bound raised to 420 s (`8366673b`) |
| f4 | `8366673b` | 29 | Same stage: all six steps passed, but the success condition still required five | Now requires the six ordered step names (`8a84ac05`) |
| f5 | `8a84ac05` | 36 | Privacy stage demanded log lines from a legitimately quiet collector | Running and scanned required for every owned service; lines required only from services that always log (`5b1dcc41`) |
| f6 | `5b1dcc41` | 44 | **P09 gate: authorized pooled p95 +12.7 percent** (round 1 telemetry-on outlier 105 ms against about 50 ms; baseline +3.6, memory +5.9) | Not tuned. Budgets and method unchanged. The owner chose a new disclosed attempt on the same source |
| f7 | `5b1dcc41` | 49 | none | COMPLETE |

f7 reran the identical source as f6. The performance outcome therefore varies between runs of the same code, which is stated here as a qualification and not as proof that the overhead is within budget with comfortable margin.

## What the new witnesses measured

- **F01:** a recorded real below-threshold window (32 genuine reads, six recorded points, eligible 18.2 to 31.7 against the 100 floor, raw 5xx rate a measured 0, guarded series absent) rendered Unknown for the 5xx and p95 panels and a measured request rate in Chromium, Firefox and WebKit.
- **F02:** one page-originated 503 set joined server correlation, trace, sanitized log and a firing `HarborOpsDatabaseUnavailable` row in the exact alert panel after scrolling it into view.
- **F03:** 40 retained traces, 149 metric series, 3,490,895 log bytes across all eight owned services (api 680, tempo 1545, prometheus 21, grafana 2378, postgres 28, dashboard gateway 7, web 27 lines; the collector produced 0 lines and was still required to be running and scanned). Worker journey: 10 retained worker-service traces and worker metric series.
- **F04:** both `.last-run.json` records present, indexed and verified.
- **F07:** a seeded active lease was identical (state, token, epoch, attempts, expiry, update time) before stop, during stop and after restart; forged token and epoch rejected; original holder finished once; duplicate finish rejected; no duplicate effect. 140 canceled requests, 60 of them in flight against a held table lock; 6 of those were found in Tempo, all starting before shutdown and none exported as a server error.
- **F08:** owned ENOSPC on the Prometheus store; the store logged the error, the newest sample went stale (47.6 s against a 4.2 s healthy control), the readiness expression turned Unknown, business endpoints stayed healthy, observation recovered, and row-level hashes of the seeded M8, M11, M15 and M16 records were equal before and after together with the whole-schema data hash.

## Assertion-by-assertion reconciliation (75)

All 72 technical assertions are TECHNICAL PASS against the raw artifact shown. The three S25 clauses remain pending independent review. A technical pass here means the named witness executed and its raw evidence passed the source-locked verifiers. It is not a reviewer disposition.

| Assertion | Expected | Witness (path :: case) | Raw artifact | Result |
| --- | --- | --- | --- | --- |
| G17-S01-A01 | all four work packages implemented | `scripts/g17-source-boundary.test.mjs` :: M17 four work packages exist with actual referenced production and verification boundaries | `g17-node-tests.xml` | TECHNICAL PASS |
| G17-S01-A02 | approved P01-P09 and exact instrument mapping match source | `scripts/g17-source-boundary.test.mjs` :: every approved M17 policy has a concrete source-side verification control | `g17-node-tests.xml` | TECHNICAL PASS |
| G17-S01-A03 | no unapproved default service, API or migration changes | `scripts/g17-source-boundary.test.mjs` :: source diff does not change the accepted default Compose or database migrations | `g17-node-tests.xml` | TECHNICAL PASS |
| G17-S02-A01 | each metric has exact name/type/unit | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: EveryLocalInstrumentHasTheApprovedNameTypeAndUnit | `g17-integration.trx` | TECHNICAL PASS |
| G17-S02-A02 | no duplicate request counters | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: RequestCountIsDerivedOnlyFromDurationHistogramAndNotDuplicated | `g17-integration.trx` | TECHNICAL PASS |
| G17-S02-A03 | histogram produces nonzero latency samples | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: RealNonzeroRequestAndDatabaseDurationsReachMeterListeners | `g17-integration.trx` | TECHNICAL PASS |
| G17-S03-A01 | live stays responsive with DB down | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS |
| G17-S03-A02 | ready goes unavailable on DB stop or absent seed within budget | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS |
| G17-S03-A03 | existing JSON/status paths unchanged | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS |
| G17-S04-A01 | response correlation generated on server | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope | `g17-contract.trx` | TECHNICAL PASS |
| G17-S04-A02 | error and structured log scope use same value | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope | `g17-contract.trx` | TECHNICAL PASS |
| G17-S04-A03 | client-supplied correlation cannot spoof it | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope | `g17-contract.trx` | TECHNICAL PASS |
| G17-S05-A01 | metric attributes contain only approved keys | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: CustomMetricLabelsRejectUntrustedHostsPathsAndBusinessIdentifiers | `g17-integration.trx` | TECHNICAL PASS |
| G17-S05-A02 | no user/tenant/request/SQL fields | `scripts/check-g17-export-privacy.mjs` :: owned-sanitized-service-logs | `m17-export-privacy-results.json` | TECHNICAL PASS |
| G17-S05-A03 | unknown route has stable fallback | `tests/HarborOps.IntegrationTests/M17TelemetryContractTests.cs` :: TenThousandAttackerChosenPathsUseOneBoundedOperationSeries | `g17-integration.trx` | TECHNICAL PASS |
| G17-S06-A01 | 10,000 distinct routes/identifiers stay within approved active series cap | `tests/browser/m17/cardinality.spec.mjs` :: ten thousand hostile HTTP requests remain within complete exported-series budget | `m17-cardinality-results.json` | TECHNICAL PASS |
| G17-S06-A02 | overflow coalesces | `tests/HarborOps.IntegrationTests/M17TelemetryContractTests.cs` :: TenThousandAttackerChosenPathsUseOneBoundedOperationSeries | `g17-integration.trx` | TECHNICAL PASS |
| G17-S06-A03 | memory does not grow without bound | `scripts/check-g17-cardinality-memory.mjs` :: FOUR_TEN_THOUSAND_PATH_STRESS_WAVES | `m17-cardinality-memory-results.json` | TECHNICAL PASS |
| G17-S07-A01 | W3C valid trace context accepted without altering auth | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: ValidW3CTraceparentConnectsApiRequestAndBoundedDatabaseSpan | `g17-contract.trx` | TECHNICAL PASS |
| G17-S07-A02 | API -> safe DB work connected by trace | `tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs` :: HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan | `g17-integration.trx` | TECHNICAL PASS |
| G17-S07-A03 | original audit correlation preserved | `tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs` :: OptedInTrackingAcceptancePreservesImmutableAuditCorrelation | `g17-integration.trx` | TECHNICAL PASS |
| G17-S08-A01 | malformed traceparent/tracestate handled safely | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: MalformedTraceContextDoesNotTurnDependencyFailureIntoUnhandled500 | `g17-contract.trx` | TECHNICAL PASS |
| G17-S08-A02 | oversized baggage ignored | `scripts/check-g17-export-privacy.mjs` :: baggage:"secret=" | `m17-export-privacy-results.json` | TECHNICAL PASS |
| G17-S08-A03 | no raw header is exported, no 500 | `scripts/check-g17-malformed-export.mjs` :: malformed-context-export-safe | `m17-malformed-export-results.json` | TECHNICAL PASS |
| G17-S09-A01 | successful bounded DB operation emits duration/outcome | `tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs` :: HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan | `g17-integration.trx` | TECHNICAL PASS |
| G17-S09-A02 | outage emits classified failure without secret SQL | `scripts/check-g17-database-fault.mjs` :: real-db-outage-keeps-live-and-errors-classified | `m17-database-fault-results.json` | TECHNICAL PASS |
| G17-S09-A03 | business error contract remains unchanged | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS |
| G17-S10-A01 | worker that is intentionally disabled/held remains labelled disabled/held | `tests/browser/m17/dashboard.spec.mjs` :: authenticated samples match the disposable ready API and suppress held workers | `m17-browser-results.json` | TECHNICAL PASS |
| G17-S10-A02 | enabled disposable worker emits correct attempts/outcomes | `tests/HarborOps.IntegrationTests/M17WorkerShellFunctionalTests.cs` :: ActualWorkerShellEmitsProcessedAndReconciliationDispositionsWithoutDuplicateEffects | `g17-integration.trx` | TECHNICAL PASS |
| G17-S10-A03 | no worker auto-start | `tests/HarborOps.IntegrationTests/M17WorkerShellFunctionalTests.cs` :: DefaultApiHostDoesNotRegisterTheHeldTrackingWorker | `g17-integration.trx` | TECHNICAL PASS |
| G17-S11-A01 | authorized worker work links safely to source or documented correlation | `tests/HarborOps.IntegrationTests/M17RestrictedWorkerReaderTests.cs` :: RestrictedWorkerCanLinkOnlyOwnedReceiptAndCannotReadM16AuditOrModifyHistory | `g17-integration.trx` | TECHNICAL PASS |
| G17-S11-A02 | fabricated parent/tenant trace context rejected | `tests/HarborOps.IntegrationTests/M17CorrelationJourneyTests.cs` :: ForgedTenantAndW3CParentHeadersCannotControlDurableReceiptLink | `g17-integration.trx` | TECHNICAL PASS |
| G17-S11-A03 | replay/retry has unique bounded spans | `tests/HarborOps.IntegrationTests/M17ReplayRedriveSpanTests.cs` :: RealReplayAndAuthorizedRedriveEmitOneSpanPerWorkerAttemptNotPerHttpReplay | `g17-integration.trx` | TECHNICAL PASS |
| G17-S12-A01 | collector down does not change API response, transaction or readiness | `tests/HarborOps.IntegrationTests/M17ExporterOutageMutationTests.cs` :: UnreachableCollectorCannotChangeAuthorizedMutationOrConflictInvariants | `g17-integration.trx` | TECHNICAL PASS |
| G17-S12-A02 | export queue bounded/drops counted | `tests/HarborOps.IntegrationTests/M17ExporterQueuePressureTests.cs` :: QueueFullDropsAreCountedWithoutBlockingSpanProducers | `g17-integration.trx` | TECHNICAL PASS |
| G17-S12-A03 | exporter recoverable without manual data repair | `scripts/check-g17-collector-fault.mjs` :: collector-recovery-automatic | `m17-collector-fault-results.json` | TECHNICAL PASS |
| G17-S13-A01 | secrets, cookies, personal canaries and SQL values absent from logs, spans and metric labels | `scripts/check-g17-export-privacy.mjs` :: owned-sanitized-service-logs | `m17-export-privacy-results.json` | TECHNICAL PASS |
| G17-S13-A02 | sanitized errors still diagnose component and type | `scripts/check-g17-database-fault.mjs` :: database-alert-identifies-only-safe-component | `m17-database-fault-results.json` | TECHNICAL PASS |
| G17-S13-A03 | retained M16 audit is not exposed | `tests/HarborOps.IntegrationTests/M17RestrictedWorkerReaderTests.cs` :: RestrictedWorkerCanLinkOnlyOwnedReceiptAndCannotReadM16AuditOrModifyHistory | `g17-integration.trx` | TECHNICAL PASS |
| G17-S14-A01 | cross-organization request produces same denial as baseline | `tests/HarborOps.IntegrationTests/M17TenantIsolationTests.cs` :: OptedInTelemetryPreservesTenantDenialsAndAnalystWriteRevocation | `g17-integration.trx` | TECHNICAL PASS |
| G17-S14-A02 | telemetry does not disclose tenant detail | `scripts/check-g17-worker-journey.mjs` :: Prometheus exported private tenant/source/account identifiers | `m17-worker-journey-results.json` | TECHNICAL PASS |
| G17-S14-A03 | instrumentation cannot elevate a role | `tests/HarborOps.IntegrationTests/M17TenantIsolationTests.cs` :: OptedInTelemetryPreservesTenantDenialsAndAnalystWriteRevocation | `g17-integration.trx` | TECHNICAL PASS |
| G17-S15-A01 | process restart and canceled request do not leak stale spans or requests | `scripts/check-g17-restart.mjs` :: actual_spans_inspected | `m17-restart-results.json` | TECHNICAL PASS |
| G17-S15-A02 | shutdown drain is bounded | `scripts/check-g17-restart.mjs` :: owned-api-shutdown-bounded | `m17-restart-results.json` | TECHNICAL PASS |
| G17-S15-A03 | no duplicated business effect or worker lease change | `scripts/check-g17-restart.mjs` :: restart-recovers-health-without-business-duplication | `m17-restart-results.json` | TECHNICAL PASS |
| G17-S16-A01 | default Compose contains no collector/dashboard | `scripts/check-m17-source.test.mjs` :: the default stack does not start or expose M17 components | `g17-node-tests.xml` | TECHNICAL PASS |
| G17-S16-A02 | optional dashboard bound loopback | `scripts/check-g17-network.mjs` :: dashboard_listener | `m17-network-results.json` | TECHNICAL PASS |
| G17-S16-A03 | collector not exposed externally, untrusted reads denied | `scripts/check-g17-network.mjs` :: Untrusted host client reached protected monitoring port | `m17-network-results.json` | TECHNICAL PASS |
| G17-S17-A01 | controlled 5xx/latency/readiness faults trigger configured alerts | `scripts/check-g17-alert-matrix.mjs` :: real-http-5xx-rate-fires-reference-alert | `m17-alert-matrix-results.json` | TECHNICAL PASS |
| G17-S17-A02 | threshold/window evidence exact | `monitoring/m17/alert-test.yaml` :: Readiness must not remain critical | `m17-promtool-test.txt` | TECHNICAL PASS |
| G17-S17-A03 | clear/hysteresis works after recovery | `monitoring/m17/alert-test.yaml` :: Latency recovers below | `m17-promtool-test.txt` | TECHNICAL PASS |
| G17-S18-A01 | zero/low traffic is unknown/insufficient, not healthy | `tests/browser/m17/dashboard.spec.mjs` :: zero and insufficient traffic render unknown rather than a healthy zero | `m17-browser-results.json` | TECHNICAL PASS |
| G17-S18-A02 | held workers cause no false failure alert | `scripts/check-g17-worker.mjs` :: fault-worker-stop-suppresses-stale-backlog | `m17-worker-results.json` | TECHNICAL PASS |
| G17-S18-A03 | cool-down deduplicates repeat alerts | `monitoring/m17/alert-test.yaml` :: S18-A03 repeat suppression | `m17-promtool-test.txt` | TECHNICAL PASS |
| G17-S19-A01 | critical panels show actual live measurement | `tests/browser/m17/dashboard.spec.mjs` :: critical monitoring panels use live series and no-data views are explicitly unknown | `m17-browser-results.json` | TECHNICAL PASS |
| G17-S19-A02 | unknown/unavailable have explicit distinct states | `tests/browser/m17/dashboard.spec.mjs` :: rendered readiness distinguishes missing data from real downtime in three browsers | `m17-browser-results.json` | TECHNICAL PASS |
| G17-S19-A03 | alert links only sanitized diagnostic evidence | `tests/browser/m17/dashboard.spec.mjs` :: alert dashboard links stay local and never embed private diagnostic parameters | `m17-browser-results.json` | TECHNICAL PASS |
| G17-S20-A01 | browser/API fault yields server correlation | `scripts/check-g17-diagnostic-journey.mjs` :: page-originated-same-request | `m17-diagnostic-journey-results.json` | TECHNICAL PASS |
| G17-S20-A02 | trace to DB/worker or documented async bridge resolves | `scripts/check-g17-diagnostic-journey.mjs` :: real-trace-to-db | `m17-diagnostic-journey-results.json` | TECHNICAL PASS |
| G17-S20-A03 | dashboard alert names correct component without private payload | `scripts/check-g17-diagnostic-journey.mjs` :: same-fault-rendered-alert | `m17-diagnostic-journey-results.json` | TECHNICAL PASS |
| G17-S21-A01 | paired workload measurement meets approved p95 and memory budgets | `scripts/check-g17-performance.mjs` :: measured_routes | `m17-performance-raw.json` | TECHNICAL PASS |
| G17-S21-A02 | exact environment/variance reported | `scripts/check-g17-performance.mjs` :: THREE_MATCHED_PAIRS | `m17-performance-raw.json` | TECHNICAL PASS |
| G17-S21-A03 | pressure does not block business | `scripts/check-g17-pressure-matrix.mjs` :: business-under-tenfold-pressure-and-exporter-outage | `m17-pressure-matrix-results.json` | TECHNICAL PASS |
| G17-S22-A01 | local telemetry capped and expires only its own disposable data | `scripts/check-g17-retention-matrix.mjs` :: m17-tempo-retention-results.json | `m17-retention-matrix-results.json` | TECHNICAL PASS |
| G17-S22-A02 | full store degrades observation safely | `scripts/check-g17-storage.mjs` :: full-telemetry-store | `m17-storage-results.json` | TECHNICAL PASS |
| G17-S22-A03 | M8/M11/M15/M16 retained data unchanged | `scripts/check-g17-storage.mjs` :: all-application-history-byte-identical | `m17-storage-results.json` | TECHNICAL PASS |
| G17-S23-A01 | M9/M10/M11/M13/M15 opt-in/hold semantics unchanged | `scripts/check-g17-regression-matrix.mjs` :: M13AdmissionControlAcceptanceTests | `g17-regression-matrix-results.json` | TECHNICAL PASS |
| G17-S23-A02 | accepted authorization/health/audit APIs preserved | `scripts/check-g17-regression-matrix.mjs` :: G15AuditAcceptanceTests | `g17-regression-matrix-results.json` | TECHNICAL PASS |
| G17-S23-A03 | changed source build, .NET regression and targeted browser suites pass | `scripts/verify-g17-manifest.mjs` :: Completed run has missing evidence or a failed stage | `run-status.json` | TECHNICAL PASS |
| G17-S24-A01 | every claimed assertion maps to real source-locked result | `scripts/reconcile-g17-assertions.test.mjs` :: rejects a missing assertion evidence row even when other suites pass | `g17-node-tests.xml` | TECHNICAL PASS |
| G17-S24-A02 | source hashes and manifests complete/no unlisted files | `scripts/check-g17-manifest.test.mjs` :: altered evidence cannot pass hash verification | `g17-node-tests.xml` | TECHNICAL PASS |
| G17-S24-A03 | negative control rejects tampering, skip and wrong SHA | `scripts/g17-adversarial-evidence.test.mjs` :: G17 verifier rejects tampering, skipped cases, duplicate results and wrong source SHA | `g17-node-tests.xml` | TECHNICAL PASS |
| G17-S25-A01 | separate reviewer checks raw source, privacy, metrics and full cases | independent reviewer | none | PENDING INDEPENDENT REVIEW |
| G17-S25-A02 | policy and procedural exceptions disclosed | independent reviewer | none | PENDING INDEPENDENT REVIEW |
| G17-S25-A03 | reviewer issues explicit signed PASS/NOT PASS before G17 acceptance | independent reviewer | none | PENDING INDEPENDENT REVIEW |

## Boundaries and remaining risks

- **Not accepted.** G17 needs a genuinely separate reviewer with source and raw evidence access to issue the explicit signed PASS or NOT PASS (S25-F06). This work cannot self-sign.
- The P09 result passed in one of two runs of identical source; margins are 0.7 point (authorized p95) and 1.2 points (memory).
- Scope of this run: the full unit and contract suites, the M17 and selected integration classes, eight accepted-milestone regression classes with M17 opted in, and the M17 browser suites. It is not a rerun of every milestone suite.
- The M9 transport hold, approved M15 fail-closed behaviour, M16 audit protections and default-off M17 are unchanged.
- `scripts/check-planning-docs.mjs` already fails at the starting commit on an unrelated M11 assertion; it was not modified.

Related records: [gap closure and test audit](M17_G17_S25_GAP_CLOSURE_AND_TEST_AUDIT_20261114.md), [open findings register](M17_G17_S25_OPEN_FINDINGS_REGISTER_20261114.md), [candidate and review packet](M17_G17_S25_CANDIDATE_AND_REVIEW_PACKET_20261114.md).

## Addendum, 2026-11-15: post-review work

The independent review of source `5b1dcc41` supported the technical evidence above. The following was added afterwards and does not change any figure in this record.

- **Source identity.** No product code, verifier, monitoring rule, dashboard or script under test changed after `5b1dcc41`. Later commits changed tests, test fixtures, the Node test list in `scripts/run-g17-local.ps1` and documentation only. The f7 evidence therefore remains evidence for `5b1dcc41`, and the tests added later were not part of the f7 run.
- **Re-verification with the final verifier code.** The technical ledger re-derived from the preserved f7 evidence has SHA-256 `8d0342fc24893919ec5e4443da0ad9e1cec3975c83bfe9e39169d12db163da22`, identical to the recorded ledger (72 of 72 witnesses). The manifest verifies in place (54 files, complete, no missing artifact, gate NOT_ACCEPTED, 0 assertions reviewed).
- **Evidence directory.** An archive named `g17-final-f7-evidence.zip` that was not created by the runner appeared in the evidence directory. The repository owner confirmed they created it. It was moved, not deleted, to `preserved-20261115\owner-made-g17-final-f7-evidence.zip` (SHA-256 `1b830a403e5d11913599a9c425e82f0010a6f2981a08270dc654ff9277232198`, unchanged). The 54 manifested files were never modified.
- **P09.** [Variance assessment](M17_G17_S25_P09_VARIANCE_ASSESSMENT_20261115.md): f6 and f7 compared under the approved method; both remain on record; the margin is thin; additional controlled verification is recommended and needs an owner decision.
- **Mutation testing.** [Survivor classification](M17_G17_S25_MUTATION_SURVIVOR_CLASSIFICATION_20261115.md) with the complete per-mutant data under `docs/m17-s25-mutation-evidence-20261115/`.
- **Suite status.** The 21-file G17 Node suite passes with 355 cases and none skipped; the five milestone Node tests outside the G17 runner (M7, M8, M9, G4, DCSA) pass with 42 cases.
