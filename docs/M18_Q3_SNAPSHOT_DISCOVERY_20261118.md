# M18 Q3 Historical Snapshot Discovery

**Date:** 2026-11-18
**Scope:** G18-S24-A01 and the roadmap's quarter-three checkpoint.
**Disposition:** NOT APPLICABLE, pending the independent reviewer's concurrence. This is an applicability finding, not migration proof and not a gate decision.

## Discovery

A read-only metadata inventory (`scripts/g18-retained-inventory.mjs`) listed every named Docker volume, every container with its state, restart count and volume mounts, the evidence archives and any dump directory. No volume was mounted and no retained database was contacted. The same command was run before the campaign and again at the end; a matching digest shows no retained resource changed.

| Candidate | Finding | Used |
| --- | --- | --- |
| `harborops_harborops-postgres` (created 2026-10-30) | Backs the compose project `harborops`, whose database, API and web containers were running throughout. A consistent copy needs the database stopped or a client session opened against it, and neither is permitted for a retained, in-use resource. G6 recorded that it holds only the M0 bootstrap record. | No |
| `ho-verify-oldcopy` | Origin undocumented; G6 discovered it and did not use it. | No |
| `ho-g13g15-pg-dev-data`, `ho-g13g15-pg-dev2-data` | Disposable synthetic G13 and G15 verification data, not a retained historical snapshot. | No |
| `s1scratch` and other projects' volumes | Unrelated. | No |
| `E:\HarborOps-Verification\archives` | Test evidence archives, not database snapshots. | No |

## Compensating evidence

A populated database at the last accepted schema (`20261112090000`) is upgraded to the M18 head. A content hash and row count of every pre-existing table, including the M16 audit trail, is identical before and after, a second application is a no-op, the downgrade guard refuses to drop retained incident evidence, and a clean downgrade followed by reapplication succeeds (`M18MigrationTests`).

## Limitation

This does not prove compatibility with any older retained production data. If a representative snapshot is supplied, reopen this checkpoint and run it on a disposable clone. G18-S24-A02 (original unchanged, fresh and upgrade migrations and runtime grants correct) and G18-S24-A03 (regressions) remain mandatory and are recorded separately.
