# G17 Consolidated Final Remediation and Source-Locked Result

**Recorded:** 2026-11-17
**Branch:** `feat/harborops-g17-final-verification-60b4bf`
**Frozen verification source:** `634053c4d0c3a2cb8c51b2c29fda87b126e08993`
**Campaign outcome:** attempt **m4 COMPLETED technically**: runner exit 0, 51 of 51 stages PASS, 72 of 72 technical witnesses, 3 independent S25 assertions pending.
**G17:** **NOT ACCEPTED; acceptance pending.** This is a technical result only. It is not an independent review and does not sign any S25 assertion. This record supersedes the 2026-11-16 final result (`7cc25dab`, attempt k4) as the current technical checkpoint. The k4 record is preserved unchanged for history.

## Purpose

The previous campaigns each found a further defect only after a complete run. This work audited the whole of G17 before the next campaign (M17 API, infrastructure and worker source, startup wiring, health services, telemetry configuration, collector, Tempo, Prometheus rules, dashboard JSON, compose files, browser specs, verification scripts, runner and cleanup, the P09 scripts and the accepted-milestone regressions), corrected every defect found with a reproduction and a negative control, and then ran one complete source-locked campaign on a frozen commit.

## Findings register

Severity: **High** can hide a failure from an operator or break an approved contract; **Medium** weakens verification or leaves a defect reachable under load; **Low** wording or maintenance.

