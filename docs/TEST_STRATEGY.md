# Test and Verification Strategy

**Current verified baseline (2026-11-08):** G0-G12 and G16 accepted and merged. [G11/G12 closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) records the 388/388 integration, 48/48 browser, 17/17 effective mutant and N01 10/10 evidence. Earlier source-only and pending verification banners are historical, not current. Preserve the M9 ARRI/DEPA hold, 10,000 unresolved-item cap, default-off M10 projector, null ETA and opt-in M11 evaluator; no deployment.

## Current G10 source verification boundary

**Current M10/G10 verification checkpoint (2026-11-07):** G10 S01/S25 reviewed and accepted; [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) integrated the remediation. The correction-authority campaign is source-locked to `a26d19a`, while final upgrade, configuration and targeted tests are source-locked to `e3002bd`. Preserve original A1-A7 evidence separately. [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md).

**Final merge-readiness update (2026-11-07):** Two review findings are closed in source and verified on `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343` ([record](M10_G10_FINAL_MERGE_READINESS_20261107.md)): the accepted twelfth migration is restored unchanged and the NULL-safe projector scope function now arrives through the thirteenth migration, and the default `compose.yaml` requires `HARBOROPS_M10_CORRECTION_PROOF_KEY` for the migrator and API only. Targeted results: unit 239/239, contract 39/39, projector scope tests 3/3, change-impact integration set 126/126, default-stack correction flow 11/11, six projector mutants killed. The full integration suite, real-stack check and browser run were not repeated on this source. At this intermediate source-review checkpoint, G10-S25 and S01 were not yet executed. Both were subsequently reconciled and G10 accepted on 2026-11-07; the G10 gate closure is authoritative.

## Reviewed G10 technical evidence

The [independent G10 review](M10_G10_INDEPENDENT_EVIDENCE_REVIEW.md) validates the 500-entry consolidated package manifest and reconciles original failed attempts with same-runtime-SHA supplements A1-A7. Reported passing results include 239 unit, 39 contract, 18 web, 302 integration tests twice, 198 three-engine browser executions and 28/28 killed mutation variants. A6 and A7 added targeted coverage for the five previously disputed scenarios without rerunning successful stages.

Future changes require focused, source-locked verification of affected contracts and independent reconciliation. The approved L07/L14 and conditional L06 implementation passed S01/S25 review; [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md) records this decision and limitations. Full historical campaigns should not be rerun without a demonstrated impact reason.

## M7 source-locked verification boundary

The accepted M7 implementation contains an E:-backed technical runner (`scripts/verify-m7-local.sh`) and source, HTTP, database and browser regression tests. The runner uses a disposable SDK/database pair and independently configured G7 Compose project, plus controlled source outage, five disposable security mutations and image scans. The optional protected operator CLI lifecycle must run without redirected output. D-002 was accepted on 2026-11-03; the pinned reference snapshots are checked by `scripts/verify-dcsa-reference.mjs` and the M7 contract tests; a runner file is not passing evidence. See [M7 remediation](M7_CORRECTNESS_REMEDIATION.md) and [G7 plan](M7_VERIFICATION_PLAN.md).

## Sixth-pass M11 rule-calibration checks, unexecuted

Add source-locked checks for recorded arrival within grace (Inactive, Info), not-yet-overdue arrival schedule (Inactive, Info), inactive departure-slippage risk (Info) and active late/overdue warnings. A clean disposable database fixture must corrupt a synthetic severity under migration-role access, then confirm the signed evaluator repairs it by appending exactly one Reevaluated event despite unchanged fingerprint, followed by a true no-op. Verify null and populated UUID keyset bounds for the due scanner. Static assertions are not runtime evidence.

## Fifth-pass M11 recurrence regression, not executed

