# G13 and G15 Local Closure Verification Results, 2026-11-10

**Status:** Technical verification PASSED locally on the frozen source below. Independent S25 reviews and the G13 and G15 acceptance decisions: **acceptance pending**. This record is not a gate decision, not a merge authorization and not a GitHub Actions result. No merge, deployment, migration, dispatcher, projector, evaluator or retention deletion is authorized.

**Frozen tested source:** `5bcb7e5b2ec5e590bd26e64ffd659c17aaee1f92` on `feature/g13-g15-acceptance-closure-20261110` and `verify/g13-g15-evidence-closure-20261109` (both fast-forwarded from `1b878736e69be5431687868693a8df8bc65f5564`; protected `main` unchanged at `eb529368a2447061fc1838cd3967b0ffedae669d`). Commits after this SHA must change documentation only; every later commit is checked with `git diff --name-only 5bcb7e5 HEAD` and must list only files under `docs/` and `README.md`.

GitHub Actions was not dispatched, triggered, rerun or monitored for any of this work. The trigger file `.github/verification/g13-g15-once-20261109.txt` was not modified.

## What this record answers

The [formal gate decisions](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) at `15cabd8c542159753c927c2bd65b4346223e84f6` (G13 NOT ACCEPTED, G15 NOT ACCEPTED) listed five closure conditions. This table maps each to executed local evidence at `5bcb7e5`.

| Closure condition | Local evidence at `5bcb7e5` |
| --- | --- |
| 1. Per-tenant report quota and idle bounded cleanup, with concurrency and storage proof | Migration `20261110010000_M13TenantReportCapacity` and `OperationalKpiCleanupService`. `FourLiveSnapshotsAreAdmittedFifthDeniedAndExpiredPruningRestoresCapacity` and the new `IdleCleanupRemovesAtMostSixteenExpiredSnapshotsPerCallAndRejectsInvalidAuthority` PASS. 170,000-shipment run and four-principal concurrency PASS. |
| 2. Effective specific mutation witnesses for G13-S24 and G15-S24 with clean controls | G13 source: control PASS, 24 of 24 mutants KILLED. SQL security: control PASS, 4 of 4 KILLED. G15: control PASS, 11 of 11 KILLED, including `source-history-outbox-uniqueness-removed`. |
| 3. Bounded tenant-safe G15 HTTP behavior during real dependency outage | `G15HttpOutageAcceptanceTests` PASS, including `RealNotificationDatabaseLockReturnsBoundedScopedHttpFailureAndRecovers`. |
| 4. Assertion-level reconciliation of all scenarios with raw artifacts, then two new S25 recommendations | 246 atomic assertions reconciled with raw artifacts (below). S25 recommendations: **outstanding**, see [review brief](G13_G15_S25_REVIEW_BRIEF_20261110.md). |
| 5. Positive acceptance decisions and a fresh full campaign after source changes | Fresh full local campaign `c1` executed on the changed source. Positive decisions: **acceptance pending**. |

## Verification results by group

All groups ran against `5bcb7e5` in campaign `c1`. Raw evidence root: `E:\HarborOps-Verification\g13-g15-final\evidence\c1`. Disposable PostgreSQL 16.15 (Debian image) per job, restricted `harborops_api` role where the hosted workflow used it.

