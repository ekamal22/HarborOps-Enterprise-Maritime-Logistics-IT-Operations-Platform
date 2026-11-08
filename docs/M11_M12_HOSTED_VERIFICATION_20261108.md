# M11/M12 Hosted Verification Record

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
| Browser isolation rerun | [42434504136](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42434504136) | `a3b8548fe87b978e9eb29ea6a477bd85d99ca382` | RUNNING at this checkpoint; per-spec API restart and independent Chromium/Firefox/WebKit result JSON |
| Whole integration regression campaign | [42433586007](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433586007) | `0287c449101a93875a822906aa88f4907859e747` | RUNNING at this checkpoint; this is before archived-clearance SQL remediation, so it cannot certify final corrected source |

The failed first G11 targeted run [42433307087](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42433307087) passed 16 of 17 but exposed a 409 when archived shipments lacked current equipment projections. The revised SQL bypasses that projection requirement only for signed, ineligible-shipment `Inactive` clearance. The successful 17/17 rerun is a distinct later SHA. The first image-scan attempt [42434098494](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42434098494) stopped at image discovery before any vulnerability conclusion; the corrected attempt succeeded.

## Security finding requiring remediation

The direct SQL check used a real disposable PostgreSQL service, seeded an exception through the API, and confirmed the role-based select privilege with row-level security disabled. Changing only an HTTP tenant guard does not close this boundary. G11-S12 is **FAIL** on the tested source. The broad inherited API SELECT permissions require a least-privilege design and tested enforcement without breaking accepted M2/M5/M10/M16 behavior. A successful G11 functional test suite does not override this result.

Conflict-specific M10 event/container/correction provenance is not yet complete. Existing summary digests are not full attribution. G11-S08 therefore requires additional source remediation and executed negative evidence.

## Evidence and acceptance limits

The source and limited runtime workflows are not a 25-scenario G11 or G12 matrix. G11-S01 policy choices are recorded in [M11 local contract](M11_LOCAL_CONTRACT_DECISION_20261108.md), but G11-S25 independent review is not complete. M12-P01 through P16 and G12-S01 remain pending; G12-S25 independent review is not complete. No unexecuted scenario is labeled PASS merely because relevant code, tests or other gate results exist.

The currently working hosted path executes shell commands directly. Reusable `actions/*` steps failed during startup on this repository, so the workflows have used GitHub-hosted job logs and SHA-256 summaries rather than claiming complete uploaded raw TRX, browser trace and scan archives. The raw-evidence and source-lock requirements in the [combined integration plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md) remain mandatory for independent acceptance. Historical failures must not be removed or reclassified as passes.

The original M11 and M12 feature branches remain preserved. Protected `main`, production systems and the M9 transport hold are unchanged. ARRI/DEPA remain parked, the 10,000 unresolved-work cap stays binding, and M10 ETA remains null.

**Disposition:** Corrected source and targeted G11/G12 technical checks passed, but **G11 is not ready for acceptance**, and **G12 full gate acceptance is pending**.
