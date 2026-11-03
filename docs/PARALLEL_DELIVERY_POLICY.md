# Parallel Delivery and Integration Policy

**Version:** 1.0  
**Date:** 2026-10-31  
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
| M16 audit foundations | M16-01 | G3 accepted; never weaken existing M1-M3 security |
| M17 observability | M17-01 | G3 accepted; use stable telemetry conventions without coupling to incomplete business tables |
| M19 recovery foundations | M19-01 | G3 accepted; D-008 and safe isolated backups before risky operations |
| M20 security assurance | M20-01 | Baseline review begins after G3; attack matrix waits for G15/G18, scanners for G19 |
| M23 UI system | M23-01 | Visual-system definitions begin after G3; operational polish waits for G12/G13/G14/G15/G18 |

M6 required accepted G4 and G5; its delivery and G6 gate are now accepted and integrated. The parallel migration and shared-file ownership rule remains documented as historical guidance. M8-01 inbox contract work is now eligible after accepted G7; receipt and intake implementation also require accepted G6 and the separate D-006 decision. M13 KPI definitions can start after G11; aggregate integration requires G12. M18 incident policy can start after G17; CRUD integration requires G16. M21 can define benchmark workloads after G9 but needs G12/G13 for complete results. M22 may plan deployment after G19 but cannot close until G20/G21 integration. D-003 was accepted for M4. Nothing authorizes bypassing remaining D-004, D-005, D-006, D-007 or D-008; D-002 is accepted for the pinned M7 profile.

## M8 parallel implementation boundary (planned)

M8-01 contract design is eligible after accepted G7. The [M8 planning package](M8_IMPLEMENTATION_PLAN.md) recommends D-006 Option A but **does not accept** the decision. After an explicit D-006 contract freeze, M8-02 schema and M8-03 transactional route work can progress in separate E:-backed branches from the same accepted baseline. Assign one coordinator for the EF model/migration and one for the M7 tracking endpoint/final bearer revalidation, then integrate both before M8-04 and a single source-locked G8 run. Do not merge the implementation merely because planning passes. The source-scoped uniqueness, lock order and Pending work-state schema must remain compatible with the future M9 worker, with no changed execution-graph dependencies. See [M8 architecture](M8_ARCHITECTURE_DESIGN.md) and [D-006 proposal](M8_D006_DURABILITY_PROPOSAL.md).

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
- No release, production deployment or M8-M24 implementation is implied by updating planning documents. The [Status](STATUS.md) ledger alone records what was actually executed and accepted.

## Graph maintenance

Edit `docs/execution-graph.json`, not the generated view. Preserve milestone/work-package IDs and requirement links unless a separate scoped change is explicitly approved. Regenerate with `node scripts/render-execution-graph.mjs`; check with `node scripts/render-execution-graph.mjs --check` and `node scripts/check-planning-docs.mjs`. Update [Roadmap](ROADMAP.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Implementation Notes](IMPLEMENTATION_PLAN.md), [PRD](PRD.md), [README](../README.md) and [Status](STATUS.md) when policies or dependencies change. The graph describes planning only and may never override observed evidence.
