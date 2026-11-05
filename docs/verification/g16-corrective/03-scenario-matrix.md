# G16-S01 to G16-S30 disposition, corrected revision

**Source revision for every row:** `e5711573839b185a2dfc7d9f2c64ce4fc18d4c13` (branch `feat/m16-audit-access-hardening`). Final campaign: unmodified `scripts/verify-m16-local.sh`, 2026-11-04 21:48:51Z to 22:59:24Z, runner exit 0. **Acceptance pending.**

Evidence root `E:\HarborOps-Verification\evidence\new-campaign\m16` (private). Keys: **TRX** `evidence/trx/g16-integration*.trx`; **HTTP** `evidence/audit-http-matrix.log` (74 checks); **OUT** `evidence/audit-outage.log` (14 checks); **MUT** `evidence/g16-mutations.json`; **ROLES** `evidence/restricted-db-roles.log`; **BR** `evidence/playwright/*.json`; **SUPP** supplemental probe on this revision, `supplement-s07-s08-s28-output.txt` (console output), `supplement-s07-s08-s28-results.json` and `supp-s07-s08-s28.mjs` in this folder (not a runner stage).

| ID | Observed result | Evidence | Status | Limit |
| --- | --- | --- | --- | --- |
| S01 | Taxonomy `membership.role_changed`, `integration.tracking_accepted`, table-operation types; actor role `Administrator`; metadata keys exactly `operation` and `changedFields` from a fixed field allowlist; no values, credentials or payloads; retention is policy only and nothing deletes rows | HTTP, `AuditHardeningTests`, MUT (`unredacted-metadata`) | PASS | Archival and purge are not implemented by design (M16-D02) |
| S02 | Fresh migration creates table, indexes, guards, capture triggers and the two runtime roles; compose migrator succeeds | compose stage, `AuditMigrationTests`, role test | PASS | Migration login is a superuser (F006) |
| S03 | Accepted M8 data survives the M16 upgrade; M8 receipts and keys unchanged | TRX (`AuditMigrationTests`, `TrackingInbox*`) | PASS | Small fixture volume |
| S04 | Snapshot equals the migrated model, migrations in order | TRX `MigrationSnapshotTests` | PASS | |
| S05 | Audited mutation writes the correct row in the same transaction with actor, session, role and changed fields | HTTP, TRX | PASS | |
| S06 | Injected audit failure rolls the business change back; swallowing the failure is detected | TRX, MUT (`swallow-audit-failure`), HTTP S22 | PASS | |
| S07 | Stale, invalid and unauthorized mutations leave no business change and no audit row | SUPP | PASS | Supplement; the unauthorized case used an account without membership (409), not a role denial. Role-denied business mutations are covered by the accepted M2 to M8 suites |
| S08 | 80 contended same-version updates give 10 winners and no server errors, audit rows equal committed updates; API killed mid-burst leaves audit equal to commits; lock-race tests pass | SUPP, TRX x2 | PASS | Process kill, not storage failure |
| S09 | Runtime UPDATE, DELETE and direct INSERT on `audit_entries` fail with 42501 for API and worker; guards raise 23514 for the owner | TRX `AuditHardeningTests`, ROLES, MUT (`api-can-delete-audit`) | PASS | |
| S10 | TRUNCATE fails (42501 for runtime roles, 23514 guard for the owner); removing the guard is detected | same, MUT (`no-truncate-guard`) | PASS | |
| S11 | API and worker cannot create objects, disable triggers, replace guard functions, create roles or change replication role | TRX role test (14 statements per role), ROLES, MUT (`public-can-create-objects`) | PASS | |
| S12 | Migration identity migrates; API and worker grants minimal (worker cannot read audit) | compose, role test, MUT (`worker-can-read-audit`) | PASS | M9 grants checked only in the diagnostic |
| S13 | Active selected-organization Auditor lists only that tenant | HTTP, `AuditHttpTests` | PASS | |
| S14 | Detail read with exact response allowlist | HTTP | PASS | |
| S15 | Analyst, Manager, Support and Administrator get 403 on list and detail | HTTP | PASS | |
| S16 | Foreign ids and organizations indistinguishable from unknown ids (404, identical bodies) | HTTP | PASS | |
| S17 | Multi-organization Auditor role does not cross tenants; mixed-role user denied | HTTP | PASS | |
| S18 | Role downgrade, removal, session revoke, account disable and organization suspend deny the next request | HTTP | PASS | |
| S19 | All 12 original lock and revocation race tests pass twice; reintroducing the FK is detected | TRX x2, MUT (`audit-fk-lock-inversion`) | PASS | |
| S20 | Anonymous 401; spoofed forwarded headers from the trusted peer and from an untrusted peer do not widen access; untrusted peer over plain HTTP is refused (400 or 403); audit routes are GET only so no CSRF token is required, and write verbs return 405 | HTTP | PASS | Origin and CSRF protections apply to mutations; none exist on audit routes |
| S21 | Denied reads recorded once; at most 30 denial rows per user per window; forbidden mutations leave no effects | HTTP, SUPP | PASS | Total volume is bounded per window, not overall |
| S22 | Forbidden request stays 403 and authorized read returns 503 `audit_unavailable` when audit recording fails | HTTP | PASS | Injected failure mode is a revoked EXECUTE grant |
| S23 | No credentials, cookies or e-mail text in list bodies, audit metadata or evidence; evidence safety passed on 162 files; no traces were produced | HTTP, TRX, `evidence-safety.log` | PASS | |
| S24 | Malformed page, limit, cursor, id and filter input rejected or inert; tampered cursors rejected | HTTP, `AuditCursorTests` | PASS | The 1024-byte metadata CHECK is not exercised separately |
| S25 | One `audit.read` row per successful read, one `audit.read_denied` per denial, no recursion | HTTP | PASS | |
| S26 | Cursor walk under concurrent writes has no duplicates or gaps against the pre-walk snapshot; limiter 30 per minute returns 429 | `AuditCursorTests`, HTTP, MUT (`cursor-ignores-organization`) | PASS | Rows committed late with an older timestamp can fall behind an issued cursor |
| S27 | POST, PUT, PATCH, DELETE return 405; no export or global route | HTTP | PASS | M12 export does not exist yet |
| S28 | Operator rows are `OperatorCli` with no user or session; tracking events are `IntegrationSource` and ignore stale human context; human rows carry role | SUPP, TRX, MUT (`stale-human-context`), HTTP | PASS | |
| S29 | Real PostgreSQL stop: explicit 503, mutations not reported successful, anonymous 401; restart on the same volume: read recovers in 39 ms without API restart, pre-outage audit rows intact | OUT | PASS | Single database instance |
| S30 | Chromium, Firefox and WebKit each 61 of 61; M0 to M8 regression through 252 of 252 integration tests twice and the browser suite | BR, TRX | PASS (standalone M16) | Integrated M9: diagnostic only, see `02-m9-compatibility-refresh.md` (273 of 273 integration tests on a local combined revision; combined Compose, browser and outage verification not run) |

**Totals:** PASS 30 of 30 for the standalone M16 revision, with S30's integrated-M9 element limited to the separate diagnostic described above. No FAIL, BLOCKED or NOT EXECUTED rows. A runner exit of 0 was not treated as closing any row by itself.
