# M17 Implementation Candidate Status

**Date:** 2026-11-12
**Base:** protected `main` at `b490fb8cfc879cdf05cb1f7484b99f6a9b264ed3`
**Source branch:** `feature/m17-observability-20261112`
**Disposition:** Implementation source candidate in progress. G17 NOT ACCEPTED. Not merged or deployed.
**G17 evidence:** 0 of 25 scenarios and 0 of 75 atomic assertions have complete executed source-locked evidence. Independent S25 review not started.

## Implementation inventory

| Package | Source delivered on the feature branch | Still required |
| --- | --- | --- |
| M17-01 | Approved P01-P09 policy baseline, fixed meter names and safe attribute families, private endpoint check, provisional 10% trace sampling / 200 sampled spans/sec / 2,048 batch queue | Exact metric-name export mapping, 10,000-path cardinality measurement, alert calibration and overhead budget evidence |
| M17-02 | Opt-in API OTLP metrics/traces, bounded export, pre-export trace tag sanitizer, existing correlation preservation, readiness measurement, notification delivery observation, opt-in tracking worker attempt metrics | Compile and real DB/source traces, complete asynchronous causal links, direct SDK queue-loss evidence, full service and backlog metrics, fault tests |
| M17-03 | Separate optional Compose override with collector, Prometheus, Tempo, local Grafana dashboard, initial Prometheus rules, isolated memory-backed storage and no host-published OTLP port | Compose startup/health verification, exact Prometheus metric naming, Grafana security/browser validation, full P05 trigger and clear hysteresis, storage pressure testing |
| M17-04 | M17 contract tests, separate three-browser dashboard test suite, source-locked local runner, evidence manifest helper | Actual .NET, PostgreSQL, Docker, browser, process interruption, privacy, load, performance and fault campaigns with retained raw evidence |
| G17-S25 | Existing 25-scenario/75-assertion plan preserved | Independent complete raw artifact review, explicit policy exceptions, formal PASS/NOT PASS and separate gate decision |

## Source changes and invariants

- Default `compose.yaml`, migrations, existing database roles, M8/M11/M15 histories and M16 audit tables are unchanged.
- Monitoring is opt-in, with exporter endpoint restricted to internal `otelcol:4317` or loopback.
- The Grafana interface is local-only, password protected and not exposed through the shipment UI.
- The worker continues to require its separate existing opt-in Compose file. M9's transport-backlog hold is not lifted.
- M10 projector, M11 evaluator, M13 cleanup and M15 dispatcher retain their existing default-off policies.
- No retained record deletion, public export, hosted monitoring, production deployment or GitHub Actions run is authorized.

## Verification honesty and blocking gaps

The current execution environment has no .NET SDK or Docker engine and cannot run the actual API, PostgreSQL or three-browser stack. Source inspection confirmed that the expected new files, flags, guards and dashboard definitions exist, but it is not a replacement for compilation, integration execution, collector startup or raw artifacts. No passing G17 executable results are claimed.

There are material implementation and evidence gaps. Specifically, the current source does not yet demonstrate a complete request-to-database traced journey for all business operations, a persisted safe link to asynchronous tracking work, the exact 500-series metric budget, SDK queue drop reporting under exporter outages, full backlog metrics, measured alert hysteresis, or the provisional latency and memory budgets. Browser panel rendering and login have not been exercised. Any such omission must be addressed and verified before a G17 PASS recommendation, rather than inferred from the presence of a test file.

## Next closure sequence

1. Perform .NET restore/build and review package dependency advisories on the exact branch SHA.
2. Run the source checks and scoped unit/contract/integration tests on an E:-backed disposable environment.
3. Execute and reconcile the private collector, Tempo, Prometheus and Grafana startup, with listener, privacy and storage bounds checked.
4. Close missing behavior and evidence for all 75 assertions, including metrics, tracing, worker, alerts, browser, failure and performance.
5. Independently review actual source-locked artifacts for G17-S25. Record explicit acceptance only after that review and a separate decision.

No historical failed or pending gate evidence is altered by this candidate status.
