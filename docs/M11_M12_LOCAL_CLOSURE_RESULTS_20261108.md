# M11/M12 Local Closure Results

**Date:** 2026-11-08
**Branch:** `feat/m11-m12-combined-integration`
**Hosted application SHA (earlier evidence):** `f56d017d9b2619be56fe533f73ba1d11f63974c0`
**Closure application source:** hosted source plus two fixes, `5dedbb7` (export serialization retry) and `b2a72bf` (explicit UTC designator). Application source is identical from `2eee441` onward; later commits change tests and the stack probe only.
**Accepted baseline:** protected `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`, unchanged
**Status:** Technical evidence for the independent S25 re-reviews. **G11 and G12 acceptance pending.** No merge, deployment, release or production migration occurred.

Dispositions are in the [G11 matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12 matrix](M12_G12_EVIDENCE_MATRIX_20261108.md); the machine-readable index is [M11_M12_LOCAL_EVIDENCE_INDEX_20261108.json](M11_M12_LOCAL_EVIDENCE_INDEX_20261108.json).

## History

1. **First local campaign** (`5dedbb7`, `3d450ac`, documentation `8571050`): focused tests, measured plans and one defect fix; package `m11m12-local-01`, 277 files.
2. **Independent reviews** (2026-11-08): [G11](M11_G11_INDEPENDENT_EVIDENCE_REVIEW_20261108.md) and [G12](M12_G12_INDEPENDENT_EVIDENCE_REVIEW_20261108.md) verified the package and returned **G11-S25 NOT PASS** and **G12-S25 NOT PASS** with open mandatory findings. The recommendations in the first version of this record were made before those reviews.
3. **Closure campaign** (this record): addresses every named finding on the final application source; package `m11m12-local-02`.

## Review findings and the evidence that answers them

| Open finding | Closure evidence |
| --- | --- |
| G11-S21 commit succeeds and the reply is lost | `EvaluationWithLostCommitOutcomeRecoversWithoutDuplicates` (durable commit with the reply dropped: 1 exception, 4 evaluations, 1 Opened; commit never delivered: 0/0/0; both converge to one set) and `DispositionWithLostReplyIsReplayedByIdempotencyKeyWithoutDuplicates` (committed version 2, same-key retry Replayed, one replay row, one audit) through a disposable commit socket proxy |
| G11-S21 real evaluator process termination | Compose stack `g11-faults.json`, 12/12: SIGKILL with the evaluation transaction open (0 exceptions, 0 opened, rule not Active) then restart completes exactly once; SIGKILL after the commit is visible (1/4/1 stable for 40 s); PostgreSQL SIGKILL with crash recovery and orderly stop and start (1/4/1); no duplicates anywhere |
| G11-S20 simulated clock | `CheckpointCrossedInRealTimeWhileTheHostIsDownIsRecoveredOnRestart`: host stopped before the stored checkpoint, 52 s of real time pass it with no stored data changed, the restarted evaluator opens exactly one exception (supplement at `6e90295`, 7/7); the earlier simulated case remains, labelled |
| G11-S08 equal-instant tie never forced | `WitnessTieBreaksOnSourceThenCollationOrderedSourceEventWhenReceiptInstantsAreEqual`: receipt instants set equal, witness follows source id then C-collation source event id (`tie-Z` over `tie-a`) in both source orders, computed independently by ordinal comparison |
| G11 S05, S06, S07, S16 partial | `RuleStateAndSourceConfidenceMatrixHoldsThroughHttpAndRestrictedSql` (10 cases) and `ConcurrentEvaluatorsOpenExactlyOneRecurrenceOccurrence` |
| G11/G12 S22 browsers not local | 48/48 across Chromium, Firefox and WebKit through Nginx with raw JSON (expected 48, unexpected 0, skipped 0, flaky 0) |
| G11/G12 S23 final-SHA regression | complete integration suite **388/388, no skips** on an exclusive disposable PostgreSQL server at `2eee441`, with 291/291 unit and 46/46 contract; no change-impact waiver is needed |
| G11/G12 S24 hosted-only mutants and scans | 17/17 effective mutants killed locally; dependency, source and three image scans with 0 HIGH or CRITICAL |
| G12-S17/S19 real proxy, mid-download, CPU | Compose stack `g12-network-rerun.json`, 32/32 (below) |
| G12 S03 to S08, S10, S12 partial | `M12MatrixRuntimeTests` (3 tests) |
| S01 formal decisions, signed M9 hold | recommendations below; decisions remain separate |

