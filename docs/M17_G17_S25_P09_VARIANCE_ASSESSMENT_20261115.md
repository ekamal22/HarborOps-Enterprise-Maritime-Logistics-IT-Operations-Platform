# G17-S25 P09 Variance Assessment: f6 versus f7

**Recorded:** 2026-11-15. **Source of both runs:** `5b1dcc41d5b19dcbacd66de38193dc5020964647` (identical). **Gate:** G17 NOT ACCEPTED; independent S25 review pending.

This is a read-only analysis of preserved raw evidence. The approved method (pooled p95 over three matched pairs against 10 percent, mean paired peak memory against 15 percent) and both budgets are unchanged. Nothing was rerun, tuned, selected or discarded for this assessment. Both runs remain on record exactly as measured.

## 1. Preserved evidence

| Run | Raw evidence | SHA-256 of `m17-performance-raw.json` | Run outcome |
| --- | --- | --- | --- |
| f6 | `E:\HarborOps-Verification\g17-s25-audit\final-f6-evidence` (66 files, hash tree in `preserved-20261115\f6-evidence.sha256`) | `19266f48763343bce366ebde0a7cea35d74af2ea204cba77eb6052961e7ef970` | FAILED at stage 44, three paired overhead measurements; later stages not run |
| f7 | `E:\HarborOps-Verification\g17-s25-audit\final-f7-evidence` (54 files plus manifest, hash tree in `preserved-20261115\f7-evidence.sha256`) | `5bf29d8bab9931bb00f56988c511388a2a2216b8d6ec8f6502f759bbacba3f6f` | COMPLETE, 49 of 49 stages |

Each raw file holds all six measured rows per run (3 rounds, telemetry on and off), each with 1,200 raw latencies per route, the sampled working-set readings and the recorded pairs and pooled results. The full computed analysis is `preserved-20261115\p09-variance-analysis.json` beside the evidence. The analysis script re-derived both outcomes with the approved `recalculateP09` first: f6 reproduces as FAIL and f7 as PASS, matching what the campaign recorded.

An archive named `g17-final-f7-evidence.zip` (SHA-256 `1b830a403e5d11913599a9c425e82f0010a6f2981a08270dc654ff9277232198`) was found inside the f7 evidence directory after the run. It was not created by the verification runner or by this work and is not in the manifest, and the unlisted file made `verify-g17-manifest.mjs` fail on the directory. The repository owner confirmed that they created it and that it may be removed. It was moved, not deleted, to `preserved-20261115\owner-made-g17-final-f7-evidence.zip` with its hash unchanged. The 54 manifested files were never modified, and the manifest now verifies in place on `final-f7-evidence` for source `5b1dcc41` (complete, no missing artifacts).

## 2. Comparison under the approved method

| Measure | f6 | f7 | Budget |
| --- | --- | --- | --- |
| Pooled p95, baseline route (off, on) | 20.06 ms, 20.78 ms: **+3.59 percent** | 18.41 ms, 18.06 ms: **-1.90 percent** | 10 |
| Pooled p95, authorized route (off, on) | 55.96 ms, 63.07 ms: **+12.71 percent** | 39.77 ms, 43.47 ms: **+9.30 percent** | 10 |
| Mean paired peak memory | **+5.86 percent** | **+13.79 percent** | 15 |
| Outcome | FAIL (authorized p95) | PASS | |

Per pair (baseline p95, authorized p95, memory), in percent:

| Pair | f6 | f7 |
| --- | --- | --- |
| 1 | +13.60, **+84.07**, +6.64 | -7.67, +5.23, +13.19 |
| 2 | +7.17, -16.27, +10.53 | -12.34, +7.96, +13.60 |
| 3 | -1.35, -16.98, +0.41 | **+17.54, +11.10**, +14.59 |

Both runs used the same execution order (round 1 off then on, round 2 on then off, round 3 off then on), the same 160-request warm-up per mode, concurrency 8, 1,200 samples per route per mode per round, the same 12 CPU, 16.8 GB machine and the same preceding stage.

## 3. Why f6 failed and f7 passed

The difference is concentrated in one measured row.

- **f6, round 1, telemetry on, authorized route** carries a severe tail: p95 105.1 ms against 35.3 ms median, p99 237.6 ms, maximum 487.9 ms, 133 of 1,200 samples above twice the median (the other five f6 rows have 10 to 28). Its worst 100-request window sits at the very start of the row (samples 0 to 99) with a window p95 of 295.6 ms, about eight times the row median.
- Remove that single row's pair and the f6 pooled authorized increase is **-17.1 percent**. This is a sensitivity figure only. It is not an alternative acceptance result and was not used for any decision.
- f6's other two telemetry-on authorized rounds measured 50.7 ms and 40.3 ms, faster than their telemetry-off partners (60.6 ms and 48.6 ms).
- **f7** has the opposite shape: all six rows are tight (authorized p95 37.9 to 45.3 ms, standard deviation 7.7 to 14.3 ms), with one smaller burst in round 3 telemetry-on (a window p95 of 146.8 ms around samples 740 to 840, 27 samples above twice the median) that lifts that single pair to +11.1 percent while the pooled result stays at +9.3 percent.

