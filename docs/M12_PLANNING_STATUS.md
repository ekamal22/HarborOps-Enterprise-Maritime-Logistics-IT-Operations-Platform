# M12 Local Contract Register

**Final M11/M12 technical verification (2026-11-08):** [Run reconciliation](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) confirms **341/341 integration PASS** on `f56d017` with no skips, plus 40/40 impacted, G11 19/19, G12 4/4, three-engine browser 36/36, effective mutants 5/5, SQL read isolation and security scans. [G11/G12 acceptance sufficiency review](M11_M12_ACCEPTANCE_REVIEW_20261108.md), [G11 matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12 matrix](M12_G12_EVIDENCE_MATRIX_20261108.md) record exact remaining evidence gaps. Independent S25 reviews and acceptance pending. Older 340-case and 'in progress' notes below describe historical checkpoints, not current execution.

**Executed verification reconciliation (2026-11-08):** [M11/M12 source-locked evidence](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) establishes 340/340 earlier full integration, 40/40 updated impacted regressions including populated 13-to-15 migration, G11 19/19, G12 4/4, 36/36 browser tests, 5/5 killed mutants, restricted SQL isolation and security scans on individually identified commits. The updated source gate passed 291/291 unit and 46/46 contract tests. Final full rerun `42443065884` is still in progress. Gate matrices, independent S25 reviews, and acceptance remain pending. Historical source-only statements below are not current test results.

**Current verification policy (2026-11-08):** [M11/M12 verification operations](M11_M12_VERIFICATION_OPERATIONS_20261108.md) now requires manual, coordinated, source-locked workflows instead of automatic push-triggered campaigns. [Run-by-run progress](M11_M12_VERIFICATION_PROGRESS_20261108.md) is the current verification checkpoint; older reports and source-only notes below are historical. G11/G12 independent S25 review and acceptance remain pending. Protected `main`, the M9 transport hold, and deployment restrictions are unchanged.

**Verification reconciliation, 2026-11-08:** The [source-locked M11/M12 evidence register](M11_M12_VERIFICATION_PROGRESS_20261108.md) records 338/338 passing full integration tests on the previously tested source, 291/291 unit, 46/46 contract, 19/19 G11, 4/4 G12, 36/36 three-engine browsers, five effective mutants killed and runtime-image vulnerability checks. A newer expanded full integration run and populated 13-to-15 upgrade test were still in progress at this checkpoint. Older 18/18, 36/36 and source-only statements below are historical. [M12-P01..P16 local contract](M12_LOCAL_CONTRACT_DECISION_20261108.md) is selected for verification, not independent gate acceptance; [Q2 applicability](M12_Q2_HISTORICAL_SNAPSHOT_APPLICABILITY_20261108.md) is recorded separately. Independent G11/G12 evidence reviews and gate acceptance pending, with protected `main` and the M9 hold unchanged.

**Current local policy decision (2026-11-08):** [M12 local search and export contract](M12_LOCAL_CONTRACT_DECISION_20261108.md) records M12-P01..P16 selected for implementation and verification, preserving restricted Analyst/Manager export and the existing caps. Final G12-S01 evidence reconciliation and independent gate acceptance remain pending. Older pending-policy descriptions below document the pre-decision state.

**Historical hosted verification checkpoint:** [Executed M11/M12 run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records a successful .NET and frontend source gate, G11 18/18 scoped PostgreSQL tests, G12 2/2 exclusive PostgreSQL tests, 36/36 impacted integration regressions, and direct SQL isolation of exception, history and evaluation tables with forged-scope denial. Updated source/dependency and runtime-image security scans passed. The three-engine browser rerun passed 36/36 tests, with 12/12 each in Chromium, Firefox and WebKit, including M11 exception journeys, M12 search/export and targeted M5 regressions. G11-S08 conflict-specific provenance remains open; M12 local policy decisions and independent G11/G12 S25 reviews remain pending. No G11/G12 acceptance, merge or deployment is authorized. Historical source-only notes below are retained as previous checkpoints.

**Historical combined-source checkpoint (before hosted verification):** The isolated `feat/m11-m12-combined-integration` candidate now contains both M11 and M12 implementations with reconciled shared API, React, SQL role, Compose and EF migration surfaces. [M11 policy decisions](M11_LOCAL_CONTRACT_DECISION_20261108.md) are recorded for the combined candidate; earlier parallel-only and M11-policy-pending statements below describe the previous branches. M12 policy approval, executed G11/G12 source-locked verification, independent S25 reviews and gate acceptance remain pending. The accepted M9 hold and protected `main` remain unchanged.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). Combined test orchestration is approved, but M12-P01..P16 remain unapproved. G12-S01 policy disposition, executable campaign, Q2 historical compatibility and independent G12-S25 review remain pending; M12 does not depend on G11.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records M11 draft PR #31 and M12 draft PR #32 with different additive migration IDs, shared API/UI/role integration surfaces and no combined execution. G11-S01 is BLOCKED, G11-S02..S25 NOT EXECUTED and G12 verification pending. Accepted `main` and the M9 transport hold remain unchanged.

**State:** Local M12-P01..P16 choices recorded on the combined verification candidate. G12-S01 reconciliation, executed gate evidence and acceptance pending. G5 prerequisite accepted.

| ID | Candidate policy choice | Disposition |
| --- | --- | --- |
| M12-P01 | Preserve existing M5 shipment list and mutation routes | Selected for verification |
| M12-P02 | Separate advanced-search and authorized export GET routes | Selected for verification |
| M12-P03 | Reuse one normalized business-filter contract | Selected for verification |
| M12-P04 | Preserve M5 text and status semantics with strict input checks | Selected for verification |
| M12-P05 | Match current M6 voyage assignment at server-side asOfUtc | Selected for verification |
| M12-P06 | Paired UTC half-open windows, max 366 days | Selected for verification |
| M12-P07 | Protected UUID keyset cursor bound to tenant, user, session, filters; 15-minute TTL | Selected for verification |
| M12-P08 | Export for Analyst/Manager, Auditor denied pending further approval | Selected for verification |
| M12-P09 | Fixed versioned CSV v1 fields | Selected for verification |
| M12-P10 | 5,000 rows, 10 MiB, 15-second SQL and 25-second preparation deadlines (within the 30-second Nginx read timeout), 10/min and one in-flight per principal | Selected for verification |
| M12-P11 | UTF-8 quoted CSV and spreadsheet-formula defense | Selected for verification |
| M12-P12 | Mandatory signed, 45-second HMAC proof, migration-only verifier key, no replay and transactional export-preparation audit | Selected for verification |
| M12-P13 | Live search pages versus single-transaction export consistency | Selected for verification |
| M12-P14 | Existing selected organization, five-role policy, bounded 4,096-character URL, sanitized errors and no-store | Selected for verification |
| M12-P15 | No dependence on M11 exception rules, inferred ETA or M9 parked events | Selected for verification |
| M12-P16 | G12 Q2 safe-copy historical snapshot checkpoint or evidenced NOT APPLICABLE | Selected for verification |

See [latest fifth-pass audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md), [fourth-pass audit](M12_CORRECTNESS_AUDIT_20261108_FOURTH_PASS.md), [second-pass audit](M12_CORRECTNESS_AUDIT_20261108.md), [first audit](M12_CORRECTNESS_AUDIT_20261107.md), [architecture](M12_ARCHITECTURE_DESIGN.md), [implementation status](M12_IMPLEMENTATION_STATUS.md), [verification](M12_VERIFICATION_PLAN.md). The canonical work-package graph is unchanged.
