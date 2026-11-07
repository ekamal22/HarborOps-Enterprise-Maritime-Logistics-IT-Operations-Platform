# M10/G10 Correction Authority Remediation: Executed Verification (2026-11-07)

**Subsequent gate disposition (2026-11-07):** The original results and pending decisions below are retained as historical source-locked evidence. G10 S01/S25 subsequently passed independent review, G10 was accepted, and the security remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) at `9b6c801fde4004a4c0021ec7a6296efe28cf1544`. See [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md) and [post-merge reconciliation](M10_G10_POST_MERGE_RECONCILIATION_20261107.md). No deployment or M9 transport-hold change was authorized.

**Status:** Technical verification executed locally on E:-backed disposable resources. **G10 acceptance pending.** This record is not a gate decision, merge approval, release or deployment authorization. Independent review (G10-S25) is NOT EXECUTED.

**Branch:** `fix/m10-g10-l07-l14-remediation`. Starting head `1dfd6e285a0d662f0841db4859d86f9549eb284f`; last runtime-code revision supplied with the task `3ef7a50859b5f023aa1457b373728888f6d1cba9`.

## Source locks

| Role | SHA | Notes |
| --- | --- | --- |
| Final runtime and campaign source | `a26d19acd57308033bdf6fe7e25c61287cd17b0b` | Source-locked runner attempt `g10r-a2` and mutation attempt `g10r-a3-mutations` |
| Supplement tests | `c5e14440154780b4ef8f964442beaacebc96917d` | Test-only commit; `src`, `scripts`, `docker` and the G10 Compose files are byte-identical to the final SHA |
| First candidate | `cb1ad8c8ca8b21840c06b1869e7fcfc95f98996a` | Failed runner attempt `g10r-a1`; failed mutation control `g10r-a1-mutations` |
| Intermediate | `397b50b5f02e8d3df377b35ec96f49c30935f1c4` | Mutation attempt `g10r-a2-mutations` stopped after the control and 20 kills when the runtime fix superseded it |

Results from different revisions are not combined. Only `a26d19a` results are counted as the source-locked campaign. Runtime source differs from `3ef7a50` only by the correction listed as D3 below.

## Attempt history (all preserved, none altered)

| Attempt | Source | Outcome |
| --- | --- | --- |
| `g10r-rehearsal-01` | `cb1ad8c` | Development rehearsal (ten stages DEV-SKIPPED, Chromium only). Exit 0, 28/28 stack checks. Not gate evidence |
| `g10r-a1` runner | `cb1ad8c` | **Failed** at integration: 303/312 passed, 9 failed, runner stopped. Unit 239/239, contract 39/39 passed |
| `g10r-a1-mutations` | `cb1ad8c` | **Control failed** (two stale migration-count assertions); no mutants ran |
| `g10r-a2-mutations` | `397b50b` | Stopped by the operator after the control and 20 kills because a runtime change superseded it. Partial only |
| `g10r-a2` runner | `a26d19a` | **Exit 0**, final source-locked run |
| `g10r-a3-mutations` | `a26d19a` | **Exit 0**, 41 variants, 40 mutants killed |

## Defects and corrections

| ID | Class | Finding | Correction |
| --- | --- | --- | --- |
| D1 | Harness (pre-existing in branch) | `scripts/check-m10-contract.mjs` declared `store` twice, so the guard could not run (`node --check` failed) | Removed duplicate (`accdcb6`) |
| D2 | Test (stale after the security change) | Stale lock probe inserted a correction directly; three HTTP tests posted corrections through the administrative database login, which the signed operation refuses by design; nine head-count assertions and one explicit migration list still expected twelve migrations; a downgrade expectation ignored that EF reverts one migration per transaction; session-expiry fixture violated `CK_auth_session_times`; a trigger-count query ignored schema scope | Tests updated (`b888ec4`, `397b50b`, `a26d19a`, `c5e1444`) without weakening assertions |
| D3 | **Implementation defect (runtime)** | The correction audit entry emitted `changedFields: ["correction"]`. Accepted M16 INSERT events carry an empty list, and `AuditMetadataKeysAndFieldNamesStayWithinTheFixedAllowlist` rejected the new field name | Migration now emits `{"operation":"insert","changedFields":[]}` (`a26d19a`); stack check updated |
| D4 | Unresolved, low confidence | One `40P01` deadlock in `AuditRoleProvisioner.ProvisionAsync` during test setup in attempt `g10r-a1`, while a mutation control ran concurrently on a separate database. Not reproduced in six isolated loops of the new authority tests or in either final 312-test pass; the final PostgreSQL logs contain no deadlock lines | Runner now preserves PostgreSQL server logs. Cause unproven |

