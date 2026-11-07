# M10/G10 Final Merge-Readiness Remediation (2026-11-07)

**Status:** Technical verification executed locally on E:-backed disposable resources. **G10 acceptance pending.** This record is not a gate decision, merge approval, release or deployment authorization. G10-S01 implementation conformance and G10-S25 independent evidence review are NOT EXECUTED for these changes.

**Branch:** `fix/m10-g10-l07-l14-remediation`. Starting head `a2d021dada35626788e56e6d9932e46aba006669`. Protected `main` baseline `5edf0035a1fb9d854cba00673eb8fdf1f3033b0c` is untouched.

An independent source review of the earlier [remediation verification](M10_G10_REMEDIATION_VERIFICATION_20261107.md) found two merge-readiness gaps. Both are closed in source and verified below. The earlier results stay associated with their own revision (`a26d19a`) and are not restated as having run on this source.

## Source locks

| Role | SHA | Notes |
| --- | --- | --- |
| Frozen verification source | `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343` | Detached worktree on E:. Every source-locked result below was produced from it |
| Migration fix and tests | `891247df4ad28859758dde0ac3733d92cd1805ff` | Restores migration 12, adds the migration 13 replacement and the projector scope tests |
| Compose key, guards, smoke script | `1bfa9d52713d0acf3bb8e1b1b71c183968864e60` | `compose.yaml`, `.env.example`, `scripts/check-default-compose-correction.mjs`, `scripts/check-m10-contract.mjs` |
| Mutation retarget | `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343` | `scripts/run-g10-mutations.mjs` only |
| Previously verified runtime | `a26d19acd57308033bdf6fe7e25c61287cd17b0b` | The only runtime-code difference to the frozen source is the 45-line migration 13 addition below |

Commits after `e3002bd` change documentation only.

## Issue A: migration 12 was edited in place

**Finding.** `20261105180000_ShipmentTrackingProjections` had been changed to return `COALESCE(organization_state='Active' AND shipment_state='Active',false)`. A database that already executed migration 12 never re-runs it, so it would keep the original three-valued function that returns NULL for an absent scope.

**Correction.**

- Migration 12 is restored byte-for-byte to the accepted `main` version (`git diff 5edf003 HEAD` over that file is empty; blob `c19f6dd8043062d42b7c94f80f1dca0830409a77`).
- Migration 13 (`20261107120000_M10CorrectionAuthority`) now replaces the function with `CREATE OR REPLACE FUNCTION` using the same signature, `SECURITY DEFINER`, `SET search_path=pg_catalog` and schema binding, followed by `REVOKE ALL ... FROM PUBLIC`. Replacement keeps the function object (same oid), owner and existing grants; the tests verify this rather than assume it.
- `Down` of migration 13 restores the original body, so a downgrade returns to the accepted migration 12 state. The nonce-retention downgrade guard is unchanged and still runs first.
- The effective security behavior is unchanged: the store already treats a non-true result as not lockable (`ExecuteScalarAsync(...) is true`). The change makes the SQL contract explicit for other callers.
- `scripts/check-m10-contract.mjs` now pins the SHA-256 of migration 12 so an in-place edit fails a static check.
- Databases migrated from the separate unmerged draft branch (commit `6a723b8` edited the function there) already hold the corrected body; the replacement is idempotent for them.

**Tests** (`M10ProjectorScopeUpgradeTests`, 3 tests, real `harborops_projector`, `harborops_api` and `harborops_worker` logins):

| ID | Evidence |
| --- | --- |
| A1 fresh install | Empty schema applies 13 migrations, no pending model changes. The function body contains the NULL-safe return, is `SECURITY DEFINER`, pins `search_path=pg_catalog`, has no PUBLIC execute entry and is owned by the migration login. The projector gets explicit false for an absent scope and other sessions get false |
| A2 genuine historical upgrade | Only the original twelve migrations are applied to an empty schema (migration 13 never runs). The function body is the original. Then the API, a worker finalization, accepted events, a legacy correction and replay, and an archived shipment are created on that schema. Upgrade to 13 keeps the M8/M9/M16 and M10 fingerprints, audit row count, and the first twelve migration-history rows unchanged. The body becomes NULL-safe while oid, owner, `SECURITY DEFINER`, `search_path`, ACL (including the projector grant), result type and volatility stay identical |
| A3 restricted projector | Active scope true; archived shipment false; suspended organization false; absent shipment false; absent organization and shipment false; other tenant's organization with this shipment false; non-projector session false. Before the upgrade the same absent-shipment call returns NULL, which the test asserts as the historical behavior. The calls change no projection, application, timeline or evidence rows |
| A4 permissions | The projector holds EXECUTE on the scope function and not on the signed correction function; `harborops_api` holds it on the signed correction function and not on the scope function; the worker on neither. The API and worker get 42501 when calling the scope function. API, worker and projector cannot replace the function and a non-owner GRANT to PUBLIC leaves the ACL unchanged |
| A5 idempotency and downgrade | A second migrator run changes nothing. Downgrade without nonce evidence restores the exact original body on the same oid and re-upgrade reproduces the corrected definition. With a retained nonce row the downgrade is refused with a check violation and the corrected function stays in place |

