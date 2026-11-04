# M9 Correctness Audit: Follow-up Source Review

**Date:** 2026-11-04  
**Reviewed revision:** `dd68fb6c4bedd1cf1279394d152e9bdd5f10b58a`  
**Branch:** `feat/m9-worker-reconciliation`  
**Baseline:** G0 through G8 remain accepted on `main` at `7b9e19d730e3a09c5e1184262460dfeac9319e3d`.  
**Method:** Read-only comparison of the updated source, migration, grants, role policy, deployment definitions and regression-test source. No application build, test suite, PostgreSQL query, browser execution, Docker image, mutation campaign or G9 gate run was performed.  
**Later runtime status:** The campaign recorded in [M9 executed verification](M9_EXECUTED_VERIFICATION.md) executed the behaviors reviewed here; this audit itself remains a source-only review.  
**Disposition:** All ten initial findings have source changes addressing their identified causes. Runtime and integration correctness remain unproven. G9 acceptance pending.

## Audit findings and corrective changes

| ID | Original issue | Source correction | Audit disposition |
| --- | --- | --- | --- |
| CA-01 | Worker lacked SELECT for deferred effect completeness check | `m9-worker-role.sql` explicitly grants narrowly scoped reads on effects and attempt history | Corrected in source; role execution pending |
| CA-02 | Missing EF transaction-extension namespace | `TrackingWorkStore.cs` and `TrackingWorkAdminStore.cs` now import `Microsoft.EntityFrameworkCore.Storage` | Corrected in source; compilation pending |
| CA-03 | G9 Compose overrides could inherit default ports and API URL | `compose.g9.verify.yaml` is now a standalone stack with dedicated 55439/8089/5179 host ports, subnet 172.30.249.0/24 and matching browser API address | Corrected in source; compose rendering pending |
| CA-04 | Concurrent reference changes could be misclassified as permanent failures | Effect-validation SQL emits a named constraint; `WorkerShell` catches that exact error and `TrackingWorkStore` parks the still-valid lease in `NeedsReconciliation` using a new transaction | Corrected in source; race test pending |
| CA-05 | Privileged organization lock used hard-coded public schema | The migration binds its organization table to the migration-time schema in a fixed, quoted SECURITY DEFINER function; isolation-schema upgrade test now claims real work | Corrected in source; PostgreSQL upgrade test pending |
| CA-06 | Worker INSERT access could forge append-only attempt events | Work-transition audit is generated inside a transactionally coupled SECURITY DEFINER trigger; worker role no longer has direct attempt-table INSERT/UPDATE/DELETE privileges | Corrected in source; privilege-adversarial test pending |
| CA-07 | MaxRedrives was configurable but the API enforced a hard-coded value | MaxAttempts and MaxRedrives are validated as fixed five/three contract values, matching database constraints and redrive logic | Corrected in source; startup-configuration tests pending |
| CA-08 | Redrive transaction did not recheck account security epoch | Locked redrive query now requires `auth_sessions.security_version = users.security_version` alongside active session, role and membership predicates | Corrected in source; revocation-race test pending |
| CA-09 | Tenant quota assertion counted foreign rows and omitted rejected intake | The predicate is correctly grouped; rejected ingress attempts cover `Leased` and `NeedsReconciliation`. Distinct-context and independent-worker-process race tests now use coordination barriers | Corrected in test source; real two-process runtime witness pending |
| CA-10 | Running container did not signal database or permission readiness | Worker offers a bounded `--readiness` probe checking connectivity and operational grants; both worker Compose definitions use a health check | Corrected in source; unhealthy/recovery runtime test pending |

## Additional source-review invariants

- Original M8 receipt, accepted-event and conflict tables and M8 migration remain unchanged.
- Work-state transitions retain database-side lease-token, epoch, time and attempt constraints.
- A unique immutable processing effect remains required for a terminal Processed outcome.
- The worker cannot directly insert attempt events. Every valid non-redrive work transition produces its own append-only attempt event from PostgreSQL.
- Manual redrive still requires an active Manager session, current account security version, CSRF checks, a sanitized reason, a bounded generation and immutable attributed evidence.
- Successful equipment resolution requires a unique tenant-scoped, time-valid shipment-container assignment. Opaque transport identifiers remain unresolved, not guessed.
- The G9 worker image is opt-in; default Compose resources and accepted M8 storage remain untouched.
- `TrackingWorkerProcessRaceTests.cs` now starts two independent worker executables in an isolated named schema, observes PostgreSQL lock contention, and asserts a single claim and disposition. Its execution is not yet evidence.
- Source checks were inspected across 15 independent invariants. These are structural inspections, not executed test results.

## Remaining blockers and evidence required

1. M9-01 local contract: the six proposed decisions are **acceptance pending** and not silently frozen by this source correction.
2. Compile the application, worker and test projects with warnings as errors. Confirm NuGet restore and EF model snapshot parity.
3. Execute fresh/populated migrations under both the primary application schema and isolated named schemas. Prove the restricted worker role can process real work, cannot forge attempts and cannot bypass operator redrive.
4. Run unit, contract, integration and affected G0-G8 regression suites twice against isolated E:-backed PostgreSQL, with source-locked results.
5. Execute the authored two-process PostgreSQL lock-barrier test, then additional worker collisions, lease expiry, crash windows, ambiguous COMMIT outcomes, claim recovery, suspension, quota boundary and adversarial mutation tests.
6. Render and run the standalone G9 Compose stack in isolation, confirm health checks and teardown. Do not touch retained application volumes.
7. Run browser coverage as applicable, dependency and exact-image vulnerability scans, and seal the raw G9 evidence archive for independent review.

The existing development runner is a smoke/regression entrypoint, not a full G9-S01 through G9-S25 acceptance campaign. Test source is not evidence that the tests passed. A positive source audit alone is not grounds for merging into `main`.

**Conclusion:** The ten initial defects have been addressed in the inspected source. End-to-end behavior remains unverified until the listed execution evidence is produced. **G9 acceptance pending.**
