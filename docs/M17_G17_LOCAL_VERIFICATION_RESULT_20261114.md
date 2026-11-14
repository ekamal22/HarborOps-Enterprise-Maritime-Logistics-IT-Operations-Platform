# G17 Local Source-Locked Verification Result

**Latest G17 evidence, 2026-11-14:** Hosted [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851) succeeded at executable SHA `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`: 47/47 stages passed, 72/72 technical assertions reported PASS, no required skips, 25 scenarios represented, and 3 independent S25 assertions still pending. Suite TRX reports 319 unit, 60 contract, 54 M17 integration and 34 accepted regression passes. Performance and fault, export privacy, malformed context, worker journey and three-engine dashboard witnesses passed. The 45-entry manifest has zero missing expected artifacts; GitHub's downloadable archive omits two hidden Playwright `.last-run.json` metadata files that were hashed in the manifest. Historical c7 and hosted failure evidence stay intact. **Technical campaign GREEN; G17 NOT ACCEPTED; independent S25 review pending.** [Current green evidence record](M17_G17_TECHNICAL_VERIFICATION_GREEN_20261114.md).

**Historical c7 result:** c7 remains complete at source `085fc666`. Subsequent independent S25 assessment returned NOT PASS, requiring expanded behavioral evidence. The result below is not evidence for those later corrections. The report was published on `verify/g17-local-evidence-20261114` and protected main remains unchanged. [Remediation register](M17_G17_S25_REMEDIATION_20261114.md).

**Recorded:** 2026-11-14. **Gate:** G17 NOT ACCEPTED; independent G17-S25 review pending.
**Verified source:** `085fc666448962ec4d133bd01bf3992f7d360191` on local branch `feat/m17-g17-verification-s25-b4c2e4`, eight local commits above the handoff head `6a21718a3f222628aeada95a4b33a2337293b366` of `feature/m17-observability-20261112`. At the time of c7, nothing had been pushed. Its source was later published on the verification branch; protected `main` remained untouched.
**Method:** the complete campaign ran locally through `scripts/run-g17-local.ps1` (PowerShell 7) against a disposable Docker project. GitHub Actions was not used for any part of this verification; the earlier hosted run was only read as historical evidence.

## Result in one paragraph

Campaign c7 completed with run state COMPLETE: 40 of 40 recorded stages passed with exit code 0, the reconciler found 72 of 72 technical assertions individually witnessed by raw evidence, and the evidence manifest (35 files) verified with zero missing artifacts and zero hash mismatches when recomputed independently. The three G17-S25 assertions remain unreviewed by design. This is a technical verification result, not a gate acceptance, and it does not authorize a merge, release or production monitoring.

## Implemented, tested, verified, reviewed, accepted

| Level | State |
| --- | --- |
| Implemented | M17 telemetry, worker correlation, private monitoring stack and alerts |
| Tested | 72 technical witnesses authored and executed |
| Technically verified | 72 of 72 at `085fc666`, evidence below |
| Independently reviewed | No. G17-S25-A01 to A03 are pending a separate reviewer |
| Accepted | No |
| Integrated | No. Not merged; local commits only |

## Run totals (c7, `085fc666`)

| Suite | Result |
| --- | --- |
| Node source and negative-control tests | 55 passed, 0 failed, 0 skipped |
| .NET unit tests | 319 of 319 passed |
| .NET contract tests | 60 of 60 passed |
| .NET M17 integration tests (separate disposable PostgreSQL, restricted roles) | 53 of 53 passed |
| Accepted-milestone regressions with M17 opted in (M9, M10, M11, M13, M15, audit, authorization, held defaults) | 34 of 34 passed in 8 of 8 groups |
| Dashboard browser suite (Chromium, Firefox, WebKit) | 18 of 18 passed, 0 skipped, 0 flaky, 0 retried |
| Cardinality browser suite (10,000 hostile requests, Chromium) | 1 of 1 passed |
| Live stack checks (network, database fault, collector fault, latency, alert matrix, privacy, storage, retention x3, restart, cardinality memory, performance, pressure, worker, worker journey) | all passed |
| Prometheus rule check and rule fixtures | 11 rules, fixtures SUCCESS |

