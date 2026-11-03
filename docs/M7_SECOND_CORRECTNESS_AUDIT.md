# M7 Second Correctness Audit

**State:** Confirmed defects remediated on the M7 feature branch. Source review only. Executable G7 evidence is not yet available.
**Scope:** Re-audit of the post-remediation M7 branch, including source lifecycle, EF Core, SQL guards, HTTP boundaries, fixture completeness, Compose isolation, rate limiting and local G7 evidence.
**G7:** Not executed or accepted. **D-002:** Acceptance pending. **Main:** Unchanged.

## Confirmed defects and remediation

| Severity | Finding | Correction | Required execution |
| --- | --- | --- | --- |
| High | The HTTP boundary test used a JSON response extension without a matching import and read error bodies inconsistently | Parse each response body once using System.Text.Json and remove unnecessary extension dependencies | Release build and focused HTTP tests |
| High | The populated M6 migration test expected a shipment status-history record without inserting one | Explicitly seed a valid M5 status-history row before taking the preservation snapshot | M6-to-M7 populated upgrade, rollback and reapply |
| High | Tracked organization, source and credential instances could remain stale after changes by another context, despite a locking SQL query | Refresh each entity while holding its transaction lock, including idempotent revocation | External suspension, duplicate disable and post-revoke tests |
| Medium | LINQ SingleAsync could compose a query around raw SELECT FOR UPDATE SQL | Materialize the bounded locking result without provider-side composition, then select the sole row | PostgreSQL locking and concurrency cases |
| High | The original G7 Compose network reused the accepted stack's fixed subnet | Assign a distinct G7 subnet and test the exact resolved network and trusted proxy configuration before Compose starts | Isolated G7 stack and transport checks |
| Medium | Request-limiter partitions could grow without a hard bound under high remote-address cardinality | Enforce a maximum of 4096 partitions with stale-window reclamation and fail-closed overflow | Threshold, concurrent-call and partition-cap tests |
| Medium | A purported chunked oversized-request test used a seekable body with a computable Content-Length | Send an HttpContent body that deliberately has unknown length | HTTP streaming and 413 body-limit checks |
| Medium | Direct SQL inserts and updates could bypass the source display-name constraints enforced in C# | Add matching PostgreSQL and EF model/snapshot constraints, plus direct SQL negative testing | Fresh migration, model diff and SQL constraint test |
| Medium | The browser fixture still used obsolete family/code JSON property names | Use eventFamily/eventCode consistently | Browser and fixture contract checks |
| Medium | Bearer formatting accepted a non-256-bit caller-supplied secret and rejected decoded data was not always cleared | Require a nonempty credential ID and exactly 32 secret bytes; clear invalid decoded bytes | Bearer parsing/entropy tests |
| High for G7 evidence | The G7 runner did not prove security tests would detect disabled controls | Add an isolated E:-backed five-variant security mutation stage with passing unmodified controls | Run all mutants and verify intended failures from raw TRX |
| Medium | The revocation race test could pass before the validation query reached the row lock | Poll PostgreSQL lock-wait state before committing the revocation, then verify denial | Real concurrent PostgreSQL sessions |
| High | The credential schema admitted future creation timestamps while source disable used the current time as the revocation timestamp | Require coherent created/not-before ordering, bound future creation skew, use monotonic source timestamps and a revocation timestamp no earlier than creation | Future-not-before, early disable, revocation and SQL-check tests |
| Medium | The advisory and image scanning stages did not fully distinguish missing advisory data or identify every scanned image | Fail on NuGet advisory-service errors, record scanned image digests, and include explicit CLI opt-in denial | E:-backed dependency and image scan records |

## Audit methodology and limits

Repository code, migration SQL, EF model declarations, synthetic fixtures, test methods and verification scripts were inspected. Eight source-contract checks passed in a script-level evaluation; this is not an application build or G7 runtime pass. Source-level static checks are different from a compiler, PostgreSQL database engine or browser execution. This audit does not claim successful .NET compilation, database migrations, browser tests, controlled outage, dependency/image scans, or mutation execution.

Every G7-S01 through G7-S25 check remains subject to execution and independently reviewed, commit-bound evidence. Some checks, including protected operator-terminal issuance and upstream DCSA reference approval, require separate recorded action.

## Required next actions

Run the clean E:-backed `scripts/verify-m7-local.sh` against one exact implementation SHA. Do not use GitHub Actions or retained volumes. Inspect every failure and apply fixes on the M7 feature branch. Review the source-locked technical evidence, complete the D-002 reference decision, resolve pending scenario outcomes and request explicit G7 acceptance before merging. No tracking event may receive a 2xx intake response until M8 implements durable receipt.

## Correction recorded during G7 execution

The finding that the populated M6 migration test "expected a shipment status-history record without inserting one" was itself mistaken. The accepted M5 `shipments_status_history` trigger records the initial transition on insert, and the `status_history_immutable` trigger rejects direct inserts into `shipment_status_changes`. The test was corrected to rely on the trigger, not to seed a row. See the [remediation register](M7_CORRECTNESS_REMEDIATION.md) for the other defects found by execution.