Source-locked integration verification must exercise direct Acknowledged/Inactive-to-Active recurrence with old AutoResolved history and a fresh Open occurrence; repeated reevaluation must not duplicate either. Test the manager UI hides Resolve on Open/Inactive. Confirm expected replay-primary-key conflicts map to HTTP 409 while unrelated SQL 23505 failures surface as actual errors. These new focused checks do not replace G11-S01 policy decisions or S02-S25 execution.

## Fourth-pass M11 focused verification, unexecuted

Source changes require testing a database-clock first-page cutoff and new exception creation during list pagination, then mutable lifecycle status changes between stateless pages. For immutable history, generate more than 500 synthetic entries in a disposable database and verify the first preview, subsequent `(occurred_at_utc,id)` pages, protected token expiry/tampering, tenant and exception swapping, per-page Analyst redaction, unmodified retained rows, and Chromium/Firefox/WebKit loading and cancellation behavior. This extends the existing G11-S11/S12/S17/S20-S24 matrix. The [fourth audit](M11_FOURTH_CORRECTNESS_AUDIT_20261108.md) is source-level only.

## M11 candidate verification, not executed

[Source and limitations](M11_IMPLEMENTATION_STATUS.md) and the [25-scenario G11 matrix](M11_VERIFICATION_PLAN.md) define the required rule-boundary, M10 correction, tenant/session/SQL-role, atomic M16 audit, populated upgrade, failure recovery, browser and mutation checks. The E:-backed [focused runner](../scripts/verify-g11-local.sh) can gather development evidence but is not a completed G11 campaign. No G11 scenarios are claimed passing; policy decisions and acceptance pending.

## Philosophy

Test domain invariants, integration boundaries, security boundaries, and operational recovery before investing in visual polish. All results must be reproducible and linked to an exact commit, environment, and dataset seed.

## M0 observed baseline

The historical implementation-branch run at `4fb34eb` was recorded as verified, and subsequent fresh-database testing reopened G0 temporarily. The corrected code passed complete GitHub Actions verification in [run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) at `fec7429415fede121db8a4a34f0df72cd4fac609`; G0 is verified and accepted. The original evidence register reports 1 unit, 10 API contract, 5 real PostgreSQL integration and 6 frontend tests passing; locked frontend build, clean-volume Compose, browser path, health checks, controlled database outage and recovery executed. Browser coverage used one Chrome 152 engine; the historical local run did not cover CI or alternative browsers. The corrected run #2 verified 12/12 functional tests in Chromium, Firefox and WebKit; accessibility remains deferred to M24-04. G0 was accepted on 2026-10-31. Refer to [M0 Verification](M0_VERIFICATION.md) for exact commands and residual issues.

All test workspace, cache, browser evidence and container/database data must follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md). C: is not permitted for new persistent project-specific test data.

## Accessibility verification timing

**Accessibility tests run only in M24-04, after feature delivery and M23 interface polish.** Do not include dedicated accessibility assertions, accessibility scans, or WCAG checks in G0-G23 gates. M0 browser tests may prove ordinary API-to-browser functionality and error recovery without checking accessibility. The final M24-04 suite covers color contrast, keyboard/focus, screen-reader operation, button/form labels, error announcements, browser zoom, responsive usability, and complete workflows. Accessibility defects identified there require corrections and re-testing before G24 acceptance.

## Existing-volume compatibility cadence

M0 verifies clean PostgreSQL initialization, schema migration, seed idempotency and application recovery against disposable databases. It does **not** require access to or compatibility tests against the existing local Docker volume.

Lightweight historical snapshot compatibility is checked only at roadmap quarter-mile milestones **M6, M12, M18 and M24-03**, on a disposable clone when representative historical data exists. Without a historical snapshot, record NOT APPLICABLE and continue. M19 performs the complete backup/restore and migration rehearsal. Breaking schema, PostgreSQL-major or volume-layout upgrades affecting existing retained data require a one-off safe-copy upgrade test before deployment, irrespective of the quarterly cadence.

## M1 verification matrix (executed and accepted)

