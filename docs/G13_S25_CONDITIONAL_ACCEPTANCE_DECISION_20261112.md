# G13 Gate Decision, 2026-11-12

**Later G15 status, 2026-11-12:** This is a historical G13-specific decision/reconciliation record; it remains valid for G13's conditional acceptance. G15 was subsequently accepted and merged through PR #44 at `49934d793542ef43cfe587c54b16cfd5cce8e2f4`. [Final G15-S25 acceptance](G15_S25_FINAL_ACCEPTANCE_20261112.md) and [post-merge reconciliation](G15_S25_POSTMERGE_ACCEPTANCE_RECONCILIATION_20261112.md).

**Disposition: G13 CONDITIONALLY ACCEPTED FOR G13-ONLY INTEGRATION.** This supersedes the historical 2026-11-10 G13 NOT ACCEPTED decision for the tested G13 source only. It does not accept G15, authorize notification delivery, or approve deployment. The conditional gate decision was explicitly authorized on 2026-11-12. Formal reviewer independence remains **not attested**: the technical S25 assessment was performed in a session that also participated in previous remediation. The conditional decision acknowledges this procedural exception rather than recording a nonexistent independent sign-off.

## Exact source and evidence

- Frozen application source: `c6d2dc86e30913b8d67a011131f9980422e4c197`.
- Source-locked combined verification: [run 43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445), **SUCCESS** on 2026-11-12. All 15 jobs succeeded, including 14 technical groups and the final evidence reconciliation.
- Same-run TRX: **590 passed, zero failed or skipped**. Assertion audit: **248/248 automated technical assertions supported by declared witnesses**, 48/48 technical scenario checks. The remaining 7 policy and 8 review assertions are not automatically closed by CI.
- Mutation and recovery results are source-bound to this run. Earlier failed attempts remain part of the history and are not retrospectively passed.
- Approved G13-S20 Option A: 250,000 member rows per report, four live reports per tenant, two per user, at most 1,000,000 aggregate live member rows per tenant. Reports exceeding the single-report cap fail closed with 413 and no partial publication. The previous unrestricted realistic 170,000-shipment single-report target was **superseded**, not satisfied.

## G13 review result and residual limits

**Technical S25 recommendation: PASS for G13 on the tested combined source**, with the independence qualification above. Reviewed areas include signed reporting authority, tenant isolation, tenant/user quota enforcement, early admission and late commit guards, snapshot rollback/cancellation, accepted source-history preservation, resource measurements, 45 G13 source mutation cases, four restricted-SQL security mutations and browser security coverage.

Recorded residuals requiring monitoring rather than a new G13 blocker:

1. Report admission and aggregate tenant load can still cause bounded 429 or 413 refusals. There is no guaranteed zero-wait admission under all cross-tenant workloads.
2. Substantial PostgreSQL WAL and temporary-file churn occurs during the realistic benchmark. Monitor capacity, concurrent tenants and storage in later scale/operations milestones.
3. The 25-second statement limit and 30-second proxy boundary are demonstrated under the measured fixtures, not under every production topology.
4. Report snapshots expire after ten minutes; automatic cleanup remains **disabled by default**. No notification retention deletion is authorized.

## Integration boundary

The verified branch contains **G13 and unaccepted G15 together**. No wholesale merge of that branch is permissible under this decision. A G13-only integration branch must be constructed from protected `main` with only the required G13 source, migrations, client changes and scoped tests. Verify the split branch's complete source, dependency graph, migration ordering, compile/runtime correctness, and security boundaries before any merge; the earlier combined source run cannot certify a newly assembled source. The G13-only integration must not include G15 migrations, notification service, endpoints, UI, new delivery enablement, or retention operations.

**G15 remains NOT ACCEPTED.** The still-open S25 finding concerns the `outbox-claim-skip-locked-removed` mutant: it returned a cancellation during test unwinding rather than the intended prompt-progress assertion. A valid clean control plus an explicit bounded behavioral kill is required in the follow-up G15 verification. The large revoked-receipt backlog and database operating characteristics are residual follow-up performance checks. G15 source must be rebased on the merged G13 baseline, tested at one exact source SHA and reviewed before a separate merge.

**Merge status at this decision:** pending a proven clean G13-only split and final merge-readiness check. **Main:** unchanged. No production activation, deployment, retention deletion or Turkish localization integration authorized by this decision.
