# G16 findings register

Findings are numbered in order of discovery. Severity reflects impact on the M16 candidate or on verification. Root causes are marked confirmed or hypothesis.

## G16-F001: Generated execution graph is stale; the G16 runner fails at its second stage

- **Severity:** Medium (documentation defect that blocks the whole scripted campaign; Low as a product defect)
- **Component:** `docs/EXECUTION_GRAPH.md`, `scripts/verify-m16-local.sh` stage `docs`
- **Observed:** `node scripts/render-execution-graph.mjs --check` exits 1 with "Execution graph view is stale". The runner stops after `preflight` (exit 0) and `docs` (exit 1); attempt 01 ran no further stage.
- **Expected:** The committed generated view matches `docs/execution-graph.json`, so the runner proceeds.
- **Reproduction:** from a clean checkout of `ef08d7c`, `node scripts/render-execution-graph.mjs --check`.
- **Evidence:** `E:\HarborOps-Verification\evidence\m16\evidence\docs.log`, `stages.tsv`.
- **Root cause (confirmed):** Commit `bb5015d` updated the M16 entries in `docs/execution-graph.json` (architecture decisions line and G16 acceptance evidence line) without regenerating `docs/EXECUTION_GRAPH.md`. A regeneration in a scratch copy changes exactly two lines (789 and 818) and nothing else.
- **Impact:** Verification completeness. The runner cannot reach any technical stage from the committed source. `check-planning-docs.mjs` passes separately on a clean `git archive` of the revision (25 milestones, 126 graph nodes, 107 Markdown files).
- **Recommended correction:** Regenerate and commit `docs/EXECUTION_GRAPH.md` on the implementation branch, then rerun on the new revision. Consider making the docs stage run the planning check even if the graph check fails.
- **Retest:** `render-execution-graph.mjs --check` exits 0 on the corrected revision; the full runner proceeds past `docs`.
- **Handling in this campaign:** Source stays frozen. Attempt 02 uses a derived runner that tolerates this one stage failure so the remaining stages can run; its evidence is labelled derived.

## G16-F002: Audit foreign key to organizations inverts the accepted lock order; 12 regression tests fail

