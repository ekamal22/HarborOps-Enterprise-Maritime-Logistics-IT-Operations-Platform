# M6 Executed Verification (G6 technical evidence)

**Status:** Executed technical evidence for G6-S02 through G6-S25; complete twelve-rule M6 contract, G6-S01 and G6 explicitly accepted on 2026-11-02. See [gate closure](M6_G6_GATE_CLOSURE.md).
**Tested source:** `a94d7cd406a93806527d28ba7d2ce2d6ba3307b6` on `feat/m6-container-voyage-assignments`
**Initial source:** `b72ba558bd1bced4fbb7c00b8008b61ca278799b`; accepted baseline `main` `344c6eb96a9af600b5843cc97c56a8449174bad8`
**Executed:** 2026-11-02, UTC 17:03:19 to 17:49:22 (46 minutes), Windows 11 with Docker Desktop on E:
**Verification plan:** [M6 verification plan](M6_VERIFICATION_PLAN.md)

After the tested SHA only this record, the status documents and the read-only evidence summary script `scripts/summarize-g6-evidence.mjs` were added. No application source, migration, test, runner or harness script changed after `a94d7cd`.

## Result

All 33 executable pipeline stages exited 0 on the tested source. The evidence assertion exits 1 for one reason only: G6-S01 requires an explicit acceptance of the twelve-rule contract in [M6 local contract decision](M6_LOCAL_CONTRACT_DECISION.md), which was Proposed, acceptance pending during execution. That was an approval-only blocker and was closed by the explicit decision on 2026-11-02. The original evidence-stage exit 1 is preserved; no post-approval rerun is claimed.

| Suite | Total | Passed | Failed | Skipped |
| --- | --- | --- | --- | --- |
| Unit | 149 | 149 | 0 | 0 |
| Contract | 12 | 12 | 0 | 0 |
| PostgreSQL integration, fresh database | 187 | 187 | 0 | 0 |
| PostgreSQL integration, repeated on the same database | 187 | 187 | 0 | 0 |
| Chromium (15 reports) | 57 | 57 | 0 | 0 |
| Firefox (15 reports) | 57 | 57 | 0 | 0 |
| WebKit (15 reports) | 57 | 57 | 0 | 0 |

The accepted M0-M5 inventory is preserved (the browser baseline of 47 specs per engine plus ten M6 specs; the integration inventory of 187 includes the accepted suites). No assertion was weakened, no test removed and no threshold lowered; Playwright retries remain 0.

Other stages: Release build with warnings as errors (0 warnings), NuGet and npm audits with no vulnerable packages, planning-document consistency, PostgreSQL outage and recovery for assignments, shipments, references and maritime data, scoped session recovery, Trivy scans of the PostgreSQL, API and web images (zero HIGH or CRITICAL findings), four security mutations, the Q1 historical checkpoint, teardown (0 remaining containers, volumes or integration databases) and C:/E: storage accounting.

## Security mutations

The clean control passed (11 targeted tests). Each mutation was applied to a disposable source export and failed its intended test for the intended reason.

| Mutation | Intended test | Failure |
| --- | --- | --- |
| Tenant read scoping removed | `ForeignAssignmentIdsReturnNotFoundInsideSelectedTenant` | Expected NotFound, actual OK |
| Post-lock revalidation removed | `QueuedAssignmentWriteRechecksEveryAuthorizationDependency` | Queued write was not rejected after a dependency change |
| Container exclusion removed | `OverlapMutationRequiresBehavioralExclusionRejection` | No PostgreSQL exception for an overlapping insert |
| Parent archival protection removed | `ParentGuardMutationRequiresBehavioralArchiveRejection` | No PostgreSQL exception for archiving a parent with an open link |

## G6-S10 historical checkpoint

A retained historical volume exists: `harborops_harborops-postgres` (created 2026-10-30, PostgreSQL 16, 47 MB, cleanly stopped). It was mounted read-only only. A fingerprint of every file was identical before and after. A disposable copy was restored into an isolated PostgreSQL 16.15 container, migrated from `20261030170000_InitialBootstrap` to `20261102080000_ShipmentAssignments`, and the pre-existing `bootstrap_records` table re-read with an identical content hash; the two M6 exclusion constraints and both assignment tables were present. The copy, container and volume were removed.

The snapshot holds only the M0 bootstrap record. Its upgrade path is a real historical upgrade, but it contains no users, organizations, shipments or voyages, so preservation of those entities is proved by `ExistingM5RowsSurviveM6UpgradeRollbackAndReapply` against a seeded M5 schema, not by this snapshot. Its M0-only representativeness limitation was acknowledged at G6 acceptance. A second volume, `ho-verify-oldcopy`, was discovered and not used.

## Defects found by execution

Execution found real defects that source review had not; they are listed with root causes in [M6 implementation status](M6_IMPLEMENTATION_STATUS.md). The production defects were the EF1003 raw SQL concatenation in the list cursor and the M6 parent guard trigger ordering that changed an accepted M5 rejection message. The remainder were test and tooling defects (browser spec races, fixture identifier length, log buffer size, login rate limiting between outage checks and harness path handling).

## Evidence

The raw evidence bundle is outside the repository under `E:\HarborOps-Verification\m6`:

| Item | Location |
| --- | --- |
| Evidence directory | `evidence` (167 files, SHA-256 manifest `g6-file-hashes.csv`) |
| Final archive | `archives\G6-incomplete-20261102T175309Z-a94d7cd406a9.zip` with `.sha256` and `.json` |
| First archive of the same evidence (without the supplementary reports) | `archives\G6-incomplete-20261102T174923Z-a94d7cd406a9.zip` |
| Failed and superseded runs | `archives\G6-superseded-and-development-runs-20261102T175353Z-a94d7cd406a9.zip` (499 files, manifest inside) |
| Reports | `evidence\reports\G6_RUN_SUMMARY.md`, `G6_EVIDENCE_INDEX.md`, `G6_ENVIRONMENT.md`, `G6_RUN_HISTORY.md` |

The archive name carries the prefix `incomplete` because the evidence gate is incomplete solely for G6-S01. The final archive was re-extracted into a separate directory and every file was checked against the manifest by size and SHA-256 with no mismatch.

## Acceptance resolution

The complete twelve-rule M6-local contract and G6 were explicitly accepted on 2026-11-02; integration into `main` was separately authorized. G6-S01 is closed by that decision. The original technical evidence and its pending-decision exit status remain unchanged. See [gate closure](M6_G6_GATE_CLOSURE.md).
