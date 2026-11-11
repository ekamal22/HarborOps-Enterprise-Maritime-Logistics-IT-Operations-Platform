# G13/G15 post-c3 hosted verification register, 2026-11-11

**2026-11-12 verification update:** [Run 43120072016](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43120072016) at `34dfdb691b9d18e273c6e84357c8cfaac2b51e0c` completed FAILURE after 13 of 14 technical jobs passed. G15 mutation `outbox-claim-skip-locked-removed` timed out after 400 seconds and was correctly classified INVALID, not killed. The trigger in `SecondDispatcherSkipsALockedIntentAndDeliversOthersWhileTheFirstIsBusy` delayed every inserted recipient, potentially prolonging the locked claim and cleanup across many recipients. The new fixture delays once per claim transaction while retaining the required second-dispatcher prompt-progress assertion. This correction is unverified until executed on its own SHA; G13/G15 remain NOT ACCEPTED.

**Post-c3 hosted correction checkpoint, 2026-11-12:** [Run 43093329982 attempt 2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982/attempts/2) completed with **14 successful technical jobs**, **590 passing TRX tests**, no failed or skipped tests, integrity PASS, and technical scenario checks PASS at frozen source `067e1ab57f33fcabefae55727265499d86daf7ae`. The final atomic assertion audit correctly failed: 9 missing evidence mappings (eight preflight-command witnesses and one hosted CSP browser-case mapping) and 11 still partially assessed G13 assertions. Subsequent branch commits correct the evidence mapping and strengthen the cleanup preservation, PostgreSQL resource, and tenant-filter behavioral witnesses. The new source has not yet passed a full source-locked campaign. **G13/G15 acceptance pending.** No merge, deployment, service activation or retention deletion is authorized.

**Record type:** Source-bound, time-scoped GitHub Actions observation. This register is not an unconditional acceptance or release decision. Run statuses below are based on GitHub Actions inspections on 2026-11-11 and must be refreshed against each live run before an acceptance decision.

**Working branch:** `feature/g13-post-c3-remediation-20261111`. **Protected `main`:** `eb529368a2447061fc1838cd3967b0ffedae669d`, unchanged at this checkpoint. **Formal status:** G13 NOT ACCEPTED; G15 NOT ACCEPTED. The round 3 G13-S25 review at historical c3 source `4e7b9e22aacef25f7f60e2295938ec6cf79bdadd` returned NOT PASS. A positive G15 round 3 review is not established by the repository records inspected here.

## Approved reporting contract

G13-S20 Option A was approved with a stricter **250,000 KPI-member limit per report**, four live reports per tenant, two per user and at most **1,000,000 active members per tenant**. Over-capacity reports must fail closed without partial publication. Larger data sets can be represented by separately filtered, independent reports; this does not promise stitched totals or cross-report deduplication. The earlier realistic 170,000-shipment single-report condition was superseded, not passed. The c3 500,000-member technical results remain historical and cannot certify the amended source.

## Hosted run ledger

| Actions run | Exact tested source SHA | Observed result | Evidence boundary |
| --- | --- | --- | --- |
| [43089871915](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43089871915) | `08c25abc317df38e7e7993bd444b5ad8a7ba36a6` | Completed FAILURE, preflight only | The M15 contract rejected a workflow enabling delivery without the dedicated disposable-browser exception. All dependent tests were skipped. |
| [43090424080](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43090424080) | `30fd28f6d2eaab2535cd68a9139c2c70435f1898` | Completed FAILURE, 10 jobs passed, 5 failed including final ledger | Combined database and G13 mutation clean control failed on a PostgreSQL API role password rotation (`28P01`). Browser failed on a missing container runtime dependency. The live API crash probe ended without its marker. Final evidence reconciliation correctly rejected the incomplete packet. |
| [43091726641](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43091726641) | `777a6cd391d8fc2311cba13ad61da0b5df1da399` | Completed FAILURE, preflight only | The M11 source contract rejected an unrelated worker Dockerfile change; no combined technical jobs ran. The worker change was subsequently reverted. |
| [43091966161](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43091966161) | `1f4ce182950e91a2fce8f4188e1b4397dde740df` | IN PROGRESS at inspection: 12 jobs passed, browser FAILED, one G13 mutation job running | Browser session refresh encountered HTTP 502 immediately after an API restart. The new crash phase, database, scale and multiple security and regression jobs passed at this source. Its pending job prevents final reconciliation. |
| [43093329982](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982) | `067e1ab57f33fcabefae55727265499d86daf7ae` | IN PROGRESS at inspection: 10 jobs passed, four technical jobs running, no recorded failures | Preflight, combined Chromium/Firefox/WebKit browser, live process-kill recovery, G13 concurrency and fault recovery, G15 million-receipt and fanout, security checks and combined M10-M12 regressions passed. Pending: G13 and G15 mutation suites, source-locked scale and combined M13/M15 database jobs. Final gate-ledger job had not started. |

**Counts are snapshots, not final predictions.** The latest branch HEAD may include documentation-only commits after the source SHA under test. Never substitute HEAD for a run's actual `head_sha`. A passing job in one run cannot repair a failed or missing job in another source-locked run.

## Distinct failure classifications

- **Source-contract enforcement:** The first run violated the M15 live-delivery safeguard. The third run violated M11's unchanged-worker-source constraint. Both preflight failures correctly prevented downstream execution.
- **Test-fixture identity rotation:** The combined database job and G13 mutation clean control in run 43090424080 independently observed PostgreSQL `28P01` for `harborops_api` after double provisioning. A later source correction uses the current role credentials. The corrected full mutation outcome is pending.
- **Browser availability:** The missing API runtime library in 43090424080 was corrected. Run 43091966161 still attempted browser session refresh before the restarted API was ready. The revised workflow waits for API and proxy readiness; run 43093329982's combined browser job passed.
- **Process-kill evidence:** Run 43090424080 lacked a real-API crash marker. A revised monitoring connection and retained phase logs were introduced. The live process-kill job passed at the next two source SHAs, including 43093329982.
- **Evidence reconciliation:** The failed ledger in 43090424080 is a correct rejection of missing technical witnesses. No false PASS should be inferred from partial test success.

## Remaining release requirements

1. Wait for all technical jobs and the final evidence ledger on a **single exact SHA** to complete. Review raw TRX, mutation manifests, PostgreSQL/resource measurements, browser outputs, process-kill phase markers, artifact hashes and sensitive-evidence scan.
2. Reconcile every mandatory G13/G15 scenario and atomic assertion. Source inspection, new test names and historical green runs are not substitutes for passing execution.
3. Complete independent G13-S25 and G15-S25 re-reviews against the final source and its raw packet. Record explicit positive decisions separately, if supported.
4. Maintain the integration order: G13/G15 acceptance first, merge only after separate authorization, then reconcile and verify Turkish localization against the updated `main`. Do not mix localization into this branch.

## Execution and safety boundary

The disposable post-c3 GitHub Actions campaign was expressly authorized, including automatic fanout only following a successful source preflight. This does not authorize unrelated workflows, deployment, production processing, automatic notification or report retention deletion, merges or GitHub Actions modification unrelated to this scope. Retain all historical failures and passed artifacts unchanged. Documentation-only commits use `[skip ci]` and do not claim newly executed technical evidence.