| ID | Location | Reproduced behavior | Severity | Root cause | Correction or disposition | Negative control |
| --- | --- | --- | --- | --- | --- | --- |
| R01 | `src/HarborOps.Api/Observability/M17Telemetry.cs`, `monitoring/m17/alerts.yaml` | The first burst of HTTP 5xx failures in the auth, organization, tracking, health or other operation family was never counted by the 5xx alert on a fresh process. Only `operation="business"` had a zero baseline | High | Prometheus `increase()` cannot count the first sample of a series. The heartbeat seeded zero only for the business family | Zero baselines are seeded for all six operation families (`auth`, `organization`, `tracking`, `health`, `business`, `other`) with status class `5xx`. The eligible-request denominator now adds the failure counter to the non-5xx request counter so a failure-only family still reaches the 100 request floor. No label and no series count were added beyond the six seeded series | Real stack check `scripts/check-g17-first-failure.mjs` (8 ordered steps, real exported Prometheus observations, exact counter equality per family): the old source fails, the new source passes. Promtool per-family first-burst cases, an unseeded negative control, threshold boundary (1.48% fires, 0.74% does not), steady hysteresis, insufficient traffic, false-positive and recovery cases. 21 of 21 alert mutants killed |
| R02 | `src/HarborOps.Api/Observability/M17TelemetryMiddleware.cs` | On the real stack a 503 from an auth, organization or tracking route was exported as `operation="other"` | High | The exception handler clears the routed endpoint before telemetry classified the request | The middleware captures the routed endpoint before `next` and passes it to the classifier | `M17ExceptionRouteClassificationTests`; the real stack check now requires `other` to stay at 0 and exact per-family equality |
| R03 | `src/HarborOps.Api/Health/DatabaseReadinessCheck.cs` | Against a server that accepts a connection and never answers, the readiness check waited for the provider timeout (30 seconds for a 30 second setting) instead of the approved 5 seconds. A first fix still took 6.05 seconds, and under ten concurrent probes on the real stack with PostgreSQL stopped it took 6.8 to 7.8 seconds | High | The provider ignores the cancellation token while it connects and starts up, and name resolution and queued connection attempts are not covered by the provider timeouts | Three layers: provider connect and command ceilings lowered to 3 seconds and cancellation timeout to 300 ms for this check only (never raised), a linked token cancelling at 4 seconds, and a hard 4.5 second wall-clock limit that abandons the provider call. The abandoned call ends on its own 3 second ceiling and its outcome is observed and discarded | Silent server tests, caller cancellation, refused connections, outage and recovery, concurrency and a delaying proxy; a stuck connection interceptor drives the hard limit. Real stack with PostgreSQL stopped, ten concurrent probes: HTTP 503, maximum 4.51 seconds. 9 of 9 readiness mutants killed |
| R04 | same file, found while correcting R03 on the real stack | With the hard limit in place, concurrent probes returned HTTP 500 | High | The abandoned call left the request scope's shared DbContext connection in the Connecting state and disposing it threw | The probe owns a short-lived DbContext of its own and never touches the request scope's context | `M17ReadinessPipelineTests` runs the real health-check service, the real exception handler and the real endpoint with twelve concurrent probes against a provider call that never finishes: 503 with the same JSON body, never 500, within the bound |
| R05 | `tests/HarborOps.IntegrationTests/M17ReadinessCancellationTests.cs` | The test named `OwnFiveSecondLimitStillPublishesUnavailableWhenCallerWaits` did not exercise a five second limit and misdescribed its behavior | Low | Misleading name | Renamed and replaced by tests that name what they assert | The silent-server and hard-limit tests above |
| R06 | same file | Not a defect, an obligation: a caller that abandons the check (client disconnect, shutdown) must not publish "database unavailable" | Medium | n/a | Preserved. Telemetry is published only when the result is healthy or the caller was not cancelled. The check's own limit is still reported | Caller-cancellation theory tests assert no unavailable observation on caller cancel and one on the check's own limit |
| R07 | `docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1` | Stop could hash diagnostics before child processes had exited, a late write could be missed, and the final state was not covered consistently | Medium | Stop did not wait for children or confirm stability | State schema 2 records child identity (pid, name, creation time) and the sidecar id. Stop verifies each child, kills, waits and verifies exit, runs a settle loop that detects late writes, rejects subdirectories and reparse points, writes the final state first and the hash list second, and rechecks after 500 ms. Repeated Stop is idempotent | Producer guard tests with impostor processes and a stub docker; late-write negative control |
| R08 | same file | Stop could act on a stale or tampered process or container id | Medium | Identity was not revalidated | Each child is checked by name, creation time within 2 seconds and a command-line marker built from the project and output directory. The sidecar is removed only if its id, name, owner label and network `none` match and it carries no compose label | Tests with stale ids, wrong names, wrong labels and a foreign container |
| R09 | `scripts/g17-diagnostic-hash-list.mjs` | The verifier accepted missing or malformed `started_utc`, loose timestamps, symlinks and entries outside the directory, and a historical `onlyListed` mode could return success | Medium | Verification was too permissive | Strict verifier: exact timestamps compared in integer ticks, `lstat`, symlink and non-regular rejection, case-folded duplicates, alternate stream and wildcard names rejected, unlisted entries rejected, schema 2 outcomes checked. The historical `onlyListed` mode never returns success and is not used for new evidence | 17 strict verifier tests, 12 hash-list tests; verifier mutation campaign 38 of 38 effective mutants killed |
| R10 | `scripts/run-g17-local.ps1`, `scripts/g17-docker-ownership.ps1`, `scripts/cleanup-g17-owned.ps1` | A stopped stack still held its fixed compose subnet (the cause of attempt k3) and a fresh campaign needed manual network removal | Medium | The runner stopped containers and never removed networks | Ownership-checked cleanup: exact compose project label, exact name pattern, exact network name, foreign-endpoint rejection, volumes always retained, no prune. State is preserved first. A cleanup failure is a real failure. A pre-start check proves the subnets are free | Real Docker tests and failure-path tests through a stub docker front. The cleanup refused a renamed leftover container during this work (ambiguous ownership, nothing removed), which is the intended behavior |
| R11 | `scripts/g17-evidence-archive.mjs` | Earlier archives had no extraction check and no inventory | Low | Packaging was an afterthought | `create` writes a real zip, `verify` extracts it with a second reader and compares the full inventory, signature, sizes and hashes, and rejects traversal and duplicates | Archive tests including tampered and truncated archives |
| R12 | `scripts/check-planning-docs.mjs`, `scripts/planning-current-state.mjs` | The checker reported 4 failures (one M11, two M8, one M9) | Low | The checker required literal phrases that were true on 2026-11-08 and stale afterwards. The documents were right and the checker was stale | Stale phrase rules replaced by current-state rules and forbidden-claim patterns, checked against the authoritative closure records. No acceptance decision changed and the generated execution graph was not edited by hand | 19 of 19 planning mutants killed; the checker passes (231 Markdown files) |
| R13 | `scripts/g17-witness-binding.mjs`, `scripts/reconcile-g17-assertions.mjs` | Alert and manifest-rejection witnesses were bound by case strings only | Medium | Weak binding | A witness now counts only when the named case exists and demonstrates the named effect | Witness binding tests; redundant guards classified below |
| R14 | `scripts/check-g17-worker-journey.mjs` | Attempt m2 failed its last stage with 0 matched API and worker trace pairs although 50 receipts reconciled | Medium | Tracing of a journey is a keyed 10 percent decision. Fifty receipts hold fewer than three traced journeys about one run in nine and fewer than two about one run in thirty, so the stage could fail by chance | The script computes the decision from each receipt digest, tops up with more receipts (paced past the 60 per minute per source limiter) until at least 3 are traced, records `sampled_journeys` and `top_up_batches`, and still requires 2 real API and worker trace pairs in Tempo. The manifest verifier and raw witness accept at least 50 receipts with every one reconciled and at least 3 traced journeys. A cross-language vector table ties the script to the C# sampler | Sampling unit tests, C# vector test (10 of 10), campaign and witness rejection cases (80 receipts with 79 reconciled, 2 traced, missing count), 6 of 6 mutants killed. The real top-up path was rehearsed on a disposable stack with the requirement forced to 8: 90 receipts accepted, 8 traced, 8 real pairs, one top-up batch |
| R15 | Node test harness | Attempt m1 failed at its first stage: EPERM deleting a per-test directory that held an 80 MB executable copy under the load of the full parallel suite | Medium | Test-harness defect, no product behavior involved | Stand-in executables copied once per test process, removal retried on Windows locks, children awaited before removal. Three full-suite stress runs passed | Stress runs and a test that fails against the old harness |
| R16 | `scripts/g17-raw-witness.mjs`, `scripts/verify-g17-manifest.mjs`, fixtures | The first-failure evidence was not a required, validated artifact of the campaign | Medium | New evidence had not been registered | `m17-first-failure-results.json` is required, validated (steps, exact counts, restored database, label allow-list with value checks) and bound to witnesses; `m17-owned-cleanup.json` is required as well | Campaign rejection cases for each fact |

