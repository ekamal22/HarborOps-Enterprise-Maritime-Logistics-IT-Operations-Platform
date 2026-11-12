# Documentation Status Guide

**Current integration state (2026-11-12):** G0-G12 and G16 are accepted and integrated into protected `main`. G13 is conditionally accepted and merged through PR #38 at `b2856449c8d1ab629c8b5419a5af0ebddafbb47d`, with formal reviewer independence not attested. G15 is not accepted or merged. M9 transport controls, default-off services, no retention deletion and no deployment remain in force. See [G13 post-merge reconciliation](G13_POSTMERGE_RECONCILIATION_20261112.md).

The current G13 disposition is recorded in [the post-merge reconciliation](G13_POSTMERGE_RECONCILIATION_20261112.md). Earlier dated review findings and run checkpoints remain source-specific historical records.

## Sources of truth

- [Status](STATUS.md) records observed implementation, source-locked verification, acceptance and integration.
- [PRD](PRD.md), [Requirements](REQUIREMENTS.md) and [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) define product goals, requirements and gate conditions.
- [Roadmap](ROADMAP.md) defines work packages. [Execution graph JSON](execution-graph.json) is authoritative for dependency edges; the rendered [Execution Graph](EXECUTION_GRAPH.md) is a planning view rather than an acceptance ledger.
- [Decisions](DECISIONS.md), [API Contracts](API_CONTRACTS.md), [Security](SECURITY.md), [Operations](OPERATIONS.md) and [Domain Model](DOMAIN_MODEL.md) distinguish accepted behavior from proposed work.

- [Repository-wide reconciliation](REPOWIDE_DOCUMENTATION_RECONCILIATION_20261108.md) indexes the 2026-11-08 review of current-state documentation, generated graph consistency and relative file links.
- M11 and M12 are accepted on `main` following [formal G11/G12 gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) and [PR #33](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/33). Their original draft PRs #31 and #32 are historical source branches, not unverified current implementation.

## Historical records

Source-locked plans, proposal files, audit reports and executed-verification registers preserve their original time-specific dispositions. Words like `BLOCKED`, `NOT EXECUTED` or `acceptance pending` can be correct for their initial runs even when later separate gate decisions closed the milestone. Do not silently rewrite failed attempts, original SHA associations, historical risk findings or decision-time records. Consult [G8 closure](M8_G8_GATE_CLOSURE.md), [G9 closure](M9_G9_GATE_CLOSURE.md), [G16 closure](M16_G16_GATE_CLOSURE.md) and [G11/G12 closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) for later accepted dispositions. Removed redundant status banners remain retrievable in the repository history at `ef6ce8b6c00984b0d7ce45bffca6f9b9e66fd8a3`; dated source-locked evidence reports are preserved.

## Current unaccepted milestones and restrictions

G13-G15 and G17-G24 remain unaccepted. Their work is subject to the existing execution graph, policy approvals and gate evidence.

- M10 planning [PR #22](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/22) and original source [PR #23](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/23) preceded G10 acceptance. The original frozen 23 PASS / S01 BLOCKED / S25 NOT EXECUTED evidence remains historical. On 2026-11-07 S01/S25 were closed through separately verified remediation and review, and [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) integrated the accepted source. See [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md).
- Older open documentation [PR #21](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/21) and G16 supplement [PR #24](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/24) may contain superseded current-state wording. Their historical content must be reconciled before any merge and is not a replacement for current `main`.
- M9's mandatory hold on sustained transport-event processing remains until separately approved and verified backlog handling prevents exceeding the 10,000 unresolved-work-item cap. M16 audit retention is 365 days online followed by separately approved archival, not automatic purge.
- The repository is public for inspection, but hosting, production deployment and G24 release are not authorized. GitHub Actions is not generally authorized; the previously approved manual N01 verification is retained as evidence, and new runs require separate authorization.

[Pre-release Documentation Checklist](RELEASE_READINESS_TODO.md) tracks the final M24-03 evidence and documentation audit. Planning documents cannot grant an unaccepted gate.