The [M1 Verification Plan](M1_VERIFICATION_PLAN.md) extends the accepted M0 GitHub Actions sequence. The [M1 verification register](M1_IMPLEMENTATION_VERIFICATION.md) records the passing execution at `fdefe052e4335ca8d1d287160a9859828d05f9d8`, with G1 explicitly accepted on 2026-10-31 and PR #4 merged into `main`. G1 covers controlled account provisioning, Argon2id verify and incorrect credentials; session issue, concurrent devices, idle and absolute expiry under an injected clock, logout and logout-all; disablement/password-reset security-version revocation; cookie replay denial; CSRF invalid/foreign origin and login throttling; protected JSON 401 without redirects; sanitized 503 on database outage; Data Protection persistence across API restart; and functional React authentication through Chromium/Firefox/WebKit.

Run an **additive M0-seeded-to-M1** migration in a disposable database and a fresh-schema M1 migration. Test that `bootstrap_records`, its synthetic baseline contract and M0 outage/recovery remain unchanged. Scan the actual running Docker images, protect secret values in logs and CI artifacts, and record exact SHA plus per-check PASS/FAIL/BLOCKED/NOT RUN status. M1 source, regression suites and G1 evidence are recorded in [M1 verification](M1_IMPLEMENTATION_VERIFICATION.md). M2 tenant authorization was subsequently implemented and accepted; its separate local evidence is in [M2 executed verification](M2_EXECUTED_VERIFICATION.md).

This does not add accessibility checks to G1. All formal accessibility testing remains at M24-04. Historical local-volume compatibility remains at M6/M12/M18/M24-03; M19 retains full backup/restore.

## M2 verification matrix (G2 verified locally and accepted on 2026-10-31)

The [G2 verification plan](M2_VERIFICATION_PLAN.md) specifies two active organizations, a multi-organization user with different roles, Administrator/Analyst/Manager/Support/Auditor grants, suspended and removed memberships, selected-session context and real PostgreSQL authorization. Mandatory cases include foreign organization/member IDs, wrong-role read/write denial, active membership changes, last-Administrator concurrency, migration preservation, CSRF/origin rejection, browser role-switch and M0/M1 regressions.

M2 source, now integrated into `main` through PR #5, follows the [architecture](M2_ARCHITECTURE_DESIGN.md) and [implementation plan](M2_IMPLEMENTATION_PLAN.md). G2 verification ran locally with `scripts/verify-m2-local.sh`, a .NET 10 SDK container, disposable PostgreSQL 16.15, isolated `ho-verify` Compose, three Playwright browser engines, real PostgreSQL outage/recovery, and Trivy, with controlled project paths on E:. On `fbddc1361d79b027275bb53b48133ba0dc7d5cb1`, the run passed 22 unit, 12 contract, 32 fresh and 32 repeated PostgreSQL integration, 6 frontend and 24 browser tests plus operational/security checks. The [executed record](M2_EXECUTED_VERIFICATION.md) accounts for G2-S01 through G2-S25. G2 was accepted on 2026-10-31. No new GitHub Actions runs are authorized.

## Parallel milestone and integration regression strategy

Milestone gates may execute out of numeric order when their explicitly named prerequisites are accepted. Each independent branch retains a fixed source SHA and complete feature-specific positive/negative evidence. When separate streams converge, run a fresh integrated regression of changed API contracts, EF Core migration ordering, authorization, tenant isolation, session security, browser workflows and relevant earlier accepted modules. Do not treat parallel branch passes as a pass for the combined branch.

Early M19 recovery and M20 security assurance cannot preapprove features added later; rerun affected checks at integration and again before G24. G24 requires all earlier gates accepted and complete release verification. Formal accessibility tests remain only M24-04, and historical-volume checkpoints remain M6/M12/M18/M24-03. See [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md) and [Execution Graph](EXECUTION_GRAPH.md).

## M4 local verification (G4 accepted on 2026-11-01)

