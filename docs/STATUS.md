# Project Status

**As of:** 2026-10-31
**Default branch:** `main` contains the accepted M0 application, runtime, tests, docs and CI workflow.
**Baseline:** The accepted M0 engineering foundation, runtime, tests, documentation and verification workflow are integrated into `main`. PRs #1 and #3 are merged.
**Acceptance:** G0 verified at source revision `fec7429415fede121db8a4a34f0df72cd4fac609` in [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) and **accepted** on 2026-10-31; G1 verified on the M1 feature branch, acceptance pending; G2-G24 remain open.

## In-progress M1 implementation branch

On `feat/m1-authentication-session-lifecycle`, M1 persistence, cookie authentication, restricted account CLI, same-origin browser shell, and an initial automated test matrix are implemented in source. **G1 verified on the feature branch, acceptance pending.** [M1 verification register](M1_IMPLEMENTATION_VERIFICATION.md) records the successful execution at `fdefe052e4335ca8d1d287160a9859828d05f9d8`. `main` continues to hold the accepted M0 baseline until a separate verified integration and acceptance decision.

## Delivery ledger

The ledger below is the current *observed* state, not a projection from planning nodes. The v1 M0-M6 scheme was superseded for planning by v2 M0-M24. Old v1 milestone IDs beyond M0 must not be read as matching new v2 milestones.

| Milestone | State | Evidence |
| --- | --- | --- |
| **M0 Engineering baseline verification** | G0 verified and accepted | [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) passed all required .NET, clean/seeded PostgreSQL, web, functional browser, Compose recovery and final image checks on `fec7429415fede121db8a4a34f0df72cd4fac609`. |
| **M1 Authentication and session lifecycle** | Verified on feature branch, acceptance pending | D-001 accepted; M1 identity migration, auth API, React shell, negative tests and [G1 evidence](M1_IMPLEMENTATION_VERIFICATION.md) executed on `fdefe052e4335ca8d1d287160a9859828d05f9d8`. Not merged into `main`. |
| **M2 Organizations and role isolation** | Planned | No verified implementation or accepted gate G2 |
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
- [x] D-001 architecture approved; M1 implemented and verified on the feature branch with acceptance pending.
- [x] Milestone acceptance and verification approach drafted.
- [x] Local-first cost constraints and demonstration plan drafted.
- [ ] Implemented module architecture validated by code.
- [ ] API schema generated from working implementation.
- [ ] Full product CI, migrations, unit, integration, browser, and security tests executed (M0-specific local tests executed and documented separately).
- [ ] Observability, incident workflow, and backup/restore demonstrated.
- [ ] Local deployment cost and shutdown verified; hosted safeguards checked only if optional hosting is used.
- [ ] Complete release acceptance.

## Verification evidence

The `main` branch contains M0 application source and the verified Docker runtime. A disposable GitHub-hosted environment, not a local retained Docker volume, executed the passing G0 verification. No public or production runtime has been deployed. See [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md).

## Open prerequisites

1. G0 is accepted. D-001 is now an accepted M1 architecture decision, documented in [D-001](ADR_D001_BROWSER_SESSIONS.md).
2. M1-01 through M1-04 are implemented on the feature branch. Review [M1 verification evidence](M1_IMPLEMENTATION_VERIFICATION.md) and record acceptance separately.
3. The [G1 Verification Plan](M1_VERIFICATION_PLAN.md) has been executed against a fixed M1 revision. Preserve its results and rerun it after material implementation changes.
4. Keep organization membership and two-organization isolation fixtures in **M2**, container identifier decision D-003 in **M4**, and tracking source decision D-002 in **M7**. Do not expand M1 scope.
5. G1 verification is recorded; acceptance pending. Full accessibility testing is scheduled only for M24-04; historical-volume checks remain at M6, M12, M18 and M24-03.

## Storage and residual risk

The [Local Storage Policy](LOCAL_STORAGE_POLICY.md) requires all controllable persistent project storage on E:. The M0 host used E-drive repository, caches, evidence and Docker WSL data; operating-system temporary activity on C: may be unavoidable. The historical PostgreSQL image scan reported one critical and 25 high findings in `gosu`; the accepted rootless image, API and web images passed new high/critical Trivy scans on the verified hardening SHA. The runtime was upgraded from .NET 8 to .NET 10. Later security, backup/restore and performance gates remain planned; all accessibility testing is reserved for M24-04 immediately before the final G24 release gate.

## Status update rule

Update this page whenever a milestone materially changes state. Record implemented paths, exact executed checks, failures, accepted limitations, and links to verification artifacts. Do not mark a capability verified based only on documentation or source inspection.

## Quarterly data compatibility policy

G0 does not depend on any retained local Docker volume. Fresh disposable database checks are required. Historical-volume compatibility moves to quarterly progress checkpoints at M6, M12, M18 and M24-03, using safe copies only; M19 separately owns comprehensive backup, restore and upgrade rehearsal. If no representative snapshot exists at a checkpoint, record NOT APPLICABLE. Breaking changes applied to retained data still require a safe-copy rehearsal before rollout.

