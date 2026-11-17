# G17-S25 Final Independent Review Handoff Packet, Revision 2

**Recorded:** 2026-11-17
**Frozen source under review:** `634053c4d0c3a2cb8c51b2c29fda87b126e08993` on branch `feat/harborops-g17-final-verification-60b4bf`
**Status:** **READY FOR INDEPENDENT S25 REVIEW. G17 NOT ACCEPTED; acceptance pending.**
**Signatures:** none. No reviewer has signed S25-A01, S25-A02 or S25-A03. Nothing in this packet is an approval, and the contributor who implemented and tested these changes cannot supply the independent review.
**Supersedes:** the [2026-11-16 packet](M17_G17_S25_FINAL_HANDOFF_PACKET_20261116.md) (source `7cc25dab`, attempt k4), which is kept unchanged for history. Nothing from k4 is used as evidence for this source.

This packet lets a separate reviewer inspect the real source and raw evidence without relying on summaries. Every figure can be re-derived from the artifacts named here.

## 1. What is being reviewed

- **Source:** the commit above. Documentation commits that follow it change no executable file; confirm with `git diff 634053c4..<docs head> --stat -- . ":(exclude)docs" ":(exclude)README.md"`, which must be empty.
- **Campaign:** local attempt **m4**, 51 of 51 stages, 72 of 72 technical witnesses, 3 independent assertions pending. [Consolidated result and findings register](M17_G17_CONSOLIDATED_FINAL_REMEDIATION_20261117.md).
- **Changes since the k4 source:** three product files (`DatabaseReadinessCheck.cs`, `M17Telemetry.cs`, `M17TelemetryMiddleware.cs`), the diagnostic producer and its strict verifier, the Docker ownership and cleanup scripts, the evidence archive module, the first-failure real-stack check and its verifier, the worker journey top-up and sampling module, witness binding, the planning checker rules, new and extended alert rule tests, new .NET tests, and runner changes. Each is listed with its cause, correction and negative control in the findings register (R01 to R16).
- **Not reviewed or changed:** accepted milestone branches, `main`, the M9 transport hold, M16 audit protections, tenant isolation, authentication, privacy rules, default-off observability and every approved threshold.

## 2. Evidence access (local, not published)

| Item | Where |
| --- | --- |
| Raw campaign evidence | `E:\HarborOps-Verification\g17-final2\final-m4-evidence` and archive `g17-m4-evidence.zip` (real zip, 69 files, SHA-256 `0a5324bcb33987fde1a565684ea65e356af81c44d34ff85f4d554f0c10098d0b`) with `g17-m4-evidence.zip.inventory.json` |
| Manifest, ledger, run status | `manifest.json` `978ddfb9...`, `g17-technical-assertion-ledger.json` `d936408e...`, `run-status.json` `b33447ad...` (full values in the consolidated result) |
| Preserved failed attempts | `final-m1-evidence`, `final-m2-evidence`, `final-m3-evidence` and the matching `-run.log` files in the same folder; k1 to k4 in `E:\HarborOps-Verification\g17-final` |
| Further archives | `g17-m-worker-topup-rehearsal-evidence.zip` `1ffb13b7...` (forced top-up rehearsal and the other worker rehearsals with raw results and traces), `g17-m-mutation-and-control-evidence.zip` `e95a906d...` (mutation JSONL, control logs, real-stack first-failure red and green runs, tooling, derived non-kill status), `g17-m-run-logs.zip` `b679be2e...`, `g17-k1-k3-failed-attempt-evidence.zip` `5b201b53...`; index `cfbd6ed0...` (replaces the earlier value) |
| Mutation retest archive | `g17-s25-mutation-retest-evidence.zip` SHA-256 `c88759b9e6f9091626153de60128ab95e6007561857120cde102d838d0175ca6` (`g17-s25-mutation-retest-evidence-r2.zip`, 405 files, extraction-verified, `.inventory.json` and `.sha256` beside it): reconstruction of the 17 candidates, per-mutant retest records and raw output from the local container and from hosted Windows and Ubuntu runners, adversarial demonstrations, source fingerprints, derived inventories of the supplied archives. Same content is committed in `docs/m17-s25-mutation-retest-20261117/` |
| Failed-attempt archives | `g17-m1-failed-attempt-evidence.zip` `7a9da5e7...`, `g17-m2-failed-attempt-evidence.zip` `14f428eb...` and **`g17-m3-failed-attempt-evidence.zip` `a92f2a4e...` (the failed P09 campaign at this same source)**, real zips verified by extraction, with `g17-final2-attempt-archive-index.json` (`cfbd6ed0...`) listing all eight archives and the four run logs with hashes |
| P09 measurements | `m17-performance-raw.json` of m2, m3 (failed) and m4 in their evidence folders |
| Rehearsals and mutation outputs | `first-failure-*`, `worker-rehearsal-*`, `rehearsal-*`, `mutation\*.jsonl` and the preflight logs in `E:\HarborOps-Verification\g17-final2` |
| Original r1c, r2 and r3 evidence | `docs/m17-s25-p09-controlled-protocol-20261115/results/` (committed, byte-identical to before this work) |
| Prior independent bundle and mutation evidence | `docs/m17-s25-mutation-evidence-20261115/` |

