# G13 and G15 Local Closure Verification Results, Campaign c3, 2026-11-11

**Status:** Technical verification PASSED locally on the frozen source below, after remediation of the S25 round 2 findings. Independent S25 round 3 has **not** been performed. G13 and G15 acceptance decisions: **acceptance pending**. G13-S20 needs the owner's scope decision ([decision record](G13_S20_SCOPE_DECISION_20261111.md)). This record is not a gate decision, not a merge authorization and not a GitHub Actions result. No merge, deployment, migration, dispatcher, projector, evaluator or retention deletion is authorized.

**Frozen tested source:** `4e7b9e22aacef25f7f60e2295938ec6cf79bdadd` on `feature/g13-g15-acceptance-closure-20261110` and `verify/g13-g15-evidence-closure-20261109`. Protected `main` is unchanged at `eb529368a2447061fc1838cd3967b0ffedae669d`. Commits after this SHA must change documentation only (`git diff --name-only 4e7b9e2 HEAD` lists only `docs/` and `README.md`).

GitHub Actions was not dispatched, triggered, rerun or monitored. `.github/verification/g13-g15-once-20261109.txt` was not modified. This record supersedes the [c2 results](G13_G15_LOCAL_CLOSURE_RESULTS_C2_20261111.md) and the [interim record](G13_G15_REMEDIATION_C3_INTERIM_20261111.md) for the current source.

## What changed since `3e63bd0` (round 2 source)

See the table in the [interim record](G13_G15_REMEDIATION_C3_INTERIM_20261111.md). In short: early per-tenant admission control (G13), a per-intent dispatcher time limit and exact unread count contract (G15), many new clause-level tests and 20 new mutants, a clause-level assertion mapping with six added assertions, and web, browser and operations changes.

## Verification results by group (campaign c3)

Raw evidence root: `E:\HarborOps-Verification\g13-g15-final\evidence\c3`. Disposable PostgreSQL 16.15 per job; restricted `harborops_api` role where the hosted workflow used it.

| Group | Result | Detail |
| --- | --- | --- |
| Source, contracts, planning docs, Release build with warnings as errors, unit, contract, web (including the inline style guard) | PASS | 371 TRX tests |
| Database job (14 ordered filters, now including the admission, security envelope, downgrade guard and runtime opt-in classes) | PASS | 585 TRX results in the whole campaign, 0 failed |
| Cross-milestone restricted regressions | PASS | 63 tests |
| G13 rollback, cancellation, timeout boundary and recovery | PASS | 20 s hold completes, 28 s hold returns 503 at about 25 s with no partial rows |
| G13 scale under the restricted API role, reference 170,000 shipments (Draft and Cancelled, 450,006 members) | PASS | 30 samples, p50 8,988 ms, p95 9,670 ms against 15,000 ms and 20,000 ms; peak working set 586,260,480 bytes; CPU 7,251 ms |
| G13 scale, realistic skewed 85,000 shipments (466,448 members) | PASS | p50 8,293 ms, p95 8,821 ms, max 9,241 ms; peak working set 587,214,848 bytes; report storage growth 1.83 GB of the 2 GiB assertion |
| G13 concurrency | PASS | 5,002 shipments, 4 users, 4 reports, 687 ms. Realistic 85,000, four builders: 4 of 4 HTTP 200 in 18.2 s. Realistic 85,000, eight distinct builders: 4 of 8 HTTP 200 in 15.0 to 16.3 s, the other 4 HTTP 429 `report_tenant_capacity_exceeded` in 122 ms each. Realistic 170,000, four builders: 4 of 4 HTTP 413 `report_capacity_exceeded` in 19.9 to 20.1 s, no 503 |
| G13 source mutants | PASS | clean control PASS, 41 of 41 KILLED |
| SQL security mutants | PASS | clean control PASS, 4 of 4 KILLED |
| G15 mutants | PASS | clean control PASS, 25 of 25 KILLED (rerun alone; see attempts) |
| G15 million-row scoped feed | PASS | 1,000,000 receipts, 10 pages, 500 members, p50 30 ms, p95 57 ms. Unread count 200 in 70 ms (exactly 1,000 and capped), Unread filter 30 ms, Read 152 ms, Acknowledged 143 ms |
| G15 dual dispatcher, 250 additional members, 100 readers | PASS | 252 members, fan-out 37 ms, 100 readers in 208 ms, at most 32 concurrent database readers, exactly once true |
| Process SIGKILL before and after commit | PASS | 8 of 8 phases KILLED_AND_VERIFIED |
| Chromium, Firefox, WebKit: 11 notification cases and 8 KPI cases each | PASS | 57 of 57 case executions, first attempt |
| Dependency advisories and committed-secret scan | PASS | no high or critical findings |
| Evidence reconciliation (local mode) | PASS | verify, scenarios, assertion audit (`--enforce-complete`) and sensitive-evidence scan all exit 0 |

