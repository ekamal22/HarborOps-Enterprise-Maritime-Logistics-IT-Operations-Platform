# Documentation Status Guide

**Current baseline:** 2026-11-05, following [PR #20](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/20). M0-M9/G0-G9 are accepted and integrated into protected `main`. G10-G24 remain unaccepted.

## Current sources of truth

- [Project Status](STATUS.md) is authoritative for implementation state, executed evidence, acceptance and integration.
- [Product Requirements Document](PRD.md) and [Requirements](REQUIREMENTS.md) define product scope and atomic requirements.
- [Roadmap](ROADMAP.md) assigns milestone gates; [execution-graph.json](execution-graph.json) is the canonical technical dependency DAG. The generated [Execution Graph](EXECUTION_GRAPH.md) expresses planned evidence requirements, **not live acceptance status**.
- [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Decisions](DECISIONS.md), [API Contracts](API_CONTRACTS.md), [Domain Model](DOMAIN_MODEL.md), [Security](SECURITY.md) and [Operations](OPERATIONS.md) describe accepted contracts alongside clearly identified future work.

## Historical milestone records

Dated planning documents, initial proposals, correctness audits, remediation registers, source-locked verification plans and executed evidence describe **their original point in time**. Terms such as `candidate`, `BLOCKED`, `NOT EXECUTED`, `G8 acceptance pending` or `G9 not merged` in those records can be correct as historical observations. Do not silently rewrite old evidence or replace a previously recorded decision with a later outcome.

For definitive later dispositions consult [G7 closure](M7_G7_GATE_CLOSURE.md), [G8 closure](M8_G8_GATE_CLOSURE.md) and [G9 closure](M9_G9_GATE_CLOSURE.md). In particular, old G8/G9 verification tables retain execution-time blocked or unexecuted scenarios even though their gates were later accepted following separately documented decisions and reviews.

## Outstanding restrictions and review status

- Accepted M9 includes an opt-in worker, but **sustained transport-event processing is held** until verified M10 or separately approved handling of parked `NeedsReconciliation` work prevents exhausting the 10,000 unresolved-item cap. No public or production deployment is authorized.
- M16 is a standalone, unmerged implementation candidate. The [G16 main-evidence review](M16_G16_INDEPENDENT_EVIDENCE_REVIEW.md) checked 162 files, but **`supplement-s07-s08-s28.log` remains UNREVIEWED**. Combined M9/M16 migrations, permission compatibility, browser/outage checks, G16 acceptance and merge authorization remain pending.
- Planning and documentation updates do not count as milestone runtime verification or acceptance. No GitHub Actions runs are authorized.

When a current-state discrepancy appears, correct the living document and preserve the source-locked historical record. Use [Project Status](STATUS.md) for the latest accepted baseline.