Observations, not changed:

- The signed operation checks `session_user = 'harborops_api'`. An API process configured with another login (for example the migration login) receives a sanitized denial for every correction. All G10 Compose files use `harborops_api`.
- The proof does not bind the correlation identifier; it affects only the stored correlation label.
- The base `compose.yaml` does not pass `M10CorrectionProof__Key`; only the G10 and combined override files do. Without it, corrections return a sanitized HTTP 503. This held at `a26d19a`; it was corrected afterwards (see the [final merge-readiness record](M10_G10_FINAL_MERGE_READINESS_20261107.md)).
- `20261105180000_ShipmentTrackingProjections` was edited in place (COALESCE in the projector lock function). A database migrated from the PR #23 text before this branch will not receive that edit. This held at `a26d19a`; migration 12 was restored and the function is now replaced by migration 13 (see the [final merge-readiness record](M10_G10_FINAL_MERGE_READINESS_20261107.md)).

## Measured results on `a26d19a`

Runner `g10r-a2`: 02:50:53Z to 03:54:00Z (63 min), technical exit 0, clean teardown, 64/64 manifest entries verified, redaction probe passed. 31 stages executed and 1 DEV-SKIPPED (`security-mutations`, executed as a separate campaign on the same SHA).

| Stage | Result | Duration |
| --- | --- | --- |
| .NET build, `-warnaserror` | 0 warnings, 0 errors | 155 s |
| Unit | 239/239 | 5 s |
| Contract | 39/39 | 13 s |
| Integration, pass 1 | 312/312 | 24 m 40 s |
| Integration, pass 2 (fresh database) | 312/312 | 23 m 41 s |
| Real-stack check | 28/28 | 136 s |
| M16 audit HTTP matrix | 74/74 | 77 s |
| M16 audit outage | 14/14 | 20 s |
| Browser `tracking-timeline` | Chromium 5/5, Firefox 5/5, WebKit 5/5; 0 unexpected, flaky or skipped | 57 s |
| Image scans (postgres, api, web, worker) | 0 HIGH or CRITICAL findings | 24 s |

Counts were reparsed independently from the raw TRX, Playwright and JSON artifacts, not taken from runner summaries.

## R01 to R15

| ID | Result | Primary evidence |
| --- | --- | --- |
| R01 | PASS | Restricted-API HTTP correction tests; stack correction check; three-engine browser spec |
| R02 | PASS | Replay and key-collision cases in integration and the stack check |
| R03 | PASS | `AuthorityIsDeniedForAnalystDowngradedRevokedAndExpiredSessions`, live downgrade, suspended-organization and archived-shipment tests |
| R04 | PASS | Real `harborops_api` direct INSERT denial (integration and stack) |
| R05 | PASS | `EveryProofBindingAndInvalidProofIsDeniedWithoutEffectsOnTheRestrictedLogin`: missing, malformed, truncated, uppercase, expired, far-future, foreign-key and replayed proofs |
| R06 | PASS | Stolen readable session and actor identifiers cannot produce a valid proof (integration and stack) |
| R07 | PASS | Tenant, shipment, actor, session, target, replacement, reason, generation, idempotency key, nonce, expiry and digest each denied |
| R08 | PASS | Real lock waits behind in-flight revocation and downgrade; a revocation queued behind an in-flight correction commits after it; no commit after revocation |
| R09 | PASS | Store-level rollback test; HTTP outage test returns sanitized 503, leaves no correction, revision, replay, nonce or audit row, and the same key then applies once |
| R10 | PASS | Auditor reads exactly one scoped, sanitized entry; Administrator, Manager and Analyst denied; foreign tenant invisible; metadata allowlist test |
| R11 | PASS | API, worker and projector grant matrices (integration, stack, `restricted-db-roles`) |
| R12 | PASS | Chain, generation, concurrency, projector kill and rebuild, large-history tests |
| R13 | PASS | Fresh 13-migration application; populated history backfill with downgrade guards; supplement test below |
| R14 | PASS | Missing key is a dependency failure, mismatch is denied, rotation and restart recover, invalid provisioning input is rejected, no secret in audit or correction rows, runtime identities cannot read or write the verifier; log scan found no secrets |
| R15 | PASS | Audit HTTP 74/74, outage 14/14, M9 worker, effect and commit-reconstruction tests, M10 regressions |