## What was executed

| Stage | Result | Raw report (under `E:\HarborOps-Verification\evidence\m11m12-local-02`) |
| --- | --- | --- |
| Release build, warnings as errors, no incremental | 0 warnings, 0 errors | `final/logs/build.log` |
| Unit tests | 291/291 | `final/raw/unit.trx` |
| Contract tests | 46/46 | `final/raw/contract.trx` |
| Source contract, graph and planning checks | pass | `final/logs/source-contract-checks.log` |
| **Complete integration suite** | **388/388, no skips** | `final/raw/integration-full.trx` |
| Measured query plans (23 plans) | 1/1 | `final/raw/query-plans.trx`, `final/plans/` |
| G11 process and database faults, Compose stack | 12/12 | `final/stack/g11-faults.json` |
| G12 real Nginx path, Compose stack | 32/32 | `final/stack/g12-network-rerun.json` |
| Browsers, three engines | 48/48 | `final/browser/*.json` |
| Effective mutants | 17/17 killed | `final/mutations/summary.json` |
| Dependency scans (dotnet, npm web, npm browser) | clean | `final/scans/` |
| Trivy source and postgres, api, web images | 0 HIGH or CRITICAL; image revision label equals the source SHA | `final/scans/` |
| Test supplement `M11ClosureRuntimeTests` at `6e90295` | 7/7 | `final-supplement/raw/m11-closure-class.trx` |

The 388 cases run on a single exclusive server in one invocation, including the populated 13-to-15 upgrade, the restricted-role tests and every accepted M0 to M10 and M16 suite. The query-plan probe runs separately because it seeds 170,000 rows that would slow the evaluator sweep in the suite.

## Defects found and fixed

**F-M12-01, export failed with 503 on a serialization conflict (`5dedbb7`).** The export runs at REPEATABLE READ and locks the caller's session row. Every authenticated request updates that row, so a concurrent request from the same session, or a revocation, raised SQLSTATE 40001, which the API reported as 503. It failed closed (no CSV, no audit) but a valid export could fail spuriously and a revoked caller received the wrong status. The fix retries up to three attempts inside the one 25 second budget and matches the exception chain (the EF provider wraps the error; a first fix that did not match the wrapped exception was ineffective and is retained as evidence).

**F-M4-01, instants without a UTC designator were accepted (`b2a72bf`).** The shared parser used an optional offset specifier, so `2026-03-01T00:00:00` parsed in the host's local zone and passed the zero-offset check only on UTC hosts, contrary to the explicit-UTC contract and dependent on the host time zone. The parser now requires `Z`, `+00:00` or `-00:00`. Unit cases added; 66 impacted integration tests and later the complete suite pass.

## Real network and fault measurements

Compose stack `ho-verify-m11m12` (Nginx 1.30.6, Kestrel, PostgreSQL 16.15), own ports and subnet, other stacks untouched.

