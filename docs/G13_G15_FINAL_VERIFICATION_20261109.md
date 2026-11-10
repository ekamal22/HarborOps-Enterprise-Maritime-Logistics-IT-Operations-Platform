# Final G13/G15 Source-Locked Verification Campaign

**Current review decision (2026-11-10):** The [15-job G13/G15 technical run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543 passed TRX cases, 227/227 mapped technical assertions and source integrity PASS. The separate [G13/G15 S25 review](G13_G15_REVIEW_MATRIX_20261110.md) and [formal decision](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record G13 NOT ACCEPTED and G15 NOT ACCEPTED. G13 permanent quota/idle cleanup and G13/G15 exact negative mutation and HTTP-outage proof remain outstanding. Earlier status snapshots below are historical and apply only to their own source SHAs. No main merge, deployment, cleanup or activation.
**Current technical evidence (2026-11-10):** [15-job run 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`, including 541 passing TRX tests, no failures/skips, and 48 passing technical scenario contract checks. The separate 242-assertion audit has 224 technical witnesses present, 3 missing, 7 policy-review and 8 independent-review items. Full G13/G15 acceptance remains pending. [Current source-locked evidence](G13_G15_VERIFIED_EVIDENCE_20261110.md).

**Verified successor:** Both [preflight 42726180647](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42726180647) and [15-job full run 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`. The 18-mutant G13 preflight, 541 passing tests and full technical source-lock results supersede the older 13-mutant campaign as the latest test evidence. The original campaign instructions, branch references and failed-run accounts below are historical and must not be used as the current launch instruction.

**Decision and preparation date:** 2026-11-09.
**Verification branch:** `verify/g13-g15-final-acceptance-20261109`.
**Execution:** Manual GitHub Actions dispatch only. No workflow triggered by commits.
**Baseline:** Prior [nine-job technical PASS 42555586856](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42555586856) at `ef2dd697df6c4a2dc8b53c55e7968ac37e41a745`.
**Earlier failed eleven-job run:** [42561392681](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42561392681), FAILED at `895c20650e04f165116daf8a96327907cda09bf9`. Eight independent jobs succeeded; the G13 database and G13 mutation jobs failed on source-fixture illegal status transitions and the evidence aggregator correctly failed as a dependent gate. No technical or milestone PASS is claimed from that attempt.
**Earlier failed fifteen-job run:** [42565412367](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42565412367) FAILED at `6f1f34b2fba4c13a75a85bedc482118755921e11`. Thirteen jobs passed, including G13 and G15 runtime, mutation, scale, browser, crash/restart, concurrent-load and dependency security checks. The only failing substantive test was the historical M12 downgrade assertion: the later M15 migration correctly prevents evidence loss before the older M12 export-audit guard can execute. The downstream evidence aggregator failed because a required test failed. The regression now asserts the first applicable retained-evidence safeguard, validates surviving M12/M15 authority, and verifies M15 outbox, receipt and replay rows remain unchanged.

**Verified final candidate:** The final manual run 42569970541 passed all fifteen technical jobs against its exact source SHA. Milestone acceptance is not recorded; see the separate current evidence review. The branch is separate from main, prior feature branches and the previous verified campaign. The exact candidate SHA is frozen by the manual dispatch, not by this document.

## Most recent failed source-locked run and correction

[Run 42567231512](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42567231512) failed on source `0a32e807404ab13b3e80a74e8dc553b76ba69f98`: 13 of 15 jobs passed. Firefox's M15 rejected-notification-refresh browser test tried to click a disabled Refresh button while the organization-selection notification request was still loading. The 30-second Playwright timeout prevented the remainder of the browser matrix from running; the final evidence ledger correctly failed on missing browser coverage. All non-browser verification jobs passed, including the earlier corrected M12 combined regression.

The test now explicitly waits until the authorized notification refresh finishes before setting the revocation flag and clicking Refresh. It checks both protected notification removal and removal of the revoked organization's selector. The revision was verified successfully in run 42569970541. No passing results from the previous source are retroactively attached to the new SHA.

## Approved policies and limits

The latest [G13/G15 approval record](G13_G15_POLICY_APPROVAL_20261109.md) is binding. M15-P01 through P09, P11, P13 and P14 are approved; P10 fail-closed M11 history/outbox coupling is approved conditional on real recovery and accepted-M11 regression; P12 provisional 90-day unread/read and 30-day acknowledged windows are accepted, but deletion, legal hold bypass and automatic cleanup are **not** approved. Production dispatcher remains disabled.

The approved provisional G13 load envelope covers 170,000 shipments and about 450,006 report members, at least 30 measured reports, p50 at most 15 seconds, p95 at most 20 seconds, hard 25-second request deadline, 15-second SQL statement timeout, at most 500,000 member rows, 10-minute snapshot expiry, maximum 100-member drill pages, and a separate four-principal concurrent campaign. Actual storage, memory, CPU and query-plan evidence are required. A numerical permanent storage limit remains subject to measured review.

## One manually dispatched workflow, fifteen isolated jobs

The named workflow is [verify-m11-m12-combined.yml](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/workflows/verify-m11-m12-combined.yml). It retains an alternate synced entry point [verify-g13-combined-full.yml](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/workflows/verify-g13-combined-full.yml). Dispatch only one entry point against `verify/g13-g15-final-acceptance-20261109`.

| Job | Required proof |
| --- | --- |
| source | Warning-as-error Release build, unit/contract checks, React build and documentation checks |
| m15-database | Restricted real PostgreSQL G13/G15, 12+2 exact KPI members, 117-member keyset, 5-role M15, expiry, replay, audit and migration regressions |
| m15-browser | Three-engine KPI and notification journeys |
| scale | 170k-shipment report source, SQL plan, 30 latency samples and the approved p50/p95 limits |
| security-mutations | Real G13 read-policy attack variants |
| g13-faults | SQL timeout, cancellation, report rollback/recovery |
| g13-mutations | Clean reference oracle and 13 effective KPI source variants |
| g15-mutations | Clean controls and 8 effective notification SQL variants |
| g15-scale | Million-row feed and 10 x 50-row scoped keyset pages |
| g15-fanout | Two dispatchers, 250 additional recipients and 100 scheduled scoped readers with bounded database admission |
| g13-concurrency | Four distinct authorized principals, simultaneous scoped reporting |
| g13-g15-crashes | Actual separate .NET testhost SIGKILL before and after two G13/G15 commit boundaries, new-host post-commit recovery |
| dependency-security | Dependency advisories and committed-secret file inventory, fail closed on high/critical |
| combined-regressions | Accepted M2/M5/M10/M11/M12/M16 real-database regression matrix |
| gate-ledger | Exact source, all artifacts/sha256, raw test outcomes, 3-browser reports, effective mutations, crash phases, performance thresholds, approvals and 50 conservative scenario dispositions |

Each mutable database job has a distinct disposable PostgreSQL server. Source mutation scripts restore the unchanged checked-out source before artifact upload. Evidence files are retained 30 days. Every job's source and manifest must match the workflow SHA; the final ledger fails on a failed/skipped required test, absent manifest, incomplete mutation controls or missing evidence.

## Scope of the final review

After the workflow finishes, inspect **all fifteen jobs**, raw TRX and browser output, historical failures, performance samples and query plan, latency and storage limits, protected SQL roles, process-kill markers and backend recovery, and the resulting G13-S01..S25 and G15-S01..S25 disposition matrix.

Successful technical jobs do not automatically close an unproven full-scenario requirement. In particular, independent G13-S25 and G15-S25 reviews and subsequent explicit acceptance remain separate decisions. No unsourced scenario is allowed to become PASS just to meet a release deadline. If a new failure appears, remediate its root cause and verify the revised exact SHA before claiming completion. Additional test campaigns should not be scheduled merely to repeat an unchanged successful job, but no future test can responsibly be ruled out before the actual result and independent evidence review.

**No merge, deployment, production migration, M10 projector activation, M11 evaluator activation, M15 dispatcher activation or retention deletion is authorized by this campaign.**