## Supplement (same runtime, test-only commit `c5e1444`)

Run on a disposable PostgreSQL 16.15 container outside the source-locked runner, so they are not counted in the 312/312 totals:

- `PopulatedTwelveMigrationDatabaseWithAPreAuthorityCorrectionUpgradesToThirteenWithoutLoss` passed: a populated database stopped at twelve migrations, with a correction and replay written the old way, upgrades to thirteen with identical M10 and M8/M9/M16 fingerprints and an unchanged audit row count; verifier and nonce tables are empty; downgrade without nonce evidence is reversible and re-applies cleanly.
- `AuditOutageReturnsASanitized503WithNoEffectsAndTheSameKeyRecoversAfterwards` passed.
- The four authority tests passed six consecutive isolated loops with no deadlock.

## Security mutations

41 variants (clean control plus 40 mutants), 02:56:38Z to 03:43:01Z (46 m 23 s), run concurrently with the runner on a separate database container and root. The control ran 14 unit and 30 integration tests with 0 failures. All 40 mutants compiled and all 40 were killed by a named test; 0 survived, 0 were invalid. Nine variants were added for the authority boundary (expiry window both directions, session revocation, session expiry, security version, row locks removed, replay guard removed, verifier readable by the API role, swallowed audit failure) in addition to the signature, Manager-only, SECURITY DEFINER and grant variants.

## Not executed or limited

- The other 17 browser specifications were not rerun (no `web` source changed). The earlier `3e81b88` browser evidence is a reference only.
- The historical A1 to A7 campaign was not rerun; unchanged scenarios are referenced, not repeated.
- The two supplement tests above ran outside the source-locked runner.
- The cause of the single deadlock in D4 is unproven.
- G10-S25 independent review and the gate decision: NOT EXECUTED. Acceptance pending.

## Evidence

Raw evidence stays on E: under `E:\HarborOps-Verification\evidence\`: `g10r-a1`, `g10r-a1-mutations`, `g10r-a2-mutations`, `g10r-a2`, `g10r-a3-mutations`, `g10r-rehearsal-01` and `g10r-assessment` (coverage assessment, reconciliation script and output, supplement logs and TRX). The sanitized review package is `g10r-remediation-evidence-20261107.tar.gz` (251 files, no caches, traces or credentials; redaction probe passed), SHA-256 `d4c7ba5d1ce4c63c415693fd61814842a2a4d2a6b1551054c8a9ebe94fcff3d2`. Disposable keys and passwords were created per run, kept in private directories and never committed.

## Reproduction

From a clean checkout of the final SHA on an E: path, with `HARBOROPS_DOCKER_VHDX` set and a unique single-use `HARBOROPS_VERIFICATION_ROOT` ending in `/g10`:

```bash
HARBOROPS_COMBINED_DEV_SKIP=security-mutations HARBOROPS_BROWSER_SPECS=tracking-timeline \
HARBOROPS_G10_PROJECT=ho-verify-cg16-<suffix> bash scripts/verify-g10-local.sh
node scripts/run-g10-mutations.mjs <E: root>   # separate root, same SHA
```

The reduced browser scope and the skipped in-runner mutation stage are development settings: a full gate run leaves both unset.
