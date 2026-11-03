# M7 Correctness Audit Remediation

**State:** Source and execution-time corrections committed; G7 acceptance pending.
**Implementation branch:** `feat/m7-tracking-source-validation`.
**Decision boundary:** D-002 accepted 2026-11-03 (Option A); see the [decision record](M7_D002_TRACKING_REFERENCE_PROPOSAL.md). G7 acceptance pending.
**Deployment boundary:** Local only. No durable tracking-event receipt or successful ingestion response until M8.

The [second correctness audit](M7_SECOND_CORRECTNESS_AUDIT.md) records follow-up fixes, including EF tracked-state refresh, migration fixtures, capped limiter partitions, future credential revocation timing, security mutations and verification-network isolation.

## Remediation register

| Finding | Implemented correction | Required execution evidence |
| --- | --- | --- |
| Malformed EF Core model and snapshot | Both model files restored from the accepted M6 baseline and the M7 entity configurations inserted once, identically in model and snapshot | Release build, EF pending-model check and fresh M7 migration |
| Credential validation and revocation race | Credential, source and organization joined in one authorization query, with PostgreSQL row-share locks and direct lifetime/revocation checks | Concurrent revoke/suspend versus authentication and controlled database interruption |
| Organization suspension versus source rotation | Create, rotate, revoke and disable lock organization before source inside a transaction | Concurrent organization-status mutation with rotation and source provisioning |
| Unbounded anonymous database probes | IP pre-authentication limiter executes before database lookup, with separate invalid-credential and source partitions | HTTP 401/429 threshold, remote peer and rate partition scenarios |
| Mutable credential identity and historical records | PostgreSQL source and credential triggers forbid deletions, identity/digest changes, revocation reversal, expiry extension and disabled-source reactivation | Direct SQL mutation probes and migration upgrade/reapply |
| No database-enforced two-key limit | Source-locked issuance trigger rejects a third overlapping credential window, including future overlap | Parallel rotation and raw SQL overlapping third-key insert |
| Canonical field inconsistency | JSON parser, schema, test fixtures and HTTP examples consistently use `eventFamily` and `eventCode` | Contract fixture parser and end-to-end HTTP tests |
| Identifier regex end matching | Use absolute string boundaries for event and transport identifiers and strict timestamp grammar | Decoded control-character, trailing newline and UTC boundary tests |
| Credential status error ambiguity | Invalid, expired and revoked tokens receive 401; a cryptographically valid token linked to a disabled source or organization is denied 403 | Negative HTTP source and organization-state cases |
| Sparse M6 upgrade coverage | Populated rehearsal preserves actual accounts, sessions, memberships, ports, vessels, voyage, container, shipment history and assignments | Exact database row snapshots across M6 to M7, rollback and reapply |
| Foreign credential test was inconclusive | Both tenants now have identical source codes and distinct credentials; cross-tenant credential revocation is explicitly denied | Two-tenant PostgreSQL and HTTP checks |
| Canonical event lacked trusted tenant identity | Validated in-memory event is bound to the authenticated database source and organization | Positive source-bound parse and spoofed-tenant rejection |
| Incomplete G7 runner | Added source-locked SDK build/test container, disposable PostgreSQL, cross-browser regression, source-specific outage, security scans, hashed evidence and resource cleanup | Real Windows E:-backed end-to-end execution and independent evidence review |

## G7 execution findings

Running the committed work on a Windows E:-backed workstation exposed defects that source review did not find. Each was reproduced from raw output, corrected at its cause and covered by a rerun.

