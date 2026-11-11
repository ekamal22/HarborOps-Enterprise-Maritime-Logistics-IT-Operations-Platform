# G13 and G15 Local Closure Verification Results, Campaign c2, 2026-11-11

**Status:** Technical verification PASSED locally on the frozen source below, after remediation of the round 1 findings. Independent S25 review round 2 has **not** been performed. G13 and G15 acceptance decisions: **acceptance pending**. This record is not a gate decision, not a merge authorization and not a GitHub Actions result. No merge, deployment, migration, dispatcher, projector, evaluator or retention deletion is authorized.

**Frozen tested source:** `3e63bd0e71bdf4a80cd8ea1d27e7cceca3c37524` on `feature/g13-g15-acceptance-closure-20261110` and `verify/g13-g15-evidence-closure-20261109`. Protected `main` is unchanged at `eb529368a2447061fc1838cd3967b0ffedae669d`. Commits after this SHA must change documentation only; each is checked with `git diff --name-only 3e63bd0 HEAD`, which must list only files under `docs/` and `README.md`.

GitHub Actions was not dispatched, triggered, rerun or monitored. `.github/verification/g13-g15-once-20261109.txt` was not modified. This record supersedes the [campaign c1 record at `5bcb7e5`](G13_G15_LOCAL_CLOSURE_RESULTS_20261110.md) for the current source; that record and the round 1 reviews remain the history for `5bcb7e5`.

## Decisions applied

| Decision | Implementation at `3e63bd0` |
| --- | --- |
| 503 is not accepted as normal behavior; realistic concurrent testing first | Skewed realistic tenants (legal transitions, evaluations, exceptions, `ANALYZE`, `CHECKPOINT`) at 85,000 and 170,000 shipments, four simultaneous builders |
| Two active reports per user inside four per tenant | `m13_user_quota` and refusal `report_user_capacity_exceeded` (HTTP 429); tenant refusal remains `report_tenant_capacity_exceeded` |
| Automatic KPI cleanup behind an explicit switch, default OFF | `OperationalKpiCleanupService` is registered only when `M13:EnableIdleCleanup` is true and the login is `harborops_api`; `compose.yaml` passes `M13__EnableIdleCleanup` defaulting to `false`; poll period `M13:IdleCleanupPollSeconds` (1 to 300) |
| Verification-only workflow changes excluded from the merge | `check-m15-contract.mjs` no longer asserts workflow content and instead asserts that no workflow enables M15 delivery; the verification workflows are not relied on for acceptance |
| No hosted GitHub Actions at the merge SHA | None used. If branch protection requires a hosted check, that is a separate blocker for the owner |
| Option 1: scoped 25 s member-materialization limit | `MemberMaterializationTimeout` 25 s for that statement only (global 15 s unchanged), request envelope 28 s inside the 30 s proxy timeout; 500,000-member ceiling and the 15 s p50 / 20 s p95 targets unchanged |

## Application changes since `5bcb7e5`

Round 1 was evidence only; c2 follows product changes. Root causes found and the smallest proven fixes:

1. **Member materialization planner (G13 performance).** Without statistics the planner chose nested loops over materialized CTEs and a per-row security function cost of about 190 microseconds per row. `SET LOCAL enable_nestloop = off` and the scoped statement timeout are applied only around the member insert, then restored to `on` and `15s` before the transaction continues. The two helper reads (`m13_scoped_evaluations`, `m13_scoped_exceptions`) are `SECURITY DEFINER` set-returning functions gated on `harborops_api` and `m13_can_view`.
2. **Four simultaneous builders.** The tenant lock is no longer held across materialization: the transaction is READ COMMITTED, materializes first, then takes the tenant lock for the quota check with a wait of `clamp(28 s - elapsed, 3 s, 15 s)`. The client command timeout is the scoped limit plus 5 s.
3. **Timeout trap in M15 delivery.** `m15_deliver_pending` now traps `QUERY_CANCELED` (statement timeout) as `delivery_timeout`, so a database timeout cannot escape as an unbounded failure.
4. **Unread count.** `UnreadCountCap = 1000`; the bounded count returns `unreadCount`, `unreadCountCapped` and `asOfUtc`, and the web client shows `1000+`.
5. **Migration and grants.** `20261110010000_M13TenantReportCapacity` adds `m13_user_quota`, `m13_scoped_evaluations` and `m13_scoped_exceptions`; `AuditRoleProvisioner` grants EXECUTE to the restricted login; the migration revokes PUBLIC execute and sets `search_path`.

