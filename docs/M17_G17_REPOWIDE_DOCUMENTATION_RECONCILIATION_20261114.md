# G17 Repository-Wide Documentation Reconciliation, 2026-11-14

**Current G17-S25 status, November 14, 2026:** [Hosted run #43589790235](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43589790235) finished **FAILED** at executable SHA `e238a48013948f28c8ab5042583e6e6bfaaa4553`. Exact-source preflight, 319 unit, 60 contract, 55 M17 integration and 34 accepted regression tests passed without required skips. Prometheus exposed a firing `HarborOpsDatabaseUnavailable` alert, but the browser did not render its label in the local Grafana alert table. The feature-branch correction sets the panel target to table format, narrows the browser witness to the actual alert panel, and adds fail-closed configuration and raw-evidence negative checks. This new candidate has **not** passed a complete hosted verification. The earlier green run #43541279851 is evidence for its own older source only. All S25-F01 through F08 remain open until new raw evidence is reviewed, and F06 requires a separate signed reviewer decision. Protected `main`, the M9 transport hold and accepted data protections are unchanged. **G17 NOT ACCEPTED; acceptance pending.**

**Executable source SHA:** `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`.
**Hosted verification (historical complete green result):** [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851), FULL CAMPAIGN SUCCESS on the executable SHA above. Historical pending text below is preserved as the earlier checkpoint, not current status.
**Gate:** G17 NOT ACCEPTED; acceptance pending. The prior independent S25 review returned NOT PASS.

## Verified recovery

The previous [#43538965276](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43538965276), source `d8412e71d6adf0aedaa0683af9f0c14fc650e079`, FAILED at the live 5xx alert stage. It passed 319 unit, 60 contract, 53 M17 integration and 34 accepted regression tests without skips, as well as the disposable PostgreSQL and telemetry network stages. The raw archive's database-fault report confirms 120 genuine HTTP 503 responses with no observed firing failure-rate alert, a restored database, and unchanged durable business counts. A 35-file manifest was verified but the full campaign is incomplete, not green.

The corrective source seeds a zero-valued bounded `business/5xx` counter on heartbeats after registration, then requires an exported counter baseline followed by 120 HTTP 503s spread across multiple exporter and scrape intervals. The verifier asserts real positive 5-minute counter increase, at least 100 eligible requests, failure fraction above the unchanged 1 percent threshold, a firing alert and database recovery. Dedicated C# and Node tests plus positive/negative evidence assertions were added. These changes are not accepted until a new complete source-locked runtime campaign and independent S25 review finish.

Historical hosted #43537932173 and #43538289383, all previous G17 failures, and the separate passing local c7 archive remain preserved without alteration. The original S25 findings and raw-evidence requirements remain in force.

## Living documents reconciled

- Entry and project governance: `README.md`, `docs/STATUS.md`, `docs/ROADMAP.md`, `docs/DECISIONS.md`, `docs/DOCUMENT_STATUS_GUIDE.md`
- M17 implementation and planning: `docs/M17_PLANNING_STATUS.md`, `docs/M17_IMPLEMENTATION_PLAN.md`, `docs/M17_IMPLEMENTATION_CANDIDATE_STATUS_20261112.md`, `docs/M17_ARCHITECTURE_DESIGN.md`, `docs/M17_OPERATIONS_RUNBOOK.md`, `docs/M17_POLICY_PROPOSAL_20261112.md`
- Verification and evidence: `docs/M17_VERIFICATION_PLAN.md`, `docs/M17_G17_S25_TEST_READINESS_20261113.md`, `docs/M17_G17_S25_REMEDIATION_20261114.md`, `docs/M17_G17_VERIFICATION_RECOVERY_20261113.md`, `docs/M17_G17_LOCAL_VERIFICATION_RESULT_20261114.md`, `docs/M17_G17_REPOWIDE_DOCUMENTATION_RECONCILIATION_20261113.md`
- Historical M17 correctness: both source-locked M17 2026-11-13 correctness remediation reports.

The former assertion in `DOCUMENT_STATUS_GUIDE.md` that M17 runtime implementation and G17 execution were unstarted is corrected. Superseded 2026-11-12 and 2026-11-13 summaries are clearly labeled historical. The local c7 report distinguishes its original unpushed state from subsequent publication. All acceptance statements remain NOT ACCEPTED. Documentation checkpoints must not be confused with runtime evidence.

## Source and graph boundaries

The authoritative execution graph lives in `docs/execution-graph.json` with `docs/EXECUTION_GRAPH.md` generated from it. No dependency or gate acceptance changed, so the graph was intentionally not rewritten. This prevents divergence between source and generated graph. Protected `main`, accepted G13/G15/G16 and Turkish outcomes, the M9 hold, M15 data policies and earlier G17 failure evidence remain unchanged.

This reconciliation is a documentation-only descendant of executable SHA `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`, tested by [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851). The earlier [#43540639907](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43540639907) is a separate failed source and must never be called green. A later runtime result requires a new dated documentation checkpoint and cannot retroactively make a failed run pass.


## Contract regression follow-up and new source lock, 2026-11-14

After the initial checkpoint above, [#43540639907](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43540639907) FAILED during contract tests. It passed preflight and 319 unit tests, but the old `EnabledTelemetryRecordsExactlyOneRequestDurationAndFailureObservation` test counted both the new zero-valued first-incident seed and one real positive failure as two failures. The corrected test requires the zero baseline, exactly one positive increment of one, an aggregate positive count of one and precisely one duration observation. It retains the no-duplicate behavior and the original production 5xx thresholds.

The new executable SHA is `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`. [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851) passed the no-skip preflight; full verification remains pending at this dated checkpoint. All earlier failure archives remain source-locked, and the S25-remediated 72 technical witness requirements plus 3 independent review requirements are unchanged.


## Verified final hosted result, November 14, 2026

The latest source-locked G17 run [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851) passed both the no-skip source/assertion preflight and the full disposable campaign, with no failed or skipped required stages. Its evidence records 47/47 stages PASS, 72/72 technical assertions PASS, and three separate independent S25 review assertions pending. All 25 G17 scenario IDs are present. Raw TRX totals are 319 unit, 60 contract, 54 M17 integration and 34 accepted regressions, with zero failed and skipped results. The 24-case dashboard Playwright run reports zero unexpected, flaky or skipped cases. Export privacy, malformed-context traces, worker journey, real DB and 5xx alerts, cardinality, retention and measured overhead passed.

The manifest contains 45 signed file entries, zero missing required artifacts and source `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`. The downloaded GitHub archive contains 43 of those entries with matching hashes and omits two hidden `.last-run.json` Playwright metadata files. This packaging difference is explicitly for independent S25 consideration. The current feature branch adds only documentation beyond the executable verified SHA; protected main has not changed.

**Current disposition: technically verified, NOT ACCEPTED.** Independent G17-S25 must inspect the raw artifacts, source-bound assertion witnesses, privacy evidence and P09 aggregation decision before acceptance is considered. [Detailed green record](M17_G17_TECHNICAL_VERIFICATION_GREEN_20261114.md).
