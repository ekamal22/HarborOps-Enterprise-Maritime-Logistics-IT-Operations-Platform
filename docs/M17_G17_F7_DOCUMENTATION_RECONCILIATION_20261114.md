# G17 f7 documentation reconciliation record

**Date:** 2026-11-14  
**Document-only baseline:** `7b3d3e87a62c03ba6a7ce7d8d92b93bf7c3c78a7`  
**Evidence source:** `5b1dcc41d5b19dcbacd66de38193dc5020964647`, local-only and not represented by the executable contents of this documentation branch.  
**Gate:** G17 NOT ACCEPTED; independent S25 acceptance pending.

[Current source-locked f7 checkpoint](M17_G17_F7_S25_CURRENT_CHECKPOINT_20261114.md) is the active cross-repository evidence status. Older hosted campaign records, including their original FAILED and PASS outcomes, remain historical and must not be relabeled as f7. The local f1 through f6 attempts remain failed. Historical technical green results apply only to their original source SHA.

## Reconciled current-facing files

- `README.md`
- `docs/STATUS.md` (also updated M17 status row)
- `docs/ROADMAP.md` (also corrected outdated G8 and G13 milestone exit wording and clarified G17)
- `docs/EXECUTION_GRAPH.md`
- `docs/M17_G17_S25_OPEN_FINDINGS_REGISTER_20261114.md` (preserved original findings and linked later f7 dispositions)
- `docs/M17_G17_S25_INDEPENDENT_EVIDENCE_REVIEW_20261114.md`
- `docs/M17_G17_S25_CANDIDATE_AND_REVIEW_PACKET_20261114.md`
- `docs/M17_G17_TECHNICAL_VERIFICATION_GREEN_20261114.md`
- `docs/M17_G17_REPOWIDE_DOCUMENTATION_RECONCILIATION_20261114.md`
- `docs/M17_PLANNING_STATUS.md` (clarified the later local technical pass)
- `docs/M17_IMPLEMENTATION_PLAN.md`
- `docs/M17_OPERATIONS_RUNBOOK.md`
- `docs/M17_VERIFICATION_PLAN.md`
- `docs/M17_POLICY_PROPOSAL_20261112.md`
- `docs/M17_ARCHITECTURE_DESIGN.md`
- `docs/M17_IMPLEMENTATION_CANDIDATE_STATUS_20261112.md`
- `docs/TEST_STRATEGY.md`
- `docs/OPERATIONS.md`
- `docs/SECURITY.md`
- `docs/CONTRIBUTING.md`
- `docs/DOCUMENT_STATUS_GUIDE.md`
- `docs/DECISIONS.md`
- `docs/ACCEPTANCE_CRITERIA.md`
- `docs/ARCHITECTURE.md`
- `docs/REQUIREMENTS.md`
- `docs/PRD.md`
- `docs/PARALLEL_DELIVERY_POLICY.md`

## Unchanged obligations and source preservation

- The same approved P09 10% latency, 15% memory budgets and three-pair pooled method remain in force. f6 failed; f7 passed narrowly; no synthetic acceptance or retry-until-green claim was added.
- Full source review of `5b1dcc41d5b19dcbacd66de38193dc5020964647`, F07 positive effect-once proof, f6 variance, material mutation survivors and a genuinely separate signed G17-S25 review remain open.
- No source file, workflow, application component, migration, database role, branch protection, production configuration, retained evidence or accepted milestone decision was changed by this documentation-only update.
- The previously failing planning-document checker requires a separate code/test correction; it was not executed or passed here. This is not a fresh end-to-end full repository test.
- GitHub Actions remains forbidden for the local-only remediation task. M18/G18 remains blocked on G17 acceptance. No merge to protected main is implied.

This record replaces no historical approval, review signature, artifact or failed execution. The current f7 ZIP and manifest digests are in the linked checkpoint.

