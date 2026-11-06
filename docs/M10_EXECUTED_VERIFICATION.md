# M10 Executed Verification Record (G10)

**State:** Technical verification complete on the frozen runtime source. **G10 acceptance pending.** Independent evidence review pending. No gate decision, merge authorization or deployment authorization is implied.
**Baseline:** `main` at `5edf0035a1fb9d854cba00673eb8fdf1f3033b0c` (merge of [PR #23](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/23), 2026-11-06), which contains accepted M0-M9 and M16/G16 plus the M10 source.
**Verification branch:** `feat/harborops-m10-g16-verify-f80276`.
**Frozen runtime source:** `3e81b884b6a4cc782f84c58510793666786c5796`. Later branch commits change only verification tooling, one added test and documentation; `git diff` of the frozen SHA against the branch head over `src`, `web`, `docker` and the Compose files is empty.
**Restriction preserved:** The M9 transport-backlog hold and the 10,000 unresolved-work cap are unchanged. M10 indexes only M9-resolved equipment events and does not process parked transport events. M0-M9 and M16/G16 accepted evidence is not rewritten.

Earlier source audits and remediation registers are historical and are not rewritten by this record.

## Evidence attempts

Evidence roots are E:-backed under `E:\HarborOps-Verification\evidence`. Raw logs, TRX files, browser JSON, mutation JSON, Trivy JSON and SHA-256 manifests are preserved; credentials are removed and Playwright traces are quarantined under each root's `private` folder.

| Attempt | Root | Scope | Outcome |
| --- | --- | --- | --- |
| A1 final-01 | `g10-final-01\g10` | Full campaign on the frozen SHA | 30 stages passed, then `browser-engines` failed (one WebKit test in an accepted M6 spec) and the runner stopped. `security-mutations` was not reached. Manifest 166 files. Preserved. |
| A2 interrupted supplement | `g10-final-01-supp-browser\g10` | Browser matrix | Interrupted by a host shutdown (Chromium complete, Firefox partial, WebKit not run). It also contains three Chromium failures caused by repeating state-mutating specs on one stack, a methodology error and not a product defect. Invalid as matrix evidence; preserved as partial raw evidence. |
| A3 browser supplement | `g10-final-01-supp-browser-b\g10` | Browser matrix, same SHA, exact matrix once | Exit 0. Chromium, Firefox and WebKit each 66 of 66 on 18 spec files; 0 unexpected, flaky or skipped. Manifest 135 files. |
| A4 mutation run | `g10-final-01-supp-mutations` | 29 variants on the frozen SHA | Control clean; 28 of 28 mutants killed; 0 survivors or invalid mutants. |
| A5 S17 supplement | `g10-final-01-assessment\s17-supplement` | One focused HTTP test, runtime source identical to the frozen SHA | 1 of 1 passed (commit `0578358`). |

Development rehearsals are not evidence for any scenario. The independent reparse (`scripts/summarize-g10-evidence.mjs`) re-reads ledgers, TRX, Playwright JSON, stack JSON, mutation JSON, Trivy JSON, performance records and manifests and reported all sections consistent. The assessment folder holds `G10_SCENARIO_DISPOSITIONS.md`, `g10-independent-summary.json` and `assessment-manifest.sha256`.

| Evidence set | Manifest SHA-256 |
| --- | --- |
| A1 | `f7a83454f45a360521072d40f13af5c0b3b82c924d0cb3c1ce396929a9e8639a` |
| A3 | `9f5d0efb61656b6cad0362940bcd4b80ea194a51c85ed794204ff6acbaac8b23` |
| A4 | `4cbd28a6e8041c0b0114efae8981f23e6a14db78adf4ccc12580e691f955ced6` |
| Reparse summary | `27c1a8086133691e6d2a9118d8895f009665a53c41241433a87536c2b6ff92d1` |
| S17 TRX | `d150a7ffa11fa39411e5f799bc7149ef52f59e684386669f9fa62b89b1c7a9e9` |

## Results on the frozen source (independently reparsed)

- Unit 239 of 239. Contract 39 of 39. Web 18 of 18 and production build.
- Integration 302 of 302, then 302 of 302 again on a fresh database (16 M10 rows each).
- M16 audit HTTP matrix 74 of 74; audit outage 14 of 14.
- G10 stack check 27 of 27 (seven M10 checks): twelve migrations in order, timeline index and revision from the real restricted worker, parked transport events, scoped reads, Manager-only rebuild and correction under the restricted API role, exact API and worker grants, least-privilege projector confined by real-login probes, read-route recovery after a database outage.
- Trivy HIGH or CRITICAL findings: 0 for the postgres, api, web and worker images. .NET and npm audits clean.
- Large history: 12,001 events, 121 pages, page p95 12.6 ms and maximum 18.7 ms, rebuild 760 ms, private memory delta 2 MiB, plan uses `ix_m10_timeline_keyset` without a sort.
- Browsers: A3 3 engines x 66 of 66 including five M10 journeys (real worker-resolved events in order, materialized state, stale and revoked recovery, organization switch). A1 Chromium 66 of 66, Firefox 66 of 66, WebKit 65 of 66.
- Mutations: 29 variants including the control; 28 killed. Families cover M10 API, worker and projector grants, definer triggers, timeline immutability and provenance checks, downgrade retention, keyset index, cursor digest, idempotency collision, rate limit, Manager-only on both layers, projector session guard and lock result, reducer rules, and the accepted M9/M16 surfaces M10 touches.

## Defects found and corrected during verification

| ID | Severity | Finding | Root cause | Correction |
| --- | --- | --- | --- | --- |
| D1 | Blocker | The solution did not compile (CS0246 in `Program.cs`, xUnit2031). | The M10 source was merged without ever being compiled. | Added the import and the `Assert.Single` overload (`82e75d0`). |
| D2 | High | The restricted `harborops_projector` role could not run: `DirtyAsync` reads `shipment_tracking_correction_heads`, which the provisioning script never granted, and the startup self-check did not test it. | Grant list and query list diverged; verification had only run the store as the migrator. | Added the grant, the startup privilege check and a contract guard, plus tests under the real role (`42c91ec`). |
| D3 | Low | `m10_lock_projector_scope` returned NULL for an unknown scope. | Three-valued logic in the final `RETURN`. | `COALESCE(..., false)` (`6a723b8`). |
| D4 | Medium (tooling) | The G10 runner used a host `dotnet` that is not .NET 10, omitted the worker, audit, role and M10 stack stages, and its mutation helper covered three reducer mutants. | Runner written without execution. | Rebuilt on the accepted combined stack with a containerized SDK, a derived stack check and a container-based mutation runner (`480a239`, `8bedb73`, `349a029`). |
| D5 | Low (tooling) | Stale documentation assertions in `check-planning-docs.mjs` and a pre-F001 anchor in the combined mutation runner. | Not refreshed after the G16 reconciliation. | Refreshed in the G10 tooling; the accepted combined runner is unchanged (`349a029`, `3c399e1`). |
| D6 | Low (tooling) | The stale-cursor and revocation browser journeys raced the panel's initial load. | The mock was armed before the mount request finished and was consumed by it. | The spec waits for the initial load first (`3e81b88`). |

## Residual risks and disclosed limits

1. **One intermittent WebKit failure (A1).** `assignment.spec.mjs` "G6 logout disposes allocation views and context projections" failed once in A1 and passed in A3. The preserved trace shows the click on "View shipment" racing the shipments panel's initial list requests in an M6 spec; the M10 UI addition is not on that path. One pass does not prove the cause, so it is carried as an intermittent residual risk. The accepted M6 spec was not edited after the source freeze.
2. **Populated upgrade method (S03).** The populated pre-M10 state is recreated by removing M10 objects from a populated head and re-running the real `Up`; it is not an upgrade of a database created by M9-era binaries.
3. **Not separately executed:** a DEPA transport fixture (same opaque parking path as ARRI), an archived shipment or suspended organization through the M10 routes, and a process-level kill of the API or projector container after commit. The idempotent retry and replay paths cover that last window.
4. **The 10,000 cap** is covered by the accepted M8/M9 quota tests and not re-measured at 10,000 here.

## G10-S01 to G10-S25 dispositions

| ID | Disposition | Basis |
| --- | --- | --- |
| S01 | **BLOCKED** | The M10 local policy decisions have no approval record. Acceptance pending. |
| S02 | PASS | 12 migrations, no model drift, ordered fresh-volume stack check. |
| S03 | PASS (method disclosed) | Populated backfill with unchanged M8/M9/M16 evidence fingerprints, immutable backfill, guarded downgrade and empty-history control. |
| S04 | PASS | Unit and database ordering tests; 12,001-event history with timestamp ties matches SQL order. |
| S05 | PASS (gap disclosed) | Reducer tests and a parked, unindexed transport event; DEPA not separately executed. |
| S06 | PASS | Same-container same-time contradiction yields Conflict; mutant killed. |
| S07 | PASS | Late deliveries and a late effect during rebuild. |
| S08 | PASS | Accepted M8/M9 suites inside both integration runs; one application per receipt and policy. |
| S09 | PASS | Accepted M9 reference tests; unresolved event parked with no effect or entry. |
| S10 | PASS | Transport event stays parked and unindexed; no cap or hold change. |
| S11 | PASS (gap disclosed) | Cross-tenant HTTP and SQL denials; archived shipment not separately executed. |
| S12 | PASS (gap disclosed) | Live downgrade, `logout-all`, removed member, browser revocation; suspended organization not separately executed. |
| S13 | PASS | Full 12,001-event keyset walk; tampered and stale cursors rejected. |
| S14 | PASS | No raw payload or input digest in responses; cursors encrypted and scope-bound. |
| S15 | PASS | Manual `WorkflowStatus` unchanged; UI separates observed tracking. |
| S16 | PASS | Null ETA, unmaterialized state reported stale, Unknown for empty input. |
| S17 | PASS | Manager-only, CSRF, idempotency, generation, chain and replacement rules, reason sanitization (A5), immutability. |
| S18 | PASS | Repeated rebuilds identical; 760 ms at 12,001 events. |
| S19 | PASS | Identical replay keeps the revision and adds no rows. |
| S20 | PASS | Real lock-wait overlap: late effect blocked behind a rebuild, retained, projection stale, then recovered; no deadlock. |
| S21 | PASS (variant disclosed) | Projector killed before COMMIT rolls back fully and retries idempotently; database outage; lost-response replay. |
| S22 | PASS (residual risk) | A3 3 x 66 of 66 with five M10 journeys; the A1 WebKit failure is preserved. |
| S23 | PASS | Real-login role matrices, direct SQL guards, 28 of 28 mutants killed. |
| S24 | PASS (technical) | Zero HIGH or CRITICAL scan findings, clean audits, M16 audit and outage probes, unchanged worker grants and transport hold. This record is the documentation update. |
| S25 | **NOT EXECUTED** | Independent evidence review and the explicit gate decision are pending. |

## Readiness and open items

The implementation is technically ready for independent G10 evidence review on the frozen source. This is not G10 acceptance, release readiness or deployment authorization. Open items: S01 policy approval, S25 independent review and explicit decision, and the residual risks above. Acceptance pending.

## Reproduction

- Full source-locked campaign: `scripts/verify-g10-local.sh` with `HARBOROPS_VERIFICATION_ROOT` set to a new E: path ending in `/g10`, `HARBOROPS_G10_PROJECT` set to a unique `ho-verify-cg16-<suffix>` and `HARBOROPS_DOCKER_VHDX` set to the E: Docker disk image. It runs the .NET 10 SDK in a container and layers the accepted combined Compose files.
- Mutations alone: `node scripts/run-g10-mutations.mjs <E: root>` with `HARBOROPS_SOURCE_SHA` and `HARBOROPS_G10_PG_IMAGE`.
- Independent reparse: `node scripts/summarize-g10-evidence.mjs <base root> <browser supplement root> <mutation root>`.
