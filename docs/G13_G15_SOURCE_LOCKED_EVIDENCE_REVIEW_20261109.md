# G13/G15 Source-Locked Evidence Review

**G13/G15 verification checkpoint, 2026-11-12:** [Run 43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445) is IN PROGRESS at frozen source `c6d2dc86e30913b8d67a011131f9980422e4c197`. Preflight, browser, process crash, security, notification load/fanout, concurrency, scale and combined M10-M12 regressions had passed at the recorded check; G13 mutation and combined M13/M15 database regressions remained running, and the final ledger was pending. [Prior run 43120072016](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43120072016) ended FAILURE at `34dfdb691b9d18e273c6e84357c8cfaac2b51e0c`: 13 technical jobs passed, but G15 mutation `outbox-claim-skip-locked-removed` timed out and was INVALID, correctly blocking final reconciliation. The new locked-intent fixture delays once per transaction and must still kill that mutant by behavior. [Run 43093329982 attempt 2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982/attempts/2) passed all technical jobs but its atomic evidence gate failed. These source SHAs must not be combined into a passing campaign. G13-S20 Option A remains approved at 250,000 members per report, four live reports per tenant, two per user and 1,000,000 active members per tenant. **G13/G15 NOT ACCEPTED**, pending a complete single-SHA packet and independent S25 reviews and decisions. No main merge, Turkish localization integration, deployment, production activation or retention deletion. See [hosted run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) and [2026-11-12 reconciliation](REPOWIDE_DOCUMENTATION_RECONCILIATION_20261112.md). Historical records below remain source-bound.

