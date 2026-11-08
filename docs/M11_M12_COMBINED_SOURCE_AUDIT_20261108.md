# M11 and M12 Combined Source Reconciliation and Correctness Audit

**Latest hosted verification (2026-11-08):** [Executed M11/M12 run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records a successful .NET and frontend source gate, G11 18/18 scoped PostgreSQL tests, G12 2/2 exclusive PostgreSQL tests, 36/36 impacted integration regressions, and direct SQL isolation of exception, history and evaluation tables with forged-scope denial. Updated source/dependency and runtime-image security scans passed. The three-engine browser rerun passed 36/36 tests, with 12/12 each in Chromium, Firefox and WebKit, including M11 exception journeys, M12 search/export and targeted M5 regressions. G11-S08 conflict-specific provenance remains open; M12 local policy decisions and independent G11/G12 S25 reviews remain pending. No G11/G12 acceptance, merge or deployment is authorized. Historical source-only notes below are retained as previous checkpoints.

**Earlier hosted verification checkpoint (before SQL remediation):** [Hosted run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records successful corrected-source checks, G11 17/17 and G12 2/2 targeted PostgreSQL tests, dependency and runtime-image scans, and a confirmed **G11-S12 SQL read-isolation FAIL**. The current HTTP database login can read M11 exception rows without scoped-session authority. The browser and full integration campaigns remain in progress at the recorded checkpoint. Independent G11/G12 gate reviews and acceptance pending.

**Date:** 2026-11-08
**Branch:** `feat/m11-m12-combined-integration`
**Accepted main baseline:** `6ead1d076946dd96b53401265a31efc35c4a666c`
**M11 source input:** `020abbe9e8b79722eff9650aa4ddf382a8b655ec`
**M12 source input:** `24427e5543f1a9168fcba5e197a38b29516045e3`
**Source-review checkpoint before this audit record:** `60895dad2c2a82a3c6b2d963eafab1247d7eb0fb`
**Disposition:** Both source implementations and shared changes integrated on an isolated branch. Major M11 lifecycle policy defects addressed in source. **Targeted hosted correctness and security tests passed. Complete G11/G12 gate verification and independent review are incomplete; protected-main merge remains unauthorized.**

## 1. Integration inventory

- The branch starts at the verified accepted `main`, and its Git ancestry includes the original M11 and M12 source heads. Original branches and `main` remain unchanged.
- Both feature-specific migrations are present: `20261107180000_ShipmentExceptions` followed by `20261107230000_M12ShipmentExportAudit`, for 15 migrations total. EF discovery test and affected current-HEAD migration assertions were reconciled. Historical rollback counts were retained.
- `Program.cs` registers both feature services and maps both shipment exception and shipment search routes. `compose.yaml` carries M11 evaluator and M12 export-key configuration.
- `AuditRoleProvisioner.cs` includes both M11 signed evaluation/disposition grants and independent M12 export-audit proof-key provisioning. The M12 key is distinct from M10/M11 authority.
- The React shipment workspace includes both the exception panel and advanced search/CSV export without removing the accepted shipment detail and mutation route.
- Common living documents were merged, retaining independent M11 and M12 milestone contracts. No dependency edges changed.
- `scripts/check-g10-stack.mjs` recognizes 13, 14 and 15 migration states. `scripts/check-m11-contract.mjs` was updated for changed source predicates, fixed a premature test-variable reference and recognizes the M12 migration.
- A combined HTTP regression test source checks M11 exception, M12 search and accepted M5 shipment detail against one application; browser source tests cover Manager review of historical recorded lateness.

## 2. M11 local decisions and source remediation

The [M11 local policy contract](M11_LOCAL_CONTRACT_DECISION_20261108.md) records delegated M11-P01 through P12 decisions. These include 60-minute UTC arrival and departure tolerances, separate unverified schedule risk versus recorded workflow evidence, and Manager-only audited dispositions. This is a recorded **policy decision**, not G11-S01 PASS or independent acceptance.

| Concern | Source change | Evidence status |
| --- | --- | --- |
| Confirmed clearance followed by Unknown/Stale and renewed Active | Added durable `last_confirmed_inactive_at_utc`, preserved through uncertainty. Signed evaluator system-closes an acknowledged cleared occurrence and opens the next occurrence with a new identity. | Integration test source added, NOT EXECUTED |
| Acknowledged immutable `RecordedArrivalLate` cannot resolve | Signed SQL permits a Manager to close an acknowledged reviewed recorded-late fact without changing condition to Inactive. Preserves original history and central audit. | Integration and browser test source added, NOT EXECUTED |
| Repeat alert for unchanged reviewed historical arrival after source-version change | Signed evaluator checks prior human-resolved `RecordedArrivalLate` with equivalent arrival evidence and threshold before opening again. | Integration test source added, NOT EXECUTED |
| UI disposition conflict | Resolve is offered for reviewed recorded lateness and normal Acknowledged/Inactive only, with an explanatory historical-evidence label. | Browser test source added, NOT EXECUTED |
| Shared source integration | API, role provisioning, Compose, migrations, search, exception UI and accepted shipment interaction merged. | Source diff reviewed, compilation and runtime NOT EXECUTED |

## 3. Blocking source risks and unexecuted checks

| Finding | Severity | Required closure |
| --- | --- | --- |
| Earlier G11-S12 finding: inherited API SELECT could read M11 exception rows without a selected organization. | Remediated and tested on `2a855ad65b073ac4fc1140920ea06a680ab15377` | The M11 migration now enables RLS for exceptions, history and evaluation checkpoints. Authenticated HTTP reads use short-lived user/session-bound HMAC proofs, and due-work scanning uses a separate purpose-bound proof. The direct SQL probe and new actual-API-login/forged-scope tests passed. Independent gate review is still required. |
| M11 conflict-specific provenance remains incomplete: the M10 projection digest and bounded container sample are not a complete list of both conflicting source events, containers and corrections. | High | Trace conflict-specific retained event identities and correction state from authoritative M10 history, with bounded evidence and scoped SQL tests, before claiming G11-S08 completeness. |
| M11 stateless list cursor freezes first-detection cutoff, but existing lifecycle states can change while browsing. | Medium | The local policy now explicitly defines **live lifecycle status** alongside stable creation-cutoff/UUID pagination; the UI discloses this. A new integration test source covers an intervening disposition. Runtime concurrency execution is still mandatory. |
| No .NET 10 SDK, Docker, PostgreSQL or Playwright runtime was available to this connected source-review environment. No actual SQL migrations, compilations, restricted-login tests, browser runs, scanning or fault campaigns were executed. | Blocking evidence gap | Run the staged G11/G12 campaign on a clean frozen candidate with disposable E:-backed resources and raw outputs. |
| G12 policy decisions M12-P01 through P16 remain pending. | Policy blocker | Record explicit decisions separately from M11 and independent technical evidence. |
| Existing historical verification records describe previously accepted gates and cannot certify the new combined source. | Evidence guard | Preserve raw historical evidence and independently review new source-locked G11-S25 and G12-S25 packages. |

## 4. Exact gate disposition as of this source review

- G11-S01: local M11 policy disposition recorded; final gate reconciliation and acceptance pending.
- G11-S02 through G11-S25: NOT EXECUTED on the combined candidate.
- G12-S01: M12 policy disposition pending.
- G12-S02 through G12-S25: NOT EXECUTED on the combined candidate.
- No G11/G12 scenario has an executed PASS from this source integration review.
- The G12 server-scoped login-mutation test must run only on a dedicated disposable PostgreSQL server with `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1`, or report SKIPPED.
- Source review, docs edits, authored tests and successful commit creation do not prove correctness or gate acceptance.

## 5. Protection and operational invariants

The accepted M9 hold persists: ARRI/DEPA events parked, sustained transport processing held, 10,000 unresolved-work-item cap binding, M10 projector off by default and ETA null. The combined source must retain accepted M5 workflows and G10/G16 audit authority. No GitHub Actions, deployment, production database migration, transport-hold release or protected-main merge has occurred or is authorized by this document. Original feature branches remain intact.

**Source-integration result:** Both source candidates and shared surfaces are assembled. The M11 lifecycle decisions are implemented as source changes and tests are authored. **Final security remediation, runtime verification and gate acceptance remain open.**
