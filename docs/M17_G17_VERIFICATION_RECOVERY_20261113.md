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
| [#43445954783](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43445954783) | `3500bc33997b0ad4fa803e5983c5444d7d1d598a` | Relaunched after correcting the recovery fixture; in progress when this record was written; inspect the linked run for its final outcome |

Corrections were committed as `8fadeae` (C# test compilation and xUnit assertions), `889f729` (valid isolated verification port, with source regression check), and `3500bc3` (backlog alert should remain active after the first low sample and clear after sufficient sustained recovery samples). These corrections do not imply success in later campaign stages.

## Next verification obligations

- Continue checking the current source-locked run and fix observed new failures against actual raw artifacts. No rerun of an old SHA substitutes for verification of corrected source.
- Examine complete method-level TRX and browser case totals, PostgreSQL and worker behavior, Prometheus rules and live transitions, privacy/export evidence, alert hysteresis, storage and retention, outage and restart behavior, performance, no-skip and integrity controls.
- Reconcile all 72 technical assertions individually and run the three independent G17-S25 reviews. Document exceptions explicitly and record a separate gate decision.
- Preserve all historical failed runs and their evidence; never rewrite a failed artifact as a pass. Do not alter accepted `main`, default Compose, migrations, notification retention, or production configuration.

[Current milestones and gate authority](STATUS.md) | [Authoring and independent-review readiness](M17_G17_S25_TEST_READINESS_20261113.md) | [Verification matrix](M17_VERIFICATION_PLAN.md).
