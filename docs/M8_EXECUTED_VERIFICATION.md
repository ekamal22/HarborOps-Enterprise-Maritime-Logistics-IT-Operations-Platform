# M8 Executed G8 Verification Record

**State:** Technical verification executed on one source revision. D-006 acceptance pending; independent evidence review pending; G8 acceptance pending. This record is not an acceptance and does not authorize a merge.
**Tested revision:** `537af9c2af99751aa2fbedf1a2115b7715e7b9e6` on `feat/m8-durable-event-inbox`, draft [PR #14](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/14), clean worktree, accepted baseline `main` at `47f030f9fa4b588d589f3ae082d03b3d5b94736a`. Commits after it change documentation and the expected status phrases in `scripts/check-planning-docs.mjs` only (`git log 537af9c..HEAD`); no product, test, runner or migration file differs from the tested revision.
**Runner:** `scripts/verify-m8-local.sh`, one complete run (attempt 04), wrapper exit status 2 by design: technical evidence passed while formal decisions are pending.
**Run ID:** `20261104T073952Z-0d093512c50ec17d`.
**Evidence root:** `E:\HarborOps-Verification\g8-attempt-04\m8`. Sealed archive `archives\20261104T073952Z-0d093512c50ec17d\g8-537af9c2af99751aa2fbedf1a2115b7715e7b9e6-20261104T073952Z-0d093512c50ec17d-evidence.tar.gz`, SHA-256 `b5144d0e39fe22f2ea71f996245df49b62ff43a3ac68c10d09bbd01133fa349c`. Manifest `...-manifest.sha256` (174 entries), SHA-256 `89a4adc610945152ddd821a6bfaaed49a0fc054ac41b92a61fb02dc7f91ef5c7`. The runner's own archive inspection passed (`archive-safety.log`: 174 files).
**Contracts:** [verification plan](M8_VERIFICATION_PLAN.md), [runbook](M8_G8_VERIFICATION_RUNBOOK.md), [remediation register](M8_CORRECTNESS_REMEDIATION.md), [implementation status](M8_IMPLEMENTATION_STATUS.md), [independent reconciliation](M8_G8_RECONCILIATION.md).

No GitHub Actions run was started; Actions remain disabled for the repository. All storage, caches, Docker data and evidence are on E:.

## Results by stage

All 24 recorded stages exited 0 on the tested revision. Durations in seconds: preflight 1, script-syntax 5, network-contract 0, documentation 1, tracking-contract 0, postgres-image 1, build 20, nuget-advisories 39, npm-advisories 4, web-build 10, dotnet-regressions 1207, compose 19, accounts 69, organizations 37, source-fixture 0, source-cli-negative 3, ingress-before 1, browser-fixtures 12, browsers 697, source-outage-recovery 6, image-scans 16, security-mutations 618, evidence-safety 0, evidence-summary 1.

| Suite | Result |
| --- | --- |
| Unit | 203 of 203 passed |
| Contract | 39 of 39 passed |
| Integration, fresh pass | 242 of 242 passed |
| Integration, repeated pass | 242 of 242 passed |
| Browser, Chromium | 58 passed, 0 failed, 0 skipped or flaky |
| Browser, Firefox | 58 passed, 0 failed, 0 skipped or flaky |
| Browser, WebKit | 58 passed, 0 failed, 0 skipped or flaky |
| Node contract tests | 14 of 14 passed |
| NuGet and npm advisories | No findings |

The real PostgreSQL COMMIT transport-loss variants (interruption before COMMIT is forwarded, and after COMMIT completes but before the client receives it) executed in both integration passes. The source outage and recovery stage stopped and restarted the disposable PostgreSQL container and compared the original accepted receipt, canonical digest and Pending work identity across the restart.

## Image identities and scans

Scanner Trivy 0.76.1, vulnerability database version 2 updated 2026-11-04 01:28 UTC, downloaded 2026-11-04 07:22 UTC. Severity HIGH and CRITICAL, exit code 1 on any finding.

| Service | Image ID | Revision label | HIGH/CRITICAL findings |
| --- | --- | --- | --- |
| postgres | `sha256:667ce183ea5fefdd9668b370028720ef1b0ba0657d9a1e564ba01f3ebaaebecc` | tested revision | 0 |
| api | `sha256:42bdd517607a31e3aad05ec4d805ac6c49eb3f5f8d8ec0c77524622e0b6795e8` | tested revision | 0 |
| web | `sha256:18c9ad509b20ca68260401ecaabb3546037543e1efd190c471f6a4be8f6eebd9` | tested revision | 0 |

## Security mutation results

The unmodified control passed 12 of 12 selected tests. Each of the 11 source-isolated mutants compiled cleanly and was killed by its intended regression.

| Mutant | Intended regression that failed |
| --- | --- |
| final-digest | `FinalTransactionAuthorizationRejectsWrongDigestAndForeignSource` |
| collision-as-replay | `ChangedPayloadConflictsWithoutMutatingOriginalOrLeakingBody` |
| no-work | `NewReceiptAndCanonicalReplayCommitOneImmutableTriple` |
| early-ack | `NewReceiptAndCanonicalReplayCommitOneImmutableTriple` |
| append-only | `DirectSqlCannotRewriteAcceptedEventReceiptOrWork` |
| quota | `PendingCapacityReturnsRetryable503ButReplaysOriginalReceipt` |
| no-event | `NewReceiptAndCanonicalReplayCommitOneImmutableTriple` |
| unique-key | `M8DatabaseIndexesMatchRelationalModel` |
| tenant-fk | `M8DatabaseIndexesMatchRelationalModel` |
| final-revocation | `RevocationBeforeFinalAuthorizationIsRejected` |
| source-status | `OverlappingRevocationDisableAndSuspensionRespectCommitOrder` |

## G8-S01 to G8-S25

| Scenario | Result |
| --- | --- |
| G8-S01 D-006 explicit approval | BLOCKED, D-006 acceptance pending |
| G8-S02 to G8-S24 | PASS (23 technical scenarios in total with no gaps) |
| G8-S25 Independent source-locked raw evidence review | BLOCKED, independent package review and gate acceptance pending |

The technical status file reports 23 of 23 required technical scenarios passed, `technicalStagesComplete: true` and `formalGateStatus: ACCEPTANCE_PENDING`. G8-S01 and G8-S25 are decisions and reviews that this run cannot make; they are not reclassified.

## Defects found by execution

Four complete runs were needed. Attempts 01 to 03 are preserved unchanged as failed runs and are not gate evidence; development rehearsals are likewise not evidence. Every defect is registered in the [remediation register](M8_CORRECTNESS_REMEDIATION.md) as M8-X01 to M8-X11:

- **Attempt 01** (source `2847220`): two historical upgrade tests still expected the M7 migration at head. Fixed in `ce0f7b1`.
- **Attempt 02** (source `e6eed51`): one Firefox browser fixture teardown fault in the `tracking-ingress` spec, after its assertions had completed. Not reproduced in 70 isolated contexts; spec hardened without retries (M8-X08).
- **Attempt 03** (source `ef0b0e7`): all stages exited 0, but only one of three image scan reports existed because the scan function masked failures when the vulnerability database download failed twice with a transient name-resolution error, and the archive check could not open the archive on Windows. Fixed in `537af9c` (M8-X09 to M8-X11).
- **Before the first complete run:** compile errors (missing import, unreferenced type in a unit test, nullable host), an EF snapshot that lacked a foreign-key index (51 of 54 tracking integration tests failed), a receipt timestamp that differed between the first response and replays, a planning-check mismatch, and a latent bearer authentication lock-order deadlock inherited from M7 (M8-X01 to M8-X06). Two mutants initially failed to compile, which is not a valid kill (M8-X07).

The authentication deadlock existed in code merged for M7. The accepted G7 evidence is unchanged and is not re-labelled; the correction is recorded against M8.

## Known limits

- D-006 acceptance, independent review of the sealed archive and G8 acceptance remain open decisions.
- The Firefox teardown fault in attempt 02 is infrequent, was not reproduced and has no proven root cause. The hardening is a synchronization change only.
- Browser coverage is the accepted G0 to G7 journeys plus the M8 ingress spec on three engines. Final accessibility, M9 worker behavior and M10 projections are excluded.
- The vulnerability database is a point-in-time snapshot recorded in `trivy/version.txt`.
- Storage: see the cleanup record below.

## Storage and cleanup

Docker Desktop's active WSL data location was verified by the runner's preflight (configured directory and registered distribution on E:, image `docker_data.vhdx`). Named resources `ho-verify-g8`, `ho-g8-pg` and `ho-g8-mutant-pg` were removed by the runner and the teardown record shows no containers, volumes or databases remaining. The retained `harborops_harborops-postgres` volume and unrelated containers were not touched.

C: free space was 6.054 GB before the work and 5.173 GB after. The inventoried project-related C: locations (Temp, wsl-crashes, CrashDumps, Docker settings and logs, Playwright, NuGet, `.docker`) did not grow beyond about 30 KB of Docker Desktop settings and log files, no new files appeared in Temp or CrashDumps, and `TEMP` and `TMP` were on E: throughout. The difference of 0.88 GB could not be attributed to this task. Task-session state under the workstation user profile (about 28 MB written) and the browser profile of the same account (about 186 MB written) account for part of it. Nothing was deleted because no task-created C: file could be identified. This is recorded as an unattributed difference rather than a cleanup result.

## Remaining decisions

1. D-006 acceptance or an explicit alternative.
2. Independent review of the sealed archive and the [reconciliation procedure](M8_G8_RECONCILIATION.md).
3. G8 acceptance and any merge of PR #14, none of which this record grants.
