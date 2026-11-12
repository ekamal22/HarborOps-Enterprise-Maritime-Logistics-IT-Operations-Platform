# M12 Implementation Status

**Post-G11/G12 acceptance checkpoint (2026-11-08):** G0-G12 and G16 are accepted and merged into `main`. G11 and G12 have separately approved S01 policies and S25 evidence reviews in the [formal gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md). Earlier pending, unverified and source-candidate checkpoints below remain historical if explicitly dated. The M9 ARRI/DEPA transport hold and 10,000 unresolved-item cap remain binding. M10 projector defaults off, ETA remains null and M11 evaluator is opt-in. No production deployment or migration is authorized. For the later conditional G13 integration and pending G15 and Turkish localization, see [current Status](STATUS.md).

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
- At this earlier implementation checkpoint, G12-S25 review and explicit acceptance were pending. Both were subsequently completed in the [gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md).

No GitHub Actions were run during this earlier source-only audit. Later targeted hosted verification and acceptance are recorded in the gate closure. The M9 transport hold and no-deployment restriction remain unchanged.
