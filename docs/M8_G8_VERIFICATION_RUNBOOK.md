# G8 Local Verification Runbook

**State:** M8 source candidate committed, not executed. D-006 acceptance pending; G8 acceptance pending.  
**Accepted baseline:** G0-G7 integrated into protected `main`.  
**Source:** Clean, exact HEAD of `feat/m8-durable-event-inbox`, draft PR #14.  
**Storage:** Every G8 attempt requires a new E:-backed verification root ending in `/m8`.  
**Exclusions:** M9 worker, M10 projections, final accessibility and production deployment.

This runbook governs **G8 technical verification**. A technical PASS is not D-006 approval, independent evidence acceptance, G8 acceptance or permission to merge. See [G8 verification plan](M8_VERIFICATION_PLAN.md), [second audit remediation](M8_SECOND_REMEDIATION_STATUS.md) and [independent evidence reconciliation](M8_G8_RECONCILIATION.md).

## Preflight and safety

1. Start from the M8 feature checkout on E:. Confirm a clean worktree, exact `git rev-parse HEAD`, accepted upstream baseline and current PR head. Do not claim evidence for a different or dirty revision.
2. **GitHub Actions must remain disabled.** Do not dispatch, enable, rerun, trigger or depend on workflows. If local fixes are required, commit and push only to the feature branch using `[skip ci]` after verifying workflow triggers are disabled. Never merge before explicit G8 acceptance.
3. All controllable project storage, repositories, caches, Docker/WSL data, browser assets, databases and evidence must be on E:. Permanent project-specific C: storage is prohibited. Unavoidable task-specific temporary C: files may exist only if inventoried, safely attributable, and deleted at task completion. Record C:/E: disk metrics and before/after temporary-path inventories. Do not delete unrelated C: files, Windows caches or retained Docker data.
4. Verify Docker Desktop's actual disk-image/WSL data location, not just environment variables. List existing Docker projects, volumes and networks before running. Do not touch default Compose, primary data, historical volumes or unrelated resources. The G8 project is `ho-verify-g8` with isolated ports and network.
5. Keep bearer credentials, passwords, connection secrets, private publisher payloads and other sensitive fixture details out of shell history, logs, shareable archives, screenshots and commit content.

## Fast failure detection without reduced scrutiny

Use one full run on each clean candidate revision whenever possible. If a targeted defect is found, fix and retest that narrow case first, then execute a **new complete source-locked G8 run**. A targeted test does not establish G8.

| Order | Required work | Why this order |
| --- | --- | --- |
| 1 | Script syntax, generated execution graph, all Markdown links, current-status consistency and M7/M8 Node contract tests | Quick failures before container startup |
| 2 | Clean Release .NET restore/build with warnings as errors, NuGet/npm advisories and frontend tests/build | Catch compiler, dependency and browser-build issues early |
| 3 | Fresh and repeated PostgreSQL integration, unit/contract suites, migration/EF snapshot, direct SQL attacks, real revocation waits, 9,999/10,000/10,001 capacity and real COMMIT disruption | Full data correctness and tenant boundaries |
| 4 | Disposable Compose stack, source CLI, three Playwright engines, outage/recovery, byte-accurate Pending-state continuity, three exact-image security scans | Full end-to-end and operational behavior |
| 5 | One passing control and all eleven source-isolated security mutations, each killed by an intended regression | Most expensive work only after other gates pass |
| 6 | Named-resource teardown, final Compose log collection, secret scan, immutable per-run tarball/manifest and independent archive scan | Produce safe, source-locked evidence |

No existing mandatory test, mutation, browser engine, dependency audit or image scan is omitted for speed. Avoid duplicated full integration or browser runs outside the source-locked runner unless diagnosing a failure. All source changes invalidate an earlier clean-run claim.

## Run command

From Windows Git Bash in a clean E:-backed checkout:

```bash
cd "/e/path/to/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform"
git status --porcelain
git rev-parse HEAD
export HARBOROPS_VERIFICATION_ROOT='E:\HarborOps-Verification\g8-attempt-01\m8'
bash scripts/verify-m8-local.sh
```

The checkout directory is an example, not a workstation-specific location. Use a new `g8-attempt-02/m8` parent for the next run even when repeating the same source SHA. The runner refuses previously used evidence roots.

The wrapper uses exit **1** for a technical or archive-finalization failure and exit **2** when technical verification and final archive safety pass but formal decisions are pending. Exit 2 is **not** G8 acceptance. Inspect `evidence/g8-technical-status.json`, `evidence/g8-formal-gate-status.json`, `archives/<run-id>/g8-finalization-status.json`, stage logs and the full 25-case scenario register. Preserve failed runs unchanged.

## Mandatory high-risk coverage

- Both `TrackingInboxRealCommitDisruptionTests.RealPostgresCommitTransportLossNeverAcknowledgesAndReplayResolves` variants must execute against the disposable real PostgreSQL connection in **both** fresh and repeated integration test passes. One interrupts before forwarding COMMIT; one interrupts after PostgreSQL reports COMMIT complete but before the client receives that response. Verify 503, independent database outcome and same-key retry. Fault hooks alone are insufficient.
- Restart testing must compare original private raw accepted bytes, canonical digest/bytes, receipt metadata, exact Pending work identity and counters across an actual PostgreSQL stop/start, with safe digest-only shared proof.
- Preserve real PostgreSQL row-lock races, tenant isolation, replay collision 409, original-receipt 202, canonical JSON equivalence, 20 concurrent deliveries, cross-source admission cap and exact response cache/security controls.
- All eleven source-isolated mutants must be killed by the intended tests. Unrelated compilation failures are not valid kills. Browser evidence must cover Chromium, Firefox and WebKit, including relevant accepted G0-G7 journeys.
- Evidence must contain exact source SHA and run ID, immutable archive and SHA-256 manifest, scanned final Compose logs, cleanup proof and separately recorded technical versus governance states. Do not leak original bearer or publisher payloads.

## Failure handling and delivery

If an early stage fails, preserve its raw records, diagnose the precise cause, make a minimal safe source/test/runner correction, run the targeted reproduction, and commit/push the feature-branch fix with `[skip ci]`. Then start a complete run with a **new** E: root. Never change test expectations to match a defect, weaken a negative assertion, skip a browser engine, reduce mutation coverage or mark a blocked case PASS.

Final handoff must report: tested commit SHA and clean worktree; complete stage table with exit codes; unit, contract and fresh/repeated integration counts; browser results by engine; eleven mutation results; image identities and vulnerability scans; REST/COMMIT/recovery and tenant/race evidence; run ID and exact E: paths; SHA-256 archive/manifest digests; all FAIL/BLOCKED/NOT EXECUTED scenarios; owned Docker cleanup; C: temporary file cleanup; and explicit remaining decisions. Independent evidence review and D-006 approval are separate. **G8 acceptance remains pending until explicitly granted.**
