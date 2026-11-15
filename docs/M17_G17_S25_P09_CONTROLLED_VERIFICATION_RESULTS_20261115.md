# G17-S25 Controlled P09 Verification Results

**Recorded:** 2026-11-15. **Plan:** [controlled verification plan](M17_G17_S25_P09_CONTROLLED_VERIFICATION_PLAN_20261115.md), pre-registered and pushed before any counted run, with Amendment 1. **Gate:** G17 NOT ACCEPTED; independent S25 review pending. Nothing here is a signed review.

## 1. Result

Three additional complete P09 measurements were taken, as planned, with the approved gate, budgets and method unchanged and every result retained. **All three passed.** Each is also a complete 49-of-49-stage campaign with a manifest that verifies (54 files, no missing artifact).

| Measure | r1c | r2 | r3 |
| --- | --- | --- | --- |
| Baseline pooled p95 | -7.83 percent | +3.82 percent | -7.83 percent |
| Authorized pooled p95 | +0.76 percent | -0.67 percent | +2.55 percent |
| Mean paired peak memory | +12.53 percent | +13.35 percent | +13.51 percent |
| Budget | 10, 10, 15 | 10, 10, 15 | 10, 10, 15 |

Across the five complete runs of record (f6, f7 and these three) the gate passed four times and failed once. **f6 remains a recorded failure and is not discarded.** The pre-registered reading for "all three pass" applies: P09 passed in 4 of 5 complete runs of record and failed in 1.

## 2. What differs from the plan, disclosed

1. **Source.** The three counted runs executed source `8d5e24137ad2943a6b2787b6472e4cea7bd9947e`, which is the tested source `5b1dcc41` plus one verification-stage fix, not the identical `5b1dcc41` of f6 and f7. See section 3. The P09 measurement script, its verifier, the budgets and the method are byte-identical to `5b1dcc41`.
2. **Two aborted attempts**, both before the P09 stage, both retained and neither a measurement. Both of the two permitted replacements (decision D2) were therefore used, and none remains.
3. **Host preparation.** The preflight failed on 8 tries before r1b started and on 3 tries before r2 started because of desktop load. With the owner's agreement Chrome was closed and the WinZip file-association helper `FAHWindow64.exe` was stopped by this session. No threshold was changed.
4. **Diagnostics overhead.** Host counters, Docker events and a Docker VM pressure sidecar ran for the whole of each counted run; f6 and f7 had none.
5. **Burst threshold.** The 5-times-the-row-median definition of a burst row was chosen after seeing f6 and f7 and before any new run.
6. **Host was not idle during the rows.** The preflight demands a quiet host, but while the rows were measured the host CPU averaged 13.8 to 21 percent (the benchmark client and the Docker VM are part of that), and free memory fell to 2.78 GiB at its lowest (r3), slightly under the 3 GiB preflight floor.
7. **Analysis script corrections.** The host-attribution part of `analyze-controlled.mjs` was corrected before anything was interpreted: row windows were first misaligned by the benchmark's one-off provisioning containers (which carry the `api` service label) and by the final telemetry restore. The corrected windows are about 36 seconds per row, matching the stage length. The gate verdicts and the statistics do not depend on that part.

## 3. The two aborted attempts and the source fix

| Attempt | Stage that failed | Cause |
| --- | --- | --- |
| a1 | `postgres-readiness-alert-and-durable-recovery` (29) | The fresh clean worktree had no `tests/browser/node_modules`; a launcher gap, fixed by installing the lockfile's package set before the preflight |
| r1b | `malformed-context-real-tempo-export` (37) | A latent defect in the verification source: the stage queried Tempo with a start equal to the second the injection began, the 960 requests finished inside that second, Tempo capped the end at its own clock and answered 400 |

The second defect was reproduced on a disposable stack: the unfixed script failed 2 of 8 runs with the identical 400 and Tempo logged `start` equal to `end`; the fixed script passed 8 of 8. The fix (`tempoSearchWindow`) starts the search window one second early, adds two unit tests, and its module has 15 of 15 mutations killed by the strict harness. It is commit `8d5e2413` on `verify/g17-s25-p09-source-20261115` and commit `1374779f` on the review branch. **This is a defect in the G17 verification source that can abort the campaign on a quiet, fast host, so a reviewer rerunning the original `5b1dcc41` could hit it.**