The reviewer should obtain the folder from the contributor through a private channel. It has not been pushed anywhere.

## 3. Reproducing the checks without trusting this packet

```bash
git checkout 634053c4d0c3a2cb8c51b2c29fda87b126e08993
git status --porcelain                      # must be empty
node scripts/verify-g17-manifest.mjs "E:\HarborOps-Verification\g17-final2\final-m4-evidence\manifest.json" 634053c4d0c3a2cb8c51b2c29fda87b126e08993
node scripts/audit-g17-test-coverage.mjs --strict   # 25 scenarios, 75 clauses, 72 authored, 3 independent
node scripts/g17-evidence-archive.mjs verify "E:\HarborOps-Verification\g17-final2\g17-m4-evidence.zip"
node --test scripts/*.test.mjs              # 520 cases expected
node scripts/check-planning-docs.mjs
```

To rederive the ledger, copy the evidence folder, delete the copied `g17-technical-assertion-ledger.json`, run `node scripts/reconcile-g17-assertions.mjs <copy> 634053c4d0c3a2cb8c51b2c29fda87b126e08993` and compare the new ledger record by record with the original (the script refuses to overwrite an existing ledger). The contributor's own run of this comparison found the two files identical. To recompute P09, take the per-request samples in `m17-performance-raw.json`, pool them per route and telemetry mode, take the 95th percentile (rank `ceil(0.95 n)`) and compare telemetry on against off; the recorded values are baseline -3.56% and authorized -0.27%, mean peak memory +10.81%.

## 4. Disposition of every requirement

Legend: **TECHNICAL PASS** means a source-locked executable witness passed in m4 and the ledger re-derives. It is not independently reviewed. **PENDING** means it needs a reviewer. No row is an acceptance.

