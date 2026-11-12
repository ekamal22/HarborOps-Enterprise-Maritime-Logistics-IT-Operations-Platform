# M17 Correctness Remediation Register

**Recorded:** 2026-11-13
**Feature branch:** `feature/m17-observability-20261112`
**Base main:** `b490fb8cfc879cdf05cb1f7484b99f6a9b264ed3`
**State:** Source remediation candidate. Runtime verification and acceptance pending.

## Disposition rule

A source change and a corresponding test implementation are not executable verification evidence. The findings below have candidate fixes implemented on the feature branch, but none may be declared independently closed until the tests run against the exact source SHA and their raw results are reviewed. G17 remains NOT ACCEPTED with 0 of 25 scenarios and 0 of 75 atomic assertions executed and reviewed.

| Finding | Corrective implementation | Required executable proof |
| --- | --- | --- |
| F17-001 Grafana image | Optional stack uses `grafana/grafana:13.3.1` rather than the obsolete OSS repository | Pin and verify image digest, Compose pull/startup and dashboard provisioning |
| F17-002 stale database state | M17-only 15-second readiness sampling, sample timestamp and stale-data panel suppression | PostgreSQL stop/recovery, two consecutive failed/fresh samples, unchanged health endpoint contracts |
| F17-003 misleading tracking outcomes | Exact `Processed`, `NeedsReconciliation`, and `Stale` disposition mapping | Restricted worker fixture for processed, parked, stale, retry and lease races |
| F17-004 collector vs app availability | Application heartbeat freshness, collector reachability and separate collector scrape checks | Kill exporter/collector independently, check absent-data and recovery |
| F17-005 notification state and count | Distinct enabled/running state, actual nonzero delivered counts and failure attempts | Opt-in worker startup, idle polls, failed batches and shutdown |
| F17-006 incomplete alerts | Local recording/alert rules for 5xx, p95, database, backlog, telemetry absence, exporter losses plus rate-limit visibility; hysteresis and held-worker suppression | `promtool check rules`, `promtool test rules`, actual trigger, clear and startup/noise observations |
| F17-007 exporter and queue drops | Bounded trace batch processors, SDK `otel.sdk.experimental` queue-loss metrics, counting OTLP failed submissions and separate sampling drops | Collector outage, queue saturation, raw metric family mapping and timed recovery |
| F17-008 incomplete diagnostic path | Safe baseline DB span and HMAC-based linkage through existing durable receipt correlation without storing raw IDs in telemetry | Source-lock HTTP-to-DB and intake-to-worker trace inspection with privacy canaries |
| F17-009 unsafe integration DB | Separate E-backed verification Compose file, unique project and dedicated port, refusing inherited database targets | Verify role, volume, image and project identity before integration; original resources unchanged |
| F17-010 unrestored faults | Collector fault probe always invokes restoration in a `finally` block | Intentionally fail a fault assertion and prove cleanup path runs |
| F17-011 superficial dashboard tests | Authenticated Playwright checks actual Prometheus state and held/disabled worker state | Chromium, Firefox, WebKit results compared with raw collector samples, anonymous access denial |
| F17-012 missing evidence files | Hash all supported report, screenshot, recording and trace files; reject unlisted, mismatched and canary-bearing files; verify manifest independently | Source-locked raw artifacts, positive and negative manifest mutation checks; manual binary privacy review |
| F17-013 synthetic cardinality only | Add real HTTP 10,000-request hostile-path campaign and exported Prometheus series census | Less than or equal to 500 active custom series/service and bounded process memory |
| F17-014 permanently failing gate test | Validate scenario/atomic inventory and require complete witnesses only if accepted; source-only checks do not enforce perpetual NOT_ACCEPTED | Fail test on fabricated accepted state, independent G17-S25 decision |

## Instrument and data-flow baseline

- API metrics: bounded `harborops.request.duration` histogram in seconds with explicit `0.8` and `1` second buckets; `harborops.request.failures` counter with fixed operation and status class.
- PostgreSQL readiness: `harborops.db.readiness` gauge and `harborops.db.readiness_sample_unix_seconds` gauge, sampled every 15 seconds when the optional profile is active.
- Tracking worker: read-only eligible backlog age in seconds and finite outcome/worker-state dimensions. M9 transport is never activated merely to populate monitoring.
- Trace export: ten-percent deterministic trace-ID sampling, provisional limit of 200 admitted spans per process-second and batch capacity 2,048 records. SDK queue-full and failed OTLP batch submission counters are separate, and TCP reachability is an advisory signal, not delivery confirmation.
- Cross-process correlation: a private 256-bit key computes an HMAC digest of the existing server-generated receipt correlation. Raw receipt IDs, organization IDs, credential material and correlation IDs are never trace attributes. Separate process roots are associated by the digest, not by a fabricated shared parent span.
- Local stack: separate verification project, owned E:-backed database and evidence paths, loopback-only Grafana, no host-published collector receiver, and bounded isolated telemetry tmpfs. Retained application database volumes are not touched or pruned.
- Metrics and dashboard queries require a raw collector series-name reconciliation: OpenTelemetry and Prometheus translation are not assumed equivalent until observed.

## Required closure, not yet done

1. Check exact current SHA, package/image digests, NuGet restore, compilation with warnings as errors and complete changed-source unit and contract tests.
2. Start a new disposable M17-only PostgreSQL/Compose profile. Confirm identity and network isolation and run the restricted integration campaign.
3. Run `promtool` rule parsing and deterministic alert fixtures, then verify actual trigger/clear transitions and no false positives for held/disabled services.
4. Execute Chromium, Firefox and WebKit dashboard tests, raw-export metric naming, 10,000 hostile paths, source-locked trace canaries and 500-series census.
5. Simulate API/worker restart, exporter queue-full, collector/network outage, storage exhaustion, and cancellation without business side effects.
6. Collect three paired latency/memory benchmarks against the same source and workload, document P02/P04/P05/P09 calibrations, preserve all raw evidence and pass manifest-negative tests.
7. Resolve all 75 G17 atomic assertion dispositions and conduct a separate independent G17-S25 review. G17 acceptance requires an explicit separate decision.

**Prohibitions unchanged:** No GitHub Actions dispatch, production telemetry, external paging, unapproved deployment, M9 hold release, default-on dispatcher/projector/evaluator, retained-history deletion, or merge to protected main.