## 4. Detail

### 4.1 Verdicts per run of record

| Run | Verdict | Baseline pooled p95 | Authorized pooled p95 | Mean paired peak memory | Execution order |
| --- | --- | --- | --- | --- | --- |
| f6 (source 5b1dcc41) | FAIL | +3.59 percent | +12.71 percent | +5.86 percent | 1-off 1-on 2-on 2-off 3-off 3-on |
| f7 (source 5b1dcc41) | PASS | -1.90 percent | +9.30 percent | +13.79 percent | 1-off 1-on 2-on 2-off 3-off 3-on |
| r1c (source 8d5e2413) | PASS | -7.83 percent | +0.76 percent | +12.53 percent | 1-off 1-on 2-on 2-off 3-off 3-on |
| r2 (source 8d5e2413) | PASS | +3.82 percent | -0.67 percent | +13.35 percent | 1-off 1-on 2-on 2-off 3-off 3-on |
| r3 (source 8d5e2413) | PASS | -7.83 percent | +2.55 percent | +13.51 percent | 1-off 1-on 2-on 2-off 3-off 3-on |

### 4.2 Pairs (percent increase)

| Run | Pair | Baseline p95 | Authorized p95 | Memory |
| --- | --- | --- | --- | --- |
| f6 | 1 | +13.60 | +84.07 | +6.64 |
| f6 | 2 | +7.17 | -16.27 | +10.53 |
| f6 | 3 | -1.35 | -16.98 | +0.41 |
| f7 | 1 | -7.67 | +5.23 | +13.19 |
| f7 | 2 | -12.34 | +7.96 | +13.60 |
| f7 | 3 | +17.54 | +11.10 | +14.59 |
| r1c | 1 | -9.95 | -10.48 | +15.22 |
| r1c | 2 | -1.57 | +3.57 | +12.24 |
| r1c | 3 | +5.01 | +21.82 | +10.12 |
| r2 | 1 | +2.06 | -4.20 | +14.49 |
| r2 | 2 | +8.83 | +4.27 | +13.19 |
| r2 | 3 | +2.60 | -0.29 | +12.39 |
| r3 | 1 | -12.64 | +6.39 | +11.20 |
| r3 | 2 | -12.63 | -4.19 | +13.14 |
| r3 | 3 | +10.82 | +6.25 | +16.20 |

### 4.3 Rows (authorized route)

A burst row is a row whose worst 100-sample window p95 is at least 5 times its median on either route.

