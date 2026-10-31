# M4 Second Correctness Audit

**Audited branch:** `feat/m4-voyage-container-references`  
**Audited source revision:** `7a28e0eec6f8c2ba3d124ab4c77b69f5af32e4a6`  
**Review type:** Repository source and verification-contract review only. No .NET compilation, PostgreSQL execution, browser run, or GitHub Actions.  
**Outcome:** G4 OPEN. Acceptance pending. Findings below are unremediated at the audited revision.

## Findings by severity

### M4-A2-01: Cross-tenant SQL test has a second, independent reason to fail (High)

`tests/HarborOps.IntegrationTests/MaritimeMigrationTests.cs`, `DirectSqlRejectsCrossTenantVoyagesBadIdentifiersAndMutatedIdentity`, constructs a voyage using a foreign vessel ID **and a completely nonexistent destination port UUID**. The test checks only that a PostgreSQL exception occurs. The nonexistent port can trigger a foreign-key failure even if the composite tenant restriction on the vessel reference were absent. This test cannot prove the advertised cross-tenant invariant independently.

**Required correction:** Seed a valid active vessel and two distinct valid ports in the target organization; vary exactly one referenced parent to an existing foreign-organization row in each negative case, separately for vessel, origin, and destination. Assert SQLSTATE 23503, unchanged row counts and no partial writes. Include a same-tenant valid direct insert as a positive control.

### M4-A2-02: D-003 evidence approval matcher can consume unrelated future decisions (High)

`scripts/assert-g4-evidence.mjs` uses the regex `/D-003[\s\S]*?\*\*Status:\*\*\s*Accepted/m` across the entire `docs/DECISIONS.md` file. The expression is not confined to the D-003 decision section. The additional pending phrase check only looks for a particular phrase within 400 characters. Adding an unrelated later decision marked `**Status:** Accepted` causes the checker to report D-003 as approved even while the D-003 table still says pending. This failure mode was reproduced against the actual decision file with a hypothetical later accepted entry.

**Required correction:** Parse a bounded D-003 decision section or an explicit structured decision record, verify its exact status, edition and scope, and separately verify that the open-decisions table no longer lists D-003.

### M4-A2-03: G4-S07 fixtures do not fulfill the declared five-role/two-tenant fixture contract (High)

`scripts/prepare-m4-browser-fixtures.mjs` creates M4 voyages and containers in two organizations but delegates all roles to the M3 fixture script. `scripts/prepare-m3-browser-fixtures.mjs` assigns Manager in both organizations and Analyst in only the North organization, with each organization having its Administrator. It does not provision explicit Support and Auditor memberships for both organizations. `scripts/assert-g4-evidence.mjs` marks S07 evidence present from only successful `fixtures` and `m4-fixtures` stages, without a named assertion covering five roles in each tenant.

**Required correction:** Provision all five roles in both disposable tenants, check exact role memberships and permissions, and verify idempotent re-run yields the same ids, status, codes and versions. Require an executed named fixture assertion for G4-S07.

### M4-A2-04: Voyage listing waits for independent parent catalogs and can leave a promise rejection unhandled (Medium)

`web/src/maritime/MaritimeWorkspace.tsx`, `reload`, starts `listMaritime(...)` and then awaits all active port and vessel pages before awaiting the list request. A failing voyage-list promise may reject before it is awaited, while lengthy parent pagination is still active, leading to a temporarily unhandled rejection. Failure of either parent catalog also suppresses otherwise available voyage rows. An Analyst viewing existing voyages should not need the complete editable parent picker.

**Required correction:** Await the voyage-list request immediately or launch all requests in a single settled concurrency boundary. Decouple optional form catalogs from read-only row loading, and guard stale generations and abort/cancel behavior for all requests. Test one failed/delayed request while other requests resolve.

### M4-A2-05: G4-S14 does not explicitly require the new queued revocation test (Medium)

`scripts/assert-g4-evidence.mjs` maps `MaritimeRevocationInterleavingTests.QueuedAdministratorDowngradePrecedesContainerMutation` to G4-S09 and G4-S20, but not to G4-S14, where live membership/organization revocation under queued operations is the explicit requirement. G4-S14 may be described as evidence present using lighter scenarios without the newly authored lock-observed case. Since S20 also requires the case, this does not by itself bypass the overall G4 gate, but it misstates scenario-level proof.

**Required correction:** Add the named case to S14, add a real queued read case, and require before/after database state assertions for every revocation interleaving.

### M4-A2-06: Additional named G4 coverage is incomplete or weakly asserted (Medium)

- G4-S05 explicitly requires an existing authenticated session and selected organization to survive the M3-to-M4 upgrade/rollback/reapply. `SeededM3HistorySurvivesM4UpgradeRollbackAndReapply` provisions a user and organization but creates no authenticated session and asserts only user/membership totals.
- G4-S18 requires the same valid identifier in **different organizations** to succeed. Current M4-specific tests exercise within-tenant conflict and cross-tenant isolation, not that permitted positive cross-tenant duplicate.
- G4-S13 labels Manager access allowed based on `Assert.NotEqual(HttpStatusCode.Forbidden, ...)` for randomized or missing parents/records. That allows server errors to satisfy this part of the permission test. Other successful lifecycle tests exist, but this role matrix alone cannot prove positive access.
- G4-S14 suspended organization tests use only non-OK and non-Created comparisons, so a server error also satisfies their negative assertions.

**Required correction:** Require exact expected HTTP codes and sanitized error envelopes, M3-to-M4 live session/selection preservation, and the permitted identical-identifier cross-tenant positive case.

## Audit boundaries and status

This is a new **source audit**, not a successful G4 verification. The exact audited commit has no newly executed .NET, PostgreSQL, browser, E:-backed local test, or image-scan evidence in this review. None of the six findings has been fixed in this audit. Original M4 corrections and the first audit remain in `M4_CORRECTNESS_AUDIT.md`.

M4 remains on its feature branch. G0-G3 remain the accepted baseline on `main`. D-003 is still pending. Do not merge M4 or mark G4 passed. No GitHub Actions execution is authorized.

## Remediation order

1. Correct the cross-tenant SQL test controls and the D-003 decision gate.
2. Complete five-role deterministic fixtures and strengthen the scenario-to-evidence mapping.
3. Decouple browser list loading from the parent catalogs and add failure/race cases.
4. Close precise HTTP/status and migration preservation assertions.
5. Run complete E:-backed local G4 verification, remediate every failure, rerun at one final SHA and independently review the raw evidence before acceptance.
