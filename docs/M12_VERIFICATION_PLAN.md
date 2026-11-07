# G12 Source-Locked Verification Plan

**State:** NOT EXECUTED. G12 acceptance pending. Applies only to final frozen M12 source SHA. G5 accepted; G11 is not an M12 prerequisite.

| ID | Required scenario | Expected proof |
| --- | --- | --- |
| S01 | Explicit M12-P01..P16 decision | Recorded policy disposition |
| S02 | Build/typecheck/contracts | .NET and web outputs, zero warnings |
| S03 | M5 identifier and status compatibility | Case, null, archive and workflow fixtures |
| S04 | M6 voyage assignment | Current, closed, future, foreign and duplicate |
| S05 | Origin/destination port filters | Matching and wrong-tenant port UUIDs |
| S06 | UTC windows | Equal, just before/after, null and offsets |
| S07 | Invalid and combined filters | Repeated, unknown, malformed, encoded query >4096, AND semantics |
| S08 | Cursor integrity and UUID paging | Replay, expiry, tamper, tenant/filter/user/session swaps |
| S09 | Concurrent writes and live pages | No incorrect snapshot claim |
| S10 | Search-to-M5 detail | Fresh authorization and archive semantics |
| S11 | Five-role search/export matrix | Analyst/Manager export, Auditor search only, Administrator/Support denied with real restricted SQL login |
| S12 | Tenant boundary in SQL and HTTP | Cross-org UUID/join/export denial |
| S13 | Revocation, session and org switch | In-flight and post-revocation behavior |
| S14 | Search and export parity | Identical stable fixture/filter subset |
| S15 | CSV injection and encoding | Formula, quotes, comma, newline, Unicode |
| S16 | Safe fields and response headers | Fixed 12-column schema, no-store |
| S17 | Row/byte/timeout limits | 5000, 5001, 10 MiB, 15-second statement timeout, 30-second total deadline, no partial CSV |
| S18 | M16 signed export audit and restricted role | Nine-field HMAC substitution, expiry, key missing/mismatch/rotation, unreadable verifier table, replay, SQL forgery, audit outage, M12 downgrade refusal after audited exports |
| S19 | Outage/cancel/rate/concurrency | Safe no-partial-output failure |
| S20 | PostgreSQL plans | EXPLAIN actual cost for representative data |
| S21 | Q2 historical snapshot | Disposable clone test or evidenced NOT APPLICABLE |
| S22 | Three-engine browser | Search/export/role/draft/organization changes |
| S23 | Accepted source regressions | M2/M5/M6/M10/M16, M9 hold |
| S24 | Effective security mutants and scans | Intended assertions kill defects, no unresolved high/critical |
| S25 | Independent raw-evidence review | SHA, artifacts, hashes, failure logs and residuals |

## Efficient tiers

**Tier 1:** On the frozen candidate, `dotnet build HarborOps.sln --configuration Release`; targeted unit/contract suites; `npm --prefix web run build`; `node scripts/render-execution-graph.mjs --check`; `node scripts/check-planning-docs.mjs`. Stop on source defects before heavy tests.

**Tier 2:** Real scoped HTTP/PostgreSQL `ShipmentSearchHttpTests`, restricted API DB role, migration 14 on fresh and copied populated source, signer and key provisioning, audit function grants, replay index, missing/mismatched/rotated key, cross-role/revocation/SQL, failure atomicity, row and byte limits. The restricted-role test reprovisions a global PostgreSQL login, so run only on an exclusive disposable database/server; no parallel mutable DB suites.

**Tier 3:** One final source-locked G12 campaign, required relevant regressions, browser tests in Chromium/Firefox/WebKit using `HARBOROPS_M12_FIXTURES=1`, applicable security/mutation/image scans, measured query plans and Q2 historical check. Parallelize non-conflicting static checks only; isolate browser ports/profiles and mutable DB state.

Evidence on E: must include exact SHA, clean/dirty status, branch/parent/migration hash, config digest without secrets, commands, versions, exit codes, timing, TRX/JUnit, SQL/HTTP and browser captures, mutation effective-kill reasons, scan outputs, failures and SHA-256 manifest. Mark each S01..S25 PASS, FAIL, BLOCKED or NOT EXECUTED based only on executed artifacts. An independent reviewer reconciles S25; explicit G12 gate acceptance remains separate. Never run GitHub Actions or alter the only retained volume.

## Adversarial checks

Remove tenant predicates, the M6 association scope, cursor org/fingerprint binding, live role validation, export limit, CSV formula defense, export audit or failure rollback in disposable mutations and prove targeted assertions fail for their intended reason. Test API-login forged audit arguments and unauthorized audit-write SQL. No test execution has been claimed. See [source correctness audit](M12_CORRECTNESS_AUDIT_20261107.md) for the required evidence distinctions.

## Second-pass additions

The [2026-11-08 source audit](M12_CORRECTNESS_AUDIT_20261108.md) added tests for a full five-role HTTP matrix, nine forged/modified proof fields, oversized URLs, and non-destructive EF migration rollback/privilege-operation assertions, plus source guards for audit rollback and database/total export deadlines. G12 must validate these in an exclusive real PostgreSQL and HTTP campaign. Do not infer any new PASS from the source additions.
