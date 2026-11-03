# M7 Planning Status

**As of:** 2026-11-03
**State:** Architecture and implementation plan drafted, acceptance pending.
**Baseline:** `main` at `97b67739cf1dad543bf25fa56326fb4702990f41`
**Working branch:** `plan/m7-tracking-contracts-source-auth`
**Implementation:** Committed on `feat/m7-tracking-source-validation`; technical verification executed (see [executed verification](M7_EXECUTED_VERIFICATION.md))
**G7 verification:** Technical run executed on `7b87f3250aba677b1f4aeef4350803d9ca7b6880`; 23 PASS, 2 BLOCKED on D-002 after supplements
**G7 acceptance:** PENDING
**D-002:** Proposed public DCSA TNT 2.2.0 reference/subset; exact upstream artifact and approved field/code profile pending.

## Scope and authorities

M7 defines the versioned tracking adapter contract, source/tenant credentials and strict payload rejection while deliberately returning 503 for otherwise valid events until M8 implements durable intake.

- [D-002 proposed reference](M7_D002_TRACKING_REFERENCE_PROPOSAL.md): public baseline, alternatives, upstream pin, fixture and subset decision requirements.
- [M7 architecture](M7_ARCHITECTURE_DESIGN.md): trust boundaries, source lifecycle, tenant binding, schema, errors, migration and M8 handoff.
- [M7 implementation plan](M7_IMPLEMENTATION_PLAN.md): M7-01 contract; M7-02 entitlements; M7-03 validator; M7-04 integration and G7 gate.
- [G7 verification plan](M7_VERIFICATION_PLAN.md): G7-S01 to G7-S25 local, source-locked evidence obligations.
- [Roadmap](ROADMAP.md) and [execution graph](execution-graph.json): authoritative prerequisites and work-package IDs.

## Decisions and execution order

1. Explicitly accept or revise D-002 after pinning and reviewing exact upstream normative artifact, license, first event codes and mapping fixtures.
2. Freeze the canonical HarborOps profile and schema (M7-01).
3. Implement M7-02 credentials and M7-03 validation in parallel on non-conflicting worktrees. Coordinate one additive EF migration after accepted M6.
4. Integrate M7-04 HTTP contract and run all G7-S01 to G7-S25 cases locally with isolated E:-backed evidence.
5. Obtain independent G7 technical review and explicit acceptance before merging the M7 implementation.

The planning PR itself contains no application changes. The separate implementation branch contains source, tests and a migration, and the executed verification record, but no accepted G7 evidence. See [implementation register](M7_IMPLEMENTATION_STATUS.md). Its approval should not be conflated with D-002 acceptance unless that decision is explicitly included. Accepted M0-G0 through M6-G6 remain unchanged; D-006 for M8 remains open.

## Operating constraints

The source repository is public, `main` is protected and GitHub Actions is disabled. Use pull requests and `[skip ci]` commits. All controlled local verification and Docker/cache/evidence storage must remain on E:, with no persistent use of C:. Do not host the application or publish credentials. Accessibility testing remains deferred to M24-04.

## Next state

**M7 architecture review and D-002 decision: acceptance pending.**