### Dispositions that are not corrections

| Item | Disposition |
| --- | --- |
| Classification limit of operation families | A route is classified by its prefix. A request that never reaches routing is `other`. Documented, not a defect |
| First sample of a new non-5xx series | Prometheus loses the first sample of any series that first appears during traffic. Only the 5xx series are seeded. The denominator is built so that this cannot suppress the 5xx alert. The seeding is not claimed to solve every start-up race |
| Abandoned provider call | After the readiness check answers at its hard limit, the provider call can run on until its own 3 second ceiling. Its outcome is observed and discarded and it uses its own context, so nothing is left running beyond that |
| Unproductive mutants | Mutation survivors were classified individually. Equivalent or redundant: redundant guards in the witness binding, presence redundancy in the archive size, extra and missing checks, two Docker label guards that the exact label filter makes unreachable, and child-exit awaiting in the producer, which cannot be observed without a kernel stall. Infrastructure failures, compile errors and timeouts were never counted as kills |

## Changes in this work

Relative to the k4 frozen source `7cc25dab`, the frozen source `634053c4` differs in 66 files (4,922 insertions, 214 deletions), made up of product source, tests, scripts, monitoring rule tests and the runner. Of these, 3 are product files (131 insertions, 29 deletions); the 2026-11-16 documentation commit `a64aa580` changed no executable file.