| Case | Observed |
| --- | --- |
| Export via Nginx versus direct Kestrel | byte-identical, 4,870,215 bytes (worst-case 5,000 rows, 46% of 10 MiB) |
| 15 second PostgreSQL statement timeout | 503 at 15.02 s, JSON body, no CSV, no audit |
| 25 second application budget | 503 at 25.02 s, body names the request budget, no audit |
| Upstream stalled (container paused) with the request in flight | Nginx 504 at 30.18 s, no CSV, no leaked backend, lease released |
| CPU starvation, 3,500 microseconds per second | 503 at 27.88 s (internal budget expired; 2.9 s over the 25 s budget), no audit |
| CPU starvation, 4,000 microseconds per second | 200 at 25.86 s, complete CSV, one audit |
| CPU starvation, 5,000 microseconds per second | 200 at 14.92 s, complete CSV, one audit |
| Client reset after paused headers and after 64 KiB | partial body only, 0 idle-in-transaction, backends unchanged, next export 200 and byte-identical |
| Client disconnect while queued on a database lock | 0 waiting backends while the lock is still held (cancellation reached PostgreSQL through Nginx and Kestrel), no audit, lease released |
| Session revoked while queued behind Nginx | 403, no CSV, no audit |

## Findings and observations (no defect unless stated)

1. **Free-text search scales with the whole table for large tenants.** For the largest tenant (70% of 170,000 rows) the plan is a parallel sequential scan of the whole shipments heap (4,974 buffers, 35 to 60 ms). The small tenant uses an index and 300 buffers. All other statements use indexes (at most 12 ms). A trigram index is the remediation if text search must serve millions of rows. Not changed.
2. **The 10 MiB export limit cannot be reached over HTTP** (maximum about 4.9 MB). It is enforced and verified at the SQL function (10,485,760 accepted, 10,485,761 refused).
3. **End-to-end time under extreme CPU starvation can exceed the 25 second application budget** (27.88 s observed); the proxy envelope still cut or answered within 30 s in every case and no partial CSV appeared. M12-P10 already states preparation deadlines do not guarantee network timing.
4. **The M11 evaluator is opt-in** (`M11:EnableEvaluator`, default off, kept off). Its due scan pages 25 shipments per 200 ms, so a sweep takes about N/125 seconds for N shipments; with 5,000 unevaluated shipments a new shipment waited behind the sweep in a rehearsal. Index a due queue before enabling it by default.
5. **Export lease and rate limit are per API instance** (documented M12-P10); there is no cross-instance aggregate. Retained unchanged.
6. **The export audit proves preparation, not receipt** (documented M12-P12); a reset client still leaves one prepared audit.
7. **Equivalent mutants.** Removing only the cursor actor binding, or only the replay actor comparison, survives because the session binding and the request digest independently cover the same cases; the effective mutants remove both. Removing `COLLATE "C"` is unobservable on the Alpine image (bytewise ordering) and is killed on a Debian `en_US.utf8` cluster. These survivors are retained with reasons in the mutation summary (the attempt 2 raw report for the collation variant was overwritten by its rerun; its result is kept in `summary-subset-attempt2.json`).
8. **Simulations and limits.** One S20 case still moves stored data to simulate a clock advance and a login outage (labelled); the real-elapsed-time case supersedes it. The in-memory host cannot interrupt a body, so mid-download interruption uses the real stack. A production-like cluster, node loss and sustained load were not exercised.

## Focused evidence-correction checkpoint (2026-11-08)

The 17 effective mutation kills belong to three retained passes, not a single execution SHA. Attempt 1 ran at `ed58449c2fe4eab20ef23c2cdb7f2b5778a2fd14` (10 valid kills); attempt 2 and attempt 3 ran at `6e9029562becc27bf197a1568a60e3d676de83e6` (five and two valid kills). Neither revision changes application source after the `2eee441fa37c17101846516cf133a92c7e59335c` full-suite source. The first pass's invalid SQL mutations and equivalent non-kills are retained, not reclassified. See `M11_M12_LOCAL_EVIDENCE_INDEX_20261108.json` for all 17 variant IDs and individual source attribution.

The evidence-index archive path is corrected to a literal `E:\\HarborOps-Verification\\archives\\m11m12-local-02-evidence-20261108.tar.gz`, without a control character. The N01 live-network audit assertion now checks the exact audit-count increase across proxy and direct exports, rather than accepting any total of two or more. Focused hosted re-verification is required before claiming this corrected assertion has passed.

## Historical failures retained

