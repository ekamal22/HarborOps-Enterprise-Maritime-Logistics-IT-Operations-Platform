# M16 G16 Verification Plan

**Status:** Verification matrix retained as the original requirements. A source-locked standalone campaign was executed on `e571157` with a reported 30/30 standalone scenario disposition; source evidence and caveats are in [executed verification](M16_EXECUTED_VERIFICATION.md). Independent evidence review and G16 acceptance pending. **Approved architecture/policy:** 2026-11-04.

Use a fixed source SHA, disposable E:-backed databases and isolated Compose project/ports. Preserve raw TRX, SQL probes, HTTP results, sanitized logs, Playwright artifacts for Chromium/Firefox/WebKit, test exit codes, environment, SHA-256 manifest and independently reviewed evidence. A failed, blocked or skipped case is not PASS.

| ID | Required executable evidence |
| --- | --- |
| G16-S01 | Accepted policy, event taxonomy, retention and redaction mapping agree with code. |
| G16-S02 | Fresh schema migration creates correct tables, indexes and separate roles. |
| G16-S03 | Accepted M8 to M16 upgrade preserves immutable receipts and source keys. |
| G16-S04 | EF snapshot and deployed migration model match; no unsafe migration drift. |
| G16-S05 | Sensitive successful mutation creates correct audit row in same transaction. |
| G16-S06 | Injected audit failure rolls back the sensitive business mutation. |
| G16-S07 | Business validation/rejection/rollback never emits a false successful audit row. |
| G16-S08 | Concurrent mutations and interruptions preserve atomic audit/business pairing. |
| G16-S09 | Direct runtime SQL UPDATE and DELETE fail without changing audit rows. |
| G16-S10 | Runtime TRUNCATE fails via privileges/statement-level guard; rows survive. |
| G16-S11 | API and worker roles cannot run DDL or disable audit protections. |
| G16-S12 | Migration role can migrate while API and worker grants remain minimal. |
| G16-S13 | Active selected-org Auditor can list only that tenant's entries. |
| G16-S14 | Active Auditor can read tenant-owned detail with response allowlist. |
| G16-S15 | Analyst, Manager, Support and Administrator cannot view audit entries. |
| G16-S16 | Foreign organizations and known foreign audit IDs reveal no existence/content. |
| G16-S17 | Multi-org Auditor role never grants visibility outside selected org. |
| G16-S18 | Role revoke, membership/org suspend and session/account revoke deny stale access. |
| G16-S19 | Lock/revocation races cannot bypass access or commit unauthorized changes. |
| G16-S20 | Anonymous, CSRF/Origin spoof and transport trust probes are denied. |
| G16-S21 | Denied mutations have no business effects and bounded denial auditing. |
| G16-S22 | Denial-audit failure/outage never converts a forbidden action into success. |
| G16-S23 | Secret canaries absent in DB, HTTP, logs, browser artifacts and evidence. |
| G16-S24 | Unknown/oversized metadata and malicious filters safely rejected/bounded. |
| G16-S25 | Audit list/detail access is itself security-relevant without recursive logging. |
| G16-S26 | Pagination stable; query/denial rate and input abuse bounded. |
| G16-S27 | No audit HTTP write/export endpoint; M12 export cannot bypass role rules. |
| G16-S28 | Operator/source actor identity correct; M8 replay/immutability unaffected. |
| G16-S29 | Real database outage/recovery preserves committed audits and fails closed. |
| G16-S30 | Three browser engines, affected M0-M8 regression and integrated M9 checks pass. |

Prove guards with controlled fault injection, including missing audit writes, removed tenant predicates, widened permission, removed TRUNCATE protection and unredacted metadata. The 365-day policy does not authorize automatic deletion before a separately verified archival capability. Reconcile each scenario with raw evidence and source revision; no gate closure based on documentation or unit tests alone. Independent evidence review and explicit G16 approval are mandatory.

## Execution reconciliation, 2026-11-05

The recorded final run passed 205/205 unit, 39/39 contract, 252/252 PostgreSQL integration twice, 13 effective fault-injection mutants, 74/74 HTTP assertions, 14/14 database stop/restart checks and 61/61 browser tests per engine. A separate final-revision probe passed 6/6 for S07, S08 and the operator-identity portion of S28; it was not a runner stage. The published S01-S30 matrix marks all 30 PASS for standalone M16, but S30's integrated M9 clause is satisfied only by a partial disposable worktree diagnostic. M9/M16 combined Compose, browser and outage checks remain unexecuted. The metadata-size CHECK and late-commit cursor ordering are also documented limits. Independent review must judge each limitation against this matrix before acceptance. See [G16 executed verification](M16_EXECUTED_VERIFICATION.md).
