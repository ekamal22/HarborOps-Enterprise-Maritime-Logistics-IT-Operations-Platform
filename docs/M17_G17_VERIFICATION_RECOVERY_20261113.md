# G17 Source-Locked Verification Recovery

**Recorded:** 2026-11-13, during the source-locked campaign for `feature/m17-observability-20261112`.
**Protected main:** `b490fb8cfc879cdf05cb1f7484b99f6a9b264ed3`, unchanged by this recovery.
**Gate:** G17 NOT ACCEPTED; acceptance pending.
**Source coverage:** 72 technical witnesses authored, three independent G17-S25 reviewer assertions outstanding.

## Failures, corrections and retained evidence

| Run | Exact source SHA | Observed outcome and boundary |
| --- | --- | --- |
| [#43442998935](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43442998935) | `c18d55d9943623e3db89ef3a322a29d275c5609c` | Preflight passed; full campaign failed during .NET test compilation: three missing worker-settings imports and two xUnit analyzer errors |
| [#43444477442](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43444477442) | `8fadeae34e989fbdabc6a4a7069d79fc10dbb74b` | Build passed with zero errors; unit 319/319, contract 60/60 and web 41/41 passed; disposable Compose failed because host port 155432 exceeded the valid TCP range |
| [#43445207627](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43445207627) | `889f729eef0516525f85f05b477fdbbfbfd1b997` | Preflight and early suites passed; Prometheus rule test failed: expected stale backlog at 4m30s despite the fixture having already provided multiple clearing samples |
| [#43445954783](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43445954783) | `3500bc33997b0ad4fa803e5983c5444d7d1d598a` | FAILED integration: 441 passed, 54 failed and 83 skipped. Of the failures, 48 were missing the exclusive G12 disposable PostgreSQL guard; six exposed source, test and accepted-baseline expectation defects. Preflight, build, 319 unit, 60 contract, 41 frontend, Prometheus rules and disposable PostgreSQL startup passed. |
| [#43452458500](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43452458500) | `6675faf93860029db7dcf46893d38c842491af44` | FAILED broad integration: 446 passed, 49 failed, 83 skipped. This was an intermediate source push before the separate G17 integration filter was committed. The exclusive G12 guard was not enabled in this older runner. |
| [#43452683183](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43452683183) | `1c61b655e21602a403871bd733e9b5ce04a52398` | FAILED scoped integration: 50 passed, 3 failed, 0 skipped. The failure witnesses are two HTTP 503 readiness/baseline checks with no seeded bootstrap record and one manual-redrive assertion expecting cumulative attempt 2 rather than attempt 1 in generation 1. |
| [#43462657399](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43462657399) | `fe81a763f5a4e7a8bfc6394aceea34ae1f927e60` | Intermediate source-fix run started on push; still in progress at this checkpoint, but missing a C# namespace import later added. It is not current final verification. |
| [#43462771279](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43462771279) | `b1741377b8a1f9af740a131c231a689ab6a1f6eb` | Latest source with test fixture fixes and required C# import; queued as of this documentation checkpoint. Outcome pending. |

Corrections were committed as `8fadeae` (C# test compilation and xUnit assertions), `889f729` (valid isolated verification port, with source regression check), and `3500bc3` (backlog alert should remain active after the first low sample and clear after sufficient sustained recovery samples). These corrections do not imply success in later campaign stages.

## Latest scoped failure triage, 2026-11-13

The [#43452683183](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43452683183) raw evidence contains `g17-integration.trx` with exactly 53 executed tests, 50 passing and three failing. The two HTTP 503 responses were not collector-related assertions: the underlying real database readiness probe checks that the `bootstrap_records` baseline exists, while those two test fixtures never called `BootstrapSeeder.SeedAsync`. The redrive event endpoint succeeded; the test incorrectly waited for `attempt_count=2`, whereas `TrackingWorkAdminStore.RedriveAsync` deliberately resets `attempt_count=0` and increments `redrive_generation`. The second processed attempt is `generation=1, attempt=1`.

Commit `fe81a763f5a4e7a8bfc6394aceea34ae1f927e60` seeds the actual database baseline in both affected test fixtures and asserts generation-scoped redrive transitions and durable attempt history. Commit `b1741377b8a1f9af740a131c231a689ab6a1f6eb` adds the required dependency-injection import to the exporter-outage test fixture. No production retry semantics were changed. Current verification source is `b1741377b8a1f9af740a131c231a689ab6a1f6eb`, launched in [#43462771279](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43462771279) and not yet proven green.

## Integration triage and corrective source

The failed integration TRX was parsed by case identity: 48 failures were exclusive G12 PostgreSQL precondition exceptions; the remaining six were a M17 worker fixture using an invalid synthetic equipment reference, a worker redrive test missing its settings dependency, a normalized reference-code assertion, an M16 audit allowlist missing the accepted M15 acknowledgment action, an obsolete fixed migration-history list, and a legitimate mismatch between server-issued receipt correlation and audit correlation.

Commit `6675faf` corrects the six findings. It adds transaction-local server-issued audit correlation on accepted receipt persistence, without changing protected migration history, and repairs the relevant test fixtures and strict allowlists. Commit `1c61b65` restricts the G17 integration TRX to authored M17 witnesses and directly impacted audit/upgrade checks while keeping separately required G12 and G13 accepted-milestone regressions behind explicit disposable exclusive-server controls.

The 83 skipped cases in the failed broad run belonged to independently gated G13/G15 crash, scale, exclusive-role and other dedicated campaigns. They were not counted as passed. The revised G17 campaign avoids scheduling unrelated gated suites as implicit G17 evidence; it still requires every mapped G17 witness and every named accepted regression to execute without skip, failure, or missing method. Full accepted-gate replays remain separate obligations if explicitly authorized.

No source correctness, accepted G17 status, or finished hosted verification is inferred from these commits.

## Next verification obligations

- Continue checking the current source-locked run and fix observed new failures against actual raw artifacts. No rerun of an old SHA substitutes for verification of corrected source.
- Examine complete method-level TRX and browser case totals, PostgreSQL and worker behavior, Prometheus rules and live transitions, privacy/export evidence, alert hysteresis, storage and retention, outage and restart behavior, performance, no-skip and integrity controls.
- Reconcile all 72 technical assertions individually and run the three independent G17-S25 reviews. Document exceptions explicitly and record a separate gate decision.
- Preserve all historical failed runs and their evidence; never rewrite a failed artifact as a pass. Do not alter accepted `main`, default Compose, migrations, notification retention, or production configuration.

[Current milestones and gate authority](STATUS.md) | [Authoring and independent-review readiness](M17_G17_S25_TEST_READINESS_20261113.md) | [Verification matrix](M17_VERIFICATION_PLAN.md).
