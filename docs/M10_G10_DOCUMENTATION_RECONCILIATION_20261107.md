# M10/G10 Repository Documentation Reconciliation

**Date:** 2026-11-07  
**Branch:** `fix/m10-g10-l07-l14-remediation`  
**Last runtime-code revision:** `3ef7a50859b5f023aa1457b373728888f6d1cba9`  
**Status:** Documentation reconciled with the committed source candidate. New-source build, PostgreSQL execution and independent supplemental review are NOT EXECUTED. G10 acceptance pending.

## Current authoritative state

1. M0-M9/G0-G9 and M16/G16 are accepted and merged in protected `main`.
2. PR #23 merged the original M10 source before G10 acceptance. Independently reviewed A1-A7 technical results support 23 PASS scenarios on frozen runtime `3e81b884b6a4cc782f84c58510793666786c5796`, with S01 BLOCKED at the time and S25 NOT EXECUTED.
3. M10-L01 through M10-L14 were approved on 2026-11-07 with changes to L07 and L14 and a condition on L06. Policy selection is resolved, but implementation conformance under S01 requires new-source verification.
4. The separate remediation branch contains signed correction authority, private database proof verification, removal of direct API correction INSERT, guarded correction replay and transactionally inserted M16 correction audit evidence. It adds SQL-only migration `20261107120000_M10CorrectionAuthority`, bringing the new-source inventory to thirteen.
5. The remediation source has not been compiled or run against PostgreSQL in this session. It has no new source-locked test result. S25 formal acceptance and any merge/release/deployment decision remain separate and pending.
6. The accepted M9 sustained transport-processing hold and 10,000 unresolved-work cap remain mandatory. M10 currently does not resolve parked ARRI/DEPA transport traffic or compute ETA.

## Living documents reconciled

The following current-state documents were updated to distinguish accepted historical milestones, the approved M10 policy, committed remediation source and unexecuted new-source verification:

- **Project and delivery:** `README.md`, `STATUS.md`, `ROADMAP.md`, `IMPLEMENTATION_PLAN.md`, `PARALLEL_DELIVERY_POLICY.md`, `CONTRIBUTING.md`, `DOCUMENT_STATUS_GUIDE.md`.
- **Product and contracts:** `PRD.md`, `PRODUCT_SCOPE.md`, `REQUIREMENTS.md`, `ACCEPTANCE_CRITERIA.md`, `API_CONTRACTS.md`, `DOMAIN_MODEL.md`, `EVENT_PROCESSING.md`, `DEMO_SCENARIOS.md`.
- **Security and operations:** `SECURITY.md`, `ARCHITECTURE.md`, `OPERATIONS.md`, `COST_AND_DEPLOYMENT.md`, `TEST_STRATEGY.md`, `DECISIONS.md`.
- **M10 and M16 linkage:** `M10_IMPLEMENTATION_STATUS.md`, `M10_EXECUTED_VERIFICATION.md`, `M10_G10_L07_L14_REMEDIATION_PLAN.md`, `M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md`, `M10_M16_SOURCE_RECONCILIATION.md`, `M16_ARCHITECTURE_DESIGN.md`, `M16_AUDIT_PRIVACY_POLICY.md`, `M16_IMPLEMENTATION_STATUS.md`.

Paths in these four groups are relative to `docs/` unless explicitly `README.md`.

## Historical records preserved

The [G10 independent evidence review](M10_G10_INDEPENDENT_EVIDENCE_REVIEW.md) remains a historical review of the twelve-migration frozen source. Its original pass counts, failed attempts, residual WebKit issue and scenario dispositions have not been rewritten as new-source results. The accepted M9 and G16 gate closures, their raw evidence and decision records have not been reclassified. Original M10 source-correctness audits and previous remediation records are historical findings, not statements of current code correctness.

The [M10/M16 source reconciliation](M10_M16_SOURCE_RECONCILIATION.md) now contains a clearly labeled subsequent-source note while retaining its original twelve-migration handoff. The current runtime source and required new test matrix are tracked in [M10 correction authority implementation](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md) and [G10 remediation plan](M10_G10_L07_L14_REMEDIATION_PLAN.md).

## Scope and verification boundary

The source-code baseline is unchanged by this documentation campaign. GitHub's branch comparison from `3ef7a50859b5f023aa1457b373728888f6d1cba9` confirms the reconciliation is limited to `README.md` and files under `docs/`. Protected `main`, executable code, migrations, tests, Docker/Compose configuration and GitHub workflow definitions were not changed during documentation reconciliation.

The prerequisite DAG in `execution-graph.json` is unchanged. No graph regeneration was needed because the M10 policy and corrective implementation do not add an accepted upstream gate or change work-package dependencies.

This documentation update does not claim execution of .NET tests, PostgreSQL migration checks, restricted-role security probes, browser tests, mutations or vulnerability scans. Before changing S01 conformance or S25 acceptance, execute and independently review source-locked supplemental evidence on the new remediation SHA. Preserve raw results and original failures separately from the frozen G10 campaign.

## Operating restrictions

No GitHub Actions execution or enablement, protected-branch merge, public deployment, release, transport-hold release or cleanup of historical evidence is authorized by this reconciliation. Commits include `[skip ci]`. Permanent local project files, Docker/WSL state, caches, databases and verification evidence remain restricted to E: on the Windows verification machine. Unavoidable short-lived C: temporary activity is allowed only within the established storage policy.
