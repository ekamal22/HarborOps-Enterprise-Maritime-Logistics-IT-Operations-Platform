# Project Status

**As of:** 2026-10-31
**Default branch:** `main` contains accepted M0 and M1 source, runtime, tests, documentation and verification workflows.
**Baseline:** M0 was merged via PRs #1 and #3. M1 was accepted and merged via [PR #4](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/4) at `8f9fc0d506a9e1dc1d4f5acab936622df988f413`.
**Acceptance:** G0 and G1 verified and explicitly accepted on 2026-10-31. M1 verification: [run 41143665657](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41143665657) and [run 41144356898](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41144356898). G2-G24 remain open.

## Accepted M1 integration

G1 verified and accepted on 2026-10-31; M1 merged to `main` via PR #4 (`8f9fc0d506a9e1dc1d4f5acab936622df988f413`). Authentication remains local-first; tenant membership and role isolation are M2. The accepted M0 regressions remain covered by M1 verification. The merge and the subsequent documentation reconciliation use `[skip ci]`.

## Delivery ledger

The ledger below is the current *observed* state, not a projection from planning nodes. The v1 M0-M6 scheme was superseded for planning by v2 M0-M24. Old v1 milestone IDs beyond M0 must not be read as matching new v2 milestones.

| Milestone | State | Evidence |
| --- | --- | --- |
| **M0 Engineering baseline verification** | G0 verified and accepted | [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) passed all required .NET, clean/seeded PostgreSQL, web, functional browser, Compose recovery and final image checks on `fec7429415fede121db8a4a34f0df72cd4fac609`. |
| **M1 Authentication and session lifecycle** | G1 verified and accepted; merged into `main` | [M1 verification register](M1_IMPLEMENTATION_VERIFICATION.md); both final CI runs passed; PR #4 merge `8f9fc0d506a9e1dc1d4f5acab936622df988f413` on 2026-10-31. |
| **M2 Organizations and role isolation** | Source implemented on feature branch, unverified | [M2 implementation register](M2_IMPLEMENTATION_STATUS.md): organization/membership migration, permission service, API, local CLI, React UI and unexecuted test source. No M2 runtime evidence, not merged into `main`; G2 open. |
| **M3 Ports and vessels reference data** | Planned | No verified implementation or accepted gate G3 |
| **M4 Voyages and containers reference data** | Planned | No verified implementation or accepted gate G4 |
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

The `main` branch contains the M0 runtime and M1 authentication implementation. Disposable GitHub-hosted environments, not retained workstation Docker volumes, executed the passing G0 and G1 verifications. No public or production deployment has occurred. See [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md) and [M1 Verification](M1_IMPLEMENTATION_VERIFICATION.md).

## Next milestone: M2

The [M2 architecture](M2_ARCHITECTURE_DESIGN.md), [M2 execution sequence](M2_IMPLEMENTATION_PLAN.md) and [G2 local verification plan](M2_VERIFICATION_PLAN.md) are documented. Source for M2-01 through M2-03 has been committed on a feature branch; verification is excluded from this handoff. The `main` branch remains at the accepted M1 baseline, while M2 source is committed on `feat/m2-organization-authorization`. [M2 implementation status](M2_IMPLEMENTATION_STATUS.md) lists the additive migration, scoped endpoints, membership service, browser controls and unexecuted test source. No M2 migration or test has been run and no G2 runtime evidence exists. GitHub Actions usage is prohibited until explicitly restored, so no workflows, dispatches, reruns or remote CI checks are authorized.

G0 and G1 are accepted. M2 is the next unverified milestone: organization and membership modeling, roles, server-enforced tenant isolation and cross-organization denial tests. Follow the [roadmap](ROADMAP.md) and [execution graph](EXECUTION_GRAPH.md); do not claim G2 until its implementation and dedicated verification are complete. D-003 remains scheduled for M4, D-002 for M7. Dedicated accessibility verification remains M24-04; historical-volume compatibility stays at M6, M12, M18 and M24-03.

## Storage and residual risk

The [Local Storage Policy](LOCAL_STORAGE_POLICY.md) requires all controllable persistent project storage on E:. The M0 host used E-drive repository, caches, evidence and Docker WSL data; operating-system temporary activity on C: may be unavoidable. The historical PostgreSQL image scan reported one critical and 25 high findings in `gosu`; the accepted rootless image, API and web images passed new high/critical Trivy scans on the verified hardening SHA. The runtime was upgraded from .NET 8 to .NET 10. Later security, backup/restore and performance gates remain planned; all accessibility testing is reserved for M24-04 immediately before the final G24 release gate.

## Status update rule

Update this page whenever a milestone materially changes state. Record implemented paths, exact executed checks, failures, accepted limitations, and links to verification artifacts. Do not mark a capability verified based only on documentation or source inspection.

## Quarterly data compatibility policy

G0 does not depend on any retained local Docker volume. Fresh disposable database checks are required. Historical-volume compatibility moves to quarterly progress checkpoints at M6, M12, M18 and M24-03, using safe copies only; M19 separately owns comprehensive backup, restore and upgrade rehearsal. If no representative snapshot exists at a checkpoint, record NOT APPLICABLE. Breaking changes applied to retained data still require a safe-copy rehearsal before rollout.

