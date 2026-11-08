# M12 Local Contract Register

**Latest hosted verification (2026-11-08):** [Executed M11/M12 run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records a successful .NET and frontend source gate, G11 18/18 scoped PostgreSQL tests, G12 2/2 exclusive PostgreSQL tests, 36/36 impacted integration regressions, and direct SQL isolation of exception, history and evaluation tables with forged-scope denial. Updated source/dependency and runtime-image security scans passed. The three-engine browser rerun is separately tracked and does not yet have a confirmed final disposition in this checkpoint. G11-S08 conflict-specific provenance remains open; M12 local policy decisions and independent G11/G12 S25 reviews remain pending. No G11/G12 acceptance, merge or deployment is authorized. Historical source-only notes below are retained as previous checkpoints.

**Historical combined-source checkpoint (before hosted verification):** The isolated `feat/m11-m12-combined-integration` candidate now contains both M11 and M12 implementations with reconciled shared API, React, SQL role, Compose and EF migration surfaces. [M11 policy decisions](M11_LOCAL_CONTRACT_DECISION_20261108.md) are recorded for the combined candidate; earlier parallel-only and M11-policy-pending statements below describe the previous branches. M12 policy approval, executed G11/G12 source-locked verification, independent S25 reviews and gate acceptance remain pending. The accepted M9 hold and protected `main` remain unchanged.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). Combined test orchestration is approved, but M12-P01..P16 remain unapproved. G12-S01 policy disposition, executable campaign, Q2 historical compatibility and independent G12-S25 review remain pending; M12 does not depend on G11.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records M11 draft PR #31 and M12 draft PR #32 with different additive migration IDs, shared API/UI/role integration surfaces and no combined execution. G11-S01 is BLOCKED, G11-S02..S25 NOT EXECUTED and G12 verification pending. Accepted `main` and the M9 transport hold remain unchanged.

**State:** Candidate implementation defaults on `feat/m12-shipment-search-export`. The request to implement M12 does not itself record formal acceptance of every local policy. G12 acceptance pending. G5 prerequisite accepted.

| ID | Candidate policy choice | Disposition |
| --- | --- | --- |
| M12-P01 | Preserve existing M5 shipment list and mutation routes | Pending |
| M12-P02 | Separate advanced-search and authorized export GET routes | Pending |
| M12-P03 | Reuse one normalized business-filter contract | Pending |
| M12-P04 | Preserve M5 text and status semantics with strict input checks | Pending |
| M12-P05 | Match current M6 voyage assignment at server-side asOfUtc | Pending |
| M12-P06 | Paired UTC half-open windows, max 366 days | Pending |
| M12-P07 | Protected UUID keyset cursor bound to tenant, user, session, filters; 15-minute TTL | Pending |
| M12-P08 | Export for Analyst/Manager, Auditor denied pending further approval | Pending |
| M12-P09 | Fixed versioned CSV v1 fields | Pending |
| M12-P10 | 5,000 rows, 10 MiB, 15-second SQL and 25-second preparation deadlines (within the 30-second Nginx read timeout), 10/min and one in-flight per principal | Pending |
| M12-P11 | UTF-8 quoted CSV and spreadsheet-formula defense | Pending |
| M12-P12 | Mandatory signed, 45-second HMAC proof, migration-only verifier key, no replay and transactional export-preparation audit | Pending |
| M12-P13 | Live search pages versus single-transaction export consistency | Pending |
| M12-P14 | Existing selected organization, five-role policy, bounded 4,096-character URL, sanitized errors and no-store | Pending |
| M12-P15 | No dependence on M11 exception rules, inferred ETA or M9 parked events | Pending |
| M12-P16 | G12 Q2 safe-copy historical snapshot checkpoint or evidenced NOT APPLICABLE | Pending |

See [latest fifth-pass audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md), [fourth-pass audit](M12_CORRECTNESS_AUDIT_20261108_FOURTH_PASS.md), [second-pass audit](M12_CORRECTNESS_AUDIT_20261108.md), [first audit](M12_CORRECTNESS_AUDIT_20261107.md), [architecture](M12_ARCHITECTURE_DESIGN.md), [implementation status](M12_IMPLEMENTATION_STATUS.md), [verification](M12_VERIFICATION_PLAN.md). The canonical work-package graph is unchanged.
