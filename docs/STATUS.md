# Project Status

**As of:** 2026-11-01 (M4 feature-branch state; main remains at the accepted G3 baseline)
**Default branch:** `main` contains accepted M0, M1, M2 and M3 implementations, tests, documentation and verification records.
**Baseline:** M0 merged via PRs #1 and #3; M1 merged via PR #4; M2 merged via [PR #5](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/5) at `1df638d5300191f6c2c6c9d201fd5f4c4ab9e562` on 2026-10-31.
**Acceptance:** G0, G1, G2 and G3 explicitly accepted on 2026-10-31. G2 passed all 25 scenarios on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1`; [G2 evidence](M2_EXECUTED_VERIFICATION.md) remains preserved. G3 passed all 25 scenarios on `b72025d35d62d0a8a45b466986a0b3bd89eebadb`; the independent raw-evidence review found 174 successful .NET executions, 57 successful browser tests and clean HIGH/CRITICAL image scans. See [G3 executed verification](M3_EXECUTED_VERIFICATION.md). M3 is integrated into `main` by fast-forward; G4-G24 remain open.

## Accepted M1 integration

G1 verified and accepted on 2026-10-31; M1 merged to `main` via PR #4 (`8f9fc0d506a9e1dc1d4f5acab936622df988f413`). Authentication remains local-first; tenant membership and role isolation are M2. The accepted M0 regressions remain covered by M1 verification. The merge and the subsequent documentation reconciliation use `[skip ci]`.

## Delivery ledger

The ledger below is the current *observed* state, not a projection from planning nodes. The v1 M0-M6 scheme was superseded for planning by v2 M0-M24. Old v1 milestone IDs beyond M0 must not be read as matching new v2 milestones.

| Milestone | State | Evidence |
| --- | --- | --- |
| **M0 Engineering baseline verification** | G0 verified and accepted | [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) passed all required .NET, clean/seeded PostgreSQL, web, functional browser, Compose recovery and final image checks on `fec7429415fede121db8a4a34f0df72cd4fac609`. |
| **M1 Authentication and session lifecycle** | G1 verified and accepted; merged into `main` | [M1 verification register](M1_IMPLEMENTATION_VERIFICATION.md); both final CI runs passed; PR #4 merge `8f9fc0d506a9e1dc1d4f5acab936622df988f413` on 2026-10-31. |
| **M2 Organizations and role isolation** | G2 verified locally, accepted and merged into `main` | [Executed M2 verification](M2_EXECUTED_VERIFICATION.md): all G2-S01 to G2-S25 scenarios passed on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1`. [PR #5](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/5) merged as `1df638d5300191f6c2c6c9d201fd5f4c4ab9e562` on 2026-10-31. |
| **M3 Ports and vessels reference data** | **G3 verified locally, accepted and integrated into `main`** | [G3 executed verification](M3_EXECUTED_VERIFICATION.md) records the 25/25 passing matrix at `b72025d35d62d0a8a45b466986a0b3bd89eebadb`. Independent review of 63 raw evidence files confirmed 174 passing .NET executions, 57 passing browser tests across three engines and 0 HIGH/CRITICAL findings in three image scans. Accepted 2026-10-31; fast-forward integration from `feat/m3-port-vessel-reference-data`. [Implementation register](M3_IMPLEMENTATION_STATUS.md) and historical [first](M3_CORRECTNESS_AUDIT.md)/[second](M3_SECOND_CORRECTNESS_AUDIT.md) audits retain provenance. |
| **M4 Voyages and containers reference data** | Feature-branch implementation, expanded tests and source correctness audit committed; G4 unverified, acceptance pending | [M4 source inventory](M4_IMPLEMENTATION_STATUS.md) and [first correctness audit](M4_CORRECTNESS_AUDIT.md) and [second correctness audit](M4_SECOND_CORRECTNESS_AUDIT.md) record domain, migration, API, React, tests, six additional source remediations and outstanding execution. D-003 remains unaccepted. A partial E:-backed G4 run at `7d0dd3e60049` passed decision-unit, preflight and PostgreSQL image stages but stopped at the .NET test build with two xUnit analyzer errors (exit 11). The subsequent run at `fa46c86fd6e` built successfully with zero warnings/errors and passed 82 unit and 12 contract tests; all but one integration test failed on the same EF migration snapshot drift. The three missing voyage indexes were then aligned across live model, snapshot and SQL migration, but no corrected-SHA runtime result exists. No passing PostgreSQL integration or browser evidence, no G4 acceptance and no merge to `main`. |
| **M5 Shipment lifecycle and concurrency** | Planned | No verified implementation or accepted gate G5 |
| **M6 Container and voyage assignments** | Planned | No verified implementation or accepted gate G6 |
| **M7 Tracking contracts and source authentication** | Planned | No verified implementation or accepted gate G7 |
| **M8 Durable event inbox and immutable history** | Planned | No verified implementation or accepted gate G8 |
| **M9 Idempotent worker and reconciliation** | Planned | No verified implementation or accepted gate G9 |
| **M10 Event timeline and projection rebuild** | Planned | No verified implementation or accepted gate G10 |
| **M11 Delay rules and shipment exceptions** | Planned | No verified implementation or accepted gate G11 |
| **M12 Shipment search and authorized export** | Planned | No verified implementation or accepted gate G12 |
| **M13 Operational KPI definitions and dashboards** | Planned | No verified implementation or accepted gate G13 |
| **M14 Route mapping and geographic context** | Planned | No verified implementation or accepted gate G14 |
| **M15 Scoped in-app notifications** | Planned | No verified implementation or accepted gate G15 |
| **M16 Audit logging and access hardening** | Planned | No verified implementation or accepted gate G16 |
| **M17 Observability and service health** | Planned | No verified implementation or accepted gate G17 |
| **M18 IT incident lifecycle and remediation** | Planned | No verified implementation or accepted gate G18 |
| **M19 Database recovery and migration rehearsal** | Planned | No verified implementation or accepted gate G19 |
| **M20 Security assurance and dependency review** | Planned | No verified implementation or accepted gate G20 |
| **M21 Performance and capacity benchmarks** | Planned | No verified implementation or accepted gate G21 |
| **M22 Deployment portability and cost controls** | Planned | No verified implementation or accepted gate G22 |
| **M23 Interface polish and workflow consistency** | Planned | Polish functional interfaces; accessibility tests are not an M23 gate |
| **M24 Final release validation and acceptance** | Planned | Full accessibility testing scheduled only in final package M24-04, followed by G24 release decision |

## Planning sources of truth

- [PRD](PRD.md): product scope, personas, success criteria and priorities.
- [Roadmap](ROADMAP.md): milestone scopes, delivery order and G0-G24 acceptance gates.
- [Execution Graph](EXECUTION_GRAPH.md): rendered work-package DAG from [execution-graph.json](execution-graph.json).
- [Requirements](REQUIREMENTS.md) and [Acceptance Criteria](ACCEPTANCE_CRITERIA.md): atomic requirement IDs and detailed checks.
- This file: evidence and true implementation state, not milestone commitments.

## Full-product documentation and release readiness

The unchecked items below concern the complete M0-M24 product release. They do not reverse M0's separately recorded G0 verification.

- [x] Product vision, 25 small-scope milestones and the dependency graph documented.
- [x] Functional and non-functional requirements drafted.
- [x] Architecture, domain model, and API boundary drafted.
- [x] Tracking processing and security policies drafted.
- [x] D-001 architecture approved; M1 implemented, verified, accepted and merged into `main`.
- [x] Milestone acceptance and verification approach drafted.
- [x] Local-first cost constraints and demonstration plan drafted.
- [ ] Implemented module architecture validated by code.
- [ ] API schema generated from working implementation.
- [ ] Full product CI, migrations, unit, integration, browser, and security tests executed (M0-specific local tests executed and documented separately).
- [ ] Observability, incident workflow, and backup/restore demonstrated.
- [ ] Local deployment cost and shutdown verified; hosted safeguards checked only if optional hosting is used.
- [ ] Complete release acceptance.

## Verification evidence

The `main` branch includes M0 runtime, M1 authentication and M2 organization authorization. G0 and G1 used disposable GitHub-hosted verification; G2 used disposable local database, Compose and browser resources on E:. None of these milestone acceptances constitutes public or production deployment. See [M0 evidence](M0_HARDENING_VERIFICATION.md), [M1 evidence](M1_IMPLEMENTATION_VERIFICATION.md) and [M2 evidence](M2_EXECUTED_VERIFICATION.md).

## Accepted milestone: M2

M2-01 through M2-04 and all G2-S01 through G2-S25 scenarios are complete. The locally verified source revision is `fbddc1361d79b027275bb53b48133ba0dc7d5cb1`. G2 was explicitly accepted on 2026-10-31 and [PR #5](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/5) merged the accepted work into `main` at `1df638d5300191f6c2c6c9d201fd5f4c4ab9e562`. See [architecture](M2_ARCHITECTURE_DESIGN.md), [implementation record](M2_IMPLEMENTATION_STATUS.md), [G2 verification plan](M2_VERIFICATION_PLAN.md) and [executed evidence](M2_EXECUTED_VERIFICATION.md).

G0, G1, G2 and G3 are accepted and integrated into `main`. D-003 remains scheduled for M4 and D-002 for M7. Historical-volume compatibility remains at M6, M12, M18 and M24-03; dedicated accessibility verification remains M24-04.

## M4 implementation status

The proposed [M4 architecture](M4_ARCHITECTURE_DESIGN.md), [implementation plan](M4_IMPLEMENTATION_PLAN.md), [G4 plan](M4_VERIFICATION_PLAN.md) and [D-003 proposal](M4_D003_CONTAINER_IDENTIFIER_PROPOSAL.md) now have initial source implementation on the dedicated M4 feature branch. The [M4 implementation register](M4_IMPLEMENTATION_STATUS.md) and [source correctness audit](M4_CORRECTNESS_AUDIT.md) identify the committed code, expanded tests, remediations and remaining verification gaps. D-003 remains pending. The first local G4 execution at `7d0dd3e60049` failed on two subsequently fixed xUnit analyzer errors. The second E:-backed execution at `fa46c86fd6e` built cleanly, passed 82 unit and 12 contract tests, then failed 86/87 fresh and repeated integration tests due to three missing voyage foreign-key index definitions in the snapshot. The model, snapshot, and existing migration index names have been reconciled, and a new `schema-index` precheck added. See the [third correctness audit](M4_THIRD_CORRECTNESS_AUDIT.md). The corrected SHA has not been run; browser matrix and full G4 remain unverified, with no acceptance or merge implied.

## Next eligible parallel workstreams

M3-01 through M3-04 are implemented, G3 is accepted and M3 has been fast-forward integrated into `main`. The E:-backed G3 run at `b72025d35d62d0a8a45b466986a0b3bd89eebadb` passed 50 unit, 12 contract, 56 fresh and 56 repeated PostgreSQL integration tests plus 19 browser tests in each of Chromium, Firefox and WebKit. [Executed verification](M3_EXECUTED_VERIFICATION.md) and [implementation status](M3_IMPLEMENTATION_STATUS.md) record the evidence. **M4 source is under implementation on its feature branch, without G4 acceptance; M5-M24 are planned.** The v3.0 dependency graph allows distinct eligible tracks from accepted G3, including M4 (D-003), M5, M7 (D-002), M16, M17, M19 (D-008), M20-01 and M23-01. See [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md). No G4-G24 gate is verified or accepted and no production deployment is authorized.

## Storage and residual risk

The [Local Storage Policy](LOCAL_STORAGE_POLICY.md) requires all controllable project storage and verification on E:, with no deliberate project writes to C:. During the earlier G2 test iterations, WSL created crash reports on C: (approximately 1.5 GB at the time of verification). The CLI crash handling and container core-dump controls were corrected; the final local G2 run created no new dumps. Preexisting crash reports were not removed and require separate authorized handling. See [G2 executed verification](M2_EXECUTED_VERIFICATION.md). The historical PostgreSQL image scan reported one critical and 25 high findings in `gosu`; the accepted rootless image, API and web images passed new high/critical Trivy scans on the verified hardening SHA. The runtime was upgraded from .NET 8 to .NET 10. Later security, backup/restore and performance gates remain planned; all accessibility testing is reserved for M24-04 immediately before the final G24 release gate.

## Status update rule

Update this page whenever a milestone materially changes state. Record implemented paths, exact executed checks, failures, accepted limitations, and links to verification artifacts. Do not mark a capability verified based only on documentation or source inspection.

## Quarterly data compatibility policy

G0 does not depend on any retained local Docker volume. Fresh disposable database checks are required. Historical-volume compatibility moves to quarterly progress checkpoints at M6, M12, M18 and M24-03, using safe copies only; M19 separately owns comprehensive backup, restore and upgrade rehearsal. If no representative snapshot exists at a checkpoint, record NOT APPLICABLE. Breaking changes applied to retained data still require a safe-copy rehearsal before rollout.

