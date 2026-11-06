# M9/M16 Combined Integration: Executed Technical Verification

**Recorded:** 2026-11-06
**Integration branch:** `integrate/m9-m16-g16-candidate-20261105` (draft [PR #25](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/25)), never merged into `main`
**Initial integration commit:** `fbb5b1457cd36f21b441bf1d5bc399fe6083455e` (static checks only)
**Accepted baseline:** `main` at `b4f7b93f1ee8135b9a16b605ded315b3fbc036cc`, unchanged during this work
**Frozen runtime source for the campaign:** `79318c6b6126362a0b26a1452aad6d2b48fdf4b4`
**State:** Technical campaign passed on the frozen source through a failed run plus a same-source supplement (see Runs). G16 acceptance, merge and deployment are not authorized and remain acceptance pending.

## Runs and outcomes

| Run | Source | Result |
| --- | --- | --- |
| a01 | `fbb5b14` | Clean warning-as-error build, 228 unit, 39 contract, 18 web tests plus production build, docs checks, focused integration 60/60. Found no defect itself. |
| Phase B probes | `fbb5b14` | SQL probes found F001 (below). First run of the correction failed (42P01, unqualified relation under a SECURITY DEFINER search path), preserved, repaired in the same commit family. |
| c01, c02 | accepted `main`, then `e146b35` | Accepted M9 stack populated (14/14 accepted stack checks), upgraded in place. The before and after grant diff found F002. |
| final-01 | `1bade42` | Integration 286/286 twice, then image scans failed: 2 HIGH findings (F003). Preserved. |
| final-02 | `79318c6` | Everything through the combined stack check passed. The browser stage failed on Firefox (maritime, organizations, shipment). Preserved. |
| final-03 | `79318c6` | Same-source supplement: Compose, probes, combined stack check, full three-browser regression and mutations all passed. The .NET stages are recorded DEV-SKIPPED here and are covered by final-02. |
| c03 | `79318c6` | Final accepted-M9 populated upgrade with 12 post-upgrade behavior checks, all passed. |

A single uninterrupted green run on one source was not achieved. The campaign pair final-02 plus final-03 on the identical source is the evidence. The independent script reparse of both roots reports every section consistent.

## Observed totals (re-parsed from raw TRX and JSON, not from runner summaries)

- .NET on `79318c6`: unit 228/228, contract 39/39, integration 286/286 and a repeat 286/286 on a brand new database container.
- Web: 18/18 unit tests and a production build. Dependency scan: no vulnerable packages in eight projects. `npm audit --audit-level=high` clean for `web` and `tests/browser`.
- M16 audit HTTP matrix 74/74 and PostgreSQL outage and recovery 14/14, in both final-02 and final-03.
- Combined stack check 20/20 in both runs (accepted M9 stack checks adapted to 11 migrations, plus exact grant sets, real-login DDL and audit denials, API redrive confinement, audit and tracking-work coexistence, attributed Manager redrive, tenant isolation and live revocation).
- Browsers in final-03: Chromium, Firefox and WebKit each 61/61 across 17 specs (the M0 to M9 journeys and the M16 audit spec). In final-02 Chromium and WebKit passed 61/61 each and Firefox had 4 unexpected results in 3 specs.
- Mutations (final-03): clean control plus 18/18 targeted mutants killed by their intended tests.
- Image scans (Trivy 0.76.1) for postgres, api, web and worker: 0 HIGH or CRITICAL in final-02 and final-03.
- Final upgrade c03: 12/12 checks. The first upgrade c02 ran on an earlier source.

## Corrective changes on PR #25

| Commit | Path | Change |
| --- | --- | --- |
| `84f5139` | `src/HarborOps.Api/Audit/AuditRoleProvisioner.cs`, `tests/HarborOps.IntegrationTests/CombinedRestrictedRoleTests.cs` | F001: confine the restricted API role to the Manager redrive transition and the attributed Redriven attempt row. Add real restricted-role integration tests. |
| `c7aab6a` | `AuditRoleProvisioner.cs`, `CombinedRestrictedRoleTests.cs` | F002: remove worker reads of `integration_sources` and `integration_credentials`. Assert the exact worker grant and column set. |
| `e146b35` | `compose.cg16.override.yaml` | Private PostgreSQL image tag overlay so builds never retag an image another stream uses. |
| `f277a3c`, `75fab19`, `7d0a081` | `scripts/verify-combined-local.sh`, `check-combined-stack.mjs`, `run-combined-browsers-local.sh`, `run-combined-mutations.mjs`, `summarize-combined-evidence.mjs`, `check-m16-db-roles.sh` | Combined runner, stack check, browser runner, mutation harness, independent summarizer. The M16 role probe gains an optional project name; its default is unchanged. |
| `8fde0f9`, `bcd538e`, `ea62f6d` | `tests/HarborOps.IntegrationTests/CombinedRetentionTests.cs`, `CombinedRestrictedRoleTests.cs`, mutation harness | Close a downgrade-retention test gap and add a cross-tenant queue assertion found by mutant calibration. |
| `4c74a60`, `729a0d9`, `1bade42` | `scripts/verify-combined-local.sh`, `run-combined-browsers-local.sh` | Docker CLI plugin config for the redirected HOME, API restart before the login-heavy stack check, development rehearsal filters. |
| `79318c6` | `src/HarborOps.Api/Dockerfile`, `src/HarborOps.Worker/Dockerfile` | F003: upgrade only `apt` and `libapt-pkg6.0t64` in the final image stage. |

Original standalone M16 and accepted M9 evidence and tooling were not edited. `scripts/check-m9-stack.mjs` is accepted M9 tooling and was copied, not changed.

## Findings

- **F001, API role over-privilege (fixed).** Under the provisioned `harborops_api` role, a SQL probe could move a Pending work item to Leased and insert a forged `Processed` attempt row. The accepted M9 stack ran the API as the owner role, so this was narrower than baseline but wider than the redrive requirement. Role-scoped guard triggers installed by the single migration-time provisioner now limit that identity to the redrive transition and an attributed Redriven row for a current Manager, with schema-qualified dynamic SQL because the guard also runs nested in the SECURITY DEFINER worker trigger. The migration head stays 11.
- **F002, worker role widening (fixed).** The provisioner granted the worker SELECT on source and credential tables, including `secret_digest`. The accepted M9 worker does not hold it and the accepted stack check asserts the denial.
- **F003, base image advisory (fixed).** CVE-2026-109518 in `apt` 2.8.3 of the Ubuntu layer, fixed in 2.8.4ubuntu0.1. The newest upstream .NET digests still shipped 2.8.3. This is environmental, not a source defect.
- **Test gap (fixed).** The existing M9 downgrade test also passed with the retention guard weakened, because re-adding the M8 constraint failed with the same SQLSTATE. A redriven Pending item with retained attempt history now proves the guard on its own.
- **Firefox teardown failure (open observation).** final-02 failed with `Browser.removeBrowserContext ... _maybeDontRestoreTabs` and one timeout after a Firefox graphics error. The same signature appeared in the G8 campaign. The failed artifacts are preserved and final-03 passed all 183 browser executions on the same source. Treated as an environment fault, not proven.

## Phase C: populated accepted-M9 upgrade (c03)

An accepted `main` stack (`compose.g9.verify.yaml` at `b4f7b93`, with the worker role from `scripts/m9-worker-role.sql`) was populated by the accepted stack check: 4 receipts, 17 immutable attempt rows, 3 processing effects, one item parked at redrive generation 3. The same database was then upgraded by the combined migrator. Results: all 20 table content hashes identical, no index, constraint, function or trigger removed or altered, only `20261104180000_AuditEntries` added out of order after `TrackingWorker`, `audit_entries` empty (no fabricated history), worker table grants exactly the accepted M9 set, `harborops_api` created without privileged flags, a second and a third migrator run applied nothing and left grants unchanged, the restricted worker did not reprocess anything, a new intake was accepted by the restricted API, parked by the restricted worker and redriven by a Manager with an attributed row, the generation ceiling and Processed terminal state still returned 409, audit rows afterwards were all post-upgrade and attributed, and a Manager could not read audit entries.

Fresh combined installation (11 migrations) and the historical M8 upgrade paths are exercised by the integration suite (for example `AuditMigrationTests`, `TrackingWorkerMigrationTests`, `TrackingInboxMigrationTests`, `CombinedRetentionTests`), which passed twice.

## Pending decisions, all acceptance pending

1. **Worker-effect audit scope.** Option A: the immutable M9 attempt and effect tables are the audit of record, with no extra M16 rows. Option B: add M16 capture for Redriven and effect creation. Verified today: zero M16 rows reference M9 tables. Recommendation: A, revisit if M12 or M20 needs one unified query surface.
2. **Role provisioning authority.** Option A: the migration-time provisioner is authoritative and the M9 manual script is retired for the combined stack. Option B: keep both. The provisioner demonstrably converges a database built with the manual script. Recommendation: A.
3. **Privileged migration identity.** Option A: retain the owner migrator with the documented limitation. Option B: a restricted migration identity through a separate design. Runtime roles cannot run DDL or disable guards. Recommendation: A for the local-first scope.
4. **Pre-correction M16 databases.** None found in accessible retained databases (the development stack is at the M8 schema). The retained volume `ho-verify-oldcopy` was not opened or modified. Recommendation: no action unless one is found, then rebuild from a verified copy, never edit in place.
5. **Audit cursor late-commit ordering.** Unchanged by this work. Recommendation: accept the documented caveat for now.
6. **Supplemental evidence attestation.** The combined campaign gives new evidence for the S07, S08 and S28 behaviors tied to `79318c6`. Recommendation: keep the standalone supplement as a historical record with its limitation.
7. **F001 mechanism.** Option A (implemented): provisioner-installed guard triggers, head stays 11. Option B: a twelfth migration with a SECURITY DEFINER redrive function and no direct API table grants. Recommendation: A now, B if the API role is ever shared more widely.
8. **F003 interim.** The apt upgrade lines should be removed once upstream base images carry 2.8.4ubuntu0.1 or later.

## Limitations and notes

- Accepted M9 restrictions remain: no public deployment, no sustained transport-event processing, the 10,000 unresolved item limit.
- During final-01 the orchestrating session ended and the finished run sat unattended for about six hours before it was noticed. This is recorded as a supervision lapse; the run itself was not affected.
- Raw per-run test passwords briefly appeared in one Firefox failure artifact of final-02 and in two `docker compose config` dumps of the upgrade runs. Those stacks were destroyed. The files were redacted and the raw copies are kept privately, outside every archive (`REDACTION-NOTE.md` files in the archive).
- Mutation scope is targeted at the integration boundaries and did not rerun the accepted G9 and standalone G16 mutation families.
- No separate independent reviewer participated. The summary was recomputed by script from the raw evidence.

## Evidence

Private location: `E:\HarborOps-Verification\evidence\combined-g16`. Portable archive: `E:\HarborOps-Verification\evidence\combined-g16\archive\cg16-combined-evidence-79318c6.tar.gz`, SHA-256 `ffa263f7f5abb3c27a0907d3704b902fd8b0812b237920079b6c92c4461dd74b`. The archive holds 544 files listed in its `SHA256SUMS.txt`, covers every run above including the failed ones, and excludes credentials, caches and development rehearsals.

## Recommendation

- **G16 technical acceptance readiness:** technically ready for acceptance consideration on `79318c6`, with the caveat that the campaign is a failed run plus a same-source supplement and that decisions 1 to 3 and 7 are still open.
- **Merge readiness:** not ready. PR #25 stays a draft. Merge needs G16 acceptance, the decisions above, a review of the corrective commits and a separate merge authorization.

## Subsequent gate disposition (2026-11-06)

The frozen technical evidence, including its failed and successful source-matched runs, was independently inspected from the uploaded archive. All 544 submitted manifest entries matched. G16 and the four recommended Option A integration decisions were explicitly accepted on 2026-11-06. This later decision does not change any original run result, the reported Firefox failure or the fact that no single uninterrupted all-green campaign occurred. See the separate [G16 gate closure](M16_G16_GATE_CLOSURE.md). PR #25 merge and production deployment are not authorized by the gate decision.

## Subsequent F001 extension and integration disposition (2026-11-06)

The independent final diff review identified an F001 generation-zero forged-redrive case. Corrected source `8ba7727` subsequently passed 17/17 focused and 286/286 full PostgreSQL integration tests, and an intentional previous-guard mutation failed the new regression. The independently inspected extension archive contained 47 matching SHA-256 manifest entries and matched the supplied full-archive checksum `cf53e0329a8ec49669fe93f951e60e2575a159dc88a510d828ccc0397d2a71fe`. These results supplement, not overwrite, the earlier technical campaign and its failed-run evidence.

G16 acceptance and four Option A decisions were followed by separate conditional authorization and completion of PR #25 integration at `47ba55e2c88556bc5ca0da7d50335ffc8c5ca56d`. Its merge tree matches reviewed PR head `e7d63212`. The pre-acceptance recommendations above remain a historical record, not the current merge status. See [G16 gate closure](M16_G16_GATE_CLOSURE.md).
