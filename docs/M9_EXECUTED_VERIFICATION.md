# M9 Executed G9 Technical Verification

**Status:** G9 technical campaign executed on one exact source revision. **Technical result: PASS for every executable scenario.** G9-S01 is BLOCKED (M9-01 contract acceptance pending) and G9-S25 is NOT EXECUTED (independent evidence review pending). **G9 acceptance pending.** This record is not an acceptance decision and does not authorize a merge into `main`.

**Source revision:** `0abeeb0e0eca3f639ef0036483f6cfc6e6e65440` on `feat/m9-worker-reconciliation` ([draft PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18))  
**Accepted baseline:** G0 through G8 on `main` at `7b9e19d730e3a09c5e1184262460dfeac9319e3d`  
**Run:** attempt 08, run id `20261105T015029Z-29816397e2c9920d`, 2026-11-05T01:50:30Z to 2026-11-05T02:41:20Z  
**Runner:** `scripts/verify-g9-local.sh` (clean committed tree, E:-backed root, isolated Compose project `ho-verify-g9-a08`, no GitHub Actions)  
**Evidence root:** `E:\HarborOps-Verification\evidence\g9-attempt-08\m9`

## Stage results

All 24 runner stages exited 0 (seconds in parentheses): preflight (2), script-syntax (5), compose-contract (1), documentation (1), tracking-contract (0), no-automatic-actions (0), base-images (3), postgres-image (2), build (46), nuget-advisories (40), npm-advisories (5), web-build (15), unit-tests (3), contract-tests (9), stack-up (42), stack-e2e (85), image-scans (20), stack-teardown (4), integration-tests (776), integration-repeat (778), trx-summary (1), security-mutations (1206), evidence-safety (0), evidence-summary (1).

| Suite | Tests | Passed | Failed | Skipped or not executed |
| --- | --- | --- | --- | --- |
| Unit | 226 | 226 | 0 | 0 |
| Contract | 39 | 39 | 0 | 0 |
| PostgreSQL integration, first pass | 272 | 272 | 0 | 0 |
| PostgreSQL integration, repeat pass | 272 | 272 | 0 | 0 |
| Total TRX results | 809 | 809 | 0 | 0 |

The TRX summary fails closed on zero tests, any failure and any skipped or not-executed test. Node source-contract tests (M7, M8 and M9, 24 cases) and the documentation guards also passed.

## Scenario register

| Case | Status | Primary evidence |
| --- | --- | --- |
| G9-S01 | **BLOCKED** | The six M9-01 local choices remain acceptance pending. Passing code is not approval. |
| G9-S02 | PASS | Populated M8 to M9 upgrade and guarded downgrade, EF snapshot parity, ordered migrations, M0-M2 historical upgrade, fresh-volume migration of all ten migrations in the stack |
| G9-S03 | PASS | Direct-SQL illegal transitions, deletes, Processed without effect, forged effects; attributed-redrive bypass; restricted worker role probes |
| G9-S04 | PASS | Duplicate and cross-tenant effect inserts rejected, append-only evidence, one effect per receipt |
| G9-S05 | PASS | Due-only organization polling with exclusive cursor, partial due and expiry indexes, single claim under contention |
| G9-S06 | PASS | Two independently started worker processes observed waiting on the PostgreSQL organization lock, one claim; intake and claim both serialize on the lock without deadlock |
| G9-S07 | PASS | Same external key in two organizations processed independently, no foreign association |
| G9-S08 | PASS | Heartbeat extends only a live lease; wrong token, epoch or generation and post-expiry heartbeats fail |
| G9-S09 | PASS | Expired lease takeover with epoch 2 and attempt 2, stale finalize and heartbeat fenced, one effect |
| G9-S10 | PASS | Real worker process killed after its claim committed; lease retained; replacement worker recovers after expiry |
| G9-S11 | PASS | Same process kill while the effect insert was blocked inside the finalize transaction; no effect committed, one effect after recovery |
| G9-S12 | PASS | Real PostgreSQL wire proxy cut before COMMIT reaches the server and after the server processed it; durable state is the oracle |
| G9-S13 | PASS | 12 sequential and 20 concurrent replays plus post-processing replays, one receipt, one work item, one effect; replay through the live stack |
| G9-S14 | PASS | Time-valid tenant assignment resolves; a reference that changes while a real worker is inside its effect insert is parked for reconciliation; unknown, out-of-window, foreign-tenant and archived-container references do not (a container with an active assignment cannot be archived, a database guard) |
| G9-S15 | PASS | An opaque transport token equal to an existing voyage reference is not equated with it |
| G9-S16 | PASS | Real delays within 80 to 100 percent of 5, 10, 20 and 40 seconds, undue work not claimable, exactly five claims then DeadLetter |
| G9-S17 | PASS | Permanent failure dead-letters with immutable evidence; unresolved references are parked with receipts intact |
| G9-S18 | PASS | Manager-only attributed redrive, three-generation ceiling returns 409, Processed redrive denied, immutable audit rows; a security-version change that commits while a redrive waits on the organization lock is denied after the lock |
| G9-S19 | PASS | Administrator, Analyst, Auditor and integration principal denied redrive or queue access as specified; Manager queue access and redrive stay denied after session revocation, account disabling, security-version change, membership removal and organization suspension; foreign organization and receipt identifiers never cross tenants; worker role cannot read identity or credential tables; logs contain no secrets |
| G9-S20 | PASS | Real 10,000 ceiling with Pending, Leased, NeedsReconciliation and RetryWaiting all counted, canonical replay bypasses capacity, competing intake and terminal transition stay bounded |
| G9-S21 | PASS | Suspended organization not claimed, heartbeat or finalize fenced, receipts preserved, resumed work completes once |
| G9-S22 | PASS (stated boundary) | Unit, contract and integration suites twice, M7 to M9 source contracts, frontend unit tests and production build, EF snapshot, stack end-to-end with database outage and recovery. Existing browser journeys were not rerun; see limitations. |
| G9-S23 | PASS | Clean control (16 tests) plus 14 mutation families, each killed by its intended test (table below) |
| G9-S24 | PASS | No vulnerable NuGet packages, no high npm advisories, no HIGH or CRITICAL Trivy findings on the four exact-source images, zero leftover resources, evidence safety scan and archive verification |
| G9-S25 | **NOT EXECUTED** | Requires independent evidence review and a separate explicit acceptance decision. |

