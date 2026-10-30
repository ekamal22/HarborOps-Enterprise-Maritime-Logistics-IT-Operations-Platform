# Test and Verification Strategy

## Philosophy

Test domain invariants, integration boundaries, security boundaries, and operational recovery before investing in visual polish. All results must be reproducible and linked to an exact commit, environment, and dataset seed.

## Test layers

| Layer | Tools | Representative assertions |
| --- | --- | --- |
| Unit | xUnit | Domain lifecycle, ETA rule, permissions evaluator, event ordering |
| Database integration | xUnit + PostgreSQL test database | Transactions, constraints, migration paths, concurrency |
| API integration | ASP.NET Core integration test host | Status codes, validation, auth, organization isolation |
| Contract | OpenAPI validation + fixture tests | Schema versions, errors, event adapter mapping |
| Browser E2E | Playwright | Shipment workflow, alerts, filters, accessibility-critical flows |
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
