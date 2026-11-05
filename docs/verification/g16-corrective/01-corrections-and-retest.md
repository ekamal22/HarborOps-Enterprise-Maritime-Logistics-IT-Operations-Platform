# G16 corrective campaign: corrections and retest status

**Status:** Complete for the standalone M16 revision. G16 acceptance pending. The original failed campaign report in `docs/verification/g16/` is preserved unchanged.

## Revisions

| Role | Revision |
| --- | --- |
| Initial failed verification | `ef08d7c91d368f082fc97bac32acab1fdbd5cfca` |
| Corrective handoff | `14d3f142d37970f52512015784228d5bbc5ccf1f` |
| Final tested source (source-locked campaign) | `e5711573839b185a2dfc7d9f2c64ce4fc18d4c13` |
| Corrections added during this assignment | `b9ada8f` (downgrade test and cursor test), `ec0cc5e` and `ac9f977` (HTTP and outage stages), `49e3cb6` (role, stale-context and metadata tests, provisioner database name, mutation runner), `e571157` (mutation stage in runner) |

All commits carry `[skip ci]`. Nothing was merged. The M16 branch contains no assistant attribution.

## Original findings: audit of the committed corrections

| ID | Original defect | Independent verdict on the correction | Evidence |
| --- | --- | --- | --- |
| G16-F001 | Stale generated execution graph stopped the runner | Fixed. `render-execution-graph.mjs --check` passes and the `docs` stage passes | final campaign stage `docs` exit 0 |
| G16-F002 | Audit FK took `FOR KEY SHARE` on `organizations`, inverted lock order | Fixed. FK removed; tenant identifier is an immutable historical column with indexes. All 12 failing tests pass; new regression test holds the organization row `FOR UPDATE` while an audited write commits; reintroducing the FK is killed by exactly that test | focused run 93 of 94 on `14d3f14` (the one failure was a different, new test defect, F011); final integration 252 of 252 twice; mutant `audit-fk-lock-inversion` killed |
| G16-F003 | Offset paging repeated rows | Fixed. Keyset cursor `(occurred_at_utc, id)` bound to the organization. Walk under concurrent writes has no duplicates and no gaps against the pre-walk snapshot; tampered, cross-organization, oversized and malformed cursors return 400; `page` other than 1 returns 400 | `AuditCursorTests` (new, added by this assignment); HTTP stage S26; mutant `cursor-ignores-organization` killed |
| G16-F004 | Audit rows lacked taxonomy, actor role, changed fields, source identity | Fixed within the approved design. Role change produces `membership.role_changed`, actor `Human`, actor role `Administrator`, `changedFields ["role"]`; metadata keys are exactly `operation` and `changedFields`; no values stored. Accepted tracking events are attributed to the `IntegrationSource` and never inherit a human or CLI context | HTTP stage S01/S05/S28; `AuditHardeningTests` (stale context and allowlist); mutants `stale-human-context` and `unredacted-metadata` killed |
| G16-F005 | M16 tests did not prove restricted-role guarantees | Closed by this campaign. Added: runtime-role denial test under the real provisioned roles (33 denial statements, SQLSTATE 42501 required), runner role-denial matrix with `SET ROLE` and SQLSTATE checks, HTTP authorization matrix (74 checks), real PostgreSQL stop and restart stage (14 checks), source-locked fault-injection stage (13 mutants and a control) | final campaign stages `restricted-db-roles`, `audit-http-matrix`, `audit-outage`, `security-mutations` |
| G16-F006 | Migration identity is a PostgreSQL superuser | Not changed. Documented limitation consistent with M16-D05; whether to introduce a non-superuser table owner is a decision, acceptance pending | role probe output |
| G16-F007 | Storage failure mapped to a revocation code; denial volume | Fixed. Authorized read with failed audit recording returns `503 audit_unavailable`; forbidden callers stay 403; limiter lowered to 30 requests per minute per user; one user wrote at most 30 denial rows in a window | HTTP stage S22, S21/S26 |
| G16-F008 | Audit browser spec raced the organization selection | Fixed in the spec (awaits the selection response). Browser result in the scenario matrix | final campaign: Chromium, Firefox and WebKit 61 of 61 each, audit spec included |
| G16-F009 | Runner provisioned wrong browser member names | Fixed (accepted display names restored) | final campaign browser stage, organization-revocation passes on all three engines |
| G16-F010 | Evidence controls: traces, teardown label | Fixed. Traces are moved to `private/quarantine` with a private manifest; leftover checks use container name prefix. Quarantine block executed against synthetic traces: zips moved, other artifacts stay, safety scan passes | synthetic test; final teardown clean (no containers, volumes or SDK database left), evidence safety passed on 162 files |

## New findings in this campaign

| ID | Severity | Finding | Root cause | Fix and retest |
| --- | --- | --- | --- | --- |
| G16-F011 | Medium (test) | `TrackingInboxAuditSqlTests.PopulatedM8DowngradeIsRejectedAndPreservesImmutableHistory` failed on `14d3f14` (expected 9 applied migrations, observed 10) | The new audit trigger on `tracking_accepted_events` writes an audit row at head, so the M16 downgrade guard (retained audit evidence) fires before the M8 guard; the test populated M8 data after migrating to head | Test populates M8 data before the M16 migration, as in a real upgrade (`b9ada8f`). The M16 guard keeps its own coverage in `AuditMigrationTests`. Test passes; no guard weakened |
| G16-F012 | Low | `AuditRoleProvisioner` hard-coded `GRANT CONNECT ON DATABASE harborops` | Provisioner could not run against any other database name, so it was untestable in the integration database | Uses `current_database()` (`49e3cb6`); role-isolation test now runs the real provisioner |
| Observation | Info | The M16 migration was edited in place (`20261104180000_AuditEntries`) rather than adding a second migration | Branch is unmerged and unreleased | Fresh and M8-to-M16 upgrade paths pass; a deployed pre-correction database would need a rebuild, acceptance pending |
| Observation | Info | Audit ordering uses statement start time; a transaction that commits late can insert a row older than a cursor already issued | Inherent to time-ordered live logs | Walk is stable for rows committed before the walk starts; documented limit |
| Observation | Info | The rate limit applies to denied requests too, and the HTTP stage budgets its own reads to stay under 30 per minute | By design | none |
