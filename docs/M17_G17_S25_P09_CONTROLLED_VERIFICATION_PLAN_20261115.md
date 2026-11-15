# G17-S25 Controlled P09 Verification Plan (pre-registered, not executed)

**Recorded:** 2026-11-15. **Status:** PLANNED. No additional measurement has been run. **Gate:** G17 NOT ACCEPTED; independent S25 review pending.

This plan implements the recommendation in the [P09 variance assessment](M17_G17_S25_P09_VARIANCE_ASSESSMENT_20261115.md): three additional complete P09 measurements, with the run count fixed beforehand, every result retained, host-load diagnostics collected, and the existing thresholds and method unchanged. Planning them is not approval to change the measurement policy, and it is not permission to disregard any result. The plan is committed before any run so that the design, the counting rule and the analysis cannot be adjusted after seeing data.

## 1. What stays fixed

| Item | Value (unchanged from the approved method and from f6 and f7) |
| --- | --- |
| Gate | Pooled p95 over three matched pairs at most 10 percent for the baseline and the authorized route; mean paired peak memory at most 15 percent |
| Per run | Three rounds in the order off then on, on then off, off then on; 160-request warm-up per mode; concurrency 8; 1,200 samples per route per mode per round; memory from `docker stats` |
| Measurement code | `scripts/check-g17-performance.mjs` at source `5b1dcc41d5b19dcbacd66de38193dc5020964647`, unmodified |
| Verifier | `scripts/check-g17-p09-review.mjs` and `recalculateP09`, unmodified |
| Machine | The same 12-CPU, 16.8 GB Windows host and the same Docker Desktop configuration (12 CPUs, about 8.15 GB), checked by the preflight |
| Evaluation | Each run is judged alone, exactly as approved. Results pooled across runs are characterization and never replace a run's own verdict |

## 2. Fixed run count and counting rule

1. **Exactly three measurements**, named `r1`, `r2` and `r3`.
2. **What counts as a measurement:** an attempt in which `m17-performance-raw.json` was written with six rows, three pairs and a result of PASS or FAIL. Both PASS and FAIL count. A failing run is reported as failing; there is no rerun to obtain a pass.
3. **What does not count:** an attempt that stops before the P09 stage produced its raw file (for example an earlier stage fails, the Docker daemon stops, the machine sleeps, or the preflight cannot be satisfied). It is retained, labelled `aborted`, and is not a measurement. **Owner decision D2:** this plan proposes at most two replacement attempts in total, each only for an aborted attempt, with the owner informed; an attempt whose P09 stage completed is never replaced, whatever it shows.
4. No row, pair, round or run is excluded, trimmed or reweighted. Outliers are reported, not removed.

## 3. Design of one run

Each run is one complete campaign run with the flags used for f6 and f7 (`-StartDisposableStack -FaultProbe -VerifyDisposableWorker -RequireFullG17 -StopOwnedContainers`), because the gate is evaluated in that context: the P09 stage follows the cardinality-memory stage on a freshly built stack, takes about 4 minutes, and the whole run takes 34 to 38 minutes (f6: 34 minutes, f7: 38 minutes). Three runs therefore take about two hours of measurement plus cooldowns. A stage-only harness was rejected: it would need a source change (so the source would no longer be the identical `5b1dcc41`) and its numbers would not be comparable with f6 and f7.

Per run, in order:

1. Create a clean worktree at the tested SHA: `git worktree add --detach E:\HarborOps-Verification\g17-s25-audit\p09-controlled\src-rN 5b1dcc41d5b19dcbacd66de38193dc5020964647`. The runner of that commit is used, so the executed code is the tested code. The final verifiers (current branch head) are used afterwards to check the evidence, as for f7.
2. Run `preflight.ps1` (section 4). If it fails, wait and repeat; a failed preflight is recorded and is not an attempt.
3. Start `diagnostics.ps1 -Action Start` for the run's project (section 5).
4. Launch the runner in the supervised background with a verified completion monitor (section 7), evidence directory `E:\HarborOps-Verification\g17-s25-audit\p09-controlled\rN-evidence`, project `ho-verify-m17-p09-rN`.
5. On completion: stop diagnostics, record the exit code and the stage ledger, copy the P09 raw file hash, verify the manifest, remove the finished owned project's containers and networks by project label (never volumes or unrelated objects), and wait at least 10 minutes before the next preflight.
6. Do nothing else on the machine during steps 3 to 5.

## 4. Preflight gate (objective go or no-go)

`preflight.ps1` observes and changes nothing. It writes `preflight.json` with every check and its observed value and exits non-zero if any check fails. **Owner decision D3:** the thresholds below are proposals. They are not derived from f6 or f7, because those runs recorded no host state.

