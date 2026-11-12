# M15 Executed Verification and Remaining Gate Evidence

**Current integration state (2026-11-12):** G0-G12 and G16 accepted and merged. G13 and G15 are conditionally accepted and integrated through PR #38 and PR #41. G15 merge commit `47af1994b9958188666e5dde0aa6b5a536d7c55c`, verified implementation source `479616a5b7f17c494d897834a66d10b6276bb517`. Formal independent review attestation was not established for G15; unconditional S25 approval is not claimed. Notification delivery remains default-off and retention deletion and deployment remain unauthorized. [G15 post-merge reconciliation](G15_POSTMERGE_RECONCILIATION_20261112.md).

**2026-11-10 final G13/G15 review:** The [final source-locked run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED 15/15 jobs, 543 tests and 227/227 technical assertion witnesses at `15cabd8c542159753c927c2bd65b4346223e84f6`. The separate [formal gate decision](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_FORMAL_GATE_DECISIONS_20261110.md) is G13 NOT ACCEPTED and G15 NOT ACCEPTED following [S25 reviews](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_REVIEW_MATRIX_20261110.md). G13 permanent tenant capacity/idle cleanup enforcement and exact G13/G15 mutation/HTTP-outage proof remain pending. P10 conformance is supported; P12 deletion remains disabled. All earlier dates and SHAs in this file describe historical checkpoints only. No merge or production authorization.
**Source-locked successor checkpoint, 2026-11-10:** [G13/G15 workflow 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed 15/15 jobs on `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` (541 tests passed, 0 failed/skipped). The separate atomic audit found 224 technical witnesses, 3 missing, 7 policy-review and 8 independent-review assertions; G13 and G15 acceptance remain pending. [Current detailed status](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feature/g13-post-c3-remediation-20261111/docs/G13_G15_VERIFIED_EVIDENCE_20261110.md). Earlier source-specific reports below remain historical.

**Historical execution register:** The original tests, failures and source SHAs below are preserved without retroactive changes. The later 2026-11-10 technical campaign is recorded in the current source-locked evidence page.

**Recorded:** 2026-11-09, Türkiye local date.  
**Implementation branch:** `feat/m15-scoped-notifications-20261108`  
**Gate decision:** G15 acceptance pending; this document records executed technical checks and failed attempts, not a release decision.  
**Production disposition:** No merge, deployment, production database change or rollout.

## 1. Executed source-locked evidence

| Run | Source SHA | Observed result | Evidence boundary |
| --- | --- | --- | --- |
| [42526983899](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42526983899) | `e9064925c687904473319f72837d658794a1923a` | FAIL before compile, inherited historical Git commit absent in shallow checkout | Verification harness setup only |
| [42527214008](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42527214008) | `09debcbb800fb728e5a50aa4a5af49e073c9a98b` | FAIL on stale M11 planning assertion after M11 and M15 source-contract checks passed | Documentation gate mismatch |
| [42527425709](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42527425709) | `7a6e3c8aea51cba8293a5418c4cf65493d920a91` | FAIL on outdated reference to historical sixth M11 source audit | Historical checker mismatch |
| [42527741929](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42527741929) | `f7b5dea327d423b8b6eb0e958bfe7799080e96c6` | Source PASS; PostgreSQL FAIL: 3 M15 downgrade dependency errors and 1 overly broad queue-empty test assertion | Four findings recorded and remediated in later commits |
| [42528679584](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42528679584) | `8fc433d2d54687033e4a2d0de24b6f60f1847917` | FAIL on malformed source during an incomplete migration edit | Superseded by rebuild from last clean migration |
| [42528936587](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42528936587) | `b0831e34aae477f5a59b834ed2895f163bd0e3ad` | Source PASS; PostgreSQL PASS; browser FAIL before Playwright because disposable Compose project name was not allowlisted | Project-name fix committed later |
| [42530770493](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42530770493) | `82dac56bc00443cc022ecae0b38cd945d3db8f59` | Source PASS, database PASS, browser FAIL during fixture preparation before Playwright | Historical source/DB evidence, subsequently superseded by the successful run |
| [42541604417](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541604417) | `b1e79c826de71daea195250c00b46dcd7ab9a1a6` | Source and PostgreSQL PASS; browser FAIL on disabled notification receipt action | Preceded the next receipt-lock correction; earlier failed result retained |
| [42542518598](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42542518598) | `40aa049b88afbcf29d1ce40a3792524cf93ba990` | Source and PostgreSQL PASS; Chromium 5 PASS / 1 FAIL at strict `Acknowledged` locator; Firefox/WebKit NOT EXECUTED | Browser selector fix and same-source rerun required; do not mark G15 verified |
| [42547260912](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42547260912) | `bbf4ba38c9742f4e78b9d184d5b60e8aabf6a04c` | **All three jobs PASS:** 299 unit, 46 contract, 26 selected PostgreSQL, 5 separate security/fault, and Chromium/Firefox/WebKit each 7/7 | Standalone technical PASS; G15 acceptance remains pending |

### Measurements from run 42530770493

- Source identity confirmed by GitHub Actions: `82dac56bc00443cc022ecae0b38cd945d3db8f59`.
- .NET 10 warning-as-error Release build: **0 warnings, 0 errors**.
- HarborOps.UnitTests: **299 passed, 0 failed, 0 skipped**.
- HarborOps.ContractTests: **46 passed, 0 failed, 0 skipped**.
- Selected PostgreSQL/M11/M12/M15 integration: **26 passed, 0 failed, 0 skipped**, approximately 3 minutes 13 seconds of test execution. These are targeted tests, not the entire integration project.
- M11 and M15 source-contract checks: PASS. Generated graph check: PASS. Planning checker: PASS, 25 milestones, 126 graph nodes, 187 Markdown files.
- Web tests and production build: completed successfully in the source job.
- Browser job: FAIL before Playwright. The first browser blocker was the disposable Compose project naming rule; the next was a missing `.verification/evidence` folder required by `prepare-m5-browser-fixtures.mjs`. Fixes committed in later source revisions. **No browser test is claimed as passed on run 42530770493.**

### Evidence artifacts from that frozen run

- [Source build, unit and contract logs](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42530770493/artifacts/13637646035).
- [PostgreSQL integration logs and TRX](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42530770493/artifacts/13637512365).
- [Failed browser setup evidence](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42530770493/artifacts/13637143519).

These are hosted temporary artifacts; retention is seven days under the workflow configuration. Each run reports its own source SHA. Passing counts cannot be transferred to later edited source revisions.

## 2. Subsequent implementation source not yet run

The following changes postdate `82dac56`, and therefore **do not inherit its PASS**:

- Browser fixture evidence-directory initialization under the disposable Compose workflow.
- An atomic receipt replay snapshot in `m15_notification_replays` containing the original result version and read/ack timestamps.
- API replay response restoration from the original committed receipt result, instead of returning a potentially later personal state.
- Added focused regression that first marks a receipt read, acknowledges it separately, then replays the earlier MarkRead key and checks the original read-only snapshot.
- Additional replay snapshot source-contract assertions.

**Verification dependency:** A new source-locked Actions run on the final M15 commit is necessary for compile, migration and three-browser evidence. The connected dispatch service returned an account task-limit error when attempting to start the updated run; no successful new dispatch was reported after that error. The workflow is still manually dispatchable from the GitHub repository on the M15 branch. Do not infer a PASS from source inspection alone.

## 3. M13 parallel implementation boundary

M13's separate branch `plan/m13-operational-kpi-dashboards-20261108` advanced to `4e717bd45011b0a75b3d764b3f298bfcfb1d9056` during this work. It now contains KPI application code and an additive `20261109020000_M13OperationalKpis` migration. M15 uses provisional `20261109010000_M15ScopedNotifications`. These timestamps imply M15-before-M13 ordering on a future combined branch, but compatibility has **not** been validated on one combined source SHA.

Shared files requiring an explicit merge reconciliation include `src/HarborOps.Api/Program.cs`, `src/HarborOps.Api/Audit/AuditRoleProvisioner.cs`, `web/src/auth/OrganizationWorkspace.tsx`, current-head migration assertions in `MigrationSnapshotTests.cs` and `M11M12UpgradeTests.cs`, plus coordinated identity and API permissions. Keep M13's dedicated dashboard-read permission separate from M15's `ViewShipmentData` business-notification entitlement. M13 KPI results must not be derived from notification delivery or read state. Neither branch is merged.

## 4. Remaining G15 gate work

G15-S01 remains blocked on explicit dispositions of M15-P01 through M15-P14. G15-S25 requires independent review of executed raw evidence. G15-S02 through G15-S24 are **partially covered** by the executed checks above, not globally PASS. In particular, the full standalone three-engine browser campaign and selected PostgreSQL boundary checks now PASS at the recorded frozen source. Concurrent crash/recovery, expanded signed SQL scope mutations, pagination/cursor expiry and malicious replay coverage still require scenario-by-scenario reconciliation. Operational retention cleanup remains intentionally disabled while policy P12 is acceptance pending.

Preserve M9 ARRI/DEPA NeedsReconciliation hold, the 10,000 unresolved-item cap, default-off M10 projector, null ETA and opt-in M11 evaluator. Keep the M15 dispatcher default-off until explicitly approved after G15. Any acceptance, merge or deployment requires a separate decision.

## Subsequent M15 source delivery configuration and browser coverage

The implementation branch now exposes `HARBOROPS_M15_ENABLE_DELIVERY=false` in `.env.example` and `compose.yaml` (API-only). The isolated browser verification workflow explicitly sets it to `true` on an `ho-verify-*` project. A committed preparation script opens a real manual M11 evaluation, waits for a genuine M15 personal row from the hosted dispatcher and records source-locked disposable fixture IDs. Playwright checks the real notification UI and authorized shipment-exception navigation as well as mocked receipt, revocation and inaccessible-target scenarios.

The workflow now runs `M15NotificationBoundaryTests` separately from the 26 previously measured integration cases to protect their database-wide fault-injection constraints. The M15 restricted-role test now includes forged read-scope proof and worker-role negative probes. These changes are included in the standalone source-locked successful run 42547260912. This result does not satisfy unexecuted G15-S01..S25 obligations. No G15-S22, G15-S24 or new migration result is claimed from them.
