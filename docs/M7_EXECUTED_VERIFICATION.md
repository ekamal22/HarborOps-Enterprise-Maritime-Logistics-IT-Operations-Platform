# M7 Executed G7 Verification Record

**State:** Technical verification executed on one source revision, with two supplements. G7 is **acceptance pending**. D-002 is **acceptance pending**. M7 is not merged into `main`.
**Tested revision:** `7b87f3250aba677b1f4aeef4350803d9ca7b6880` on the M7 feature branch. Commits after it (`git log 7b87f32..HEAD`) change documentation and one documentation-guard assertion in `scripts/check-m7-contract.test.mjs`; no product, migration, runner or integration test code differs.
**Runner:** `scripts/verify-m7-local.sh`, one complete run, exit status 2 by design because the open-gate register is not empty.
**Evidence root:** `E:\HarborOps-Verification\m7`. Final package: `E:\HarborOps-Verification\m7\archives\g7-7b87f3250aba677b1f4aeef4350803d9ca7b6880-final-package.tar.gz`, SHA-256 `257f4d66608b17396187a324a76a5fc35c6fbc7fa802762ee12d90b3b56c0dbc` in the adjacent `.sha256` file. The runner's own archive and manifest sit beside it.
**Contracts:** [Verification plan](M7_VERIFICATION_PLAN.md), [remediation register](M7_CORRECTNESS_REMEDIATION.md), [D-002 proposal](M7_D002_TRACKING_REFERENCE_PROPOSAL.md), [implementation register](M7_IMPLEMENTATION_STATUS.md).

This record does not accept G7 or D-002. No GitHub Actions run was started. All storage, caches, Docker data and evidence are on E:.

## Results by stage

All 22 runner stages exited 0 on the tested revision: preflight, network-contract, documentation, tracking-contract, postgres-image, build, dotnet-regressions, security-mutations, nuget-advisories, web-build, npm-advisories, compose, accounts, organizations, source-fixture, source-cli-negative, ingress-before, browser-fixtures, browsers, source-outage-recovery, image-scans, evidence-summary.

| Area | Result |
| --- | --- |
| Release build, warnings as errors | Passed |
| Unit tests | 189 of 189 passed |
| Contract tests | 39 of 39 passed |
| PostgreSQL integration, fresh run | 210 of 210 passed, none skipped |
| PostgreSQL integration, repeat run | 210 of 210 passed, none skipped |
| Browsers (accepted M0 to M6 specs plus the M7 ingress spec) | 58 of 58 in each of Chromium, Firefox and WebKit, no failures, skips or flaky results |
| Controlled security mutations | Control passed; 5 of 5 mutants killed by their intended regression |
| NuGet advisories | No vulnerable packages in any project, advisory source reachable |
| npm advisories | 0 vulnerabilities in `web` and `tests/browser` |
| Image scans (Trivy 0.76.1, database updated 2026-11-03) | 0 HIGH or CRITICAL findings in the `postgres`, `api` and `web` images |
| Teardown | No containers, volumes or networks left; the retained historical volume and unrelated containers were not touched |

Image identifiers, labelled with the tested revision: postgres `sha256:5075a5f34655db979be8a4895f459483c003bd685aaa24e20ba419ce64b762c7`, api `sha256:e952c5d1451c470c251f81fc2ac30f070d7fe1c0ad8ea1b4e694d4d44a09f95b`, web `sha256:c29a2096c1c636dc950253be4474fefd26941603c36d6efc148a32ec952edaa9`.

## Security mutation results

Each mutant ran in its own disposable source export; the working tree was never modified.