## Stack end-to-end check (14 of 14)

Real isolated Compose stack (`postgres`, `migrator`, `api`, `web`, `worker`) with the restricted `harborops_worker` role applied from `scripts/m9-worker-role.sql`: services ready, ten migrations on a fresh volume, account and organization provisioning, memberships, maritime fixture, source provisioning, one resolved and one unresolved event processed by the restricted worker, operator role matrix and redrive ceiling, worker least privilege (reads of `users`, `user_credentials`, `auth_sessions`, `integration_credentials` and `tracking_receipt_conflicts` denied, forged attempts and deletes denied, redrive bypass rejected), readiness failing on a revoked grant and recovering, database outage with worker survival and recovery, stopped worker leaving accepted work Pending then processing once, image revision labels, and a log scan for secrets.

## Mutation effectiveness

| Family | Mutation | Killed by |
| --- | --- | --- |
| 1 | Effect uniqueness removed | `DirectSqlCannotForgeDuplicateOrTerminalWorkEvidence` |
| 2 | Lease token predicate removed | `HeartbeatExtendsOnlyLiveLeaseAndExpiredLeaseIsFencedThenRecovered`, `DistinctConcurrentDbContextsCannotShareOneLease` |
| 3 | Lease epoch predicate removed | `HeartbeatExtendsOnlyLiveLeaseAndExpiredLeaseIsFencedThenRecovered` |
| 4 | Lease expiry ignored | `HeartbeatExtendsOnlyLiveLeaseAndExpiredLeaseIsFencedThenRecovered` |
| 5 | Claim lock removed | `TwoIndependentWorkersContendOnOneReceiptAndOnlyOneClaimsIt`, `IntakeAndWorkerClaimSerializeOnTheOrganizationLockWithoutDeadlock` |
| 6 | Intake organization lock removed | `IntakeAndWorkerClaimSerializeOnTheOrganizationLockWithoutDeadlock` |
| 7 | Unprocessed states omitted from the count | `LeasedAndReconciliationStatesStillConsumeAdmissionQuota`, `UnresolvedCeilingCountsLeasedRetryAndReconciliationAtTheExactLimit` |
| 8 | Retry budget bypassed | `TransientFailuresFollowBoundedScheduleAndStopAtFiveAttempts` |
| 9 | Unauthorized redrive allowed (policy and locked store check) | `UnknownReferenceIsParkedAndCanBeRedrivenOnlyByManager` |
| 10 | Tenant resolver filter removed | `ReferenceResolutionIsTenantTimeSafeAndNeverGuessed` |
| 11 | Work transition SQL guard disabled | `DirectSqlCannotForgeDuplicateOrTerminalWorkEvidence` |
| 12 | Commit acknowledgment weakened | `ValidTimeScopedContainerAssignmentProducesExactlyOneImmutableEffect`, `AmbiguousFinalizeCommitIsReconstructedFromDurableState` |
| 13 | Concurrent reference change handling removed from the worker | `ReferenceChangedWhileWorkerFinalizesIsParkedForReconciliation` |
| 14 | Redrive security version recheck removed | `SecurityVersionChangeWhileRedriveWaitsOnTheOrganizationLockIsDenied` |

