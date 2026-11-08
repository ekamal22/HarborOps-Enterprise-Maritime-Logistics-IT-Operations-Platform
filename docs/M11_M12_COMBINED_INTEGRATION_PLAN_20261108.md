# M11 and M12 Combined Integration and Verification Plan

**Decision date:** 2026-11-08
**Decision:** The combined-source integration and coordinated verification strategy is approved. This is a planning decision, not permission to execute integration, run tests, approve policies, merge or deploy.
**Accepted baseline:** Protected `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`, with G0 through G10 and G16 accepted.
**Inputs:** Draft M11 PR #31 (`feat/m11-delay-exception-lifecycle`), draft M12 PR #32 (`feat/m12-shipment-search-export`), and separate M11 documentation-only PR #30.
**Execution status:** No combined implementation branch, combined frozen SHA, G11/G12 campaign, independent evidence review or gate acceptance is established.

## 1. Approved orchestration and preserved boundaries

Prepare one new integrated candidate from the latest verified accepted `main`, reconcile both feature sources, and execute one coordinated verification campaign sharing compatible test setup, disposable infrastructure and regression fixtures. Keep **separate G11-S01..S25 and G12-S01..S25** evidence matrices, independent S25 reviews and independent gate acceptance decisions. Never infer one gate's PASS from another's PASS.

This approval is not acceptance of M11-P01..P12, M12-P01..P16 or M11's provisional 60-minute UTC grace rule. **G11-S01 remains BLOCKED; G11-S02..S25 remain NOT EXECUTED. G12-S01 policy disposition is pending; G12-S02..S25 remain NOT EXECUTED.** Source-authored tests and prior static audits do not establish executable PASS.

M11 depends on accepted G10. M12 depends on accepted G5 and does **not** depend on G11. M13 needs G11 and G12; M15 needs G11. No changes to `docs/execution-graph.json`, generated graph or acceptance hierarchy are approved or necessary. Preserve the original M11 and M12 branches and their history. The strategy alone does not authorize creating a combined branch, running GitHub Actions or making production changes.

## 2. Future integration sequence

1. Revalidate live protected `main`, both PR heads, exact merge bases, accepted evidence and migration inventory. Treat all recorded SHAs as snapshots until independently confirmed.
2. Under a separate integration instruction, start an isolated candidate branch from verified accepted `main` while leaving M11, M12 and protected `main` intact. Record the exact imported source revisions and all manual conflict resolutions.
3. Reconcile `src/HarborOps.Api/Program.cs`, `src/HarborOps.Api/Audit/AuditRoleProvisioner.cs`, API route bindings, tenant authorization, SQL verifier/grant provisioning, EF model snapshot and migration discovery, `compose.yaml`, API/worker configuration, shipment React workspace, test fixtures and browser scripts.
4. Preserve M11 evaluation, immutable exception history, Manager dispositions and default-off bounded reevaluator. Preserve M12 scoped live search, protected cursor, authorized safe CSV and atomic export-preparation audit. Neither feature may displace accepted M5 shipment list, detail, edit or navigation behavior.
5. Keep M11 signed SQL evaluation/disposition authority distinct from M12's separate `HARBOROPS_M12_EXPORT_PROOF_KEY`. Do not reuse signing keys, widen effective SQL grants, relax live role/session checks, omit required M16 audit or expose audit verifier keys to lower-privilege runtime roles.
6. Run fast change-impact compilation and contract checks before expensive database and browser campaigns. Repair problems, preserve unsuccessful attempts and freeze a new SHA after source changes. A superseded SHA cannot certify final source.

### Migrations and database rules

The accepted baseline has **13 migrations**. M11 adds `20261107180000_ShipmentExceptions`; M12 adds `20261107230000_M12ShipmentExportAudit`. Each standalone branch expects migration 14. The fully reconciled combined candidate must discover **15** in timestamp order, including a coherent EF model snapshot. Verify clean installation and populated **13-to-15 upgrade** with database integrity, restricted SQL grants, audit proof provisioning and historical data preservation. Adjust migration-count assertions only after confirming real SQL behavior and compatibility. Preserve M12's downgrade refusal after audited exports.

Use disposable E:-backed PostgreSQL resources, not production or retained databases.

### Known issues that integration must not hide

M11 still requires decisions or remediation for Unknown/Stale recurrence, immutable `RecordedArrivalLate` versus clear-only Manager resolution, complete conflict provenance, inherited broad API SELECT grants, and list pagination across mutable lifecycle state. M12 still requires actual restricted-role SQL verification, timeout and cancellation measurements, M5 browser compatibility, and Q2 historical-snapshot evidence or justified NOT APPLICABLE. Neither set of open issues disappears because the feature branches are combined.

## 3. Coordinated source-locked verification tiers

