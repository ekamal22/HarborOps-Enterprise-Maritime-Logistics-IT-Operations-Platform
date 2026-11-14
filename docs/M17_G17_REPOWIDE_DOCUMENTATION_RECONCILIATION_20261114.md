# G17 Repository-Wide Documentation Reconciliation, 2026-11-14

**Executable source SHA:** `a236ab64574cfec8b0dcbde232a5d22ef086783b`.
**Hosted verification:** [#43540639907](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43540639907), no-skip preflight PASS and full campaign pending at the time of this record.
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

This reconciliation is a documentation-only descendant of the executable SHA used in [#43540639907](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43540639907). A later runtime result requires a new dated documentation checkpoint and cannot retroactively make a failed run pass.
