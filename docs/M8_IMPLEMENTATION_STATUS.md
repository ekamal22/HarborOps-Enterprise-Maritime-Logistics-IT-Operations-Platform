# M8 Implementation Status

**As of:** 2026-11-04 (first and second correctness audit remediations committed; runtime verification pending)
**Source branch:** `feat/m8-durable-event-inbox`
**Planning branch:** `plan/m8-durable-inbox-immutable-history` (draft PR #13)
**Accepted baseline:** `main` at `47f030f9fa4b588d589f3ae082d03b3d5b94736a`, G0-G7 accepted.
**Status:** M8-02 persistence and M8-03 intake implementation candidates committed. M8-04 tests and a dedicated G8 runner authored. **No local .NET/PostgreSQL/Compose/Playwright verification has been executed.**
**D-006:** Option A used as a provisional implementation contract. **Acceptance pending.**
**G8:** Not executed and not accepted; 25 required scenarios remain subject to raw evidence and independent review.
**Integration:** Not merged into accepted `main`; GitHub Actions disabled.
**Deployment:** None. No internet-facing hosting or accepted event processing.

## Implemented source candidate

| Boundary | Source | Intended behavior |
| --- | --- | --- |
| Domain canonicalization | `src/HarborOps.Domain/Tracking/TrackingReceiptPolicy.cs` | Fixed-version canonical UTF-8 serialization, SHA-256 replay digest over normalized approved M7 events |
| EF model | `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs`, model snapshot, `TrackingInboxEntities.cs` | Receipt, immutable event, Pending work, bounded conflict entities, composite tenant/source/credential keys |
| Additive migration | `20261103190000_TrackingInboxAndHistory.cs` | Four new PostgreSQL tables, scoped unique key, append-only guards, deferred receipt-completeness checks, disposable-only downgrade |
| Transactional intake | `src/HarborOps.Infrastructure/Integrations/TrackingInboxStore.cs` | Organization lock, original bearer revalidation, same-key replay, 409 collision, pending cap, atomic receipt + event + Pending work and post-commit-only success |
| HTTP and DI | `TrackingEndpoints.cs`, `DependencyInjection.cs`, `IntegrationSourceStore.cs` | Preserve negative M7 contract, convert authenticated valid new/replayed event to HTTP 202 only after transaction commit, sanitize failures and recheck revocation |
| Regression tests | `TrackingReceiptPolicyTests.cs`, `TrackingInboxHttpTests.cs`, `TrackingInboxSqlTests.cs`, `TrackingInboxMigrationTests.cs`, `TrackingInboxFailureWindowTests.cs`, `TrackingInboxRealCommitDisruptionTests.cs`, `TrackingCommitSocketProxy.cs`, `TrackingInboxAuditSqlTests.cs`, `TrackingInboxQuotaBoundaryTests.cs`, `TrackingInboxLockRaceTests.cs`, `TrackingInboxCredentialWindowTests.cs`; updated M7 tests | Canonical digest, 20-way replay/collision, temporal authorization, live row-lock waits, 10,000-item admission, lost acknowledgments, cancellation, adversarial SQL, populated upgrade/downgrade guards and restart durability |
| Local verification scripts | `scripts/verify-m8-local.sh`, `scripts/check-m8-*.mjs`, `run-g8-mutations.mjs`, `summarize-g8-evidence.mjs`, `compose.g8.verify.yaml` | Separate `ho-verify-g8` E:-backed stack, ports 55438/8088/5178, subnet `172.30.252.0/24`, TRX/browser/mutation/scan/evidence stages |

No M9 retry/worker claim/dead-letter execution, no M10 projection, no public receipt GET, no source-profile expansion, and no retention deletion are included.

## Source-level findings and corrections

- The first EF source assembly accidentally duplicated portions of the accepted model. Both EF mapping and snapshot were rebuilt from the clean M7 main baseline and the M8 mappings inserted once, with exact matching block content. This is a source-only correction, not a passing build.
- The legacy G7 no-durable-ack unit/contract assertions and historical migration test boundaries were updated to distinguish M8 behavior from original immutable G7 evidence. G7 historical records were not altered.
- The G8 Compose configuration has isolated ports, subnet and project; browser fixtures use a configurable base URL. The runner checks the E: path and refuses an existing disposable project before provisioning.
- Controlled mutation coverage now defines 11 variants across source digest, authorization, tenant scope, immutable history, collision, quota and commit integrity. They have not been executed here. See [remediation register](M8_CORRECTNESS_REMEDIATION.md).

## Second audit fixes

The [second audit remediation register](M8_SECOND_REMEDIATION_STATUS.md) documents source-level repairs for evidence finalization, actual PostgreSQL COMMIT wire interruption, immutable run archives, byte-accurate restart recovery and separate technical/governance status plus sealed archive finalization. These are implementation definitions, **not** executed G8 evidence.

## Unverified and blocked requirements

1. **D-006 acceptance pending.** Finalize replay/receipt contract, Pending-cap default, immutable retention and M9 worker handoff before freezing schema and HTTP semantics.
2. **No executable G8 run.** .NET and PostgreSQL/Docker/Playwright tooling are not available to this repository connection; no compile, migration, fixture, browser, security mutation or image scan PASS is claimed.
3. The [G8 25-scenario matrix](M8_VERIFICATION_PLAN.md) still requires actual two-session revocation interleavings, database restart, commit uncertainty injection, client cancellation windows, direct SQL attack outcomes, pending-cap boundary, clean full G0-G7 regressions, three-engine browser evidence, advisory/image scans, exact source/image hashes and E:-backed cleanup.
4. Tests and scripts may expose further compile, database, concurrency or framework defects on first execution. Correct those on the feature branch and rerun the full affected matrix before proposing acceptance.
5. An independent review of the sanitized raw G8 evidence is necessary before G8 acceptance. No changes to historical accepted M0-G7 evidence are authorized.

## Execution and acceptance boundary

Run the existing `bash scripts/verify-m8-local.sh` from a clean E:-backed checkout after verifying Docker/WSL storage placement and [local storage policy](LOCAL_STORAGE_POLICY.md). Set `HARBOROPS_VERIFICATION_ROOT` to a **fresh, single-use** E:-backed path ending in `/m8`, for example `E:\\HarborOps-Verification\\g8-attempt-01\\m8`. Use a new parent for every repeat. The runner performs cheap validation before expensive integration, browsers and all eleven mutations. Original G8-S01 and G8-S25 remain blocked pending D-006 and independent review. Preserve exact raw evidence, staged archive hashes and separate supplement findings rather than rewriting original runs. Follow [G8 verification runbook](M8_G8_VERIFICATION_RUNBOOK.md) and [independent review](M8_G8_RECONCILIATION.md).

**Next decision:** D-006 Option A acceptance or an explicit alternative. **Next executable gate:** G8, acceptance pending. **Merge:** Only after independent evidence review and explicit G8 acceptance.
