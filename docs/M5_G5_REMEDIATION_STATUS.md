# M5/G5 Correctness Remediation

**Date:** 2026-11-01  
**Branch:** `feat/m5-shipment-lifecycle`  
**Original audit:** [M5/G5 correctness audit](M5_G5_CORRECTNESS_AUDIT.md) at source revision `7c85c3271e481fd460d533f08940c6bd4211d4cc`  
**Disposition:** All eleven reported findings have code or verification-source corrections committed. These corrections have **not** been compiled or runtime verified. G5 OPEN, acceptance pending.

## 1. Remediation ledger

| Finding | Correction committed | Verification remaining |
| --- | --- | --- |
| G5-A01 | `assert-g5-evidence.mjs` now requires all 25 scenario IDs, source-matched methods, independent .NET TRX files, eight named shipment browser specs in three engines, exact fixture state and teardown proof. | Actual G5 evidence run, mutation controls and independent review |
| G5-A02 | Created G5-only stage/browser/outage/recovery scripts. The Compose project is `ho-verify-g5`, with `ho-g5-pg`, `ho-g5-sdk` and G5 labels. Existing G0-G4 scripts remain unchanged. | Execute against a concurrent unrelated verification stack and confirm no takeover or foreign cleanup |
| G5-A03 | Added full console capture, per-stage logs, before/after drive snapshots, owned-resource teardown verification, Docker service logs, SHA-256 file manifest and final ZIP/digest on E:. Evidence evaluation follows teardown; archive follows evidence evaluation. | Verify ZIP integrity, completeness, no orphan resources and C: attribution on the desktop |
| G5-A04 | Added exhaustive workflow, independent SQL negative/positive controls, six-route five-role permission tests, anonymous/missing selection, malformed bodies, real cross-tenant IDs, read/write revocation interleavings, duplicate/archive/restore races, and cancelled-write rollback. | Run and inspect every negative case against real PostgreSQL and HTTP; resolve any observed failures |
| G5-A05 | Expanded shipment Playwright coverage to eight named cases: Analyst flow, Auditor scope, tenant switch, Manager terminal states/history, catalog failure/retry, stale conflict, delayed tenant read and logout. All required in Chromium, Firefox and WebKit. | Execute all engines with no skipped/failed specs; inspect real screenshots, traces and races |
| G5-A06 | The M4-to-M5 upgrade fixture now includes a vessel, a voyage with two linked ports, two containers, user/session selection and migration history, with rollback and reapply checks. | Validate migrated values and schema on disposable PostgreSQL |
| G5-A07 | G5 fixture script now seeds active, Booked, InTransit and Archived shipments per tenant; checks identifier reuse/uniqueness across tenants, proves two-pass idempotency and verifies actual five-role memberships. It produces `g5-fixtures.json`. | Execute twice on the same disposable database and review row/version/history output |
| G5-A08 | The evidence checker reads separate `unit.trx`, `contract.trx`, `integration-fresh.trx` and `integration-repeat.trx`, checks failing/skipped cases, compares complete fresh/repeat test identities, and requires parameter-row counts including all 36 workflow pairs and all queued revocation variants. | Confirm generated TRX metadata and parameter naming against the .NET 10 runner |
| M5-P01 | `ShipmentWorkspace` has an explicit Retry port catalog action, port-loading state, and Refresh retries both shipment list and catalog without hiding shipment records on catalog error. | Browser 503/recovery scenario in each engine |
| M5-P02 | A 409 now preserves form input and conflict notice. Load current version updates the expected version without discarding entered edits and lets the user consciously resubmit. | Two-page version-conflict and retry flow across engines |
| M5-P03 | Shipment detail returns same-tenant origin/destination display labels via nonmapped fields, without granting Auditors M3 reference access. | Auditor read-only browser and scoped HTTP detail assertion |

## 2. Additional correctness corrections

- Regenerated the G5 stage helper from the accepted G2 baseline after detecting a duplicated stage block in the first local copy. The final helper has one `stage_dotnet` function and a distinct result filename per .NET test pass.
- Changed the project identifier to `ho-verify-g5`, which is separate from G2/G4 and also satisfies the accepted disposable-project allowlist used by the M0/M1 smoke scripts.
- Repaired shell-quoted Docker container filters in the G5 teardown stage, avoiding accidental lookup misses.
- Added a `check-g5-source-coverage.mjs` preflight that rejects missing mapped test methods, missing browser cases, incomplete scenario numbering and G2/G4 container aliases in G5 scripts.
- Added a cancellation-under-lock regression to ensure that an aborted queued mutation does not commit shipment data or manual status history.
- Strengthened pagination tests for no repeated rows across pages, literal wildcard treatment, case-insensitive query and status filters.
- Strengthened direct SQL and tenant tests using a **real** shipment created in a foreign organization and attempted access through the wrong selected organization.

## 3. Source review performed

A repository-read audit at the remediation branch verified:
- Every referenced G5 unit/integration method has a declaration in its expected test class.
- All 25 scenario identifiers are present in the source checker, with eight browser spec names resolvable.
- Newly authored JavaScript modules are parseable as modules in the available JavaScript runtime.
- The G5 helper has a single .NET stage and references the accepted disposable project naming convention, while retaining separate G5 integration and browser resource identities.
- The feature branch retains the accepted `main` parent and has not been merged.

**No successful build, SQL migration, Postgres direct-SQL execution, xUnit run, browser launch, Docker outage, Trivy scan, or local storage cleanup has been performed in this review.** Corrections committed to a source branch do not themselves constitute a verified result.

## 4. Follow-up source review

At source `c2cdaf935bd4`, the shipment workspace was found to read its list row
without requesting scoped port labels from the detail endpoint. The selection
path now retrieves both scoped detail and manual history, retains local form
edits and ignores stale responses. The existing Auditor browser scenario
exercises the port labels.

The G5 runner previously stopped before final evidence packaging when G5-S01
was pending. It now records the incomplete evidence gate, seals and archives
the technical run after verified teardown, and still returns a nonzero status.
It does not bypass the contract decision or convert incomplete evidence into
a passing gate.

The browser runner also required absolute Compose paths after changing into
`tests/browser`, and the JSON reporter needed the shared Playwright
configuration's actual environment variable. Both are corrected so per-spec,
per-engine JSON files are retained under the G5 evidence directory. Incomplete
evidence bundles now carry an explicit incomplete archive prefix and metadata.

These follow-up changes are committed source corrections only. Execution and
independent evidence review remain required.

## 5. Required next execution

1. Use the committed `scripts/verify-m5-local.sh` in an E:-backed Windows Git Bash context. Source and browser caches, Docker data, evidence and scratch output must remain on E:, with only safe, positively attributable C: temporary cleanup permitted.
2. Run the complete local G5 pipeline on one final SHA. The mandatory M5-local decision at [M5 local contract](M5_LOCAL_CONTRACT_DECISION.md) is still pending. G5-S01 must not be bypassed.
3. Repair any actual compile/runtime failures or flaky/false-positive tests, commit fixes with `[skip ci]`, audit again and rerun at the changed SHA.
4. Preserve the original stage logs, TRX, JSON browser reports, service/outage logs, zero-resource teardown, hash manifest and final E: ZIP. Review the original raw evidence rather than only a runner summary.
5. Request explicit G5 acceptance before merging M5 into `main`. No GitHub Actions have been authorized or triggered.

**Gate outcome:** G5 OPEN, acceptance pending. M6 integration remains dependent on G5.
