# G13/G15 Atomic Acceptance Coverage and Behavioral Verification

**Verification checkpoint, 2026-11-10:** [G13/G15 run 42705372608](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42705372608) executed source `6b254196dbfce3489f90c024e79fb9f9e4a7b5ea`. Thirteen jobs passed, including three-engine browser tests, combined PostgreSQL regressions, security mutations, one-million-row feed, process-crash recovery, and accepted-milestone regressions. The 170,000-shipment KPI test failed with HTTP 503 after crossing its existing 15-second SQL timeout. The evidence-reconciliation job correctly failed because that scale evidence is missing. Later source changes have **not** passed a complete campaign. A fail-closed preflight must pass the 170,000-shipment scale test, new M13/M15 cross-milestone regressions, and all 18 G13 source mutants before dispatching another full workflow. The [atomic inventory](G13_G15_ASSERTION_INVENTORY_20261109.json) contains 242 requirements: 227 candidate test-mapped, zero partially mapped, and 15 policy or independent-review items. Test mapping is not passing evidence or approval. G13/G15 acceptance remains pending, with no merge or deployment.

**Source-locked rerun checkpoint (2026-11-09):** [Run 42703411975](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42703411975) at `8c19d94d439e3faf6957208b79b6332ed98f91c4` passed 12 execution jobs. The browser job failed because the Chromium logout scenario revoked the reusable manager cookie before Firefox; Firefox was then refused access. PostgreSQL acceptance failed because the invalid-query fixture exceeded the six-request per-minute reporting limit. The downstream evidence reconciliation correctly failed. The disposable browser fixture now refreshes its manager session between engines, and malformed-query probes use two authorized accounts without changing application rate limits. A new exact-SHA campaign is required. Current source classification is 214 candidate fully tested, 13 partially tested, and 15 manual/policy assertions; no milestone acceptance, merge or deployment.

**G13/G15 2026-11-09 test campaign correction:** [Run 42701028600](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42701028600) at `c6528206f2ebb91ac99b83090817251bbd0b3ee4` passed 12 execution jobs but failed the browser runner's obsolete 7/3 case-count assertion and the queued-notification authorization race wait detector; the final evidence gate failed accordingly. The workflow now requires 10 notification and 8 KPI browser cases per engine, and the queued mutation test observes the PostgreSQL function call holding the lock. Both changes need an exact-SHA rerun. Previously passing work is historical, not successor acceptance. No acceptance, merge or deployment is granted.