| Run | Row | Mode | Authorized p95 (ms) | Median (ms) | Worst 100-sample window over median | Peak memory (MiB) | Burst row |
| --- | --- | --- | --- | --- | --- | --- | --- |
| f6 | 1 (round 1) | off | 57.1 | 33.74 | 2.27x | 122.0 | no |
| f6 | 2 (round 1) | on | 105.11 | 35.3 | 8.38x | 130.1 | yes |
| f6 | 3 (round 2) | on | 50.71 | 29.17 | 3.25x | 136.5 | no |
| f6 | 4 (round 2) | off | 60.56 | 34.77 | 2.22x | 123.5 | no |
| f6 | 5 (round 3) | off | 48.57 | 29.54 | 1.98x | 122.9 | no |
| f6 | 6 (round 3) | on | 40.32 | 25.34 | 2.64x | 123.4 | no |
| f7 | 1 (round 1) | off | 37.88 | 25.61 | 1.8x | 122.1 | no |
| f7 | 2 (round 1) | on | 39.86 | 25.47 | 1.88x | 138.2 | no |
| f7 | 3 (round 2) | on | 43.95 | 27.14 | 2.01x | 139.5 | no |
| f7 | 4 (round 2) | off | 40.71 | 26.63 | 1.77x | 122.8 | no |
| f7 | 5 (round 3) | off | 40.73 | 26.17 | 1.74x | 121.3 | no |
| f7 | 6 (round 3) | on | 45.25 | 27.6 | 5.32x | 139.0 | yes |
| r1c | 1 (round 1) | off | 44.41 | 27.13 | 1.88x | 122.2 | no |
| r1c | 2 (round 1) | on | 39.76 | 26.38 | 2.14x | 140.8 | no |
| r1c | 3 (round 2) | on | 40.55 | 25.65 | 2.28x | 136.6 | no |
| r1c | 4 (round 2) | off | 39.15 | 25.96 | 1.75x | 121.7 | no |
| r1c | 5 (round 3) | off | 36.75 | 25.87 | 1.73x | 121.5 | no |
| r1c | 6 (round 3) | on | 44.77 | 27.96 | 2.49x | 133.8 | no |
| r2 | 1 (round 1) | off | 42.73 | 25.7 | 2.07x | 121.5 | no |
| r2 | 2 (round 1) | on | 40.94 | 25.99 | 1.69x | 139.1 | no |
| r2 | 3 (round 2) | on | 40.04 | 25.22 | 2.05x | 137.3 | no |
| r2 | 4 (round 2) | off | 38.4 | 24.99 | 1.77x | 121.3 | no |
| r2 | 5 (round 3) | off | 39.71 | 25.38 | 1.96x | 121.1 | no |
| r2 | 6 (round 3) | on | 39.59 | 25.43 | 1.88x | 136.1 | no |
| r3 | 1 (round 1) | off | 39.72 | 26.15 | 1.64x | 121.4 | no |
| r3 | 2 (round 1) | on | 42.26 | 27.67 | 1.85x | 135.0 | no |
| r3 | 3 (round 2) | on | 41.04 | 26.64 | 1.78x | 137.8 | no |
| r3 | 4 (round 2) | off | 42.83 | 27.14 | 1.89x | 121.8 | no |
| r3 | 5 (round 3) | off | 41.42 | 27.03 | 1.88x | 121.6 | no |
| r3 | 6 (round 3) | on | 44.02 | 26.51 | 2.05x | 141.3 | no |

### 4.4 Host state during each row of the new runs

The window of a row runs from its API container start to the next one; the last row ends at the final telemetry restore.

| Run | Row | Mode | Window | Host CPU mean | Host CPU peak | Minimum free memory (MiB) | Docker VM CPU pressure (some avg10, max) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| r1c | 1 | off | 37 s | 21 percent | 58.4 percent | 3208 | 0.37 |
| r1c | 2 | on | 36 s | 19.3 percent | 61.5 percent | 3241 | 0.29 |
| r1c | 3 | on | 36 s | 17.7 percent | 40.4 percent | 3156 | 0.25 |
| r1c | 4 | off | 36 s | 18.1 percent | 38.6 percent | 3198 | 0.26 |
| r1c | 5 | off | 37 s | 17.2 percent | 41.5 percent | 3196 | 0.34 |
| r1c | 6 | on | 36 s | 19.6 percent | 43.4 percent | 3165 | 0.52 |
| r2 | 1 | off | 36 s | 16.2 percent | 48.6 percent | 3055 | 0.4 |
| r2 | 2 | on | 36 s | 16.2 percent | 39.8 percent | 3102 | 0.29 |
| r2 | 3 | on | 35 s | 17.3 percent | 37.6 percent | 3034 | 0.18 |
| r2 | 4 | off | 36 s | 13.8 percent | 34.7 percent | 3041 | 0.15 |
| r2 | 5 | off | 36 s | 16.2 percent | 35.4 percent | 3050 | 0.18 |
| r2 | 6 | on | 36 s | 14.6 percent | 35 percent | 3014 | 0.18 |
| r3 | 1 | off | 37 s | 16.6 percent | 57.1 percent | 2945 | 0.57 |
| r3 | 2 | on | 36 s | 20.6 percent | 47.8 percent | 2904 | 0.3 |
| r3 | 3 | on | 36 s | 17.6 percent | 41.9 percent | 2912 | 0.43 |
| r3 | 4 | off | 36 s | 18.4 percent | 32.7 percent | 2874 | 0.18 |
| r3 | 5 | off | 36 s | 15.7 percent | 36.9 percent | 2842 | 0.18 |
| r3 | 6 | on | 36 s | 19.2 percent | 48.5 percent | 2844 | 0.18 |