| Group | Result | Detail |
| --- | --- | --- |
| Source, contracts, planning docs, Release build with warnings as errors, unit, contract, web | PASS | 319 unit and 52 contract tests; web unit tests and production build; M11 and M15 source contracts; execution graph check; 217 Markdown files consistent |
| Combined M13 and M15 PostgreSQL regressions (13 ordered filters, includes restricted SQL, revocation, truth tables, event catalog, audit, historical targets, HTTP outage, tenant capacity, rate limit, recurrence, tracking source, M11 source, notification boundaries, cancellation, history uniqueness) | PASS | 104 tests passed |
| M2, M5, M10, M11, M12, M16 cross-milestone restricted PostgreSQL regressions | PASS | 63 tests passed |
| G13 rollback, cancellation and failure recovery | PASS | `BlockedMemberMaterializationRollsBackAndRecovers` |
| G13 170,000-shipment workload under the restricted API role | PASS | 30 samples, p50 9,605 ms and p95 10,430 ms against approved limits of 15,000 ms and 20,000 ms; peak working set 522,579,968 bytes; process CPU 7,948 ms; report table growth 6,021,120 to 1,703,124,992 bytes over the 30 samples; four restricted-role EXPLAIN plans archived with matching SHA-256 receipts (small, medium10k, skewed170k, empty) |
| G13 four-principal simultaneous tenant-scoped reporting | PASS | 4 users, 5,002 shipments, 4 independent reports, 1,266 ms |
| G13 source mutation sensitivity | PASS | clean control PASS, 24 of 24 KILLED |
| Restricted SQL security mutations | PASS | clean control PASS, 4 of 4 KILLED |
| G15 million-row scoped feed and keyset workload | PASS | at least 1,000,000 receipts, 10 pages, 500 members, p50 32 ms, p95 66 ms |
| G15 dual dispatcher, 250 additional members, 100 readers | PASS | 252 members, 2 dispatchers, fan-out 31 ms, 100 readers in 220 ms, at most 32 concurrent database readers, exactly once delivery true |
| G15 mutation sensitivity | PASS | clean control PASS, 11 of 11 KILLED |
| Process SIGKILL before and after commit, with recovery | PASS | 8 of 8 phases KILLED_AND_VERIFIED; the 6 phases that require recovery report recovery PASS |
| Chromium, Firefox and WebKit: notifications and operational KPIs | PASS | 10 notification cases and 8 KPI cases per engine, 0 unexpected, 0 skipped, 0 flaky (54 case executions) |
| Dependency advisories and committed-secret scan | PASS | npm high 0, critical 0; NuGet high or critical 0; no credential-shaped committed strings |
| Evidence reconciliation (local mode) | PASS | integrity PASS; 549 TRX results passed, 0 failed, 0 skipped, 0 other; 48 of 48 scenario technical checks; sensitive-evidence scan PASS over 238 files |

Scenario ledger statuses are conservative by design: 1 PASS, 23 BLOCKED and 1 NOT EXECUTED per gate. BLOCKED means targeted technical evidence exists and the complete scenario needs reviewer disposition; S25 is NOT EXECUTED until the independent reviews exist. None of these statuses is an acceptance.

## Assertion reconciliation

The inventory was recounted: 50 scenarios and 242 atomic assertions at the start (227 automated, 7 policy, 8 independent review). Four assertions were added because the approved tenant quota and bounded cleanup behavior had no atomic assertion or witness: `G13-S19-A07`, `G13-S19-A08`, `G13-S19-A09` and `G13-S23-A06`. The inventory now has **246** assertions.

| Class | Count | Basis |
| --- | ---: | --- |
| Automated, technical witnesses present and passing at `5bcb7e5` | 231 | every named witness found in the passing TRX, mutation, crash, browser, budget or scan artifacts for this SHA |
| Policy review | 7 | not automated; includes permanent storage and cleanup numerical approval items |
| Independent review (S25) | 8 | not automated; outstanding |
| Missing, partial or unmapped technical witness | 0 | `audit-g13-g15-assertions.py --enforce-complete` exit 0 |

Each witness name was also confirmed to exist as a test method in the source. The audit proves that the named witness executed and passed; whether each witness truly proves its atomic clause is the content-adequacy question assigned to the independent S25 reviews. The author checked the four new mappings and the capacity tests directly (see "Test and harness changes").

## Test and harness changes made in this closure

Application source under `src/` was not changed. Changes since `1b878736`:

1. `M13TenantCapacityAcceptanceTests`: the audited correction from `1b878736` (bounded batches, 16-per-call cap, intended report removed, capacity restored) is retained and strengthened with identity checks that the three surviving snapshots are the live ones. A new test seeds 40 expired snapshots with the oldest possible expiry and proves exactly 16 are removed by each of the first two calls (24 and then 8 synthetic snapshots remain), that limits 0, 17, -1, 1000 and NULL and a non-runtime login are rejected with `insufficient_privilege`, and that shipment records are untouched. Sensitivity check: with the limit guard loosened to 1000 the new test fails (`Assert.Throws` failure, kept as `evidence/targeted-01/logs/supplemental-sensitivity-limit-1000.log`); this check is supplemental and is not one of the counted mutants.
2. Inventory: the four added assertions above, and the audit script's expected total changed from 242 to 246. Verifier scenario references and required-test list include both capacity tests.
3. Evidence scripts: an explicit local mode (`HARBOROPS_EVIDENCE_MODE=local`) was added to the reconciler, scenario checker, assertion audit and sensitive-evidence check. Local mode takes the real source SHA, a campaign identifier and explicit input and output folders; derives each job result from the recorded exit status of every command in `commands.tsv`; requires the SHA to equal the checked out `HEAD` and the tracked tree to be clean; and labels the ledger `derived_from_github_actions: false`. No `GITHUB_*` value is read or fabricated in local mode, and hosted mode is unchanged.
4. Mutation and crash scripts record the actual checked out `HEAD` when `GITHUB_SHA` is absent (the G13 mutation script previously recorded the literal "local") and accept an exact PostgreSQL container name so several disposable servers can coexist.
5. `M13ScaleLoadTests` can write evidence to a configured workspace.

## Attempts, failures and resolutions

Nothing was deleted. Superseded artifacts are under `evidence/c1/superseded` and the aborted first attempt's logs are in `evidence/c1/logs-attempt1-aborted`.

| Attempt | Outcome | Cause and resolution |
| --- | --- | --- |
| Hosted run 42834960000 at `d6e02bec` | 13 jobs passed, PostgreSQL job and reconciliation failed | Test assumed exactly one expired report; database-wide cleanup removed eight. Test corrected in `1b878736`, audited and strengthened here |
| Browser rehearsals 1 to 4 (development only) | Failed | Harness defects: temp path escape, compose file location, missing fixture directories, and a rehearsal started while another was cleaning up. All fixed; rehearsal 5 passed |
| Campaign c1 attempt 1 | Aborted | Concurrent containers shared a Roslyn compiler server and temp folder, and the helper did not pass the exclusive PostgreSQL variable to the mutation scripts. Fixed, campaign restarted |
| `combined-source`, `dependency-security`, three mutation jobs, first pass | Failed, superseded | Same two harness defects, including an unset variable in the helper. Rerun passed |
| `g13-faults`, first pass | Test passed; job failed | Its build step raced with two sibling jobs building in the same checkout (file copy lock). Rerun alone passed |
| Ledger, first pass | Failed | A stale database variable pointed at a removed container. Rerun passed |

No product defect was found at `5bcb7e5`. No test expectation, workload size or policy threshold was weakened.

## Local environment

Windows host with Docker Desktop 29.6.2 (8 GB, 12 CPUs). Runner image built from `mcr.microsoft.com/dotnet/sdk:10.0` (.NET SDK 10.0.403) with Python 3.12.3, Node v22.23.4 and the Docker CLI (image id `sha256:0034f02b...b3964`). PostgreSQL `postgres:16.15` (Debian 16.15-1.pgdg13+2, image `sha256:f9a7b9eb...9657`). Browser stage on the host: Node v24.20.0, npm 11.19.0, Playwright 1.64.1, Docker Compose v5.3.1. Everything ran on E: with the NuGet, npm and Playwright caches under `g13-g15-final\cache`. The browser stack used a derived compose file (`compose.g13g15.local.yaml`, copied into the browser artifact together with its diff): project `ho-verify-g13g15`, API port 8093, web port 5183, PostgreSQL port 55413, subnet `172.30.253.0/24` and a private PostgreSQL image tag, because unrelated containers occupy the default ports and subnet. M15 delivery was enabled only inside that disposable stack, as in the workflow.

