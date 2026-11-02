# M6/G6 Gate Closure

**Decision date:** 2026-11-02
**Decision:** M6-01 through M6-04, the complete twelve-rule M6-local contract, and G6 are explicitly accepted.
**Integration authorization:** Merge `feat/m6-container-voyage-assignments` into `main` with `[skip ci]`.
**Verified application SHA:** `a94d7cd406a93806527d28ba7d2ce2d6ba3307b6`
**Post-verification head:** `9265ca971e6d6abde7d5025616eb250f9a8f5d66` (documentation and read-only evidence summarization only).

## Technical evidence and G6-S01 closure

The accepted source passed 149 unit, 12 contract, 187 fresh PostgreSQL integration, 187 repeated PostgreSQL integration and 57 browser tests in each of Chromium, Firefox and WebKit: **706 successful test executions**, with zero failed and zero skipped. All 33 executable pipeline stages passed. Four controlled security mutations failed their intended tests. Dependency audits, three image scans, outage/recovery and disposable resource teardown were recorded as passing.

The original evidence assertion exited 1 solely because G6-S01, the explicit contract decision, was awaiting approval at test time. G6-S02 through G6-S25 had executed evidence. The explicit acceptance on 2026-11-02 closed G6-S01 without modifying, reclassifying or rerunning that historical evidence. **G6 is accepted by the subsequent decision, not by a retroactive PASS claim for the original evidence stage.**

## G6-S10 historical snapshot limitation

The retained PostgreSQL 16 M0-era volume was inspected through a disposable read-only copy, upgraded to M6 and checked for file and bootstrap-record preservation. The retained dataset contained **only one bootstrap record** and no historic shipments, voyages, containers, organizations or users. Populated M5-to-M6 migration preservation was separately exercised by `ExistingM5RowsSurviveM6UpgradeRollbackAndReapply`. The known limitation was explicitly acknowledged in accepting G6-S10; the checkpoint is not described as a production-volume or historical scale test.

## Evidence provenance

- Final archive: `E:\HarborOps-Verification\m6\archives\G6-incomplete-20261102T175309Z-a94d7cd406a9.zip`
- Archive SHA-256: `51C2EB9BEE04DC6DDC2D999A7375A36976A491958AC06E212005FECAAACDC65C`
- 167 evidence files, individually hashed; ZIP extraction and file hash validation passed.
- Tested source SHA: `a94d7cd406a93806527d28ba7d2ce2d6ba3307b6`
- Earlier failed and superseded evidence was separately archived.
- See [executed verification](M6_EXECUTED_VERIFICATION.md) and [G6 verification plan](M6_VERIFICATION_PLAN.md).

## Limits and follow-on work

This decision closes M6/G6 and authorizes integration, but does not authorize a production deployment, public hosting, hosted GitHub Actions or a future gate. G7-G24 remain open; G7 and any applicable D-002 decisions are separate. G6 satisfies the M8 prerequisite only where the execution graph requires it; G7 and D-006 may still be necessary. Formal dedicated accessibility testing remains at M24-04. The next historical snapshot checkpoint is M12/G12.