Key measured values from the raw files:

- **P09 overhead** (budgets 10% p95 and 15% peak memory, unchanged): pooled p95 change -8.1% (baseline route) and +0.7% (authorized route) over 3,600 samples per mode and route; peak memory increase 13.6%, 14.4%, 13.6% per pair, mean 13.9%.
- **Cardinality:** 40,000 hostile paths in four waves; post-stress memory change -6.9%; exported custom series within the approved ceiling.
- **Worker journey:** 50 receipts accepted and reconciled by the restricted worker role, replay created no duplicate, 4 API-to-worker safe trace pairs, 683 exported series checked for tenant identifiers.
- **Privacy:** canary values in headers, query string, cookies and baggage were absent from the 2 retrieved traces (4 spans), 134 metric series and the sanitized service logs of the API, collector and Tempo.
- **Network:** nine services on exactly the expected networks; the four protected monitoring ports (4317, 9464, 9090, 3200) refused untrusted host access; dashboard published only on `127.0.0.1:3301` by the forwarder; non-loopback addresses refused.
- **Evidence integrity:** every report carries the same source SHA and project; no disposable credential appears in any evidence text file.

## Evidence location and hashes

| Item | Value |
| --- | --- |
| Evidence directory | `E:\HarborOps-Verification\g17-local\c7-evidence` |
| Archive | `E:\HarborOps-Verification\g17-local\c7-evidence-085fc66.zip`, sha256 `6fafaa1d2ab3db637079f989c3db775059096875bcff498d8d24ae4ba1a5f761` |
| `manifest.json` sha256 | `77f9cf12cd85175a7c1ba7e3d81be9ae8e8c21ff8bdeda2f0c84539d4d65c52b` |
| `g17-technical-assertion-ledger.json` sha256 | `60bc9bf22abb35219d4d8cdddd33266504f60c0e4a8327e994ed8096c22d4fe9` |
| `run-status.json` sha256 | `9af4dd1bffcd93a92e6e90b7bba5e75663523de44adbac956908957ecba1ed71` |
| Failed campaigns c1 to c6 | kept unmodified in `E:\HarborOps-Verification\g17-local\c1-evidence` to `c6-evidence` |
| Historical hosted run 43477790029 | `E:\HarborOps-Verification\g17-run-43477790029` (read only) |

## Every attempt, cause and correction

The hosted run 43477790029 failed because Prometheus rejected `--web.enable-admin-api=false`. Everything after that stage had never executed, so each later local campaign exposed the next defect. All failed campaigns are preserved.

| Attempt | Source | Outcome | Root cause | Correction |
| --- | --- | --- | --- | --- |
| Hosted 43477790029 | `7f439b95` | failed at network check | Prometheus exits 1: invalid `--flag=false` boolean flags | removed both flags (features default off) |
| c1 | `94937502` | failed at database fault stage | integration and regression suites re-provision the shared `harborops_api` role, so the running API lost its login | suites now use their own disposable PostgreSQL cluster (port 15544) |
| c2 | `36fc20c6` | failed at export privacy stage | bounded Tempo search over a backend holding thousands of hostile-path traces returned an arbitrary subset; Tempo answers an empty trace envelope for unstored ids | canary and journey traces fetched by submitted id, stored spans required |
| c3 | `43518038` | failed at overhead measurement | per-pair p95 carries about 10% noise, the size of the budget | raw latencies retained, 3x samples, owner-confirmed pooled rule (below) |
| c4 | `a32fbc07` | failed at database fault stage | "current sample" bound of 35 s was below the telemetry path worst case of 45 s | bound 60 s; source test derives it from the configured intervals |
| c5 | `06c1f020` | failed at overhead measurement | real overhead near the budget edge (baseline pooled +10.4%, two memory pairs above 15%) | owner decided the evaluation rule; the budgets were not changed |
| c6 | `ab76c039` | failed at worker stage | verifier checked `running healthy` before the first container health check could run | bounded wait |
| c7 | `085fc666` | **COMPLETE, 72 of 72** | none | none |

