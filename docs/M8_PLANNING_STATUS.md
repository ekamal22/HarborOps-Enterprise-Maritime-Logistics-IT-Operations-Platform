# M8 Planning Status

**As of:** 2026-11-03
**State:** Planning package completed; an M8 implementation candidate now exists on `feat/m8-durable-event-inbox`. **D-006 acceptance pending and G8 not executed.**
**Branch:** `plan/m8-durable-inbox-immutable-history`.
**Accepted base:** Protected `main` `47f030f9fa4b588d589f3ae082d03b3d5b94736a`, M0/G0 through M7/G7 accepted.
**D-006:** Option A recommended; **acceptance pending**.
**M8 implementation:** Source candidate committed on `feat/m8-durable-event-inbox`; see [implementation status](M8_IMPLEMENTATION_STATUS.md).
**G8:** Not executed; acceptance pending.
**Runtime/deployment:** Protected `main` remains accepted M7. The candidate changes valid-intake behavior only in its feature branch; there is no verified runtime, public hosting or GitHub Actions authorization.

## Planning deliverables

- [M8 architecture](M8_ARCHITECTURE_DESIGN.md): PostgreSQL receipt transaction, immutable history, source-scoped replay keys, final credential revalidation, quota and M9 handoff.
- [D-006 decision proposal](M8_D006_DURABILITY_PROPOSAL.md): alternatives, recommended Option A, acknowledgment and retention decisions awaiting acceptance.
- [M8 implementation plan](M8_IMPLEMENTATION_PLAN.md): M8-01 through M8-04 paths, contracts, sequencing, parallel boundaries and safe stop rules.
- [G8 verification plan](M8_VERIFICATION_PLAN.md): 25 mandatory executable cases, failure injection, security mutations and independent evidence.
- Existing [execution graph](execution-graph.json) already expresses M8 package prerequisites. No new graph dependency or scope change is proposed.

## Verified baseline versus planned work

M7's `POST /integrations/v1/tracking-events` authenticates source credentials, validates the bounded tracking contract and deliberately returns `503 intake_unavailable` for otherwise valid events. It has **no durable accepted event**. M8 proposes changing only successful receipt behavior after atomic PostgreSQL commit. All M8 tables, response variants, scripts and tests in the plan are **proposed, not existing**.

After a D-006 decision, implementation may proceed in a separately named feature branch and disposable E:-backed worktree. Keep a single coordinated EF migration, preserve source locks and G0-G7 accepted invariants. Run complete G8 verification and request explicit acceptance before merging implementation to `main`. Do not turn a documentation merge into D-006 acceptance or an executed G8 PASS.

## Open acceptance dependencies

1. D-006 Option A or an explicitly revised alternative: source-key identity, canonical replay, first collision record, 202/409/503 responses, 10,000-pending per-org admission cap, immutable retention and future worker/dead-letter state.
2. M8-02 and M8-03 implementation after D-006 decision, then integrated M8-04 with real PostgreSQL concurrency and failure recovery.
3. Raw G8-S01 through G8-S25 evidence, independent review and explicit G8 acceptance before implementation merge.

G8, G9 and G10 do not become accepted through this plan. M16/M17/M19 preparatory work remains separately eligible under its own prerequisites.
