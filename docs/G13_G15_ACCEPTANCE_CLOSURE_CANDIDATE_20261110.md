# G13/G15 Final Acceptance Closure Candidate

**G13/G15 source-locked acceptance checkpoint, 2026-11-12:** [Run 43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445) is in progress at `c6d2dc86e30913b8d67a011131f9980422e4c197`. Preflight, browser, process crash, G13 rollback/concurrency, G15 notification load/fanout and security checks passed at inspection; G13/G15 mutants, source-locked scale, database and final evidence reconciliation were still pending. [Run 43120072016](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43120072016) ended FAILURE: 13 technical successes but one G15 locked-intent mutant INVALID after a 400-second timeout. The current fixture changes the five-second delay to once per transaction without lowering the SKIP LOCKED behavioral expectation. [Run 43093329982 attempt 2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982/attempts/2) passed all technical jobs but failed the full atomic assertion gate. Evidence cannot be pooled across source SHAs, and source-only fixes are not executed proof. Approved G13-S20 Option A: 250,000 members per report and 1,000,000 active tenant members, with four live reports per tenant and two per user. Historical G13 round 3 independent review returned NOT PASS. **G13/G15 NOT ACCEPTED**, pending final source-locked technical PASS, independent S25 reviews, and explicit decisions. No merge, deployment, Turkish integration or retention deletion. See [run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) and [2026-11-12 reconciliation](REPOWIDE_DOCUMENTATION_RECONCILIATION_20261112.md). The prior source-bound facts below remain historical.

**Date:** 2026-11-10  
**Status:** SOURCE CANDIDATE, VERIFICATION PENDING. Formal G13/G15 acceptance remains NOT ACCEPTED until exact-SHA raw evidence reconciliation and independent S25 assessments.

## Candidate implementation

- Tenant reporting admission now has cross-instance actor and tenant advisory locks acquired before a repeatable-read materialization transaction. It rejects more than four live reports or two million active materialized member rows per organization, without publishing a rejected snapshot.
- A new restricted-identity migration introduces the tenant quota authority function and a global, bounded expired-M13-snapshot-only prune function. An API-hosted idle sweeper wakes every five minutes and deletes no more than 128 expired report snapshots per sweep. It does not touch M11/M15 immutable histories, notification recipients, audit, replay, or holds.
- Exact source-history/outbox uniqueness, 60-minute materialized K06 tolerance, strict tenant admission/rollback and HTTP notification dependency-failure/recovery tests are added.
- Effective mutation campaign additions target the new capacity guard, quota rollback, tenant authority, exact 60-minute boundary and source-history outbox uniqueness.

## Source and policy limits

These changes have not yet passed the final source-locked campaign. A finite 128-deletion sweep is not a guarantee that any arbitrarily large historical backlog drains within 30 minutes; the worker flags saturation for follow-up. Postgres relation disk allocation may not shrink after row pruning; the policy controls active report count and live member-row count, not allocated database file bytes. Performance envelope remains the approved provisional 170k/30 report benchmark and must be remeasured at the final implementation SHA. The latest committed formal gate decisions remain NOT ACCEPTED until independent review of the updated evidence.

## Required decision sequence

1. Run grouped Release build and targeted integration tests, 170k workload and effective mutation suite in one preflight.
2. Run full 15-job source-locked acceptance verification only after preflight success, retaining all failed attempts and raw artifacts.
3. Audit source mutation equivalence, SQL authorization, HTTP outage status/scoping, capacity and idle cleanup, performance regression and all 50 scenario criteria.
4. Complete separate G13-S25 and G15-S25 assessments, then record new formal gate decisions. No positive acceptance is inferred from a green workflow alone.
5. Keep protected main, production, Turkish localization, notification deletion and dispatch activation unchanged.