**Historical G13/G15 coverage checkpoint (2026-11-09):** The most recent successful [15-job run #42632573516](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42632573516) verified commit `50ed2950bceaf3478172c3f9d9bf85186f874082`. New source-locked behavioral suites and the [atomic coverage inventory](G13_G15_ASSERTION_INVENTORY_20261109.json) were added later on `verify/g13-g15-evidence-closure-20261109` and have not passed GitHub Actions at that newer SHA. The 242 atomic requirements are currently classified as 214 with candidate full behavioral coverage, 13 partially covered, and 15 needing policy or independent review. Candidate means source-mapped, **not verified**. The accepted main baseline, manual reviewer S25 gates, G13 permanent storage decision, M15 P10 conditional review and M15 P12 cleanup prohibition remain unchanged. No main merge or deployment has occurred.

**Updated:** 2026-11-09
**Working branch:** `verify/g13-g15-evidence-closure-20261109`
**Disposition:** Acceptance pending. No merge, deployment, production migration, activation or retention cleanup authorized.

## Source-locked checkpoints

The last fully successful execution is [G13/G15 workflow 42632573516](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42632573516), at commit `50ed2950bceaf3478172c3f9d9bf85186f874082`. It passed 15 of 15 GitHub Actions jobs, including 48 separately recorded technical scenario evidence checks. This run is not evidence for code subsequently authored on the same branch.

## Assertion inventory and assessment

The committed [assertion inventory](G13_G15_ASSERTION_INVENTORY_20261109.json) decomposes 50 scenarios into 242 separately classified assertions: 227 require automated technical evidence and 15 are policy or independent-review items. This is a **source-reviewed candidate coverage map**, not a claim that all tests have been executed on the newest commit.

| Classification | Assertions | Interpretation |
| --- | ---: | --- |
| Candidate fully tested | 227 | Candidate source-linked witnesses must pass against the exact new source SHA before they can count |
| Partially tested | 0 | An existing test covers part of a compound requirement; an explicit behavioral review or extra test is still required |
| Manual policy or independent review | 15 | Cannot be auto-approved through CI |
| **Total** | **242** | A passing run cannot approve policy or independent-review items |

The `scripts/audit-g13-g15-assertions.py` tool emits `assertion-audit.json` and `assertion-audit.md`. Missing/failed witnesses and partial categories remain explicit. `--enforce-complete` must fail while any automated assertion remains partially covered or missing. Each evidence file is retained with the same-run SHA-256 manifest. A separate `scripts/check-g13-g15-scenarios.py` gate checks 48 technical scenario contracts.

## New behavioral coverage added after the successful run

- G15: live notification list/count/detail/action denial after 11 distinct session, account, membership and organization changes; a queued acknowledgment loses authority after a committed change.
- G13: previously persisted report tokens and overview after 12 independent revocation modes, plus an organization-lock queued report that must not publish after a committed security-version change.
- G13/G15: direct SQL denials for restricted API and worker roles attempting to enumerate personal replay/outbox data or modify KPI materialization.
- G13: origin, destination, active/future voyage assignment and allowed record-source filtering with exact member IDs; invalid UTC and filter requests leave report state unchanged.
- G13: empty tenant and Draft-only denominator behavior; exact percentage rendering for 1/3, 2/3 and 8/9 in the browser.
- G15: three approved event actions versus Reevaluated, InputUnavailable and Acknowledged; unique immutable history IDs; low-noise MarkRead, exactly-once acknowledgment audit and same-key replay; archived target scope and historical event preservation.
- UI: notification dependency error/retry, delayed old-organization response denial, KPI empty-data error/retry and exact displayed ratios.
- G13 performance: the existing 170,000-shipment / 30-successful-report sample retains the approved p50 <= 15 seconds and p95 <= 20 seconds envelope. A separate final overcapacity query seeds 16,665 extra Draft shipments, proves the 500,000-member cap fails closed with HTTP 413, and verifies that its failed transaction does not replace the prior committed report.
- G13 measured provisional resource ceilings: 2 GiB additional report-table storage across the 30-report exercise, and 1 GiB peak working set. These are verification bounds justified by the prior 1.63 GB final-size and 609 MB peak-memory observation, **not approved permanent retention or production service limits**.

## Required workflow

Manually dispatch [Verify G13 G15 final integrated acceptance campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/workflows/verify-m11-m12-combined.yml) on the exact working branch HEAD, after these changes. Both manual workflow entry files must remain identical. The existing source, PostgreSQL, browser, scale, mutation, SIGKILL, dependency and accepted-milestone regression jobs remain mandatory. All newly added tests execute in the exclusive disposable PostgreSQL server or three-engine browser job, and the same-run gate reconciles their TRX or Playwright results. If a required test fails, neither a skipped case nor a renamed test can substitute for passing evidence.

## Remaining acceptance blockers

1. Resolve the 89 partially covered atomic assertions by inspecting each actual test body and implementing a dedicated missing behavioral case where necessary. Do not mark a scenario fully accepted just because one candidate test passed.
2. The permanent G13 report storage, quota, pruning and retention resource policy remains subject to explicit measured numerical approval. The temporary 2 GiB verification exercise ceiling is not permanent authority.
3. M15-P10 requires source-locked rollback, process-kill recovery and accepted-M11 regression proof before its conditional approval is fully satisfied. M15-P12 allows provisional retention periods but no data deletion.
4. G13-S25 and G15-S25 require independent review of all raw evidence and explicit milestone decisions. These cannot be replaced by an automated PASS.

This campaign may establish that all currently enumerated **technical tests** passed. It cannot grant policy authority, sign off an independent review or authorize a main merge by itself.
