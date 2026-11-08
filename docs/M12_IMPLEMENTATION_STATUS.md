# M12 Implementation Status

**Hosted verification checkpoint (2026-11-08):** [Hosted run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records successful corrected-source checks, G11 17/17 and G12 2/2 targeted PostgreSQL tests, dependency and runtime-image scans, and a confirmed **G11-S12 SQL read-isolation FAIL**. The current HTTP database login can read M11 exception rows without scoped-session authority. The browser and full integration campaigns remain in progress at the recorded checkpoint. Independent G11/G12 gate reviews and acceptance pending.

**Combined-source checkpoint (2026-11-08):** M11 and M12 are assembled on `feat/m11-m12-combined-integration`; shared API, role provisioning, React and migration-count surfaces have been reconciled, and M11 lifecycle code and tests updated under the [recorded M11 policies](M11_LOCAL_CONTRACT_DECISION_20261108.md). Earlier descriptions of missing combined integration or pending M11 policy choices are superseded. G11/G12 executable security and browser evidence does not yet exist, M12 local policies are still pending, and no gate acceptance, protected-main merge, deployment or M9 hold release is authorized.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). M12 remains an independent source candidate in draft PR #32, with no executed G12 technical gate. Shared verification strategy approval does not establish a combined source SHA, executable correctness or M12 acceptance.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records M11 draft PR #31 and M12 draft PR #32 with different additive migration IDs, shared API/UI/role integration surfaces and no combined execution. G11-S01 is BLOCKED, G11-S02..S25 NOT EXECUTED and G12 verification pending. Accepted `main` and the M9 transport hold remain unchanged.

**Last source audit:** 2026-11-08. **Branch:** `feat/m12-shipment-search-export`.
**Accepted baseline:** `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`.
**Disposition:** Code and test candidate committed; no G12 technical campaign executed, no independent G12 review and no gate acceptance. Nothing merged or deployed.

| Package | Candidate implementation | Gate |
| --- | --- | --- |
| M12-01 | Shared typed filter and CSV policies, protected cursor and role contract | Policy decisions pending |
| M12-02 | New tenant-scoped search route, M6 voyage and M5 port/status/date predicates | Authored, runtime unverified |
| M12-03 | Restricted Analyst/Manager CSV export, size limits, role/audit migration | Authored, runtime unverified |
| M12-04 | React UI plus new unit, contract, real-PostgreSQL HTTP and three-engine browser test sources | Authored, NOT EXECUTED |
| G12 | Source-locked campaign, Q2 snapshot, raw evidence, independent S25 and decision | NOT EXECUTED |

## Changed source and tests

Domain: `ShipmentSearchPolicy.cs`, `OrganizationPolicy.cs`.
Infrastructure: `ShipmentSearchStore.cs`, `M12ExportProofSigner.cs`, `DependencyInjection.cs`, `20261107230000_M12ShipmentExportAudit.cs`.
API: `ShipmentSearchEndpoints.cs`, `Program.cs`, `AuthenticationSetup.cs`, `AuditRoleProvisioner.cs`.
Web: `shipmentSearchClient.ts`, `ShipmentWorkspace.tsx`.
Tests: `ShipmentSearchPolicyTests.cs`, `M12ExportProofSignerTests.cs`, `M12MigrationSafetyTests.cs`, `ShipmentSearchContractTests.cs`, `ShipmentSearchHttpTests.cs`, `shipment-search.spec.mjs`, the corrected M5 `shipment.spec.mjs` and `shipment-draft.spec.mjs` interceptors, updated `MigrationSnapshotTests.cs` and event-specific M16 `AuditHardeningTests.cs`. Configuration: `compose.yaml` and `.env.example` use the separate untracked M12 signing key only in migrator/API.

[M12 source correctness audit](M12_CORRECTNESS_AUDIT_20261107.md) records ten findings, committed source fixes, and the unexecuted checks. The signed proof and optional key configuration must be verified before acceptance.

No .NET SDK, Docker, PostgreSQL or browser runtime is available in the connected source-editing environment. **No build, runtime test, migration, real-stack check, scan, mutation, or G12-S25 review passed or failed here because none was run.** All G12 scenarios are NOT EXECUTED. Reference-dataset p95 measurements belong to M21-02.

## Second-pass remediation checkpoint

[Second-pass correctness audit](M12_CORRECTNESS_AUDIT_20261108.md), 2026-11-08: eight further source-level findings were corrected or covered by new regression source. This includes the missing hosting namespace in SQL-role tests, the M12 audit-history downgrade guard, 4,096-character URL bound, 15-second PostgreSQL statement timeouts, the then-proposed 30-second CSV preparation deadline (superseded by 25 seconds in the fourth pass), the full five-role HTTP matrix, and field-by-field HMAC substitution tests. **None of these tests were executed** in this environment. The G12 gate remains pending.

## Third-pass M5 browser integration review

The [2026-11-08 audit](M12_CORRECTNESS_AUDIT_20261108.md#third-pass-m5-browser-regression-findings-2026-11-08) records two further source-level regressions found by comparing the accepted M5 browser suite with the M12 workspace's new search URI. The deferred shipment response test and in-flight background-refresh/draft test now intercept `/shipments/search` and retain their original behavioral assertions. They have **not** been executed in any browser. This is not accepted G12 evidence.

## Fourth-pass search and export timeout reconciliation

The [fourth-pass source audit](M12_CORRECTNESS_AUDIT_20261108_FOURTH_PASS.md) corrected the M12 SQL audit call to use the EF Core 10 parameterized `ExecuteSqlAsync` API, and introduced **25-second internal budgets for both search and export** with existing 15-second per-statement SQL limits. Both local and hosted Nginx continue to use a 30-second API read timeout. Internal deadline expiry maps to sanitized 503; browser abort remains separate. The budget change is a source-level mitigation and does not prove proxy-timing behavior. **No .NET build, PostgreSQL, browser or proxy test was executed.** G12 acceptance pending.

## Fifth-pass safety remediation

The [2026-11-08 fifth-pass source audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md) records two further corrections: the restricted-role SQL integration test now fails closed into an explicit xUnit **SKIPPED** state unless `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1` declares an exclusive disposable PostgreSQL server, and its collection is nonparallel within the integration assembly. CSV materialization now checks cancellation per row and around UTF-8 conversion, preserving the existing 25-second store deadline. Separate processes and test assemblies still require external serialization. **No database, browser, .NET build, or timeout execution evidence was produced in this audit.** G12 acceptance pending.

## Open conditions

- Explicit policy disposition M12-P01..P16 and review of Auditor export, audit grants and row/byte/rate limits.
- Verify .NET Release compilation, migration downgrade guard, SQL timeouts and total deadline behavior, the expanded five-role and nine-field HMAC test sources; then run local E:-backed disposable DB and browser verification with the source frozen at the final SHA. Preserve every failure, not just green outcomes.
- Coordinate M11 and M12 migration discovery on a combined SHA: their IDs are distinct (`20261107180000` and `20261107230000`), but separate branch migration-count tests and shared integrations must be reconciled.
- Execute the new signed-proof, key rotation/mismatch, same-origin GET, worker denial, forged caller, duplicate-correlation, role change and audit-failure tests against real restricted PostgreSQL credentials. Source hardening is not runtime proof.
- Perform G12 Q2 historical-volume compatibility on a safe copy, or document NOT APPLICABLE with discovery evidence.
- Independent G12-S25 review and explicit G12 acceptance pending.

No GitHub Actions were run. The M9 transport hold, no-deployment status and accepted main remain unchanged.
