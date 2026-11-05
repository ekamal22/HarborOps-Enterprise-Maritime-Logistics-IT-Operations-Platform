# M16 / G16 corrective verification: final technical report

**G16 technical verification complete for the standalone M16 revision `e5711573839b185a2dfc7d9f2c64ce4fc18d4c13`. Independent evidence review and acceptance pending.** The integrated M9 element of S30 is covered only by a separate local diagnostic (section 9). Nothing was merged into `main`; GitHub Actions stayed disabled; no commit carries attribution.

## 1. Source and revisions

| Item | Value |
| --- | --- |
| Branch | `feat/m16-audit-access-hardening` |
| Initial failed revision | `ef08d7c91d368f082fc97bac32acab1fdbd5cfca` |
| Corrective handoff | `14d3f142d37970f52512015784228d5bbc5ccf1f` |
| Final tested revision | `e5711573839b185a2dfc7d9f2c64ce4fc18d4c13` (clean detached worktree `E:\HarborOps-Verification\worktrees\m16-final`; runner file verified identical to the committed file) |
| Frozen first-campaign worktree | `...\m16-source` at `ef08d7c`, preserved |
| Combined diagnostic | local-only `diag/m16-m9-combined-local`, `32fc9f2` (M16 `e571157` + M9 `7665bc4`), never pushed |

## 2. Corrections made in this assignment (pushed, `[skip ci]`)

`b9ada8f` populate M8 history before M16 in the downgrade guard test and add the audit cursor walk test; `ec0cc5e`, `ac9f977` HTTP authorization matrix and real PostgreSQL stop and restart stages; `49e3cb6` runtime-role, stale-context and metadata-allowlist tests, provisioner uses `current_database()`, fault-injection runner; `e571157` fault injection as a runner stage. The committed corrections `fb5c564` to `14d3f14` were audited independently (see `01-corrections-and-retest.md`).

## 3. Root causes and regression tests

F002 FK lock inversion (regression test and mutant `audit-fk-lock-inversion`); F003 offset paging (`AuditCursorTests`); F004 thin audit content (HTTP S01 check, allowlist and stale-context tests); F007 failure mapping and denial volume (HTTP S22, S21); F008 and F009 test and runner defects; F011 downgrade test ordering; F012 hard-coded database name in the provisioner. Details in `01-corrections-and-retest.md`.

## 4. Final campaign stages (UTC 2026-11-04, total 70 min 33 s)

preflight 1 s, docs 1 s, scripts 0 s, postgres image 3 s, build 50 s (0 warnings), unit 3 s, contract 10 s, integration 699 s, integration repeat 725 s, security mutations 1213 s, dependencies 65 s, web build 17 s, package audit 8 s, compose 25 s, restricted database roles 13 s, image scans 28 s, accounts 189 s, organizations 58 s, fixtures about 15 s, HTTP matrix 89 s, outage 27 s, browsers 981 s. All exit 0. Stage table: `evidence/stages.tsv` in the evidence root.

## 5. Results

| Suite | Result |
| --- | --- |
| Unit | 205 of 205 |
| Contract | 39 of 39 |
| Integration, run and repeat | 252 of 252 and 252 of 252 (11 m 33 s, 11 m 57 s), including the 12 original failures |
| Web unit | 18 passed; build passed |
| Fault injection | control clean; 13 of 13 mutants killed by their intended tests (missing capture, removed TRUNCATE and DELETE guards, swallowed audit failure, unredacted metadata, stale human context, FK lock inversion, widened permission, removed tenant predicate, cursor ignoring organization, API delete grant, public create grant, worker audit read) |
| HTTP authorization matrix | 74 of 74 |
| Real PostgreSQL stop and restart | 14 of 14 |
| Browsers | Chromium 61, Firefox 61, WebKit 61, 0 failed, 0 skipped, 0 flaky |
| Dependencies and images | no vulnerable NuGet packages; npm audit 0; Trivy HIGH and CRITICAL 0 for postgres, api, web |
| Supplement on the final revision | S07, S08, S28 operator identity: 6 of 6 |

## 6. Database and privilege evidence

Fresh and M8-to-M16 migrations pass; indexes are the primary key and two scope indexes; there is no foreign key from `audit_entries` to `organizations`. Under the real provisioned runtime roles every UPDATE, DELETE, TRUNCATE, DDL, trigger-disable, role-creation and replication-role statement fails with SQLSTATE 42501; the owner path is stopped by the guards (23514). The migration identity remains a PostgreSQL superuser (documented limit, M16-D05; a non-superuser owner is a decision, acceptance pending).

## 7. Audit integrity and privacy

Metadata keys are exactly `operation` and `changedFields` from a fixed allowlist; no credential, cookie, e-mail or payload text appears in rows, responses or evidence. Accepted tracking events are attributed to the integration source and ignore a stale human context. Operator work is `OperatorCli`.

## 8. Security scans and evidence handling

Evidence safety passed on 162 uncompressed files; no Playwright traces were produced; the quarantine block was exercised against synthetic traces and works. Raw evidence stays private on E:.

## 9. M9 compatibility (diagnostic, separate)

See `02-m9-compatibility-refresh.md`: combined build clean, 268 of 273 initially (5 hard-coded migration-count assertions), 273 of 273 after adjusting them. Worker grant contradiction resolved; provisioning duplication and audit coverage of worker effects need decisions; combined Compose, browser and outage runs were not executed.

## 10. Scenario results

`03-scenario-matrix.md`: 30 of 30 PASS for the standalone revision, S30 limited as above. No FAIL, BLOCKED or NOT EXECUTED row.

## 11. Missing evidence and unresolved items

Combined M9 Compose, browser and outage verification; runner coverage for S07, S08 and S28 (currently the supplemental probe); the metadata size CHECK is not exercised independently; a pre-correction database would need rebuilding because the migration was edited in place (acceptance pending); worker-effect audit coverage and a single worker provisioning path (decisions pending); statement-time ordering can place a late-committing row behind an issued cursor.

## 12. Evidence locations and hashes

Private: `E:\HarborOps-Verification\evidence\new-campaign\m16` (`evidence-manifest.sha256`, 162 files, SHA-256 of the manifest `a9a13a6abf4f6b05ca305affb020c8237f12f81ed65de7cda5337fab3fa72671`). Earlier attempts, probes, rehearsal and development outputs are preserved under `E:\HarborOps-Verification\evidence`, `g16-rehearsal` and `g16-repair`. Sanitized summaries and the supplemental probe are in this folder.

## 13. Commits and pushes

Implementation branch: `b9ada8f`, `ec0cc5e`, `ac9f977`, `49e3cb6`, `e571157` (pushed). Reporting branch `verify/g16-local-evidence`: `c8be39d` and the commit containing this report (pushed).

## 14. Cleanup

No containers, volumes or networks remain for `ho-verify-g16`, `ho-verify-dev16`, `ho-g16*`, `ho-dev16*` or `ho-probe*`. Docker images and E: caches were kept. Nothing project-specific was written to C:. C: free space fell from 4.7 GB to about 3.5 GB during the sessions; the WSL crash dumps there all predate the campaign, so the drop is unattributed and nothing was deleted.

## 15. Readiness recommendation

Ready for independent evidence review of `e571157`. Open decisions for the project: migration identity privilege, in-place edit of the unreleased migration, M9 provisioning and worker-audit policy. G16 acceptance pending.
