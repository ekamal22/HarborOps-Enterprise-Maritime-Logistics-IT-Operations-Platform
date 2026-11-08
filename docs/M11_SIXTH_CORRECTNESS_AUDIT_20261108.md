# M11 Sixth Correctness Audit and Source Remediation

**Review date:** 2026-11-08
**Implementation branch:** `feat/m11-delay-exception-lifecycle`; draft PR #31
**Protected accepted baseline:** `main` `6ead1d076946dd96b53401265a31efc35c4a666c`
**Starting review revision:** `314740e0c2f6de0a75d537ef7db275ca64100a32`
**Sixth source remediation checkpoint:** `3b81c54210956081f46466c57caa6467c53019ff`
**Historical audits:** [first](M11_CORRECTNESS_AUDIT_20261107.md), [second](M11_SECOND_CORRECTNESS_AUDIT_20261107.md), [third](M11_THIRD_CORRECTNESS_AUDIT_20261108.md), [fourth](M11_FOURTH_CORRECTNESS_AUDIT_20261108.md), [fifth](M11_FIFTH_CORRECTNESS_AUDIT_20261108.md)
**Disposition:** Source defects corrected; no executable verification or policy approval. **G11 acceptance pending.**

## Review scope and evidence quality

Reviewed the candidate M11 pure rule policy, full additive migration 14, signed SQL evaluation/Manager functions, M10 source-version and projection boundaries, organization and shipment locks, due-keyset scanner, scoped API read/mutation endpoints, immutable history paging, frontend disposition controls and accompanying tests. Checked against the accepted M5 workflow status transitions, M9 operating hold, M10 projector/UTC semantics, M16 atomic audit path and provisional M11-P01 to P12 contracts.

The connected repository is accessible for source reading and updates, but this execution environment lacks the E:-backed local checkout, .NET SDK, PostgreSQL server, Docker and browser fixture runtime. GitHub cloning from the local container could not resolve the remote host. The review therefore does not establish compilation, successful migration application, passing SQL role tests, browser acceptance or an independent frozen-SHA gate verdict.

## New findings and source corrections

| ID | Severity | Finding | Correction | Executable evidence |
| --- | --- | --- | --- | --- |
| F11-039 | Medium | The deterministic rules labeled an `Inactive` `RecordedArrivalLate` outcome `High`, and inactive schedule-overdue/departure-risk outcomes could retain `Warning`. That conflicts with the absence of a current qualifying condition and can mislead UI consumers. | Timely recorded arrivals and inactive schedule/departure warnings now use `Info`. Active recorded late arrival remains `High`, and active overdue/departure risk retains `Warning`. Added a focused unit test for active/inactive severity boundaries. | Unit source added; NOT EXECUTED |
| F11-040 | Medium | The signed evaluator's semantic no-op shortcut compared only stored condition and source fingerprint. If visible severity or other stored rule metadata changed without changing that fingerprint, the shortcut refreshed the evaluation watermark but silently retained stale values, without immutable reevaluation history. | No-op return is now conditional on matching all stored rule metadata: severity, confidence, reason code, evidence reference/threshold timestamps and next-due timestamp. A genuine metadata change proceeds through the normal versioned update and append-only Reevaluated event. Added a real-database fixture simulating a pre-acceptance severity recalibration and checking one history event, corrected severity and subsequent idempotent no-op. | Integration source added; NOT EXECUTED |
| F11-041 | Low | The bounded due-work keyset compared a UUID row tuple against uncast nullable parameters, relying on implicit PostgreSQL inference. | Typed both tuple elements explicitly as UUID without changing the scan order or batch limit. Added a source-contract assertion. | Source corrected; PostgreSQL planner tests NOT EXECUTED |

## Reconciled source contracts

- Rule severity is informational when an actual qualifying condition is inactive. Unknown/Stale retain the explicitly classified uncertainty semantics; source facts remain distinct from physical proof.
- The `m11-rules-v1` policy designation remains **provisional** before G11. A late pre-acceptance rule calibration is recorded through a versioned Reevaluated history event when it changes stored visible rule metadata, despite an identical fingerprint. Repeated evaluations with completely identical metadata still advance checkpoints without duplicate history.
- Bounded due work remains keyed by `(organization_id,shipment_id)`. Explicit UUID casts for the continuation tuple do not alter the existing 25/50-candidate limits, scan checkpoints or the separately held M9 transport worker.
- Historical M11 audit documents remain unchanged. They describe earlier source checkpoints and must not be construed as completed test evidence.

## Unresolved risks and G11 blockers

1. **G11-S01 BLOCKED** until all M11-P01 to P12 local choices are explicitly approved, including severity thresholds, recurrence after an uncertain state, eventual disposition of an immutable recorded-late-arrival condition and semantics of manager clearance.
2. **G11-S02 through G11-S24 NOT EXECUTED**. Required evidence includes warning-as-error .NET compilation, focused unit and contract tests, M11 migration 14 on clean and populated accepted M10/G16 copies, actual restricted-role SQL and HMAC tests, idempotency and serialized race cases, stored severity recalibration, role redaction, history pagination, browser engines, outage/restart/load and accepted-gate regression checks.
3. **G11-S25 NOT EXECUTED**. Independent review of raw, hashed, source-locked verification evidence and explicit gate acceptance remain necessary.
4. **Conflict event lineage incomplete.** M10 projection digests alone are not a complete event/container-level explanation. Do not claim G11 provenance closure.
5. **Inherited API SQL SELECT scope remains broad.** HTTP tenant and role guards are not independent database row-level read protection.
6. **Stateless pagination is not an immutable lifecycle-state snapshot.** The database-clock cutoff prevents newly created exception rows entering the same browse, but existing rows can change state. The immutable history continuation is source-implemented and unverified under load, cancellation and concurrent appends.
7. **Review environment limitation:** static source inspection and authored regression source are not equivalent to running `scripts/verify-g11-local.sh` or a complete G11 campaign.

## Mandatory verification sequence

Freeze the final PR commit in a clean, disposable E:-backed checkout. Run source-contract and planning/graph checks; .NET build/tests with warnings as errors; frontend typecheck; clean/populated migration 14; restricted PostgreSQL authority, replay and audit-failure tests; exact severity/history source regressions; bounded keyset scanner; browser verification; and relevant M5/M9/M10/M16 regressions. Preserve all attempts and independently reconcile them against the required 25 G11 scenarios. No GitHub Actions, merge, deployment or accepted-gate changes are implied by this document.

Protected `main`, the accepted M10/M16 migrations, M9 worker and GitHub workflow configuration are unchanged. The M9 sustained-transport hold remains in force, ARRI/DEPA stay parked, the 10,000-item unresolved-work cap remains binding and M10 ETA stays null. No destructive cleanup was performed.

**Final source-review verdict: corrections committed; executable correctness unverified. G11 acceptance pending.**