The [G4 verification plan](M4_VERIFICATION_PLAN.md) contains 25 mandatory cases: D-003 and checksum policy, time/route invariants, M3 composite foreign keys and direct SQL, five-role HTTP matrix, real foreign UUIDs, concurrent parent archival, identifier reservation, scoped pagination, Chromium/Firefox/WebKit, real dependency outage, and accepted M0-M3 regressions. M4 unit, HTTP/integration, migration/SQL, concurrency and three-browser source tests, fixtures and the guarded E:-only runner are committed, with expanded role, session, scope, paging, security and recovery coverage. The initial E:-backed G4 run at `7d0dd3e60049` passed four decision-unit checks, preflight and PostgreSQL image build; the .NET build stopped on two xUnit analyzer errors (exit 11). Both source errors were fixed. The next run at `fa46c86fd6e` built cleanly and passed 82 unit and 12 contract tests, but 86/87 fresh and 86/87 repeated integration cases failed on the same EF pending-model warning. The missing three voyage indexes were aligned across code, snapshot and migration, with a new `schema-index` precheck; see [third correctness audit](M4_THIRD_CORRECTNESS_AUDIT.md). No complete G4 run has passed at the corrected SHA. Do not mark any G4 scenario PASS from the aborted run. The [first correctness audit](M4_CORRECTNESS_AUDIT.md) maps every G4 scenario to authored evidence; the [second correctness audit](M4_SECOND_CORRECTNESS_AUDIT.md) records new failure modes and subsequent source corrections. Full runtime coverage remains unexecuted. New G4 evidence must be SHA-tied, E:-backed and inspectable. M6/M12/M18/M24-03 historical compatibility and M24-04 dedicated accessibility remain in their established gates.

The fifth E:-backed G4 execution at `f1854c3fb684d576d6b16d94d5cb44113bd25f64` passed the clean .NET Release build, 82/82 unit, 12/12 contract, 92/92 fresh and 92/92 repeated integration tests, and 31/31 browser tests in each of Chromium, Firefox and WebKit, followed by M3 and M4 outage and recovery and clean image scans. Test data rules confirmed by this run: a test that must survive repetition against the same disposable database generates its own identifiers, never counts rows across organizations it did not create, and finds records by search rather than by list position, because lists are ordered by record id. Browser specs that share one disposable database must not reuse fixed unique identifiers across engines. Concurrency tests that claim to cover a revocation must land it while the request is demonstrably queued, include a no-revocation control, and are checked by mutation (see [fifth correctness audit](M4_FIFTH_CORRECTNESS_AUDIT.md)). D-003 Option B and G4 were accepted on 2026-11-01.

The final E:-backed G4 execution on `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6` passed the same 82 unit, 12 contract, 92 fresh and 92 repeat integration checks, 31 browser tests per engine, M3/M4 outage and recovery, and zero HIGH/CRITICAL image findings. All 149 listed raw evidence file hashes were independently verified. The evidence stage alone exited 1 because D-003 was still pending; Option B and G4 were explicitly accepted following independent review on 2026-11-01. Preserve the exact [executed verification](M4_EXECUTED_VERIFICATION.md) and historical audits. No runtime verification is claimed for later documentation commits.

## M3 reference-data verification (G3 verified and accepted)

The [G3 plan](M3_VERIFICATION_PLAN.md) specified 25 mandatory scenarios spanning port/vessel validation, five-role permissions, real foreign UUID substitution, direct PostgreSQL constraints, M2-to-M3 upgrade, optimistic concurrency, archive/restore, real tenant/session races and three-browser flows. The full local G3 matrix passed at `b72025d35d62d0a8a45b466986a0b3bd89eebadb`: 50 unit, 12 contract, 56 fresh and 56 repeated integration, 6 frontend and 19 browser tests per engine, with zero HIGH/CRITICAL findings in the three running image scans. Evidence and failure history are retained in [G3 executed verification](M3_EXECUTED_VERIFICATION.md). Independent raw-evidence review preceded explicit G3 acceptance on 2026-10-31. Preserve these suites as M4 regressions.

