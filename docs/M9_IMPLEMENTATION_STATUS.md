# M9 Implementation Candidate Status

**Repository-wide documentation reconciliation:** Current-facing project documents now distinguish this corrected implementation candidate from accepted M8 code and from future M10 processing. The [correctness audit](M9_CORRECTNESS_AUDIT_20261104.md) remains source-only. No M9 runtime verification, M9-01 acceptance, G9 acceptance or merge into `main` is implied.

**Recorded:** 2026-11-04  
**Branch:** `feat/m9-worker-reconciliation`  
**Baseline:** accepted `main` `7b9e19d730e3a09c5e1184262460dfeac9319e3d`  
**Contract:** M9-01 local decisions acceptance pending  
**G9:** Technical campaign executed on `7665bc419618bb587873169b1dff8772e6a68e22` (see [executed verification](M9_EXECUTED_VERIFICATION.md)); every executable scenario passed. G9-S01 is blocked by contract acceptance, G9-S25 independent review is not executed, and G9 acceptance pending.  
**Integration:** Not merged into `main`.

## Candidate source

- `src/HarborOps.Domain/Tracking/TrackingWorkPolicy.cs`: finite lease, attempt, redrive and deterministic retry rules with runtime validation.
- `src/HarborOps.Infrastructure/Persistence/Migrations/20261104190000_TrackingWorker.cs`: additive M9 lease fields, state transition constraints, append-only attempt history, receipt-unique immutable processing effects, scoped foreign keys and guarded downgrade.
- `src/HarborOps.Infrastructure/Integrations/TrackingWorkStore.cs`: organization-first work claim, PostgreSQL `SKIP LOCKED`, lease fencing, heartbeat, timeout recovery, source-locked event resolution and effect-once finalization.
- `src/HarborOps.Infrastructure/Integrations/TrackingWorkAdminStore.cs`: scoped, paginated status queries and attributed redrive with bounded generations.
- `src/HarborOps.Api/Integrations/TrackingWorkEndpoints.cs`: authenticated work inspection and CSRF-protected Manager-only redrive; Auditor and Analyst read-only.
- `src/HarborOps.Worker`: opt-in host replacing the idle shell and an image build definition.
- `src/HarborOps.Infrastructure/Integrations/TrackingInboxStore.cs`: unresolved backlog includes Pending, Leased, RetryWaiting and NeedsReconciliation.
- `compose.m9.worker.yaml`, `compose.g9.verify.yaml`, `scripts/m9-worker-role.sql`: opt-in deployment, isolated verification configuration and restricted database privileges.
- EF context, snapshot, entity mappings, API registration and role permission matrix updated together.

The accepted M8 receipt, event and collision tables and their existing migration remain unchanged.

## Deliberate M9 boundary

Equipment events can be resolved only to an active tenant-owned container and a unique shipment-container assignment valid at the original event timestamp. Ambiguous, unknown and opaque transport identifiers enter NeedsReconciliation rather than being guessed. Successful M9 effects do not update shipment workflow statuses, timelines or projections. M10 owns projection processing.

A valid durable receipt survives revocation of the source credential after ingestion. A suspended organization cannot receive a newly committed processing effect. Processed outcomes remain terminal; redrive requires a separately authorized, attributed generation and cannot rewrite receipts or effects.

## Tests authored but not executed

*Historical record from the time the candidate was authored. These tests were later executed; see the [executed verification record](M9_EXECUTED_VERIFICATION.md) and the [remediation register](M9_CORRECTNESS_REMEDIATION.md).*

- `tests/HarborOps.UnitTests/TrackingWorkPolicyTests.cs`
- `tests/HarborOps.IntegrationTests/TrackingWorkerIntegrationTests.cs`
- `tests/HarborOps.IntegrationTests/TrackingWorkerMigrationTests.cs`
- Existing historical upgrade and downgrade migration assertions updated for the additive M9 migration.
- `scripts/check-m9-contract.test.mjs` performs static source contract checks.
- `scripts/verify-m9-local.sh` stages source-locked development checks on E:-backed isolated infrastructure. This is deliberately **not** the complete G9 acceptance campaign.
- Subsequent source corrections make transition audit entries database-generated, deny direct worker ledger inserts, add live security-version revalidation, and route concurrent reference changes to reconciliation.
- The G9 Compose configuration is standalone and port-isolated; worker runtime exposes a database-backed `--readiness` command and a container health check.
- Runtime attempts and manual redrive budgets are fixed at five and three, respectively, to match the PostgreSQL constraints.

No tests, build, database migrations, browser tests, worker races, vulnerability scans or controlled outage scenarios were run during preparation of this branch. Do not treat the above test source as execution evidence.

## Follow-up correctness audit

The ten original source-level findings have corrective changes, reviewed in [M9 follow-up correctness audit](M9_CORRECTNESS_AUDIT_20261104.md). No runtime verification has been performed. The corrected candidate is not yet approved for G9 execution or merge.

## Outstanding before G9 can be considered technically ready

1. Explicitly approve or amend the six decisions in `docs/M9_LOCAL_CONTRACT_PROPOSAL.md`; any changes require code and regression alignment.
2. Build all projects with warnings as errors; run unit, contract and PostgreSQL integration suites twice in an isolated E:-backed environment.
3. Complete every G9-S01 through G9-S25 case in `docs/M9_VERIFICATION_PLAN.md`. Include two independent workers, forced lease expiry, stale-token fencing, retry exhaustion, orphaned claim recovery, source suspension and redrive race tests.
4. Add and execute actual crash windows and ambiguous-commit interruption witnesses. Confirm one effect per receipt under repeated, concurrent and adverse execution.
5. Complete the G9 security mutation matrix, affected M0-G8 regressions, browser coverage as applicable, vulnerability and image scans, teardown integrity and disk-storage compliance.
6. Seal an immutable evidence archive with exact source SHA, raw results, hashes and failed-attempt history. Obtain an independent evidence review and explicit acceptance.
7. Reconcile the local verification findings on this branch. Do not merge or claim G9 closure while any mandatory case is unexecuted, failed or blocked.

## Technical verification record

The source-locked G9 technical campaign (attempt 06) passed on `7665bc419618bb587873169b1dff8772e6a68e22`: 791 of 791 TRX results (226 unit, 39 contract, 263 integration twice), a 14-check isolated Compose stack end-to-end run with the restricted worker role, twelve mutation families killed by intended tests, clean NuGet, npm and Trivy scans of the four exact-source images, zero leftover resources and a verified evidence archive. The campaign found one product-source defect (migration and snapshot drift, V02) and several test and tooling defects, all fixed; see the [remediation register](M9_CORRECTNESS_REMEDIATION.md). M9-01 contract approval, independent evidence review and G9 acceptance are separate decisions that remain pending. Nothing is merged into `main`.

## Parallel integration

The M9 branch owns its EF snapshot and migration until integration coordination. M12, M16, M17 and M19 may progress independently but must reconcile shared security, persistence and API contracts before combining branches. The accepted G0-G8 status, execution graph and history remain unchanged.

**Disposition: Implementation candidate; source and tests require execution and correction. G9 acceptance pending.**
