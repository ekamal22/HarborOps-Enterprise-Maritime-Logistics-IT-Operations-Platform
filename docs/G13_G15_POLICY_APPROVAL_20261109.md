# G13/G15 Approved Policy and Verification Envelope

**Post-c3 G13/G15 acceptance boundary, 2026-11-11:** The approved G13-S20 limit is 250,000 report-member rows, four active reports per tenant, two per user, and 1,000,000 active tenant members. Historical c3 PASS at `4e7b9e2` was not unconditional acceptance: G13 S25 round 3 returned NOT PASS. [Hosted run 43090424080](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43090424080) completed FAILURE at `30fd28f6`; [run 43091966161](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43091966161) had a failed browser job; [run 43093329982](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982) at `067e1ab57f33fcabefae55727265499d86daf7ae` passed preflight, three-engine browser, crash recovery and several other checks while its longer technical jobs and final evidence ledger were pending at inspection. Do not combine passes from different source SHAs, reclassify a failed run as PASS, or treat the planned tests as executed evidence. **G13 and G15 acceptance pending**, no main merge or deployment. Authorized Actions are limited to the disposable post-c3 campaign. Retention deletion remains disabled. See [hosted run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md). Earlier findings, source hashes and test counts below are historical and remain valid only for their own source.

**Final G13/G15 status, 2026-11-10:** [Source-locked run 42745334415](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED all 15 jobs at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543 passed TRX cases and 227/227 technical witnesses. The separate [policy decisions](G13_G15_POLICY_DECISIONS_20261110.md), [S25 reviews](G13_G15_REVIEW_MATRIX_20261110.md) and [formal decisions](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record **G13 NOT ACCEPTED; G15 NOT ACCEPTED**. G13 tenant storage quota/idle cleanup and exact negative mutation/HTTP-outage requirements remain. P10 is technically fulfilled; P12 deletion remains prohibited. Earlier numbers in this page are historical checkpoints, not the newest results. No main merge, deploy or activation.
**2026-11-10 successor checkpoint:** The original design, decision or source review below remains a historical record for its stated date and source SHA. A later [G13/G15 15-job technical campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` with 541 passing TRX cases, but the 242-assertion audit still identifies 3 missing technical witnesses and 15 policy/independent-review items. See [current verification and acceptance status](G13_G15_VERIFIED_EVIDENCE_20261110.md). No main merge, release, activation or deletion authority follows from the technical result.

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
