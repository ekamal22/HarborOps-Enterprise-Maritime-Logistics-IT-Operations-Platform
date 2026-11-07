# Documentation Status Guide

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records separate unmerged source candidates in draft PRs #31 and #32. M11 policy approval is blocked, both technical gate campaigns are unexecuted, and acceptance pending. Protected `main` and the M9 transport hold are unchanged.

**M12 candidate status (2026-11-08):** [Draft PR #32](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/32) contains scoped shipment search, bounded signed CSV export and source-level correctness repairs. [Latest audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md), [implementation status](M12_IMPLEMENTATION_STATUS.md) and [G12 plan](M12_VERIFICATION_PLAN.md) distinguish authored tests from executed evidence. The fifth pass adds exclusive PostgreSQL role-test gating and CSV cancellation checks. Earlier source fixes remain in force. G12 execution and independent acceptance remain pending. Protected `main` and the M9 transport hold are unchanged.

**Current integrated baseline (2026-11-07):** M0-M10/G0-G10 and M16/G16 are accepted and integrated into protected `main`. Original M10 PR #23 plus remediation [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) at `9b6c801fde4004a4c0021ec7a6296efe28cf1544` complete the G10 integration. G11-G15 and G17-G24 remain unaccepted. The [G10 closure](M10_G10_GATE_CLOSURE_20261107.md) and [project status](STATUS.md) are authoritative for current decisions.
**Current documentation reconciliation:** [G10 post-merge record](M10_G10_POST_MERGE_RECONCILIATION_20261107.md) indexes the current-state changes without rewriting historical evidence reports.


**Current integrated baseline (2026-11-07):** M0-M10/G0-G10 and M16/G16 are accepted and integrated into protected `main`. Original M10 PR #23 plus remediation [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) at `9b6c801fde4004a4c0021ec7a6296efe28cf1544` complete the G10 integration. G11-G15 and G17-G24 remain unaccepted. The [G10 closure](M10_G10_GATE_CLOSURE_20261107.md) and [project status](STATUS.md) are authoritative for current decisions.

For the 2026-11-07 M10 policy and source reconciliation, see [G10 documentation reconciliation](M10_G10_DOCUMENTATION_RECONCILIATION_20261107.md). Earlier source-locked reports preserve their original evidence and dispositions; this reconciliation does not replace any of those results.

## Sources of truth

- [Status](STATUS.md) records observed implementation, source-locked verification, acceptance and integration.
- [PRD](PRD.md), [Requirements](REQUIREMENTS.md) and [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) define product goals, requirements and gate conditions.
- [Roadmap](ROADMAP.md) defines work packages. [Execution graph JSON](execution-graph.json) is authoritative for dependency edges; the rendered [Execution Graph](EXECUTION_GRAPH.md) is a planning view rather than an acceptance ledger.
- [Decisions](DECISIONS.md), [API Contracts](API_CONTRACTS.md), [Security](SECURITY.md), [Operations](OPERATIONS.md) and [Domain Model](DOMAIN_MODEL.md) distinguish accepted behavior from proposed work.

## Historical records

Source-locked plans, proposal files, audit reports and executed-verification registers preserve their original time-specific dispositions. Words like `BLOCKED`, `NOT EXECUTED` or `acceptance pending` can be correct for their initial runs even when later separate gate decisions closed the milestone. Do not silently rewrite failed attempts, original SHA associations, historical risk findings or decision-time records. Consult [G8 closure](M8_G8_GATE_CLOSURE.md), [G9 closure](M9_G9_GATE_CLOSURE.md) and [G16 closure](M16_G16_GATE_CLOSURE.md) for later accepted dispositions.

## Currently unaccepted work and restrictions

- M10 planning [PR #22](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/22) and original source [PR #23](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/23) preceded G10 acceptance. The original frozen 23 PASS / S01 BLOCKED / S25 NOT EXECUTED evidence remains historical. On 2026-11-07 S01/S25 were closed through separately verified remediation and review, and [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) integrated the accepted source. See [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md).
- Older open documentation [PR #21](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/21) and G16 supplement [PR #24](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/24) may contain superseded current-state wording. Their historical content must be reconciled before any merge and is not a replacement for current `main`.
- M9's mandatory hold on sustained transport-event processing remains until separately approved and verified backlog handling prevents exceeding the 10,000 unresolved-work-item cap. M16 audit retention is 365 days online followed by separately approved archival, not automatic purge.
- The repository is public for inspection, but hosting, production deployment and G24 release are not authorized. GitHub Actions remains disabled.

[Pre-release Documentation Checklist](RELEASE_READINESS_TODO.md) tracks the final M24-03 evidence and documentation audit. Planning documents cannot grant an unaccepted gate.