| Requirement | Disposition at `634053c4` |
| --- | --- |
| G17-S01 to G17-S24, 72 atomic assertions | **TECHNICAL PASS, pending independent review** of the witness behavior (table in section 10) |
| G17-S25-A01 separate reviewer checks raw source, privacy, metrics and full cases | **PENDING** |
| G17-S25-A02 policy and procedural exceptions disclosed | **PENDING** (disclosures are in section 7; review and attestation are the reviewer's) |
| G17-S25-A03 reviewer issues explicit signed PASS or NOT PASS | **PENDING** |
| S25-F01 rendered rate, 5xx and p95 states (S18, S19) | **TECHNICAL PASS at m4**, pending review |
| S25-F02 page-originated correlated fault journey (S20) | **TECHNICAL PASS at m4**, pending review |
| S25-F03 participating-service log capture and canary control (S05, S13, S14) | **TECHNICAL PASS at m4**, pending review |
| S25-F04 hidden Playwright metadata records (S24) | **TECHNICAL PASS at m4** (manifest 68 of 68 verified), pending review |
| S25-F05 / P09 | **QUALIFIED PASS (policy decision of 2026-11-16, unchanged).** m4 PASS: baseline -3.56%, authorized -0.27%, memory +10.81% against 10% and 15%. **m3 at the same source FAILED** (baseline +12.07%) and is preserved. f6 FAIL and its unexplained burst preserved; optimization before G24 release readiness |
| S25-F06 procedural separation | **PENDING.** Requires a separate reviewer, or an explicitly recorded and approved independence exception. None is recorded |
| S25-F07 restart and cancellation witness (S15) | **TECHNICAL PASS at m4**, pending review |
| S25-F08 storage exhaustion with retained M8/M11/M15/M16 history (S22) | **TECHNICAL PASS at m4**, pending review |
| First 5xx burst visible in every operation family (R01, R02) | **FIXED and WITNESSED on a real stack** (first-failure stage, 8 steps, exact counts) and by promtool cases; pending review |
| Readiness bound of 5 seconds (R03, R04, R06) | **FIXED, regression-tested and real-stack verified** (503, at most 4.51 s under ten concurrent probes); pending review |
| Diagnostic state hash producer | **CORRECTED and VERIFIED prospectively and hardened** (section 6). Historical r1c, r2 and r3 mismatch **preserved, not repaired** |
| Disposable Docker cleanup (R10) | **FIXED**: exact ownership, cleanup failure is a failure, no subnet left behind; m4 cleanup REMOVED with 0 containers and 0 networks left |
| Planning documents checker (R12) | **PASS** (231 files). The four earlier failures were a stale checker, not stale documents |
| Mutation survivors | Earlier 11 non-material survivors unchanged. The 17 distinct mutants of this revision that had no killing run were retested individually and classified in the [mutation retest record](M17_G17_S25_MUTATION_RETEST_20261117.md): 3 killed on the frozen source, 9 killed by new or tightened behavioural controls on candidate `1489a80e` (tests plus a bounded settle in the diagnostic producer, not yet through a full campaign), 5 individually justified as redundant or unreachable. Reviewer attention requested for the disclosed residual M04 and for the producer change |
| GitHub Actions at this source | **NOT RUN, not authorized.** Only local verification exists |
| Merge to `main`, deployment, release, G18 start | **NOT AUTHORIZED** |

## 5. Mutation and negative-control evidence

| Subject | Control | Result |
| --- | --- | --- |
| Verifier modules already covered | Strict exhaustive campaign of 805 candidates (earlier source) | 790 runtime kills, 4 load-time kills, 11 survivors classified non-material (unchanged) |
| Alert rules | 21 mutants against the promtool cases | 21 of 21 killed |
| First-failure evidence verifier | single-fact mutants | 38 of 38 killed (second round; two first-round survivors were closed by added tests) |
| C# telemetry seeding and classification | 12 mutants | 12 of 12 killed |
| Readiness check | 9 mutants | 9 of 9 killed |
| Planning current-state rules | 19 mutants | 19 of 19 killed (two earlier rounds were void because their unmutated control failed and are preserved) |
| Strict diagnostic verifier | single-fact mutants | 38 killed, 1 survivor (stream name accepted at the first position) and 1 mutant whose pattern referred to code that did not yet exist; both resolved in the retest record (survivor killed by an added test on candidate `60576b16`) |
| Diagnostic producer | single-fact mutants | 37 killed, 2 survivors (child exit not awaited, lingering child tolerated); retested on a hosted Windows runner: the exit mutant was detected only by a source text assertion and the lingering child mutant survived; both are killed by new behavioural controls on candidate `1489a80e` |
| Docker ownership and cleanup | single-fact mutants | 21 killed, 5 survivors and 1 pattern not found (a locator aimed at the wrong file); 3 closed by new behavioural controls and 1 by a tightened message assertion (confirmed on a hosted Ubuntu runner with Docker), 2 label guards unreachable behind the daemon's exact label filter, and the volume mutant was previously detected only by a source text assertion |
| Evidence archive | single-fact mutants | 18 killed, 3 survivors classified individually as redundant (2 verdict-equivalent, 1 defence in depth against a misbehaving extractor) |
| Witness binding and reconcile binding | single-fact mutants | 12 killed, 4 survivors: reconcile binding 2 killed on the frozen source (their tests post-date the mutation round), witness binding 2 killed by new tests on candidate `60576b16` |
| Manifest and witness changes, worker top-up rules | single-fact mutants | 5 of 5 and 6 of 6 killed |
| Real-stack first-failure check | old source against the same check | fails, as required; new source passes |
| Real-stack readiness | stopped PostgreSQL, ten concurrent probes | all 503, maximum 4.51 s |
| Worker journey | forced top-up rehearsal (requirement raised to 8 in a generated copy; reported source `826e096f`, not the final code) | 90 receipts, 8 traced, 8 pairs, 1 top-up batch; supplemental only, not an exact-source witness |

Mutation kills count only assertion-level test failures. Compile errors, infrastructure failures and timeouts were never counted as kills.

## 6. Diagnostic checksum issue and the prospective fix

The independent review found that `diag-hashes.txt` in r1c, r2 and r3 disagrees with the final `diag-state.json` in each run, because the producer hashed the state and only then added `stopped_utc`. Recalculated from the committed files: r1c lists 332 bytes and the file is 373, r2 lists 330 and 371, r3 lists 330 and 371.

- **Preserved:** the three historical records are unchanged and a test pins their exact mismatch. They were not reconstructed.
- **Fixed (k4 work):** `docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1` writes the finalized state first and the hash list last without listing itself.
- **Hardened (this work, R07 to R09):** Stop waits for and verifies child exit, detects late writes, revalidates process and container identity before acting, rejects subdirectories and links, and the verifier is strict about timestamps, links and coverage. The historical permissive mode never reports success.
- **Not claimed:** no P09 campaign has used the hardened producer for controlled diagnostics evidence yet.

## 7. Procedural exceptions and limitations (for S25-A02)

1. **Eight local attempts, six failed.** k1, k2, k3, m1, m2 and m3 failed for the reasons in the consolidated result; each is preserved. None changed a requirement or threshold. m3 was a performance failure; m4 ran the identical source after the failure and a recommendation were presented and a new complete campaign was approved.
2. **P09 is close to its limits and varies between campaigns.** Four counted measurements at the current or preceding source: k4 PASS, m2 PASS (memory +14.99% against 15%), m3 FAIL (baseline +12.07%), m4 PASS. Round 3 of m3 showed a sustained tail disturbance whose cause was not resolved. The QUALIFIED PASS status and the obligation to optimize before G24 are unchanged. A reviewer should weigh all four results.
3. **Implementer is also the verifier of this packet.** All local verification here was run by the contributor who made the corrections. That is why every row says "pending independent review". The post-run recalculation (manifest, ledger, P09 arithmetic, archive extraction) used fresh processes but the same contributor.
4. **Local only.** No hosted GitHub Actions run exists for `634053c4`.
5. **Raw evidence is not in the repository.** It is large and unpublished by design; the manifest and hashes in the repository identify it exactly.
6. **Worker journey top-up.** The journey tops up receipts until at least 3 are traced. Whether a receipt is traced is a deterministic keyed decision known before any trace is looked up, so the top-up does not select results; the two real trace pair gate is unchanged. m4 needed no top-up (4 traced of 50, `top_up_batches` 0), so the final top-up code at `634053c4` was not exercised by any retained run. The forced rehearsal executed a generated, modified copy at an earlier source (`826e096f`) and is not an exact-source witness; it is preserved unchanged. The branch is not mandatory for any technical atomic assertion (the end-of-stage conditions are the same on both paths) and is a reliability measure for the roughly 11 percent of runs in which the first 50 receipts hold fewer than 3 traced journeys. Limitation disclosed for S25-A02, see the [mutation retest record](M17_G17_S25_MUTATION_RETEST_20261117.md).
7. **Documented limits of R01.** The first sample of any non-5xx series that first appears during traffic is not counted by Prometheus; the denominator design prevents this from suppressing the 5xx alert. Seeding is not claimed to solve every start-up race.
8. **No benchmark was repeated to obtain a result.** m3 and m4 are both complete campaigns on the same source and both are reported.

## 8. Exactly what remains for S25

| ID | Needs | Cannot be supplied by |
| --- | --- | --- |
| **S25-A01** | A separate reviewer with access to the source commit and the raw evidence confirms the source, privacy boundaries, metrics, and that the full 72-case technical campaign is real and complete (check the table in section 10 against the raw files, spot-check negative controls) | the implementing contributor, this packet, the ledger alone, or a green run |
| **S25-A02** | The reviewer confirms that policy and procedural exceptions in section 7 and the P09 qualified decision are disclosed and acceptable, or records objections | the contributor |
| **S25-A03** | The reviewer issues an explicit signed PASS or NOT PASS recommendation before any G17 acceptance decision | the contributor, any automation, or this document |
| **S25-F06** | Either a genuinely separate signed review exists, or an independence exception is explicitly approved and recorded | this work |
| **G17 acceptance** | A distinct, recorded acceptance decision after S25 | this work |

Suggested reviewer procedure: (1) run section 3; (2) read the findings register and open the changed files; (3) walk section 10, opening the named witness and the raw artifact for each row, at least all rows of S05, S09, S13, S15, S17, S20, S21 and S22, which carry the highest risk; (4) try one deliberate corruption of a copy of the evidence and confirm `verify-g17-manifest.mjs` rejects it; (5) compare the m3 and m4 P09 files and decide whether the variation is acceptable; (6) record the PASS or NOT PASS recommendation in a new document written by the reviewer.

## 9. Boundaries that must stay true

M9 transport restrictions and the M16 audit protections are unchanged; the regression stage ran 34 accepted-milestone tests with M17 opted in against an unreachable collector and all passed. Telemetry stays default off. P09 numerical budgets are unchanged. Optimization is scheduled just before G24 release readiness unless a source-locked gate fails earlier, in which case the remedy is agreed first.

## 10. The 25 scenarios and 75 atomic assertions at `634053c4`

Derived by script from the recorded ledger, not edited by hand: each row was compared with the m4 ledger record for the same assertion (witness path, case, artifact and status) before this table was written. Technical rows mean the named witness executed and passed in m4; none has been reviewed by anyone other than the contributor.

| Assertion | Required outcome | Witness (path :: case) | Raw artifact in the k4 evidence | Disposition |
| --- | --- | --- | --- | --- |
| **G17-S01** | **Source/contract conformity** | | | |
| G17-S01-A01 | all four work packages implemented | `scripts/g17-source-boundary.test.mjs` :: M17 four work packages exist with actual referenced production and verification boundaries | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S01-A02 | approved P01-P09 and exact instrument mapping match source | `scripts/g17-source-boundary.test.mjs` :: every approved M17 policy has a concrete source-side verification control | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S01-A03 | no unapproved default service, API or migration changes | `scripts/g17-source-boundary.test.mjs` :: source diff does not change the accepted default Compose or database migrations | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| **G17-S02** | **Metric schema and type** | | | |
| G17-S02-A01 | each metric has exact name/type/unit | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: EveryLocalInstrumentHasTheApprovedNameTypeAndUnit | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S02-A02 | no duplicate request counters | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: RequestCountIsDerivedOnlyFromDurationHistogramAndNotDuplicated | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S02-A03 | histogram produces nonzero latency samples | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: RealNonzeroRequestAndDatabaseDurationsReachMeterListeners | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S03** | **Existing health preservation** | | | |
| G17-S03-A01 | live stays responsive with DB down | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S03-A02 | ready goes unavailable on DB stop or absent seed within budget | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S03-A03 | existing JSON/status paths unchanged | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S04** | **Server-controlled correlation** | | | |
| G17-S04-A01 | response correlation generated on server | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S04-A02 | error and structured log scope use same value | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S04-A03 | client-supplied correlation cannot spoof it | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S05** | **Label allowlist** | | | |
| G17-S05-A01 | metric attributes contain only approved keys | `tests/HarborOps.IntegrationTests/M17MetricSchemaTests.cs` :: CustomMetricLabelsRejectUntrustedHostsPathsAndBusinessIdentifiers | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S05-A02 | no user/tenant/request/SQL fields | `scripts/check-g17-export-privacy.mjs` :: owned-sanitized-service-logs | `m17-export-privacy-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S05-A03 | unknown route has stable fallback | `tests/HarborOps.IntegrationTests/M17TelemetryContractTests.cs` :: TenThousandAttackerChosenPathsUseOneBoundedOperationSeries | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S06** | **Cardinality and memory bound** | | | |
| G17-S06-A01 | 10,000 distinct routes/identifiers stay within approved active series cap | `tests/browser/m17/cardinality.spec.mjs` :: ten thousand hostile HTTP requests remain within complete exported-series budget | `m17-cardinality-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S06-A02 | overflow coalesces | `tests/HarborOps.IntegrationTests/M17TelemetryContractTests.cs` :: TenThousandAttackerChosenPathsUseOneBoundedOperationSeries | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S06-A03 | memory does not grow without bound | `scripts/check-g17-cardinality-memory.mjs` :: FOUR_TEN_THOUSAND_PATH_STRESS_WAVES | `m17-cardinality-memory-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S07** | **Valid context propagation** | | | |
| G17-S07-A01 | W3C valid trace context accepted without altering auth | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: ValidW3CTraceparentConnectsApiRequestAndBoundedDatabaseSpan | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S07-A02 | API -> safe DB work connected by trace | `tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs` :: HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S07-A03 | original audit correlation preserved | `tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs` :: OptedInTrackingAcceptancePreservesImmutableAuditCorrelation | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S08** | **Invalid context rejection** | | | |
| G17-S08-A01 | malformed traceparent/tracestate handled safely | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: MalformedTraceContextDoesNotTurnDependencyFailureIntoUnhandled500 | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S08-A02 | oversized baggage ignored | `scripts/check-g17-export-privacy.mjs` :: baggage:"secret=" | `m17-export-privacy-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S08-A03 | no raw header is exported, no 500 | `scripts/check-g17-malformed-export.mjs` :: malformed-context-export-safe | `m17-malformed-export-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S09** | **DB timing and outages** | | | |
| G17-S09-A01 | successful bounded DB operation emits duration/outcome | `tests/HarborOps.IntegrationTests/M17SuccessfulDatabaseAndAuditTests.cs` :: HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S09-A02 | outage emits classified failure without secret SQL | `scripts/check-g17-database-fault.mjs` :: real-db-outage-keeps-live-and-errors-classified | `m17-database-fault-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S09-A03 | business error contract remains unchanged | `tests/HarborOps.ContractTests/M17HttpContractTests.cs` :: EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts | `g17-contract.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S10** | **Worker status and hold** | | | |
| G17-S10-A01 | worker that is intentionally disabled/held remains labelled disabled/held | `tests/browser/m17/dashboard.spec.mjs` :: authenticated samples match the disposable ready API and suppress held workers | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S10-A02 | enabled disposable worker emits correct attempts/outcomes | `tests/HarborOps.IntegrationTests/M17WorkerShellFunctionalTests.cs` :: ActualWorkerShellEmitsProcessedAndReconciliationDispositionsWithoutDuplicateEffects | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S10-A03 | no worker auto-start | `tests/HarborOps.IntegrationTests/M17WorkerShellFunctionalTests.cs` :: DefaultApiHostDoesNotRegisterTheHeldTrackingWorker | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S11** | **Asynchronous causal linking** | | | |
| G17-S11-A01 | authorized worker work links safely to source or documented correlation | `tests/HarborOps.IntegrationTests/M17RestrictedWorkerReaderTests.cs` :: RestrictedWorkerCanLinkOnlyOwnedReceiptAndCannotReadM16AuditOrModifyHistory | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S11-A02 | fabricated parent/tenant trace context rejected | `tests/HarborOps.IntegrationTests/M17CorrelationJourneyTests.cs` :: ForgedTenantAndW3CParentHeadersCannotControlDurableReceiptLink | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S11-A03 | replay/retry has unique bounded spans | `tests/HarborOps.IntegrationTests/M17ReplayRedriveSpanTests.cs` :: RealReplayAndAuthorizedRedriveEmitOneSpanPerWorkerAttemptNotPerHttpReplay | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S12** | **Bounded exporter failures** | | | |
| G17-S12-A01 | collector down does not change API response, transaction or readiness | `tests/HarborOps.IntegrationTests/M17ExporterOutageMutationTests.cs` :: UnreachableCollectorCannotChangeAuthorizedMutationOrConflictInvariants | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S12-A02 | export queue bounded/drops counted | `tests/HarborOps.IntegrationTests/M17ExporterQueuePressureTests.cs` :: QueueFullDropsAreCountedWithoutBlockingSpanProducers | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S12-A03 | exporter recoverable without manual data repair | `scripts/check-g17-collector-fault.mjs` :: collector-recovery-automatic | `m17-collector-fault-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S13** | **Security redaction** | | | |
| G17-S13-A01 | secrets, cookies, personal canaries and SQL values absent from logs, spans and metric labels | `scripts/check-g17-export-privacy.mjs` :: owned-sanitized-service-logs | `m17-export-privacy-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S13-A02 | sanitized errors still diagnose component and type | `scripts/check-g17-database-fault.mjs` :: database-alert-identifies-only-safe-component | `m17-database-fault-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S13-A03 | retained M16 audit is not exposed | `tests/HarborOps.IntegrationTests/M17RestrictedWorkerReaderTests.cs` :: RestrictedWorkerCanLinkOnlyOwnedReceiptAndCannotReadM16AuditOrModifyHistory | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S14** | **Tenant and authorization isolation** | | | |
| G17-S14-A01 | cross-organization request produces same denial as baseline | `tests/HarborOps.IntegrationTests/M17TenantIsolationTests.cs` :: OptedInTelemetryPreservesTenantDenialsAndAnalystWriteRevocation | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| G17-S14-A02 | telemetry does not disclose tenant detail | `scripts/check-g17-worker-journey.mjs` :: Prometheus exported private tenant/source/account identifiers | `m17-worker-journey-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S14-A03 | instrumentation cannot elevate a role | `tests/HarborOps.IntegrationTests/M17TenantIsolationTests.cs` :: OptedInTelemetryPreservesTenantDenialsAndAnalystWriteRevocation | `g17-integration.trx` | TECHNICAL PASS (not independently reviewed) |
| **G17-S15** | **Restart and cancellation** | | | |
| G17-S15-A01 | process restart and canceled request do not leak stale spans or requests | `scripts/check-g17-restart.mjs` :: actual_spans_inspected | `m17-restart-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S15-A02 | shutdown drain is bounded | `scripts/check-g17-restart.mjs` :: owned-api-shutdown-bounded | `m17-restart-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S15-A03 | no duplicated business effect or worker lease change | `scripts/check-g17-restart.mjs` :: restart-recovers-health-without-business-duplication | `m17-restart-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S16** | **Private stack exposure** | | | |
| G17-S16-A01 | default Compose contains no collector/dashboard | `scripts/check-m17-source.test.mjs` :: the default stack does not start or expose M17 components | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S16-A02 | optional dashboard bound loopback | `scripts/check-g17-network.mjs` :: dashboard_listener | `m17-network-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S16-A03 | collector not exposed externally, untrusted reads denied | `scripts/check-g17-network.mjs` :: Untrusted host client reached protected monitoring port | `m17-network-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S17** | **Alert triggers and clears** | | | |
| G17-S17-A01 | controlled 5xx/latency/readiness faults trigger configured alerts | `scripts/check-g17-alert-matrix.mjs` :: real-http-5xx-rate-fires-reference-alert | `m17-alert-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S17-A02 | threshold/window evidence exact | `monitoring/m17/alert-test.yaml` :: Readiness must not remain critical | `m17-promtool-test.txt` | TECHNICAL PASS (not independently reviewed) |
| G17-S17-A03 | clear/hysteresis works after recovery | `monitoring/m17/alert-test.yaml` :: Latency recovers below | `m17-promtool-test.txt` | TECHNICAL PASS (not independently reviewed) |
| **G17-S18** | **Low-traffic and disabled suppression** | | | |
| G17-S18-A01 | zero/low traffic is unknown/insufficient, not healthy | `tests/browser/m17/dashboard.spec.mjs` :: zero and insufficient traffic render unknown rather than a healthy zero | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S18-A02 | held workers cause no false failure alert | `scripts/check-g17-worker.mjs` :: fault-worker-stop-suppresses-stale-backlog | `m17-worker-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S18-A03 | cool-down deduplicates repeat alerts | `monitoring/m17/alert-test.yaml` :: S18-A03 repeat suppression | `m17-promtool-test.txt` | TECHNICAL PASS (not independently reviewed) |
| **G17-S19** | **Local dashboard rendering** | | | |
| G17-S19-A01 | critical panels show actual live measurement | `tests/browser/m17/dashboard.spec.mjs` :: critical monitoring panels use live series and no-data views are explicitly unknown | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S19-A02 | unknown/unavailable have explicit distinct states | `tests/browser/m17/dashboard.spec.mjs` :: rendered readiness distinguishes missing data from real downtime in three browsers | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S19-A03 | alert links only sanitized diagnostic evidence | `tests/browser/m17/dashboard.spec.mjs` :: alert dashboard links stay local and never embed private diagnostic parameters | `m17-browser-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S20** | **End-to-end fault journey** | | | |
| G17-S20-A01 | browser/API fault yields server correlation | `scripts/check-g17-diagnostic-journey.mjs` :: page-originated-same-request | `m17-diagnostic-journey-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S20-A02 | trace to DB/worker or documented async bridge resolves | `scripts/check-g17-diagnostic-journey.mjs` :: real-trace-to-db | `m17-diagnostic-journey-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S20-A03 | dashboard alert names correct component without private payload | `scripts/check-g17-diagnostic-journey.mjs` :: same-fault-rendered-alert | `m17-diagnostic-journey-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S21** | **Performance overhead** | | | |
| G17-S21-A01 | paired workload measurement meets approved p95 and memory budgets | `scripts/check-g17-performance.mjs` :: measured_routes | `m17-performance-raw.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S21-A02 | exact environment/variance reported | `scripts/check-g17-performance.mjs` :: THREE_MATCHED_PAIRS | `m17-performance-raw.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S21-A03 | pressure does not block business | `scripts/check-g17-pressure-matrix.mjs` :: business-under-tenfold-pressure-and-exporter-outage | `m17-pressure-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S22** | **Storage and retention isolation** | | | |
| G17-S22-A01 | local telemetry capped and expires only its own disposable data | `scripts/check-g17-retention-matrix.mjs` :: m17-tempo-retention-results.json | `m17-retention-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S22-A02 | full store degrades observation safely | `scripts/check-g17-storage.mjs` :: full-telemetry-store | `m17-storage-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S22-A03 | M8/M11/M15/M16 retained data unchanged | `scripts/check-g17-storage.mjs` :: all-application-history-byte-identical | `m17-storage-results.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S23** | **Defaults and regression** | | | |
| G17-S23-A01 | M9/M10/M11/M13/M15 opt-in/hold semantics unchanged | `scripts/check-g17-regression-matrix.mjs` :: M13AdmissionControlAcceptanceTests | `g17-regression-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S23-A02 | accepted authorization/health/audit APIs preserved | `scripts/check-g17-regression-matrix.mjs` :: G15AuditAcceptanceTests | `g17-regression-matrix-results.json` | TECHNICAL PASS (not independently reviewed) |
| G17-S23-A03 | changed source build, .NET regression and targeted browser suites pass | `scripts/verify-g17-manifest.mjs` :: Completed run has missing evidence or a failed stage | `run-status.json` | TECHNICAL PASS (not independently reviewed) |
| **G17-S24** | **Evidence integrity** | | | |
| G17-S24-A01 | every claimed assertion maps to real source-locked result | `scripts/reconcile-g17-assertions.test.mjs` :: rejects a missing assertion evidence row even when other suites pass | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S24-A02 | source hashes and manifests complete/no unlisted files | `scripts/check-g17-manifest.test.mjs` :: altered evidence cannot pass hash verification | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| G17-S24-A03 | negative control rejects tampering, skip and wrong SHA | `scripts/g17-adversarial-evidence.test.mjs` :: G17 verifier rejects tampering, skipped cases, duplicate results and wrong source SHA | `g17-node-tests.xml` | TECHNICAL PASS (not independently reviewed) |
| **G17-S25** | **Independent review and gate recommendation** | | | |
| G17-S25-A01 | separate reviewer checks raw source, privacy, metrics and full cases | none: independent reviewer | none | PENDING independent S25 review |
| G17-S25-A02 | policy and procedural exceptions disclosed | none: independent reviewer | none | PENDING independent S25 review |
| G17-S25-A03 | reviewer issues explicit signed PASS/NOT PASS before G17 acceptance | none: independent reviewer | none | PENDING independent S25 review |

**G17 NOT ACCEPTED; acceptance pending.**
