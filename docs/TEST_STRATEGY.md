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

Run an **additive M0-seeded-to-M1** migration in a disposable database and a fresh-schema M1 migration. Test that `bootstrap_records`, its synthetic baseline contract and M0 outage/recovery remain unchanged. Scan the actual running Docker images, protect secret values in logs and CI artifacts, and record exact SHA plus per-check PASS/FAIL/BLOCKED/NOT RUN status. M1 source, regression suites and G1 execution evidence are recorded in [M1 verification](M1_IMPLEMENTATION_VERIFICATION.md). M2 tenant authorization is next.

This does not add accessibility checks to G1. All formal accessibility testing remains at M24-04. Historical local-volume compatibility remains at M6/M12/M18/M24-03; M19 retains full backup/restore.

## M2 local verification matrix (source tests added, not executed)

The [G2 verification plan](M2_VERIFICATION_PLAN.md) specifies two active organizations, a multi-organization user with different roles, Administrator/Analyst/Manager/Support/Auditor grants, suspended and removed memberships, selected-session context and real PostgreSQL authorization. Mandatory cases include foreign organization/member IDs, wrong-role read/write denial, active membership changes, last-Administrator concurrency, migration preservation, CSRF/origin rejection, browser role-switch and M0/M1 regressions.

M2 implementation must use the [M2 architecture](M2_ARCHITECTURE_DESIGN.md) and [M2 work plan](M2_IMPLEMENTATION_PLAN.md). GitHub Actions use is prohibited until the restriction is explicitly lifted. Do not dispatch, rerun, inspect or modify workflows; verify using explicitly invoked local build, unit, API, disposable PostgreSQL, browser and Docker smoke checks. No G2 commands have run. M2 application and initial test source are committed on a feature branch, but no cases have been verified. [Implementation register](M2_IMPLEMENTATION_STATUS.md). **G2 open.**

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
