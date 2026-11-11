# Architecture Decisions

**Newer interim record (2026-11-11):** after the round 2 NOT PASS the findings were remediated and the source frozen at `4e7b9e22aacef25f7f60e2295938ec6cf79bdadd`; campaign c3 passed 13 of 14 jobs and one G15 mutation job still has to be rerun, so the figures in the paragraph below describe the earlier source `3e63bd0` (see the [interim record](G13_G15_REMEDIATION_C3_INTERIM_20261111.md)). Acceptance pending.

**Latest G13/G15 local closure verification (2026-11-11):** Source-locked local campaign `c2` (not a GitHub Actions result) PASSED every technical group at `3e63bd0e71bdf4a80cd8ea1d27e7cceca3c37524`, after remediation of the round 1 findings: 565 TRX tests passed with none failed or skipped, 29 of 29 G13 source mutants, 4 of 4 SQL security mutants and 17 of 17 G15 mutants killed with clean controls, 8 of 8 process SIGKILL phases recovered or verified, 54 of 54 Chromium, Firefox and WebKit browser case executions (the browser job passed on its third attempt; the first two failures were harness defects and are recorded), and 257 atomic assertions reconciled. G13 performance is measured under the restricted API role: realistic skewed 85,000 shipments p50 8.8 s and p95 9.3 s, reference 170,000 p50 9.2 s and p95 11.0 s against 15 s and 20 s; four simultaneous builders return 4 of 4 HTTP 200 at 85,000 and 4 of 4 HTTP 413 at 170,000 with no 503. Independent S25 round 2 at `3e63bd0` returned **NOT PASS** for both gates (narrow and closable: no security, isolation or data-loss defect; mutation and witness gaps, thin concurrency headroom beyond four builders for G13, and three unproven clauses for G15; see the [G13 review](G13_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md) and [G15 review](G15_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md)); round 1 at `5bcb7e5` also returned NOT PASS for both gates. The G13 and G15 acceptance decisions: **acceptance pending**. See the [round 2 results](G13_G15_LOCAL_CLOSURE_RESULTS_C2_20261111.md) and the [round 2 review brief](G13_G15_S25_REVIEW_BRIEF_ROUND2_20261111.md). No merge, deployment or activation is authorized.
**Previous G13/G15 gate-decision checkpoint (2026-11-10, hosted run; the local closure verification above supersedes its evidence, its decisions stand until new decisions are recorded):** [Full source-locked run 42745334415](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED 15/15 jobs at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543/543 passed TRX tests, 227/227 technical assertion witnesses, 48/48 technical scenario checks and artifact integrity PASS. The separate [G13/G15 gate decisions](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record **G13 NOT ACCEPTED and G15 NOT ACCEPTED**, after [individual S25 assessments](G13_G15_REVIEW_MATRIX_20261110.md). G13 permanent tenant quota/idle cleanup enforcement and some exact mutation and HTTP-outage evidence remain open. P10 verification condition is satisfied; P12 cleanup/deletion remains disabled. Protected `main` is unmodified. No merge, deployment or activation is authorized.
**Historical 2026-11-10 predecessor evidence:** [Verified campaign 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed all 15 jobs at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`, with 541 passed tests, zero failures/skips and intact artifacts. The assertion audit still has 3 missing technical witnesses, 7 policy-review and 8 independent-review items. G13/G15 acceptance and integration remain pending. See [the current evidence and policy boundary](G13_G15_VERIFIED_EVIDENCE_20261110.md).

This document records accepted architecture decisions and outstanding choices. D-003 was explicitly accepted on 2026-11-01 alongside G4 after independent evidence review. D-002 Option A and M7/G7 were accepted on 2026-11-03; see [G7 closure](M7_G7_GATE_CLOSURE.md). D-006 Option A and M8/G8 were explicitly accepted on 2026-11-04 after independently reviewed source-locked evidence. See [gate closure](M8_G8_GATE_CLOSURE.md).

**M15 decision state (2026-11-10):** P01-P09, P11, P13 and P14 are approved; P10 is conditionally approved, and P12 authorizes provisional periods only with cleanup disabled. Remaining conformance, independent reviews and gate acceptance are distinct from these decisions. [Binding policy decision](G13_G15_POLICY_APPROVAL_20261109.md), [M15 register](M15_POLICY_RECOMMENDATIONS_20261109.md).

## Local storage constraint

The mandatory [Local Storage Policy](LOCAL_STORAGE_POLICY.md) requires E-drive storage for all controllable persistent project files, caches, Docker/WSL state, database data and evidence. C-drive use is limited to unavoidable short-lived Windows temporary activity. This does not grant permission for cleanup, package migration or changes to global tool installations.

The accepted M0 baseline uses the hardened rootless PostgreSQL container and .NET 10. The prior upstream `gosu` findings were closed in the immutable-image high/critical scan performed during GitHub Actions run #2. G0 was accepted on 2026-10-31; future image changes require scanning.

## Approved M10-local policy decisions, 2026-11-07

M10-L01 through M10-L14 are approved, with modifications to L07 and L14 and a condition on L06. L07 mandates atomic M16 audit evidence for every successful correction, without duplication on replay. L14 removes unrestricted direct SQL correction INSERT by the API identity and requires nonforgeable, session-bound authority at the database boundary. L06 Manager-only correction semantics depend on verification of L14. The decision details are recorded under [M10 local policy selection for G10](#m10-local-policy-selection-for-g10-2026-11-07).

The revised L07/L14 and conditional L06 implementations are accepted after executed security verification. S01 and S25 closed on 2026-11-07, and the remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28). See [G10 closure](M10_G10_GATE_CLOSURE_20261107.md) for source and evidence boundaries.

**Final merge-readiness update (2026-11-07):** Two review findings are closed in source and verified on `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343` ([record](M10_G10_FINAL_MERGE_READINESS_20261107.md)): the accepted twelfth migration is restored unchanged and the NULL-safe projector scope function now arrives through the thirteenth migration, and the default `compose.yaml` requires `HARBOROPS_M10_CORRECTION_PROOF_KEY` for the migrator and API only. Targeted results: unit 239/239, contract 39/39, projector scope tests 3/3, change-impact integration set 126/126, default-stack correction flow 11/11, six projector mutants killed. The full integration suite, real-stack check and browser run were not repeated on this source. At this intermediate source-review checkpoint, G10-S25 and S01 were not yet executed. Both were subsequently reconciled and G10 accepted on 2026-11-07; the G10 gate closure is authoritative.

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
| D-005 | Exact operational KPI tolerances | **Option A approved** for `m13-kpi-v1` with safeguards; [signed decision](M13_D005_POLICY_APPROVAL_20261108.md). Verification and G13 acceptance remain separate. | M13 |
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

The [M16 technical report](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/04-final-report.md) records standalone verification on `e571157`. The separate [G16 main-evidence review](M16_G16_INDEPENDENT_EVIDENCE_REVIEW.md) verified 162/162 file hashes and the principal raw results, but the S07/S08/S28 supplement was subsequently [reviewed](M16_G16_SUPPLEMENT_EVIDENCE_REVIEW.md), with 6/6 consistent checks and a source-provenance caveat, and the combined accepted-M9 verification has since passed. The standalone review alone did not grant acceptance; subsequent independent combined evidence review, four approved Option A decisions, and the [G16 gate closure](M16_G16_GATE_CLOSURE.md) established acceptance on 2026-11-06. Do not alter accepted M9 behavior, D-006 or historical evidence without an approved change.

## G16 integration decision and acceptance (2026-11-06)

The combined M9/M16 runtime at `79318c6b6126362a0b26a1452aad6d2b48fdf4b4` passed the reviewed technical gate through a preserved initial Firefox failure followed by a complete same-source browser and mutation supplement. The [G16 gate closure](M16_G16_GATE_CLOSURE.md) records explicit acceptance and four approved Option A choices: M9 immutable worker history as audit of record; the migration-time role provisioner as the sole combined authority; privileged migrator retained within its stated threat model; and F001 provisioner-installed guards accepted. Accepted M9 sustained-processing restrictions and the 10,000-item unresolved limit remain mandatory. Final source-diff review found and corrected the generation-zero F001 issue. The correction `8ba7727` passed 17/17 focused and 286/286 full integration tests plus an effective regression mutation. PR #25 was separately authorized and merged on 2026-11-06 at `47ba55e2c88556bc5ca0da7d50335ffc8c5ca56d`. No public deployment or full-product release is approved.

## M10 local policy selection for G10, 2026-11-07

M10-L01 through M10-L14 were approved with modifications to **L07** and **L14**, and **L06** conditioned on the L14 authorization boundary being implemented and verified. L01-L05 and L08-L13 retain the exact proposed contracts in `S01_PROPOSED_M10_LOCAL_POLICY_DECISIONS.md` from the submitted decision package. L06 retains its append-only, Manager-only, idempotent and same-shipment behavior, subject to the new database boundary.

**L07:** A successful tracking correction must write an M16 audit event atomically with the immutable M10 correction and dependent effects; audit persistence failure rolls back the transaction. The original correction ledger remains authoritative specialized evidence, and an idempotent replay must not duplicate an audit event.

**L14:** Ordinary API-role direct correction INSERT is no longer authorized as the target contract. The replacement constrained database operation must validate live authority and nonforgeable request provenance, even against a database-credential-only attacker who can read session IDs. Worker and projector rights stay restricted. Existing M16 audit and M9 operational boundaries remain unchanged.

The [targeted remediation plan](M10_G10_L07_L14_REMEDIATION_PLAN.md) specified executable safeguards. [Final remediation evidence](M10_G10_FINAL_MERGE_READINESS_20261107.md) and [G10 closure](M10_G10_GATE_CLOSURE_20261107.md) establish S01 and S25 conformance and accepted integration in PR #28. The policy selection alone was not test proof; its implemented controls were separately verified. No deployment or release is authorized.