The r1c and r3 baseline figures both round to -7.83 percent from different p95 values (18.27 and 16.84 ms against 19.68 and 18.14 ms); the 0.01 ms resolution of the raw samples makes such a coincidence possible.


## 5. Characterization across all five runs (not a gate)

| Measure | Value |
| --- | --- |
| Pooled increase, baseline route | -0.76 percent, 95 percent block-bootstrap interval -6.34 to +4.20, share of resamples above 10 percent: 0 |
| Pooled increase, authorized route | +1.01 percent, interval -3.07 to +5.32, share above 10 percent: 0.1 |
| Burst rows (5 times the row median or more) | 2 of 15 telemetry-on rows (f6 round 1, f7 round 3), 0 of 15 telemetry-off rows; one-sided exact p = 0.24, which does not distinguish them |
| Burst rows in the 18 rows of the new runs | 0 |

## 6. What this does and does not establish

- **Latency.** In the three new runs the authorized pooled increase is +0.76, -0.67 and +2.55 percent against a 10 percent budget, and the pooled estimate over all five runs is +1.01 percent with an interval well inside the budget. f7's +9.30 percent and f6's +12.71 percent now look like the high tail of a noisy measurement, not the typical overhead. The latency margin is no longer thin on this evidence.
- **The f6 failure is not explained.** None of the 18 new rows contains a burst, so the host-attribution tooling had nothing to attribute, and f6 has no host data. The cause of its one-row burst remains undetermined: a host or VM stall, a runtime warm-up effect and an intermittent telemetry cost cannot be separated. Three clean runs lower the suspicion of a systematic cost; they do not prove the f6 burst was external.
- **Memory is now the tighter dimension.** Telemetry-on peak working set is about 136 MiB against about 122 MiB with telemetry off (means of the six rows of each mode across five runs), and the mean paired increase is +12.53, +13.35, +13.51 percent in the three new runs and +13.79 percent in f7. That leaves 1.2 to 2.5 points to the 15 percent budget. f6's +5.86 percent is the low outlier (its telemetry-on rows read 130.1, 136.5 and 123.4 MiB). Individual pairs exceed 15 percent (r1c pair 1 at +15.22, r3 pair 3 at +16.20), which the approved mean rule allows but which shows the memory result is not comfortably clear of the budget.
- **Five runs are a sample, not proof.** All runs are on one machine and one Docker configuration; the gate verdict of each run stands on its own.

## 7. Evidence and reproduction

Raw P09 files (SHA-256): f6 `19266f48...ef970`, f7 `5bf29d8b...ba3f6f`, r1c `a6702950...b99c8`, r2 `28980fc8...494d7`, r3 `ba7249a4...93cd4`. The complete evidence of each run is under `E:\HarborOps-Verification\g17-s25-audit\p09-controlled\` (`r1c`, `r2`, `r3` evidence, diagnostics with hash lists, preflight records, driver and runner logs, plan commit files). A compact copy (raw P09 files, run status, manifests, driver logs, preflight records, diagnostics hash lists, the analysis output and the launcher) is in `docs/m17-s25-p09-controlled-protocol-20261115/results/`. The diagnostics themselves (about 6 MB per run) stay on the evidence drive and are identified by their hashes.

```
node docs/m17-s25-p09-controlled-protocol-20261115/analyze-controlled.mjs <out.json> f6=<raw> f7=<raw> r1c=<raw> r2=<raw> r3=<raw> --diagnostics r1c=<dir> --diagnostics r2=<dir> --diagnostics r3=<dir>
```

## 8. Decisions that remain with the owner

1. Whether P09 may be treated as passed for acceptance purposes, given 4 of 5 complete runs of record, the unexplained f6 failure and the thin memory margin. This work records the evidence and does not decide it.
2. Whether the review should rely on the fixed source (`8d5e2413` and review-branch commit `1374779f`) for any further campaign, since `5b1dcc41` can abort on the Tempo window defect. The f7 campaign remains valid evidence for `5b1dcc41`; the three runs here are complete campaigns for `8d5e2413`.
3. The independent S25 A01, A02 and A03 and S25-F06 remain open; G17 remains NOT ACCEPTED.