- **Severity:** High
- **Component:** `20261104180000_AuditEntries` (`audit_entries.organization_id REFERENCES organizations(id)` plus `m16_audit_capture()` triggers on `organization_memberships`, `integration_sources`, `integration_credentials` and other organization-scoped tables)
- **Observed:** Integration stage on `ef08d7c`: 247 tests, 235 passed, 12 failed (attempt 02, 16 m 37 s). A rerun of only the four affected classes (attempt 03) failed the identical 12 of 53, so the failure is deterministic. Two tests (`TrackingInboxLockRaceTests.OverlappingRevocationDisableAndSuspensionRespectCommitOrder` for `source` and `credential`) fail with PostgreSQL `40P01 deadlock detected` in `TrackingInboxStore.ReauthorizeAsync`. Ten queued-request tests (`AssignmentQueuedSecurityTests`, `ShipmentExtendedGateTests`, `MaritimeRevocationInterleavingTests`, variants `membership`, `downgrade`, `member_removed`, `auditor`) hit a 32 to 36 s read timeout on the revoking `UPDATE`.
- **Expected:** M2 to M8 revocation and lock-ordering behavior is unchanged (accepted regression baseline): a membership, source or credential revocation commits independently of a transaction that holds the organization row.
- **Root cause (confirmed by experiment):** The audit insert performed inside the trigger runs the foreign key check `SELECT 1 FROM ONLY organizations x WHERE id = $1 FOR KEY SHARE`, which conflicts with a concurrent `SELECT ... FROM organizations FOR UPDATE`. Every audited write to an organization-scoped table therefore waits for, or deadlocks with, any transaction holding the organization row, reversing the order "organization first, dependents second" used by the application. Experiment on a disposable database (`probes/lock1-*.txt`, `probes/lock2-*.txt`): with the blocker holding the organization `FOR UPDATE`, `UPDATE organization_memberships` failed with `canceling statement due to lock timeout` and context `FOR KEY SHARE OF x ... INSERT INTO public.audit_entries ... m16_audit_capture()`; `UPDATE users` (its audit row has no `organization_id`) completed in 8 ms; and with trigger `m16_memberships_audit` disabled the same membership update completed in 3 ms. This matches why only the variants that touch organization-scoped rows fail.
- **Impact:** Correctness and reliability regression of accepted M2/M5/M6/M8 behavior and of G16-S08 and G16-S19 (races between lock and revocation). In production, revocations and source or credential disablement can stall behind long business transactions, and concurrent tracking intake can abort with deadlocks. The failures fail closed; no authorization bypass was observed.
- **Recommended correction:** Remove the lock edge, for example drop the foreign key (keep a validated `organization_id` column with a non-locking check), or reference the organization in a way that does not take `FOR KEY SHARE` on its row, or acquire locks in the accepted order in every audited path. Reconcile and rerun the accepted lock-race suites.
- **Retest:** All 12 listed tests pass in the full integration run (twice), plus a new test that holds an organization lock while an audited write commits.
- **Evidence:** `E:\HarborOps-Verification\evidence\attempt-02\m16\evidence\integration.log` and `trx\g16-integration.trx`; `attempt-03\m16\evidence\integration-failed-subset.log`; `probes\`.

## G16-F003: Offset pagination over a log that appends on every read returns duplicates

- **Severity:** Medium
- **Component:** `AuditReader.ListAsync`, `AuditEndpoints` list route
- **Observed:** Page 1 and page 2 (limit 5) requested consecutively by the same Auditor overlapped by one row (`probes/http-results.json`, S26).
- **Expected:** G16-S26: pagination is stable.
- **Root cause (confirmed):** The list uses `Skip((page-1)*limit)` ordered by `occurred_at_utc DESC`. Each successful read appends an `audit.read` row at the head of the same ordering (the S25 behavior), shifting every later offset by one between calls.
- **Impact:** Auditors paging through evidence see repeated rows and can miss rows under concurrent writes.
- **Recommended correction:** Keyset pagination on `(occurred_at_utc, id)` with an opaque cursor.
- **Retest:** Fetch consecutive pages while writes occur; the union of pages has no duplicates or gaps.

## G16-F004: Audit entries lack the semantic content required by the privacy policy

- **Severity:** Medium
- **Component:** `m16_audit_capture()`, `AuditActorContext`
- **Observed:** Event types are `<table>.<operation>` (for example `organization_memberships.update`), metadata is always `{"operation": "<op>"}`, `actor_role` is NULL for every trigger-captured row (the setting `harborops.audit_actor_role` is read by the trigger but never set by any code path), and `IntegrationSource` is never produced as an actor kind. After an HTTP membership role change the row was `organization_memberships.update`, actor kind Human, actor role NULL, correct actor user, metadata `{"operation": "update"}`. The planned `src/HarborOps.Domain/Audit/AuditEventPolicy.cs` does not exist.
- **Expected:** `docs/M16_AUDIT_PRIVACY_POLICY.md`: event taxonomy, actor role, changed-field names, bounded outcome codes; G16-S01 requires taxonomy and redaction mapping to agree with code; G16-S28 requires correct operator and source identity.
- **Root cause (confirmed):** Trigger-based capture records only table and operation. A role change and a status change are indistinguishable.
- **Impact:** Completeness of audit evidence (S01, S05, S28). Privacy is preserved (no values stored) but investigative value is low.
- **Recommended correction:** Populate actor role, event-specific codes and changed-field names from the application, or implement the planned event policy; document the mapping.
- **Retest:** SQL witness of event type, actor kind, actor role and changed fields for each audited operation, including the integration source path.

## G16-F005: M16-specific automated tests do not prove the restricted-role guarantees

- **Severity:** Medium (verification coverage)
- **Component:** `tests/HarborOps.IntegrationTests/Audit*.cs`, `scripts/check-m16-db-roles.sh`
- **Observed:** M16 adds 2 unit, 5 integration and 3 browser tests for 30 scenarios. The SQL immutability test runs as the database owner and superuser, so it proves the trigger guard (SQLSTATE 23514) but not the runtime grants. There are no tests for concurrent audit and business atomicity (S08), runtime-role UPDATE, DELETE, TRUNCATE or DDL (S09 to S11), roles other than Administrator in the HTTP matrix, foreign audit ids, session and account revocation, pagination, rate limits, secret canaries, database outage (S29) or denial-audit failure (S22). `check-m16-db-roles.sh` is a grant-contract probe only.
- **Impact:** A green runner would not have closed G16. This campaign substituted independent probes (`probes/`), which passed the runtime-role cases.
- **Recommended correction:** Add the missing executable tests to the implementation or an agreed verification suite.

## G16-F006: Migration identity is a PostgreSQL superuser

- **Severity:** Low
- **Component:** `compose.g16.verify.yaml`, `compose.yaml`, role design (M16-D04)
- **Observed:** The migration login `harborops` has `rolsuper`, `createdb`, `createrole`, `replication` and `bypassrls` (`probes/roles-harborops.txt`). It can disable guards, drop `audit_entries` and insert audit rows directly.
- **Expected:** M16-D04 asks for a least-privilege migration identity; M16-D05 excludes privileged administrators from the tamper boundary.
- **Impact:** Consistent with the documented boundary, but the migrator is the cluster superuser, so "separately privileged" holds only for the API and worker roles.
- **Recommended correction:** Document the limitation or use a dedicated non-superuser table owner for migrations.

## G16-F007: Authorized read fails closed with a revocation code; denial rows are bounded only by the request limiter

- **Severity:** Low
- **Observed:** With `EXECUTE` on `m16_record_audit_access` revoked from the API role (fault injection on a disposable database) an authorized Auditor read returned `403 organization_access_revoked` (SQLSTATE 42501 is mapped to revocation) rather than the 503 that M16-D03 specifies for unavailable persistence; a forbidden caller still received 403. A user without audit rights wrote 120 denial rows in one minute before the 120 per minute limiter returned 429.
- **Impact:** Misleading error code; tenant audit volume can grow by 120 rows per minute per user.
- **Recommended correction:** Map storage failures to a sanitized 503, and add a per-user or per-session cap or coalescing for denial rows.
