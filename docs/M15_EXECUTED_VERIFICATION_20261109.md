# M15 Executed Verification and Remaining Gate Evidence

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
| [42530770493](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42530770493) | `82dac56bc00443cc022ecae0b38cd945d3db8f59` | Source PASS, database PASS, browser FAIL during fixture preparation before Playwright | Earlier source/DB evidence |
| [42539491364](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539491364) | `1cad2e8128cc1799fdb229e20551cd3cb49db6c3` | FAIL compiling tests: xUnit2020 rejected `Assert.True(false,...)` in helper | Fixed by `Assert.Fail(...)`; DB/browser skipped |
| [42539950914](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539950914) | `51920dcb25cbe02109683852d4dc217dbf104c71` | Source PASS, PostgreSQL PASS, browser FAIL after 2/6 Chromium tests; Firefox and WebKit not reached | Browser login limit exhausted during fixture and per-test login sequences |
| [42541604417](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541604417) | `b1e79c826de71daea195250c00b46dcd7ab9a1a6` | Source PASS, PostgreSQL PASS, live M11/M15 fixture PASS, Chromium 5/6 PASS and 1 timeout | UI `change()` left `busyId` set after `refresh(true)` advanced the serial counter; Firefox and WebKit not reached |

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

## 2. Latest source-locked browser findings and remediation

[Run 42541604417](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541604417) exercised frozen `b1e79c826de71daea195250c00b46dcd7ab9a1a6`: source/build/tests passed, and the isolated PostgreSQL suite passed. Live M11-to-M15 delivery and personal recipient fixture passed; Chromium passed neutral rendering, administrator exclusion, revocation, inaccessible navigation and live exception navigation (5/6). The personal MarkRead-to-Acknowledge browser test alone timed out at 30 seconds. Firefox and WebKit did not execute.

Source inspection identified a real UI state-machine defect: receipt `change()` stored a `serial.current` token, then called `refresh(true)`, which increments that token. In `finally`, the old token check prevented clearing `busyId`. This left the Acknowledge control disabled after a successful MarkRead, creating an indefinite disabled-control wait in Playwright. Remediation at `464ac5728c4cc210eec4239e0bbcdc10d3786a58` clears the lock only when the mounted component still owns the matching notification ID. The browser regression now explicitly requires the Acknowledge button to become enabled after MarkRead. This is not a login-limit workaround, and production permissions/rate limits are unchanged.

**Latest source has not been re-executed.** A fresh complete source-locked campaign must pass all three jobs, including Chromium, Firefox and WebKit, before closing technical verification. Passing results from either frozen predecessor do not transfer to the patched revision.

## 3. Earlier measured run and source boundary

[Source-locked Actions run 42539950914](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539950914), revision `51920dcb25cbe02109683852d4dc217dbf104c71`:

- Source job **PASS**: warning-as-error .NET build, 299/299 unit tests, 46/46 contract tests, frontend tests/build and M15 static contracts.
- PostgreSQL job **PASS**: 26/26 selected integration tests, then 5/5 separately executed M15 security/fault-injection boundary tests. No skips.
- Disposable end-to-end M11 event, opt-in M15 dispatcher, personal recipient and scoped API fixture: **PASS**.
- Browser job **FAIL**: Chromium passed 2 of 6 specs. Four specs failed before their assertions while signing in. Firefox and WebKit were not reached. The 5-minute login source limiter allows 20 attempts, and the M3, M4, M5 and M15 preparation scripts together perform 18 successful HTTP logins before Playwright starts; two browser tests then logged in successfully. Subsequent login attempts in the same window could not reach the organization workspace. This is consistent with the configured rate-limit boundary. Production login limits have **not** been relaxed.
- Raw evidence: [source job](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539950914), [PostgreSQL artifacts](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539950914/artifacts/13641535251), [browser artifact](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539950914/artifacts/13640803911).

Subsequent source revisions preserve the authenticated manager session already created during the real M15 fixture, create one additional authenticated administrator snapshot, and reuse these exact server-issued cookies across isolated Playwright contexts and the three browser engines. The source copies the original cookie attributes, stores the session snapshots only in `.verification/.private` (excluded by `.gitignore`), never uploads them as artifacts and deletes them from the disposable runner on exit. Browser tests fail closed if the required session snapshots are absent. The static contract was updated to require this fixture pattern.

**These changes are not yet executed on the latest source SHA.** Do not transfer the earlier passing test counts to this source or mark G15 complete until a final source-locked run passes all jobs.

## 4. M13 parallel implementation boundary

M13's independent branch `plan/m13-operational-kpi-dashboards-20261108` is at `e5a7f70219bd9ad5aa5f4eccc0d894fffcda2127` at this checkpoint. An isolated integration verification branch also exists at `verify/g13-m13-m15-integration-20261109` (`6b05d84bf2f01741b182669c2dd17e39e63d9dda`). Its separate gate status is not imported into M15 acceptance. It now contains KPI application code and an additive `20261109020000_M13OperationalKpis` migration. M15 uses provisional `20261109010000_M15ScopedNotifications`. These timestamps imply M15-before-M13 ordering on a future combined branch, but compatibility has **not** been validated on one combined source SHA.

Shared files requiring an explicit merge reconciliation include `src/HarborOps.Api/Program.cs`, `src/HarborOps.Api/Audit/AuditRoleProvisioner.cs`, `web/src/auth/OrganizationWorkspace.tsx`, current-head migration assertions in `MigrationSnapshotTests.cs` and `M11M12UpgradeTests.cs`, plus coordinated identity and API permissions. Keep M13's dedicated dashboard-read permission separate from M15's `ViewShipmentData` business-notification entitlement. M13 KPI results must not be derived from notification delivery or read state. Neither branch is merged.

## 5. Remaining G15 gate work

G15-S01 remains blocked on explicit dispositions of M15-P01 through M15-P14. G15-S25 requires independent review of executed raw evidence. G15-S02 through G15-S24 are **partially covered** by the executed checks above, not globally PASS. In particular, the complete three-engine browser campaign, concurrent crash/recovery, signed SQL scope mutations, pagination/cursor expiry and malicious replay cases require execution and review on a final frozen SHA. Operational retention cleanup remains intentionally disabled while policy P12 is acceptance pending.

Preserve M9 ARRI/DEPA NeedsReconciliation hold, the 10,000 unresolved-item cap, default-off M10 projector, null ETA and opt-in M11 evaluator. Keep the M15 dispatcher default-off until explicitly approved after G15. Any acceptance, merge or deployment requires a separate decision.

## Subsequent M15 source delivery configuration and browser coverage

The implementation branch now exposes `HARBOROPS_M15_ENABLE_DELIVERY=false` in `.env.example` and `compose.yaml` (API-only). The isolated browser verification workflow explicitly sets it to `true` on an `ho-verify-*` project. A committed preparation script opens a real manual M11 evaluation, waits for a genuine M15 personal row from the hosted dispatcher and records source-locked disposable fixture IDs. Playwright checks the real notification UI and authorized shipment-exception navigation as well as mocked receipt, revocation and inaccessible-target scenarios.

The workflow ran `M15NotificationBoundaryTests` separately from the 26 targeted integration cases. Both suites passed on frozen SHA `51920dcb`. The restricted-role suite includes forged read-scope proof, worker-role denials, and rollback of failed audit/history. These results do not certify subsequent cookie-reuse changes or the remaining G15 scenarios.
