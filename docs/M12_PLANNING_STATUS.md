# M12 Local Contract Register

**Accepted milestone (2026-11-08):** M12/G12 was formally accepted and integrated into `main` through PR #33. [G12 gate matrix](M12_G12_EVIDENCE_MATRIX_20261108.md) and [formal G11/G12 closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) supersede earlier pending gate and source-only checkpoints. M12-P01..P16 are approved as implemented, including export audit, permission and per-instance limits. G12-S21 is NOT APPLICABLE to a nonexistent Q2 production snapshot. The M9 transport hold remains binding; no deployment.

**State:** M12-P01..P16 formally approved, G12-S01 and G12-S25 closed, G12 accepted and merged through PR #33. The policy selection table below is an earlier planning-stage record. G5 prerequisite accepted.

| ID | Candidate policy choice | Disposition |
| --- | --- | --- |
| M12-P01 | Preserve existing M5 shipment list and mutation routes | Approved and accepted for G12 |
| M12-P02 | Separate advanced-search and authorized export GET routes | Approved and accepted for G12 |
| M12-P03 | Reuse one normalized business-filter contract | Approved and accepted for G12 |
| M12-P04 | Preserve M5 text and status semantics with strict input checks | Approved and accepted for G12 |
| M12-P05 | Match current M6 voyage assignment at server-side asOfUtc | Approved and accepted for G12 |
| M12-P06 | Paired UTC half-open windows, max 366 days | Approved and accepted for G12 |
| M12-P07 | Protected UUID keyset cursor bound to tenant, user, session, filters; 15-minute TTL | Approved and accepted for G12 |
| M12-P08 | Export for Analyst/Manager, Auditor denied pending further approval | Approved and accepted for G12 |
| M12-P09 | Fixed versioned CSV v1 fields | Approved and accepted for G12 |
| M12-P10 | 5,000 rows, 10 MiB, 15-second SQL and 25-second preparation deadlines (within the 30-second Nginx read timeout), 10/min and one in-flight per principal | Approved and accepted for G12 |
| M12-P11 | UTF-8 quoted CSV and spreadsheet-formula defense | Approved and accepted for G12 |
| M12-P12 | Mandatory signed, 45-second HMAC proof, migration-only verifier key, no replay and transactional export-preparation audit | Approved and accepted for G12 |
| M12-P13 | Live search pages versus single-transaction export consistency | Approved and accepted for G12 |
| M12-P14 | Existing selected organization, five-role policy, bounded 4,096-character URL, sanitized errors and no-store | Approved and accepted for G12 |
| M12-P15 | No dependence on M11 exception rules, inferred ETA or M9 parked events | Approved and accepted for G12 |
| M12-P16 | G12 Q2 safe-copy historical snapshot checkpoint or evidenced NOT APPLICABLE | Approved and accepted for G12 |

See [latest fifth-pass audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md), [fourth-pass audit](M12_CORRECTNESS_AUDIT_20261108_FOURTH_PASS.md), [second-pass audit](M12_CORRECTNESS_AUDIT_20261108.md), [first audit](M12_CORRECTNESS_AUDIT_20261107.md), [architecture](M12_ARCHITECTURE_DESIGN.md), [implementation status](M12_IMPLEMENTATION_STATUS.md), [verification](M12_VERIFICATION_PLAN.md). The canonical work-package graph is unchanged.