Mutant kill notes: `member-sql-shipment-tenant-predicate-removed` and `k03-first-event-deduplication-removed` are killed by a database level failure that the API returns as HTTP 503 (row security check or duplicate key on the member rows), not by a content assertion; the database constraints are the control for those two. All other new mutants fail on a content assertion.

## Assertion reconciliation

The inventory holds **263** assertions: 248 automated technical, 7 policy review, 8 independent review. `audit-g13-g15-assertions.py --enforce-complete` exits 0: every automated assertion now lists only witnesses that prove its own clause (named tests, named job steps, named mutants, named browser cases) and every named witness executed and passed in this campaign. The audit proves the witness ran and passed; whether each witness proves its clause is the question for the round 3 reviewers. The assertion mapping and the per-assertion proof of each is in `docs/G13_G15_ASSERTION_INVENTORY_20261109.json` and `ASSERTION-MATRIX.csv` in the evidence packet.

## Disposition of the round 2 findings

| Finding | Disposition |
| --- | --- |
| G13 F1: dropped mutants, unwitnessed protections | Added mutants for the tenant predicate in the member SQL, both scoped-reader organization filters, first-event de-duplication and ordering, the tenant quota lock, both admission mechanisms, session-wide planner settings, the D05 plan predicate, voyage assignment end and idle cleanup registration; a two-tenant test of the privileged readers |
| G13 F2: thin headroom, more than four builders untested | Early admission; six-user and eight-builder tests, measured above. Heavy concurrency is now bounded to the tenant's remaining capacity (at most four) |
| G13 F3: weak S19-A07 and A08 witnesses | Exact error body key set and message; before and after counts over shipments, status history and assignments, also for idle cleanup |
| G13 F4: scenario-level mapping | Rewritten at clause level |
| G13 F5: practical tenant size limit | [Scope decision](G13_S20_SCOPE_DECISION_20261111.md), awaiting the owner |
| G13 F6: quota wording | Corrected: the 429 needs three or more sessions of one user inside ten minutes |
| G13 F7 to F10 | F7 (fixture realism), F8 (cost of a refusal), F9 (cancel during commit) are recorded as residual risks. F10: a pooled, no-reset host now makes a leaked planner setting observable, with its mutant; the 2 GiB assertion has about 15 percent headroom and is recorded |
| G15 Medium 1: unread cap | Boundary tests at 999, 1000, 1001 and 1,000,000 rows; capped flag; browser `1000+` and exact-count case; three mutants |
| G15 Medium 2: delivery timeout only under a role setting | Each intent has its own limit applied by the application (`M15:DeliveryEventTimeoutSeconds`, default 15 s); test without any role setting with two slow intents; two mutants |
| G15 Medium 3: dispatcher race, readers | Both dispatchers claim work; a second dispatcher skips a locked intent (SKIP LOCKED mutant); different-user HTTP readers; Administrator denied throughout |
| G15 L1, L2 | Recorded as residual risks |
| G15 L3 | Default-off wiring proven on a started host, with a mutant; config assertions were vacuous before |
| G15 L4 | Recorded as residual risk (surrogate crash windows) |
| G15 L5 | [Quarantine recovery procedure](OPERATIONS.md) |
| G15 L6 | Administrator negative branch added to the archived target test |
| Migration edited in place | Neither edited migration is on `main`; whether any non-disposable database ever applied an earlier version is not verifiable from here |