| Mutant | Failing regression | Failure observed |
| --- | --- | --- |
| Credential digest check forced true | `AuthenticationFailuresAreThrottledBeforeExcessiveDatabaseWork` | Wrong secret expected 401, observed 503 `intake_unavailable` (the credential authenticated) |
| Source and organization status check forced true | `ValidCredentialDeniedAfterSourceDisableOrOrganizationSuspension` | Suspended organization expected 403, observed 503 |
| Revocation predicate removed | `AuthenticatedSourceValidatesButNeverAcknowledgesEvent` | Revoked credential expected 401, observed 503 |
| Body bound raised from 16384 to 65536 bytes | `MediaProfilesSizeAndMethodRejectSafely` | Oversized body expected 413, observed 400 |
| Credential lifecycle trigger removed | `DatabasePreventsCredentialIdentityMutationAndHistoryDeletion` | Expected a PostgreSQL exception, none thrown |

## G7-S01 to G7-S25 (original run)

Outcomes come from `g7-scenarios.json` in the evidence root, derived from the raw TRX files, stage records, browser reports, mutation report and scan reports of the tested revision.

| Scenario | Outcome | Basis |
| --- | --- | --- |
| G7-S01 | **BLOCKED** | D-002 acceptance pending; see below |
| G7-S02 to G7-S05 | PASS | Policy, credential storage, fresh schema and EF snapshot, populated M6 to M7 upgrade with rollback and reapply |
| G7-S06 | **NOT EXECUTED** | The protected-terminal create, rotate, revoke and disable lifecycle needs an operator terminal; the redirected-output refusal and opt-in denial passed in `source-cli-negative` |
| G7-S07 to G7-S16 | PASS | Authentication matrix, concurrency, tenant isolation, browser identity separation, transport trust, limits, media contract, parser, identity and UTC rules |
| G7-S17 | **BLOCKED** | Fixture tests passed; the recorded upstream mapping and fixture pins depend on D-002 |
| G7-S18 to G7-S24 | PASS | Unsupported variants, envelope, no durable acknowledgment, outage and cancellation, accepted regressions, Compose and browser integration, mutations and scans |
| G7-S25 | **BLOCKED** | Independent raw-evidence review pending |

Totals of the original run: 21 PASS, 3 BLOCKED, 1 NOT EXECUTED. Nothing failed. This result is preserved unchanged; the supplements below are recorded separately.

## Supplement A: G7-S06 protected-terminal lifecycle (PASS)

Performed on 2026-11-03 against a new isolated stack `ho-verify-g7s06` built from the same tested revision in a separate detached worktree on E:. The operator ran the lifecycle in a private terminal; the wrapper recorded exit code 0 from 13:27:48Z to 13:28:08Z and captured no output. Direct PostgreSQL checks confirmed one source `g7_interactive_cli` in status `Disabled` at version 4 and exactly two credentials, both revoked, none active, with a 32-byte digest each. The authentication predicate selects 0 rows for them, seven mutating probes (digest change, revocation reversal, expiry extension, reassignment, deletion of credentials and sources, source reactivation) were rejected with SQLSTATE 23514 inside rolled-back transactions, and six non-interactive negative commands exited 1 without creating anything. Both displayed bearer values were exposed outside the terminal, were treated as exposed disposable test credentials, are revoked, and the database was destroyed at teardown. The stack, volumes, network and images of the project were removed and unrelated Docker resources were unchanged.

The operator script differs from the tested script only by a parameterized Compose project name and a `winpty` helper (diff and hashes in the supplement). Evidence: `E:\HarborOps-Verification\m7\s06\evidence`, archive `E:\HarborOps-Verification\m7\s06\archives\g7-s06-7b87f3250aba677b1f4aeef4350803d9ca7b6880-supplement.tar.gz`, SHA-256 `0fd0562840b170a5185c586907b07b8b45b6f30f6f4dac5eed3e671f9358fd13`.

The lifecycle printed `Cannot load library libgssapi_krb5.so.2`. The message comes from the .NET runtime's native security library when the PostgreSQL client probes GSS encryption; it also appears in the API process during the original passing run. Password authentication (scram-sha-256) was in use, no session used GSS, and a run with `GSS Encryption Mode=Disable` printed no warning and returned identical results. It is not a defect and nothing was changed.

