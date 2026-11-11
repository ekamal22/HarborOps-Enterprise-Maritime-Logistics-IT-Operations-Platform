# G13/G15 Final Conditional Policy Decision Record

**Current gate boundary (2026-11-11):** G13-S25 round 3 returned NOT PASS on the c3-tested source. G13-S20 Option A has since been approved with a 250,000-member cap per report, superseding the former single-report shipment-scale requirement. Post-c3 source remediation and its unexecuted verification witnesses are tracked in [the current status](G13_POST_C3_REMEDIATION_20261111.md). Historical pass/failure records below remain unchanged and apply only to their recorded SHAs. G13 and G15 are not accepted; the G15-S25 round 3 verdict is not recorded here. No main merge, GitHub Actions, production activation or retention deletion is authorized.

> **Update (2026-11-10, later source):** The conditions and open items recorded here describe source `15cabd8c542159753c927c2bd65b4346223e84f6`. Tenant quota and bounded idle cleanup, the specific mutation witnesses and the real HTTP outage test were subsequently implemented and verified locally at `5bcb7e5b2ec5e590bd26e64ffd659c17aaee1f92`; see the [local closure results](G13_G15_LOCAL_CLOSURE_RESULTS_20261110.md), and for the later remediated source `3e63bd0e71bdf4a80cd8ea1d27e7cceca3c37524` the [round 2 results](G13_G15_LOCAL_CLOSURE_RESULTS_C2_20261111.md). This record's dispositions remain in force until new positive decisions are recorded after independent review. Acceptance pending.

**Date:** 2026-11-10  
**Verified source:** `15cabd8c542159753c927c2bd65b4346223e84f6`  
**Evidence:** [source-locked 15-job run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415), all 543 TRX tests passed.  
**Scope:** Policy and technical conformance decisions only. No production activation, migration, deletion, deployment or merge.

## G13 D-005 and numerical storage decision

D-005 Option A, `m13-kpi-v1`, is approved. The following existing verified technical safeguards remain unchanged: 500,000 member rows per report with atomic capacity failure, 10-minute snapshot expiration, 100-row drill page maximum, a 15-second SQL statement timeout, a 25-second report-build budget, one simultaneous build per actor and organization and up to eight expired report deletions in each selected-organization report-build transaction.

The last 170,000-shipment/30-report sample recorded p50 **11,903 ms**, p95 **13,000 ms**, worst measured **13,493 ms**, 1,600,331,776 bytes final report/member relations (6,094,848 bytes initial), and peak process working set **662,376,448 bytes**. The provisional verification envelopes of 15,000 ms p50, 20,000 ms p95, 2 GiB additional report-table relation growth and 1 GiB process working set passed. These results are not a production SLA or a tenant-level physical disk quota.

**Selected numerical operating targets for subsequent implementation and verification:**

| Resource | Target decision | Present implementation conformance |
| --- | --- | --- |
| Unexpired report snapshots per organization | At most 4 at a time | NOT ENFORCED per tenant |
| Live stored member rows per organization | At most 2,000,000 across active snapshots | NOT ENFORCED per tenant |
| Individual report membership | At most 500,000 | VERIFIED |
| Access-token expiry | 10 minutes | VERIFIED |
| Expired-report maintenance interval | At most 5 minutes, including idle periods | NOT IMPLEMENTED |
| Scoped pruning batch | At most 8 expired reports per organization per pass | VERIFIED on report build only |
| Expected expired report removal | Within 30 minutes after expiry under admitted load, alert on lag and backlog | NOT VERIFIED or guaranteed |
| Page, statement and request limits | 100 rows, 15 seconds, 25 seconds respectively | VERIFIED in existing campaign |
| Temporary benchmark process/disk envelope | 1 GiB peak process working set and at most 2 GiB report-relation growth in 30 builds | VERIFIED for the disposable 170,000-shipment reference load only |

The targets above are **engineering decisions for a future closure candidate**, not evidence that the current source enforces them. The existing 30-report growth measures PostgreSQL relation allocation, including reuse/bloat, not a permanent tenant quota. No physical disk allowance or admission guarantee can be claimed from this measurement. In particular, only doing cleanup on a subsequent report build does not satisfy a five-minute idle cleanup rule. This unresolved implementation and evidence mismatch blocks G13-S01-A04, G13-S20-A07 and final G13 acceptance. Do not label Phase 3 entirely complete for G13.

## M15 P10 conformance condition

**Decision: P10's technical verification condition SATISFIED on the tested SHA.** Source-locked evidence includes real `FailedOutboxInsertRollsBackNewImmutableHistoryInSameTransaction`, `RejectedAuditWriteRollsBackDispositionCompletelyAndKeyRemainsReusable`, actual SIGKILL at before-history, claim, fanout and after-receipt boundaries, post-commit recovery, no duplicate recipients, and passing accepted-M11 regression suites. The availability tradeoff remains accepted: outbox capture failure can fail the originating M11 history transaction. This closes the conditional evidence issue, but does not override other G15 acceptance criteria.

## M15 P12 retention

**Decision: provisional periods preserved; deletion NOT APPROVED.** Unread/read notification rows: 90 days. Acknowledged rows: 30 days. These are planned retention periods, not deletion triggers. Background cleanup stays disabled. No audit log, immutable exception history, source-history intent, replay record or legal hold is authorized for deletion. There is no evidence-based reason to activate physical deletion as part of G15 gate acceptance.

## Remaining policy gate

G15 P01-P09, P11, P13-P14 remain approved. P10 is now technically evidenced and P12 remains deliberately non-deleting. G15 still requires G15-S21/S24 evidence precision and a passing independent S25 decision. G13 requires implementing and independently validating its selected numerical storage and cleanup policy. No acceptance is inferred from a green workflow.
