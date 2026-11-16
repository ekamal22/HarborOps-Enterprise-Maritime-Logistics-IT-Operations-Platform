# G17 Final Correctness and Test Audit

**Recorded:** 2026-11-16
**Audited source:** `7cc25dabc42b78debf6c9b200fd5209e45b0f56d` (starting point: review branch `9cbcc369`)
**Gate:** **G17 NOT ACCEPTED; acceptance pending.** This audit is the implementing contributor's own and is not independent. Independent S25-A01, A02 and A03 remain mandatory.

## 1. Scope and honest limits

Read line by line in this audit: every M17 production file (API, Infrastructure and Worker `Observability` folders, 15 files, 833 lines), their wiring in `Program.cs`, `DatabaseReadinessCheck.cs`, the tracking receipt span in `TrackingEndpoints.cs`, `WorkerShell.cs`, `monitoring/m17/alerts.yaml`, the OTLP environment in the M17 compose overrides, the runner `scripts/run-g17-local.ps1`, `reconcile-g17-assertions.mjs`, `g17-manifest.mjs`, `g17-p09-review.mjs`, `check-g17-p09-review.mjs`, `audit-g17-test-coverage.mjs` and the diagnostic producer.

Exercised by the complete k4 campaign and by the earlier mutation audit but **not re-read line by line here**: `monitoring/m17/dashboard.json`, collector, Tempo and Prometheus configuration, the storage, retention and restart check scripts, the Playwright specs and the .NET integration witnesses. For these, the assurance is the passing source-locked execution plus the earlier [805-candidate mutation audit](M17_G17_S25_MUTATION_SURVIVOR_CLASSIFICATION_20261115.md), not a new review. A reviewer should treat them as unaudited by this document.

## 2. Findings and closures

Severity is the contributor's assessment. Nothing below weakens a threshold, policy, assertion or source-lock rule.