| Check | Rule |
| --- | --- |
| Mains power | AC power or no battery |
| Host CPU | Mean at most 10 percent and peak at most 40 percent over 60 seconds |
| Free memory | At least 3 GiB |
| Competing work | No mutation, test, build, Playwright or verification process running |
| Antivirus and servicing | No scan running and no servicing worker running (unknown is recorded, not failed) |
| Docker configuration | 12 CPUs and about 8.15 GB, as in f6 and f7 |
| Leftover project | No `ho-verify-m17-*` container, running or stopped |
| Evidence drive | At least 20 GiB free on `E:` |
| Source identity | Worktree HEAD equals the tested SHA and is clean |

The power scheme is recorded (it is Balanced today). Changing it would change the conditions relative to f6 and f7, so the plan keeps it and records it (decision D5).

**Observed while preparing this plan:** a dry preflight on the current working session failed the CPU check (mean 20 percent, peak 69 percent) and the free-memory check (2.99 GiB), with the usual desktop applications open. These runs therefore need a dedicated quiet window with other applications closed. Separately, the machine was observed to pause for hours earlier on this date (a long wall-clock gap during a mutation job), so the runs also need the machine kept awake, and any gap in the host counters during a run is treated as an interruption.

## 5. Diagnostics (no change to the measurement)

`diagnostics.ps1` reads counters only. It does not touch the owned project, its containers, its network or the measurement script.

| Source | Content | Resolution |
| --- | --- | --- |
| Host counters (`typeperf`) | CPU, privileged CPU, available memory, disk time, the Docker VM process CPU | 1 s |
| Docker events | Container lifecycle of the owned project; every measured row starts with a new API container, so the six row windows are known | event time |
| Docker VM sidecar | `/proc/stat` (including steal) and CPU, memory and IO pressure (PSI) read inside the Docker VM | 1 s, whole-second timestamps |

Limits, stated plainly. Per-sample timestamps cannot be added without changing the measurement script, so a burst is attributed to its row and to a window by index, not to an exact second. The sidecar and the counters add about one process wake per second for the whole run, present in all six rows alike; this is a disclosed difference from f6 and f7, which were measured without it (**decision D6**). Tooling tests so far: the diagnostics were run for about 10 seconds with a labelled test container and captured counters, events and VM pressure; the analysis was dry-run on the preserved f6 and f7 data and reproduces their approved verdicts (f6 FAIL at +12.71 percent, f7 PASS at +9.30 percent).

## 6. Pre-registered analysis

`analyze-controlled.mjs` is committed with this plan. It reports, for every run of record (f6, f7, r1, r2, r3):

1. **The approved verdict** from `recalculateP09`: pooled p95 per route, mean memory, per-pair figures, outcome.
2. **Per-row tail statistics:** median, p95, p99, maximum, count above twice the median, and the worst 100-sample window p95.
3. **Burst rows.** A row is a burst row when its worst 100-sample window p95 is at least 5 times the row median on either route. This threshold was chosen after seeing f6 and f7 (their two severe rows are 8.4 and 5.3 times the median; every other row of both runs is below 4.0) and before any new run; it is disclosed here because it is not independent of those two runs.
4. **Characterization across all five runs:** pooled increase per route over every run, a block bootstrap interval (blocks of 50 samples, 2,000 resamples, fixed seed 12345), the share of resamples above 10 percent, the number of runs passing, and the number of burst rows among telemetry-on and telemetry-off rows with a one-sided exact test. None of these is a gate.
5. **Host attribution for each burst row** of the new runs: host CPU and available memory and the Docker VM CPU pressure within the row's window.

Interpretation rules, fixed now:

| Outcome of r1 to r3 | Statement that will be made |
| --- | --- |
| All three pass | P09 passed in 4 of 5 complete runs of record and failed in 1; the f6 failure stays on record; the pooled characterization is reported next to it |
| At least one fails | P09 is reported as intermittently failing, with the number of failures out of five and each failing run's cause analysis; no run is repeated to obtain a pass; remediation is for the owner to decide |
| Bursts coincide with host load | Reported as host-attributed. The run is **not** excluded and its verdict is unchanged |
| Bursts occur with no host signal | Reported as unexplained, which keeps open the possibility that the telemetry path contributes |

Nothing in the analysis can change a budget, the method, or the verdict of a run.

## 7. Supervision, continuity and integrity

Following the standing rules for unattended work:

