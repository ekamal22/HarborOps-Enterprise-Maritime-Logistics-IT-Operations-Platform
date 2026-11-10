# G13/G15 Independent Scenario Disposition Matrix

**Review date:** 2026-11-10
**Reviewed implementation SHA:** `15cabd8c542159753c927c2bd65b4346223e84f6`
**Technical source-locked run:** [42745334415](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415), 15/15 success.
**Review result:** G13-S25 and G15-S25 NOT PASS. The source-locked script verifies candidate witnesses, not the complete acceptance contract of each of 48 prior scenarios.

## Assessment method

Read back 15 retained ZIP archives, verified all 169 internal SHA-256 manifest entries, parsed 543 passing TRX executions, verified source markers, reviewed eight actual process interruption records, and reconciled the executed 242-assertion audit with targeted source and mutation witnesses. The matrix below preserves the conservative full-scenario ledger while documenting actual candidate coverage and review exceptions. No pass is inferred from a test name alone. Independent external reviewer signoff is not represented by this source review.

| Scenario | Gate disposition | Automated assertions witnessed | Review finding |
| --- | --- | ---: | --- |
| G13-S01 | **BLOCKED** | 3/3 | Permanent numerical storage and idle-cleanup policy lacks source enforcement |
| G13-S02 | **PASS** | 4/4 | Release build, contract tests and source compatibility verified |
| G13-S03 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S04 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S05 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S06 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S07 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S08 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S09 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S10 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S11 | **BLOCKED** | 6/6 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S12 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S13 | **BLOCKED** | 6/6 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S14 | **BLOCKED** | 6/6 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S15 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S16 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S17 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S18 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S19 | **BLOCKED** | 6/6 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S20 | **BLOCKED** | 6/6 | Measured query and latency evidence passes; permanent tenant quota and cleanup still absent |
| G13-S21 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S22 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S23 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G13-S24 | **BLOCKED** | 6/6 | Missing dedicated killed revocation-bypass and rollback mutants; strict edge and unknown-value mutant equivalence not fully established |
| G13-S25 | **NOT PASS** | 0/0 | Separate source/evidence assessment completed as NOT PASS; independent signoff unavailable |
| G15-S01 | **BLOCKED** | 2/2 | P10 evidence reconciled and P12 remains restricted; full S25 and S21/S24 still block gate |
| G15-S02 | **PASS** | 4/4 | Release build, contract tests and source compatibility verified |
| G15-S03 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S04 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S05 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S06 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S07 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S08 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S09 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S10 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S11 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S12 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S13 | **BLOCKED** | 5/5 | Eight exact-SHA crash phases and recovery are verified; technical criteria supported |
| G15-S14 | **BLOCKED** | 3/3 | Fail-closed M11 history/outbox rollback is verified; P10 technical condition fulfilled |
| G15-S15 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S16 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S17 | **BLOCKED** | 6/6 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S18 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S19 | **BLOCKED** | 4/4 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S20 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S21 | **BLOCKED** | 5/5 | Actual HTTP dependency outage and bounded scoped failure path not fully proved |
| G15-S22 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S23 | **BLOCKED** | 5/5 | All mapped technical witnesses passed, but test presence alone does not certify each complete scenario's behavior; retain acceptance BLOCKED pending assertion-specific reviewer signoff |
| G15-S24 | **BLOCKED** | 5/5 | No dedicated killed source-history uniqueness mutant; recipient uniqueness is not the same constraint |
| G15-S25 | **NOT PASS** | 0/0 | Separate source/evidence assessment completed as NOT PASS; independent signoff unavailable |

## Mutation evidence precision

**G13 exact executed killers:** `tenant-materialization-scope-removed`, `auditor-sql-grant-bypass`, `k03-duplicate-status-history`, `correction-generation-stale-accepted`, `origin-member-filter-ignored`, `malformed-kpi-member-cursor-admitted`, and the other reported KPI/security mutants were killed. The manifest does not independently show a named revocation-bypass or report-rollback mutant. `k06-remove-tolerance` is not automatically equivalent to an inclusive 60-minute boundary mutant, and `unassessed-suppress-missing` is not automatically equivalent to forcing unknown data to zero.

**G15 exact executed killers:** `personal-read-policy-bypassed`, `membership-version-revocation-ignored`, `receipt-replay-session-unbound`, `dispatch-scope-proof-bypassed`, `signed-recipient-scope-unbound`, `receipt-cas-bypassed`, `source-intent-failure-swallowed`, `receipt-audit-skipped`, `receipt-recipient-uniqueness-removed`, and `history-action-filter-removed`. `receipt-recipient-uniqueness-removed` is not a source-history/outbox uniqueness mutation.

## Outstanding acceptance requirements

1. Verify and enforce the selected G13 tenant report-storage quota and bounded idle cleanup in real restricted-role PostgreSQL tests.
2. Supply exact mutation-to-assertion evidence for each remaining G13-S24 and G15-S24 mandatory negative variant.
3. Verify G15-S21 dependency-outage HTTP responses at the actual API/SQL boundary, preserving tenant isolation and bounded failure.
4. Finish independent scenario-level review and record explicit PASS decisions only for claims fully substantiated by source and raw evidence.
5. Retain the historical failed workflows and reconcile them to their own source SHAs; none are retroactively accepted.

**Disposition:** Technical campaign PASS; full G13/G15 acceptance NOT PASS. No merge or production rollout authority.
