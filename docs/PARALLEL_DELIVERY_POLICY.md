# Parallel Delivery and Integration Policy

**Version:** 1.1  
**Updated:** 2026-11-04  
**Authority:** [Roadmap](ROADMAP.md), [Execution Graph](EXECUTION_GRAPH.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Local Storage Policy](LOCAL_STORAGE_POLICY.md), [Status](STATUS.md)

## Purpose

HarborOps milestones M0-M24 retain 101 work packages and 25 separately verified, explicitly accepted gates. Milestone numbering and P0-P5 topic groupings do not impose sequential implementation. `execution-graph.json` version 3.0 is the sole dependency-edge authority. The generated Markdown graph must be regenerated after changing dependencies; never hand-edit it.

The accepted baseline is G0-G7 on `main` as of 2026-11-03. A future milestone is **eligible**, not started, merely because its prerequisites are accepted.

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
| M9 worker and reconciliation | Corrected source candidate on draft PR #18; G9 pending | G8 accepted; [M9 local contract](M9_LOCAL_CONTRACT_PROPOSAL.md) acceptance pending despite an isolated, unverified implementation candidate; [M9 plan](M9_IMPLEMENTATION_PLAN.md) documents original shared-file coordination |
| M12 shipment search/export | M12-01 | G5 accepted; scoped search/export remains independently eligible in an isolated branch with no shared EF snapshot or Compose collision |
| M14 route mapping | M14-01 | G4 accepted; map licensing and fallback must be decided before implementation |
| M16 audit foundations | M16-01 | G3 accepted; never weaken existing M1-M3 security |
| M17 observability | M17-01 | G3 accepted; use stable telemetry conventions without coupling to incomplete business tables |
| M19 recovery foundations | M19-01 | G3 accepted; D-008 and safe isolated backups before risky operations |
| M20 security assurance | M20-01 | Baseline review begins after G3; attack matrix waits for G15/G18, scanners for G19 |
| M23 UI system | M23-01 | Visual-system definitions begin after G3; operational polish waits for G12/G13/G14/G15/G18 |

M6 required accepted G4 and G5; its delivery and G6 gate are now accepted and integrated. The parallel migration and shared-file ownership rule remains documented as historical guidance. M8-01 planning and M8-02/M8-03 implementation candidates are committed on `feat/m8-durable-event-inbox`, and were accepted 2026-11-04 after D-006 Option A and independent source-locked G8 review. See [gate closure](M8_G8_GATE_CLOSURE.md). M13 KPI definitions can start after G11; aggregate integration requires G12. M18 incident policy can start after G17; CRUD integration requires G16. M21 can define benchmark workloads after G9 but needs G12/G13 for complete results. M22 may plan deployment after G19 but cannot close until G20/G21 integration. D-003 was accepted for M4. Nothing authorizes bypassing remaining D-004, D-005, D-007 or D-008; D-002 is accepted for the pinned M7 profile.

## M8 verification and independent parallel implementation boundary

The [M8 implementation](M8_IMPLEMENTATION_STATUS.md), [D-006 Option A proposal](M8_D006_DURABILITY_PROPOSAL.md), [second-audit remediation](M8_SECOND_REMEDIATION_STATUS.md) and [G8 matrix](M8_VERIFICATION_PLAN.md) are committed as a single feature-branch candidate. They were source-locked technically verified and accepted after independent review on 2026-11-04. Keep G8 source locked and verify its EF model/migration, transaction/authorization, real PostgreSQL COMMIT interruption and replay, restart-byte continuity and final archive safety. The approved D-006 Option A and G8 acceptance are separately recorded in [gate closure](M8_G8_GATE_CLOSURE.md). A parallel M12 search/export stream may begin from accepted G5, but it must use its own branch and resources and may not modify M8's shared migration or tracking route. M9 starts only after G8 acceptance. No execution-graph dependency changes are proposed.

## M9 worker and retry implementation boundary (unverified feature-branch candidate)

**Current integration boundary:** [Draft PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18) owns the M9 migration, EF snapshot, tracking-work entities, worker DI, tracking operator permissions and handler on its isolated branch. M9-01 contract acceptance and G9 execution are pending; the [source audit](M9_CORRECTNESS_AUDIT_20261104.md) does not authorize integration. Parallel M12, M14, M16, M17 and M19 work must coordinate shared API/authorization files, additive migration ordering, and E:-backed separate PostgreSQL and Docker resources before combined verification.

The [M9 architecture](M9_ARCHITECTURE_DESIGN.md) and [G9 matrix](M9_VERIFICATION_PLAN.md) preserve the accepted G8 immutable receipt/event schema and add a new worker-only state migration with tenant-specific resolution effects, explicit lease fencing and bounded retry/dead-letter/reconciliation. The original plan allowed M9-02 and M9-03 to proceed in parallel only after [M9-01 local contract](M9_LOCAL_CONTRACT_PROPOSAL.md) acceptance. The current candidate was prepared ahead of that decision and must be reconciled to any accepted contract revisions; one integration coordinator controls the EF snapshot, new migration, permissions and worker DI. M12 search/export may run in a different branch from accepted G5, without conflicting migrations or default Compose resources. The 25-case G9 full verification follows M9-04 and requires independent review and explicit G9 acceptance. No changes to the canonical graph dependencies are proposed. [M9 planning status](M9_PLANNING_STATUS.md) records the unimplemented state.

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
- No release, production deployment, new gate acceptance or M10-M24 implementation is implied by updating planning documents. M9 has an unverified feature-branch implementation only. The [Status](STATUS.md) ledger alone records what was actually executed and accepted.

## Graph maintenance

Edit `docs/execution-graph.json`, not the generated view. Preserve milestone/work-package IDs and requirement links unless a separate scoped change is explicitly approved. Regenerate with `node scripts/render-execution-graph.mjs`; check with `node scripts/render-execution-graph.mjs --check` and `node scripts/check-planning-docs.mjs`. Update [Roadmap](ROADMAP.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Implementation Notes](IMPLEMENTATION_PLAN.md), [PRD](PRD.md), [README](../README.md) and [Status](STATUS.md) when policies or dependencies change. The graph describes planning only and may never override observed evidence.
