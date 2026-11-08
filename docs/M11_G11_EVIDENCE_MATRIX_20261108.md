# G11 Source-Locked Scenario Evidence Matrix

**Reviewed:** 2026-11-08
**Frozen application SHA:** `f56d017d9b2619be56fe533f73ba1d11f63974c0`
**Basis:** GitHub run logs and source inspection. This is not an independently sealed raw evidence archive.
**Gate:** Acceptance pending. Individual EXECUTED CHECKS rows describe demonstrated tests, not a closed whole-scenario Sxx gate.

| Scenario | Evidence state | Demonstrated evidence | Remaining requirement |
| --- | --- | --- | --- |
| G11-S01 | POLICY RECORDED | M11-P01..P12 and P10b documented | Formal S01 independent reconciliation |
| G11-S02 | EXECUTED CHECKS | source 291 unit, 46 contract, web, Release build | Consolidate onto reviewed source evidence package |
| G11-S03 | EXECUTED SUBSET | UTC strict and DST boundary unit tests | Independently map exact truth table |
| G11-S04 | EXECUTED SUBSET | ScheduleOverdueIsScopedAuditedAndIdempotentForManager | Retain output and boundary fixture |
| G11-S05 | EXECUTED SUBSET | ExplicitWorkflowArrivalIsDistinguishedFromUnconfirmedSchedule | Reconcile transition provenance against detailed fixture |
| G11-S06 | EXECUTED SUBSET | DepartureRiskNeedsRecordedWorkflowAndFutureArrival | Full real HTTP and source-confidence matrix |
| G11-S07 | EXECUTED SUBSET | StaleProjectionIsNeverUsedAsConfirmedConflict; NullSchedulesAreUnknownNotInventedDelays | Absent/manual/stale negative matrix |
| G11-S08 | EXECUTED SUBSET | EquipmentConflictCarriesExactAcceptedEventAndContainerWitness: corrected replacement, two conflicts, count=2 and truncation=true | Independent deterministic selection and bounded lineage review, additional negative source cases |
| G11-S09 | EXECUTED SUBSET | Correction-witness replacement, accepted M9/M10 regressions | Cross-check late/out-of-order/duplicate/superseded permutations |
| G11-S10 | EXECUTED SUBSET | SameFingerprintWithStaleSeverityWritesAttributedReevaluation, correction generation | Rebuild drift and interruption evidence |
| G11-S11 | EXECUTED CHECKS | PopulatedAcceptedThirteenMigrationDatabaseSurvivesFifteenMigrationUpgrade and fresh install | Raw migration diff and SQL role manifest |
| G11-S12 | EXECUTED CHECKS | DirectApiSqlCannotReadExceptionsOrHistoryWithoutSignedSession; SQL read isolation run | Raw real-role probe and cross-join review |
| G11-S13 | EXECUTED SUBSET | Restricted Manager/Analyst/Administrator fixture and browser | Full five-role matrix, Support and Auditor |
| G11-S14 | NOT FULLY EVIDENCED | General session tests exist in accepted baseline | M11 live revoke/logout/org suspension at transaction boundary |
| G11-S15 | EXECUTED SUBSET | ScheduleOverdueIsScopedAuditedAndIdempotentForManager | Forced audit failure and exact immutable history proof |
| G11-S16 | EXECUTED SUBSET | VerifiedClearancePersistsAcrossUnknownBeforeRecurrence; late arrival Manager resolve | Concurrent recurrence/late-source edge cases |
| G11-S17 | EXECUTED SUBSET | ReplayKeyCannotBeReusedForAnotherException | Same-key response and wrong-actor replay matrix |
| G11-S18 | EXECUTED SUBSET | ConcurrentFirstEvaluationsDoNotDuplicateOccurrenceOrHistory | Acknowledge, rearchive and cancel concurrency race |
| G11-S19 | NOT FULLY EVIDENCED | Audited successful Manager lifecycle | Injected audit write rejection with full rollback |
| G11-S20 | NOT FULLY EVIDENCED | UTC boundary policy unit tests and due cursor logic | Missed timer and process restart due-scan recovery |
| G11-S21 | NOT FULLY EVIDENCED | General accepted recovery regressions | M11 evaluator outage and process crash before/after commit |
| G11-S22 | EXECUTED CHECKS | 36/36 three-engine browser run with M11 and M12 plus M5 | Retain raw browser results and source/fixture identities |
| G11-S23 | EXECUTED CHECKS | 341/341 full suite, 40/40 impacted | Explicit signed M9 hold and final source reconciliation |
| G11-S24 | EXECUTED SUBSET | 5/5 targeted effective mutants; SQL access and image/dependency scans | Broader required attack families, evidence safety and raw scans |
| G11-S25 | BLOCKED | Run logs and TRX hash reported; no downloadable raw artifacts found | Independent reparse, manifest validation, complete S01..S24 dispositions |

## Review outcome

All rows are traceability and sufficiency observations. No unresolved scenario is silently promoted to PASS. Source and log evidence alone does not fulfill the separate S25 independent raw-evidence review. Historical failures remain valid for their superseded source and are recorded in [run index](M11_M12_EXECUTED_RUN_INDEX_20261108.json).

See [coordinated readiness review](M11_M12_ACCEPTANCE_REVIEW_20261108.md) and [verification plan](M11_VERIFICATION_PLAN.md).
