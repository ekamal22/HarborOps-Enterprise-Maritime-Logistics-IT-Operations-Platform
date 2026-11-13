# M17 Correctness Audit Round 2 Remediation

**Historical correctness checkpoint:** This register preserves the recorded findings and candidate remediations from its original review. Later work authored all 72 technical assertion witnesses, attempted source-locked G17 runs and recorded new failures and fixes. Do not interpret this historical review as the latest test status. **G17 NOT ACCEPTED; acceptance pending.** [Current readiness](M17_G17_S25_TEST_READINESS_20261113.md) | [Run and failure record](M17_G17_VERIFICATION_RECOVERY_20261113.md).

**Recorded:** 2026-11-13
**Branch:** `feature/m17-observability-20261112`
**Gate:** G17 NOT ACCEPTED
**Status:** Remediation source candidate. Execution evidence and independent S25 review pending.

## Finding dispositions

These rows describe implemented corrective source, not executable passes. No finding is accepted as closed until the source-locked test and independent review evidence exists.

| ID | Priority | Source remedy | Required verification |
| --- | --- | --- | --- |
| F17-R2-01 | High | Exporters reject profiles lacking `OTEL_SPAN_EVENT_COUNT_LIMIT=0` and `OTEL_SPAN_LINK_COUNT_LIMIT=0`. Both trace processors also clear trace state and status descriptions, and continue to allowlist tags. | Compiled tests plus actual OTLP serialization with hostile events, links, trace-state, exception status and canary values |
| F17-R2-02 | High | Export-drop test annotation synchronized with live alert. Recovery fixtures added for backlog. | `promtool check rules` and `promtool test rules` against exact source |
| F17-R2-03 | High | Explicit `-VerifyDisposableWorker` mode layers the preexisting M9 worker profile only into a fresh, verified G17 database. A separate worker probe observes restricted-role readiness, advancing telemetry, stop, suppressed stale alert and restore. | Actual disposable worker process, restricted role integration TRX, real processed/reconciliation/stale/retry dispositions and safe receipt correlation |
| F17-R2-04 | High | Browser reporter and traces write directly to the E: evidence root. The runner records stage failures, emits `run-status.json` and creates a failure manifest in `finally`, independently of successful browser tests. | Intentionally fail each stage and confirm raw artifacts, exact SHA and no false completed state |
| F17-R2-05 | Medium | Backlog firing is retained until two fresh sub-60-second samples, while held/unavailable worker suppression applies immediately. | Prometheus pending/firing/clearing fixture plus real worker stop/recovery |
| F17-R2-06 | Medium | Manifests require a run ledger and expected artifact set, reconcile raw TRX counters, Playwright stats, network evidence, cardinality and worker results when selected, and reject fabricated complete campaigns. Failed runs remain flagged incomplete. | Run manifest positive and negative controls, verify missing/altered/fake passing outputs rejected |
| F17-R2-07 | Medium | Three-browser test allowance now exceeds the 95-second exporter polling window. The cardinality test has a separate longer budget. | Chromium, Firefox and WebKit results with no time-budget cancellation |
| F17-R2-08 | Medium | Durable accepted receipt and detached worker diagnostic spans use the same HMAC-derived 10-percent decision without fabricating a cross-process parent span; each retains the independent 200-span-per-second safety ceiling. | Captured paired sampling at normal traffic, privacy canary, and controlled rate-cap partial-pair proof |
| F17-R2-09 | Medium | 10,000-path campaign now queries the complete Prometheus series history across the test window, asserts a stricter combined 500 custom-series ceiling, and records Docker API working-set samples during the workload. | Raw historical series and RAM samples; separate matched telemetry-off paired runs remain needed for P09 |
| F17-R2-10 | Medium | Monitoring backends use an isolated internal `telemetry_monitor` network. API and worker use a separate internal `telemetry_ingest` network. The web container remains on the application network and cannot join either. | `docker compose config`, actual container/network labels and private-network ownership results |

## Test and evidence behavior

The runner uses a fresh E: evidence directory and a separate sibling directory for disposable PostgreSQL storage. It refuses an existing Compose project identity or a different Git SHA. Optional `-VerifyDisposableWorker` is separate from a default observability-only run, and cannot be enabled without `-StartDisposableStack`. This is a disposable test capability only; the held M9 worker remains disabled on the normal application stack.

The `expected-artifacts.json` file and `run-status.json` are included in the hashed evidence manifest. A failed campaign may be integrity-verified as FAILED but never counted as COMPLETE. A COMPLETE claim requires all selected raw witnesses and zero failed stages, including owned-service cleanup when requested. Evidence integrity is distinct from correctness or gate acceptance.

The tracking receipt and worker source spans retain separate trace roots. The keyed digest permits diagnostics to relate them without exporting raw receipt or correlation IDs. The paired sampling decision is deterministic on that digest. The throughput ceiling may independently reject spans during overload; such loss must be recorded and reviewed.

## Remaining blockers to G17

1. Restore, compile, and run all changed .NET source with warnings as errors. Inspect package API compatibility and direct test assertions.
2. Run the fresh Compose project and assert network ownership, base health compatibility, collector startup, local login, metrics, OTLP trace serialization, Prometheus alerts and browser behavior.
3. Execute restricted worker disposition fixtures and the explicit disposable worker fault probe. The held M9 transport process must not be enabled in any retained stack.
4. Execute the 10,000-hostile-request series test and collect memory, trace sampling, collector failure, storage and privacy evidence. Complete three matched telemetry-on/off benchmark pairs for P09.
5. Reconcile all 25 G17 scenarios and 75 atomic assertions against raw source-locked witnesses. Perform a separate independent G17-S25 review and explicit acceptance decision.

**State unchanged:** Protected main is not altered, G17 is NOT ACCEPTED, no deployment or external notification route is approved, and retained histories must not be removed.