**Current review decision (2026-11-10):** The [15-job G13/G15 technical run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543 passed TRX cases, 227/227 mapped technical assertions and source integrity PASS. The separate [G13/G15 S25 review](G13_G15_REVIEW_MATRIX_20261110.md) and [formal decision](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record G13 NOT ACCEPTED and G15 NOT ACCEPTED. G13 permanent quota/idle cleanup and G13/G15 exact negative mutation and HTTP-outage proof remain outstanding. Earlier status snapshots below are historical and apply only to their own source SHAs. No main merge, deployment, cleanup or activation.
**2026-11-10 successor checkpoint:** The original design, decision or source review below remains a historical record for its stated date and source SHA. A later [G13/G15 15-job technical campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` with 541 passing TRX cases, but the 242-assertion audit still identifies 3 missing technical witnesses and 15 policy/independent-review items. See [current verification and acceptance status](G13_G15_VERIFIED_EVIDENCE_20261110.md). No main merge, release, activation or deletion authority follows from the technical result.

**Review date:** 2026-11-09.
**Reviewed implementation SHA:** `e4bf9cd1527f85f554baf623e90805d96d08ac1a`.
**Manual workflow:** [Run 42569970541](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42569970541).
**Reviewed source branch:** `verify/g13-g15-final-acceptance-20261109`.
**Disposition:** TECHNICAL VERIFICATION PASS. FULL G13/G15 ACCEPTANCE NOT PASS. MERGE NOT READY.

## Evidence inspected

The source-locked workflow completed 15 of 15 jobs successfully. The final artifact-integrity script reported `PASS`, zero issues, 487 passed TRX tests, zero failed, zero skipped and zero other outcomes. The browser output contains 30 passing tests, 10 each in Chromium, Firefox and WebKit, with no flaky, skipped or unexpected results. The 15 retained artifact ZIPs were read directly and their internal SHA-256 manifests were verified. Job source markers agree with `e4bf9cd1527f85f554baf623e90805d96d08ac1a`. Four separate test-source SHA-256 references are recorded outside the relevant artifact ZIPs, so their out-of-archive source-file digest comparisons were not independently reproduced here. The recorded GitHub commit and manifests remain available for that review.

G13 source mutation coverage: 13 effective mutants killed, clean control passed. G13 restricted-read-policy campaign: two variants killed, clean control passed. G15 notification campaign: eight mutants killed, clean control passed. Four actual testhost SIGKILL phases were independently represented in the archived crash manifest, including new-process recovery after the two post-commit phases. All referenced archive and manifest hashes present within the retrieved artifact ZIPs matched. The affected accepted-milestone regression suite ran 60 tests successfully on a disposable PostgreSQL server.

G13 scale evidence: 170,000 newly seeded shipments, approximately 450,006 materialized KPI members per report, 30 successful measured report builds, measured p50 **12,097 ms**, p95 **13,453 ms**, peak process working set **661,880,832 bytes** and measured process CPU **7,362 ms**. The approved provisional p50 15,000 ms and p95 20,000 ms thresholds were met. Report/member relation size increased from **136,298,496** to **1,558,806,528 bytes**, or **1,422,508,032 bytes** (about 1.325 GiB) over 30 report builds. This is approximately 45.22 MiB of relation growth per build in the isolated campaign. These figures are not a production capacity promise.

G15 scale evidence: at least 1,000,000 seeded receipt rows, ten 50-row pages, measured page p50 **43 ms** and p95 **61 ms**. G15 fanout evidence: two dispatchers, 250 additional recipients (252 in the test), 100 concurrent scheduled readers and maximum 32 admitted database readers, reported effect-once. G13 concurrent campaign: four separate authorized principals, 5,002 shipments and four distinct reports.

## Gate disposition

The final ledger intentionally did **not** declare the complete 50 acceptance scenarios PASS. It records two PASS, 44 BLOCKED and four NOT EXECUTED. These are acceptance states, not failing test counts. The retained automated matrix follows, without changing its verdicts:

| Scenario | Recorded disposition |
| --- | --- |
| G13-S01 | BLOCKED |
| G13-S02 | PASS |
| G13-S03 | BLOCKED |
| G13-S04 | BLOCKED |
| G13-S05 | BLOCKED |
| G13-S06 | BLOCKED |
| G13-S07 | BLOCKED |
| G13-S08 | BLOCKED |
| G13-S09 | BLOCKED |
| G13-S10 | BLOCKED |
| G13-S11 | BLOCKED |
| G13-S12 | BLOCKED |
| G13-S13 | BLOCKED |
| G13-S14 | BLOCKED |
| G13-S15 | BLOCKED |
| G13-S16 | BLOCKED |
| G13-S17 | BLOCKED |
| G13-S18 | NOT EXECUTED |
| G13-S19 | BLOCKED |
| G13-S20 | BLOCKED |
| G13-S21 | BLOCKED |
| G13-S22 | BLOCKED |
| G13-S23 | BLOCKED |
| G13-S24 | BLOCKED |
| G13-S25 | NOT EXECUTED |
| G15-S01 | BLOCKED |
| G15-S02 | PASS |
| G15-S03 | BLOCKED |
| G15-S04 | BLOCKED |
| G15-S05 | BLOCKED |
| G15-S06 | BLOCKED |
| G15-S07 | BLOCKED |
| G15-S08 | BLOCKED |
| G15-S09 | BLOCKED |
| G15-S10 | BLOCKED |
| G15-S11 | BLOCKED |
| G15-S12 | BLOCKED |
| G15-S13 | BLOCKED |
| G15-S14 | BLOCKED |
| G15-S15 | NOT EXECUTED |
| G15-S16 | BLOCKED |
| G15-S17 | BLOCKED |
| G15-S18 | BLOCKED |
| G15-S19 | BLOCKED |
| G15-S20 | BLOCKED |
| G15-S21 | BLOCKED |
| G15-S22 | BLOCKED |
| G15-S23 | BLOCKED |
| G15-S24 | BLOCKED |
| G15-S25 | NOT EXECUTED |

The following items require closure before claiming both gates accepted:

1. **G13-S01 and S20, source and capacity:** The 170k-row `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` artifact is explicitly `explain-admin-170k.json` and was executed by `CapturePrivilegedPlan` under a privileged database connection. G13-S20 additionally requires plans with the real restricted application role for empty, small, medium and skewed 170k workloads. Its full query-plan matrix was not retained. G13's numerical permanent storage/capacity and expired-report cleanup limit remain undecided. The measured 1.325 GiB relation growth demonstrates why an explicit resource decision is necessary. Provisional p50/p95 approval does not close the rest of S20.
2. **G13-S09, S17, S18, evidence mapping:** The ledger expects `SUITE:M10CorrectionAuthorityTests`, which is absent from the retained regression run. The `RequiresExplicitRepresentableUtcTimestamp` parameterized unit cases and `MigrationSnapshotTests` did execute in TRX, but the ledger's simple matching missed them and reported missing references. Fix the index mapping and substantiate any still-absent M10 correction-authority checks before crediting these requirements.
3. **G13-S24, mutation breadth:** Passing 13 KPI source and two SQL permission mutants does not show every mutation named in S24, notably grant bypass, revocation bypass, forged report cursor and rollback, each with a targeted kill. Existing negative runtime assertions provide some related evidence but do not automatically satisfy the specified mutation matrix.
4. **G15-S13 and S23, actual crash boundaries:** The archived SIGKILL phases are G13BeforeReportCommit, G13AfterReportCommit, G15BeforeHistoryCommit and G15AfterReceiptCommit. G15-S13 additionally requires interruption around intent claim, fanout commit and API response. The response-loss test is useful but not equivalent to a killed process at every required boundary. The conditionally approved M15-P10 has positive rollback and accepted-M11 regression evidence, but the full specified interruption and recovery proof remains incomplete.
5. **G15-S21 and S24, negative and resilience breadth:** The archived fanout retry and eight notification mutants do not independently demonstrate all required SQL outage/cancellation behavior and mutation categories. In particular, no separately named source-history uniqueness or cross-tenant predicate mutant appears in the G15 manifest. Full evidence for these cases remains to be reconciled or obtained.
6. **G13/G15 remaining scenarios and S25:** The rest of the 44 BLOCKED entries require scenario-by-scenario verification of their complete expected behavior, including role/session edges, negative cases, migration/grant preservation, evidence-safe failure handling, UI states and regression scope. Two S25 entries require an independently accepted review and explicit milestone decisions. Neither the automated artifact check nor this exception-focused review is an unconditional S25 acceptance.

## Boundaries preserved

M15 P01-P14 approvals remain exactly as recorded, with P10 conditioned on full transactional recovery proof. P12's provisional periods do not authorize notification deletion or automated retention cleanup. M15 dispatcher remains disabled by default. Existing M9 transport hold, M10 projector default-off, M11 evaluator opt-in, null ETA, immutable M11/M16 records and accepted-main boundaries must not change to resolve these findings.

The prior failed runs are historical and remain valid for their own SHAs. A passing technical run on `e4bf9cd1527f85f554baf623e90805d96d08ac1a` cannot be transferred to an implementation-modified successor SHA. A documentation-only review update should preserve the tested implementation SHA and label the documentation branch separately.

## Integration decision

**Do not merge the verification branch into `main` yet.** Its ancestry is clean (149 commits ahead, zero behind against main at review time), but Git mergeability is not equivalent to G13/G15 gate acceptance. No source defect is inferred merely from a BLOCKED scenario. Close the concrete gaps with targeted evidence or a separately approved, accurately recorded requirement decision. Reconcile all 50 scenario dispositions and complete both independent S25 reviews before authorizing merge. No production deployment, migration, dispatcher activation or deletion is included in this review.
