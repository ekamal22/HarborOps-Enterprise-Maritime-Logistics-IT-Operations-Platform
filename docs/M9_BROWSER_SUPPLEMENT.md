# M9 G9-S22 Browser Regression Supplement

**Status:** Supplemental technical evidence, executed. **Result: PASS** (final run). It closes the browser requirement of G9-S22 and does not replace or alter the attempt 08 campaign in the [executed verification record](M9_EXECUTED_VERIFICATION.md). G9 acceptance pending.

**Implementation source:** `0abeeb0e0eca3f639ef0036483f6cfc6e6e65440`, the exact revision of attempt 08, built from a clean detached worktree (`E:\HarborOps-Verification\worktrees\m9-browser-0abeeb0`).  
**Tooling:** `scripts/verify-g9-browsers-local.sh`, `scripts/run-g9-browsers-local.sh`, `scripts/summarize-g9-browsers.mjs` and `scripts/redact-g9-browser-artifacts.mjs` at `1ee653fc37a7c6e6c60a4a96a73d4b304fa582ed`. Everything the stack and specs execute (`src`, `web`, `tests`, `compose.g9.verify.yaml`, `docker` and the fixture scripts) is byte-identical between the implementation revision and the tooling revision; the diff is recorded in the evidence (`source-vs-tooling-diff.txt`, empty).  
**Why it was needed:** G9-S22 requires the existing M0 to M8 browser journeys. Attempt 08 ran the .NET suites, the stack end-to-end check and the frontend tests but not Playwright.

## Browser inventory

All 16 specs under `tests/browser/specs` run in Chromium, Firefox and WebKit (Playwright 1.64.1; chromium 1248, firefox 1555, webkit 2370), one worker, API restarted before each spec run so login rate limits are not shared. No spec was skipped or replaced. Fixtures are the accepted M3 to M6 browser fixture scripts; accounts and organizations are provisioned with the same synthetic names the accepted G8 runner used. The stack is the isolated G9 Compose project including the restricted-role worker.

| Spec | Milestone | Tests per engine | Fixtures | Assertions (from the recorded test titles) |
| --- | --- | --- | --- | --- |
| `baseline` | M0 | 3 | none | Baseline loads without unrelated calls, refresh reloads it, an unavailable API is shown and later recovers |
| `regression` | M0 | 1 | none | Rapid refresh ends with current data and no unrelated network failures |
| `auth` | M1 | 2 | provisioned operator account | Sign in, survive reload, log out; an incorrect credential is denied generically |
| `organizations` | M2 | 1 | M2 accounts and organizations | Selection, reload, foreign identifier and last-admin denial |
| `organization-revocation` | M2 | 1 | M2 accounts and organizations | Membership switching and live revocation clear protected state |
| `references` | M3 | 4 | M3 fixtures | Manager port and vessel create, edit, search, archive and restore; Analyst read-only; organization switch discards rows |
| `reference-paging` | M3 | 1 | M3 fixtures | Scoped pagination beyond 25 results without duplicates |
| `reference-races` | M3 | 5 | M3 fixtures | Delayed old-tenant response, dependency failure then recovery, logout and revocation during requests, reload persistence |
| `reference-live-revocation` | M3 | 1 | M3 fixtures | Revocation removes management controls using real HTTP permissions |
| `maritime` | M4 | 4 | M4 fixtures | Manager container lifecycle, Analyst read-only, tenant switch, voyage progress and archive restoration |
| `maritime-gate` | M4 | 3 | M4 fixtures | Tenant-safe pagination, logout during a delayed response, invalid identifier recovery |
| `maritime-races` | M4 | 5 | M4 fixtures | Delayed old-tenant response, failed-request recovery, role revocation, parent catalog failure, first-load tab selection |
| `shipment` | M5 | 8 | M5 fixtures | Analyst create and advance, Auditor read-only, tenant change, Manager terminal workflows, catalog retry, stale edit conflict, deferred response, logout |
| `shipment-draft` | M5 | 8 | M5 fixtures | Local draft survives leaving the tab, a background refresh, a server change and a stale save; dropped on organization switch, revocation and logout; archive and restore |
| `assignment` | M6 | 10 | M6 fixtures | Attach container and voyage, organization switch, Auditor and Support scoping, tab return, logout, conflict error, deferred response, live downgrade, shipment switch |
| `tracking-ingress` | M7 and M8 | 1 | none | The integration route is proxied and rejects requests without source credentials |

