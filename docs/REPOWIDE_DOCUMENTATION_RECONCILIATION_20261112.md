# Repository-wide documentation reconciliation, 2026-11-12

**Status:** Documentation synchronized to the 2026-11-12 G13/G15 verification checkpoint. The active test run remains source-locked to `c6d2dc86e30913b8d67a011131f9980422e4c197` even when the documentation-only branch HEAD advances. This document does not declare a verification PASS.

## Repository inventory

The full recursive Git tree was reviewed without truncation. Before this index there were 236 documentation resources (232 Markdown and four JSON) including the root README. With this new index the total is **237 resources: 233 Markdown and four JSON**. Fifty active G13/G15, M13/M15 and core references were updated, while the 2026-11-11 reconciliation and other historical review records remain unchanged. This covers the active status/navigation surfaces; classification of historical archives does not represent a fresh line-by-line audit of every archived document.

## Exact source and run history

| Record | Source | Verified status |
| --- | --- | --- |
| [43093329982 attempt 2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982/attempts/2) | `067e1ab57f33fcabefae55727265499d86daf7ae` | Completed FAILURE. All 14 technical jobs passed, 590 TRX tests passed, technical integrity and scenarios PASS; atomic assertion coverage failed with nine missing mappings and 11 partially assessed requirements. |
| [43120072016](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43120072016) | `34dfdb691b9d18e273c6e84357c8cfaac2b51e0c` | Completed FAILURE. Thirteen of fourteen technical jobs passed; the G15 SKIP LOCKED mutation timed out after 400 seconds and was INVALID; final evidence reconciliation failed. |
| [43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445) | `c6d2dc86e30913b8d67a011131f9980422e4c197` | IN PROGRESS at this checkpoint: preflight plus 11 technical jobs passed, G13 source mutations and combined database regressions still running; the final evidence gate is pending. No observed failure at this checkpoint. |

The root cause addressed after run 43120072016 is a test-fixture sleep inside each recipient-row insertion while the outbox intent is locked. The subsequent branch change in `G15RemediationAcceptanceTests.cs` delays once per transaction using a local transaction setting. The test still requires the second dispatcher to make prompt forward progress and the removed SKIP LOCKED mutant to be killed by that assertion. No application notification behavior was changed in that correction. Its correctness is not established until the exact-SHA mutation campaign and final packet pass.

## Active status and policy boundaries

- **G13-S20 Option A approved:** 250,000 KPI members per report, at most four live reports per tenant, two per user and at most 1,000,000 live tenant members. The former 170,000-shipment single-report requirement was superseded, not claimed complete.
- **G13 and G15:** NOT ACCEPTED. Technical source-bound runs, atomic assertion coverage and independent G13-S25/G15-S25 reviews are distinct acceptance requirements. Historical G13 S25 round 3 returned NOT PASS; do not invent a positive G15 S25 round 3 review.
- **No new milestone authorized:** An independent-review finding could be proposed as a separately scoped follow-up milestone after its safety and significance have been assessed. Conditional acceptance has not been granted; no roadmap or execution-graph milestone has been added on that basis.
- **Protected `main`:** `eb529368a2447061fc1838cd3967b0ffedae669d`, unchanged at inspection. The Turkish localization work remains separate, to be reconciled after any authorized G13/G15 merge.
- **Execution authorization:** Only the disposable post-c3 G13/G15 verification campaign is authorized under the current scope. Do not merge, deploy, activate default-off services or enable notification/report retention deletion.

## Document reconciliation

The living README, STATUS, ROADMAP, RELEASE_READINESS_TODO, product/scope, requirements, acceptance, testing, architecture, security, operations, API, planning and decision documents were refreshed. The M13 and M15 architecture, implementation, policy, planning and verification documents were refreshed. The active G13/G15 acceptance references now link to the same exact source and [hosted run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md). The [documentation guide](DOCUMENT_STATUS_GUIDE.md) points here as the current index. The prior [2026-11-11 repository-wide reconciliation](REPOWIDE_DOCUMENTATION_RECONCILIATION_20261111.md) is preserved for historical source-bound context.

The 263-assertion inventory and executable verification controls have not been changed by this documentation-only pass. Older source checks, failed and passed run records, S25 reviews, accepted G0-G12/G16 baselines, migration evidence and execution-graph topology remain intact. No new technical verification result was created by documentation edits.

## What remains

Complete the two still-running technical jobs on run 43122895445, inspect the source-locked ledger and artifacts when available, and investigate any failing witness without suppressing evidence. If a scoped fix is required, perform a targeted correctness audit before rerunning. After a complete passing packet, conduct independent G13-S25 and G15-S25 reviews, classify any findings, and obtain explicit decisions before integration.

**Recorded disposition:** Documentation reconciled, historical evidence preserved, acceptance pending.
