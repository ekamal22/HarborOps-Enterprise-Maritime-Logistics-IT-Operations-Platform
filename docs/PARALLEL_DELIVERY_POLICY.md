# Parallel Delivery and Integration Policy

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records independent unmerged source candidates in draft PRs #31 and #32. M11 policy approval is blocked, both technical gate campaigns are unexecuted, and acceptance pending. Protected `main` and the M9 transport hold are unchanged.

**Current M11 source audit (2026-11-08):** [Sixth correctness audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) records correction of inactive severity and same-fingerprint metadata drift, plus typed due-work cursor keys. Tests are authored but unexecuted. G11-S01 BLOCKED, G11-S02 through S25 NOT EXECUTED, acceptance pending; M9 transport processing held and PR #31 unmerged.

**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted on protected `main`. The M10 signed-correction remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) following S01/S25 independent review; see [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Earlier G10 pending statements are historical source-locked records. The M9 transport-processing hold remains mandatory. No deployment or release is authorized.


**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted on protected `main`. The M10 signed-correction remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) following S01/S25 independent review; see [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Earlier G10 pending statements are historical source-locked records. The M9 transport-processing hold remains mandatory. No deployment or release is authorized.

**Version:** 1.1  
**Updated:** 2026-11-04  
**Authority:** [Roadmap](ROADMAP.md), [Execution Graph](EXECUTION_GRAPH.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Local Storage Policy](LOCAL_STORAGE_POLICY.md), [Status](STATUS.md)

## Purpose

HarborOps milestones M0-M24 retain 101 work packages and 25 separately verified, explicitly accepted gates. Milestone numbering and P0-P5 topic groupings do not impose sequential implementation. `execution-graph.json` version 3.0 is the sole dependency-edge authority. The generated Markdown graph must be regenerated after changing dependencies; never hand-edit it.

The accepted baseline is G0-G10 plus G16 on `main` as of 2026-11-07. M10's original PR #23 and remediation [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) are merged after S01/S25 verification. Work packages may proceed only when their named technical prerequisites are accepted. Downstream gates, production release and the M9 transport hold remain separately governed.

## Dependency contracts

| Type | Meaning | Delivery rule |
| --- | --- | --- |
| Hard prerequisite | A referenced upstream G gate supplies an authoritative business or platform contract | Upstream gate must be locally verified and explicitly accepted before dependent implementation |
| Integration prerequisite | A later package of a milestone depends on another workstream, while earlier design and contract packages are independent | Independent packages may progress, but the dependent package and gate must wait for upstream acceptance |
| Release prerequisite | Final assembled platform depends on all milestone outcomes and accepted evidence | G24 requires all earlier gates accepted and a new integrated final-system verification |

The dependency is expressed by **work-package ID**. Gate `Gx` does not inherit a dependency on `G(x-1)` unless the graph explicitly specifies it.

## Eligible parallel development after accepted G7

| Stream | First eligible package | Necessary boundary |
| --- | --- | --- |
| M4 voyage/container records | Completed | G4 accepted 2026-11-01; D-003 Option B accepted, implementation integrated |
| M5 shipment lifecycle | Completed | G5 accepted 2026-11-02 and integrated into `main` after independent evidence review |
| M6 shipment assignments | Accepted and merged | G4/G5 prerequisites were accepted; the [twelve-rule M6 contract](M6_LOCAL_CONTRACT_DECISION.md) and full G6 were accepted 2026-11-02, then integrated into `main`. [Gate closure](M6_G6_GATE_CLOSURE.md) preserves evidence provenance and snapshot limitations. |
| M7 tracking source contract | Completed and merged | G7 and D-002 Option A accepted 2026-11-03; see [closure](M7_G7_GATE_CLOSURE.md) |
| M8 durable receipt implementation | G8 accepted and merged 2026-11-04 via PR #14 | G6/G7 accepted; D-006 Option A and independent source-locked G8 evidence reviewed; see [closure](M8_G8_GATE_CLOSURE.md) |
| M9 worker and reconciliation | G9 accepted 2026-11-05, merged | [G9 closure](M9_G9_GATE_CLOSURE.md) records the reviewed evidence and mandatory transport-backlog restriction. M16 integration was separately completed and accepted as G16 on 2026-11-06. |
| M11 delay rules and exceptions | M11-01 through M11-04 source candidate, draft PR #31 | G10 accepted. M11-P01..P12 and G11 approval pending; SQL, API and UI source must be reconciled independently before integration. |
| M12 shipment search/export | M12-01 through M12-04 source candidate, draft PR #32 | G5 accepted. M12-P01..P16 and G12 approval pending; separate migration 14 with a different ID, shared source and Compose integration must be verified. |
| M14 route mapping | M14-01 | G4 accepted; map licensing and fallback must be decided before implementation |
| M16 audit and access hardening | G16 accepted 2026-11-06, merged via PR #25 | [G16 closure](M16_G16_GATE_CLOSURE.md) records reviewed standalone, combined and F001 extension evidence, four accepted integration decisions and residual limits. No deployment authorized. |
| M17 observability | M17-01 | G3 accepted; use stable telemetry conventions without coupling to incomplete business tables |
| M19 recovery foundations | M19-01 | G3 accepted; D-008 and safe isolated backups before risky operations |
| M20 security assurance | M20-01 | Baseline review begins after G3; attack matrix waits for G15/G18, scanners for G19 |
| M23 UI system | M23-01 | Visual-system definitions begin after G3; operational polish waits for G12/G13/G14/G15/G18 |

M6 required accepted G4 and G5; its delivery and G6 gate are now accepted and integrated. The parallel migration and shared-file ownership rule remains documented as historical guidance. M8-01 planning and M8-02/M8-03 implementation candidates are committed on `feat/m8-durable-event-inbox`, and were accepted 2026-11-04 after D-006 Option A and independent source-locked G8 review. See [gate closure](M8_G8_GATE_CLOSURE.md). M13 KPI definitions can start after G11; aggregate integration requires G12. M18 incident policy can start after G17; CRUD integration requires G16. M21 can define benchmark workloads after G9 but needs G12/G13 for complete results. M22 may plan deployment after G19 but cannot close until G20/G21 integration. D-003 was accepted for M4. Nothing authorizes bypassing remaining D-004, D-005, D-007 or D-008; D-002 is accepted for the pinned M7 profile.

## M8 verification and independent parallel implementation boundary

The [M8 implementation](M8_IMPLEMENTATION_STATUS.md), [D-006 Option A proposal](M8_D006_DURABILITY_PROPOSAL.md), [second-audit remediation](M8_SECOND_REMEDIATION_STATUS.md) and [G8 matrix](M8_VERIFICATION_PLAN.md) are committed as a single feature-branch candidate. They were source-locked technically verified and accepted after independent review on 2026-11-04. Keep G8 source locked and verify its EF model/migration, transaction/authorization, real PostgreSQL COMMIT interruption and replay, restart-byte continuity and final archive safety. The approved D-006 Option A and G8 acceptance are separately recorded in [gate closure](M8_G8_GATE_CLOSURE.md). A parallel M12 search/export stream may begin from accepted G5, but it must use its own branch and resources and may not modify M8's shared migration or tracking route. M9 starts only after G8 acceptance. No execution-graph dependency changes are proposed.

## M9 worker and retry implementation boundary (accepted and integrated)

**Current integration boundary:** M9's worker, operator permissions and EF migration are accepted and preserved in merged M16/G16 integration, [PR #25](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/25). The combined snapshot includes audit and worker mappings and 11 migrations. Runtime roles are segregated; the M16 migration-time role provisioner is authoritative for the combined stack. G16's historical source-lock and post-acceptance correction are recorded in [G16 closure](M16_G16_GATE_CLOSURE.md). M10 PR #23 merged into `main` before G10 acceptance. Frozen runtime `3e81b88` has reviewed technical evidence but no G10 gate acceptance. The approved 2026-11-07 security changes are isolated in a separate unmerged branch with no executed new-source tests.

The [M9 architecture](M9_ARCHITECTURE_DESIGN.md) and [G9 matrix](M9_VERIFICATION_PLAN.md) preserve the accepted G8 immutable receipt/event schema and add a new worker-only state migration with tenant-specific resolution effects, explicit lease fencing and bounded retry/dead-letter/reconciliation. The original plan allowed M9-02 and M9-03 to proceed in parallel only after the local contract approval. All six choices are now approved with a [transport-backlog operating condition](M9_LOCAL_CONTRACT_DECISION.md); one integration coordinator must still control the shared EF snapshot, new migration, permissions and worker DI. M12 search/export may run in a different branch from accepted G5, without conflicting migrations or default Compose resources. The 25-case G9 verification, independent review and explicit acceptance are complete; see [G9 closure](M9_G9_GATE_CLOSURE.md). No changes to the canonical graph dependencies are proposed. [M9 planning status](M9_PLANNING_STATUS.md) preserves the earlier unimplemented planning state as history.

## M9/M16 convergence rule

Completed convergence: M9 `TrackingWorkRead` and `TrackingWorkRedrive` coexist with M16 `ViewAuditEvents`; both endpoint families and additive audit/worker migrations are integrated in one EF snapshot. The four approved Option A decisions establish immutable M9 work history as its audit of record, a single migration-time provisioning authority, the documented privileged migrator and protected API-role guard triggers. The combined gate and F001 extension passed independently reviewed source-locked verification. Future shared migrations still require fresh and populated-baseline compatibility checks.

## Branches and source of truth

1. Use one named feature branch or worktree per concurrently developed milestone, based on an accepted `main` revision. Record the exact base SHA and upstream contract versions.
2. Do not allow parallel branches to independently redefine authentication, organization roles, tenant ownership, error envelopes, concurrency semantics, shared UI contracts, or API route identities. Agree and version affected contracts before editing dependent code.
3. Assign one migration coordinator for a shared PostgreSQL target. Branches may prototype separately against disposable databases, but the combined migration sequence must be additive, conflict-free, reproducible from fresh and historical accepted schemas and tested after integration.
4. Isolate all controllable build/test output, SDK and browser caches, Docker data, and evidence under E:. Never reuse or modify the only retained primary database or volume.
5. Use `[skip ci]` for every commit while GitHub Actions remain prohibited. Do not dispatch, rerun or indirectly start Actions. Do not add automated authorship or co-author trailers. Do not merge unrelated unfinished work under another milestone's gate.
6. Only an explicitly accepted gate may become a new integration baseline on `main`. A passing partial suite, completed code, or a gate marked verified but acceptance pending is not enough.
7. Cross-track integration requires review of interfaces, database and migration order, routing, authorization, frontend state, operational scripts, and corresponding documentation before any merge.

## Verification and gate acceptance

- Each gate retains its full established positive/negative, security, tenant-isolation, migration, recovery and browser evidence obligations. Gate numbers are identifiers, not execution slots.
- Verify the integrated source revision and retain commands, fixtures, results, timestamps, tool versions, and missing/blocked cases. Existing accepted M0-M7 regressions remain mandatory where a change touches those contracts.
- When parallel branches converge, run their combined tests on the merged candidate, including auth/tenant boundary races and migration rehearsals. Earlier per-branch PASS results alone do not establish integration correctness.
- G19 recovery can be implemented early, but any later migration requires appropriate upgrade/recovery testing. G20 assurance similarly does not waive security checks on later modules.
- G24 requires all G0-G23 gates explicitly accepted and a fresh complete-system verification on the final code and images. M24-04 remains the **only formal dedicated accessibility audit**; M6, M12, M18, and M24-03 retain historical-volume compatibility checkpoints. These milestone-bound checks remain required even if streams finish out of numeric order.
- No release or production deployment follows from G10 acceptance. G0-G10 and G16 are merged, while G11-G15 and G17-G24 remain open. The M9 unresolved-work cap and parked transport restrictions remain mandatory. [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md).

## Graph maintenance

Edit `docs/execution-graph.json`, not the generated view. Preserve milestone/work-package IDs and requirement links unless a separate scoped change is explicitly approved. Regenerate with `node scripts/render-execution-graph.mjs`; check with `node scripts/render-execution-graph.mjs --check` and `node scripts/check-planning-docs.mjs`. Update [Roadmap](ROADMAP.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Implementation Notes](IMPLEMENTATION_PLAN.md), [PRD](PRD.md), [README](../README.md) and [Status](STATUS.md) when policies or dependencies change. The graph describes planning only and may never override observed evidence.