## M5/G5 correctness remediation (executed)

The [M5/G5 remediation ledger](M5_G5_REMEDIATION_STATUS.md) tracks the eleven source findings and their corrections. The dedicated G5 runner now uses `ho-verify-g5`, requires exact per-phase TRX and all declared browser cases, checks teardown before evaluating evidence and packages raw E:-backed SHA-256 artifacts. Expanded direct SQL, role/session, historical migration and queued race tests have been authored. They were executed at `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91` (see [M5 executed verification](M5_EXECUTED_VERIFICATION.md)); G5-S01 was closed by explicit contract approval on 2026-11-02; full G5 accepted after independent evidence review.

## M5 verification contract (executed and accepted, G5 closed)

The [25-case G5 matrix](M5_VERIFICATION_PLAN.md) covers domain transitions, tenant SQL FKs and status history, live role/session races, version conflicts, two-tenant five-role HTTP, Chromium/Firefox/WebKit, M0-M4 regressions, recovery, scans and E:-backed raw evidence. Executed locally at `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91`: 138 unit, 12 contract, 146 fresh and 146 repeated PostgreSQL integration tests, 18 web unit tests and 141 browser runs (47 each in Chromium, Firefox and WebKit) all passed with none skipped; Trivy HIGH/CRITICAL 0; teardown left no resources. G5-S02 to G5-S25 have evidence present for review of this revision (the earlier revision `c143e67906d3a9e0c1ab5f4b259ad1e5befd2a67` with 139 integration tests and 117 browser runs was independently reviewed). G5-S01 and full G5 were accepted on 2026-11-02. Method notes: the final runner executes the browser, outage and scan stages before the long .NET stage so unstable stages fail fast; failed runs are archived, not overwritten; a security mutation proof (tenant predicate and post-lock revalidation removed in an isolated worktree) is part of the evidence. See [M5 executed verification](M5_EXECUTED_VERIFICATION.md). Method notes for the second round: interrupted-transaction tests pin the API backend with an advisory-lock barrier and prove the in-flight state from `pg_locks` and `pg_stat_activity` instead of sleeping; one scenario kills a real API child process. Draft-preservation browser tests use a genuine second page and real concurrent API changes; headless engines cannot produce real tab visibility transitions, so visibility is driven through the property and event the application reads, and the probe evidence is kept. A security mutation proof (tenant predicate, post-lock revalidation, response caching header) runs in an isolated worktree. Formal accessibility testing remains M24-04 and historical-volume checks remain M6/M12/M18/M24-03.

## M6/G6 verification contract (executed and accepted)

The accepted [M6 local assignment contract](M6_LOCAL_CONTRACT_DECISION.md), [architecture](M6_ARCHITECTURE_DESIGN.md) and [implementation plan](M6_IMPLEMENTATION_PLAN.md) define two tenant-owned UTC half-open allocation histories. The G6 technical verification at the pinned source passed 149 unit, 12 contract, 187 fresh and 187 repeat integration tests, and 57 browser tests in each engine, alongside four effective security mutations, historical migration, outage/recovery and clean scans. The original evidence assertion exited 1 solely for pending G6-S01 contract acceptance. The twelve-rule contract and full G6 were explicitly accepted on 2026-11-02. See [gate closure](M6_G6_GATE_CLOSURE.md).

## M7/G7 verification contract (accepted 2026-11-03)

The [G7-S01 through G7-S25 plan](M7_VERIFICATION_PLAN.md) requires pinned D-002 source artifacts, direct two-tenant bearer-authentication checks, credential lifecycle/revocation races, exact JSON limits, HTTPS and trusted-proxy denial, a `503 intake_unavailable` no-ack proof, M6-to-M7 migration preservation, affected accepted M0-M6 regressions and source-locked E:-backed evidence. The suites were executed locally on one revision. The original run passed 21 of 25 scenarios; after the G7-S06 operator supplement, the G7-S25 evidence reconciliation and the D-002 reference verification, all 25 pass. See the [executed verification record](M7_EXECUTED_VERIFICATION.md). G7 was explicitly accepted 2026-11-03 after supplemental review; see [G7 closure](M7_G7_GATE_CLOSURE.md). Original runner outcomes remain unchanged. GitHub Actions stays disabled.

