# Pre-release Documentation and Evidence Checklist

**M15 workstream status (2026-11-09):** The [combined M13/M15 expanded run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) and [targeted run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135788) both PASS at combined source `6b05d84bf2f01741b182669c2dd17e39e63d9dda`, which predates newer independent M13 RLS and M15 changes. Latest standalone M15 [42542518598](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42542518598) PASSed source and PostgreSQL, but Chromium passed 5/6 with an ambiguous `Acknowledged` locator; Firefox/WebKit were not reached. The [correctness audit](M15_CORRECTNESS_AUDIT_20261109.md) records fixes at `26c858d39c2b893f45f695cdbee737afed6c0aa3` that still need a full new Actions run. G13/G15 and M15-P01..P14 approvals remain pending. Accepted `main` remains G0-G12 and G16; no merge or deployment.


**Current project state (2026-11-08):** G0-G12 and G16 are accepted and integrated on `main` following [G11/G12 gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) and PR #33. Earlier source-only and gate-pending notes are superseded; retained dated evidence still describes its original run. M9's ARRI/DEPA hold and 10,000-item cap remain mandatory, M10 projector defaults off, ETA null, M11 evaluator opt-in. There is no authorization for deployment or additional GitHub Actions runs.

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

Keep controlled worktrees, caches, databases, Compose/WSL data and verification evidence on E:. Use C: only for unavoidable cleaned temporary activity. Preserve existing retained artifacts and databases. GitHub Actions runs require explicit, separate authorization; documentation-only commits use `[skip ci]` and no attribution trailers.

**Boundary:** This checklist is part of existing M24-03, not a new gate or authorization to host or deploy.
