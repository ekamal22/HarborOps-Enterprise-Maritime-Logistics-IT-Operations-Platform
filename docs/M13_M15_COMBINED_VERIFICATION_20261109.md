# M13/M15 Combined Integration: Verification and Failure Register

**Recorded:** 2026-11-09.  
**Combined branch:** `verify/g13-m13-m15-integration-20261109`; remediation source under test `a6041a10869714b3f6613190a456568d835bf881`.  
**Base:** accepted `main` at `eb529368a2447061fc1838cd3967b0ffedae669d`.  
**Source integration:** independent M13 and M15 branches are preserved. M15 migration `20261109010000` precedes M13 `20261109020000` in this branch. This integration source has not been merged, deployed or accepted.

## Workflow evidence

| Exact run | Source | Observed result | Disposition |
| --- | --- | --- | --- |
| [42536116366](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42536116366) | `e70aa0398d1203c1bd22e89ebfe045ea1167925d` | Three targeted jobs PASS after resolving an xUnit2020 test assertion | Superseded by later integration changes, not a final combined gate |
| [42538543352](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42538543352) | `45190293978b83ca50adb67794b9b888250c007b` | Targeted source/database/M13 browser jobs PASS | Does not cover expanded M15 scenarios |
| [42538543438](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42538543438) | `45190293978b83ca50adb67794b9b888250c007b` | Expanded source PASS; Chromium browser FAIL on disabled notification Acknowledge and missing expected custom KPI period | The notification button lock was corrected in `04de955d4bc0cef51b6621e7543b66e731ad3426`. The KPI date-range behavior requires independent source-locked confirmation. |
| [42539272811](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539272811) | `04de955d4bc0cef51b6621e7543b66e731ad3426` | Targeted source/database/three-engine M13 journeys PASS | Only the narrow combined campaign |
| [42539272790](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539272790) | `04de955d4bc0cef51b6621e7543b66e731ad3426` | Expanded source PASS; database FAIL on three stale 16-migration assertions; Chromium FAIL on an ambiguous `Acknowledged` locator and a 1 November filtered-period expectation | The migration-count assumptions and locator were corrected in `a6041a10869714b3f6613190a456568d835bf881`. Filtered API response is now explicitly checked, not assumed. |
| [42540547877](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42540547877) | `a6041a10869714b3f6613190a456568d835bf881` | Targeted combined workflow in progress at this register's checkpoint; source/database jobs already PASS | Browser job still requires completed result |
| [42540547857](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42540547857) | `a6041a10869714b3f6613190a456568d835bf881` | Expanded combined workflow in progress at this register's checkpoint; source job PASS | Expanded database and browsers still require completed result |

An individual passed job is not a passed workflow. A workflow pass on an earlier SHA is not a pass on later edited source.

## Exact failure classes

**Migration head expectations.** The combined source has 17 migrations, not 16. `M10UpgradeTests` and `M11ExceptionRuntimeTests` were inherited with fixed 16-count assertions. These assertions caused the expanded PostgreSQL job failure at [42539272790](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539272790) despite the source building and many restricted-role tests executing. The corrected checks must be evaluated in a complete fresh/upgrade/rollback regression before G13-S18 or G15 migration acceptance.

**Chromium notification receipt transition.** The first expanded run exposed an action button remaining disabled because a successful notification refresh advanced the request serial before the action lock was cleared. The lock cleanup was corrected on this combined branch, and the later Chromium run got past that action. It then failed at a non-unique `getByText("Acknowledged")` selector matching both a filter option and the visible receipt state. The test now scopes its assertion to notification list rows.

**Chromium M13 filter period.** The expanded run showed the default report period beginning 2 November 2026 while the test requested the custom period beginning 1 November 2026. The next test version checks that the exact UTC query was sent, its HTTP response is 200, and the displayed period matches it. Do not relax the assertion to accept the default period or describe this as fixed until a source-locked three-engine test confirms it.

**Standalone M15 boundary.** [42539950914](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539950914) at `51920dcb25cbe02109683852d4dc217dbf104c71` passed M15 source and database jobs but failed the Chromium job when four scenarios could not reach the organization workspace after login. This is not the same as the combined-branch selector issue and must be investigated on the independent M15 branch.

**Unrelated main workflow.** [42539391710](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539391710) on the protected `main` SHA `eb529368...` failed before business regression because the documentation checker references historical commit `6ead1d076946dd96b53401265a31efc35c4a666c` absent from the shallow checkout. Correct the workflow checkout depth and rerun separately; it is not a demonstrated M13/M15 application regression.

## Remaining integration and acceptance work

1. Obtain and inspect final results and raw artifacts for runs [42540547877](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42540547877) and [42540547857](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42540547857). If either is red, fix only the reported source or harness defect, then re-run against the new SHA. Do not suppress scenarios or treat skipped tests as PASS.
2. Synchronize the combined branch with the *latest* M13 implementation and M15 implementation, including any security-sensitive report member RLS changes and M15 replay/browser fixes that advanced after the initial integration. Preserve both feature branches. Verify the exact final combined SHA again.
3. Run and reconcile all M13 and M15 selected contract, restricted-role SQL, migration upgrade/rollback, security-negative and Chromium/Firefox/WebKit journeys, plus the separate large-tenant G13 volume check on the final M13 source.
4. Confirm full G13 reference ledger K01-K14, M11 rule and occurrence source membership, error/rollback/crash behavior, approved numerical budgets and audit integrity. Supplemental fixture coverage and partial green workflows do not close S01-S24.
5. Close G13-S25 only after **independent** review of immutable raw evidence and an explicit acceptance decision. G15 has its own policy approvals and independent review. Never infer either milestone's acceptance from this integration work.

**Operational protections:** Keep M9 ARRI/DEPA processing held, 10,000 unresolved-work cap, default-off M10 projector, null ETA, opt-in M11 evaluator and default-off M15 notification dispatcher. No main merge, production migration, deployment or release authorization.