Before c7, the reconciler and manifest steps were dry-run over a synthetic copy of c6 evidence (not gate evidence) and exposed two defects that would have failed every full campaign: the JUnit parser read `classname` as the case name, collapsing 53 witnesses into one, and xUnit's truncated theory row names were treated as double-reported results. Both are fixed with regression tests.

Defects found and corrected in the implementation and configuration:

1. Compose: Prometheus flags (above); Grafana could never be reached on port 3301 because Docker publishes no host port for a container attached only to internal networks (a loopback-only forwarder now owns the single published listener; Grafana publishes nothing).
2. Alerts, dashboard and verifiers queried `harborops_db_readiness`, `harborops_service_running` and `harborops_telemetry_collector_reachable`, but the collector exports them with a `_ratio` suffix, so they matched nothing. A source test now derives exported names from the C# instruments.
3. A drop counter that first appears already holding its loss count produces no Prometheus `increase()`, so the first telemetry loss could never alert. The API and worker now record a zero on each heartbeat (a zero added during static initialization is lost before the exporter subscribes).
4. The database alert freshness bound (35 s, now 60 s).
5. Verifier defects: Tempo v2 trace envelope, trimmed leading zeros and base64 ids, `pg_dump` random `\restrict` token making the byte-identical history proof impossible, OpenMetrics timestamp units, antiforgery cookie order, a synthetic container id violating its own policy regex, the `/series` response shape, and failure causes being discarded (nine reports now keep a bounded error message; the runner keeps per-service failure diagnostics).
6. Reconciliation now binds each script assertion to a named passing fact in its raw report. Previously a script witness passed on the report's overall PASS alone, and three reports (cardinality memory, worker journey, live latency) were not mapped at all, so 72 of 72 could never have been reached.
7. The valid-trace-context contract test now also proves an anonymous caller stays refused with propagated context, role-claiming baggage and a forged correlation value (G17-S07-A01).

## Decisions and qualifications a reviewer must see

1. **P09 evaluation rule (owner decision, 2026-11-14).** The approved budgets are unchanged: 10% p95 increase and 15% peak memory increase. A single matched pair measured about 10% run-to-run noise in p95, so p95 is judged on the pooled samples of all three matched rounds and memory on the mean of the three pairs. Per-pair figures and every raw latency sample are retained. Under the earlier strict per-pair rule, four of eight full measurement runs would have failed; this is disclosed, not hidden. Memory overhead sits at 13 to 14 percent of a 15 percent budget, so the margin is thin.
2. **Privacy trace sample is small.** Head sampling keeps about 10 percent of traces, so 320 submitted requests yielded 2 retrievable canary traces (4 spans) in c7; the gate requires at least 2. Metric series (134) and service logs were also scanned. A reviewer may reasonably ask for a larger sample.
3. **Environment.** Host SDK 10.0.404; local NuGet caches missing placeholder files produced a spurious "package does not support net10.0" warning, so `SuppressTfmSupportBuildWarnings=true` was set as an environment variable (no source or output change). PostgreSQL data and keyring for the disposable stack live in the Docker engine VM because Windows bind mounts cannot be chowned to the rootless UIDs; the VM disk image itself is on E:.
4. **No production claims.** All evidence comes from a disposable local stack with synthetic data.
5. **The scanned telemetry records were not retained.** `m17-export-privacy-results.json` keeps, for each scanned item, only its kind, byte size and sha256 (2 Tempo traces, 1 service-log blob, 134 metric series). The exported trace JSON, the log text and the privacy-scan series label sets are not in the archive, and the stack storage was in memory and is gone. `m17-worker-journey-results.json` likewise keeps counts only. Retained in full: the 134 label sets of the cardinality test, all raw latency samples, and all TRX, JUnit and Playwright reports. An independent reviewer therefore cannot re-run the canary search over the original exported records from this archive.

## Security and boundary confirmation (tested, with the limits above)

