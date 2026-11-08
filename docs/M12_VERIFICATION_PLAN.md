# G12 Source-Locked Verification Plan

**Accepted milestone (2026-11-08):** M12/G12 was formally accepted and integrated into `main` through PR #33. [G12 gate matrix](M12_G12_EVIDENCE_MATRIX_20261108.md) and [formal G11/G12 closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) supersede earlier pending gate and source-only checkpoints. M12-P01..P16 are approved as implemented, including export audit, permission and per-instance limits. G12-S21 is NOT APPLICABLE to a nonexistent Q2 production snapshot. The M9 transport hold remains binding; no deployment.

**State:** Source-locked G12 verification completed and accepted on 2026-11-08. The [executed G12 matrix](M12_G12_EVIDENCE_MATRIX_20261108.md) and formal gate closure control dispositions; original test plan wording below is historical. G5 accepted; G11 is not an M12 prerequisite.

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
| S17 | Row/byte/timeout limits | 5000, 5001, 10 MiB, 15-second statement timeout, 25-second internal deadline and 30-second proxy read timeout, no partial CSV |
| S18 | M16 signed export audit and restricted role | Nine-field HMAC substitution, expiry, key missing/mismatch/rotation, unreadable verifier table, replay, SQL forgery, audit outage, M12 downgrade refusal after audited exports |
| S19 | Outage/cancel/rate/concurrency | Real slow DB, 25-second search/export budget, 30-second proxy envelope, browser abort, safe no-partial-output failure |
| S20 | PostgreSQL plans | EXPLAIN actual cost for representative data |
| S21 | Q2 historical snapshot | Disposable clone test or evidenced NOT APPLICABLE |
| S22 | Three-engine browser | M12 search/export/role plus accepted M5 delayed search response across organization switch and in-flight draft background refresh, in Chromium/Firefox/WebKit |
| S23 | Accepted source regressions | M2/M5/M6/M10/M16, M9 hold |
| S24 | Effective security mutants and scans | Intended assertions kill defects, no unresolved high/critical |
| S25 | Independent raw-evidence review | SHA, artifacts, hashes, failure logs and residuals |

## Efficient tiers

**Tier 1:** On the frozen candidate, `dotnet build HarborOps.sln --configuration Release`; targeted unit/contract suites; `npm --prefix web run build`; `node scripts/render-execution-graph.mjs --check`; `node scripts/check-planning-docs.mjs`. Stop on source defects before heavy tests.

**Tier 2:** Real scoped HTTP/PostgreSQL `ShipmentSearchHttpTests`, restricted API DB role, migration 14 on fresh and copied populated source, signer and key provisioning, audit function grants, replay index, missing/mismatched/rotated key, cross-role/revocation/SQL, failure atomicity, row and byte limits. The restricted-role test reprovisions global PostgreSQL logins, so it must run on an exclusive disposable PostgreSQL **server**, not merely an isolated database. Set `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1` only after verifying server isolation. Without the flag, the test reports SKIPPED and is not G12 PASS. The test collection disables same-assembly parallel execution; also prevent concurrency across other assemblies, runners and external processes.

**Tier 3:** One final source-locked G12 campaign, required relevant regressions, browser tests in Chromium/Firefox/WebKit using `HARBOROPS_M12_FIXTURES=1`, applicable security/mutation/image scans, measured query plans and Q2 historical check. Parallelize non-conflicting static checks only; isolate browser ports/profiles and mutable DB state.

Evidence on E: must include exact SHA, clean/dirty status, branch/parent/migration hash, config digest without secrets, commands, versions, exit codes, timing, TRX/JUnit, SQL/HTTP and browser captures, mutation effective-kill reasons, scan outputs, failures and SHA-256 manifest. Mark each S01..S25 PASS, FAIL, BLOCKED or NOT EXECUTED based only on executed artifacts. An independent reviewer reconciles S25; explicit G12 gate acceptance remains separate. Never run GitHub Actions or alter the only retained volume.

## Adversarial checks

Remove tenant predicates, the M6 association scope, cursor org/fingerprint binding, live role validation, export limit, CSV formula defense, export audit or failure rollback in disposable mutations and prove targeted assertions fail for their intended reason. Test API-login forged audit arguments and unauthorized audit-write SQL. No test execution has been claimed. See [source correctness audit](M12_CORRECTNESS_AUDIT_20261107.md) for the required evidence distinctions.

## Second-pass additions

The [2026-11-08 source audit](M12_CORRECTNESS_AUDIT_20261108.md) added tests for a full five-role HTTP matrix, nine forged/modified proof fields, oversized URLs, and non-destructive EF migration rollback/privilege-operation assertions, plus source guards for audit rollback and database/total export deadlines. G12 must validate these in an exclusive real PostgreSQL and HTTP campaign. Do not infer any new PASS from the source additions.

## Third-pass M5 browser compatibility checks

The [third-pass source findings](M12_CORRECTNESS_AUDIT_20261108.md#third-pass-m5-browser-regression-findings-2026-11-08) show that the M12 shipment workspace reads `/shipments/search`, whereas two accepted M5 browser tests still intercepted the original list URI. The `shipment.spec.mjs` deferred-response test and `shipment-draft.spec.mjs` unsaved-draft background refresh test were updated to intercept the route actually used by M12, without weakening their assertions. G12-S22 must execute both tests with `shipment-search.spec.mjs` against actual M5/M12 fixtures across all three engines. Their source correction does not constitute a passing browser result.

## Fourth-pass execution prerequisites

The [fourth-pass correctness audit](M12_CORRECTNESS_AUDIT_20261108_FOURTH_PASS.md) records the parameterized EF Core `ExecuteSqlAsync` change and 25-second total search/export budgets. Source inspection established both Nginx configurations use a 30-second API read timeout, but did not measure actual cancellation or HTTP response timing. S02 must compile the API with warnings-as-errors, and S17/S19 must exercise slow SQL, authentication middleware delay, store cancellation, timeout response, and the proxy path with actual timing and no partial CSV. S25 remains unexecuted.

## Fifth-pass isolation and cancellation coverage

The [fifth-pass source audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md) requires the restricted-role integration test to report SKIPPED unless `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1` is explicitly set for a dedicated disposable local PostgreSQL server. G12-S11/S18 must not be marked PASS if that test was skipped, even if the remainder of the integration suite succeeded. Capture the exact skip/run result, role grants, server identity and process isolation evidence. The CSV preparation path now checks cancellation during each row and immediately before/after UTF-8 encoding. G12-S17/S19 still require actual CPU-delay and database-delay observations, abort tests and a no-partial-CSV assertion. Do not assume the 25-second internal budget covers authentication middleware or network delivery.