| Tier | Checks | Efficiency and isolation |
| --- | --- | --- |
| T0: source preflight | Exact diffs, secret/config/permission review, migration graph and model snapshot | Stop on unsafe conflicts before constructing long runs. |
| T1: quick executable | .NET Release warning-as-error build, frontend typecheck/lint, targeted unit and contract tests | Share setup and run independent suites in parallel where safe. |
| T2: migration and SQL authority | Fresh 15-migration install, accepted 13-to-15 populated upgrade, real grants, distinct M11/M12 proof checks, downgrade safety | Run against disposable PostgreSQL; stop before downstream tests on failure. |
| T3: functional/security | G11 rule truth tables, states, provenance, history, replay and concurrency; G12 filters, pagination, CSV, role matrices and signed audit | Reuse safe fixtures, but reset mutable state between conflicting cases. |
| T4: timing/faults | Slow SQL, 25-second M12 budgets, 30-second proxy envelope, cancellation, 5,000-row and 10-MiB caps, outage, restart, crash boundaries and EXPLAIN | Measure outcomes; serialize destructive shared-resource tests. |
| T5: browser | Shared three-engine Playwright run on combined UI: M11 exception panel, M12 search/export and accepted M5 draft/edit behaviors | Reuse a common stack; avoid gratuitous whole-suite duplication. |
| T6: impacted regressions | Relevant M2/M5/M6/M8/M9/M10/M16 paths, signed authority, M9 hold, effective security mutants and scans | Map each check to the impacted gate scenario and retain negative controls. |
| T7: independent review | Reparse raw reports and manifest, review failures, cross-check exact SHA and separate gate matrices | Independent G11-S25 and G12-S25 dispositions remain distinct. |

**Exclusive PostgreSQL requirement:** `RestrictedRuntimeExportIsScopedAndAudited` changes PostgreSQL logins at server scope. It must execute only on a **dedicated disposable PostgreSQL server**, not just a separate database on a shared server. Set `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1` only in that exclusive environment. Without it, the case is **SKIPPED**, never PASS. Internal xUnit serialization does not isolate external processes; no concurrent server-scoped credential tests or unrelated shared-server consumers are permitted during this stage.

The coordinated campaign can reuse evidence for both gates only where the same frozen-SHA run actually exercises **both** sets of predicates. Explicit cross-gate mappings include G11-S02/G12-S02 compilation and contracts; G11-S11/G12-S18 migration and SQL authority; G11-S12/S13/S19 with G12-S11/S12/S18 restricted roles and atomic audit; G11-S18/S21 with G12-S13/S19 revocation, cancellation and outage; G11-S22/G12-S22 three-engine browser; G11-S23/G12-S23 accepted regressions and M9 hold; G11-S24/G12-S24 effective security mutants; and separate G11-S25/G12-S25 reviews.

Do not collapse the two 25-scenario ledgers. G11-S01 and G12-S01 require actual policy decisions. An independently acceptable G12 does not override a blocked G11 or justify merging the combined branch into protected `main` while the other gate remains unresolved.

## 4. Mandatory M9 operating restriction

ARRI/DEPA transport events remain parked for reconciliation. Sustained M9 transport-event processing is held, the 10,000 unresolved-work-item cap binds, the M10 projector is disabled by default and ETA remains null. M11 automatic evaluation and M12 search/export may **not** release this hold, assert physical arrival from incomplete records or infer predictive ETA. Explicit combined-source assertions must support G11-S23 and G12-S23.

## 5. Evidence, acceptance and release boundaries

Freeze the final candidate SHA and create one consolidated E:-backed evidence root with separate G11 and G12 ledgers. Retain exact commands, tool versions, UTC times, fixture/database identity, role settings without secrets, raw xUnit/TRX, browser reports, PostgreSQL migration and privilege probes, fault/latency measurements, scans, mutant outcomes, all failed or skipped attempts, and SHA-256 manifest. A PASS must cite a successful executed check against the required frozen SHA. Source review or written tests alone are insufficient.

Where fixes change source after an earlier run, log a new source SHA and rerun the impacted checks plus any required final gate coverage. Give every scenario an explicit PASS, FAIL, BLOCKED, NOT EXECUTED, SKIPPED or evidenced NOT APPLICABLE disposition. Only after separate independent G11-S25 and G12-S25 reviews and policy decisions may gate acceptance be considered. A merge, production database update, Actions execution, transport-hold clearance or release requires separate authorization.

All local worktrees, caches, databases, browser fixtures and evidence must be persistent only on E:. Short-lived unavoidable C: temporary activity is permitted. Never delete retained evidence, volumes or unrelated repositories as a cleanup shortcut.

## 6. Authoritative sources

- [Current parallel source-status register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md)
- [Roadmap](ROADMAP.md), [Execution graph](execution-graph.json), [Acceptance criteria](ACCEPTANCE_CRITERIA.md), [Test strategy](TEST_STRATEGY.md), [Status](STATUS.md) and [Parallel delivery policy](PARALLEL_DELIVERY_POLICY.md)
- M11: [architecture](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m11-delay-exception-lifecycle/docs/M11_ARCHITECTURE_DESIGN.md), [source status](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m11-delay-exception-lifecycle/docs/M11_IMPLEMENTATION_STATUS.md), [G11 plan](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m11-delay-exception-lifecycle/docs/M11_VERIFICATION_PLAN.md)
- M12: [architecture](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m12-shipment-search-export/docs/M12_ARCHITECTURE_DESIGN.md), [source status](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m12-shipment-search-export/docs/M12_IMPLEMENTATION_STATUS.md), [G12 plan](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m12-shipment-search-export/docs/M12_VERIFICATION_PLAN.md)

**Current disposition:** Approved coordination plan only. Original branches remain separate; no combined implementation, G11/G12 executable result, gate acceptance, merge or deployment is claimed.
