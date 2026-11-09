# Final G13/G15 Source-Locked Verification Preparation

**Decision and preparation date:** 2026-11-09.
**Verification branch:** `verify/g13-g15-final-acceptance-20261109`.
**Execution:** Manual GitHub Actions dispatch only. No workflow triggered by commits.
**Baseline:** Prior [nine-job technical PASS 42555586856](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42555586856) at `ef2dd697df6c4a2dc8b53c55e7968ac37e41a745`.
**Latest completed run:** [42561392681](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42561392681), FAILED at `895c20650e04f165116daf8a96327907cda09bf9`. Eight independent jobs succeeded; the G13 database and G13 mutation jobs failed on source-fixture illegal status transitions and the evidence aggregator correctly failed as a dependent gate. No technical or milestone PASS is claimed from that attempt.
**Most recent fifteen-job run:** [42565412367](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42565412367) FAILED at `6f1f34b2fba4c13a75a85bedc482118755921e11`. Thirteen jobs passed, including G13 and G15 runtime, mutation, scale, browser, crash/restart, concurrent-load and dependency security checks. The only failing substantive test was the historical M12 downgrade assertion: the later M15 migration correctly prevents evidence loss before the older M12 export-audit guard can execute. The downstream evidence aggregator failed because a required test failed. The regression now asserts the first applicable retained-evidence safeguard, validates surviving M12/M15 authority, and verifies M15 outbox, receipt and replay rows remain unchanged.

**Current final candidate:** The correction is committed and awaits manual source-locked GitHub Actions verification. The branch is separate from main, prior feature branches and the previous verified campaign. The exact candidate SHA is frozen by the manual dispatch, not by this document.

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
