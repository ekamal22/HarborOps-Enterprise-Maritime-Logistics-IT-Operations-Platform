# M16 / G16 independent local verification: final report

**Technical result: FAILED.** One High and several Medium defects were found; 24 of 30 scenarios are supported by evidence, 5 failed and 1 was not executed. Independent evidence review and G16 acceptance pending. No implementation file, test, script, migration or configuration was modified. No branch was merged. GitHub Actions remained disabled and untouched.

## 1. Source verified

| Item | Value |
| --- | --- |
| Branch and revision | `feat/m16-audit-access-hardening` at `ef08d7c91d368f082fc97bac32acab1fdbd5cfca` (remote HEAD matched the recorded revision; no drift during the campaign) |
| Source worktree | `E:\HarborOps-Verification\worktrees\m16-source`, detached, clean at every check (ignored build outputs only) |
| Reporting branch | `verify/g16-local-evidence` (based on `main` `7b9e19d`) |
| Base of M16 | Approved documentation `6c14c78`, before the M8 merge into `main`; no combined revision with `main` or M9 was created |

## 2. Executive summary

1. **G16-F002 (High):** the audit table's foreign key to `organizations` makes every audited write to an organization-scoped table take `FOR KEY SHARE` on the organization row, inverting the accepted lock order. 12 accepted M2 to M8 lock-race and queued-revocation tests now fail deterministically (deadlock 40P01 and 32 s timeouts). Confirmed by a direct SQL experiment. Fail-closed, no bypass seen, but revocation and tracking intake can stall or deadlock.
2. Database privilege design is sound for the runtime roles: independent probes show API and worker cannot UPDATE, DELETE, TRUNCATE or INSERT audit rows, run DDL, or disable guards. The migration login is a PostgreSQL superuser (F006), consistent with the documented boundary.
3. Authorization behavior of the read API is correct in every case probed (Auditor-only, selected-org scope, multi-org, foreign ids indistinguishable from unknown ids, immediate denial after revoke, suspend, disable and session revocation).
4. Audit content is thinner than the privacy policy (F004: table and operation only, no actor role, no changed fields, no integration-source actors); pagination duplicates rows because every read appends a row (F003).
5. The shipped runner could not have closed G16: it stops at a stale generated document (F001), its M16 tests do not exercise the restricted roles (F005), and two defects in the browser suite and runner fixtures (F008, F009) fail three browser specs on every engine. With those two test defects corrected in scratch copies, all three pass on all three engines.
6. M9 compatibility (read only): two definite contract conflicts (worker INSERT on M9's attempt log; hard-coded migration count) plus textual conflicts in six shared files (`02-m9-compatibility.md`).

## 3. Environment and tooling

Windows 11, Git Bash. Docker Desktop 4.85.0, Engine 29.6.2 (Linux), Compose v5.3.1, Git 2.39.1, Node v24.20.0, npm 11.19.0. .NET 10 SDK and PostgreSQL 16.15 (rootless image built from `docker/postgres/Dockerfile`) run in containers. Trivy 0.76.1. Everything stored on E:; C: free space was 4.7 GB before and 4.68 GB after (no attributable usage). No host .NET SDK.

## 4. Execution timeline (UTC, 2026-11-04)

| Attempt | Time | Runner | Result |
| --- | --- | --- | --- |
| 01 | 18:38:37 to 18:38:43 | Original, unmodified | `preflight` pass, `docs` fail (F001); stopped |
| 02 | 18:39:43 to 18:58:03 | Derived (docs stage tolerated, `cd` pinned) | build, unit, contract pass; integration 235 of 247 (F002); stopped |
| 03 | 18:58:52 to 19:31:51 | Derived (unit, contract, integration skipped: already evidenced on this SHA; failing classes rerun) | dependencies, web, audits, compose, role check, image scans, accounts, fixtures pass; subset rerun 41 of 53; browsers 58 of 61 per engine |
| 04 | 19:34:46 to 19:38:41 | Derived diagnostic rehearsal, not gate evidence | corrected fixtures and spec copy: audit and organization-revocation pass on 3 engines |
| Probes | During and after attempt 03 (file timestamps in `probes/`) | Independent SQL and HTTP probes on disposable containers | See `probes/` |

Derived runner scripts and the untouched original copy: `E:\HarborOps-Verification\evidence\attempt-0N\`. Stage tables: `stage-results/`. The `technical-exit-code.txt` of attempt 04 is 0 only because the derived runner tolerated the `docs` failure and ran two specs; it is not an indication of G16 status.

## 5. Test totals

| Suite | Result | Source |
| --- | --- | --- |
| Unit | 205 passed, 0 failed | A2 `unit.log`, TRX |
| Contract | 39 passed, 0 failed | A2 `contract.log` |
| Integration (full, runner) | 235 passed, 12 failed of 247, 16 m 37 s | A2 `integration.log`, TRX |
| Integration, affected classes rerun | 41 passed, 12 failed of 53 (same 12) | A3 `integration-failed-subset.log` |
| Integration repeat | Not run (runner stopped; failures deterministic) | |
| Web unit and build | 18 tests passed; build passed | A3 `web-build.log` |
| NuGet vulnerable-package scan | None in any project | A3 `dependencies.log` |
| npm audit (web, browser tests) | 0 vulnerabilities each | A3 `package-audit.log` |
| Image scans (Trivy, HIGH and CRITICAL) | postgres 0, api 0, web 0 | A3 `trivy/*.json` |
| Compose smoke (migrator, API as `harborops_api`, web) | Passed | A3 `compose.log` |
| Browser, Chromium | 58 of 61 passed; failures: audit x2, organization-revocation | A3 `playwright/chromium-*.json` |
| Browser, Firefox | 58 of 61 passed; same failures | A3 |
| Browser, WebKit | 58 of 61 passed; same failures | A3 |
| Browser rehearsal (non-gate) | audit 3 of 3 and organization-revocation 1 of 1 on each engine | A4 |
| Independent HTTP probe | 67 PASS, 2 informational FAIL (F003, F004) | `probes/http-probe.log` |
| Independent atomicity probe | 5 PASS | `probes/atomic-probe.log` |

## 6. Migration and EF consistency

Fresh migration succeeded in compose and in two fresh disposable databases. `MigrationSnapshotTests` (migration order, snapshot equals model), `AuditMigrationTests.AcceptedM8Upgrade...`, and the M8, M3 and M2 upgrade tests passed. The audit migration's `Down` refuses to run when audit rows exist. Index set: primary key plus `IX_m16_audit_scope_time` and `IX_m16_audit_resource`.

## 7. Audit integrity and authorization

Append-only: UPDATE, DELETE and TRUNCATE are rejected by guards (SQLSTATE 23514) even for the owner and by privileges (42501) for runtime roles. Atomicity: audit rows exist exactly for committed changes under 80 contended updates and an API kill mid-burst; a forced audit failure rolled back the business update. Authorization and tenant isolation: see matrix rows S13 to S18, S25, S27. Defects: F002, F003, F004, F007.

## 8. Database privilege assessment (M16-D04, D05)

| Role | Result |
| --- | --- |
| `harborops_api` | No superuser flags; SELECT only on `audit_entries`; INSERT and UPDATE on current mutable tables; no DELETE, TRUNCATE, REFERENCES or TRIGGER; cannot create objects in `public`, create roles or schemas; EXECUTE on `m16_record_audit_access` only; can read `user_credentials` (pre-existing) |
| `harborops_worker` | No flags; SELECT on nine tables only; nothing on `audit_entries`; no EXECUTE on the audit function |
| `harborops` (migrator and owner) | Superuser with all flags (F006); owns `audit_entries` and the SECURITY DEFINER functions; outside the database-only tamper boundary per M16-D05 |

`m16_audit_capture` and `m16_record_audit_access` are SECURITY DEFINER with a fixed `search_path` and schema-qualified references; no search-path or temp-table shadowing path was found.

## 9. M9 compatibility

See `02-m9-compatibility.md`. Definite: textual conflicts, worker INSERT grant contradicts M9's role script, hard-coded migration count 10 in both branches (combined head is 11). Probable: dual provisioning paths and the F002 lock edge reaching M9 writers. Needs combined-revision verification: snapshot parity, API grants for M9 tables, audit coverage of worker effects. No combined revision was created (not authorized).

## 10. Findings by severity

| ID | Severity | Title |
| --- | --- | --- |
| G16-F002 | High | Audit FK to organizations inverts lock order; 12 regression tests fail |
| G16-F001 | Medium | Generated execution graph stale; runner stops at stage 2 |
| G16-F003 | Medium | Offset pagination duplicates rows |
| G16-F004 | Medium | Audit entries lack taxonomy, actor role and changed fields |
| G16-F005 | Medium | M16 tests do not prove restricted-role guarantees |
| G16-F008 | Medium | Audit browser spec races organization selection (test defect) |
| G16-F009 | Medium | Runner provisions wrong browser member names (runner defect) |
| G16-F006 | Low | Migration identity is a superuser |
| G16-F007 | Low | Storage failure mapped to a revocation code; denial rows bounded only by limiter |
| G16-F010 | Low | Runner evidence controls: false alarms and label contamination |

Details, reproduction, root cause and retest requirements: `01-findings.md`.

## 11. Missing or incomplete evidence

- S20: CSRF, Origin spoofing and forwarded-transport probes on the audit routes were not executed.
- Integration repeat run (runner stage) was not executed; the 12 failures were reproduced once instead.
- Integrated M9 verification not executed (no combined revision).
- Browser traces and screenshots from attempt 03 are quarantined; the safety scan cannot certify compressed traces.
- Full runner pass on one source revision has not occurred (attempts 02 to 04 are derived). A clean run requires a corrected revision.
- Database outage was a process pause; a stop and restart with persistent volume and an explicit 503 observation were not captured.
- Scenarios passed through probes (S07 to S11, S13 to S18, S21 to S25, S27, S29) need runner-integrated or committed tests before gate acceptance.

## 12. Evidence locations and integrity

Private raw evidence (never committed): `E:\HarborOps-Verification\evidence\` (`m16`, `attempt-02`, `attempt-03`, `attempt-04`, `probes`). Per-attempt SHA-256 manifests: `<attempt root>\m16\evidence-manifest.sha256`; SHA-256 of the manifest files: attempt 01 `77ba7ede034108095f670b84d5b1ee1457f305dc7656cef0fab33e04d7f49eda` (8 files), attempt 02 `e9534acb5290f940cf6eb0f705409bc758450451b6a36ba903f515253a0e2663` (19), attempt 03 `f88146e87007713d61b295d2ffc39efde02e3fdbfcf76a85948185b6c75cd873` (169), attempt 04 `eba0ae2f154155a811e0aa704ebdfa958e84c91b0f2379a95fe4a1ea2e6a7f9d` (30). Sanitized probe outputs with a hash list are in `probes/SHA256SUMS.txt`. The checkpoint file is `E:\HarborOps-Verification\evidence\m16-STATUS.md`.

## 13. Cleanup record

Compose project `ho-verify-g16` (containers, volumes, network) and the SDK database were removed by each runner's teardown (`compose-teardown.log`, `postgres-teardown.log`). Probe containers `ho-probe-pg` and `ho-probe-api` and network `ho-probe-net` were removed by this campaign (`probes` teardown record); a final query shows zero resources with the G16 project label, probe label or network names. Built images (`ho-verify-g16-*`) and E: caches were left in place. No file on C: was created for the project. The unrelated `harborops-*` stack and other containers were not touched.

## 14. Readiness recommendation

**Not ready for G16 acceptance.** Required before a new campaign: correct F002 (and reconcile the lock-race suites), regenerate the graph (F001), fix pagination (F003) and audit content (F004), correct the browser spec and runner fixtures (F008, F009), add committed tests for the runtime-role and HTTP scenarios (F005), then run the unmodified runner once on the corrected revision and perform the combined M9 verification only after authorization.

## 15. Status

Open blockers: F002 and the items in section 11. Acceptance status: **G16 acceptance pending.** Technical verification did not complete successfully.
