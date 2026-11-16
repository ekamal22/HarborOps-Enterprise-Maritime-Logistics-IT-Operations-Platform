# G17-S25 Gap Closure and Repository Test Correctness Audit

**Current G17 decision (2026-11-16):** P09 is **QUALIFIED PASS** based on four of five complete passing runs, preserving the f6 FAIL, the unexplained burst and the approved 10% pooled p95 / 15% paired mean memory limits. Optimization is deferred to pre-release readiness or an earlier jointly agreed remediation if source-locked verification fails. [Decision](M17_G17_P09_QUALIFIED_PASS_DECISION_20261116.md). **G17 NOT ACCEPTED:** final exact-source verification and independent S25-A01/A02/A03 sign-off remain pending.

**Recorded:** 2026-11-14. **Source tested:** `5b1dcc41d5b19dcbacd66de38193dc5020964647`. **Gate:** G17 NOT ACCEPTED; independent S25 review pending. Result record: [local verification result](M17_G17_S25_LOCAL_VERIFICATION_RESULT_20261114.md).

No hosted workflow was used and nothing was pushed. Historical findings, failed attempts and raw evidence are preserved. Closure below means "implemented, adversarially tested and observed in a complete source-locked local run". It is not a reviewer disposition, and no finding is signed off by the contributor who changed the source.

## 1. Gap register

| ID | Origin | Finding | Disposition at `5b1dcc41` |
| --- | --- | --- | --- |
| S25-F01 | review | Request rate, 5xx and p95 panels under zero or insufficient traffic | **Closed locally; pending review.** A real defect sat behind it: with 20 genuine requests the 5xx panel read a healthy-looking `0` (the failure counter is seeded at zero) and the p95 panel `NaN`. The panels now read recording rules guarded by the approved 100 eligible request floor. Promtool covers zero traffic, below-floor traffic, a positive control and a mutation. A new stage records a real below-floor window, and all three engines render Unknown for 5xx and p95 and a measured rate for request rate |
| S25-F02 | review | One page-originated fault joined to trace, log, alert and rendered panel | **Closed locally; pending review.** Hosted failure #43593741977 root cause reproduced: the alert panel body is queried lazily, so off-screen it renders only its title (19 characters at 1280x720 in all three engines). The no-scroll control fails with the hosted message and the scrolled witness passes. The witness now uses the exact saved panel container and requires `alertname` and `firing` as table cells. Spoofed header and wrong-correlation controls retained |
| S25-F03 | review | Worker and service log, metric and trace participation and privacy bytes | **Closed locally; pending review.** All eight owned services are scanned and must be running; worker traces from the worker service and worker series are required; hash-consistent credential, account, tenant and canary-prefix injections are rejected by the independent verifier |
| S25-F04 | review | Hidden Playwright `.last-run.json` records | **Closed locally; pending review.** Both are required artifacts, content-checked, manifest-indexed and tamper-tested. A failing spec produced `status: failed` live, which the verifier rejects |
| S25-F05 | review | P09 raw recalculation and outlier disclosure | **Recalculated; qualified.** Independent oracle tests cover the FAIL path, outlier disclosure and budget edges. f7 pooled +9.3 percent and +13.79 percent memory, pair 3 outlier disclosed. f6 on identical source failed (+12.7 percent). Budgets and method unchanged |
| S25-F06 | review | Separate reviewer and signed recommendation | **Open.** No sign-off exists or was fabricated. No independence exception is recorded |
| S25-F07 | review | Active worker lease across restart | **Closed locally; pending review.** In-process integration test plus a live stage with a seeded lease, real API stop and restart, forged fences, duplicate finish and in-flight cancellations |
| S25-F08 | review | Real telemetry exhaustion, positive history, measured degradation | **Closed locally; pending review.** Measured degradation and recovery, fixture-owned M8, M11, M15 and M16 rows with row-level hashes, whole-schema hash equality |

### Gaps found in this audit (all registered, corrected and covered by regression tests)

