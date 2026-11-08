# G11 Independent Raw Evidence Review

**Review date:** 2026-11-08  
**Gate:** G11, shipment exception lifecycle  
**Reviewed technical source:** `5dedbb70b4d2f4088f117d905e90d59f23a26915`  
**Supplement, test-only:** `3d450ac320b00538b106f2ad6d4b86c88661d7a4`  
**Documentation snapshot:** `857105081ef57d40bf0bfe97c76212a2007e64e3`  
**Disposition:** **Independent evidence examination performed; G11-S25 NOT PASS. G11 acceptance pending.**

## Evidence authenticity, completeness and source identity

Reviewed the supplied `m11m12-local-evidence-20261108.tar.gz` as a standalone package. Its independently calculated SHA-256 is `35d43c916bc4242cafa5ecf232d437aee9e1ef7918855f3cc866de8453b69b4d`, matching the submitted report. `MANIFEST.sha256` hashes to `7cadf6cb1edfd27ebaf7f65c357ace7496f1c331b3ee1d6d4fbb5abfbcfc12ba`. All **277 manifest entries** resolve to present files with independently matching SHA-256 values: zero missing, malformed or mismatched entries. `READBACK.txt`, `trx-summary.json` and `safety-scan.json` are three derived records not included in the sealed manifest. These are review aids, not separately manifest-authenticated raw evidence. The safety scan reports zero detected hits; a zero-hit result is not proof against every possible secret format.

Independently reparsed each available TRX and matched its counters against its per-test outcome rows. The final run at `5dedbb7` contains 291/291 unit, 46/46 contract, 20/20 existing G11 plus populated migration, 40/40 combined residual and existing G12, and 1/1 query plan. The test-only supplement at `3d450ac` has 15/15 G11 residual results. The 15 are a supplement and partially repeat G11 residual tests; **do not sum them as distinct cases**. The final and supplement stage logs both exit zero. Earlier failed test-harness attempts and the pre-remediation G12 serialization failures remain present in the package.

GitHub comparison confirms that the new production application change since the hosted `f56d017` source is confined to `ShipmentSearchStore.cs`, with 34 insertions and one deletion in the committed export retry; the `3d450ac` supplement changes G11 tests only. The worktree-status snapshots are empty. Source origin is evidenced by the recorded Git SHA and the current repository history, not by a cryptographic source attestation embedded in the archive.

## G11 evidence observations

- **S08:** A passed G11 residual test exercises 55 conflicting containers, correction replacement, supersession, event order and deterministic first-witness selection, extending the earlier hosted two-container test. Its test name, result and retained supplemental event notes reconcile. Equal received-time tie-breaking was inspected in source but was not independently executed against a forced tie.
- **S14/S18/S19:** Seven-case live authorization/revocation, concurrent Manager dispositions, corrected archive and cancellation ordering, and an audit-rejection rollback are evidenced by passing raw test results and before/after state counts. The earlier archive-route mistake is preserved and corrected by the 15/15 supplement.
- **S20:** Due-checkpoint and evaluator restart behavior was tested under a **simulated** schedule/clock advance. The database-login outage was created with login removal and backend termination; the host was restarted in a test harness. This supports the due-scan logic, not real elapsed scheduling under a production process/container interruption.
- **S21:** The 14-backend-termination campaign ended with 4 rollbacks and 10 already-completed evaluations in the main run, then 2 rollbacks and 12 already completed in the supplement. No partial state was observed, but **the committed-transaction/lost-reply window was never captured**, and no operating-system evaluator process kill was executed. This does **not** satisfy the complete G11-S21 outage/interruption contract. Treat S21 as **PARTIAL**, not PASS, pending a deterministic post-commit ambiguity and process-level recovery case.
- **S22/S24:** The hosted browser, security mutation and scan results remain earlier-SHA GitHub log evidence; downloadable raw reports were absent from the checked runs and the local campaign did not rerun them. They cannot be represented as locally sealed source-locked browser/mutation/scan evidence.
- **S23:** The hosted 341/341 integration suite was executed before the export implementation change. Local unit/contract, G11 and G12 impact tests passed on the updated production code, but the complete 341-case suite was not executed on the final code SHA. Preserve PARTIAL until that run passes or a separate explicit, documented change-impact waiver is approved.

The recorded G11 matrix also identifies **S05, S06, S07, S16 and S24** as partially evidenced, S22 as not locally executed, and S01 as awaiting formal contract sign-off. The unexecuted components are not cured by a valid archive checksum.

## Review determination

**G11-S25: REVIEWED WITH OPEN MANDATORY FINDINGS, NOT PASS.** The raw local evidence is internally consistent, and the new tests materially reduce risk. It does not yet justify full G11 acceptance because G11-S21's required outcome is incomplete, the other matrix PARTIAL/NOT EXECUTED scenarios remain unresolved, there is no final-source full-suite acceptance evidence, and S01 formal policy reconciliation is still open.

Targeted closure: capture a deterministic commit-succeeds/reply-lost case and a real evaluator/API process termination or an explicitly approved equivalent; reconcile all other G11 matrix PARTIAL/NOT EXECUTED rows with actually executed evidence; run or approve a final-SHA source-change regression decision; obtain an explicit G11-S01 decision; re-review the complete record. No protected-main merge, deployment, production migration or M9 hold clearance is implied.