## M8/G8 verification (accepted after independent review)

The [G8 plan](M8_VERIFICATION_PLAN.md) defines 25 mandatory real-execution scenarios, requiring explicit D-006 acceptance, atomic receipt/event/Pending inserts, source-scoped uniqueness, canonical replay, conflicting payloads, two-tenant isolation, revoked credential transaction races, direct SQL immutability, capacity backpressure, rollback/commit uncertainty, restart durability and affected G0-G7 regressions. Controlled mutants must detect missing post-lock authorization, premature 202, bypassed uniqueness, absent immutable guards, missing event/work rows and quota bypass. Run locally with E:-backed disposable PostgreSQL/Compose and source-locked evidence; independent review and separate G8 acceptance are mandatory. **M8 test suites, isolated G8 Compose stack, eleven security mutation variants and evidence summarizer are committed. A second audit added a real PostgreSQL COMMIT wire-cut test, full restart byte/state comparison, immutable per-run archive checks and separate technical/governance disposition, described in [the second remediation register](M8_SECOND_REMEDIATION_STATUS.md). All were executed in one complete source-locked local run (see the [executed verification record](M8_EXECUTED_VERIFICATION.md)); D-006 Option A and G8 accepted 2026-11-04; see [gate closure](M8_G8_GATE_CLOSURE.md).** See [M8 implementation status](M8_IMPLEMENTATION_STATUS.md), [G8 verification runbook](M8_G8_VERIFICATION_RUNBOOK.md) and [D-006 proposal](M8_D006_DURABILITY_PROPOSAL.md).

## M9/G9 worker and reconciliation verification (accepted)

The [G9 25-case plan](M9_VERIFICATION_PLAN.md) was executed against source `0abeeb0` using real PostgreSQL, lease-fencing and two-process races, COMMIT ambiguity, scoped redrive, full unit/contract/integration regressions, 14 killed mutation families and exact-image scans. [Attempt 08](M9_EXECUTED_VERIFICATION.md) reports 809/809 tests passing, while the [browser supplement](M9_BROWSER_SUPPLEMENT.md) reports 174/174 browser executions across three engines. All six [M9-01 contract decision](M9_LOCAL_CONTRACT_DECISION.md) choices were approved on 2026-11-05 with a mandatory transport-backlog condition. The raw attempt 08 G9-S01 status remains historically BLOCKED because approval followed execution; subsequent independent G9-S25 evidence review and G9 acceptance were explicitly recorded in [G9 closure](M9_G9_GATE_CLOSURE.md). No GitHub Actions may be run; M24-04 remains the dedicated accessibility gate.

M9 results: the source-locked technical campaign is in the [executed verification record](M9_EXECUTED_VERIFICATION.md). Because G9-S22 requires the existing browser journeys, the 16 Playwright specs ran in Chromium, Firefox and WebKit on the same revision in a separate run, recorded in the [browser supplement](M9_BROWSER_SUPPLEMENT.md); browser executions are reported separately from the 809 .NET tests. The supplement is a source-locked, E:-backed, one-worker run on the isolated G9 stack with credential sanitization of failure artifacts.

## Accepted M16/G16 verification and F001 extension

The independent G16 main-evidence review checked 162/162 standalone files; the S07/S08/S28 supplement was later reviewed as 6/6 consistent while retaining source-SHA attestation limitations. The final combined `79318c6` campaign had 228/228 unit, 39/39 contract, 286/286 integration twice, 61/61 browser tests per engine after a same-source supplement, 74/74 audit HTTP, 14/14 database outage and 20/20 combined stack checks, 18/18 targeted mutants and a 12/12 accepted-M9 populated upgrade. All 544 combined archive manifest entries were independently checked. The earlier Firefox failure remains disclosed. [G16 closure](M16_G16_GATE_CLOSURE.md) preserves these sources and the accepted limitations.