## Round 1 findings and their disposition at `3e63bd0`

| Finding | Disposition |
| --- | --- |
| G13 F1: scale and concurrency tests used Draft/Cancelled shipments with no history, only 5,002 rows | Realistic skewed fixture with evaluations and exceptions. Four simultaneous builders at 85,000 shipments: 4 of 4 HTTP 200, 18.6 s total, each 17.3 to 18.6 s, inside the 28 s envelope and 30 s proxy timeout. At 170,000: 4 of 4 HTTP 413 `report_capacity_exceeded` (over the 500,000-member ceiling), 19.6 s. No 503 |
| G13 F2/F3: two mutants killed by key violations; weak "no counts" and "no business record removed" witnesses | Replacement mutant `k03-counts-arrival-events-as-completions`; new mutant `member-materialization-timeout-reverted-to-15s` and a planner-guard mutant retargeted to the combined statement. Mutant set is now 29 (G13 source), 4 (SQL security) and 17 (G15), all KILLED with clean controls |
| G13 F4: one user with four sessions could hold the tenant quota | Two active reports per user (`OneUserIsLimitedToTwoActiveReports...`) |
| G13 F5: cleanup service untested, enabled by login name, period over 5 minutes | Explicit switch, default off, configurable period, and `IdleCleanupIsOffByDefaultAndRunsOnlyWhenExplicitlyEnabled` plus `M15DeliveryAndM13IdleCleanupRequireExplicitOptIn` |
| G15 M1: unread count and state filters unmeasured at 1,000,000 rows | Bounded count (cap 1,000) and `ANALYZE` after bulk load; measured at 1,000,000 receipts: unread count 200 in 77 ms, Unread filter 28 ms, Read filter 184 ms, Acknowledged filter 134 ms |
| G15 M2: 1,000-recipient cap has no runtime test | `G15RemediationAcceptanceTests` (9 tests): recipient cap, dispatcher timeout, acknowledgement isolation and Auditor denial, default page size and cursor lifetime, cancelled and archived targets, malformed bodies, failed outbox intent through a real M11 evaluate, competing dispatchers, stale version. HTTP outage test extended to count, detail and the PATCH action |
| G15 M3: at least 14 clauses lacked a proving witness | Addressed by the tests above and the added mutants; whether each clause is now proven is the content-adequacy question for round 2. The inventory is now 257 assertions |
| Both: dormant `.github` workflows, `verify-m11-m12-combined.yml` | Decision applied: excluded from reliance and from the merge scope; see the merge-readiness section |
| Both: local-mode evidence versus one hosted run | Decision applied: no hosted run. Local mode is labeled `derived_from_github_actions: false` |

## Verification results by group

All jobs ran in campaign `c2` against `3e63bd0`. Raw evidence root: `E:\HarborOps-Verification\g13-g15-final\evidence\c2`. Disposable PostgreSQL 16.15 per job; the restricted `harborops_api` role where the hosted workflow used it.

