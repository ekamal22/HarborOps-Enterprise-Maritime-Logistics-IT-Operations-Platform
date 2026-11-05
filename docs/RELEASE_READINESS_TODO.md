# Pre-release Documentation Reconciliation Checklist

**Created:** 2026-11-05  
**Scope:** M24-03 documentation and evidence audit, completed before the final M24-04 assessment and any G24 acceptance.  
**Status:** OPEN. This checklist records pending work, not completed verification or deployment approval.

## Open mandatory tasks

- [ ] Review and complete [PR #21](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/21), the follow-up reconciliation of stale current-state documentation after [merged PR #20](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/20). If PR #21 is superseded, document the replacing commit and reconciliation instead. Do not imply that PR #21 was merged when it remains open.
- [ ] Rebase or otherwise reconcile every subsequent documentation change against the accepted protected `main`, including later milestones, before the final release documentation freeze.
- [ ] On a clean, exact local revision run `node scripts/check-planning-docs.mjs`, `node scripts/render-execution-graph.mjs --check`, and `node --test scripts/*.test.mjs`. Record the exact SHA, commands, exit codes, counts and any corrections. Earlier checks on PR #20 or focused checks on PR #21 do not replace a final-branch run.
- [ ] Audit all repository Markdown, not only top-level documents, for broken **relative** links, obsolete current-state acceptance/merge language, contradictory permissions and route claims, stale milestone prerequisites, dead references and mistaken release claims. Preserve time-stamped historical planning, audit and evidence observations. Explicitly distinguish old execution-time `BLOCKED` records from later gate-closure decisions.
- [ ] Compare `docs/STATUS.md`, `docs/ROADMAP.md`, `docs/ACCEPTANCE_CRITERIA.md`, `docs/DECISIONS.md`, `docs/API_CONTRACTS.md`, `docs/OPERATIONS.md`, `docs/SECURITY.md`, `README.md` and any milestone-specific current documents against accepted source and verification at the release SHA. Resolve every unexplained discrepancy.
- [ ] Check that the generated [execution graph](EXECUTION_GRAPH.md) matches canonical [execution-graph.json](execution-graph.json); do not edit generated graph output or change dependency edges merely to satisfy a test.
- [ ] Confirm all outstanding release conditions have independent dispositions, including the [M9 transport-event hold](M9_LOCAL_CONTRACT_DECISION.md), M16/G16 supplemental evidence and combined M9/M16 verification, applicable security/recovery/performance gates and deployment limitations. A documentation check never grants gate acceptance.
- [ ] Record a dated M24-03 completion note with the exact tested SHA, reviewer disposition, remaining risks and links to evidence. Any unresolved blocking item leaves G24 acceptance pending.

## Workstation and evidence policy

Use isolated E:-backed repository worktrees, caches, test output, Docker/WSL data and evidence in compliance with [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Do not write persistent project data to C: or disturb retained databases, worktrees and historical raw evidence. No GitHub Actions execution or enabling. Commits carry `[skip ci]` and contain no attribution trailers.

**Release boundary:** This task is an additional documentation-readiness checkpoint within M24-03, not a new milestone, not a modification of the version 3.0 work-package graph, and not an authorization to host or deploy the application.
