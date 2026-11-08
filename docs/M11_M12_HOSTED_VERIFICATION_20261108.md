# M11/M12 Hosted Verification Record

**Current disposition (2026-11-08):** M11/G11 and M12/G12 accepted following separate S01 policy and S25 evidence decisions; the combined source merged into `main` through [PR #33](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/33). [Formal gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) and [project status](STATUS.md) supersede any earlier proposed, blocked or unmerged milestones in this dated record. The original evidence and earlier failed runs remain valid historical observations. M9 ARRI/DEPA hold remains binding; no deployment authorized.

## Latest verified checkpoint

| Check | Run | Source SHA | Result |
| --- | --- | --- | --- |
| Source contracts, documentation, Release build, 291 unit, 44 contract and 18 frontend tests | [42436750922](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42436750922) | `2a855ad65b073ac4fc1140920ea06a680ab15377` | PASS |
| G11 PostgreSQL, including signed tenant scope and forged SQL proof test | [42436750893](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42436750893) | `2a855ad65b073ac4fc1140920ea06a680ab15377` | 18/18 PASS |
| G12 exclusive PostgreSQL restricted-role export | [42436750893](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42436750893) | `2a855ad65b073ac4fc1140920ea06a680ab15377` | 2/2 PASS |
| Direct restricted SQL read isolation | [42436750993](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42436750993) | `2a855ad65b073ac4fc1140920ea06a680ab15377` | PASS: RLS enabled, synthetic row exists, direct unscoped SQL returns zero rows |
| Impacted migrations, M10 audit, exception and shipment-search regressions | [42436750923](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42436750923) | `2a855ad65b073ac4fc1140920ea06a680ab15377` | 36/36 PASS |
| Dependency and source filesystem scan | [42437063554](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42437063554) | `325b660f99a05145f7fdd436bf7ca96e95229a2f` | PASS at HIGH/CRITICAL threshold |
| Built PostgreSQL, API and web runtime-image scans | [42437063558](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42437063558) | `325b660f99a05145f7fdd436bf7ca96e95229a2f` | PASS at HIGH/CRITICAL threshold |
| Three-engine browser rerun after selector correction | [42436855857](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42436855857) | `0eaaa7034a231a87184129f303c6911ecbe13f62` | PASS: Chromium 12/12, Firefox 12/12 and WebKit 12/12; 36/36 total, no skips or failures |

The prior G11-S12 failure remains preserved at [42434283623](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42434283623). Its exploit was addressed by signed transaction-local proof scopes and PostgreSQL row-level security for the three M11 read tables. The new runtime test connects as the real restricted API database identity, checks zero unscoped rows, and checks a forged human read proof and a forged due-work proof. An earlier pre-remediation full integration attempt [42433586007](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433586007) failed 18/336 on an older SHA and is not reclassified. The final impacted regression run passed the selected 36/36 tests on the corrected SHA, but is not a full 336-test repeat.

**Open security/verification work:** G11-S08 still lacks complete conflict-specific M10 event/container/correction lineage. G11 and G12 still need explicit scenario-by-scenario raw evidence, effective security mutation checks and independent S25 review. M12-P01 through P16 and G12-S01 decisions remain pending. The M9 ARRI/DEPA hold, 10,000 unresolved-work cap and null ETA remain unchanged. Protected `main` and production systems have not been modified.


**Authorized scope:** Execute and correct hosted verification of the combined M11/M12 candidate. Gate acceptance, protected-main merge, production migration, deployment and M9 hold release are excluded.
**Baseline:** `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`.
**Candidate branch:** `feat/m11-m12-combined-integration`.
**Recorded on:** 2026-11-08.
**Evidence type:** GitHub Actions run logs and reported checks, not an independently sealed G11/G12 gate package.

## Source-locked run register

| Scope | Run | Tested source SHA | Verified result |
| --- | --- | --- | --- |
| Complete source gate | [42433695430](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433695430) | `45a8e957df21aa1e970a6bc6580e97c844e9167d` | PASS: source contract, graph/docs, Release build 0 warnings/errors, 291 .NET unit, 44 .NET contract and 18 web tests, frontend build and disposable Compose config |
| G11 targeted PostgreSQL runtime | [42433695510](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433695510) | `45a8e957df21aa1e970a6bc6580e97c844e9167d` | PASS: 17 of 17, including signed archived-shipment clearance correction |
| G12 exclusive PostgreSQL runtime | [42433695510](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433695510) | `45a8e957df21aa1e970a6bc6580e97c844e9167d` | PASS: 2 of 2, including executed `RestrictedRuntimeExportIsScopedAndAudited`; dedicated service and exclusive-server flag |
| Dependency advisories and source filesystem | [42434008577](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42434008577) | `4a7b3df1f9eab40dc1fa1781cf421cbf41a5b0f0` | PASS: .NET advisory query, production npm audits, Trivy filesystem HIGH/CRITICAL threshold |
| Built runtime image vulnerabilities | [42434194529](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42434194529) | `c2de9b915973a389dd75a2de4d55ab687c1bf15a` | PASS: PostgreSQL, API and web image scans at HIGH/CRITICAL threshold; revision labels checked |
| Direct M11 SQL tenant-read boundary | [42434283623](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42434283623) | `a6716a320a686e1b4ae805654a3ea602d08d91f9` | **FAIL, confirmed:** `harborops_api` can SELECT an exception without selected organization; `has_table_privilege=true`, `relrowsecurity=false`, synthetic row present |
| Initial targeted browser campaign | [42433917227](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433917227) | `d03198495733cc59ba67268b24ab64aa747b6c26` | **FAIL:** 10 pass, 20 fail, primarily later-engine login/organization load; rate limiter suspected, not independently proven |
| Browser isolation rerun | [42434504136](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42434504136) | `a3b8548fe87b978e9eb29ea6a477bd85d99ca382` | FAIL (historical): isolated rerun still had login/locator errors; a later corrected browser campaign is recorded above |
| Whole integration regression campaign | [42433586007](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433586007) | `0287c449101a93875a822906aa88f4907859e747` | FAIL (historical): 318 of 336 passed, 18 failed on superseded source; this cannot certify the corrected source |

The failed first G11 targeted run [42433307087](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433307087) passed 16 of 17 but exposed a 409 when archived shipments lacked current equipment projections. The revised SQL bypasses that projection requirement only for signed, ineligible-shipment `Inactive` clearance. The successful 17/17 rerun is a distinct later SHA. The first image-scan attempt [42434098494](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42434098494) stopped at image discovery before any vulnerability conclusion; the corrected attempt succeeded.

## Security finding requiring remediation

The direct SQL check used a real disposable PostgreSQL service, seeded an exception through the API, and confirmed the role-based select privilege with row-level security disabled. Changing only an HTTP tenant guard does not close this boundary. G11-S12 **failed on the historical unprotected source**. The latest corrected-source verification is recorded above. The broad inherited API SELECT permissions require a least-privilege design and tested enforcement without breaking accepted M2/M5/M10/M16 behavior. A successful G11 functional test suite does not override this result.

Conflict-specific M10 event/container/correction provenance is not yet complete. Existing summary digests are not full attribution. G11-S08 therefore requires additional source remediation and executed negative evidence.

## Evidence and acceptance limits

The source and limited runtime workflows are not a 25-scenario G11 or G12 matrix. G11-S01 policy choices are recorded in [M11 local contract](M11_LOCAL_CONTRACT_DECISION_20261108.md), but G11-S25 independent review is not complete. M12-P01 through P16 and G12-S01 remain pending; G12-S25 independent review is not complete. No unexecuted scenario is labeled PASS merely because relevant code, tests or other gate results exist.

The currently working hosted path executes shell commands directly. Reusable `actions/*` steps failed during startup on this repository, so the workflows have used GitHub-hosted job logs and SHA-256 summaries rather than claiming complete uploaded raw TRX, browser trace and scan archives. The raw-evidence and source-lock requirements in the [combined integration plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md) remain mandatory for independent acceptance. Historical failures must not be removed or reclassified as passes.

The original M11 and M12 feature branches remain preserved. Protected `main`, production systems and the M9 transport hold are unchanged. ARRI/DEPA remain parked, the 10,000 unresolved-work cap stays binding, and M10 ETA remains null.

**Historical hosted checkpoint disposition:** This earlier run established partial technical coverage. Its G11-S08, S25 and policy limitations were subsequently addressed by the separate local closure campaign and accepted in the [formal G11/G12 gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md). Neither gate is accepted.
