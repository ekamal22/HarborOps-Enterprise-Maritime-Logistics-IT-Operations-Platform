# G13/G15 Final Acceptance Closure Candidate

**Current gate boundary (2026-11-11):** G13-S25 round 3 returned NOT PASS on the c3-tested source. G13-S20 Option A has since been approved with a 250,000-member cap per report, superseding the former single-report shipment-scale requirement. Post-c3 source remediation and its unexecuted verification witnesses are tracked in [the current status](G13_POST_C3_REMEDIATION_20261111.md). Historical pass/failure records below remain unchanged and apply only to their recorded SHAs. G13 and G15 are not accepted; the G15-S25 round 3 verdict is not recorded here. No main merge, GitHub Actions, production activation or retention deletion is authorized.

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