Product source changed in exactly three files: `DatabaseReadinessCheck.cs` (R03, R04, R06), `M17Telemetry.cs` (R01, R02) and `M17TelemetryMiddleware.cs` (R02). Every other change is a test, fixture, verifier, script, monitoring rule test or the runner. M9 transport hold, M16 audit protections, tenant isolation and default-off telemetry are unchanged, and no new metric label or series family was added.

Commit sequence on the branch: `74715ed1` (first burst in every family, silent-server readiness bound, diagnostics ownership), `ca14bcf0` (routed operation family kept, rehearsal hardening), `cc80be08` (bounded readiness tests, ledger and cleanup failure-path controls), `c9acf7e3` (readiness inside five seconds), `7e5390c6` and `d85612a5` (harness lock fixes), `d4bddcab` (hard wall-clock limit), `826e096f` (probe owns its context), `634053c4` (deterministic worker journey).

## Pre-freeze checks (all run before the frozen commit)

| Check | Result |
| --- | --- |
| Full Node suite (`scripts/*.test.mjs`) | 520 of 520, 0 failures |
| .NET unit | 319 of 319 |
| .NET contract | 60 of 60 |
| M17 integration, audit hardening, organization upgrade | 113 of 113 (the readiness tests are wall-clock bounded and were run on an idle machine) |
| Promtool rule check and rule tests | PASS |
| Planning documents checker | PASS (25 milestones, 126 graph nodes, 231 Markdown files) |
| Strict S25 atomic coverage preflight | 75 assertions, 0 errors |
| Hygiene scan of changed files | No dash characters of the banned kind, no attribution text, no control characters, no byte-order marks |
| Mutation campaigns | alerts 21/21, first-failure evidence 38/38, C# telemetry 12/12, readiness 9/9, planning 19/19, verifier 38/38, manifest changes 5/5, worker top-up rules 6/6 |
| Real stack rehearsals | first-failure check (old source fails, new source passes), readiness under stopped PostgreSQL, worker journey (natural and forced top-up), interrupted-run cleanup, failure-path runner |
| Worktree | clean at the frozen commit, no ignored dependency directory present at launch, build servers shut down |

## Result of attempt m4

Started 2026-11-17T01:47:35Z, finished 2026-11-17T02:33:42Z (46 minutes), local runner `scripts/run-g17-local.ps1` with the disposable stack, collector fault probe, worker fixture, full-G17 reconciliation and owned cleanup, project `ho-verify-m17-final2-m4`. No GitHub Actions workflow was dispatched. Run state COMPLETE, `gate_acceptance` NOT_ACCEPTED, `g17_assertion_review` NOT_REVIEWED.

| Check | Result |
| --- | --- |
| Runner stages | 51 of 51 PASS, exit code 0, no skipped or filtered stage |
| Node source and negative-control suite | 478 cases from 32 files, 0 failures, 0 skipped |
| .NET unit | 319 of 319 |
| .NET contract | 60 of 60 |
| M17 integration | 113 of 113 |
| Accepted-milestone regressions with M17 opted in | 34 of 34 |
| Browser dashboard witness, Chromium, Firefox and WebKit | 24 expected, 0 unexpected, 0 skipped, 0 flaky |
| First-failure baseline in every operation family | PASS, 8 steps |
| Worker journey | PASS: 50 receipts accepted and reconciled, 4 traced journeys, 4 real API and worker trace pairs, no top-up needed |
| Technical assertion ledger | 72 of 72 TECHNICAL_PASS, 3 PENDING_INDEPENDENT_REVIEW, 75 total, 25 scenarios |
| Evidence manifest | 68 files indexed, complete, no missing expected artifact |
| Owned cleanup | REMOVED: 10 containers and 4 networks removed, 0 left, volumes retained, subnets released |

