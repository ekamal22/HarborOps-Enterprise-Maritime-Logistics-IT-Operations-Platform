# Architecture Decisions

This document records accepted architecture decisions and outstanding choices. D-003 was explicitly accepted on 2026-11-01 alongside G4 after independent evidence review. D-002 Option A and M7/G7 were accepted on 2026-11-03; see [G7 closure](M7_G7_GATE_CLOSURE.md). D-006 Option A and M8/G8 were explicitly accepted on 2026-11-04 after independently reviewed source-locked evidence. See [gate closure](M8_G8_GATE_CLOSURE.md).

## Local storage constraint

The mandatory [Local Storage Policy](LOCAL_STORAGE_POLICY.md) requires E-drive storage for all controllable persistent project files, caches, Docker/WSL state, database data and evidence. C-drive use is limited to unavoidable short-lived Windows temporary activity. This does not grant permission for cleanup, package migration or changes to global tool installations.

The accepted M0 baseline uses the hardened rootless PostgreSQL container and .NET 10. The prior upstream `gosu` findings were closed in the immutable-image high/critical scan performed during GitHub Actions run #2. G0 was accepted on 2026-10-31; future image changes require scanning.

## Accepted baseline decisions

### ADR-001: Modular monolith with separate tracking worker
**Status:** Accepted for initial architecture.

Use one ASP.NET Core application with explicit modules and a separate .NET event worker. Do not introduce microservices solely for architectural appearance. Reconsider only if measured scaling or deployment independence justifies them.

### ADR-002: PostgreSQL as transactional source of truth
**Status:** Accepted.

Store operational records and event inbox/outbox state in PostgreSQL. Derived dashboards may use read models, but projections must be rebuildable from retained accepted events.

### ADR-003: Local-first execution
**Status:** Accepted.

Essential features must work locally without paid services or cloud credentials. Hosted infrastructure is optional, budget-controlled, and disposable.

### ADR-004: Synthetic-data baseline
**Status:** Accepted.

Use deterministic generated datasets. No proprietary company data or private shipping integrations are needed to complete core milestones. Simulated states are clearly labeled.

### ADR-005: Secure organization boundaries
**Status:** Accepted.

Every business-data path must verify an authenticated principal and explicit organization permission. Global platform roles do not imply broad access to customer records.

### ADR-006: Inbound event history is immutable
**Status:** Accepted.

Never silently replace historical accepted events. Correct data with linked correction records and rebuild projections.

The [granular roadmap](ROADMAP.md) assigns earlier decision deadlines to narrower milestone entry points. These dates denote prerequisite boundaries, not new approvals. The original ADR-001 through ADR-006 architecture decisions remain intact.

### D-001: Human browser authentication and server-side session authority
**Status:** Accepted architecture; M1 implemented, verified and accepted on 2026-10-31, integrated into `main` via PR #4 (`8f9fc0d506a9e1dc1d4f5acab936622df988f413`).

Human browser login uses ASP.NET Core 10 cookie authentication with protected tickets, PostgreSQL-backed authoritative session validation on every authenticated request, Argon2id password hashes, antiforgery protection, same-origin React/API routing and persistent Data Protection keys. Account disablement or password reset invalidates sessions immediately. Integration-scoped tokens were implemented and accepted in M7, while organization roles/isolation begin in M2. The approved detailed record is [D-001](ADR_D001_BROWSER_SESSIONS.md), with [M1 design](M1_AUTHENTICATION_DESIGN.md) and [G1 test plan](M1_VERIFICATION_PLAN.md). This decision approval is distinct from the [executed M1 runtime verification](M1_IMPLEMENTATION_VERIFICATION.md) and from G1 acceptance, which was explicitly recorded on 2026-10-31.

## M2 implementation choices (verified locally; not a new global decision)