After G16 acceptance, a final source review found a possible generation-zero `Redriven` forgery. Correction source `8ba7727` passed 17/17 focused restricted-role tests and 286/286 full PostgreSQL integration tests; reverting the old guard caused the new regression to fail. The extension archive matched 47/47 file hashes. [PR #25](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/25) merged only after that gate extension. No extra browsers, outages or image scans were claimed on the corrected SHA because those paths were unchanged. G10 has independently reviewed technical evidence on its original frozen source but no gate acceptance; its later correction-security remediation has no executed runtime evidence. No GitHub Actions are authorized.

## Historical M10/G16 combined technical verification (subsequently accepted)

The [M10/G16 source reconciliation](M10_M16_SOURCE_RECONCILIATION.md) extends the accepted eleven-migration G16/M9 baseline with M10's SQL-only twelfth migration and its immutable effect-derived timeline index. The M16 Auditor API, audit insertion triggers and accepted M9 restricted worker behavior must remain unchanged. New focused tests extend the exact restricted API/worker grant matrix, correction-head security-definer trigger, twelve-migration inventory and downgrade counts. Full `G10-S01` through `G10-S25` source-locked execution must include the combined suites, real-role SQL, concurrency and outage/restart scenarios, three browser engines, vulnerability and mutation checks, preserved evidence and independent review. These checks were executed on frozen source `3e81b88` with the real `harborops_api`, `harborops_worker` and `harborops_projector` logins, real overlapping PostgreSQL sessions proved by lock waits, a populated-history upgrade, a 12,001-event history, a projector killed before COMMIT, three browser engines and 29 mutation variants; see [M10 executed verification](M10_EXECUTED_VERIFICATION.md) for counts, attempts, defects and the scenario dispositions. Historical G16 results remain accepted and were not rerun; only the M10 interaction surfaces were.

## Publication security verification (executed locally)

The repository became publicly viewable on 2026-11-03 after branch protection and historical artifact cleanup. Source visibility does not authorize public application hosting. GitHub Actions remains disabled; new verification uses approved, isolated E:-backed local resources. See [Publication Safety Status](PUBLICATION_SAFETY_STATUS.md) for the final disclosure and governance disposition.

`scripts/verify-publication-local.sh` runs the regression suites that the publication security changes can affect on disposable E:-backed resources: build, unit, contract (including `TransportHardeningHttpTests`), PostgreSQL integration on a fresh and a repeat database, frontend, dependency audits, Compose, transport probes through and around the Nginx proxy, three-engine browser specs, outage and recovery checks, image scans and the source-locked supply-chain audit. It never dispatches GitHub Actions. Accepted G6 mutation and historical-snapshot evidence is referenced, not repeated, because this work changes no migration or schema. Hosted HTTPS checks need a real approved domain and are recorded as not executed until one exists. The manual-only `verify-publication-security.yml` workflow checks out an exact commit, runs independent jobs and uploads no artifacts, because browser traces can contain session cookies.

## Test layers

| Layer | Tools | Representative assertions |
| --- | --- | --- |
| Unit | xUnit | Domain lifecycle, ETA rule, permissions evaluator, event ordering |
| Database integration | xUnit + PostgreSQL test database | Transactions, constraints, migration paths, concurrency |
| API integration | ASP.NET Core integration test host | Status codes, validation, auth, organization isolation |
| Contract | OpenAPI validation + fixture tests | Schema versions, errors, event adapter mapping |
| Browser E2E | Playwright | Functional shipment workflows and browser recovery through M23; accessibility-critical flows tested only in M24-04 |
| Security | Targeted adversarial tests + OWASP ZAP in test environment | BOLA/BFLA, injections, leaks, session boundaries |
| Load | k6 | Documented scenario throughput and p95 latency |
| Recovery | Controlled fault injection | Worker restart, duplicate protection, backup restore |
| Documentation | Link/schema/state audit | No broken references or unsupported implementation claims |

## Test data

- Deterministic seed with at least two independent organizations and distinct role accounts.
- Valid on-time, delayed, at-risk, completed, cancelled, and unknown shipments.
- Multiple events sharing source IDs across unrelated sources for collision behavior.
- Duplicates, corrections, late events, missing containers, and malicious payload examples.
- Synthetic incidents and monitoring signals.
- Clear data version and seed hash in test output.

## Required security matrix

Test each protected operation as:
1. Unauthenticated caller.
2. Correct role in matching organization.
3. Wrong role in matching organization.
4. Correct role in foreign organization.
5. Disabled membership.
6. Unauthorized export and search filter.
7. Forged source integration credential.

Confirm response status, absence of secret data, no unauthorized mutation, and audit entry where required.

## Deterministic event suite

- Duplicate delivery is effect-once.
- Worker restart between inbox write and projection update causes no lost or duplicate business effect.
- Out-of-order event arrival does not silently reverse a valid state.
- Correction records are visible and result in reproducible recomputation.
- Retry budget, dead-letter routing, and later replay behave as specified.
- Alerts are correctly scoped and deduplicated.

## Performance benchmark protocol

Baseline dataset: synthetic and versioned; define shipment count, event count, account count, environment resources, and indexing state. Record warm-up procedure, request concurrency, repetition count, and reported p50/p95/p99. The initial objective for common filtered shipment reads is p95 below 500 ms in the declared local reference setup, subject to measurement and review.

Never state an achieved latency number until a runnable benchmark report supports it.

## Release gates

- No failing required unit, integration, contract, or browser tests.
- Critical and high security issues are resolved or explicitly block release.
- Migrations run on fresh and historical sample databases.
- Cross-organization test matrix passes.
- Backup and restore are verified for hosted readiness.
- Application logs and metrics are available during a forced fault.
- Demo is repeatable from a clean environment.
- Documentation accurately distinguishes implemented and pending behavior.

## Evidence register

For every milestone: commit SHA, commands executed, test summary, pass/fail counts, relevant artifact paths, runtime environment, failed/waived checks, and acceptance status. A checklist without test artifacts is not verification.

## G10 correction authority targeted supplemental verification, 2026-11-07

The approved L07/L14 source modifications require a new source-locked targeted campaign, not an automatic rerun of all historical G10 stages. Verify the thirteenth migration on fresh and populated twelve-migration volumes, restricted API SQL denial, signed positive corrections, forged user/session and signature probes, altered digest/idempotency proofs, nonce reuse and forged replay rejection, live Manager revocation and downgrade, atomic M16 audit failure rollback, immutable correction history, retained M9 worker effects and projector operation. Exercise focused effective mutations and relevant M16 audit/role plus M9 worker regressions.

New-source execution on `a26d19acd57308033bdf6fe7e25c61287cd17b0b` is recorded in the [verification record](M10_G10_REMEDIATION_VERIFICATION_20261107.md). Failed and interrupted attempts, source SHAs and hash manifests are preserved; the supplement still needs independent review (NOT EXECUTED). See [targeted implementation plan](M10_G10_L07_L14_REMEDIATION_PLAN.md) and [implementation record](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md). No GitHub Actions, merge, release or deployment is authorized.

## M11/M12 local closure addendum (2026-11-08)

Residual scenarios are verified with focused tests on disposable PostgreSQL servers rather than another full-suite repeat: revocation and atomicity are interleaved with a held organization or shipment lock, recovery uses backend termination and runtime-login disabling, and query plans replay the SQL captured from the real API under the restricted role. Role-changing cases require an exclusive server and `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1`. Simulated clock advance and outage are labelled as simulations in the evidence. See [local closure results](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md).