| ID | Class | Gap | Correction |
| --- | --- | --- | --- |
| S25-N01 | defect | Dashboard 5xx and p95 panels showed a healthy-looking value below the sample floor | See F01 |
| S25-N02 | test | Alert-panel witness located its panel by walking ancestors and text heuristics | Exact saved panel container, scroll first, cell-level check |
| S25-N03 | test, vacuous | The held-worker alert-suppression witness had no stale backlog, so "no alert" could not fail | Stale eligible work (at least 300 s) is created while the worker is stopped; observed 600 to 660 s with no alert; the restarted worker drains it |
| S25-N04 | test | The S18-A03 "repeat suppression" witness only tested clearing; removing `keep_firing_for` still passed | New promtool scenario that fails without `keep_firing_for` |
| S25-N05 | test | S19-A01 asserted panel titles, not live rendered values | Rendered Ready and measured request rate from the exact panel containers |
| S25-N06 | test isolation | The lease restart test passed only after some other test had created the baseline row | The test seeds its own baseline and passes alone |
| S25-N07 | test | Regression matrix accepted one executed test per class | Every declared method must have executed; source skips refused |
| S25-N08 | test | The "tenfold concurrency" figures were constants | Both concurrencies are now the values actually used |
| S25-N09 | test, weak | Cancellation never reached the server (all fast aborts, zero traces found) | In-flight cancellation against a held table lock; 6 of 60 found in Tempo |
| S25-N10 | verifier | The final manifest verifier's complete-campaign branch was almost untested (118 of 150 sampled mutants survived) | Synthetic complete campaign with 80 single-fact negatives plus 26 header and ledger guards; 1 of 120 survived afterwards |
| S25-N11 | verifier | Raw witness predicates contained inert clauses | Single-fact and exact-edge controls; 254 of 262 mutants killed afterwards |
| S25-N12 | test | The M16 witness counted all audit rows and compared only counts | Fixture-owned row plus row-level SHA-256 before and after |
| S25-N13 | test | Worker recovery demanded exactly one heartbeat series while the stopped process was still retained | Recovery is a different process instance advancing its heartbeat |
| S25-N14 | scan scope | Third-party logs contain schema names such as `user.email` (Grafana migration ids) | Infrastructure logs are scanned for per-run canaries and credential shapes; attribute-name tokens apply to HarborOps logs and exports. Disclosed, not hidden |
| S25-N15 | stage logic | The outage stage required five steps while it records six; found only when the stage first ran to the end | Six ordered names |
| S25-N16 | pre-existing | `scripts/check-planning-docs.mjs` fails at the starting commit on an M11 assertion | Not touched; unrelated accepted-milestone documentation |

## 2. Repository test correctness audit

**Corpus inventory:** 319 unit and 60 contract tests (all run), 590 .NET test methods parsed across the three projects, 55 M17 integration tests and 34 accepted regression tests in this campaign, 5 web Node tests, 222 Node source and negative-control cases, 25 Playwright dashboard and cardinality cases across three engines, promtool rule fixtures, and the live fault, privacy, retention, restart, storage, performance and worker stages.

| Group | Checked | Deficiency found | Fix and control |
| --- | --- | --- | --- |
| Skip, ignore, only, retry markers | Whole test corpus | Earlier-milestone Playwright specs skip when their fixture environment is not set. The M17 configs have `forbidOnly`, `retries: 0` and no skips. Custom .NET fact attributes also skip without their environment | Skipped, flaky or zero-executed cases already fail the milestone summarizers and the G17 TRX and Playwright contract. Regression accounting now compares declared with executed methods |
| .NET assertion density | 590 methods | One method asserted only by not throwing | Made explicit. The few other zero-direct-assert methods assert through helpers |
| M17 .NET oracles | metric schema, telemetry, contract, restart lease | Lease test order dependence (N06) | Fixed; mutation of the claim query (allow stealing an active lease) makes it fail |
| Negative-control oracles | 111 `assert.throws` calls | Two negatives caught only a missing file | Retained as missing-artifact controls; others catch the intended message |
| Evidence gating code | 14 modules, 637 operator mutants sampled | Inert clauses and an untested complete-campaign branch (N10, N11) | New single-fact, edge and full-campaign tests; 568 of 637 killed on the final pass, with the manifest verifier at 119 of 120 |
| Dashboard and browser | 3 engines | Heuristic scoping, title-only live check (N02, N05) | Exact containers |
| Alerts | promtool | Repeat suppression untested (N04); sufficiency guard missing (N01) | New scenarios; mutations confirmed to fail them |
| Fault, restart, storage, worker stages | live | Vacuity, constants, weak cancellation (N03, N08, N09, N12, N13, N15) | See register |

**Negative mutations used to prove a control can fail:** guard weakened to `>= 0` (promtool fails), `keep_firing_for` removed (fails at 11 and 13 minutes), active leases made claimable (integration test fails), alert observer without the scroll step (fails with the hosted message), plus the operator mutation campaign over the evidence modules.

**Residual weaknesses, stated plainly**
- **Historical, superseded on 2026-11-15 by the [mutation survivor classification](M17_G17_S25_MUTATION_SURVIVOR_CLASSIFICATION_20261115.md):** the figure below is preserved as originally reported; its manifest portion is retired as unusable and every survivor is now classified individually.
- 69 of 637 sampled mutants survived. Most are `&&` to `||` flips behind earlier equivalent guards. `reconcile-g17-assertions.mjs` has the weakest coverage (24 of 57 killed): its witness derivation is exercised only by a real campaign, and the mapping from test names to the ledger is therefore proven by f7 itself, not by unit tests.
- The P09 comparison uses raw floating point percentages; a value exactly on a budget edge could be misjudged by rounding noise. Real measurements do not land on the edge, and the approved budgets were not altered.
- Third-party service logs are not scanned for the `user.email`-style attribute names (see N14).
- The Playwright specs for milestones before M17, the remaining integration classes and the G13/G15 crash and capacity campaigns were not rerun here.

## 3. Boundaries

Protected main, accepted milestone branches, the M9 transport hold, approved M15 fail-closed behaviour, M16 audit protections, M17 default-off behaviour and existing API contracts are unchanged. The only production-side change is the dashboard and recording-rule configuration under `monitoring/m17`.
