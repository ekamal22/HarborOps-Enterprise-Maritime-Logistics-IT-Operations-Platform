# G13/G15 Approved Policy and Verification Envelope

**Decision date:** 2026-11-09.
**Approval origin:** Explicit approval of the proposed M15 policy package and G13 numerical test recommendations.
**Scope:** G13/G15 combined verification and eventual acceptance review, not a source-code waiver, merge, deployment or production activation.

## M15 policy decisions

- **Approved:** P01-P09 and P11, P13, P14, as described in [the complete policy register](M15_POLICY_RECOMMENDATIONS_20261109.md).
- **P10 approved conditionally:** Atomic M11 history/outbox fail-closed coupling is the chosen behavior. Approval acknowledges that capture failure may block an M11 write. The G15-S13/S14 and G15-S23 results must demonstrate rollback, crash/recovery and retained M11 correctness before the milestone can pass.
- **P12 provisional periods approved:** 90 days for unread/read rows and 30 days for acknowledged rows, subject to later legal/evidence review. No deletion, background retention cleanup, immutable-history disposal, audit truncation or hold removal is authorized. Cleanup remains disabled.
- **P11:** Dispatcher delivery remains production default-off. Temporary activation within disposable, source-locked tests is allowed; no historical backfill without a separate decision.
- All status, security, tenant, session, receipt replay and audit rules from P01-P14 are part of the versioned candidate contract, not aspirational guidance.

## Approved G13 verification envelope

| Measurement | Approved provisional threshold / behavior |
| --- | --- |
| Reference data | At least 170,000 seeded tenant shipments, approximately 450,006 report members |
| Measurement window | At least 30 completed report requests from the same frozen implementation, evidence for all samples |
| p50 | At most 15,000 ms, nearest-rank from measured successful requests |
| p95 | At most 20,000 ms, nearest-rank from measured successful requests |
| Client request deadline | 25 seconds, bounded failure with no partial report |
| PostgreSQL statement deadline | 15 seconds |
| Member capacity | 500,000 stored members per report, fail closed rather than present truncated totals |
| Snapshot expiry | 10 minutes; expired drill returns bounded conflict and cannot silently become a live report |
| Drill page maximum | 100 |
| Concurrency | Separate campaign with 4 authorized reporting principals, exact counts and admission outcomes recorded |
| Storage | Capture actual report/member table and index bytes, growth, temporary disk, cleanup backlog and process memory, then obtain a numerical storage decision |
| Cleanup | Bounded, tenant-scoped expired-report cleanup only; never delete M5-M12 immutable source/history evidence |

These are **verification thresholds**, not an unqualified production SLA. Source-locked failure requires remediation or a separate user-approved budget change, never silent test relaxation. Three-sample historical numbers do not count as the required thirty-request evidence.

## Gate and review status

Policy approval is distinct from application conformance. Both G13-S25 and G15-S25 require independent, evidence-based reviews of the frozen source, all scenario dispositions, raw artifacts and historical failures. A green technical workflow cannot self-approve those reviews or authorize a merge. Any unverified mandatory test remains explicitly open in the final ledger.

**Approval accepted.** G13/G15 milestone acceptance, deployment, production toggles, data deletion and merge not authorized.