1. **Notification path first.** Before the first run a short harmless background task is launched to confirm that completion notifications reach the session.
2. **Status file.** `E:\HarborOps-Verification\g17-s25-audit\p09-controlled\STATUS.md` records before each launch: process or task ID, source SHA, plan commit SHA, start time, expected duration (38 minutes), evidence location, completion criteria (run state COMPLETE or FAILED in `run-status.json`, process exit code, raw P09 file present) and the next action (stop diagnostics, verify manifest, clean the owned project, cooldown, preflight, next run).
3. **Monitor.** Each run is launched with `run_in_background` and watched with a monitor on `run-status.json`. If the expected duration passes without progress, the process and the evidence folder are inspected before anything is touched, and a healthy run is never killed.
4. **Completion.** On completion the session reads the exit code and the evidence, reports pass, fail or uncertainty, records the result and starts the next authorized step without waiting for another message.
5. **Continuity.** The status file also records the current run, active process and monitor IDs, evidence paths, git status and whether a measurement is running, so a restored session inspects processes and evidence first and never restarts an active measurement.
6. **Integrity.** Per run: the runner's manifest, `preflight.json`, the diagnostics directory with its hash list, the plan commit SHA and the analysis script commit SHA. Every run's raw file is hashed on completion. Nothing is deleted. A results document will be added to the repository after r3 with the complete per-run tables.

## 8. Decisions needed from the owner before any run

| Id | Decision | Recommendation |
| --- | --- | --- |
| D1 | Start the three runs (go or no-go) and the quiet window for them | Go, in one uninterrupted window of about three hours with other applications closed |
| D2 | Replacement of aborted attempts that never produced a P09 result | Allow at most two, never for a completed run |
| D3 | Preflight thresholds in section 4 | Approve as proposed |
| D4 | Keep the machine awake for the window | Yes |
| D5 | Keep the Balanced power plan as in the earlier runs | Yes, record it |
| D6 | Accept the disclosed diagnostics overhead | Yes |
| D7 | Burst threshold of 5 times the row median, chosen after seeing f6 and f7 | Accept, with the disclosure above |

## 9. What this plan cannot do

Three further runs narrow the uncertainty but cannot prove the cause of the f6 burst: no per-sample timestamps exist and the host has no hypervisor-level telemetry beyond what the sidecar reads. The result can show how often the gate fails and whether bursts line up with host load. It cannot make P09 pass, and it does not change the fact that f6 failed on the same source as f7.

## 10. Files

All under `docs/m17-s25-p09-controlled-protocol-20261115/`: `preflight.ps1`, `diagnostics.ps1` and `analyze-controlled.mjs`.

## Amendment 1 (2026-11-15, before any counted measurement): source and attempt accounting

The owner approved the plan and the three runs started. Two attempts aborted before the P09 stage and are retained unmodified; no P09 measurement exists yet.

| Attempt | Evidence | Stage that failed | Cause |
| --- | --- | --- | --- |
| a1 (first r1) | `aborted-a1-*` | `postgres-readiness-alert-and-durable-recovery` (stage 29) | The fresh clean worktree had no `tests/browser/node_modules`, so the page-fault journey could not import `playwright`. A launcher gap, fixed by installing the lockfile's package set before the preflight. |
| r1b (replacement 1 of 2) | `r1b-*` | `malformed-context-real-tempo-export` (stage 37) | The stage queried Tempo with a start equal to the second the injection began. The 960 requests finished inside that second (the stage took 0.6 s against 15 s in f7), Tempo capped the end at its own clock, and the query window collapsed; Tempo answered 400. |

The second cause is a latent defect in the verification source at `5b1dcc41`. It is not a P09 matter, but it aborts a full campaign on a quiet, fast host, which is exactly the condition the preflight creates. It was reproduced on a disposable rehearsal stack against real Tempo: the unfixed script failed 2 of 8 runs with the identical 400 and Tempo logged `start` equal to `end`; the fixed script passed 8 of 8 (logs in `docs/m17-s25-mutation-evidence-20261115/survivor-evidence/p09-fix-validation/`).

**Owner decision taken on 2026-11-15:** a minimal fix commit on top of `5b1dcc41`, then run. The fix starts the Tempo search window one second earlier (`tempoSearchWindow` in `scripts/g17-malformed-trace-evidence.mjs`, used by `scripts/check-g17-malformed-export.mjs`) and adds two unit tests. Commit `8d5e24137ad2943a6b2787b6472e4cea7bd9947e` on branch `verify/g17-s25-p09-source-20261115` (parent `5b1dcc41`). Nothing else changed: the P09 measurement script, its verifier, the budgets and the method are byte-identical to `5b1dcc41`. Every counted run therefore executes source `8d5e2413`, not `5b1dcc41`, which weakens the exact-source comparison with f6 and f7 only in that one stage script. The strict mutation harness gives 14 killed of 15 for the changed module; the survivor is the older canary assertion, already closed on the review branch.

**Accounting.** The attempt that follows is replacement 2 of 2 under D2, because the amended source is treated as the same plan, not a new one. If it aborts before producing a P09 result, no replacement remains and the owner decides. A counted result is never replaced. A completed run at `8d5e2413` is also a full campaign, so its 49-stage result becomes evidence for that source.

**Host preparation, disclosed.** The preflight failed on tries 1 to 8 of r1b because of desktop load. With the owner's agreement Chrome was closed and the WinZip file-association helper `FAHWindow64.exe` was stopped; no threshold was changed.
