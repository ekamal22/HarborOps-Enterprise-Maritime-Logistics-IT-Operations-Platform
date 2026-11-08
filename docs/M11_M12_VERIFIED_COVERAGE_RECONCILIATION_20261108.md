# M11/M12 Verified Coverage Reconciliation

**As checked:** 2026-11-08, after the 04:09 UTC final full-suite result
**Repository:** `ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform`
**Candidate branch:** `feat/m11-m12-combined-integration`
**Historical branch HEAD when initial record was created:** `223fe592e93cdcae65d5889a14ecc4ecce0b51d8`
**Accepted protected main:** `6ead1d076946dd96b53401265a31efc35c4a666c`
**Disposition:** Substantial successful M11/M12 execution on separately identified revisions. G11/G12 independent acceptance, protected-main merge and deployment pending.

**Current acceptance evidence:** [Combined sufficiency review](M11_M12_ACCEPTANCE_REVIEW_20261108.md), [G11 scenario matrix](M11_G11_EVIDENCE_MATRIX_20261108.md), [G12 scenario matrix](M12_G12_EVIDENCE_MATRIX_20261108.md) and [indexed run results](M11_M12_EXECUTED_RUN_INDEX_20261108.json). G11/G12 independent S25 raw-evidence acceptance remains pending. 

## Exact run coverage

| Scope | Observed result | Tested source SHA | GitHub run |
| --- | --- | --- | --- |
| Whole PostgreSQL integration suite, earlier expanded candidate | **340/340 PASS**, 0 failed, 0 not executed | `239f3cd9574dd4bd197071d54e51ebd37a44c094` | [42442170451](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42442170451) |
| Earlier whole integration suite | **338/338 PASS**, 0 failed/skipped | `4b5d5c2692311cbc00a00475e1f28f29c3f55685` | [42440935080](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42440935080) |
| Final-cohort full integration rerun | **PASS: 341/341, 0 failures, 0 not executed, TRX hash logged** | `f56d017d9b2619be56fe533f73ba1d11f63974c0` | [42443065884](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065884) |
| Impacted PostgreSQL integration, including populated migration | **40/40 PASS** | `f56d017d9b2619be56fe533f73ba1d11f63974c0` | [42443065810](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065810) |
| G11 signed exception, tenant isolation and lifecycle runtime | **19/19 PASS** | `f56d017d9b2619be56fe533f73ba1d11f63974c0` | [42443065861](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065861) |
| G12 exclusive PostgreSQL export and SQL authority | **4/4 PASS** | `f56d017d9b2619be56fe533f73ba1d11f63974c0` | [42443065861](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065861) |
| M11/M12/M5 targeted browser journey | **36/36 PASS**, 12 each Chromium, Firefox, WebKit | `f56d017d9b2619be56fe533f73ba1d11f63974c0` | [42443065925](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065925) |
| Targeted source mutation controls | **5/5 mutants killed**, valid clean controls | `f56d017d9b2619be56fe533f73ba1d11f63974c0` | [42443065914](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065914) |
| Dependency and source-security check | **PASS** for configured high/critical threshold | `f56d017d9b2619be56fe533f73ba1d11f63974c0` | [42443065915](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065915) |
| PostgreSQL, API and web image checks | **PASS** for configured high/critical threshold | `f56d017d9b2619be56fe533f73ba1d11f63974c0` | [42443065956](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065956) |
| Direct M11 SQL exception-history read isolation | **PASS** | `f56d017d9b2619be56fe533f73ba1d11f63974c0` | [42443065978](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065978) |
| Source: Release build, unit, contract and web checks | **PASS**, 0 build errors/warnings, 291/291 unit and 46/46 contract | `4b9cd458abd015ac4d8db0f1e2df1bdb6b0c4ba1` | [42443225317](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443225317) |
| Documentation consistency and generated view | **PASS** | `4b9cd458abd015ac4d8db0f1e2df1bdb6b0c4ba1` | [42443225302](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443225302) |

**Coverage interpretation:** These successes do not all belong to a single SHA. Later commits through `223fe592e93cdcae65d5889a14ecc4ecce0b51d8` changed verification workflow configuration and documentation; there is no claim of a subsequent comprehensive source-locked run on that documentation-only branch tip. The final-cohort integration run on `f56d017` completed successfully with 341/341. The later commits to the documentation/workflow branch do not require reclassifying that application verification source.

## Specific live PostgreSQL scenarios verified