Each spec is applicable to the current implementation because M9 shares the API, database and authorization surface with every earlier journey. The M8 ingress and compose outage checks that the accepted G8 runner executed as separate scripts are covered for M9 by the attempt 08 stack check (ingest, replay, outage with recovery, worker survival), which runs on the same G9 stack; they are not repeated here.

## Execution history

| Run | Result | Evidence |
| --- | --- | --- |
| Supplement attempt 01 | **FAILED.** Chromium 16 of 16 specs and Firefox 16 of 16 passed; WebKit 13 of 16 passed (55 of 58 tests). One test each failed in `maritime` (test 4), `assignment` and `shipment-draft` (test 3). | `E:\HarborOps-Verification\evidence\g9-browser-supplement-01` (preserved unchanged; archive quarantined by the safety scan, see below) |
| Development diagnostics | Not evidence. Failure not reproduced. | `E:\HarborOps-Verification\evidence\g9-browser-diag-01` (summary in `DIAGNOSTIC-SUMMARY.md`) |
| Supplement attempt 02 | **PASS.** 48 of 48 spec runs, 174 test executions (58 per engine), 0 failed, 0 skipped, 0 flaky. | `E:\HarborOps-Verification\evidence\g9-browser-supplement-02\m9b` |

Final run id `20261105T075247Z-706e4b87db6ff9f9`, 2026-11-05T07:52:47Z to 08:08:46Z. Archive `archives\20261105T075247Z-706e4b87db6ff9f9\g9-browsers-0abeeb0e0eca3f639ef0036483f6cfc6e6e65440-20261105T075247Z-706e4b87db6ff9f9-evidence.tar.gz`, SHA-256 `a54358588b2d30c42e550ec87a014f9187985a35da360427c4d74423231e934d`, 126 files. The runner compared the manifest with the extracted archive and scanned it for secrets; the manifest check was repeated independently with `sha256sum -c` on an extracted copy (126 of 126 verified). Teardown left zero containers, volumes or networks.

## The WebKit login-step failures (attempt 01)

All three failures were at the login step in WebKit: the page loaded, the session check returned 401, the baseline loaded, and no login request was ever sent. The failure page snapshots show the Email field empty and the Password field filled, which makes the browser block the submit. Web and API logs show no error. The login form stores its fields in one component's state and nothing in the application clears the email other than its own change handler.

Root cause was **not proven**. Development diagnostics against the same source could not reproduce it: 16 plain login probes, 24 probes using the helper's exact action sequence with a page-side event recorder, 24 of the same with the real configuration's trace and screenshot settings, and 20 runs (about 120 tests) of the real `maritime` and `shipment-draft` specs with the recorder, all passed. The final full unmodified rerun passed all 58 WebKit tests. Across the two complete runs, 3 of 116 WebKit test executions failed, all at this step, and none of the 232 Chromium and Firefox executions failed.

No test was changed, retried or skipped to obtain the pass. The residual risk is an intermittent WebKit login-step failure at roughly 2 to 3 percent of WebKit tests in this Windows environment, most likely a Playwright WebKit interaction rather than the application; the independent reviewer should weigh that. A suggested hardening (asserting the field value before submitting) is recorded in the [remediation register](M9_CORRECTNESS_REMEDIATION.md) and was deliberately not applied to the accepted journeys.

## Evidence safety

The attempt 01 safety scan correctly refused to archive: the failure page snapshots contain the disposable account password typed into the form. Attempt 01 was left unchanged and unarchived. The tooling now keeps the raw failure artifacts (page snapshots and traces) in a private run directory, replaces them in the shareable evidence with redacted copies or stubs, and lists every action with before and after hashes in `redactions.json`. The final run had no failures, so `redactions.json` is empty and the archive contains no credentials.

## G9-S22 closure

The written criterion asks for the .NET unit, contract and integration suites twice, M8 ingress and existing browser journeys, EF snapshot and compose outage and recovery with no skipped negative cases. Attempt 08 supplies the suites twice (809 of 809), the EF snapshot checks and the stack outage and recovery; this supplement supplies all 16 existing browser journeys in three engines on the same revision with zero skipped tests. Taken together the criterion is satisfied. The two runs are separate source-locked records and are reported separately; the browser executions are not added to the 809-test total.

Limitations: the browser journeys ran against the G9 stack with the restricted worker running, not against the M8 stack; the M8 standalone ingress script was not rerun (equivalent coverage above); and the intermittent WebKit failure above is open as a residual risk.
