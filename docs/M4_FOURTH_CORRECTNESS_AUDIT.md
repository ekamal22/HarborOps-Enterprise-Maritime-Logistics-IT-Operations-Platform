# M4 Fourth Correctness Audit: Repeated Integration Isolation

**Date:** 2026-11-01  
**Audited execution SHA:** `bbb672a5b0ab094c1ae2c21d50ec5faf0101cbee`  
**Branch:** `feat/m4-voyage-container-references`  
**Gate:** G4 OPEN, acceptance pending

## Observed execution evidence

The E:-backed G4 run passed its four decision-unit tests, schema-index precheck, preflight, and PostgreSQL image build. The .NET 10 Release build completed with zero warnings and zero errors. All 82 unit and 12 contract tests passed. All 87 fresh integration tests passed, including the current EF snapshot and historical M3-to-M4 migration checks.

The second integration execution, against the **same disposable PostgreSQL database**, completed with 86 of 87 passed. One test failed:

`MaritimeGateCoverageTests.IdenticalContainerIdentifierIsAllowedInDifferentOrganizations`

At the assertion on the matching container count, the test expected 2 but observed 4. The test used the hardcoded identifier `SYN-BOX-SHAREDG4` across both executions and queried the entire shared database by identifier. Each execution created two newly scoped organizations and inserted two records with the same fixed identifier; the first execution's records intentionally remained. The second therefore observed four records.

This was a test data-isolation error, not observed evidence that PostgreSQL rejected or leaked cross-tenant data. The first execution demonstrated successful creation of identical identifiers in two independent organizations.

## Correction

`tests/HarborOps.IntegrationTests/MaritimeGateCoverageTests.cs` now derives a valid synthetic container identifier from a new 16-character uppercase GUID fragment per test invocation. Both records within the invocation deliberately use that identical identifier. The test asserts:

1. Exactly two matching records for that invocation's identifier.
2. They belong to exactly two distinct organizations.
3. One record belongs to each of the two current fixture organizations, with version 1.
4. The selected organization's API search returns exactly its own record with the expected identifier.

The underlying test still proves cross-tenant identifier reuse is permitted and scoped API reads cannot disclose the other organization. The second execution no longer matches historical records merely because the test identifier was hardcoded.

The local .NET runner also prints the start of each test batch and its per-batch log file path, so the repeated integration execution is visibly started before its detailed output is collected. This changes logging only, not test execution or success criteria.

## Correctness review

| Check | Source-audit result |
| --- | --- |
| Identifier syntax remains `SYN-BOX-[A-Z0-9]{4,16}` | Confirmed |
| Same identifier shared by both organizations within one invocation | Confirmed |
| New identifier per invocation and no reference to stale fixed identifier | Confirmed |
| Exact two records and distinct organization IDs required | Confirmed |
| HTTP search uses that generated identifier and asserts tenant and identifier | Confirmed |
| Runner still executes fresh and repeat integration against the same disposable PostgreSQL instance | Confirmed |
| Runner still fails G4 on any batch error | Confirmed |
| Database migration, domain and authorization production code changed | No |
| Current corrected revision fresh/repeat integration result | Passed 92/92 fresh and 92/92 repeated at `f1854c3`, see [fifth audit](M4_FIFTH_CORRECTNESS_AUDIT.md) |
| Browser, outage/recovery, security and full G4 evidence result | Executed at `f1854c3`, all passed except the D-003 gate, see [fifth audit](M4_FIFTH_CORRECTNESS_AUDIT.md) |

The previous G4 run stopped in the `dotnet` stage before frontend, stack, browser, outage/recovery, scan, and final evidence stages. Its zero-resource cleanup warning is expected in that early-stop path. Do not treat that warning as a separate Docker failure.

## Required continuation

Pull the latest M4 feature-branch revision and run complete local verification on E:-backed disposable resources, preserving the full original and new raw evidence. Do not run GitHub Actions, change accepted M0-M3 work, merge to main, or mark G4 accepted before all mandatory scenarios are independently verified. D-003 remains pending.

For any unavoidable temporary files on C:, record exact locations and pre/post usage, do not touch unrelated system or user files, and delete only attributable disposable artifacts after preserving evidence on E:. Do not remove Windows Installer caches, Docker images, system temporary directories or unrelated user data as a broad cleanup step.