| ID | Severity | Affected requirement | Cause | Correction | Evidence the correction works |
| --- | --- | --- | --- | --- | --- |
| **F-FINAL-01** Readiness check publishes "database unavailable" when the caller abandoned it | Low (telemetry accuracy; the alert needs two consecutive recent samples, so a single aborted probe cannot alone fire it) | G17-S09 database timing and outages, G17-S17 alert triggers (a false outage sample misreports PostgreSQL) | `DatabaseReadinessCheck` recorded its sample in a `finally` block unconditionally. A client disconnect or host shutdown during the check therefore set the readiness gauge to 0 and added an "unavailable" duration sample although the database was never found unavailable. The background probe's own comment ("graceful stop does not mean database failure") contradicted this | The sample is recorded only if the check succeeded or the caller did not cancel. The check's own failure and internal time limit are still reported | 4 new tests in `M17ReadinessCancellationTests`. **Before the fix 2 failed** (caller cancels while the server never answers; token already cancelled) and the 2 controls passed (real refusal still reports exactly one unavailable sample; no caller cancellation still reports). **After the fix 4 of 4 passed**, and the same 4 passed inside the k4 integration stage (59 of 59). The database outage step of the live fault stage also passed in k2 and k4 |
| **F-FINAL-02** Diagnostic hash list written before the final state | Medium for evidence integrity of the P09 diagnostics; no effect on any P09 sample, stage or verdict | G17-S24 evidence integrity, P09 protocol hygiene | `diagnostics.ps1 -Action Stop` hashed the directory and then added `stopped_utc` to `diag-state.json`, so the listed size and SHA-256 of that file described a state that no longer existed | Final state is written first, the hash list last, and it never lists itself | 12 regression tests, a real Start then Stop run, a preserved previous-producer negative control, mutation controls (section 4). The original r1c, r2 and r3 records still show their mismatch (332 vs 373, 330 vs 371, 330 vs 371 bytes) and a test pins that |
| **F-FINAL-03** Runner depended on an undeclared `tests/browser/node_modules` | Medium (verification reliability: a clean worktree could not run the gate; earlier local PASS records relied on pre-existing files in their worktrees) | G17-S20 end-to-end fault journey, G17-S24 evidence integrity (source-locked, reproducible campaign) | The database fault stage runs a page journey importing `playwright`; the runner installed `tests/browser` dependencies only later in the Playwright stage | Named stage `browser-test-dependencies` runs before the first stage that needs it; the later duplicate install is removed | Attempt k2 failed on exactly this; k3 and k4 passed the new stage from a clean worktree. `g17-runner-order.test.mjs` (5 tests) failed 4 of 5 against the k2 runner and passes 5 of 5 now, and has a control proving a non-browser stage is not forced to wait |
| **F-FINAL-04** New diagnostic test broke under a PowerShell 7 parent | Low (defect in this work's own new test) | G17-S24 | The test spawned `powershell.exe` 5.1 and inherited `PSModulePath` from the PowerShell 7 runner, so `Get-FileHash` was not found | The child environment omits `PSModulePath` | Attempt k1 failed on it and the failure was reproduced through a pwsh-launched Node; after the fix the full Node stage was rehearsed through a PowerShell 7 parent and passed, then passed in k4 |
| **F-FINAL-05** Stale compose network after a failed attempt | Informational (procedure) | none | The runner stops but never removes the owned project's networks, and the stack uses a fixed subnet | Procedure: remove the previous attempt's owned containers and networks by exact project label before relaunching. The runner was not changed | Attempt k3 failed on it; k4 passed after the cleanup. Recorded in the continuity file |

## 3. Observations not changed, for decision

| ID | Observation | Why it was not changed |
| --- | --- | --- |
| **O-1** | The zero-baseline seeding of the 5xx counter covers only `operation="business"`. The first 5xx burst of another operation family (`auth`, `organization`, `tracking`, `health`, `other`) after a process start appears at a nonzero first value, which Prometheus `rate()` and `increase()` do not count. Detection resumes from the next increment | Low impact (a one-off undercount, the alert fraction still uses all later failures), and changing the exported series set at the end of a gate would add risk to witnesses that query that exact series. Proposed for an agreed change with the next remediation, not silently |
| **O-2** | The readiness check's internal 5 second limit did not stop a connection to a server that accepts the socket and never answers. In the new tests the check ended at the provider timeout of the test connection string (8 seconds), including when the caller cancelled after 400 ms (the abandoned-check test took 8 s in k4, 30 s with the default test timeout before it was shortened). The production probe is therefore bounded by the Npgsql connection timeout in that case, which the alert freshness bound (15 s probe, 15 s export, 15 s scrape and margin) already budgets for | Behavior of EF Core and Npgsql connect cancellation, not introduced here. The assertion in the related test is correct but its name (`...OwnFiveSecondLimit...`) overstates the mechanism. Rename on the next change that triggers a rerun; renaming now would change the frozen source |
| **O-3** | `diagnostics.ps1 -Action Stop` stops its child processes with `Stop-Process` and does not wait for them to exit before hashing, so a final flush by a still-closing child could in principle follow the hash. The real Start then Stop run verified clean, and the verifier would report any such mismatch rather than hide it | Not observed; a wait would be a further producer change beyond the minimal fix requested |
| **O-4** | P09 margins are thin (authorized route +8.96% against 10%, memory +13.97% against 15%) and two individual pair p95 values exceeded 10% (+25.71% authorized in pair 1, +15.76% baseline in pair 2) | By the approved pooled rule these pass; they are disclosed. Optimization stays scheduled before G24 release readiness |
| **O-5** | `scripts/check-planning-docs.mjs` fails its M11 assertion before and after this work | The check wants the literal phrase `G0-G12 and G16 are accepted`; `docs/STATUS.md` says `G0-G12 and M16/G16 are accepted and integrated`. Unrelated M11 wording drift, left alone as instructed. With every failing assertion recorded in scratch copies, the check has the same four failures at `9cbcc369` and at this work (the M11 phrase, two M8 status checks and one M9 condition), so nothing here worsens it |

## 4. Mapping of the requested review areas to what was inspected and what executed

| Area | Inspected in this audit | Executed evidence in k4 |
| --- | --- | --- |
| API and worker telemetry behavior | Request middleware outside the exception handler classifies the final status; worker spans and attempts use fixed outcome sets; telemetry never changes a business result (receipt span is created after the durable commit) | Unit 319, contract 60, M17 integration 59, regressions 34 with M17 opted in against an unreachable collector, worker fixtures |
| Metrics, labels, cardinality, sampling | All dimensions drawn from finite allowlists (6 operation families, a fixed set of status classes, fixed outcomes); route templates only, never request paths; 10% trace-ID sampler with a hard 200 spans per second ceiling per process; remote sampled flags ignored | Cardinality browser witness and 40,000 hostile path memory stage; label allowlist and schema tests |
| Trace context and privacy | Tag allowlist with value validation, status description and trace state cleared, events and links suppressed by a fail-closed exporter guard that requires both serializer limits set to zero, exporter endpoint restricted to `otelcol:4317` or loopback | Export privacy and malformed context stages with positive trace canary, worker log privacy test |
| PostgreSQL and health | Readiness check and background probe separated from `/health/ready` contract; **F-FINAL-01** found here | Live database outage, alert firing and recovery stage; health contract tests |
| Collector failures and recovery | Exporter timeouts 1 s, batch processor with bounded queue, failures counted, heartbeat distinguishes reachability from delivery | Collector fault stage with business results unchanged and recovery |
| Lease and restart guarantees | `WorkerShell` renews and releases leases, cancellation never marks a failure, failure disposition deferred when the database is down | Restart and cancellation stage with durable counts and no lease change; worker runtime and fault recovery |
| Storage exhaustion and immutable history | Not re-read | Storage stage with retained M8, M11, M15 and M16 history compared before and after |
| Prometheus rules and Grafana | Alert expressions, hysteresis, freshness bound and insufficient-sample handling read; dashboard JSON not re-read | `promtool` rule check and fixture test pass, alert matrix and live latency stages, three-engine dashboard witnesses (24 of 24) |
| Chromium, Firefox, WebKit | not re-read | 24 of 24 expected, 0 unexpected, 0 skipped, 0 flaky; cardinality witness 1 of 1 |
| Source SHA, manifests, ledger derivation | Read and independently re-derived | `k4-independent-verification.json`: all checks pass, 54 of 54 files hash-verified, ledger re-derived record for record |
| P09 measurement and review logic | Nearest-rank p95, pooled rule and memory mean re-read; inclusive limits confirmed | Independent recalculation PASS; reproduces the recorded figures |
| Mutation findings | Classification document consulted; unchanged modules not re-mutated | See section 5 |
| Runner, enumeration, no-skip | The runner's Node list was cross-checked against every Node witness named by the coverage register (none missing). Six other Node test files in `scripts/` belong to accepted milestones and are outside the G17 list. Audit script scans witness sources for skip and ignored-failure markers | Strict audit 72 authored, 0 partial, 0 missing, 3 independent; Node junit has 0 failures and 0 skips |

## 5. Test correctness and completeness audit

- **Evidence mapping.** All 25 scenarios and 75 atomic assertions map to a named witness or to the independent reviewer (S25). The per-assertion table with the raw artifact for each is in the [handoff packet](M17_G17_S25_FINAL_HANDOFF_PACKET_20261116.md), derived by script from the ledger.
- **No skip, filter or silent not-applicable.** Strict coverage preflight passes (72 authored witnesses). The Node junit, four .NET result files and both Playwright reports have 0 skipped, 0 not-executed and 0 filtered cases. The `--filter` expressions of the runner (M17 integration classes and the accepted-milestone regression list) were not narrowed.
- **Vacuity.** The new tests each carry a control proving the observation path is live: the readiness suite includes a real refusal that must still publish exactly one unavailable sample; the diagnostic suite verifies the real producer's output and rejects 16 tamper and malformed cases plus 11 reason-specific cases; the order test discovers the real consuming stage rather than assuming it.
- **Source identity.** The independent check found the same SHA and project in `source-sha.txt`, `run-status.json`, the manifest, and every raw report examined. The manifest's assertion-inventory hash equals the file's hash.
- **Accepted milestone compatibility.** The 34 regression cases (M9 worker, M10 projection, M11, M13, M15, M16 audit, reference data and G13/G15 defaults) all passed with M17 enabled and a deliberately unreachable collector.
- **Changed verifier code and negative controls.** Files changed since the 805-candidate mutation campaign: `g17-malformed-trace-evidence.mjs` and `check-g17-malformed-export.mjs` (Tempo window, already mutation-tested at 15 of 15), `run-g17-local.ps1` (stage list; order now pinned by a test with negative controls) and the new `g17-diagnostic-hash-list.mjs`. For the new verifier, 22 single-fact mutants were run against its test file in a scratch copy so the worktree was never touched:

  | Round | Against | Result |
  | --- | --- | --- |
  | 1 | first test draft | 14 of 21 applicable killed, 7 survived (self-listing, missing state entry, `stopped_utc` type, stopped before started, invalid JSON, short checksum, directory handling). Cause: the tamper test asserted only that verification failed, not why |
  | 2 | rewritten tests (reason-pinned) | 21 of 21 applicable killed; the 22nd pattern (byte-order mark handling) did not apply because the source then held a literal invisible character |
  | 3 | final frozen files (BOM written as the visible escape, child-environment change in the test) | 22 of 22 killed, unmutated control passes |

  Outputs are in the evidence folder named in the handoff packet.
- **Defects found in the tests themselves.** The new verifier accepted a trailing space in a file name (found by its own parse test, fixed). The first draft of the new readiness test had a compile error and an excessive 30 second runtime (both fixed before use). The diagnostic test failed under the real runner environment (F-FINAL-04). A test name overstates a mechanism (O-2, disclosed).

## 6. Result of this audit

- Five findings, all closed or procedurally explained, one touching executable product code (one file, 5 insertions, 1 deletion).
- Five observations left open for agreement, none a blocker for independent review.
- No requirement, threshold, assertion, source-lock rule or policy was relaxed, and no required scenario was skipped.
- This audit cannot substitute for independent S25 review.

[Final result](M17_G17_FINAL_SOURCE_LOCKED_RESULT_20261116.md) | [Handoff packet](M17_G17_S25_FINAL_HANDOFF_PACKET_20261116.md) | [Diagnostic hash finding](M17_G17_DIAGNOSTIC_HASH_FINDING_20261116.md)

**G17 NOT ACCEPTED; acceptance pending.**