### Independent recalculation after the run

Performed from fresh processes on copies or read-only: `verify-g17-manifest.mjs` reports the evidence index complete for the frozen SHA (68 files); the assertion ledger recomputed from the raw artifacts on a copy is byte-for-byte identical to the one the runner wrote (72 technical, 3 pending, 75 total, 0 errors); the P09 arithmetic recomputed from the raw per-request samples matches the recorded values (below).

## P09 at this source: two measurements, one failure, disclosed

P09 limits are unchanged: 10% pooled p95 increase per route and 15% mean paired peak memory increase. The QUALIFIED PASS decision, the f6 FAIL and the pre-release optimization obligation are unchanged.

| Attempt | Source | Result | Baseline route pooled p95 | Authorized route pooled p95 | Mean peak memory | Pairs over p95 / over memory |
| --- | --- | --- | ---: | ---: | ---: | --- |
| k4 | `7cc25dab` | PASS | +3.95% | +8.96% | +13.97% | 2 / 0 |
| m2 | `d85612a5` | PASS | +6.89% | +8.61% | +14.99% | 2 / 2 |
| **m3** | `634053c4` | **FAIL** | **+12.07%** | -0.43% | +14.54% | 1 / 2 |
| **m4** | `634053c4` | **PASS** | -3.56% | -0.27% | +10.81% | 0 / 1 |

**m3 was a performance failure at the frozen source and is preserved.** Its run stopped at the paired measurement stage, so the stages after it did not run. Its cause was a sustained tail disturbance in round 3 only: the round 3 telemetry-on baseline p95 was 21.8 ms against about 17 ms in every other round, the medians were equal (15.1 and 15.3 ms), and the elevation lasted through all six segments of the round. Rounds 1 and 2 were +4.0% and +1.8%. The existing data cannot distinguish a disturbance of the machine from a real tail cost. It was not retried silently: the failure and a recommendation were presented first, and a new complete campaign on the identical source was approved before m4 ran. m4 is a complete campaign, not a rerun of P09 alone, and the thresholds, method and samples are identical.

**What this does and does not show.** Four counted measurements at the current and the immediately preceding source passed three times and failed once, and two of the passes sat within 1.1 points of a limit. P09 is therefore close to its limits on this workstation and its outcome varies between campaigns. Passing in m4 does not remove the m3 failure from the record, and it does not change the QUALIFIED PASS status or the obligation to optimize and re-measure before release readiness (G24). A reviewer should weigh the four results together, not only the last.

## Failure history, preserved

No failed attempt or rehearsal was deleted, edited or rerun to obtain a better result. Source or runner changes were made only to correct a defect in verification code or the runner, or a product defect with a reproduction.

| Attempt | Source | Result | Classification | Cause and disposition |
| --- | --- | --- | --- | --- |
| k1 | `6cbdb762` | FAILED, stage 1 | Verification defect | Child PowerShell host module path (preserved in the k4 record) |
| k2 | `56a34ee9` | FAILED, stage 29 | Verification defect | Browser dependencies installed too late (k4 record) |
| k3 | `7cc25dab` | FAILED, stage 22 | Infrastructure | Stale compose subnet from k2, now prevented by R10 (k4 record) |
| k4 | `7cc25dab` | COMPLETE, 49 of 49 | n/a | Previous technical candidate, superseded by this record for current status |
| m1 | `7e5390c6` | FAILED after 3 minutes, stage 1 | Verification defect | R15 |
| m2 | `d85612a5` | FAILED at the last stage | Verification defect | R14. All earlier stages and P09 passed. Evidence preserved untouched |
| m3 | `634053c4` | FAILED at the paired measurement | Performance failure, cause unresolved | See above |
| **m4** | `634053c4` | **COMPLETE, 51 of 51** | n/a | Final technical result of this record |