[ADR-005](#adr-005-secure-organization-boundaries) requires scoped access. The [M2 architecture](M2_ARCHITECTURE_DESIGN.md) implements one role per organization membership, a fixed typed permission matrix, server-stored selection in `auth_sessions`, live authorization checks, controlled local bootstrap and transactional last-Administrator protection. These implementation-level choices are now integrated into `main` via PR #5 and do not create a new global ADR. The full G2 matrix passed locally on disposable resources ([executed record](M2_EXECUTED_VERIFICATION.md)); no retained workstation database was changed. G2 accepted on 2026-10-31.

## Decision deadlines under parallel delivery

Each D-number belongs to the first applicable implementing work package and must be resolved before that package, even when another milestone proceeds independently. D-003 (accepted for M4), D-002 (accepted for M7), D-006 (accepted for M8), D-005 for M13, D-004 for M14, D-008 for M19, and D-007 for M22 remain milestone-specific; parallel execution does not permit bypassing these decisions. See [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md) and [Execution Graph](EXECUTION_GRAPH.md). Accepted M0-M3 decisions are unchanged.

## M3 reference-data design choices (implemented and accepted)

The [M3 architecture](M3_ARCHITECTURE_DESIGN.md) implements organization-owned port/vessel references with immutable UUID and tenant-local code, optional external identifiers, optimistic versions, and soft archive. Analysts may read; Managers may manage; Support, Administrator and Auditor do not receive implicit business permissions. These M3 implementation decisions follow ADR-002/003/004/005 without changing their scope. All 25 G3 scenarios passed locally on `b72025d35d62d0a8a45b466986a0b3bd89eebadb`; G3 was explicitly accepted on 2026-10-31 ([executed verification](M3_EXECUTED_VERIFICATION.md)). D-003 container identifiers was subsequently accepted for M4 on 2026-11-01.

## D-003: Container identifier format

**Status:** Accepted
**Decision date:** 2026-11-01
**Identifier policy:** Option B, tagged ISO6346 and Synthetic identifier kinds with disjoint validation
**Standard edition:** ISO 6346:2022
**Category scope:** U freight containers only. J and Z equipment are excluded from M4.
**Implementation:** M4-01 to M4-04, accepted after independently reviewed G4 evidence.

The [D-003 decision record](M4_D003_CONTAINER_IDENTIFIER_PROPOSAL.md) compares the alternatives and records the approved rules. ISO-formatted identifiers normalize documented presentation spaces and case, require `[A-Z]{3}U[0-9]{7}`, and must pass the weighted ISO check digit. This checks syntax, not registration, equipment ownership, or physical existence. Synthetic identifiers require `SYN-BOX-[A-Z0-9]{4,16}` and are distinctly labeled; no silent conversion between identifier kinds is permitted.

Approved equipment types: `Dry20`, `Dry40`, `HighCube40`, `Reefer20`, `Reefer40`, `Other`. Container UUID, tenant-local reference code, external/synthetic identifier, identifier kind, equipment type and source classification are immutable after creation. Identifiers remain reserved across archival within an organization; the same identifier may exist in different organizations. No BIC or other external registry validation is claimed or required.

D-003 approval and G4 acceptance were explicitly provided on 2026-11-01 following [independent G4 evidence review](M4_EXECUTED_VERIFICATION.md). The final source tested on E: was `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6`. The evidence index's policy-only G4-S01 incompleteness at execution time is closed by this explicit decision; executable checksum and validation tests had already passed. No software functionality was modified to close the decision.

See the accepted [M5 local contract decision](M5_LOCAL_CONTRACT_DECISION.md) for the shipment lifecycle/permissions contract approved alongside G5 on 2026-11-02. This M5-local decision is not an additional global D-number and does not change the execution graph.

## Milestone decisions (accepted and open)

| ID | Decision required | Default hypothesis | Required before |
| --- | --- | --- | --- |
| D-002 | DCSA reference release/subset | Accepted 2026-11-03, Option A: DCSA Track & Trace 2.2.0 as the reference baseline for the deliberately narrower, HarborOps-specific, non-conformant profile `tnt-2.2-harborops-v1`, pinned by SHA-256 as local snapshots (not official immutable releases); the supported subset is unchanged. See the [decision record](M7_D002_TRACKING_REFERENCE_PROPOSAL.md) and [reference snapshots](../third_party/dcsa-track-and-trace/README.md) | M7 |
| D-004 | Map tile provider/license | Use compliant free source with local fallback | M14 |
| D-005 | Exact operational KPI tolerances | Versioned business rules and deterministic fixtures | M13 |
| D-006 | Transactional inbox, source-key replay, collision, pending cap, retention and future worker/dead-letter durability | **Accepted 2026-11-04, Option A.** PostgreSQL transactional receipt/event/Pending work with permanent source event identity and a 10,000 Pending cap, followed by M9 worker/retry/dead-letter transitions. See [D-006 decision](M8_D006_DURABILITY_PROPOSAL.md), [independent evidence](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md) and [gate closure](M8_G8_GATE_CLOSURE.md). | M8-01 and M9 |
| D-007 | Hosting account and budget ceiling | Local only until credit and spending restrictions checked | M22 |
| D-008 | Backup retention and recovery objectives | Define measurable targets based on demo environment | M19 |

## D-006: Durable inbox and future worker handoff

**Status:** Accepted 2026-11-04, Option A.
**Decision reference:** [Explicit D-006 acceptance](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/14#issuecomment-6633731945).
**Review source:** `537af9c2af99751aa2fbedf1a2115b7715e7b9e6`, [independent evidence](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md).

PostgreSQL atomically persists one immutable receipt, accepted event and separate Pending work row. The scoped replay key is permanently reserved by a PostgreSQL unique index, with canonical digest, deterministic duplicate identity and bounded 409 collision metadata. The original bearer is reauthorized under organization/source/credential transaction locks before post-commit-only 202. The configurable 10,000 Pending per-organization default is accepted for the demonstrator, not certified production throughput. No immutable accepted-history deletion is authorized. M9 must add a separately reviewed migration permitting lease/retry/Processed/DeadLetter work-state transitions and preserve receipt/event immutability. See [D-006 contract](M8_D006_DURABILITY_PROPOSAL.md) and [M8/G8 closure](M8_G8_GATE_CLOSURE.md).

## M9 local contract (not a new D-number)

**Accepted 2026-11-05 with mandatory operating condition.** G9 was accepted following the [evidence inspection](M9_G9_INDEPENDENT_EVIDENCE_REVIEW.md). Planning PR #16 merged to `main` at `43fe326`, then implementation PR #18 at `4684352`. [Gate closure](M9_G9_GATE_CLOSURE.md) records the limitations. No production deployment is implied. All six implementation choices in [M9-01 local decision](M9_LOCAL_CONTRACT_DECISION.md) are approved as implemented: work-state leasing and immutable effects, bounded retry and redrive settings, expanded 10,000 unresolved-work admission accounting, conservative reference reconciliation, Manager-only redrive permissions, and a separate least-privilege worker. This intentionally tightens the accepted M8 admission predicate without revising or deleting D-006 historical evidence.

**Transport-backlog condition:** Do not enable sustained transport-event ingestion or processing in a deployment until M10 or a separately approved mechanism manages transport events parked in NeedsReconciliation and prevents the 10,000-item unresolved backlog from being exhausted. Controlled verification remains permitted.

The earlier [proposal](M9_LOCAL_CONTRACT_PROPOSAL.md) and [G9 acceptance package](M9_G9_ACCEPTANCE_PACKAGE.md) preserve the pre-acceptance state as history. Independent G9-S25 review and explicit gate acceptance are complete; see [G9 closure](M9_G9_GATE_CLOSURE.md). M9 merged into `main` on 2026-11-05, but no production deployment is authorized.

## M16 local audit policy and integration choices

Five M16-local policy choices were approved on 2026-11-04: only a current selected-organization Auditor may read audit entries; retain entries online for 365 days before separately verified archival; fail closed with transactional audit persistence for sensitive successful mutations; separate least-privilege migration, API and worker PostgreSQL identities; and defer external cryptographic anchoring. The [M16 feature-branch policy](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m16-audit-access-hardening/docs/M16_POLICY_DECISIONS.md) records the approved contract. These decisions are separate from G16 acceptance.

The [M16 technical report](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/04-final-report.md) records standalone verification on `e571157`. The separate [G16 main-evidence review](M16_G16_INDEPENDENT_EVIDENCE_REVIEW.md) verified 162/162 file hashes and the principal raw results, but **`supplement-s07-s08-s28.log` is UNREVIEWED** and the full M9 integration gate has not run. Neither technical success nor review of part of the evidence grants G16 acceptance. G16 acceptance pending. Open integration choices: one authoritative worker-role provisioning path, whether immutable M9 work/effect history suffices for M16 audit coverage, risk treatment for the current superuser migrator and edited unreleased M16 migration, and source-locked combined EF migration/authorization/browser/outage verification. Do not alter accepted M9 behavior, D-006 or historical evidence without an approved change.
