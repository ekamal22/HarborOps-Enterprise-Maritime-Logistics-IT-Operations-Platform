# M3 Correctness Remediation Register

**Date:** 2026-10-31  
**Branch:** `feat/m3-port-vessel-reference-data`  
**Scope:** Follow-up to [M3 correctness audit](M3_CORRECTNESS_AUDIT.md)  
**Execution status:** Source changes committed; no .NET, PostgreSQL, Compose, browser or image scan executed in this session  
**Gate:** G3 remains open. Nothing in this register constitutes executed verification or acceptance.

## Remediation ledger

| Audit ID | Source remediation | Verification status |
| --- | --- | --- |
| A01 | Replaced automatic mutation body binding in `src/HarborOps.Api/References/ReferenceEndpoints.cs` with explicit bounded JSON parsing after CSRF/Origin validation. DELETE endpoints no longer rely on implicit body binding. | Implemented, startup and HTTP tests not executed |
| A02 | `ReferenceDataWorkspace.tsx` now tracks mutations independently of fetch generations and clears the busy state after a successful reload. Tab changes invalidate old mutation state. | Implemented, browser test not executed |
| A03 | `scripts/assert-g3-evidence.mjs` correlates TRX/Playwright results with G3-S01 through G3-S25; `verify-m3-local.sh` captures stage logs and runs the evidence checker. A PASS record is not fabricated from authored tests. | Harness implemented, no executed G3 ledger |
| A04 | `ReferenceHistoricalUpgradeTests.cs` exercises an isolated M0-M2 seed, authenticated selection, M3 upgrade, repeat migration and direct PostgreSQL constraint probes. | Tests authored, not executed |
| A05 | `ReferenceAdversarialHttpTests.cs` covers Support and Auditor for all ten endpoints, extending the existing Analyst/Manager/Administrator matrix. `ReferenceSessionBoundaryTests.cs` covers revoked, disabled and expired sessions. | Tests authored, not executed |
| A06 | `prepare-m3-browser-fixtures.mjs` now generates repeatable, named port/vessel fixtures in two organizations, verifies repeat-run stable IDs and counts and preserves separated synthetic M3 users. A third isolated tenant supports pagination. | Fixtures authored, not executed |
| A07 | New foreign port and vessel UUID mutation cases, parallel update/archive, duplicate creates and cross-tenant identifier reuse in `ReferenceAdversarialHttpTests.cs` and `ReferenceConcurrencyTests.cs`. | Partial: deterministic in-flight membership-revocation ordering and suspension during write still require dedicated controlled-interleaving evidence |
| A08 | `reference-races.spec.mjs` covers delayed old-tenant results, 403 revocation, 503 recovery and logout while a response is delayed, across three Playwright engines. | Partial: simulated browser 403/503 must be paired with real server-side revocation/outage execution |
| A09 | The runner now requires an existing E:-located Docker Desktop VHDX path, preserves E:-only checks, captures per-stage logs, enables JSON browser output and fails if mapped G3 evidence is missing. | Partial: the supplied VHDX must be confirmed as the active Docker backing file on the workstation; not executed |
| A10 | Shared port/vessel transition policy accepts DELETE archival only from Active and PATCH restoration only from Archived; unchanged/misused status transitions conflict. | Implemented, contract tests not executed |
| A11 | M3 migration creates PostgreSQL triggers rejecting direct mutation of UUID, organization ownership, local code, source classification and creation timestamp. Direct SQL regression tests added. | Implemented, migration/trigger tests not executed |
| A12 | M3 mutation endpoints enforce `application/json`, an 8192-byte body limit and valid JSON object parsing with standardized 400 `invalid_request` envelopes. | Implemented, adversarial HTTP tests not executed |
| A13 | The architecture and milestone documents are updated to distinguish feature-branch source, original design, required runtime verification and pending acceptance. | Documentation review only |
| A14 | Complete vessel API lifecycle, optimistic versioning, malformed payloads and new isolated three-browser pagination tests authored. | Partial: browser reload persistence and all negative error-state journeys remain to be observed |
| A15 | Reference reads now use a transactional, organization/session/user locked authorization scope and a post-lock security-version check before querying data. | Implemented, adversarial race tests not executed |
| A16 | Reference writes now compare the session and user security versions while revalidating after the organization and session locks. | Implemented, password-reset and disablement race tests not executed |

## Verification and readiness rules

No source-level remediation is marked as a verified PASS. The [G3 verification plan](M3_VERIFICATION_PLAN.md) remains the mandatory acceptance contract. The [evidence indexer](../scripts/assert-g3-evidence.mjs) verifies a minimum set of successfully executed named tests per scenario and outputs `G3_EXECUTION_INDEX.md` under the E:-backed evidence directory, but an index does not substitute for adjudicating all G3 negative cases.

Run only on the authorized E:-drive workstation with an isolated `ho-verify` stack. Before launching, confirm the **actual active Docker Desktop data VHDX** is on E: and set `HARBOROPS_DOCKER_VHDX` to that verified file, in addition to `HARBOROPS_VERIFICATION_ROOT` and the pre-existing E:-based cache and temporary paths. Run `bash scripts/verify-m3-local.sh` from a clean, fixed source revision.

On any build, test, fixture, migration, browser, scanner or evidence-index error, record FAIL/BLOCKED, repair the implementation or harness and rerun all mandatory stages on the final revision. Do not merge M3 or mark G3 verified/accepted until genuine evidence exists and acceptance is explicit. No GitHub Actions executions are authorized.
