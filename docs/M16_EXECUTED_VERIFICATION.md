# M16 / G16 Executed Technical Verification

**Recorded:** 2026-11-05  
**Scope:** Standalone M16 candidate. This is an executed technical-verification record, not independent raw-evidence acceptance.  
**Verified source:** `e5711573839b185a2dfc7d9f2c64ce4fc18d4c13` on `feat/m16-audit-access-hardening`.  
**Acceptance:** G16 acceptance pending. No merge to `main` and no production deployment authorized.

## Source and evidence provenance

The final corrected G16 campaign ran 2026-11-04 21:48:51Z to 22:59:24Z, using the committed, unmodified `scripts/verify-m16-local.sh` runner in a clean detached source worktree. The runner reportedly exited 0 and the standalone scenario reconciliation records 30/30 PASS, with an explicit integrated-M9 limitation in G16-S30. The initial failed campaign at `ef08d7c91d368f082fc97bac32acab1fdbd5cfca` remains preserved and is not substituted for the corrected run.

Authoritative published reports from the local campaign:
- [Corrective final technical report](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/04-final-report.md).
- [G16-S01 through G16-S30 evidence matrix](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/03-scenario-matrix.md).
- [Corrections and retest register](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/01-corrections-and-retest.md).
- [M9 compatibility diagnostic](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/02-m9-compatibility-refresh.md).

Private raw evidence remains at `E:\HarborOps-Verification\evidence\new-campaign\m16`. Its reported SHA-256 manifest has 162 entries and digest `a9a13a6abf4f6b05ca305affb020c8237f12f81ed65de7cda5337fab3fa72671`. The original raw G16 evidence has **not** been independently re-parsed in this review. Documentation checks and summaries must not be presented as a replacement for that review.

## Reported standalone results

| Executed area | Result on frozen M16 source |
| --- | --- |
| Clean build | Passed with warnings treated as errors |
| Unit tests | 205/205 |
| Contract tests | 39/39 |
| PostgreSQL integration | 252/252, repeated 252/252 |
| Web unit tests and build | 18 passed; production build passed |
| Fault-injection security mutations | Clean control passed; all 13 mutants killed by intended tests |
| Audit HTTP authorization matrix | 74/74 |
| Real PostgreSQL stop and restart | 14/14, read availability recovered without API restart |
| Browsers | Chromium 61/61, Firefox 61/61, WebKit 61/61, no reported skips or flaky cases |
| Dependencies and images | No vulnerable NuGet packages reported, npm audit clean, no HIGH/CRITICAL findings in three Trivy image scans |
| Additional final-SHA probe | 6/6, covering S07, S08 and the operator-identity portion of S28 |
| Teardown | Runner-relevant containers, volumes and networks reportedly removed |

The 12 previously failing lock/revocation regression tests passed in both integration runs; the new foreign-key inversion mutant was killed. The runner also exercised role denial, append-only TRUNCATE/DELETE guards, audit failure rollback, cross-tenant read isolation, safe actor attribution and cursor pagination.

## Scenario and acceptance boundaries

The published matrix labels G16-S01 through G16-S30 PASS **for the standalone M16 revision**. However, S07, S08 and part of S28 were supplied by an additional final-SHA probe, not a stage of the standard runner. The unauthorized S07 probe used no membership, producing 409; role-denied behavior comes from the prior accepted suites. S24's metadata size CHECK was not exercised separately. S26's ordering is stable for the committed pre-walk snapshot, but late commits with older statement timestamps can fall behind an issued cursor.

G16-S30's M9 integration element has **not** passed a complete combined gate. A local diagnostic worktree combining M16 `e571157` and M9 `7665bc4` reportedly built with no warnings and passed 228 unit, 39 contract and 273 integration tests after fixing five hard-coded migration-count expectations. That worktree was not pushed and neither original branch was modified. Combined Compose, browser, outage and full source-locked G9/G16 campaigns were not run. M9 subsequently advanced beyond the tested diagnostic base, so current-head compatibility must be checked again.

## Decisions and residual risks

1. Independent review of the raw G16 stage logs, TRX, role probes, security mutants, browser results, scenario evidence and 162-file manifest is pending.
2. The migration identity remains a PostgreSQL superuser. M16-D04/D05 explicitly exclude superuser tampering from the runtime-role guarantee; whether to use a restricted migrator/table owner is a separate decision.
3. The unreleased `20261104180000_AuditEntries` migration was edited in place during corrections. A database created from the earlier unmerged migration would require a controlled migration/rebuild decision. No deployed database is claimed.
4. M9/M16 integration still has two consistent but duplicate worker-role provisioning paths. Choose one authority for runtime grants.
5. Decide whether M9 worker effects and work transitions require additional M16 audit entries or whether M9's immutable attempt/effect history is sufficient.
6. Preserve the M9/M16 shared EF snapshot, migration order and updated assertions. Re-run combined service, browser, outage and relevant gate checks on the eventual integration SHA.
7. The 365-day online retention target remains a policy. Archival, disposal and cryptographic anchoring are not implemented or approved.
8. The final G16 run reported no owned leftover runtime resources. The decline in C: free space was not attributable to any identified campaign file; no deletion is authorized by this record.

## Disposition

**Standalone technical verification: passed as reported on `e571157`. Independent raw-evidence review: pending. Integrated M9 verification: partial diagnostic only. G16 acceptance and merge: pending.**

Do not rewrite the earlier failed attempt, auto-accept the gate based on runner exit 0, or claim hosted-production clearance.