Earlier PASS records apply to their own sources and are not used as evidence for `634053c4`. The original diagnostic hash mismatch of r1c, r2 and r3 is preserved with its original hashes; the correction is prospective (see the diagnostic hash finding).

## Evidence location and hashes

Raw evidence is retained locally, was not pushed and is not published, because it contains exported traces, service logs and raw measurement samples that a reviewer should inspect in place.

| Item | Location or value |
| --- | --- |
| Evidence directory | `E:\HarborOps-Verification\g17-final2\final-m4-evidence` (stack state in `final-m4-evidence-disposable-stack`) |
| Evidence archive | `E:\HarborOps-Verification\g17-final2\g17-m4-evidence.zip`, a real zip of 69 files (6,965,959 bytes uncompressed, 513,797 bytes compressed), extraction-verified against `g17-m4-evidence.zip.inventory.json`. SHA-256 `0a5324bcb33987fde1a565684ea65e356af81c44d34ff85f4d554f0c10098d0b` |
| `manifest.json` SHA-256 | `978ddfb969bbfee2e2de1413ff581ac7916153ed7c0091cefbd7a5e0b4d148d3` |
| `g17-technical-assertion-ledger.json` SHA-256 | `d936408e047bc60b515b1d877641ca2f3dc223257d8e3c97840f7408dee9e3ee` |
| `run-status.json` SHA-256 | `b33447ade2457bc408d1db5e53df300eab0dc59e3e6cdb1c00a6e114d4539259` |
| `m17-performance-raw.json` SHA-256 (m4) | `e71efe54e721f7fe98f4196131bd46bdf0c56a46afa1fdb63ea7a3c254be98b2` |
| `m17-performance-raw.json` SHA-256 (m3, failed) | `d01b3fb9b8924b2a65a258d7e0c3455c624a67deb9fbea4a5beb0b5bc735298e` |
| `m17-performance-raw.json` SHA-256 (m2) | `0dbb24b2ba4e868f861b2d067b048e6f311b2ac0201740250497904e46112e00` |
| Run log (m4) | `E:\HarborOps-Verification\g17-final2\final-m4-run.log`, SHA-256 `bd66862c27dda892c217d7d7836d09fce034cdb523d3ae9dbc726614173d36de` |
| Failed attempts | `final-m1-evidence`, `final-m2-evidence`, `final-m3-evidence` and their run logs, untouched, in the same folder. k1 to k4 remain in `E:\HarborOps-Verification\g17-final` |
| Continuity record | `E:\HarborOps-Verification\g17-final2\STATUS-continuity.md` |
| Launcher | `E:\HarborOps-Verification\g17-final2\run-final2.sh <id>` |

## What this result does and does not establish

- It establishes that the frozen source, tests, fixtures and runner pass the complete no-skip local G17 gate (25 scenarios, 72 technical assertions) and that the evidence is internally consistent and independently recalculated.
- It does not establish G17 acceptance. S25-F06 (reviewer independence) and the independent S25-A01 (source and raw evidence review), S25-A02 (policy and procedural review) and S25-A03 (signed recommendation) require a genuinely separate reviewer, and no reviewer has signed anything. No merge, deployment or release is authorized, and no hosted GitHub Actions run exists for this source.
- Test counts above are for this source only and replace no accepted figure for any other source.
- P09 passed at this source on one of two measurements. See the disclosure above.

[Independent S25 handoff packet](M17_G17_S25_FINAL_HANDOFF_PACKET_R2_20261117.md) | [Previous final result (k4)](M17_G17_FINAL_SOURCE_LOCKED_RESULT_20261116.md) | [Diagnostic hash finding](M17_G17_DIAGNOSTIC_HASH_FINDING_20261116.md) | [P09 qualified pass decision](M17_G17_P09_QUALIFIED_PASS_DECISION_20261116.md) | [Open findings register](M17_G17_S25_OPEN_FINDINGS_REGISTER_20261114.md)