| Finding | Root cause | Correction | Evidence |
| --- | --- | --- | --- |
| Integration test project did not compile (CS8361) | A conditional expression was written inside an interpolated SQL string in the credential-window test | The value is computed before the interpolation | Release build with warnings as errors |
| Two xUnit analyzer errors (xUnit2031, xUnit1031) in the rotation race tests | `Assert.Single` over a filtered sequence, and blocking `Task.Result` reads in an async test | `Assert.Single` with a predicate, and `await` of the completed task | Release build with warnings as errors |
| `Migrations_AreDiscoveredInOrder` failed | The expected migration list ended at the M6 migration | The M7 migration `20261103090000_IntegrationSources` is listed | Focused rerun and the full integration run |
| Populated M6 upgrade test failed with `Shipment status history immutable` | The earlier audit assumed the test must seed a status-history row. The accepted M5 trigger already records the initial transition and rejects direct inserts, which is a security property | The test no longer inserts history and asserts the trigger-created `Draft` row is preserved across upgrade, rollback and reapply | Focused rerun and the full integration run |
| Mutation runner used a different cache layout from the main runner | NuGet, SDK home and temporary paths pointed outside the G7 cache tree, so packages would be fetched into a second location | Paths match the cache tree created by the main runner, and the per-variant time limit is raised | Controlled mutation run |
| Runner rejected a valid E: root | Git Bash mounts E: at `/tmp` when `TEMP` is the drive root, so the posix-form `/e/*/m7` check failed | The drive is judged from the Windows form of the path before translation | Runner preflight |
| Revocation and pagination browser specs failed in all three engines | The runner provisioned shorter account and organization display names than the accepted G6 fixtures, and both specs select options by label | Names match the accepted fixtures exactly | Targeted browser rerun, then the full browser stage |
| No test that a source bearer is refused by human-scoped endpoints | Coverage gap against G7-S10 | `SourceBearerCannotAuthenticateHumanScopedEndpoints` | Boundary HTTP suite |
| No test that cancellation never succeeds or consumes a credential | Coverage gap against G7-S21 | `CancelledAuthenticationNeverSucceedsOrAcknowledges` | Boundary HTTP suite |
| No per-scenario result file and no scanner database identity | Coverage gap against G7-S24 and G7-S25 | `scripts/summarize-g7-evidence.mjs` derives each scenario outcome from raw results, Trivy version and database identity are recorded, and the web stage fails if a source credential pattern reaches the built frontend | Evidence summary stage |

Runtime library warning: every CLI command and the API process print `Cannot load library libgssapi_krb5.so.2` once. It is emitted by the .NET runtime's native security library when the PostgreSQL client probes GSS encryption, the runtime image does not ship the Kerberos library, and no behavior is degraded; appending `GSS Encryption Mode=Disable` to the connection string removes it with identical results. It is not a defect and no change was made to the tested revision. Details are in the G7-S06 supplement assessment.

Known limit: the `499` response for a client that disconnects mid-request is implemented in the endpoint but is not driven end to end through HTTP, because the test client cannot observe a response it has abandoned. The store-level cancellation test and source review cover it.

## Verification state

Development runs on the E:-backed workstation compiled the solution, ran the unit, contract and PostgreSQL integration suites, ran the five controlled security mutations and exercised the Compose, browser, outage and image-scan stages. Development runs are not gate evidence. Gate evidence is one complete run of `scripts/verify-m7-local.sh` on one final revision, recorded in the executed verification record.

The G7 runner is `scripts/verify-m7-local.sh`. It requires a clean E:-backed checkout, `HARBOROPS_VERIFICATION_ROOT` on E: ending in `m7`, and an E:-backed Docker Desktop image verified by `HARBOROPS_DOCKER_VHDX`. It creates disposable resources under a unique `ho-verify-g7` Compose project and preserves evidence under the configured E: root. The source CLI interactive stage requires `HARBOROPS_G7_INTERACTIVE_CLI=1` and an unredirected private terminal. It displays source credentials once and does not record them in the evidence archive.

No GitHub Actions, retained Docker volume, retained database or C: persistent storage may be used. Only isolated G7-labeled resources can be torn down. Dedicated accessibility verification remains M24-04.

## Remaining G7 acceptance work

D-002 was accepted as Option A (hash-pinned local snapshots, not official immutable releases). Every G7-S01 through G7-S25 scenario must receive an executed result, including concurrent mutation effectiveness and independent review of logs, coverage, image digests and E:-backed cleanup evidence. A completed technical runner is not by itself an accepted gate. G7 acceptance and any merge into `main` require an explicit subsequent decision.
