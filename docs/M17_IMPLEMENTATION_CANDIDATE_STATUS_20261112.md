# M17 Implementation Candidate Status

**Current integration state (2026-11-17):** M17 observability is accepted for integration and merged into protected `main` through [PR #53](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/53) at `1a35838acf2d96d561c6d0288e972c33f3a5d2c5`. Authenticated Campaign 01 reports 51/51 stages passed and 72/72 technical assertions supported across 25 scenarios. Integration acceptance and the report sign-off are recorded. The separately signed S25-A03/F06 reviewer instrument remains outstanding. P09 is QUALIFIED PASS with the original f6/m3 failures preserved and optimization plus remeasurement due before G24. Telemetry remains default-off/private, and no production deployment is authorized. Earlier dated checkpoints are historical. [Acceptance decision](M17_G17_S25_ACCEPTANCE_DECISION_20261117.md).

**Historical G17 policy decision (2026-11-16):** P09 is **QUALIFIED PASS** only, based on four passing complete runs out of five, with the f6 FAIL and its unexplained latency burst preserved. The approved pooled p95 latency cap is 10% and paired mean peak-memory cap is 15%, unchanged. Performance optimization is deferred to pre-release readiness or an earlier jointly agreed remediation if source-locked verification fails. [Decision](M17_G17_P09_QUALIFIED_PASS_DECISION_20261116.md). **G17 NOT ACCEPTED:** final source-locked verification and distinct S25 sign-off remain pending.

**Sequence constraint:** This is not a new implementation scope or accepted gate. Do not change the P09 method or thresholds to match a failing run. Freeze the final verified source, retain independent assertions A01-A03, and revisit overhead optimization before G24 or when an earlier full gate fails.

**Historical G17 technical verification (2026-11-14):** [GitHub Actions #43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851) completed **SUCCESS** on executable source `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`. The downloaded evidence reports 47/47 campaign stages passed, 25 scenario IDs, 72/72 source-bound technical assertions passed, and three independent S25 review assertions still pending. No required test was skipped. The raw suite files show 319 unit, 60 contract, 54 M17 integration and 34 regression passes. The 45-entry manifest has no missing expected artifacts, although the GitHub download omits two hidden Playwright `.last-run.json` metadata files listed in that manifest. All earlier failures and the separate c7 archive remain historical. **Technical verification GREEN; G17 NOT ACCEPTED; independent S25 acceptance pending.** [Technical evidence checkpoint](M17_G17_TECHNICAL_VERIFICATION_GREEN_20261114.md).

**Historical G17 checkpoint (2026-11-13, superseded):** The G17 coverage register has 72 authored technical assertions and three pending independent S25 clauses. [Run #43462771279](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43462771279) failed scoped integration with 52 passed, one failed and zero skipped: its process-wide worker span listener and shared database could include unrelated queued work. The strict two-span, single receipt and durable generation assertions remain in the corrected test. [Run #43470770246](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43470770246) was launched on source `a5c3af3829517ec10aed19861dc49ba936b63f97`; its final status is pending at this documentation checkpoint. All historical failed evidence is preserved. **G17 NOT ACCEPTED; acceptance pending.** [S25 readiness](M17_G17_S25_TEST_READINESS_20261113.md) | [Source-locked verification recovery](M17_G17_VERIFICATION_RECOVERY_20261113.md).

**Date:** 2026-11-12
**Base:** protected `main` at `b490fb8cfc879cdf05cb1f7484b99f6a9b264ed3`
**Source branch:** `feature/m17-observability-20261112`
**Historical disposition at 2026-11-12:** Implementation source candidate in progress, not accepted, merged or deployed at that date. Superseded by the 2026-11-17 G17 integration; production deployment remains unauthorized.
**Historical 2026-11-12 G17 evidence:** At this initial snapshot, 0 of 25 scenarios and 0 of 75 atomic assertions had executed source-locked evidence. Subsequent c7 completed locally but independent S25 returned NOT PASS; the current S25-remediated hosted campaign remains unverified.

## Implementation inventory

| Package | Source delivered on the feature branch | Still required |
| --- | --- | --- |
| M17-01 | Approved P01-P09 policy baseline, fixed meter names and safe attribute families, private endpoint check, provisional 10% trace sampling / 200 sampled spans/sec / 2,048 batch queue | Exact metric-name export mapping, 10,000-path cardinality measurement, alert calibration and overhead budget evidence |
| M17-02 | Opt-in API OTLP metrics/traces, bounded export, pre-export trace tag sanitizer, existing correlation preservation, readiness measurement, notification delivery observation, opt-in tracking worker attempt metrics and a narrow baseline-read database span | Compile and real DB/source traces, complete asynchronous causal links, direct SDK queue-loss evidence, full service and backlog metrics, fault tests |
| M17-03 | Separate optional Compose override with collector, Prometheus, Tempo, local Grafana dashboard, initial Prometheus rules, isolated memory-backed storage and no host-published OTLP port | Compose startup/health verification, exact Prometheus metric naming, Grafana security/browser validation, full P05 trigger and clear hysteresis, storage pressure testing |
| M17-04 | M17 contract tests, separate three-browser dashboard test suite, source-locked local runner, evidence manifest helper | Actual .NET, PostgreSQL, Docker, browser, process interruption, privacy, load, performance and fault campaigns with retained raw evidence |
| G17-S25 | Existing 25-scenario/75-assertion plan preserved | Independent complete raw artifact review, explicit policy exceptions, formal PASS/NOT PASS and separate gate decision |

## Source changes and invariants

- Default `compose.yaml`, migrations, existing database roles, M8/M11/M15 histories and M16 audit tables are unchanged.
- Monitoring is opt-in, with exporter endpoint restricted to internal `otelcol:4317` or loopback.
- The Grafana interface is local-only, password protected and not exposed through the shipment UI.
- The worker continues to require its separate existing opt-in Compose file. M9's transport-backlog hold is not lifted.
- M10 projector, M11 evaluator, M13 cleanup and M15 dispatcher retain their existing default-off policies.
- No retained record deletion, public export, hosted monitoring or production deployment is authorized. The isolated G17 disposable verification runs are permitted; their outcomes must not be treated as acceptance.

## Verification honesty and blocking gaps

At the 2026-11-12 source-candidate checkpoint, the local implementation sandbox lacked .NET and Docker. Subsequent hosted G17 attempts compiled the candidate and passed scoped suites but failed later verification stages. Those partial results are recorded in the verification recovery register and are not complete G17 acceptance evidence.

There are material evidence gaps even after the 2026-11-13 source remediation. Candidate source now contains a bounded baseline database span, a keyed diagnostic link through the existing durable receipt correlation, SDK queue-loss instrumentation, failed OTLP batch submission accounting, a read-only eligible worker backlog gauge, six local alert families, and real-export/browser test fixtures. None of those claims has been verified by executing the current SHA. The request-to-database and asynchronous-worker paths must be inspected under real sampling, and exact Prometheus family names, 500-series bounds, alert hysteresis, 24-hour/250-MiB telemetry storage limits, provisional latency and memory budgets, three-browser rendering, and fault recovery remain unverified. A source implementation does not replace the required G17 witnesses.

## Next closure sequence

1. Perform .NET restore/build and review package dependency advisories on the exact branch SHA.
2. Run the source checks and scoped unit/contract/integration tests on an E:-backed disposable environment.
3. Execute and reconcile the private collector, Tempo, Prometheus and Grafana startup, with listener, privacy and storage bounds checked.
4. Close missing behavior and evidence for all 75 assertions, including metrics, tracing, worker, alerts, browser, failure and performance.
5. Independently review actual source-locked artifacts for G17-S25. Record explicit acceptance only after that review and a separate decision.

No historical failed or pending gate evidence is altered by this candidate status.