| Group | Result | Detail |
| --- | --- | --- |
| Source, contracts, planning docs, Release build with warnings as errors, unit, contract, web | PASS | 371 TRX tests; web unit tests and production build; M11 and M15 contracts; execution graph check |
| M15 database job (14 ordered filters including G15 remediation, capacity, restricted SQL, revocation, rate limit, recurrence, boundaries) | PASS | 117 tests |
| Cross-milestone restricted PostgreSQL regressions (M2, M5, M10, M11, M12, M16) | PASS | 63 tests |
| G13 rollback, cancellation, timeout boundary and recovery | PASS | blocked materialization rolls back and recovers; 20 s hold completes, 28 s hold returns 503 at about 25 s with no partial rows; planner setting verified default on pooled connections after success, failure and rollback |
| G13 scale under the restricted API role, reference 170,000 | PASS | 30 samples, p50 9,211 ms, p95 10,989 ms (max 12,248 ms) against 15,000 ms and 20,000 ms; peak working set 586,858,496 bytes; process CPU 7,841 ms |
| G13 scale under the restricted API role, realistic skewed 85,000 | PASS | 30 samples, p50 8,829 ms, p95 9,287 ms, max 9,415 ms; 466,448 members; peak working set 588,115,968 bytes; CPU 7,115 ms. Restricted-role EXPLAIN plans archived with SHA-256 for small, medium10k, realistic85k, skewed170k and empty |
| G13 concurrent builders | PASS | 5,002 shipments, 4 users, 4 reports, 1,050 ms; realistic 85,000 four builders 4 of 4 HTTP 200; realistic 170,000 four builders 4 of 4 HTTP 413 |
| G13 source, SQL security and G15 mutation sensitivity | PASS | 29 of 29, 4 of 4 and 17 of 17 KILLED; every control clean |
| G15 million-row scoped feed and keyset | PASS | 1,000,000 receipts, 10 pages, 500 members, p50 31 ms, p95 50 ms |
| G15 dual dispatcher, 250 additional members, 100 readers | PASS | 252 members, 2 dispatchers, fan-out 33 ms, 100 readers in 221 ms, at most 32 concurrent database readers, exactly once true |
| Process SIGKILL before and after commit | PASS | 8 of 8 phases KILLED_AND_VERIFIED; recovery PASS where required |
| Chromium, Firefox and WebKit: notifications (10) and operational KPIs (8) | PASS | 54 of 54 case executions, 0 unexpected, 0 skipped, 0 flaky. Passed on attempt 3; see attempts |
| Dependency advisories and committed-secret scan | PASS | npm and NuGet high or critical advisories 0 |
| Evidence reconciliation (local mode) | PASS | integrity PASS; 565 TRX results passed, 0 failed, 0 skipped, 0 other; verify, scenarios, assertion audit (`--enforce-complete`) and sensitive-evidence scan all exit 0 |

The admin-role plan for 170,000 (`explain-error.txt`) is expected to be refused with `Reporting read authority denied`; reporting reads require the signed scope and the restricted role.

Scenario ledger statuses remain conservative: PASS for S02 and BLOCKED for the others per gate, S25 NOT EXECUTED. BLOCKED means targeted technical evidence exists and reviewer disposition is outstanding. None is an acceptance.

## Assertion reconciliation

The inventory now has **257** assertions: 242 automated technical, 7 policy review and 8 independent review. `audit-g13-g15-assertions.py --enforce-complete` exits 0, so every automated assertion maps to named witnesses that executed and passed at this SHA. As with c1, that proves the witness ran and passed, not that it proves its atomic clause; content adequacy is the round 2 reviewers' question. Round 1's dispute about at least 14 G15 clauses is addressed by new tests but not yet independently re-reviewed.

## Attempts, failures and resolutions

Nothing was deleted. Superseded artifacts are under `evidence/c2/superseded` and logs are kept.

