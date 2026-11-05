# M16 Approved Audit and Access Policy Decisions

**Approved:** 2026-11-04. **Scope:** M16 architecture and five local policies. **Implementation:** Corrected candidate at `e571157` with reported passing standalone technical verification. **Gate G16:** Main 162-file evidence reviewed, **S07/S08/S28 supplemental log and JSON REVIEWED** ([record](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/0bf356dba78e51a6aafdeb665870ffa24789b3d9/docs/M16_G16_SUPPLEMENT_EVIDENCE_REVIEW.md)), combined M9 tests and acceptance pending.

These M16-local IDs do not replace the project-wide D-001 through D-008 decision register. Accepted M0-M8 behavior remains authoritative.

## M16-D01: Audit visibility
**Accepted:** Audit reading is restricted to a currently active **Auditor** membership within the session's actively selected organization. Administrator, Support, Manager and Analyst have no implicit audit access. Revalidate live session, membership, organization state and requested resource scope. An Auditor role in one organization conveys no rights in another. No global audit search or audit export is approved. Audit reads themselves are security-relevant.

## M16-D02: Retention
**Accepted:** **365 days online**, followed by controlled archival. No automatic deletion or hard purge until archival and integrity preservation are separately implemented and verified. Incident/legal holds override routine disposal. This policy does not shorten M8 permanent source-key reservation or accepted-event history.

## M16-D03: Persistence failure
**Accepted:** Required audit entries for sensitive successful business mutations commit in the **same PostgreSQL transaction** as the mutation. Failure to append means rollback and sanitized non-success, normally 503 for unavailable persistence. Unknown commit outcome is never reported as success. Denied requests remain denied even if their separately attempted denial-audit insert fails. No audit failure may convert denied access into allowed access. Preserve M8 atomic receipt/event/work guarantees and M9 effect integrity.

## M16-D04: Database roles
**Accepted:** Separate least-privilege **migration, API runtime and M9 worker** PostgreSQL identities. Runtime roles receive no DDL, table ownership or authority to disable append-only guards. Audit UPDATE, DELETE and TRUNCATE must be denied with grants and correct guards; a row-level DELETE trigger alone does not block TRUNCATE. Existing shared local Compose credentials require a backward-compatible bootstrap/migration plan and disposable-database rehearsal. PostgreSQL superusers and privileged schema owners remain outside database-only tamper resistance.

## M16-D05: Cryptographic anchoring
**Accepted:** Database-enforced append-only storage, restricted privileges and integrity checks are in scope. External cryptographic anchoring/witnessing is deferred. Do not claim absolute tamper resistance against privileged database administrators.

## Delivery and acceptance boundary
Follow [architecture](M16_ARCHITECTURE_DESIGN.md), [privacy](M16_AUDIT_PRIVACY_POLICY.md), [implementation](M16_IMPLEMENTATION_PLAN.md) and [G16 matrix](M16_VERIFICATION_PLAN.md). Parallel M9/M12/M14/M17/M19 work remains eligible with coordinated shared files and migration order. Policy approval by itself is not implementation or evidence acceptance. The subsequent standalone technical campaign is recorded in [executed verification](M16_EXECUTED_VERIFICATION.md); the [independent main-campaign evidence review](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/m9-m16-repowide-reconciliation-20261105/docs/M16_G16_INDEPENDENT_EVIDENCE_REVIEW.md), the [reviewed S07/S08/S28 supplemental evidence](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/0bf356dba78e51a6aafdeb665870ffa24789b3d9/docs/M16_G16_SUPPLEMENT_EVIDENCE_REVIEW.md), integrated M9 checks, G16 acceptance and authorization to merge into protected `main` remain separate.

### Open integration choices, not reversals of approved M16 policy

The migration login is currently a PostgreSQL superuser, consistent with the documented exclusion of superusers from the runtime tamper threat boundary; adopting a restricted migration owner remains a separate decision. An earlier unreleased M16 migration was edited in place, and any database initialized from that version needs explicit reconciliation. Choose one authoritative M9 worker provisioning path and clarify whether M9's immutable attempt/effect records alone satisfy M16-D03 or additional audit entries are required. These questions are acceptance pending and do not alter M16-D01 through M16-D05.
