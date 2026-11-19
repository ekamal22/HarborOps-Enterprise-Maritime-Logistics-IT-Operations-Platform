# M18 / G18 Independent Review Packet (S25)

**Prepared:** 2026-11-18
**Status:** Prepared for a genuinely separate reviewer. No review has taken place. G18-S25-A01, A02 and A03 are **pending**. This packet names no reviewer, contains no signature and records no acceptance. A passing technical campaign is not acceptance.

## 1. What the reviewer is asked to decide

| Assertion | Question |
| --- | --- |
| G18-S25-A01 | Can the reviewer authenticate the frozen source, the hashes, the 25 scenarios, the 72 technical assertions and the negative and security evidence from the raw material? |
| G18-S25-A02 | Are all policy, provenance, performance, Q3 and procedural exceptions disclosed and dispositioned without edited failures? |
| G18-S25-A03 | Does the reviewer, being separate from the people who built and ran the campaign, sign a source- and evidence-bound PASS or NOT PASS recommendation? |

A distinct, explicit decision on G18 acceptance and on any merge is still required afterwards.

## 2. Material to authenticate

| Item | Where |
| --- | --- |
| Source | branch `feat/harborops-m18-onboarding-audit-d56035`; product freeze `fb1b42e5918a0ab7767aefcaeb425b9bb26fe936`, final tooling head `5cc3fa583f638236d824a47c2c65e9615cf4ae3e`, baseline `72251f5310f24fb98322b7eb403a5e9e66e040ef` |
| Verification record | [M18_G18_LOCAL_VERIFICATION_20261118.md](M18_G18_LOCAL_VERIFICATION_20261118.md) |
| Ledger | [M18_G18_ASSERTION_LEDGER_20261118.json](M18_G18_ASSERTION_LEDGER_20261118.json) and the [witness map](../tests/g18/assertion-map.json) |
| Defects and test audit | [M18_CORRECTNESS_AND_TEST_AUDIT_20261118.md](M18_CORRECTNESS_AND_TEST_AUDIT_20261118.md) |
| Implemented contract | [M18_AS_IMPLEMENTED_20261118.md](M18_AS_IMPLEMENTED_20261118.md) |
| Q3 | [M18_Q3_SNAPSHOT_DISCOVERY_20261118.md](M18_Q3_SNAPSHOT_DISCOVERY_20261118.md) |
| Raw evidence | `E:\HarborOps-Verification\g18\evidence\5cc3fa583f638236d824a47c2c65e9615cf4ae3e\final` and `g18-final-evidence.tar.gz` |

Hashes to reproduce: manifest `a8f0b0840abfa804de339a2a42d49a3f9fbadddf2d34aa85f4a2a28cf2f50d48`, ledger `ef829201ea1115baf8e315acb53288eb1afeb548c504564697b357e5d874a674`, archive `20680ed8bcb741a9fb547dcbe3564db0ec886f9aa641397f648b657fe99631ea`.

## 3. Suggested independent checks

1. Verify every file against `sha256-manifest.txt`, then confirm the three hashes above.
2. Confirm `git diff --stat fb1b42e5 5cc3fa58 -- src web` is empty and that the other differences are the three files named in the verification record.
3. Rerun `node scripts/check-m18-source.mjs` and `node scripts/reconcile-g18-assertions.mjs` against the evidence folder and compare the ledger.
4. Open a sample of raw artifacts for a few assertions in each layer: the TRX for the real-database tests, a Playwright report per engine, the journey timeline, one mutation log with its failing test, and the paired baseline logs.
5. Re-execute the M18 integration suite on a fresh disposable database from the documented runner and compare the totals (73 tests).
6. Review the migration for the grants, row-level security, transition guard, immutability and audit taxonomy, and the role provisioner for the runtime privileges.

## 4. Exceptions needing a disposition

| ID | Exception | Evidence |
| --- | --- | --- |
| X1 | Two neighbour regression failures classified as baseline-identical and the aggregate regression stage recorded PASS on that judgement | `regression-classification.json`, `logs/baseline-classification` |
| X2 | 19 tests skipped by design (crash-probe harness, query plan evidence, two load classes) | `regression-classification.json` |
| X3 | G18-S24-A01 recorded NOT_APPLICABLE; the reviewer should concur or require a snapshot | Q3 record |
| X4 | Stages bound to three revisions whose product source is identical | `provenance.json` |
| X5 | Incidents for `notifications` and `tracking_held` cannot be resolved; policy decision needed under M18-P16 and P17 | audit record section 5 |
| X6 | Best-effort secret detection in free-text notes (P09) | policy register |
| X7 | A broad baseline comparison with 132 unexplained failures was discarded | `logs/baseline-classification/baseline-a.log` |
| X8 | G17's separately signed S25-A03 and F06 instrument and its P09 qualified performance remain open and are not resolved by M18 | G17 records |
| X9 | The WebKit three-pixel overflow of the account page at 375 px, outside M18 | verification record |

## 5. Decision record (to be completed by the reviewer)

| Field | Value |
| --- | --- |
| Reviewer identity and independence statement | not recorded |
| Date | not recorded |
| G18-S25-A01 | pending |
| G18-S25-A02 | pending |
| G18-S25-A03 | pending |
| Recommendation (PASS or NOT PASS) with source and evidence hashes | not recorded |
| Explicit G18 acceptance decision and authority | not recorded |