- Telemetry privacy: no cookie, authorization value, query value, baggage secret, SQL text, tenant identifier or account email in exported traces, metric series or service logs; trace events, links and state are erased before export.
- Isolation: the collector, Tempo and Prometheus publish no host port; untrusted host clients are refused; Grafana requires its local credential and publishes nothing itself.
- Authorization: cross-organization requests are denied identically to baseline, an analyst write stays forbidden and is not persisted, and trace context does not change an authentication decision.
- Restricted PostgreSQL roles: the worker reads only its owned receipt link and cannot read M16 audit or modify history; application history hashes are byte identical before and after telemetry store saturation.
- Retention: 24 hour metric and trace expiry proved on isolated fixtures that remove only expired data; no notification retention was changed, M15 policies untouched, the M9 transport hold unchanged.

## Assertion ledger (75 of 75 accounted for)

72 technical assertions have a raw witness in the c7 ledger; 3 are pending the independent reviewer.

| Assertion | Expected behavior | Evidence kind | Witness | Raw artifact | Outcome |
| --- | --- | --- | --- | --- | --- |
| G17-S01-A01 | all four work packages implemented | node source test | `g17-source-boundary.test.mjs` : M17 four work packages exist with actual referenced produ... | g17-node-tests.xml | PASS (technical) |
| G17-S01-A02 | approved P01-P09 and exact instrument mapping match source | node source test | `g17-source-boundary.test.mjs` : every approved M17 policy has a concrete source-side veri... | g17-node-tests.xml | PASS (technical) |
| G17-S01-A03 | no unapproved default service, API or migration changes | node source test | `g17-source-boundary.test.mjs` : source diff does not change the accepted default Compose ... | g17-node-tests.xml | PASS (technical) |
| G17-S02-A01 | each metric has exact name/type/unit | .NET test | `M17MetricSchemaTests.cs` : EveryLocalInstrumentHasTheApprovedNameTypeAndUnit | g17-integration.trx | PASS (technical) |
| G17-S02-A02 | no duplicate request counters | .NET test | `M17MetricSchemaTests.cs` : RequestCountIsDerivedOnlyFromDurationHistogramAndNotDupli... | g17-integration.trx | PASS (technical) |
| G17-S02-A03 | histogram produces nonzero latency samples | .NET test | `M17MetricSchemaTests.cs` : RealNonzeroRequestAndDatabaseDurationsReachMeterListeners | g17-integration.trx | PASS (technical) |
| G17-S03-A01 | live stays responsive with DB down | .NET test | `M17HttpContractTests.cs` : EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErr... | g17-contract.trx | PASS (technical) |
| G17-S03-A02 | ready goes unavailable on DB stop or absent seed within budget | .NET test | `M17HttpContractTests.cs` : EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErr... | g17-contract.trx | PASS (technical) |
| G17-S03-A03 | existing JSON/status paths unchanged | .NET test | `M17HttpContractTests.cs` : EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErr... | g17-contract.trx | PASS (technical) |
| G17-S04-A01 | response correlation generated on server | .NET test | `M17HttpContractTests.cs` : SpoofedCorrelationIsReplacedAndWarningKeepsServerGenerate... | g17-contract.trx | PASS (technical) |
| G17-S04-A02 | error and structured log scope use same value | .NET test | `M17HttpContractTests.cs` : SpoofedCorrelationIsReplacedAndWarningKeepsServerGenerate... | g17-contract.trx | PASS (technical) |
| G17-S04-A03 | client-supplied correlation cannot spoof it | .NET test | `M17HttpContractTests.cs` : SpoofedCorrelationIsReplacedAndWarningKeepsServerGenerate... | g17-contract.trx | PASS (technical) |
| G17-S05-A01 | metric attributes contain only approved keys | .NET test | `M17MetricSchemaTests.cs` : CustomMetricLabelsRejectUntrustedHostsPathsAndBusinessIde... | g17-integration.trx | PASS (technical) |
| G17-S05-A02 | no user/tenant/request/SQL fields | live stack check | `check-g17-export-privacy.mjs` : owned-sanitized-service-logs | m17-export-privacy-results.json | PASS (technical) |
| G17-S05-A03 | unknown route has stable fallback | .NET test | `M17TelemetryContractTests.cs` : TenThousandAttackerChosenPathsUseOneBoundedOperationSeries | g17-integration.trx | PASS (technical) |
| G17-S06-A01 | 10,000 distinct routes/identifiers stay within approved active series cap | Playwright, 3 engines | `cardinality.spec.mjs` : ten thousand hostile HTTP requests remain within complete... | m17-cardinality-results.json | PASS (technical) |
| G17-S06-A02 | overflow coalesces | .NET test | `M17TelemetryContractTests.cs` : TenThousandAttackerChosenPathsUseOneBoundedOperationSeries | g17-integration.trx | PASS (technical) |
| G17-S06-A03 | memory does not grow without bound | live stack check | `check-g17-cardinality-memory.mjs` : FOUR_TEN_THOUSAND_PATH_STRESS_WAVES | m17-cardinality-memory-results.json | PASS (technical) |
| G17-S07-A01 | W3C valid trace context accepted without altering auth | .NET test | `M17HttpContractTests.cs` : ValidW3CTraceparentConnectsApiRequestAndBoundedDatabaseSpan | g17-contract.trx | PASS (technical) |
| G17-S07-A02 | API -> safe DB work connected by trace | .NET test | `M17SuccessfulDatabaseAndAuditTests.cs` : HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan | g17-integration.trx | PASS (technical) |
| G17-S07-A03 | original audit correlation preserved | .NET test | `M17SuccessfulDatabaseAndAuditTests.cs` : OptedInTrackingAcceptancePreservesImmutableAuditCorrelation | g17-integration.trx | PASS (technical) |
| G17-S08-A01 | malformed traceparent/tracestate handled safely | .NET test | `M17HttpContractTests.cs` : MalformedTraceContextDoesNotTurnDependencyFailureIntoUnha... | g17-contract.trx | PASS (technical) |
| G17-S08-A02 | oversized baggage ignored | live stack check | `check-g17-export-privacy.mjs` : baggage:"secret=" | m17-export-privacy-results.json | PASS (technical) |
| G17-S08-A03 | no raw header is exported, no 500 | live stack check | `check-g17-export-privacy.mjs` : invalid- | m17-export-privacy-results.json | PASS (technical) |
| G17-S09-A01 | successful bounded DB operation emits duration/outcome | .NET test | `M17SuccessfulDatabaseAndAuditTests.cs` : HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan | g17-integration.trx | PASS (technical) |
| G17-S09-A02 | outage emits classified failure without secret SQL | live stack check | `check-g17-database-fault.mjs` : real-db-outage-keeps-live-and-errors-classified | m17-database-fault-results.json | PASS (technical) |
| G17-S09-A03 | business error contract remains unchanged | .NET test | `M17HttpContractTests.cs` : EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErr... | g17-contract.trx | PASS (technical) |
| G17-S10-A01 | worker that is intentionally disabled/held remains labelled disabled/held | Playwright, 3 engines | `dashboard.spec.mjs` : authenticated samples match the disposable ready API and ... | m17-browser-results.json | PASS (technical) |
| G17-S10-A02 | enabled disposable worker emits correct attempts/outcomes | .NET test | `M17WorkerShellFunctionalTests.cs` : ActualWorkerShellEmitsProcessedAndReconciliationDispositi... | g17-integration.trx | PASS (technical) |
| G17-S10-A03 | no worker auto-start | .NET test | `M17WorkerShellFunctionalTests.cs` : DefaultApiHostDoesNotRegisterTheHeldTrackingWorker | g17-integration.trx | PASS (technical) |
| G17-S11-A01 | authorized worker work links safely to source or documented correlation | .NET test | `M17RestrictedWorkerReaderTests.cs` : RestrictedWorkerCanLinkOnlyOwnedReceiptAndCannotReadM16Au... | g17-integration.trx | PASS (technical) |
| G17-S11-A02 | fabricated parent/tenant trace context rejected | .NET test | `M17CorrelationJourneyTests.cs` : ForgedTenantAndW3CParentHeadersCannotControlDurableReceip... | g17-integration.trx | PASS (technical) |
| G17-S11-A03 | replay/retry has unique bounded spans | .NET test | `M17ReplayRedriveSpanTests.cs` : RealReplayAndAuthorizedRedriveEmitOneSpanPerWorkerAttempt... | g17-integration.trx | PASS (technical) |
| G17-S12-A01 | collector down does not change API response, transaction or readiness | .NET test | `M17ExporterOutageMutationTests.cs` : UnreachableCollectorCannotChangeAuthorizedMutationOrConfl... | g17-integration.trx | PASS (technical) |
| G17-S12-A02 | export queue bounded/drops counted | .NET test | `M17ExporterQueuePressureTests.cs` : QueueFullDropsAreCountedWithoutBlockingSpanProducers | g17-integration.trx | PASS (technical) |
| G17-S12-A03 | exporter recoverable without manual data repair | live stack check | `check-g17-collector-fault.mjs` : collector-recovery-automatic | m17-collector-fault-results.json | PASS (technical) |
| G17-S13-A01 | secrets, cookies, personal canaries and SQL values absent from logs, spans and metric labels | live stack check | `check-g17-export-privacy.mjs` : owned-sanitized-service-logs | m17-export-privacy-results.json | PASS (technical) |
| G17-S13-A02 | sanitized errors still diagnose component and type | live stack check | `check-g17-database-fault.mjs` : database-alert-identifies-only-safe-component | m17-database-fault-results.json | PASS (technical) |
| G17-S13-A03 | retained M16 audit is not exposed | .NET test | `M17RestrictedWorkerReaderTests.cs` : RestrictedWorkerCanLinkOnlyOwnedReceiptAndCannotReadM16Au... | g17-integration.trx | PASS (technical) |
| G17-S14-A01 | cross-organization request produces same denial as baseline | .NET test | `M17TenantIsolationTests.cs` : OptedInTelemetryPreservesTenantDenialsAndAnalystWriteRevo... | g17-integration.trx | PASS (technical) |
| G17-S14-A02 | telemetry does not disclose tenant detail | live stack check | `check-g17-worker-journey.mjs` : Prometheus exported private tenant/source/account identif... | m17-worker-journey-results.json | PASS (technical) |
| G17-S14-A03 | instrumentation cannot elevate a role | .NET test | `M17TenantIsolationTests.cs` : OptedInTelemetryPreservesTenantDenialsAndAnalystWriteRevo... | g17-integration.trx | PASS (technical) |
| G17-S15-A01 | process restart and canceled request do not leak stale spans or requests | live stack check | `check-g17-restart.mjs` : actual_spans_inspected | m17-restart-results.json | PASS (technical) |
| G17-S15-A02 | shutdown drain is bounded | live stack check | `check-g17-restart.mjs` : owned-api-shutdown-bounded | m17-restart-results.json | PASS (technical) |
| G17-S15-A03 | no duplicated business effect or worker lease change | live stack check | `check-g17-restart.mjs` : restart-recovers-health-without-business-duplication | m17-restart-results.json | PASS (technical) |
| G17-S16-A01 | default Compose contains no collector/dashboard | node source test | `check-m17-source.test.mjs` : the default stack does not start or expose M17 components | g17-node-tests.xml | PASS (technical) |
| G17-S16-A02 | optional dashboard bound loopback | live stack check | `check-g17-network.mjs` : dashboard_listener | m17-network-results.json | PASS (technical) |
| G17-S16-A03 | collector not exposed externally, untrusted reads denied | live stack check | `check-g17-network.mjs` : Untrusted host client reached protected monitoring port | m17-network-results.json | PASS (technical) |
| G17-S17-A01 | controlled 5xx/latency/readiness faults trigger configured alerts | live stack check | `check-g17-alert-matrix.mjs` : real-http-5xx-rate-fires-reference-alert | m17-alert-matrix-results.json | PASS (technical) |
| G17-S17-A02 | threshold/window evidence exact | promtool fixture | `alert-test.yaml` : Readiness must not remain critical | m17-promtool-test.txt | PASS (technical) |
| G17-S17-A03 | clear/hysteresis works after recovery | promtool fixture | `alert-test.yaml` : Latency recovers below | m17-promtool-test.txt | PASS (technical) |
| G17-S18-A01 | zero/low traffic is unknown/insufficient, not healthy | Playwright, 3 engines | `dashboard.spec.mjs` : zero and insufficient traffic render unknown rather than ... | m17-browser-results.json | PASS (technical) |
| G17-S18-A02 | held workers cause no false failure alert | live stack check | `check-g17-worker.mjs` : fault-worker-stop-suppresses-stale-backlog | m17-worker-results.json | PASS (technical) |
| G17-S18-A03 | cool-down deduplicates repeat alerts | promtool fixture | `alert-test.yaml` : High failure rate falls to zero | m17-promtool-test.txt | PASS (technical) |
| G17-S19-A01 | critical panels show actual live measurement | Playwright, 3 engines | `dashboard.spec.mjs` : critical monitoring panels use live series and no-data vi... | m17-browser-results.json | PASS (technical) |
| G17-S19-A02 | unknown/unavailable have explicit distinct states | live stack check | `check-g17-restart.mjs` : stale-readiness-is-unknown-not-healthy | m17-restart-results.json | PASS (technical) |
| G17-S19-A03 | alert links only sanitized diagnostic evidence | Playwright, 3 engines | `dashboard.spec.mjs` : alert dashboard links stay local and never embed private ... | m17-browser-results.json | PASS (technical) |
| G17-S20-A01 | browser/API fault yields server correlation | .NET test | `M17HttpContractTests.cs` : ValidW3CTraceparentConnectsApiRequestAndBoundedDatabaseSpan | g17-contract.trx | PASS (technical) |
| G17-S20-A02 | trace to DB/worker or documented async bridge resolves | live stack check | `check-g17-worker-journey.mjs` : matched_safe_trace_pairs | m17-worker-journey-results.json | PASS (technical) |
| G17-S20-A03 | dashboard alert names correct component without private payload | live stack check | `check-g17-database-fault.mjs` : database-alert-identifies-only-safe-component | m17-database-fault-results.json | PASS (technical) |
| G17-S21-A01 | paired workload measurement meets approved p95 and memory budgets | live stack check | `check-g17-performance.mjs` : measured_routes | m17-performance-raw.json | PASS (technical) |
| G17-S21-A02 | exact environment/variance reported | live stack check | `check-g17-performance.mjs` : THREE_MATCHED_PAIRS | m17-performance-raw.json | PASS (technical) |
| G17-S21-A03 | pressure does not block business | live stack check | `check-g17-pressure-matrix.mjs` : business-under-tenfold-pressure-and-exporter-outage | m17-pressure-matrix-results.json | PASS (technical) |
| G17-S22-A01 | local telemetry capped and expires only its own disposable data | live stack check | `check-g17-retention-matrix.mjs` : m17-tempo-retention-results.json | m17-retention-matrix-results.json | PASS (technical) |
| G17-S22-A02 | full store degrades observation safely | live stack check | `check-g17-storage.mjs` : full-telemetry-store | m17-storage-results.json | PASS (technical) |
| G17-S22-A03 | M8/M11/M15/M16 retained data unchanged | live stack check | `check-g17-storage.mjs` : all-application-history-byte-identical | m17-storage-results.json | PASS (technical) |
| G17-S23-A01 | M9/M10/M11/M13/M15 opt-in/hold semantics unchanged | live stack check | `check-g17-regression-matrix.mjs` : M13AdmissionControlAcceptanceTests | g17-regression-matrix-results.json | PASS (technical) |
| G17-S23-A02 | accepted authorization/health/audit APIs preserved | live stack check | `check-g17-regression-matrix.mjs` : G15AuditAcceptanceTests | g17-regression-matrix-results.json | PASS (technical) |
| G17-S23-A03 | changed source build, .NET regression and targeted browser suites pass | runner ledger | `verify-g17-manifest.mjs` : Completed run has missing evidence or a failed stage | run-status.json | PASS (technical) |
| G17-S24-A01 | every claimed assertion maps to real source-locked result | node source test | `reconcile-g17-assertions.test.mjs` : rejects a missing assertion evidence row even when other ... | g17-node-tests.xml | PASS (technical) |
| G17-S24-A02 | source hashes and manifests complete/no unlisted files | node source test | `check-g17-manifest.test.mjs` : altered evidence cannot pass hash verification | g17-node-tests.xml | PASS (technical) |
| G17-S24-A03 | negative control rejects tampering, skip and wrong SHA | node source test | `g17-adversarial-evidence.test.mjs` : G17 verifier rejects tampering, skipped cases, duplicate ... | g17-node-tests.xml | PASS (technical) |
| G17-S25-A01 | separate reviewer checks raw source, privacy, metrics and full cases | independent S25 review | pending independent reviewer | none | PENDING (independent) |
| G17-S25-A02 | policy and procedural exceptions disclosed | independent S25 review | pending independent reviewer | none | PENDING (independent) |
| G17-S25-A03 | reviewer issues explicit signed PASS/NOT PASS before G17 acceptance | independent S25 review | pending independent reviewer | none | PENDING (independent) |


