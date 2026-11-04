# M8 Executed G8 Verification Record

**State at execution:** Technical verification completed, with D-006 and G8 acceptance still pending at the moment of the source-locked run. Original runner results remain unchanged. **Subsequent supplement:** D-006 Option A and G8 were explicitly accepted on 2026-11-04 after [independent evidence review](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md) and separately documented [gate closure](M8_G8_GATE_CLOSURE.md).
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

## Post-test delta

Commits after the tested revision are `57b4a2a` (this record and status wording) and a later wording clarification of the storage paragraph above. `git diff --name-only 537af9c..HEAD` lists Markdown files and `scripts/check-planning-docs.mjs` only; the checker change replaces the pre-execution status phrases it expects. No file under `src`, `tests`, `docker`, Compose, the migrations, the browser specs, the mutation scripts or the verification runner differs from the tested revision, so the sealed run remains the evidence for the executable behavior. The documentation check was run again on the current head instead of repeating the campaign.

## Independent review inputs

The sealed run is unchanged and the review inputs are exactly these files under `E:\HarborOps-Verification\g8-attempt-04\m8`. Hash each one and compare it with the value here before examining content.

| File (relative to the root) | SHA-256 |
| --- | --- |
| `archives\20261104T073952Z-0d093512c50ec17d\g8-537af9c2af99751aa2fbedf1a2115b7715e7b9e6-20261104T073952Z-0d093512c50ec17d-evidence.tar.gz` | `b5144d0e39fe22f2ea71f996245df49b62ff43a3ac68c10d09bbd01133fa349c` |
| `archives\20261104T073952Z-0d093512c50ec17d\g8-537af9c2af99751aa2fbedf1a2115b7715e7b9e6-20261104T073952Z-0d093512c50ec17d-manifest.sha256` (174 entries) | `89a4adc610945152ddd821a6bfaaed49a0fc054ac41b92a61fb02dc7f91ef5c7` |
| `archives\20261104T073952Z-0d093512c50ec17d\g8-finalization-status.json` | `9a58db4484aa3c4ef222fa055452ee945205a6cdd81e8e68dd5547bbc86dea1f` |
| `archives\20261104T073952Z-0d093512c50ec17d\archive-safety.log` | `44247b7f34564e55656724a463945b5d3e3ac53a10233472d33c9abbad31b6df` |
| `evidence\g8-scenarios.json` (also inside the archive) | `b6d92b3f53aa9398e53789fa88195021b519e4fbb8421dc6f887a9f9b1dc4cea` |
| `evidence\g8-technical-status.json` | `14d5f432703fcd3e0d37b5ceaf51fcf366be7de832803f7f105f2096ff7c6327` |
| `evidence\g8-formal-gate-status.json` | `325455304cd31ae95618db22a6a7570715c47d98b9e262f1334d876a255882db` |

Reviewers should extract the archive into a new empty directory and run `sha256sum -c` against the manifest from inside the extracted `evidence` folder. The archive contains no bearer credentials, passwords or raw private publisher payloads; the archive safety check and a separate pattern scan of an extracted copy found none. The `private` and `cache` directories of the run root are not part of the review package (`private` is empty).

## Conformance with proposed D-006 Option A

This is an assessment of the executed behavior against the [proposal](M8_D006_DURABILITY_PROPOSAL.md). It is not a decision, and it does not change the acceptance pending state.

| Proposed term | Verified M8 behavior | Assessment |
| --- | --- | --- |
| Unique key `(organization, source, profile, source event id)` enforced by PostgreSQL | Named unique index `UQ_m8_receipt_key` on those columns; append-only triggers prevent deletion, so the key is permanent | Conforms in effect. It is a unique index, not a named constraint (register item M8-A05) |
| Versioned SHA-256 canonical digest, raw bytes preserved | 32-byte digest check, bounded raw bytes up to 16 KiB, byte-for-byte equality across API restart | Conforms |
| Same canonical event replays the original receipt with `duplicate: true` | HTTP 202 with the original receipt identity and microsecond timestamp, ten sequential and twenty concurrent replays | Conforms |
| Different event with the same key returns 409 with first-seen metadata and no unbounded rows | 409 `source_event_collision`; one conflict row per receipt (unique index, `ON CONFLICT DO NOTHING`); attempted digest only, no conflicting body | Conforms. A second, different conflicting payload is not separately recorded, which matches the bounded intent |
| One receipt, one immutable event and one Pending work row in one transaction, enforced one to one | Deferred constraint trigger rejects a receipt without an event and work row; event and work keyed to the receipt; rollback and cancellation tests | Conforms |
| 202 only after commit; ambiguous commit never acknowledges | Real PostgreSQL wire cut before and after COMMIT, deterministic hook variants and cancellation tests; replay resolves the original outcome | Conforms |
| Original bearer revalidated under organization, source, credential lock order | Locks in that order; three lock-wait races; the M7 authentication path was corrected to the same order (M8-X05) | Conforms, with the M7 correction noted |
| Pending ceiling 10,000 per organization, 503 `intake_capacity_exceeded` with `Retry-After`, duplicates resolved before the cap | Checked under the organization lock after replay and collision resolution; 9,999, 10,000 and 10,001 boundary and cross-source race executed | Conforms. The ceiling is a demonstrator default, not measured production capacity |
| No retention deletion; downgrade protection | No delete path; immutable triggers; downgrade refused once any receipt exists | Conforms |
| Future M9 worker states in separately mutable or additively extended state | The M8 work-state check constraint admits only `Pending` with zero attempts, and the guard trigger rejects every update and delete | Stricter than the proposal. M9 must replace that constraint and trigger in a new migration |

Consequences for the decision: because the M8 work rows cannot change state, an organization that reaches 10,000 Pending items stays at capacity until M9 introduces transitions. The organization lock serializes accepted intake per organization, which the proposal states as a cost. Throughput, storage growth and backup policy were not measured and are outside G8. The 499 response for a client-cancelled request and the 403 `source_access_denied` outcome come from the accepted M7 contract and the final authorization step and are not terms of the proposal. No material deviation from Option A was found that blocks acceptance, but acceptance pending stands until the maintainer records a decision.

## Remaining decisions

1. D-006 acceptance or an explicit alternative.
2. Independent review of the sealed archive and the [reconciliation procedure](M8_G8_RECONCILIATION.md).
3. G8 acceptance and any merge of PR #14, none of which this record grants.
