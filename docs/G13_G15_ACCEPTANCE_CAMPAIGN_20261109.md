# G13/G15 Combined Acceptance Preparation

**G13/G15 source-locked acceptance checkpoint, 2026-11-12:** [Run 43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445) is in progress at `c6d2dc86e30913b8d67a011131f9980422e4c197`. Preflight, browser, process crash, G13 rollback/concurrency, G15 notification load/fanout and security checks passed at inspection; G13/G15 mutants, source-locked scale, database and final evidence reconciliation were still pending. [Run 43120072016](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43120072016) ended FAILURE: 13 technical successes but one G15 locked-intent mutant INVALID after a 400-second timeout. The current fixture changes the five-second delay to once per transaction without lowering the SKIP LOCKED behavioral expectation. [Run 43093329982 attempt 2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982/attempts/2) passed all technical jobs but failed the full atomic assertion gate. Evidence cannot be pooled across source SHAs, and source-only fixes are not executed proof. Approved G13-S20 Option A: 250,000 members per report and 1,000,000 active tenant members, with four live reports per tenant and two per user. Historical G13 round 3 independent review returned NOT PASS. **G13/G15 NOT ACCEPTED**, pending final source-locked technical PASS, independent S25 reviews, and explicit decisions. No merge, deployment, Turkish integration or retention deletion. See [run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) and [2026-11-12 reconciliation](REPOWIDE_DOCUMENTATION_RECONCILIATION_20261112.md). The prior source-bound facts below remain historical.

**Current review decision (2026-11-10):** The [15-job G13/G15 technical run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543 passed TRX cases, 227/227 mapped technical assertions and source integrity PASS. The separate [G13/G15 S25 review](G13_G15_REVIEW_MATRIX_20261110.md) and [formal decision](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record G13 NOT ACCEPTED and G15 NOT ACCEPTED. G13 permanent quota/idle cleanup and G13/G15 exact negative mutation and HTTP-outage proof remain outstanding. Earlier status snapshots below are historical and apply only to their own source SHAs. No main merge, deployment, cleanup or activation.
**Current technical evidence (2026-11-10):** [15-job run 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`, including 541 passing TRX tests, no failures/skips, and 48 passing technical scenario contract checks. The separate 242-assertion audit has 224 technical witnesses present, 3 missing, 7 policy-review and 8 independent-review items. Full G13/G15 acceptance remains pending. [Current source-locked evidence](G13_G15_VERIFIED_EVIDENCE_20261110.md).

**Historical preparation campaign:** The nine-job record below is preserved for its own SHA. The latest 15-job source-locked G13/G15 technical verification passed on 2026-11-10. Remaining acceptance work is tracked in the current evidence record, not the older candidate ledger.

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
