# M9 Planning Status

**Final G9 status (2026-11-05):** G9 accepted with the mandatory [M9-01 transport-backlog condition](M9_LOCAL_CONTRACT_DECISION.md); planning PR #16 and implementation PR #18 merged into `main` as `43fe326` and `4684352`. See [G9 gate closure](M9_G9_GATE_CLOSURE.md) and [evidence review](M9_G9_INDEPENDENT_EVIDENCE_REVIEW.md). Historical source-locked result classifications and earlier proposals below remain unaltered. No production deployment is authorized.

**Subsequent decision and execution:** The original planning and acceptance-pending text below is historical. The later [M9-01 decision](M9_LOCAL_CONTRACT_DECISION.md) approves all six choices with the transport-backlog condition; [executed verification](M9_EXECUTED_VERIFICATION.md) and the [browser supplement](M9_BROWSER_SUPPLEMENT.md) passed on source `0abeeb0`. G9-S25 formal review and gate acceptance remain pending.

**Historical planning record:** Subsequent M9 source implementation is separately tracked in [implementation status](M9_IMPLEMENTATION_STATUS.md) and the [correctness audit](M9_CORRECTNESS_AUDIT_20261104.md). This original planning record is retained, not promoted to executed verification. M9-01 contract and G9 acceptance pending.

**Historical snapshot:** This records the initial M9 planning phase. Later source implementation on `feat/m9-worker-reconciliation` is separately documented in [M9 implementation status](M9_IMPLEMENTATION_STATUS.md). It is unverified, and the original M9-01 contract remains acceptance pending.

**Prepared:** 2026-11-04  
**Planning branch:** `plan/m9-idempotent-worker-reconciliation`  
**Accepted baseline:** protected `main` at `7b9e19d730e3a09c5e1184262460dfeac9319e3d`, G0-G8 accepted.  
**M9 state:** Architecture, implementation plan, local contract proposal and G9 verification specification committed. **No M9 application/worker/migration/test implementation was performed.**  
**M9-01 contract:** Acceptance pending.  
**G9:** Not executed, no evidence, no acceptance.  
**Integration:** No changes to `main`. GitHub Actions disabled.

## Planning deliverables

- [M9 architecture](M9_ARCHITECTURE_DESIGN.md): worker lease fencing, tenant-scoped reference resolution, immutable effect ledger, state transition guards, retry/backoff and DB atomicity.
- [M9 local contract proposal](M9_LOCAL_CONTRACT_PROPOSAL.md): six decisions to freeze before implementation, including the unprocessed-work interpretation of the accepted 10,000 Pending admission limit, operator permissions and unknown transport identifiers.
- [M9 implementation plan](M9_IMPLEMENTATION_PLAN.md): exact M9-01 through M9-04 scope, impacted source files, safe parallel ownership, staged efficiency and integration requirements.
- [G9 verification plan](M9_VERIFICATION_PLAN.md): 25 gates, two-worker concurrency, lease expiry, source-specific failures, real PostgreSQL COMMIT uncertainty, targeted mutations, M0-G8 regression and independent review.

## Architecture decisions that prevent rework

1. Do **not** change M8's receipt, accepted event or conflict rows. M9 uses a new additive migration to replace the Pending-only work guard and add work/attempt/effect metadata.
2. Do **not** treat lease ownership as a process-local flag. Claim and finalization require PostgreSQL-held short transactions, token and epoch fencing, and a unique effect key.
3. Do **not** process an unknown or ambiguous reference as a resolved shipment. In particular, M7 transportReference has no proven voyage mapping. Freeze an explicit mapping or choose NeedsReconciliation.
4. Do **not** free backlog capacity merely by moving Pending to Leased. Count all nonterminal unresolved states at the same organization lock used by accepted M8 intake.
5. Do **not** implement M10 projections, shipment workflow mutations, notifications, full incident handling or production hosting under G9.
6. Do **not** begin M9 shared persistence/permission implementation until the local M9-01 state/capacity/resolution contract is accepted.

## Next steps

1. Review and explicitly approve or amend [M9 local contract](M9_LOCAL_CONTRACT_PROPOSAL.md). This is a project-local design freeze, not a new numbered global D decision.
2. Implement M9-02 schema/claimer and M9-03 retry/reconciliation on isolated branches with a single integration coordinator for EF migrations and shared permissions.
3. Integrate on the feature branch, execute the entire [G9 verification plan](M9_VERIFICATION_PLAN.md) against disposable E:-backed services, independently review evidence, then request explicit G9 acceptance before any merge.

The accepted G8 evidence and the original G0-G8 records remain unchanged. Planning approval does not authorize implementing M10, merging M9, using GitHub Actions or persistent storage on C:.

**Historical disposition:** M9 planning was ready for review when this record was created. The later M9/G9 acceptance and merge are in [gate closure](M9_G9_GATE_CLOSURE.md).