Every mutant compiled with warnings as errors and was killed by a named failing assertion, recorded with its message in `evidence\g9-mutations.json`. Relaxing only the redrive policy was proven an equivalent mutant (the locked store query independently requires the Manager role, `TrackingWorkAdminStore.cs`), so the mutation relaxes both layers. See [remediation register](M9_CORRECTNESS_REMEDIATION.md).

## Scans and cleanup

NuGet: no vulnerable packages. npm: no advisory at high or above for `web` and `tests/browser`. Trivy 0.76.1 on the exact source-labelled images, HIGH and CRITICAL, zero findings each:

| Service | Image id |
| --- | --- |
| postgres | `sha256:6ed4addf3060e922af56d3272cf4f2f8bf4039f6a796dac954d4f61ee7eb13f8` |
| api | `sha256:ae39eb024e1a11a102c19af30277d8ffb33c26de58d20346dedd6be7ed94d9d3` |
| web | `sha256:ebe18adce7c33c0d5c7b07277b3b6054a7e3fb584a1eaf490975e8f55ece8664` |
| worker | `sha256:0da1858fdfe51a65b5c0672c4fca2fb5e926de7e28357aeeebf1d10aff3371db` |

Teardown left zero containers, volumes, networks and database containers for the run. Free space on C: measured 3669 MiB before and 3690 MiB after the run, with no attributable task file and nothing deleted.

## Evidence

Archive `evidence\g9-attempt-08\m9\archives\20261105T015029Z-29816397e2c9920d\g9-0abeeb0e0eca3f639ef0036483f6cfc6e6e65440-20261105T015029Z-29816397e2c9920d-evidence.tar.gz`, SHA-256 `18a4adff0992132a8cd7db330416f7e662ef1a2c352fd7997f15a32f568d4ae6`, 84 files. The runner compared the manifest with the extracted archive and scanned it for secrets; the manifest check was repeated independently with `sha256sum -c` on an extracted copy (84 of 84 verified). `finalization.json` and the scenario register `g9-scenarios.json` are beside the archive and inside `evidence`. No secrets, bearer tokens or cookies are in the archive; disposable database passwords were used as scan canaries.

## Attempt history

Earlier attempts are preserved unchanged and are not used as gate evidence: development attempt 01 (compile error, fixed), development attempt 02 (767 of 767 tests passed on `2131f2a`), attempt 03 (refused before any work, shared-label collision), attempt 04 (stopped by me within seconds, clean teardown), attempt 05 (all scenarios passed except S23, where one mutant survived; test gap fixed), attempt 06 (technical PASS on `7665bc4`, 791 of 791 tests, archive `694fe31c0edd79ff6774e50a40a101465518ac94ee145388c6e268c30578f584`; superseded because later commits added the operator revocation, foreign tenant and archived container negatives) attempt 07 (technical PASS on `7797a4e`, 805 of 805 tests, archive `891f9bd50bb84930ec9af7d31b14bf1ed00761dd6bb45c47717bfdfafc1ebc81`; superseded because the concurrent reference change and redrive security-recheck race tests and mutation families 13 and 14 were added afterwards) and dev-only rehearsals 01 to 09. Attempt 08 is the final source-locked run. Details are in the [remediation register](M9_CORRECTNESS_REMEDIATION.md).

## Limitations and open items

- Existing M0 to M8 browser journeys were not rerun. The M9 change set touches no `web/` file and adds no UI; the frontend unit tests and production build were executed.
- Ambiguous overlapping assignments cannot be constructed because M6 constraints prevent them, so the resolver's ambiguity branch has no executing test; the database prevents the condition.
- Transient dependency retries are covered at the store level and by the outage stack check; per-error-class worker retry classification is covered by unit tests only.
- The six M9-01 local decisions (state and lease contract, retry defaults, backlog counting, identity reconciliation and effect-once outcome, operator permissions and redrive, failure handling and deployment) are acceptance pending; testing them is not approval.
- Independent evidence review (G9-S25) and G9 acceptance are separate decisions that remain pending. Nothing is merged into `main`.
