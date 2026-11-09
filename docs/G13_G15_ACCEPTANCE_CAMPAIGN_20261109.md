# G13/G15 Combined Acceptance Preparation

**Latest verified technical campaign (2026-11-09):** [Run 42569970541](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42569970541) passed all 15 jobs at source `e4bf9cd1527f85f554baf623e90805d96d08ac1a`, with 487 passed TRX cases, zero failed/skipped, all six browser suites passing, and artifact integrity PASS. This is **not** full milestone acceptance. The final ledger reports G13 and G15 each 1 PASS, 22 BLOCKED and 2 NOT EXECUTED. The [source-locked evidence review](G13_G15_SOURCE_LOCKED_EVIDENCE_REVIEW_20261109.md) records outstanding plan, capacity, recovery and mutation proof. Merge, production migration, deployment and runtime activation remain pending.


**Prepared:** 2026-11-09.
**Campaign branch:** `verify/g13-g15-acceptance-campaign-20261109`.
**Frozen proven predecessor:** `3150e5b67bace1801dfc3b02ff53c423b5af931c`, five-job [PASS run 42552035939](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42552035939).
**Disposition:** Nine-job campaign executed as [run 42555586856](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42555586856), PASS at `ef2dd697df6c4a2dc8b53c55e7968ac37e41a745`. The full scenario ledger still recorded only G13-S02 and G15-S02 PASS; remaining gate scenarios and acceptance are open. The newer eleven-job candidate is documented separately in [parallel completion](G13_G15_PARALLEL_COMPLETION_20261109.md).

## Added checks

- G13: old report-member snapshots remain fixed after M5 shipment completion while new reports reflect changed current state. This is a sequential persistence test, not two-session concurrency evidence.
- G15: 105 unique personal receipts, three pages with limit 50 and no duplicate IDs after an in-flight read-state change. This is limited paging coverage, not cursor expiry, session replay or million-row performance.
- G13: isolated PostgreSQL failure/recovery, nine source mutants, two SQL RLS mutants and the 170,000-shipment workload.
- G15: four deliberate mutations of event capture, personal RLS, membership-version authorization and dispatcher proof. Invalid compile or runner failures never count as killed mutations.
- Post-run evidence reconciliation: same-run artifact download, source SHA and SHA-256 verification, TRX, six Playwright results and mutation manifests, with explicit G13-S01..S25 and G15-S01..S25 dispositions.

## How to run

Historical dispatch: `.github/workflows/verify-m11-m12-combined.yml` on the campaign branch. Nine jobs completed PASS: combined source, PostgreSQL, Chromium/Firefox/WebKit, 170k scale, G13 SQL mutations, G13 faults, G13 source mutations, G15 mutants and gate ledger. Every PostgreSQL job uses an exclusive disposable service. The dispatcher is enabled only in ephemeral browser fixtures. Evidence retention is 30 days.

## Remaining work and honest classification

The approved D-005 12+2 fixture has specific K07-K13 M11 source memberships and an 8/9 coverage expectation. The existing twelve-A/two-B test uses a structurally different fixture with missing M11 evaluations and does not reconcile that full ledger. Implement the complete reference oracle before G13-S03..S10 can be fully PASS.

G13 also requires two-session permission/snapshot interleavings, after-commit process crash recovery, realistic concurrency and skewed-volume budgets, and complete security-negative scenario proof. G15 requires cursor expiry/session swap, long backlog and fanout scale, pre/post-commit crash recovery and the rest of the required mutation/security matrix. Policy decisions M15-P01..P14 remain pending. See [policy recommendations](M15_POLICY_RECOMMENDATIONS_20261109.md).

The run's evidence ledger verified exact-source checksums and recorded G13: 1 PASS, 14 BLOCKED, 10 NOT EXECUTED; G15: 1 PASS, 15 BLOCKED, 9 NOT EXECUTED. The evidence tool intentionally distinguishes technical workflow success from complete milestone acceptance.

## Review boundary

The requested reviewer can independently inspect raw logs, results, manifests and exact source without trusting implementation claims. As the same session authored verification changes, this does not provide organizational separation of implementation and review. G13-S25 and G15-S25 must not be represented as independently satisfied without an accepted reviewer-separation decision. Preserve historical failed and passing runs.

No main merge, deployment, production migration or production activation of M15 dispatcher, M10 projector, M11 evaluator or parked M9 events is authorized.
