# G13/G15 Final Acceptance Closure Candidate

**Post-c3 G13/G15 acceptance boundary, 2026-11-11:** The approved G13-S20 limit is 250,000 report-member rows, four active reports per tenant, two per user, and 1,000,000 active tenant members. Historical c3 PASS at `4e7b9e2` was not unconditional acceptance: G13 S25 round 3 returned NOT PASS. [Hosted run 43090424080](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43090424080) completed FAILURE at `30fd28f6`; [run 43091966161](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43091966161) had a failed browser job; [run 43093329982](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982) at `067e1ab57f33fcabefae55727265499d86daf7ae` passed preflight, three-engine browser, crash recovery and several other checks while its longer technical jobs and final evidence ledger were pending at inspection. Do not combine passes from different source SHAs, reclassify a failed run as PASS, or treat the planned tests as executed evidence. **G13 and G15 acceptance pending**, no main merge or deployment. Authorized Actions are limited to the disposable post-c3 campaign. Retention deletion remains disabled. See [hosted run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md). Earlier findings, source hashes and test counts below are historical and remain valid only for their own source.

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