| Attempt | Outcome | Cause and resolution |
| --- | --- | --- |
| Development iterations before the freeze | Several failed | Timeout and lock scope misdiagnosed once and corrected; test-order effects from accumulated database state fixed with `CHECKPOINT`, a fresh database per realistic test and a fresh schema in the mutation harness; one surviving mutant replaced. Details in the session record, none relied on as gate evidence |
| c2 first launch | Aborted | Harness defect: the shared `src` clone was not fetched to the campaign SHA (exit 90). Fixed in the phase 0 script; artifacts kept in `evidence/c2-aborted-launch-clone-sha` |
| Browser job attempt 1 | Failed, 1 of 54 | WebKit first KPI test received 429 `report_user_capacity_exceeded`: Chromium and Firefox had left the shared Manager holding two live snapshots (10 minute lifetime). Behaves as designed; harness defect (shared database and user across engines). Fixed by resetting the report table on the disposable database before each engine's KPI suite and recording the rows removed (0, 1 and 1) |
| Browser job attempt 2 | Failed before any test | Compose `migrator` container exited 139 while database and fault jobs ran in parallel. Not rerun in parallel again |
| Browser job attempt 3, run alone after all other jobs | PASS | All six suites passed |
| Ledger first pass | Failed as expected | Browser evidence was missing; reran phase 5 after attempt 3 and all four checks exited 0 |

No test expectation, workload size, ceiling or approved threshold was weakened. The source SHA did not change during the campaign.

## Observed behavior to be aware of

With the per-user limit of two and idle cleanup off by default, a user who opens the KPI dashboard three times within the 10 minute snapshot lifetime receives HTTP 429 `report_user_capacity_exceeded` until the oldest snapshot expires (expired snapshots no longer count toward the limit). This follows from the approved limits and is not a defect, but the web client does not release a snapshot on navigation. Whether that experience is acceptable, or whether the client should release snapshots, is a product decision.

## Local environment

Windows host with Docker Desktop (8 GB, 12 CPUs). Runner image from `mcr.microsoft.com/dotnet/sdk:10.0` (.NET SDK 10.0.403) with Python 3.12, Node 22 and the Docker CLI; PostgreSQL 16.15 (Debian). Browser stage on the host: Node v24.20.0, Playwright 1.64.1, Docker Compose, in a derived compose project `ho-verify-g13g15` (API port 8093, web 5183, PostgreSQL 55413, subnet `172.30.253.0/24`). Everything ran on E:, with a dedicated Playwright cache. The earlier disclosure about the shared Playwright cache pruned during a rehearsal stands (regenerable downloads, no evidence).

## Policy constraints

Unchanged: M15 P01 to P14 (P10 fail-closed, P12 provisional retention with notification deletion disabled), M9 parked ARRI/DEPA controls and the 10,000 unresolved-work cap, null ETA behavior, M10 projector default-off, M11 evaluator opt-in. `G13G15FeatureDefaultAcceptanceTests` passed, now also proving M15 delivery and M13 idle cleanup require explicit opt-in. No production feature, dispatcher, migration, deletion job or notification retention cleanup is enabled.

## Merge-readiness inspection (read only, no merge performed)

Re-inspected against `main` in the final report; see the packet. The branch adds three EF migrations applied only by the explicit migration command, changes no localization paths, and keeps all feature defaults off. The dormant verification workflows and the trigger file are not relied on and are for the owner to exclude from any merge. Remaining conditions: independent S25 round 2 for both gates, then positive gate decisions.

## Evidence index

| Item | Location |
| --- | --- |
| Raw evidence root | `E:\HarborOps-Verification\g13-g15-final\evidence\c2` |
| Packet summary and assertion matrix | `PACKET-SUMMARY.md`, `PACKET-SUMMARY.json`, `ASSERTION-MATRIX.csv` (257 rows) in the evidence root |
| Master checksum manifest | `PACKET-SHA256.txt` in the evidence root |
| Archive | `E:\HarborOps-Verification\g13-g15-final\evidence\c2-evidence-3e63bd0.zip`, sha256 `ada16acdfb7df19f97b4c79c972f345174b3fe92080906aaaf761bda1841b060` |
| Gate ledger | `gate-ledger/index.json`, `matrix.md`, `assertion-audit.json`, `technical-scenarios.json`, `sensitive-evidence-audit.json` |
| Reproduction | `harness/` (runner scripts and job definitions) and `commands.tsv` plus a `.cmd.sh` per step in each artifact |