## Procedure for the three independent assertions (G17-S25)

The reviewer must be separate from the implementation and verification work. This document is an input to that review, not a substitute for it.

**Inputs:** this repository at `085fc666`; the c7 evidence directory or archive (hash above); the ledger and manifest; the failed campaigns c1 to c6; the hosted run record; `docs/M17_VERIFICATION_PLAN.md`, `docs/M17_G17_ASSERTION_INVENTORY_20261112.json`, `docs/M17_G17_TEST_COVERAGE_20261113.json`.

- **G17-S25-A01** (separate reviewer checks raw source, privacy, metrics and full cases):
  1. Recompute the archive and manifest hashes; confirm `source-sha.txt` equals the reviewed commit and every JSON report carries it.
  2. Recompute counts from the TRX files, `g17-node-tests.xml` and the two Playwright JSON files; confirm zero skipped, flaky or retried cases.
  3. For each row of the ledger above open the named raw artifact and confirm the named fact (the predicates are in `scripts/g17-raw-witness.mjs`).
  4. Read the test bodies of the multi-assertion witnesses (`M17HttpContractTests`, `M17MetricSchemaTests`, `M17TelemetryContractTests`, `M17CorrelationJourneyTests`, `M17ReplayRedriveSpanTests`, `M17ExporterQueuePressureTests`, both browser specs) and judge whether each independent sub-condition is asserted.
  5. Run a mutation check of choice (for example restore the 35 s freshness bound or remove a label from the allowlist) and confirm the verifier fails.
  6. The privacy scan's underlying records are not in the archive (see qualification 5). The reviewer can re-scan the 134 retained metric label sets in `m17-cardinality-raw.json` and the 14,400 retained latency samples, but must either accept the recorded scan result for the two traces and the service logs or request a rerun that retains them.
- **G17-S25-A02** (policy and procedural exceptions disclosed): confirm the disclosures in "Decisions and qualifications" are complete and accurate, including the owner-confirmed P09 rule, the failed campaigns, the environment workarounds and the small privacy trace sample.
- **G17-S25-A03** (explicit signed PASS or NOT PASS before G17 acceptance): the reviewer records a signed decision. Nothing in this repository records it.

## Outstanding work

- Retain the scanned exported records (trace documents, series label sets, sanitized log text) in the evidence of the next campaign, so the privacy scan can be independently repeated. This needs a new source-locked campaign; none has been run for it.
- The independent G17-S25 review and its signed decision.
- A decision on whether to publish the local commits. Pushing to `feature/m17-observability-20261112` would start the repository's G17 workflow on that branch, which the instruction for this assignment forbids, so no push was made.
- Disposable state folders inside the Docker VM for campaigns c1 to c7 (`/var/lib/ho-verify/ho-verify-m17-local-c1` to `-c7`) and the stopped campaign containers were left in place; removal of the folders was blocked by a safety check and is left to the owner.