## Attempts, failures and resolutions

Nothing was deleted. Superseded artifacts are in `evidence/c3/superseded`.

| Attempt | Outcome | Cause and resolution |
| --- | --- | --- |
| Development iterations | Several failed | Test fixture errors (assignment trigger, shipment version, drill status) fixed in the tests; three G13 mutants first survived and were fixed by new witnesses; the first planner leak test was vacuous because the shared test logins disable pooling, fixed |
| Browser rehearsals | WebKit failed once | The new policy assertion exposed WebKit `style-src-attr` events raised by a bare native `<select>`; fixed with the engine control described above, plus an SVG bar and a source guard |
| c3 first pass, G15 mutations | Failed | Mutant `outbox-claim-skip-locked-removed` timed out after 400 s and was recorded invalid. It was killed in dev; one dev rerun and the campaign hung. Cause not found: 20 traced reruns did not reproduce it. Rerun alone at the same source: 25 of 25 killed in 18 minutes. The hang is therefore intermittent and unexplained; it appeared while three mutation jobs shared the machine |
| c3 first ledger | Failed | Expected, because of the failed job |
| c3 second ledger | Audit failed | 26 assertions named tests that were never executed: the harness job filters did not include the new test classes and the eight-builder test. Harness-only fix (no source change); database and concurrency jobs rerun alone at the same source, both passed |
| c3 third ledger | PASS | all four checks exit 0 |

No test expectation, workload size, ceiling or approved threshold was weakened. The source SHA did not change during c3.

## Residual risks that remain genuine

- The scanned-row bound of the unread count is reasoned, not measured, for a backlog the user can no longer see (rows failing the per-row policy); P12 means backlogs only grow.
- The unread count and state filters rely on current planner statistics (`ANALYZE`); production depends on autovacuum timing.
- An oversized tenant is refused with 413 only after about 20 seconds of work per builder.
- A request cancelled by the 25 second token during the commit can leave a committed snapshot whose owner never sees it; it holds a quota slot for ten minutes.
- Some crash windows remain surrogates (raw administrator statements, hand-signed scopes); the PostgreSQL semantics make the results valid.
- Large-tenant fixtures contain no M10 equipment conflicts or corrections (the reference tests cover them).
- The G13 concurrency limit is per tenant. Builders of many different tenants are not limited against each other.
- The report storage growth assertion (1.83 GB used of 2 GiB) has about 15 percent headroom.
- The intermittent hang of the skip-locked mutant test is unexplained.
- Local mode evidence is self-reported by the closure session; the archive hash below is the anchor.

## Evidence index

| Item | Location |
| --- | --- |
| Raw evidence root | `E:\HarborOps-Verification\g13-g15-final\evidence\c3` |
| Packet summary, assertion matrix, checksum manifest | `PACKET-SUMMARY.md`, `ASSERTION-MATRIX.csv` (263 rows), `PACKET-SHA256.txt` in the evidence root |
| Archive | `E:\HarborOps-Verification\g13-g15-final\evidence\c3-evidence-4e7b9e2.zip`, sha256 `d0e12322162479686708570b254c4e99cdbb843ceffdd6f657a75348cc7522ec` |
| Gate ledger | `gate-ledger/index.json`, `matrix.md`, `assertion-audit.json`, `technical-scenarios.json`, `sensitive-evidence-audit.json` |
| Reproduction | `harness/` (runner scripts and job definitions, including the harness fix) and `commands.tsv` plus a `.cmd.sh` per step in each artifact |
