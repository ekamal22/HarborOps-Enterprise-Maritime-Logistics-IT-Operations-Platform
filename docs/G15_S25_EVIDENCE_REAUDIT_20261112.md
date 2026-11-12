# G15 S25 Evidence Reaudit, 2026-11-12

**Historical checkpoint:** This file preserves the earlier conditional or NOT PASS determination at its original source and date. It was superseded by the explicit G15-S25 acceptance and PR #44 merge on 2026-11-12. The original findings and failed evidence remain unchanged. [Acceptance decision](G15_S25_FINAL_ACCEPTANCE_20261112.md) and [post-merge record](G15_S25_POSTMERGE_ACCEPTANCE_RECONCILIATION_20261112.md).


## Decision

**G15 S25 unconditional acceptance: NOT PASS at this checkpoint.**

The completed [technical run 43159414168](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43159414168) is a genuine success at source `00f0ecee208de60f29d0cb8bf44c001325e0ef4f`. It does not certify that every G15 assertion inventory reference was observed in that source-locked packet. The follow-up checker and the additional targeted M11 test commands are prepared on `review/g15-s25-complete-evidence-20261112`, but they have not run on a new immutable source SHA. No unconditional acceptance or merge is inferred.

G13 and G15 remain conditionally integrated under their existing recorded decisions. Protected main, Turkish localization, deployment, notification dispatcher activation and all retention deletion remain out of scope.

## Rechecked archived evidence

The review independently read all ten retained archives from run 43159414168 and the historical archive cited below.

| Technical record | Verified fact |
| --- | --- |
| Workflow and exact source | 10 successful jobs, implementation SHA `00f0ecee208de60f29d0cb8bf44c001325e0ef4f` |
| Evidence files | All 122 SHA-256 manifest entries across the ten archives matched their archived bytes; no unlisted input is accepted by the revised checker |
| .NET TRX | 530 passing results, including 319 unit, 52 contract and 159 PostgreSQL, crash recovery and scale results |
| Playwright | 14 distinct notifications cases passed in Chromium, Firefox and WebKit, 42 passing outcomes |
| Total completed tests | 572 passed, no skipped or failed test outcomes in the final packet |
| G15 mutation campaign | 25 targeted negative variants produced their named test failures, with a passing original-source control |
| Crash interruption | Eight phase markers show the expected killed and verified state; seven applicable recovery tests passed |
| Scale | One million retained receipts, indexed paging, bounded unread count, pre-ANALYZE check, and superseded-membership backlog checks passed in a disposable PostgreSQL server |
| Dependency scan | No high or critical findings in the package scopes checked and no matched committed secret patterns |

These are test-environment results. They do not measure production SLAs, prove arbitrary failure modes, authorize online index migration, or authorize notification deletion.

Earlier failed attempts, including 43158205021, 43157958605, 43157786996 and 43155499357, retain their failed status at their respective SHAs.

## Source and assertion reconciliation

The mandatory G15 inventory contains 25 scenarios, 119 atomic assertions, 111 automated candidate assertions and eight manual policy/reviewer assertions. Of 70 distinct test methods previously referenced as direct current-source witnesses, 64 were in the latest packet and six were not.

The previous omission is addressed as follows:

| Requirement | Reconciliation action | State |
| --- | --- | --- |
| G15-S04-A01 | Add `RecurrenceAfterAcknowledgedClearanceCreatesNewOccurrence` to targeted M11 TRX | Prepared, execution pending |
| G15-S04-A03 | Add `UnchangedReevaluationAdvancesCheckpointWithoutDuplicateHistory` | Prepared, execution pending |
| G15-S04-A04 | Add `VerifiedClearancePersistsAcrossUnknownBeforeRecurrence` | Prepared, execution pending |
| G15-S21-A06 | Keep the direct G15 dispatcher timeout test and two targeted mutants; remove an unrelated G13 report cancellation test reference | Mapping corrected |
| G15-S23-A03 | Add `NullSchedulesAreUnknownNotInventedDelays` to targeted M11 TRX | Prepared, execution pending |
| G15-S23-A03 | Record the historical G13 transport/KPI witness with its original source SHA, run, archive ID and TRX hash; verify unchanged source paths | Independent equivalence approval pending |

The historical test `ReplayedEquipmentReceiptsAndParkedTransportNeverInventKpiProgress` passed in `g13-tracking-source.trx` of artifact 13909188656, run 43139153300, at `479616a5b7f17c494d897834a66d10b6276bb517`. Its archived TRX SHA-256 is `4ce60864523884bbeeaba58defdd566d430289dc5ed590a92f6323def65ad421`. This historical result is not represented as a fresh test execution.

