# Pre-release Documentation and Evidence Checklist

**Current verification policy (2026-11-08):** [M11/M12 verification operations](M11_M12_VERIFICATION_OPERATIONS_20261108.md) now requires manual, coordinated, source-locked workflows instead of automatic push-triggered campaigns. [Run-by-run progress](M11_M12_VERIFICATION_PROGRESS_20261108.md) is the current verification checkpoint; older reports and source-only notes below are historical. G11/G12 independent S25 review and acceptance remain pending. Protected `main`, the M9 transport hold, and deployment restrictions are unchanged.

**Combined-source checkpoint (2026-11-08):** The isolated `feat/m11-m12-combined-integration` candidate now contains both M11 and M12 implementations with reconciled shared API, React, SQL role, Compose and EF migration surfaces. [M11 policy decisions](M11_LOCAL_CONTRACT_DECISION_20261108.md) are recorded for the combined candidate; earlier parallel-only and M11-policy-pending statements below describe the previous branches. M12 policy approval, executed G11/G12 source-locked verification, independent S25 reviews and gate acceptance remain pending. The accepted M9 hold and protected `main` remain unchanged.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). The combined-campaign strategy does not close G11, G12 or any release-readiness item. Retain separate policy decisions, raw evidence, independent reviews and the no-release boundary.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records draft M11 PR #31 alongside draft M12 PR #32, with different additive migration IDs and an unresolved shared API/UI/role integration boundary. Both are source candidates, not merged or runtime-verified; G11-S01 BLOCKED, G11-S02..S25 NOT EXECUTED, G12 technical verification pending. Accepted `main` and the M9 transport hold remain unchanged.

**Current M11 source audit (2026-11-08):** [Sixth correctness audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) records correction of inactive severity and same-fingerprint metadata drift, plus typed due-work cursor keys. Tests are authored but unexecuted. G11-S01 BLOCKED, G11-S02 through S25 NOT EXECUTED, acceptance pending; M9 transport processing held and PR #31 unmerged.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). The combined-campaign strategy does not close G11, G12 or any release-readiness item. Retain separate policy decisions, raw evidence, independent reviews and the no-release boundary.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records independent unmerged source candidates in draft PRs #31 and #32. M11 policy approval is blocked, both technical gate campaigns are unexecuted, and acceptance pending. Protected `main` and the M9 transport hold are unchanged.

**Opened:** 2026-11-06  
**Target:** M24-03 source/evidence reconciliation before M24-04 accessibility and G24 release acceptance.  
**Status:** OPEN. No final release verification or deployment authorization.

## Mandatory tasks before release

- [ ] Reconcile open [PR #21](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/21) stale-state cleanup, [PR #22](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/22) M10 planning, [PR #24](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/24) historical G16 supplement, and any later document branches against accepted `main`. Mark superseded work explicitly rather than silently merging stale content.
- [ ] On the final clean source SHA execute `node scripts/render-execution-graph.mjs --check`, `node scripts/check-planning-docs.mjs` and applicable `node --test scripts/*.test.mjs`. Preserve commands, exact exits and counts. An earlier documentation-only static check does not substitute for final local execution.
- [ ] Check every repository Markdown relative link, current-state statement, milestone dependency, permission matrix, API route, migration count and source/evidence reference against the final accepted code and source-locked evidence. Keep historical failed and superseded records intact.
- [ ] Reconcile [Status](STATUS.md), [Roadmap](ROADMAP.md), [Decisions](DECISIONS.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [API Contracts](API_CONTRACTS.md), [Security](SECURITY.md), [Operations](OPERATIONS.md), [README](../README.md) and the final release plan with completed milestone closures and the final merge SHA.
- [ ] Verify all pre-release gates and independent reviews, including M10 and future modules, evidence safety, security, dependency/image scans, recovery, performance and M24-04 accessibility. Final G24 acceptance remains separate.
- [ ] Explicitly resolve the accepted G9 transport-backlog operating condition: sustained transport-event processing cannot start until approved verified handling for parked `NeedsReconciliation` work prevents exhausting the 10,000 unresolved-work-item limit.
- [ ] Review G16 continuing limits: statement-time audit cursor caveat, privileged migrator boundary, 365-day online retention with separately verified archival, standalone supplemental source attestation and historic Firefox teardown observation.
- [ ] Record an M24-03 closing memo with final source SHA, evidence paths/hashes, completed gates, known limitations, reviewer disposition and acceptance pending items. Do not close G24 on documentation checks alone.

## Workstation controls

Keep controlled worktrees, caches, databases, Compose/WSL data and verification evidence on E:. Use C: only for unavoidable cleaned temporary activity. Preserve existing retained artifacts and databases. GitHub Actions must remain disabled; commit with `[skip ci]` and no attribution trailers.

**Boundary:** This checklist is part of existing M24-03, not a new gate or authorization to host or deploy.
