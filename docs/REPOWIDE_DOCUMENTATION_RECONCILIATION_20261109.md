# Repository-Wide Documentation Reconciliation, 2026-11-09

**Branch:** `verify/g13-m13-m15-integration-20261109`  
**Accepted `main` baseline:** `eb529368a2447061fc1838cd3967b0ffedae669d`  
**Latest verified combined runtime:** `6b05d84bf2f01741b182669c2dd17e39e63d9dda`  
**Latest observed M13 feature-branch head:** `38abfb37d5a5677c8b7db8364fa7477aa2f7d7f9` (documentation only after its recorded test source).  
**Latest observed M15 feature-branch head:** `40aa049b88afbcf29d1ce40a3792524cf93ba990` (standalone verification failed its browser job).  
**Disposition:** Documentation reconciliation only. Nothing here is a gate acceptance, merge, deployment, workflow authorization or permission-policy change.

## Verification anchors

| Workstream | Evidence and result |
| --- | --- |
| Combined M13/M15 | [Expanded run 42541135825](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) **PASS** at `6b05d84bf2f01741b182669c2dd17e39e63d9dda`: 312 unit, 52 contract, 32 selected real-role database, 5 security/fault, 27 Chromium/Firefox/WebKit. [Targeted run 42541135788](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135788) **PASS** on the same SHA. |
| Independent M13 | [Targeted run 42539385476](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539385476) **PASS** at `8175a28ac188b98f5b4cb3edaa7106b5d38ccb84`; [170,000-shipment run 42538812959](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42538812959), [nine source mutations 42537538212](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42537538212), [two security mutations 42539385498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539385498), [fault recovery 42537768249](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42537768249) PASS on their individually recorded SHAs. |
| Independent M15 | [Run 42542518598](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42542518598) at `40aa049b88afbcf29d1ce40a3792524cf93ba990`: source and PostgreSQL jobs PASS, Chromium 5/6 with one strict locator FAIL, Firefox/WebKit NOT EXECUTED. Earlier failed runs retained. |
| Accepted `main` | G0-G12 and G16 accepted; [workflow 42539391710](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539391710) failed due a historical Git SHA unavailable in shallow checkout, not an observed application test failure. |

## Source and gate boundaries

- M13's revised signed report-member SELECT row-level security policy on the latest feature branch is **not** present on the last green combined source. Do not transfer the 170k performance or security result to this older combined policy. Reconcile and retest on a fresh combined SHA.
- M15 standalone browser fixture/cookie changes and subsequent corrections are not necessarily present on the green combined SHA. Its newest Chromium locator still fails. Preserve separate branches and do not report M15's own workflow green.
- The full approved 12+2 risk/exception occurrence ledger, role/session and report races, process crash before/after commit, approved p50/p95 capacity budgets, complete G13-S01 through S24 mapping and independent S25 remain open.
- M15 policy M15-P01..P14, full G15-S01 through S24 mapping and independent G15-S25 remain open. Default-off dispatcher and current business-read roles remain unchanged.
- Preserve M9 ARRI/DEPA processing hold and 10,000-item backlog limit, default-off M10 projector, null ETA and opt-in M11 evaluator. No production migration, merge or deployment.

## Documentation handling

This reconciliation updated the current status and milestone planning documentation without rewriting historical acceptance source records. The independent M13/M15 feature branches retain separate histories, and protected `main` remains unchanged. Current and historical assertions are separated by the dated checkpoint links in each updated page.

The project maintains a distinction between living current-state documents (README, STATUS, ROADMAP, TEST_STRATEGY, ACCEPTANCE_CRITERIA and relevant operational/contract pages), delivery plans (M13/M15 architecture, implementation and verification plans and execution graph), and immutable historical source-locked evidence reports. Earlier FAIL, NOT EXECUTED and planning-only entries are retained when explicitly dated or contextualized; they must not overwrite this newer checkpoint. The execution graph remains a dependency plan, not an observed milestone-state or acceptance record.

[Current combined evidence](M13_M15_COMBINED_VERIFICATION_20261109.md) and [independent M13 evidence](M13_G13_EXPANDED_VERIFICATION_20261109.md) are authoritative for the run IDs above. Acceptance remains pending.
