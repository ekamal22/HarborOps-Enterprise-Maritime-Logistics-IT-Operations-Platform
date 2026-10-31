# Second M3 Correctness Audit Remediation

**Date:** 2026-10-31  
**Branch:** `feat/m3-port-vessel-reference-data`  
**Audit:** [Second correctness audit](M3_SECOND_CORRECTNESS_AUDIT.md)  
**Status:** Second-audit corrections implemented; subsequently verified locally with 25/25 G3 scenarios passing. G3 explicitly accepted and M3 integrated into `main` on 2026-10-31.

**Execution outcome:** G3-S01 through G3-S25 passed on `b72025d35d62d0a8a45b466986a0b3bd89eebadb`; the raw execution archive was independently reviewed before G3 was accepted on 2026-10-31. M3 integrated into `main`; the table below remains a historical record of initial remedy status ([executed verification](M3_EXECUTED_VERIFICATION.md)).

This register tracks the second audit's B01-B12 findings against the feature branch. A committed correction is **not** a passed verification. The [G3 verification plan](M3_VERIFICATION_PLAN.md) remains the acceptance contract.

| ID | Corrective implementation | Status |
| --- | --- | --- |
| B01 | `20261031110000_PortVesselReferences.cs` now uses valid `$$` delimiters for the PostgreSQL trigger function. `ReferenceHistoricalUpgradeTests` also exercises M3 rollback and reapplication while preserving M2 rows. | Source corrected; SQL execution pending |
| B02 | `verify-m3-local.sh` archives previous evidence before a new run and writes source revision/start timestamp to a fresh evidence directory. `assert-g3-evidence.mjs` rejects stale TRX/Playwright files and a mismatched checkout SHA. | Implemented; execution pending |
| B03 | Each verification stage records exit status and SHA in `g3-stages.tsv`. The index rejects missing/nonzero stages, skipped browser cases, failed TRX outcomes, absent integration runs, missing Trivy JSON or HIGH/CRITICAL findings. | Implemented; execution pending |
| B04 | The evidence index names actual test methods, requires all three browsers and distinguishes `EVIDENCE PRESENT, REVIEW REQUIRED` from acceptance. Missing cases remain INCOMPLETE and fail the index. | Implemented; requires execution and manual scenario review |
| B05 | `check-m3-reference-outage.mjs` stops and restores the disposable real PostgreSQL service and verifies live reference 503, recovery and post-downgrade 403. `reference-live-revocation.spec.mjs` changes real membership permissions during a delayed request. The synthetic 403/503 UI tests remain complementary. | Implemented; live tests pending |
| B06 | `ReferenceRevocationInterleavingTests` deterministically queues an administrator role downgrade before a reference write and queues a security-version change before another write using PostgreSQL lock observation. `ReferenceStore.LockManagerAsync` now locks the account row for reads before authorizing a write. | Implemented; concurrency execution pending |
| B07 | `ReferenceDataWorkspace.tsx` informs `OrganizationWorkspace.tsx` when the session, selected organization or access is invalidated. The parent immediately clears protected role/selection data and re-fetches current organization permissions. Live browser test asserts management controls disappear. | Implemented; browser execution pending |
| B08 | Reference pagination now has independent `paging` and generation state. A visibility-triggered refresh invalidates an older page and clears the busy indicator. The pagination browser spec exercises this interleaving. | Implemented; browser execution pending |
| B09 | `check-m3-docker-storage.mjs` checks Windows Docker Desktop's configured `diskImageLocation`, the supplied E:-backed Docker WSL data image, active `desktop-linux` context, Linux engine and E: worktree. It fails closed when active configuration cannot be confirmed. | Implemented; local environment confirmation pending |
| B10 | The runner refuses existing `ho-verify` containers/volumes or `ho-g2-pg` before starting, records failure and tears down only its owned disposable project and labeled test database container on exit. It never touches primary `harborops` resources. | Implemented; cleanup behavior pending execution |
| B11 | The browser harness runs specs per engine with application restarts between specs to prevent shared login-rate-limit collisions; full vessel create/edit/archive/restore, reload persistence, paging and real permission revocation are exercised across Chromium, Firefox and WebKit. | Implemented; browser execution pending |
| B12 | Historical upgrade tests cover M2 session/role preservation, repeat apply, M3 rollback and reapply, and direct database constraints. Snapshot tests check migration discovery and pending model differences. The local runner requires Release build, integration, browser, dependency/scan, recovery and documentation stages. | Source/test coverage expanded; real clean build and migration run pending |

## Execution requirements

The final verification must use a clean revision on the authorized E:-drive workstation. The runner verifies the active Docker Desktop WSL data directory, archives earlier evidence, records per-stage exits and rejects an incomplete evidence index. Start from the repository root with `HARBOROPS_VERIFICATION_ROOT` and `HARBOROPS_DOCKER_VHDX` configured for the active E: installation:

```bash
bash scripts/verify-m3-local.sh
```

Inspect `evidence/G3_EXECUTION_INDEX.md` alongside individual TRX, logs, browser JSON, PostgreSQL upgrade and Trivy output. The index intentionally does not grant acceptance. Failed/blocked/skipped results must be repaired and the entire matrix rerun at the resulting final source SHA.

No GitHub Actions may be dispatched or implicitly triggered. G3 was explicitly accepted and M3 integrated into `main` on 2026-10-31. This acceptance does not authorize M4 implementation, changes to primary volumes, C: storage migration or production deployment.
