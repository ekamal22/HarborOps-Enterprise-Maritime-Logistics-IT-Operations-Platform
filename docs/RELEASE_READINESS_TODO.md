# Pre-release Documentation and Evidence Checklist

**Turkish interface integration (2026-11-12):** Accepted and merged via [PR #48](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/48) at `c81e54bfef25a0ff957f27b488ab7bd420551244`. Source-locked [run #43199664246](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43199664246) passed 41/41 frontend and 99/99 three-browser checks. G13 conditional and G15 accepted gate dispositions remain unchanged; no G24 release or production rollout is authorized. [Integration acceptance](TURKISH_G15_S25_POSTMERGE_ACCEPTANCE_20261112.md).

**Current G15 acceptance, 2026-11-12:** G15 and G15-S25 are **ACCEPTED** following source-locked run 43178122017 at `ebb28d2332ab6e7eb4264265db3ae35e8fbcbc35`; PR #44 merged the accepted remediation to `main` at `49934d793542ef43cfe587c54b16cfd5cce8e2f4`. Reviewer-separation attestation is an explicitly disclosed procedural exception. P10 fail-closed, P12 deletion disabled, delivery default-off, and no production deployment remain in force. [Acceptance decision](G15_S25_FINAL_ACCEPTANCE_20261112.md) and [post-merge record](G15_S25_POSTMERGE_ACCEPTANCE_RECONCILIATION_20261112.md).




**Opened:** 2026-11-06  
**Target:** M24-03 source/evidence reconciliation before M24-04 accessibility and G24 release acceptance.  
**Status:** OPEN. No final release verification or deployment authorization.

## Mandatory tasks before release

- [ ] Reconcile open [PR #21](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/21) stale-state cleanup, [PR #22](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/22) M10 planning, [PR #24](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/24) historical G16 supplement, and any later document branches against accepted `main`. Mark superseded work explicitly rather than silently merging stale content.
- [ ] On the final clean source SHA execute `node scripts/render-execution-graph.mjs --check`, `node scripts/check-planning-docs.mjs` and applicable `node --test scripts/*.test.mjs`. Preserve commands, exact exits and counts. An earlier documentation-only static check does not substitute for final local execution.
- [ ] Check every repository Markdown relative link, current-state statement, milestone dependency, permission matrix, API route, migration count and source/evidence reference against the final accepted code and source-locked evidence. Keep historical failed and superseded records intact.
- [ ] Reconcile [Status](STATUS.md), [Roadmap](ROADMAP.md), [Decisions](DECISIONS.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [API Contracts](API_CONTRACTS.md), [Security](SECURITY.md), [Operations](OPERATIONS.md), [README](../README.md) and the final release plan with completed milestone closures and the final merge SHA.
- [ ] Verify all pre-release gates and independent reviews, including M10 and future modules, evidence safety, security, dependency/image scans, recovery, performance and M24-04 accessibility. Final G24 acceptance remains separate.
- [ ] Complete the [M17 implementation plan](M17_IMPLEMENTATION_PLAN.md), apply its [nine approved local design policies](M17_POLICY_PROPOSAL_20261112.md), validate and record provisional P02/P04/P09 numerical budgets and P05 local alert calibration, execute and independently reconcile all [75 G17 assertions](M17_G17_ASSERTION_INVENTORY_20261112.json), and explicitly accept G17 before treating M18 as operationally ready. No telemetry service is activated by planning.
- [ ] Explicitly resolve the accepted G9 transport-backlog operating condition: sustained transport-event processing cannot start until approved verified handling for parked `NeedsReconciliation` work prevents exhausting the 10,000 unresolved-work-item limit.
- [ ] Review G16 continuing limits: statement-time audit cursor caveat, privileged migrator boundary, 365-day online retention with separately verified archival, standalone supplemental source attestation and historic Firefox teardown observation.
- [ ] Record an M24-03 closing memo with final source SHA, evidence paths/hashes, completed gates, known limitations, reviewer disposition and acceptance pending items. Do not close G24 on documentation checks alone.

## Workstation controls

Keep controlled worktrees, caches, databases, Compose/WSL data and verification evidence on E:. Use C: only for unavoidable cleaned temporary activity. Preserve existing retained artifacts and databases. GitHub Actions runs require explicit, separate authorization; documentation-only commits use `[skip ci]` and no attribution trailers.

**Boundary:** This checklist is part of existing M24-03, not a new gate or authorization to host or deploy.