First package: five plan-probe attempts, two G11 residual attempts, the F-M12-01 failing runs and the first insufficient fix. Closure package: Trivy source scan (the `mirror.gcr.io` database host did not resolve; rerun from `ghcr.io` passed), stack G12 attempts (probe mistakes: raw response text, pause after completion, CPU limit not removed by `--cpus 0`, minimum CPU quota, revoke ordering, scope of an audit baseline), the load-count check, browser fixtures output directory, the first browser run (per-spec API restart silently skipped, so the login limiter accumulated), the first mutation pass (database not reset, so six migration-SQL variants were invalid) and the equivalent-mutant survivors. All are under `logs/`, `final/` attempt directories and the first package.

## Evidence package

- Directory: `E:\HarborOps-Verification\evidence\m11m12-local-02` (562 files).
- Manifest: `MANIFEST.sha256`, SHA-256 `25b8c25097c703635628d7ca57985edef475a9a9e629a9007f946b3cf3f6700f`. Independent read-back: `READBACK.txt` (562 files re-hashed, 0 problems, every TRX re-parsed and compared with `trx-summary.json`). Safety scan: `safety-scan.json`, 0 hits.
- Transfer archive: `E:\HarborOps-Verification\archives\m11m12-local-02-evidence-20261108.tar.gz`, SHA-256 `caa8f6a1c3d43d545f9e39a7e5b95dfce785d24371d7a4426016025f989c8969`; the extracted copy was verified against the manifest.
- The first package `m11m12-local-01` is unchanged (manifest SHA-256 `7cadf6cb1edfd27ebaf7f65c357ace7496f1c331b3ee1d6d4fbb5abfbcfc12ba`, archive SHA-256 `35d43c916bc4242cafa5ecf232d437aee9e1ef7918855f3cc866de8453b69b4d`).
- Runners are retained in `final/env/`, `final-supplement/env/` and `scripts/`; the stack compose file `compose.m11m12.verify.yaml` and the probe `scripts/check-m11m12-stack.mjs` are in the repository.

## Policy and hold reconciliation (recommendations only)

- **G11-S01, M11-P01 to P12:** recommend approval as written. P03 lineage is now evidenced by S08 including the forced tie; P07 thresholds (60 minutes) remain versioned demonstrator rules; P12 held under real process and database faults. Record the evaluator sweep latency (finding 4) as an operating note, not a blocker.
- **G12-S01, M12-P01 to P16:** recommend approval with two recorded notes: P10 per-instance limits are retained and preparation deadlines do not guarantee end-to-end timing (consider lowering the 25 second budget to widen the margin under starvation, a policy change not made here), and text search needs a trigram index before production-scale volumes. P16 stays NOT APPLICABLE for a repository-owned Q2 snapshot.
- **Signed M9 operating-hold reconciliation:** the campaign changed two files under `src` and none in M9 or M10; the hold on sustained ARRI/DEPA transport processing, the 10,000 unresolved-work cap, the default-off M10 projector and null M10 ETA are covered by accepted suites inside the 388 and are unchanged. Recommend recording the hold as unchanged. The signature is a separate decision.
- **Change-impact waiver:** not requested; the complete suite ran on the final application source.

## Outstanding

1. Independent G11-S25 and G12-S25 re-reviews of the closure package and the separate acceptance decisions.
2. The formal S01 decisions and the signed M9 hold reconciliation.
3. Items outside this campaign: production-like cluster and sustained-load behaviour, a trigram index decision, and the optional indexed due queue for the evaluator.

## Technical recommendations

- **G11:** ready for independent re-review; all 23 technical scenarios have executed evidence and S01 and S25 are the only open rows.
- **G12:** ready for independent re-review under the same terms, with findings 1 to 3 recorded as accepted limits or follow-up.
- **Combined branch:** technically ready for a final merge review once the re-reviews and decisions above are recorded. This record does not authorize a merge. Protected `main`, the M9 hold, the 10,000 unresolved-work cap, the default-off M10 projector, null M10 ETA and the no-deployment boundary are unchanged.
