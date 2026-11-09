# M15 Executed Verification and Remaining Gate Evidence

**Latest executed M15 verification (2026-11-09):** [Run 42545005498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42545005498) on frozen `27d5d4ae81162bd3607cd35e5d3ec25425f61090` completed source PASS, selected PostgreSQL and M15 security-boundary PASS, Chromium 7/7 PASS and Firefox 7/7 PASS. WebKit completed 6/7 PASS; its final real-notification test timed out because the expected personal notice was absent from the first-page DOM. The cause is not proven from that run's logs. Source inspection revealed a credible test-fixture explanation: six mocked cases still made real first-paint API requests before their individual routes were installed, and the per-user `audit-read` budget is 30 requests per minute. New source installs mock list/count routes before navigation, completely excludes the real delivery case, runs that case first in each engine and records only HTTP status codes on failure. These changes remain **unexecuted on a new frozen SHA**. See [correctness audit](M15_CORRECTNESS_AUDIT_20261109.md). G15 acceptance pending. No merge or deployment.

**Evidence boundary (2026-11-09):** M15 [run 42542518598](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42542518598), source `40aa049b88afbcf29d1ce40a3792524cf93ba990`, passed source and PostgreSQL but failed Chromium 5/6 on an ambiguous receipt-label locator; Firefox/WebKit did not run. The latest feature-branch source has subsequent locator and pagination-cancellation fixes that have not completed a new three-engine campaign. The [combined M13/M15 run 42541135825](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) passed on a different earlier SHA, not this M15 source. M15-P01..P14, full G15-S01..S24 and independent S25 remain pending; no merge or deployment. [Correctness audit](M15_CORRECTNESS_AUDIT_20261109.md).

**Recorded:** 2026-11-09, Türkiye local date. Historical attempt rows remain source-specific; this register does not transfer combined-pass evidence to later M15 source.  
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
| [42542518598](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42542518598) | `40aa049b88afbcf29d1ce40a3792524cf93ba990` | Source PASS; 299 unit, 46 contract; PostgreSQL PASS 26 integration and 5 boundary; live delivery PASS; Chromium 5/6 | `Acknowledged` text locator matched dropdown option and receipt status; Firefox/WebKit NOT EXECUTED |
| [42545005498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42545005498) | `27d5d4ae81162bd3607cd35e5d3ec25425f61090` | Source PASS, targeted PostgreSQL PASS, Chromium 7/7 PASS, Firefox 7/7 PASS, WebKit 6/7 PASS | Last live WebKit notice not found in rendered first page within 10s; no HTTP status trace preserved; source isolation fix pending verification |

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

## Latest browser assertion failure and new source correctness audit

[Run 42542518598](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42542518598) passed all source and selected PostgreSQL checks, including the live M11-to-M15 notification fixture. Five of six Chromium tests passed. The last assertion of the personal receipt test matched both the dropdown option and rendered notification state. It is a Playwright strict-mode ambiguity after the receipt actions succeeded, not a failed business operation. Firefox and WebKit did not execute.

The [correctness audit](M15_CORRECTNESS_AUDIT_20261109.md) reviewed the associated source, fixed the ambiguous locator in `c5a03c42`, and found a separate real UI issue: an in-flight pagination request invalidated by refresh could leave the paging lock set indefinitely. The cleanup and a new browser race regression were committed in `26c858d3`. Neither repair has been verified by a new Actions run yet. All gate-level G15 evidence, policy approval and independent review remain pending.

## Latest browser-run failure analysis and pending response-limiter isolation

[Run 42545005498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42545005498) executed source `27d5d4ae81162bd3607cd35e5d3ec25425f61090`. Both source and PostgreSQL jobs succeeded. All seven Chromium and all seven Firefox browser scenarios passed, including live M11-to-M15 delivery and shipment navigation. WebKit passed six of seven; its last live scenario could not locate the already-seeded personal notification in the DOM after 10 seconds. The run did not record individual notification HTTP statuses, so the precise reason is **not confirmed**.

The per-user `audit-read` partition is limited to 30 requests in one minute (`AuthenticationSetup.cs`). Earlier mocked browser tests set up their notification routes after `signIn(page)`, allowing initial list and count requests from the workspace to hit the actual API. Repeating them across three consecutive engines can exhaust the shared user quota, especially on the last WebKit test. This is a supported hypothesis, not an observed HTTP 429 claim.

The test source now installs harmless first-paint list/count mocks in Playwright `beforeEach`, only for explicitly mocked scenarios, before opening the workspace. The separately identified live delivery test never installs those routes and runs first in each engine. Per-test routes override the fallback according to Playwright's registered-route precedence. The live scenario now collects only HTTP status codes and non-secret UI diagnostics if the notice is missing; never log cookies, response bodies or private browser fixture credentials. No production rate limit, authentication, tenant authorization or delivery policy is weakened.

Current remediation code commits include `92a70dd3` (pre-navigation isolation and status diagnostics), `a27834b5` (static source contract), and `f54ee9f6` (live test first). A complete new source-locked run of source, PostgreSQL, Chromium, Firefox and WebKit is required. Passing results from `27d5d4ae` cannot be inherited by this source. G15-S01 through S25 and M15-P01 through P14 remain unaccepted.
