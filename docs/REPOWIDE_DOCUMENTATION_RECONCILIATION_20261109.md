# Repository-Wide Documentation Reconciliation, 2026-11-09

**Latest verified technical campaign (2026-11-09):** [Run 42569970541](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42569970541) passed all 15 jobs at source `e4bf9cd1527f85f554baf623e90805d96d08ac1a`, with 487 passed TRX cases, zero failed/skipped, all six browser suites passing, and artifact integrity PASS. This is **not** full milestone acceptance. The final ledger reports G13 and G15 each 1 PASS, 22 BLOCKED and 2 NOT EXECUTED. The [source-locked evidence review](G13_G15_SOURCE_LOCKED_EVIDENCE_REVIEW_20261109.md) records outstanding plan, capacity, recovery and mutation proof. Merge, production migration, deployment and runtime activation remain pending.


**Branch:** `verify/g13-m13-m15-integration-20261109`  
**Accepted `main` baseline:** `eb529368a2447061fc1838cd3967b0ffedae669d`  
**Latest verified combined runtime:** `6b05d84bf2f01741b182669c2dd17e39e63d9dda`  
**Latest observed M13 feature-branch head at documentation audit:** `fe5d3396035012a4ccd97f16e9c244a78b9c4b3c` (documentation-only reconciliation after the independently tested source).  
**Latest observed M15 feature-branch head at documentation audit:** `27d5d4ae81162bd3607cd35e5d3ec25425f61090` (subsequent source correctness and documentation changes; the latest standalone run [42545005498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42545005498) on that source passed source/database, Chromium 7/7 and Firefox 7/7, but WebKit 6/7 with one missing live notification).  
**Disposition:** Documentation reconciliation only. Nothing here is a gate acceptance, merge, deployment, workflow authorization or permission-policy change.

## Verification anchors

| Workstream | Evidence and result |
| --- | --- |
| Combined M13/M15 | [Expanded run 42541135825](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) **PASS** at `6b05d84bf2f01741b182669c2dd17e39e63d9dda`: 312 unit, 52 contract, 32 selected real-role database, 5 security/fault, 27 Chromium/Firefox/WebKit. [Targeted run 42541135788](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135788) **PASS** on the same SHA. |
| Independent M13 | [Targeted run 42539385476](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539385476) **PASS** at `8175a28ac188b98f5b4cb3edaa7106b5d38ccb84`; [170,000-shipment run 42538812959](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42538812959), [nine source mutations 42537538212](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42537538212), [two security mutations 42539385498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539385498), [fault recovery 42537768249](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42537768249) PASS on their individually recorded SHAs. |
| Independent M15 earlier attempt | [Run 42542518598](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42542518598) at `40aa049b88afbcf29d1ce40a3792524cf93ba990`: source and PostgreSQL jobs PASS, Chromium 5/6 with one strict locator FAIL, Firefox/WebKit NOT EXECUTED. Earlier failed runs retained. |
| Accepted `main` | G0-G12 and G16 accepted; [workflow 42539391710](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539391710) failed due a historical Git SHA unavailable in shallow checkout, not an observed application test failure. |

**Most recent M15 attempt:** [Run 42545005498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42545005498), SHA `27d5d4ae81162bd3607cd35e5d3ec25425f61090`, source and PostgreSQL PASS; Chromium 7/7 PASS, Firefox 7/7 PASS, WebKit 6/7 with one failure on a live M11 notification row. This improves on the prior ambiguous-locator failure but is still a failed browser job and failed workflow. The root cause of the missing WebKit notice is not yet established. No new combined-source verification followed these independent M15 changes.

## Source and gate boundaries

- M13's revised signed report-member SELECT row-level security policy on the latest feature branch is **not** present on the last green combined source. Do not transfer the 170k performance or security result to this older combined policy. Reconcile and retest on a fresh combined SHA.
- M15 standalone browser fixture/cookie changes and subsequent corrections are not necessarily present on the green combined SHA. Its newest Chromium locator still fails. Preserve separate branches and do not report M15's own workflow green.
- The full approved 12+2 risk/exception occurrence ledger, role/session and report races, process crash before/after commit, approved p50/p95 capacity budgets, complete G13-S01 through S24 mapping and independent S25 remain open.
- M15 policy M15-P01..P14, full G15-S01 through S24 mapping and independent G15-S25 remain open. Default-off dispatcher and current business-read roles remain unchanged.
- Preserve M9 ARRI/DEPA processing hold and 10,000-item backlog limit, default-off M10 projector, null ETA and opt-in M11 evaluator. No production migration, merge or deployment.

## Link and source location correction

The historical M13 campaign document lives in the combined branch, but `.github/workflows/verify-m13.yml` exists on the independent M13 feature branch rather than this combined branch. Its verification link now points to the exact feature-branch workflow, avoiding an incorrect same-branch relative link. The combined branch instead has its own `.github/workflows/verify-g13-combined.yml` and `.github/workflows/verify-g13-combined-full.yml` workflows.

## Reviewed documentation and consistency checks

- The isolated combined branch documentation reconciliation modified **35 Markdown files** without changing its previously verified application source. These include README, STATUS, ROADMAP, PRD, requirements, architecture, domain model, API contracts, security, operations, acceptance, test strategy, M13/M15 architecture/implementation/verification plans and reports, and this register.
- On the independent M13 branch, **27 Markdown files** were reconciled after the prior M13 technical evidence register. Its application source and restricted security policy were not changed by the documentation work.
- On the independent M15 branch, the current change history since the original documentation checkpoint includes **28 Markdown files**, including its separately authored correctness audit. M15 application and checker changes were also made in independent source work and are not part of this documentation-only combined-branch reconciliation.
- A relative-link check of the current source-of-truth, verification and milestone-planning pages on the three branches found no remaining broken references in those inspected pages. One cross-branch link to the M13 workflow was corrected to target the independent M13 feature branch explicitly. Historical files are retained with their original frozen result and date; they are not converted to present-day PASS labels.
- No change was made to `docs/execution-graph.json` or its generated `EXECUTION_GRAPH.md`, because dependencies and package identifiers are unchanged. Both remain planning artifacts, not an observed current gate ledger. No planning graph regeneration or hosted checks were executed as part of the documentation-only commits.
- `main` remains unchanged, with accepted G0-G12 and G16. The current M13/M15 feature changes and green combined verification have **not** been merged or deployed. The separate existing main Actions checkout-history failure is not reinterpreted as a business regression or dismissed.

## Documentation handling

This reconciliation updated the current status and milestone planning documentation without rewriting historical acceptance source records. The independent M13/M15 feature branches retain separate histories, and protected `main` remains unchanged. Current and historical assertions are separated by the dated checkpoint links in each updated page.

The project maintains a distinction between living current-state documents (README, STATUS, ROADMAP, TEST_STRATEGY, ACCEPTANCE_CRITERIA and relevant operational/contract pages), delivery plans (M13/M15 architecture, implementation and verification plans and execution graph), and immutable historical source-locked evidence reports. Earlier FAIL, NOT EXECUTED and planning-only entries are retained when explicitly dated or contextualized; they must not overwrite this newer checkpoint. The execution graph remains a dependency plan, not an observed milestone-state or acceptance record.

[Current combined evidence](M13_M15_COMBINED_VERIFICATION_20261109.md) and [independent M13 evidence](M13_G13_EXPANDED_VERIFICATION_20261109.md) are authoritative for the run IDs above. Acceptance remains pending.