A negative control removed the replacement from migration 13: the fresh and historical tests failed as intended, and the sources were restored.

## Issue B: default Compose did not supply the proof key

**Correction.** `compose.yaml` passes `M10CorrectionProof__Key: "${HARBOROPS_M10_CORRECTION_PROOF_KEY:?...}"` to the privileged `migrator` and the `api` only. The interpolation is fail-fast and its message names the variable and never a value. `.env.example` gains a placeholder (not a valid key, so an unedited copy is rejected by the migrator). No secret is tracked. The restricted `harborops_api` login and the privileged migrator login are unchanged. The static guard in `scripts/check-m10-contract.mjs` requires exactly two fail-fast entries, in `migrator` and `api`.

**Verification** (`fm-run`, disposable project `ho-verify-fm1`, private port and subnet override outside the repository so the local `harborops` stack was not touched):

| ID | Result |
| --- | --- |
| B1 valid key | Resolved configuration gives the identical value to `migrator` and `api` only; no key in build arguments or commands; API login `harborops_api`, migrator login `harborops` |
| B2 missing or empty key | `docker compose config` exits 1 and names `HARBOROPS_M10_CORRECTION_PROOF_KEY`; no secret value in the message |
| B3 invalid key | Invalid base64 and a 16-byte key make the migrator exit non-zero (exit code 139 from the container runtime abort after the unhandled `InvalidOperationException`); the supplied value is absent from the output; the verifier table holds no row |
| B4 real correction flow | 11 of 11 checks: migrator completes with the key; API ready and worker healthy; the database verifier equals the supplied key (compared in memory, never printed); an Analyst correction is 403; a Manager correction is 200, replays safely, persists exactly one correction and one nonce, and writes one `shipment.tracking_correction_created` audit row whose metadata carries no reason, proof, signature or nonce |
| B5 isolation | Running API holds the key; running worker and postgres do not; web has no entry in the resolved configuration (the web image was not built or started, because the check needs only the API path) |
| B6 overlays | `compose.g10.verify.yaml` and the combined `g16 + m9.worker + cg16 + m10 correction` layering still resolve, and the G10 file still requires the key |
| Log scan | The key is absent from all service logs of the run |

Operational consequence: any procedure that layers the base `compose.yaml` now also needs `HARBOROPS_M10_CORRECTION_PROOF_KEY`. The historical M2 to M7 milestone scripts and the `verify-m0`, `verify-m1` and `verify-m2` workflows already did not export `HARBOROPS_API_DB_PASSWORD` or `HARBOROPS_WORKER_DB_PASSWORD`, which the base file already required, so they could not resolve it before this change either. They were not modified, and GitHub Actions were not run or changed.

## Measured results on `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343`

Counts were read from the raw TRX files.

| Stage | Result | Duration |
| --- | --- | --- |
| Solution build, `-warnaserror` | Succeeded | 55 s |
| Unit | 239/239 | 4 s |
| Contract | 39/39 | 11 s |
| New projector scope tests | 3/3 | 14 s |
| Change-impact integration set | 126/126 | 10 m 4 s |
| Default Compose B1 to B6 and runtime check | 10 of 10 steps and 11 of 11 runtime checks | 1 m 38 s |
| Targeted mutation supplement | Control clean (14 unit, 33 integration); 6 of 6 mutants killed; 0 survivors, 0 invalid | 9 m 2 s |

The change-impact set is every integration class whose name contains M10, Migration, Upgrade, Audit, CombinedRetention, TrackingProjection, TrackingWorker or TrackingInbox. It includes the M10 runtime identity and correction authority tests (22), the M10 and historical upgrade tests, the audit hardening and integrity tests, the M9 worker tests and the projection tests. PostgreSQL's server log for the run has no deadlock lines and one FATAL entry, the deliberate administrator termination in a process-kill test.

### Mutations

Existing mutants that addressed migration 12 no longer test the effective definition, because migration 13 now replaces it. They were retargeted rather than left as equivalent mutants:

| Variant | Killed by |
| --- | --- |
| `m10-projector-session-guard-removed` (now removes the guard from both migrations) | historical upgrade test, projector identity test |
| `m10-projector-lock-null-for-unknown-scope` (edits migration 13) | historical upgrade, fresh install, projector identity test |
| `m10-projector-scope-reconcile-not-security-definer` | projector identity test, permissions test, fresh install test |
| `m10-projector-scope-reconcile-search-path-widened` | permissions test, fresh install test |
| `m10-down-leaves-corrected-projector-function` | historical upgrade test |
| `m10-twelfth-migration-edited-in-place` | historical upgrade test |

The run used the runner's development subset flag, so it is a targeted supplement. Two of these retargeted variants existed before; four are new. The other 38 mutants of the earlier `a26d19a` campaign test surfaces that did not change (correction authority, grants, triggers, routes, audit) and were not rerun.

## Coverage assessment

| Item | Status |
| --- | --- |
| Issue A (historical upgrade, NULL-safe projector scope, grants, idempotency, downgrade guards) | Satisfied by the tests above |
| Issue B (default Compose key supply, fail-fast, invalid keys, real correction flow, isolation, overlays) | Satisfied by the checks above |
| Full 312-test integration suite, 28 real-stack checks, audit HTTP 74, outage 14, `tracking-timeline` browser x 3 engines, image scans | NOT EXECUTED on this source. No `web`, API route or correction logic changed; the only runtime delta is the migration 13 function replacement and a Compose environment entry, both covered by the targeted set |
| 38 earlier mutants | NOT EXECUTED on this source (unchanged surfaces) |
| Web image build in the default Compose run | NOT EXECUTED; web key isolation is static only |
| G10-S01 conformance and G10-S25 independent review | NOT EXECUTED; acceptance pending |

## Intermittent PostgreSQL deadlock (`40P01`)

A focused review did not establish a cause, so the risk stays open and low confidence.

- The one occurrence was in `AuditRoleProvisioner.ProvisionAsync` during test setup (attempt `g10r-a1`). Npgsql redacted the detail, and the PostgreSQL server log was not preserved at that time, so the participating sessions are unknown.
- The provisioner issues cluster-level role statements and then one multi-statement `GRANT`/`REVOKE` script that touches catalog rows for every table in the schema. Another session changing the same catalog rows in a different order is a plausible mechanism, but no concurrent actor was identified. xUnit parallelization is disabled and the concurrent mutation run used a separate database cluster.
- Evidence since: 6 isolated loops, two 312-test passes, and in this campaign the projector tests, the 126-test impact set and the mutation control with 6 mutants, each provisioning roles, with no deadlock line in the preserved server logs of the frozen run.
- No defensive change was made, because a change without a reproduced cause could mask a real defect. The runner already preserves PostgreSQL server logs, which will carry the full participant detail if it recurs.

## Residual risks

- The deadlock above remains unexplained.
- The signed correction function still hard-codes `session_user = 'harborops_api'`; another API login gets a sanitized denial.
- The proof does not bind the correlation label.
- The API does not validate the key at startup. A wrong key reaching only the API yields sanitized 503 responses or denials, while the migrator rejects a malformed key. The default Compose supplies one value to both.
- Because the migrator aborts with exit code 139 on a malformed key, the failure is visible but not a conventional exit code 1.

## Evidence

Raw evidence stays on E: under `E:\HarborOps-Verification\evidence\`: `g10fm-frozen-tests` (stage logs, TRX, PostgreSQL server log), `g10fm-compose` (Compose steps, outputs, resolved-configuration checks, input file hashes), `g10fm-mutations` (runner JSON, per-variant logs) and the earlier `g10r-*` campaigns, all preserved. Development rehearsals (`E:\HarborOps-Verification\g10fm\evidence`) include the failed attempts that exposed test-harness defects (an archive step in the first historical test that skipped the accepted close-allocation rule, an evidence comparison taken after audited status writes, an antiforgery cookie ordering error in the smoke script); they are not gate evidence. The review archive `g10fm-final-evidence-20261107.tar.gz` (59 entries, 52 hashed files, no caches, traces or credentials) has SHA-256 `93ed14fedfe5d9a477aa4cc5ea832c92700fd32eaa8cad094cd6301cb732f413`, and `g10fm-evidence-manifest.sha256` lists each file hash. A scan for the disposable passwords and proof key found them in none of the evidence directories or in the archive. Disposable keys and passwords were created per run in a private directory and never committed.

## Reproduction

```bash
node scripts/check-m10-contract.mjs
docker compose -f compose.yaml config -q   # fails fast without HARBOROPS_M10_CORRECTION_PROOF_KEY
```

The database tests run in the .NET 10 SDK container against a disposable PostgreSQL 16 container with `--filter FullyQualifiedName~M10ProjectorScopeUpgradeTests`. The default-stack smoke check is `scripts/check-default-compose-correction.mjs` with `COMPOSE_FILE` layering `compose.yaml`, `compose.m9.worker.yaml` and a private port and subnet override.