The 40/40 impacted campaign includes the executed test `M11M12UpgradeTests.PopulatedAcceptedThirteenMigrationDatabaseSurvivesFifteenMigrationUpgrade`, preserving the accepted source state across the actual 13-to-15 migration sequence. The same suite exercised migration ordering and EF model snapshot compatibility, including accepted M10/M16 upgrade boundaries.

G11 execution covered accepted GTIN/GTOT event and correction witness attribution, restricted SQL reads, signed evaluation authority, immutable history paging, severity reconciliation, recurrence after verified clearance, archived risk closure, replay-key collision denial and concurrent first-evaluation uniqueness.

G12 execution covered restricted runtime export under actual credentials, search filter and protected cursor tenant scope, refusal of a 5,001-row export without an audit, and a real PostgreSQL blocked-read timeout without preparing or emitting partial CSV. The earlier export-role and rate-cap checks also passed, but are not evidence that all multi-instance rate and end-to-end proxy timing variants have been executed.

Negative-control PostgreSQL error lines within a successful suite may be expected denial assertions. Determine an individual scenario outcome from the test runner and assertions, not from the mere existence of a database ERROR log.

## Failed and superseded attempts

- [42443065858](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065858), the `f56d017` source workflow, **FAILED overall** even though its .NET compilation and unit/contract executions passed. The later [42443225317](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443225317) source workflow **PASSED** on `4b9cd458`.
- [42443065940](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065940), the `f56d017` documentation workflow, **FAILED**. The later [42443225302](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443225302) documentation check **PASSED** on `4b9cd458`.
- Earlier [42433586007](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433586007) was a genuine **318/336** integration failure on older source. The subsequent 338/338 and 340/340 passes are separately attributed.
- Earlier [42434283623](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42434283623) exposed real M11 restricted SQL read privilege leakage. The corrected M11 read isolation is exercised in the successful latest SQL runs.
- Earlier mutation runs [42441183635](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42441183635) and [42441543253](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42441543253) had invalid/ineffective SQL-mutant or harness evidence. Later effective control and correction-witness mutation runs passed.
- [42443065884](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065884) completed **PASS: 341/341**, zero failed/skipped, logged TRX SHA-256 `6b6559da3a968e4f9f7c1732d1d78674929a160c7b25c3f90b375ff07c6ea786`. That hash has not been independently recomputed from a retained raw TRX.

At final review GitHub reported **121 repository Actions runs, all 121 terminal, none running or queued**, including the older M0-M10 history. Older failures remain historical and are not silently erased.

## Gate requirements not established by the runs

1. Separate independently reviewed G11-S01..S25 and G12-S01..S25 evidence matrices with explicit scenario-level dispositions, raw records, exact SHA and a verified manifest. Success of a targeted suite does not automatically mark all 25 scenarios PASS.
2. Independent G11-S25 and G12-S25 raw-evidence reviews and explicit gate decisions. **Acceptance pending.**
3. Complete G11-S08 conflict provenance coverage, especially multiple conflicting containers and bounded-truncation completeness, beyond the executed positive correction-witness tests.
4. Final fault, restart/crash, cancellation, concurrency, load, measured SQL query plans, timeout/proxy and security-negative coverage wherever required but not yet evidenced by the runs. Do not assume the existing 40 impacted tests cover them all.
5. M12-P01..P16 were selected for technical implementation and verification, but that does not substitute for final G12-S01 gate reconciliation. The Q2 2026 production-snapshot branch is documented NOT APPLICABLE for the repository-owned dataset. Actual populated 13-to-15 migration testing was executed as noted above.
6. A final frozen-SHA evidence package and source-diff reconciliation if new functional code changes are made.

## Efficient continuation and restrictions

All ten combined verification workflows were changed to manual-only triggers to prevent expensive push fan-out. Do not start a new full campaign for every documentation push or duplicate a completed scope without change-impact justification. Any additional local campaign must use disposable, exclusive PostgreSQL for role-changing checks, preserve failed attempts, and retain raw source-locked evidence.

No GitHub Actions run, protected-main merge, release, deployment, production migration or M9 operating-hold change is authorized by this record. The M9 ARRI/DEPA reconciliation hold, 10,000 unresolved-work cap, disabled-by-default M10 projector and null ETA remain binding. Preserve the original M11 and M12 branches and accepted protected main.

See [verification progress](M11_M12_VERIFICATION_PROGRESS_20261108.md), [operations policy](M11_M12_VERIFICATION_OPERATIONS_20261108.md), [G11 verification criteria](M11_VERIFICATION_PLAN.md), [G12 verification criteria](M12_VERIFICATION_PLAN.md), and [combined integration plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md).
