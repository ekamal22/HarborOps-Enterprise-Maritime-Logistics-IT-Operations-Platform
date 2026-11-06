# M10 Executed Verification Record (G10)

**State:** Technical verification in progress. **G10 acceptance pending.** No gate decision, merge authorization or deployment authorization is implied.
**Baseline:** `main` at `5edf0035a1fb9d854cba00673eb8fdf1f3033b0c` (merge of [PR #23](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/23), 2026-11-06), which contains accepted M0-M9 and M16/G16 plus the M10 source.
**Verification branch:** `feat/harborops-m10-g16-verify-f80276`.
**Restriction preserved:** The M9 transport-backlog hold and the 10,000 unresolved-work cap are unchanged. M10 indexes only M9-resolved equipment events and does not process parked transport events.

This record is updated as the campaign progresses. Earlier source audits and remediation registers are historical and are not rewritten.

## Defects found and corrected during verification

| ID | Severity | Finding | Root cause | Correction |
| --- | --- | --- | --- | --- |
| D1 | Blocker | The solution did not compile (CS0246 in `Program.cs`, xUnit2031 in `TrackingProjectionG10Tests.cs`). | The M10 source was merged without ever being compiled: a missing namespace import and an analyzer-rejected assertion. | Added the import and the `Assert.Single` overload. |
| D2 | High | The restricted `harborops_projector` role could not run at all. `DirtyAsync` read `shipment_tracking_correction_heads`, which the committed provisioning script never granted, and the startup self-check did not test that privilege. The service started, then failed every sweep, so no projection was ever materialized by the projector. | Grant list and query list diverged; verification only ever ran the store as the migrator. | Added the grant, added the privilege to the startup self-check (fail closed), added a contract guard, and added tests that run the store and the hosted service under the real role. |
| D3 | Low | `m10_lock_projector_scope` returned SQL NULL for an unknown shipment instead of false. | Three-valued logic in the final `RETURN`. | `COALESCE(..., false)`; the caller already treated NULL as not locked, so this tightens the contract and is covered by a real-login test. |
| D4 | Medium (verification tooling) | The G10 runner called a host `dotnet` that is not .NET 10, omitted the M9 worker, M16 audit, restricted-role and M10 stack stages, and its mutation helper covered only three reducer mutants and also needed host `dotnet`. | Runner written without execution. | Rebuilt the runner on the accepted combined stack with a containerized SDK, a derived stack check with M10 checks, and a container-based mutation runner with M10 privilege, trigger, route and projector mutants. |
| D5 | Low (verification tooling) | `check-planning-docs.mjs` asserted stale wording (`G10-G24 remain open`, `G0-G9 are verified and explicitly accepted`), and the accepted combined mutation runner carries a pre-F001 anchor that no longer matches the provisioner. | Assertions not refreshed after the G16 reconciliation. | Refreshed the assertions; the G10 mutation runner uses the current anchor. The accepted combined runner is left unchanged. |

## New regression tests

`M10RuntimeIdentityTests` (real `harborops_api`, `harborops_worker`, `harborops_projector` logins): privilege matrices, definer-trigger behavior, M16 audit coexistence, restricted-API HTTP flows (Manager-only, CSRF/Origin, idempotency, tenant boundary), real concurrent lock waits against the projector lock and an uncommitted correction, the hosted projector service skipping a poisoned shipment and recovering, projector killed before COMMIT, a late M9 effect racing a rebuild, a 12,001-event history (index plan, full keyset paging, rebuild bounds, stale cursor), live role downgrade, rate limit and session revocation.
`M10UpgradeTests`: populated history backfilled by the real M10 migration with M8/M9/M16 evidence fingerprints unchanged, immutable backfilled rows, protected downgrade, and an empty-history downgrade control.

## Final campaign

Pending. The final source-locked run, evidence location, SHA-256 manifest, stage outcomes and G10-S01 to G10-S25 dispositions will be recorded here after the run on the committed final revision.