One side effect to disclose: a first browser rehearsal ran `playwright install` against the shared cache `E:\HarborOps-Verification\playwright-browsers`, and Playwright pruned the older browser builds stored there (chromium-1248, chromium_headless_shell-1248, ffmpeg-1013, firefox-1555, webkit-2370, winldd-1007). They are regenerable downloads and not evidence; they will be downloaded again on next use. This campaign then used its own cache.

## Policy constraints

Binding decisions were left unchanged: M15 P01 to P14 (P10 fail-closed verified by the HTTP outage and crash tests; P12 provisional retention with notification deletion disabled), M9 parked ARRI/DEPA controls and the 10,000 unresolved-work cap, null ETA behavior, M10 projector default-off, and M11 evaluator opt-in. `G13G15FeatureDefaultAcceptanceTests` passed. In source, `M15:EnableDelivery`, `M11:EnableEvaluator` and `M10:EnableProjector` each gate their hosted service. `compose.yaml` gains one line, `M15__EnableDelivery` defaulting to `false`.

## Merge-readiness inspection (read only, no merge performed)

- `git merge-tree` of `main` (`eb529368`) with `5bcb7e5`: no conflicts (exit 0). The branch is 295 commits ahead and 0 behind `main`.
- Changed areas: `tests/` (40 files), `web/src` (9), `src/HarborOps.Infrastructure` and `src/HarborOps.Api` (7 each), `src/HarborOps.Domain` (2), scripts and documents, and `.github` (9 files). No localization or Turkish paths are touched.
- Three EF migrations are added and are applied only by the explicit migration command: `20261109010000_M15ScopedNotifications`, `20261109020000_M13OperationalKpis`, `20261110010000_M13TenantReportCapacity`. Populated upgrade and model snapshot tests pass.
- **Item for reviewers:** `OperationalKpiCleanupService` is registered by default whenever the API connects as the restricted `harborops_api` login. It is the implementation of the selected G13 idle cleanup rule (at most 16 deletions per call, at most 8 calls per 5-minute poll, expired reporting snapshots only, never M11 history, M15 notifications, audit or source history). It is separate from the P12 notification retention cleanup, which remains absent. Confirm that this default-on snapshot cleanup is inside the intended boundary before any merge.
- **Item for reviewers:** the branch adds dormant GitHub workflows and one trigger file under `.github` and rewrites `verify-m11-m12-combined.yml`. They were not used. Decide whether they should be part of a merge into `main`.
- Remaining conditions: two independent S25 reviews and two positive gate decisions. The reviewed SHA is stable at `5bcb7e5` and all evidence in the packet matches it.

## Evidence index

| Item | Location |
| --- | --- |
| Raw evidence root | `E:\HarborOps-Verification\g13-g15-final\evidence\c1` (artifacts, logs, superseded attempts, gate ledger, harness copies) |
| Packet summary | `PACKET-SUMMARY.md` and `PACKET-SUMMARY.json` in the evidence root |
| Assertion matrix | `ASSERTION-MATRIX.csv` (246 rows) in the evidence root |
| Master checksum manifest | `PACKET-SHA256.txt` in the evidence root (939 files), sha256 `f0a66424ea6ec64e3981548c4c6831fc71b8b3b854a7be2020a459d46fbbe31c` |
| Archive | `E:\HarborOps-Verification\archives\g13-g15-local-evidence-c1-5bcb7e5.tar.gz`, sha256 `4dbba79d11ee38eeb03b956dd79e93b904f7a3c7ff91f15a8781ea4b120ea99d` |
| Gate ledger | `gate-ledger/index.json`, `matrix.md`, `assertion-audit.json`, `technical-scenarios.json`, `sensitive-evidence-audit.json` |
| Reproduction | `harness/` (runner scripts, job definitions, runner Dockerfile) and `commands.tsv` plus a `.cmd.sh` file per test step in every artifact |

Historical failed-run results and the earlier `15cabd8` gate decisions are unchanged and remain the record for those SHAs.