The revision preserves the 25 G15 scenario definitions, explicitly distinguishes historical evidence from current TRX, and rejects any automatic elevation of manual S25 policy/reviewer assertions.

## Previously identified browser issues

The three issues from the earlier post-merge S25 review are closed at `00f0ecee208de60f29d0cb8bf44c001325e0ef4f`:

- G15-S22-A02: positive second-page append once, ordered without duplicates.
- G15-S22-A03: pending request loading indicator displayed and cleared.
- G15-S22-A04: real authorized archived shipment opened from a historical alert fixture without permitting mutation.

All three passed in all three browsers. The archived-target API remains real; only the historical personal notification was fixture-supplied.

## Checker controls newly prepared

The updated `scripts/verify-g15-s25-remediation.py` now requires:

1. A complete signed-off candidate inventory of all 119 assertions and each of its 68 current-source test method IDs.
2. Exact passing TRX names and outcomes for every current-source method, with historical evidence kept separately and source-equivalence approval pending.
3. All 14 distinct browser cases per engine, without skipped, unexpected, retried or missing cases.
4. Individual mutation logs proving the intended named behavioral test failed, rather than counting a compilation error or generic failure as a mutant kill.
5. Individual crash phase JSON/log digests and applicable recovery records.
6. Every file in each source-locked artifact manifest, a common source SHA, and no omitted or duplicated manifest paths.
7. The security scan summary and documented source-path comparison for the historical witness.

Negative-control unit tests for the verifier are included in the preflight plan. The four targeted M11 tests are included in the impacted-regressions job. No new G15 acceptance run has executed this revised checker.

## Scenario dispositions

| Scenario family | Evidence assessment |
| --- | --- |
| G15-S01 | Policy P01 through P14 retained with existing P10 condition and P12 no-deletion restriction |
| G15-S02 through S03 | Technical evidence passed at the earlier verified implementation SHA |
| G15-S04 | Exact-source evidence incomplete until three selected M11 methods execute and are inspected |
| G15-S05 through S20 | Technical evidence passed within documented verification limits |
| G15-S21 | Direct G15 dispatcher-timeout and behavioral mutation evidence passed; incorrect cross-milestone method mapping corrected |
| G15-S22 | All 14 cases per browser passed; three earlier browser gaps closed |
| G15-S23 | M11 null-schedule test execution and historical G13 source-equivalence review remain pending |
| G15-S24 | Original-source and mutant evidence passed; revised individual-log validation not yet executed on the new source |
| G15-S25 | NOT PASS until complete new-source evidence, independent reviewer attestation and explicit final disposition |

## Correctness audit follow-up

The source and test audit checked the G15 notification store, endpoint authorization and response contracts, default-off dispatcher, retained-receipt index migration, M11 test-method identities, the archived-target integration test and browser scenarios. The baseline technical runtime evidence remains intact.

Three test-or-verification weaknesses were corrected before the next run:

- The authorized archived-shipment detail integration test now requires HTTP 200 and checks the archived identity and status, instead of treating HTTP 404 as a permitted success.
- The browser evidence checker now rejects unexpected suite-level errors, expected-failure cases and reports whose only passed attempt is a retry. Dedicated negative tests cover these branches, and the tests are runnable outside the hosted environment.
- The historical migration witness list now requires an exact TRX test-method match, rather than a substring match.

The five tracked G13 compatibility source paths were compared with the historical passing SHA using their repository blob identities. All five are byte-identical. This is a technical source-equivalence finding only; the S25 independent acceptance decision remains pending.

No G15 runtime files changed during the follow-up correctness audit. This document records prepared code and planned checks, not proof that the revised code has executed.

## Conditions for final closure

- Execute the revised, focused verification packet at one frozen new source SHA, with complete raw artifacts. This requires separate authorization for GitHub Actions.
- Independently verify the new ledger against all 25 G15 scenarios, review individual evidence and historical source equivalence, and approve or reject the documented historical exception.
- Record the final S25 attestation and explicit decision only after these checks. Do not silently mark an automated ledger as independent acceptance.
- If accepted, reconcile the authoritative status and policy documents against the exact accepted merge or candidate revision. Keep earlier failed reviews and evidence intact.

The prepared changes have no G15 runtime behavior changes and do not modify protected main. The existing conditional integration remains effective, subject to its recorded limits.
