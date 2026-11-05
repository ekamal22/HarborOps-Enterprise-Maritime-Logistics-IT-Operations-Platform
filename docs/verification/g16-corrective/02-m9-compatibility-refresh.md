# M9 compatibility refresh (diagnostic, combined revision kept separate)

**Method.** Source comparison of `feat/m16-audit-access-hardening` at `e571157` against the actual latest `feat/m9-worker-reconciliation` (`7665bc419618bb587873169b1dff8772e6a68e22`, 14 commits after the head recorded in the first campaign). A disposable, local-only worktree merged both heads (`85fc754`, then `32fc9f2` after count adjustments). It was never pushed, neither original branch was modified and nothing was merged into `main`. Its results are not part of the standalone M16 campaign.

## Observed facts

| Area | Finding | Class |
| --- | --- | --- |
| Textual conflicts | Both branches changed `Program.cs`, `OrganizationPolicy.cs` (enum and `Allows`), `MigrationSnapshotTests.cs`, `OrganizationUpgradeAndConstraintTests.cs` and nine shared documents. `HarborOpsDbContext.cs` and the model snapshot merged automatically | Definite, resolved by keeping both sides |
| Build | Combined solution builds with 0 warnings and 0 errors under `-warnaserror` | Verified |
| Unit and contract | 228 unit and 39 contract tests pass on the combined revision | Verified |
| Migration order and snapshot | `20261104180000_AuditEntries` then `20261104190000_TrackingWorker`; `MigrationSnapshotTests` (both migrations listed, snapshot equals model) pass on the combined tree | Verified |
| Hard-coded migration counts | First full combined run: 268 of 273 passed. The 5 failures were all `Expected: 10, Actual: 11` (head count assertions in `ReferenceHistoricalUpgradeTests`, `TrackingInboxAuditSqlTests`, `TrackingInboxMigrationTests`, `TrackingWorkerMigrationTests`, `AuditMigrationTests`). Both branches had set 10 independently, so Git merged those lines cleanly but the combined head is 11. One assertion is semantic, not a number: after a rejected downgrade the combined chain reverts M9 first and the M16 retained-evidence guard stops it, leaving 10. After the adjustments the full combined suite passed 273 of 273 (11 m 59 s, run exclusively) | Definite silent semantic conflict, confirmed and resolved in the diagnostic tree only |
| Worker grants | M16's conditional worker grants no longer include INSERT on `tracking_work_attempt_events` (M9 revokes it), match M9's UPDATE column list, `m9_lock_active_organization` execute and `tracking_processing_effects` insert. The earlier contradiction is corrected | Resolved |
| Provisioning paths | Two paths create or grant `harborops_worker`: M16's `AuditRoleProvisioner` (automatic, run by `--migrate`) and M9's `scripts/m9-worker-role.sql` (manual). They are now consistent but remain two sources of truth | Decision needed, acceptance pending |
| Lock order | M9 takes `organizations ... FOR UPDATE` first (`m9_lock_active_organization`, admin store), then dependent rows, which is the accepted order. The audit table has no foreign key to `organizations`, so audited M9 writes add no lock edge. All 12 original lock-race tests and M9's lease, race, kill and ambiguous-commit tests pass in the combined run | Verified at integration level |
| Audit and worker | M9 has no reference to `audit_entries` or `m16_*`. M16 does not audit M9's attempt history, effects or work-item transitions; M9 keeps its own immutable attempt log. Whether worker effects need audit rows under M16-D03 is not decided in the documents | Decision needed, acceptance pending |
| Compose and runner | The combined revision was verified only through the integration database. M16's `compose.g16.verify.yaml` has no worker service and M9 has its own stack runner, so a combined Compose, browser and outage verification was not run | Not executed |

## Required before integration (for the migration coordinator)

1. One provisioning path for `harborops_worker`, and a combined Compose stack with a worker identity.
2. One regenerated snapshot and one migration-count source (derive the head count instead of hard-coding it).
3. Decide whether M9 worker effects are audited by M16.
4. Rerun the G8, G9 and G16 gates on the combined revision, including browsers and the outage stage.
