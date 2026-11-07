# M12 Local Contract Register

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
| M12-P10 | 5,000 rows, 10 MiB, 10/min and one in-flight per principal | Pending |
| M12-P11 | UTF-8 quoted CSV and spreadsheet-formula defense | Pending |
| M12-P12 | Mandatory signed, 45-second HMAC proof, migration-only verifier key, no replay and transactional export-preparation audit | Pending |
| M12-P13 | Live search pages versus single-transaction export consistency | Pending |
| M12-P14 | Existing selected organization, five-role policy, sanitized errors and no-store | Pending |
| M12-P15 | No dependence on M11 exception rules, inferred ETA or M9 parked events | Pending |
| M12-P16 | G12 Q2 safe-copy historical snapshot checkpoint or evidenced NOT APPLICABLE | Pending |

See [correctness audit](M12_CORRECTNESS_AUDIT_20261107.md), [architecture](M12_ARCHITECTURE_DESIGN.md), [implementation status](M12_IMPLEMENTATION_STATUS.md), [verification](M12_VERIFICATION_PLAN.md). The canonical work-package graph is unchanged.