## Supplement B: G7-S25 evidence reconciliation (PASS)

- The original runner manifest `g7-7b87f3250aba677b1f4aeef4350803d9ca7b6880-manifest.sha256` (SHA-256 `2ff3254d52fe39cf6780e5e25d727da307182a91c4772e5cbf44807026194440`) verifies 156 of 156 files against the retained evidence and covers exactly that file set. The supplement manifest verifies 8 of 8 files and the final package digest `257f4d66608b17396187a324a76a5fc35c6fbc7fa802762ee12d90b3b56c0dbc` matches.
- Cleanup is recorded for both runs: the original `teardown.txt` and integrity report, and the S06 `teardown.txt`.
- An independent review of the archive reported the same outcomes: package digest, 156 of 156 evidence files across the tar and zip forms, 8 of 8 supplement hashes, the four TRX counts, the 48 browser reports, five mutation detections, 22 zero stage exits, no HIGH or CRITICAL findings, the teardown log and no bearer patterns.
- Scope of the PASS: archive integrity and the recorded outcomes. The live Windows and Docker environment claims (for example the E: storage placement and the state of the machine during the run) are supported by the archived logs and checks but were not independently reproduced.
- The original `evidence-summary` stage could not mark G7-S25 because the archive is written after it; the reconciliation is recorded in the S06 supplement as `g7-scenarios-reconciled.md`.

## Reconciled matrix

| Result | Count | Scenarios |
| --- | --- | --- |
| PASS | 23 | G7-S02 to G7-S16 (including G7-S06 by Supplement A), G7-S18 to G7-S25 (including G7-S25 by Supplement B) |
| BLOCKED | 2 | G7-S01 and G7-S17, both on D-002 |
| NOT EXECUTED | 0 | none |

The original 21 PASS, 3 BLOCKED, 1 NOT EXECUTED result is unchanged in the original evidence. The reconciled totals depend on the two supplements and are reported as such.

## Defects found by execution

Detailed in the [remediation register](M7_CORRECTNESS_REMEDIATION.md): an integration test project that did not compile, two analyzer errors, a stale migration list, a populated-upgrade test built on a wrong assumption about the M5 status-history trigger, a mutation runner cache mismatch, a root check that failed under Git Bash's E: mount, runner fixture names that differed from the accepted browser fixtures, and a summarizer that read teardown output before it existed. Two coverage gaps were closed with new tests. None of the defects was in the shipped intake, authentication or migration behavior; the migration and credential code passed the unmodified integration suite once the tests compiled.

## D-002

The upstream SwaggerHub Track & Trace 2.2.0 definition and its event domain were fetched, hashed and retained in the evidence supplement. The five provisional codes exist upstream, but no tagged or content-addressed release was found and the repository named in the first draft does not exist. No change was made to the standard version or the supported subset, and no DCSA conformance is claimed. See section 6 of the [D-002 proposal](M7_D002_TRACKING_REFERENCE_PROPOSAL.md).

## Known limits

- G7-S06 was one operator execution and the operator transcript is not retained by design.
- The `499` response for a client disconnecting mid-request is covered by a store-level cancellation test and source review, not by an end-to-end HTTP test.
- NuGet and Trivy advisory data were available; a missing advisory source would have failed the stage, not reported zero findings.
- The evidence-summary stage cannot see the teardown file, so teardown is verified in the integrity report in the evidence supplement.
- Accessibility remains M24-04.

## Items needing a decision or an operator

1. D-002: accept, amend or reject the proposal; the recommendation is in section 7 of the [D-002 proposal](M7_D002_TRACKING_REFERENCE_PROPOSAL.md).
2. Explicit G7 acceptance, and then a separate decision to merge into `main`.

The guard assertion change was rerun on its own (8 of 8 script tests pass) and the planning-document checks pass on the final head.
