# Test and Verification Strategy

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

## Proposed M7/G7 verification contract (not executed)

The proposed [G7-S01 through G7-S25 plan](M7_VERIFICATION_PLAN.md) requires pinned D-002 source artifacts, direct two-tenant bearer-authentication checks, credential lifecycle/revocation races, exact JSON limits, HTTPS and trusted-proxy denial, a `503 intake_unavailable` no-ack proof, M6-to-M7 migration preservation, affected accepted M0-M6 regressions and source-locked E:-backed evidence. No M7 suite has been executed or accepted. GitHub Actions stays disabled.

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
