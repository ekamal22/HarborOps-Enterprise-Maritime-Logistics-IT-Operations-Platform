# M12 Local Search and Export Contract Decision

**Current verification policy (2026-11-08):** [M11/M12 verification operations](M11_M12_VERIFICATION_OPERATIONS_20261108.md) now requires manual, coordinated, source-locked workflows instead of automatic push-triggered campaigns. [Run-by-run progress](M11_M12_VERIFICATION_PROGRESS_20261108.md) is the current verification checkpoint; older reports and source-only notes below are historical. G11/G12 independent S25 review and acceptance remain pending. Protected `main`, the M9 transport hold, and deployment restrictions are unchanged.

**Decision recorded:** 2026-11-08
**Scope:** M12-P01 through M12-P16 on the combined M11/M12 candidate
**Disposition:** Select the existing documented M12 implementation defaults as the local technical contract for verification. These choices implement the 2026-11-08 instruction to finish M11/M12 verification; they do not constitute G12-S01 execution proof, independent evidence review, final gate acceptance, merge or deployment authorization.
**Reference source:** `feat/m11-m12-combined-integration`; source SHA must be recorded separately for every executed test.

## Selected contract

| Policy | Selected implementation contract | Boundaries retained |
| --- | --- | --- |
| M12-P01 | Preserve accepted M5 shipment list/detail/mutation endpoints and workflows. | Advanced search is additive, not a replacement for existing clients. |
| M12-P02 | Use dedicated authenticated GET shipment-search and CSV export routes. | Same organization, live session and role checks apply separately to each route. |
| M12-P03 | Share normalized business-filter semantics for search and export. | Authorization, pagination and transaction boundaries differ by operation. |
| M12-P04 | Preserve M5 text/status semantics with strict, bounded input validation. | Reject malformed, repeated and unrecognized parameters. |
| M12-P05 | Select current M6 voyage assignment at the server-provided `asOfUtc`. | Do not infer movement or attach superseded/future associations. |
| M12-P06 | Use explicit paired half-open UTC windows, maximum 366 days. | Inclusive from, exclusive before; no ambiguous local-time interpretation. |
| M12-P07 | Use UUID keyset pagination with 15-minute protected cursors bound to tenant, actor, session, filters and reference instant. | Live pages are not represented as immutable snapshots. |
| M12-P08 | Allow CSV export for Analyst and Manager only. Auditor can search but cannot export; Administrator and Support have neither permission. | No implicit administrative export bypass. |
| M12-P09 | Use the fixed 12-column versioned CSV v1 field set. | No internal tracking records, raw payloads, audit evidence or M11 exception details. |
| M12-P10 | Cap exports at 5,000 rows, 10 MiB, 10 attempts per minute per principal and one concurrent export per principal on each API instance. Cap PostgreSQL statements at 15 seconds and search/export preparation at 25 seconds within the 30-second proxy read timeout. | Preparation deadlines do not guarantee end-to-end network timing; execute timeout/cancellation and multi-instance negative tests before acceptance. |
| M12-P11 | Use quoted UTF-8 CSV with CRLF, doubled embedded quotes and spreadsheet-formula injection protection. | Never stream a partially prepared CSV on failure. |
| M12-P12 | Require a short-lived, 45-second domain-separated HMAC proof verified by a migration-owned SQL function and transactionally coupled M16 `shipment.export_prepared` audit, with replay prevention. | Keep M12 signing keys separate from M10/M11; an audit proves preparation, not receipt by a browser. |
| M12-P13 | Accept live, stateless search pages, but prepare exports under a single repeatable-read transaction. | Do not promise search-to-export snapshot equivalence across concurrent writes. |
| M12-P14 | Enforce selected organization, five-role policy, 4,096-character encoded URL limit, sanitized error responses and no-store caching. | Restricted database authority and CSRF/Origin rules remain mandatory. |
| M12-P15 | Keep M12 independent of unaccepted M11 rules, inferred ETA and parked M9 transport events. | No changes to the accepted M9 operating restriction. |
| M12-P16 | Require safe-copy Q2 historical-volume compatibility evidence, or explicitly prove that an applicable snapshot is unavailable and document NOT APPLICABLE. | Do not silently turn unavailable data into PASS or skip migration safety checks. |

## Required execution and review

These policy choices close the *configuration-decision ambiguity* but do not prove correctness. G12-S01 remains subject to source-contract reconciliation and separate final gate decision. G12-S02 through G12-S25 require executed source-locked evidence. In particular, exercise the five-role HTTP/SQL matrix, transaction-scoped audit, 5,000-row and 10-MiB boundaries, slow database/CPU deadline and cancellation behavior, measured query plans, populated 13-to-15 migrations, Q2 snapshot applicability, browser regressions and effective security mutants.

No test executed solely because this document was created. Preserve the original failed and superseded verification runs and their exact SHAs.

## Unchanged protections

`main` remains at its separately verified accepted baseline until a specifically authorized merge. No production migration, release, deployment or M9 transport-hold clearance is implied. The M9 ARRI/DEPA reconciliation hold, 10,000 unresolved-work cap, default-off M10 projector and null ETA remain intact.