So both runs contain one telemetry-on row with a transient latency burst (f6 round 1 at the start of the row, f7 round 3 late in the row), and none of the six telemetry-off rows of the two runs does (their authorized maxima are 51 to 119 ms). Which side of 10 percent the pooled authorized figure lands on is decided by whether such a burst falls in the pooled data and how large it is.

### Uncertainty of a single run

A block bootstrap (2,000 resamples, blocks of 50 consecutive samples, fixed seed) of the pooled authorized increase gives:

| Run | Estimate | 95 percent interval | Share of resamples above 10 percent |
| --- | --- | --- | --- |
| f6 | +12.71 | -5.51 to +65.94 | 59.6 |
| f7 | +9.30 | +3.05 to +14.89 | 41.9 |

The f7 pass is a point estimate 0.7 point under the budget with an interval that spans it. The f6 interval is far wider because of the single burst. The same-route round-to-round range of the telemetry-off configuration, with no telemetry involved, is 21.6 percent of its mean in f6 and 7.2 percent in f7 (authorized) and 12.9 and 13.8 percent (baseline). That noise floor alone is as large as, or larger than, the 10 percent budget for several rows.

All twelve rows together (six pairs, 7,200 samples per mode and route, same pooled p95 estimator): baseline **+1.92 percent**, authorized **+8.15 percent**. This is a characterization, not a replacement gate.

### Memory

The telemetry-off peak working set is stable across all six rows of both runs (121.3 to 123.5 MiB). The telemetry-on readings vary (f6: 130.1, 136.5, 123.4 MiB; f7: 138.2, 139.5, 139.0 MiB), which is why the mean memory result moved from +5.86 to +13.79 percent between runs. The "peak" is the largest of about sixteen point-in-time `docker stats` readings per row, so it depends on when garbage collection falls relative to the sampling. The across-run mean of all six telemetry-on rows is +9.8 percent over telemetry-off. f7's +13.79 percent leaves 1.2 points to the 15 percent budget.

## 4. What the evidence supports and does not support

Supported by the raw data:
1. The f6 failure is caused by one row's transient latency burst, not by a uniform shift of telemetry-on latency. Without that row the pooled increase would not be near the budget.
2. The measurement is noisy enough that the 10 percent budget lies inside the sampling uncertainty of a single three-pair run, in both directions.
3. Across the twelve measured rows of the two runs (six telemetry-on, six telemetry-off), both severe bursts are in telemetry-on rows: 2 of 6 telemetry-on rows and 0 of 6 telemetry-off rows.

Not established:
- **Root cause of the burst.** The procedure records no timestamps per sample and no host or Docker VM telemetry, so a host or VM scheduling stall, runtime warm-up (the round 1 burst began at the start of the measured window, immediately after a 160-request warm-up on a freshly recreated API container) and a genuine intermittent cost of the telemetry path cannot be separated. With two bursts in twelve rows, 2 of 6 against 0 of 6 is not statistically distinguishing.
- **Whether the overhead is comfortably within budget.** The evidence says the pooled estimates are around 8 to 9 percent for the authorized route with wide uncertainty, not that they are safely below 10.

## 5. Recommendation

**Additional controlled verification is advisable before unconditional acceptance of P09.** It is characterization, not a retry loop, and it needs the owner's approval because it concerns an approved measurement procedure:

1. Decide in advance and record the number of additional complete P09 measurements (for example three) and report every one, whatever it shows. No result may be selected, discarded or repeated until a preferred figure appears.
2. Keep the approved method and both budgets unchanged for the gate. State beforehand that the gate is evaluated on each run as approved, and that the pooled characterization across all runs is reported separately as context.
3. Capture host and Docker VM load, CPU steal and a timestamp per sample alongside the existing raw data, so a burst can be attributed to the host or to the telemetry path.
4. Treat any change to warm-up length or to how peak memory is read as a methodology amendment that only the owner can approve. This assessment does not make one.
5. Keep the machine otherwise idle, as for f6 and f7.

The owner recommended exactly this design (three additional complete measurements, run count fixed beforehand, all results retained, host-load diagnostics, thresholds and method unchanged). It is written out, with its counting rule, preflight gate, diagnostics and pre-registered analysis, in the [controlled verification plan](M17_G17_S25_P09_CONTROLLED_VERIFICATION_PLAN_20261115.md). It has not been executed and needs the decisions listed there.

If the owner prefers not to run more measurements, the honest status is: P09 passed once and failed once on identical source under the approved rule, with a thin and variable margin. That qualification should travel with any acceptance decision.

## 6. Disposition

f6 is preserved in full and is not discarded or reinterpreted. f7 stands as the complete passing campaign of record for the tested source, qualified as above. No budget, method, sample count or stage ordering was changed by this assessment.
