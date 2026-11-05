# G10 Verification Plan: Event Timeline and Projection Rebuild

**State:** Planned, not executed. G10 acceptance pending.  
**Milestone:** M10-01 through M10-04 after accepted G9; [architecture](M10_ARCHITECTURE_DESIGN.md), [implementation plan](M10_IMPLEMENTATION_PLAN.md).  
**Source lock:** Record one clean tested commit SHA, canonical migration history, exact configuration, fixture version, evidence manifest and execution commands. Use only E:-backed disposable test environments.

## Required scenarios

| ID | Scenario | Required evidence |
| --- | --- | --- |
| G10-S01 | M10-local ordering/correction/projection contract approved | Explicit decisions and fixture trace; unanswered policy choices remain blocking |
| G10-S02 | Fresh M0-M10 migration | Real PostgreSQL empty schema, exact EF history and no model drift |
| G10-S03 | Populated M9 to M10 upgrade | Retain receipt/event/effect/work/attempt/shipment history byte-for-byte where immutable; compare row counts and digests |
| G10-S04 | Stable UTC ordering | Occurrence, receipt, source, event key and UUID tie breakers repeat identically |
| G10-S05 | No source sequence fabrication | Supported GTIN/GTOT/ARRI/DEPA fixtures prove that unknown source order is not inferred |
| G10-S06 | Same-timestamp contradiction | Expose explicit Conflict rather than falsely choosing a shipment lifecycle or ETA |
| G10-S07 | Late and out-of-order arrivals | Recompute the same effective timeline/projection regardless of processing order |
| G10-S08 | Canonical replay and collision | M8 202/409 semantics unchanged; one M9 effect and one projection application per eligible receipt/policy |
| G10-S09 | Unresolvable reference handling | Unknown, ambiguous, foreign or out-of-window equipment never attaches to an unrelated shipment |
| G10-S10 | Transport backlog and admission guard | Opaque ARRI/DEPA remains parked, 10,000 unresolved ceiling upheld; no transport hold release without separately approved and verified mechanism |
| G10-S11 | Tenant and object-level GET isolation | Two organizations, foreign UUIDs/cursors, archived resources and forbidden roles produce safe denials without data leakage |
| G10-S12 | Current session, role and organization changes | Immediate revocation, selection changes, suspended organization and denied roles respected |
| G10-S13 | Bounded cursor pagination | Stable order, no missing/repeated pages at snapshot, invalid/expired/foreign cursors rejected safely |
| G10-S14 | API provenance and redaction | Correct UTC source/receipt/event summary; no raw body, secret, token, unbounded payload or digest exposure |
| G10-S15 | Manual workflow separation | M5 `WorkflowStatus` and status-change rows unaffected by tracking projection or correction |
| G10-S16 | Nullable ETA and unknown state | No fabricated ETA, location, arrival or certainty; absent evidence explicitly Unknown |
| G10-S17 | Authorized append-only corrections | Manager authorization under approved policy, sanitized reason, link validity, generation/cycle/overlap rejection, CSRF/Origin and provenance |
| G10-S18 | Rebuild determinism | Recompute from full retained eligible input; derived rows and canonical digest match across repeated rebuilds |
| G10-S19 | Rebuild idempotence and version | No extra side effects or materialization revision on identical replay; changes version exactly when effective state changes |
| G10-S20 | Concurrent M9 effect and M10 rebuild | Lock order, commit race and recovery show no lost late effect or stale overwrite |
| G10-S21 | Crash and outage windows | Kill projector before/after transaction commit, drop/restart database, retry without duplication or missing work |
| G10-S22 | Browser/API workflow and existing regressions | Scoped timeline/projection in all supported browser engines if UI exists; M5-M9 related HTTP/browser flows unchanged |
| G10-S23 | Adversarial SQL and permission guards | Direct tenant FK attempts, forbidden updates to correction history, role abuse, fake cross-tenant source and forged cursor rejected |
| G10-S24 | Operational and migration coexistence | E:-isolated Compose, security scans, no M9 lease/regression changes, M16 overlap inventory, transport hold and docs synchronized |
| G10-S25 | Independent evidence and explicit gate disposition | Raw artifact hashes, source SHA, 25 scenario results, independent review and separate acceptance decision, without rewriting earlier outcomes |

G10-S01 must be formally closed before freezing implementation-sensitive policy. G10-S25 cannot be satisfied by an unreviewed runner summary. A technical pass is not explicit G10 acceptance.

## Mandatory execution layers

1. Pure reducer/unit tests for sorting, ties, corrections, deterministic digests, unknown/conflict tracking observation and nullable ETA.
2. Contract/HTTP tests for page cursor, permission matrix, session/organization changes, raw payload redaction, stable errors and CSRF/Origin on mutations.
3. Real PostgreSQL tests for migrations, composite tenant references, anti-join work discovery, append-only correction guards, transactional projection updates, M9 lease/effect invariants, concurrent late commit and rebuild recovery.
4. Full relevant G0-G9 integration and browser regression, three engines if a UI component changes, controlled outage/restart, source-locked evidence and targeted adversarial/mutation cases. Report exact test counts, never predict them.
5. Verify that M9's 10,000 unresolved-work admission cap and sustained transport-event processing restriction remain effective. Any proposed relaxation requires its own approved contract and proof of bounded backlog drain.

## Evidence disposition

Store sanitized TRX, browser, PostgreSQL, scan, mutation and outage output in a dedicated E: evidence root. Keep initial failed attempts as well as successful corrected ones. Record exact final SHA and any post-run source changes; changing runtime source invalidates an earlier source-locked verdict. No GitHub Actions or permanent C:-based project storage. Do not modify accepted historical verification records.

**Accessibility testing remains assigned only to M24-04.** G10 uses ordinary browser functional regressions without dedicated accessibility gate assertions.

**Outcome at planning time:** 0 of 25 G10 scenarios executed; no G10 verification, acceptance, merge or deployment authorization claimed.
