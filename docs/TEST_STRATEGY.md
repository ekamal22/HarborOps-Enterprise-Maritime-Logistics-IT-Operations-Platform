# Test and Verification Strategy

## Philosophy

Test domain invariants, integration boundaries, security boundaries, and operational recovery before investing in visual polish. All results must be reproducible and linked to an exact commit, environment, and dataset seed.

## M0 observed baseline

The historical implementation-branch run at `4fb34eb` was recorded as verified, but subsequent fresh-database testing reopened G0. The original evidence register reports 1 unit, 10 API contract, 5 real PostgreSQL integration and 6 frontend tests passing; locked frontend build, clean-volume Compose, browser path, health checks, controlled database outage and recovery executed. Browser coverage used one Chrome 152 engine; manual CI, screen readers and alternative browsers were not tested. Refer to [M0 Verification](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m0-engineering-baseline/docs/M0_VERIFICATION.md) for exact commands and residual issues.

All test workspace, cache, browser evidence and container/database data must follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md). C: is not permitted for new persistent project-specific test data.

## Accessibility verification timing

**Accessibility tests run only in M24-04, after feature delivery and M23 interface polish.** Do not include dedicated accessibility assertions, accessibility scans, or WCAG checks in G0-G23 gates. M0 browser tests may prove ordinary API-to-browser functionality and error recovery without checking accessibility. The final M24-04 suite covers color contrast, keyboard/focus, screen-reader operation, button/form labels, error announcements, browser zoom, responsive usability, and complete workflows. Accessibility defects identified there require corrections and re-testing before G24 acceptance.

## Existing-volume compatibility cadence

M0 verifies clean PostgreSQL initialization, schema migration, seed idempotency and application recovery against disposable databases. It does **not** require access to or compatibility tests against the existing local Docker volume.

Lightweight historical snapshot compatibility is checked only at roadmap quarter-mile milestones **M6, M12, M18 and M24-03**, on a disposable clone when representative historical data exists. Without a historical snapshot, record NOT APPLICABLE and continue. M19 performs the complete backup/restore and migration rehearsal. Breaking schema, PostgreSQL-major or volume-layout upgrades affecting existing retained data require a one-off safe-copy upgrade test before deployment, irrespective of the quarterly cadence.

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
