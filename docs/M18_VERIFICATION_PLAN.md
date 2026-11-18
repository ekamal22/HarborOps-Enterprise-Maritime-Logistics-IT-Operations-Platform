# G18 Source-Locked Verification and Independent S25 Acceptance Plan

**Prepared:** 2026-11-18  
**Status:** PLANNED, NOT EXECUTED. G18 NOT ACCEPTED.  
**Baseline:** `72251f5310f24fb98322b7eb403a5e9e66e040ef`. Actual verification must freeze its later exact implementing SHA and Git tree.  
**Contract:** 25 mandatory scenarios, 75 atomic assertions, of which 72 require technical witnesses and 3 in S25 require independent review.  
**Policy gate:** M18-P01..P20 APPROVED 2026-11-18. This does not authorize G18 acceptance or deployment.

## Gate discipline

Every G18-S01..S25 has three distinct A01..A03 assertions. A scenario title or green suite summary cannot stand in for any required witness. A single no-skip campaign must cover the exact integrated implementing source, and any supplemental evidence must be explicitly bound to its corresponding source, not silently substituted. Preserve PASS/FAIL/BLOCKED/NOT_EXECUTED as actually observed. **Only G18-S24-A01 may be NOT_APPLICABLE**, and solely with raw proof that no representative retained historical snapshot exists; the scenario's other two assertions remain mandatory. Any unresolved mandated assertion keeps technical acceptance open.

G18-S25 is not automatically satisfied by test output. Independently authenticate the source and evidence, review raw failures and exceptions, record a signed recommendation, then obtain a separately explicit G18 acceptance decision. An implementation PR merge is not the same as G18 acceptance. Preserve G17's distinct signed S25-A03/F06 obligation and P09 performance qualification without claiming M18 resolves them.

## Required 25-case matrix

| Scenario | Theme | Layer | Minimum raw witness |
| --- | --- | --- | --- |
| G18-S01 | Policy and source conformity | Policy / source | Approved M18-P01 through P20, exact graph prerequisites, source inventory and no unauthorized changes |
| G18-S02 | Five-role permission enforcement | Role / HTTP | Real authenticated HTTP, five-role positive/negative matrix and typed permission unit tests |
| G18-S03 | Tenant object isolation | Security / HTTP / SQL | Two synthetic organizations; guessed incident, note, action, evidence IDs and raw SQL boundary |
| G18-S04 | Session, organization and revocation | Identity / race | Live account/session/membership/organization suspension plus transaction-race probes |
| G18-S05 | Creation, severity and input bounds | Domain / HTTP | Creation and validation tests, malicious and over-limit payloads |
| G18-S06 | Seven-state positive transition chain | Domain / SQL | State-machine unit, HTTP and committed event sequence |
| G18-S07 | Illegal transition and bypass resistance | Negative / mutation | Invalid jump, generic patch, forged actor and state-guard mutants |
| G18-S08 | Concurrent writers and stale version | Race / PostgreSQL | Real competing connections, row locks, expectedVersion regression |
| G18-S09 | Idempotency and ambiguous retry | Replay / SQL / HTTP | Same operation key, changed payload, lost response and retry after restart |
| G18-S10 | Append-only incident history | DB grants / immutable | Direct SQL UPDATE/DELETE/TRUNCATE, EF mutation and migration tests |
| G18-S11 | M16 atomic audit coupling | Audit / transaction | Injected audit insertion failure and direct transaction observation |
| G18-S12 | Denied audit and database privilege | Security / SQL | Access denial with audit failure, runtime-role and migrator grants |
| G18-S13 | Assignment and severity reassessment | Domain / access | Active same-tenant assignee, reassignment, severity change and stale membership |
| G18-S14 | Investigation notes and confidentiality | Privacy / HTTP / browser | Bounded notes, XSS canaries, response/logger audit export inspection |
| G18-S15 | M17-safe evidence capture | Observability / privacy | Sanitized source snapshot and negative raw trace or URL attempts |
| G18-S16 | Corrective actions and root cause | Domain / SQL / browser | Versioned action state, independent cause record, owner and cancel failure |
| G18-S17 | Evidence-required resolution and closure | Recovery / validation | Contradictory recovery evidence, missing integrity proof and exception denial |
| G18-S18 | Reopen and repeated lifecycle | Domain / audit | Reopen after Resolved/Closed, concurrent reopen and state recovery |
| G18-S19 | Alert to manually confirmed incident | Integration / browser | Controlled M17 fault, safe alert preview and authenticated create |
| G18-S20 | Default-off telemetry and degradation | Operations / failure | Default Compose, collector down, stale/no telemetry and transport hold |
| G18-S21 | End-to-end fault, repair and integrity | E2E / fault | Real disposable readiness fault, logs/metrics and safe SQL checks |
| G18-S22 | HTTP API safety and pagination | Contract / adversarial | Request schema, CSRF/Origin, cursor tamper, rate limit and errors |
| G18-S23 | Three-browser English/Turkish functional UI | Browser / i18n | Chromium/Firefox/WebKit full auth workflows, locale, conflict recovery and org switch |
| G18-S24 | Q3 snapshot and accepted regressions | Compatibility / regression | Disposable historic safe-copy or reasoned N/A, impacted regression outputs and original hashes |
| G18-S25 | Independent S25 review and G18 decision | Independent / approval | Raw source and manifest, exception ledger, independent review document and formal decision |

## Atomic assertion definitions

### G18-S01: Policy and source conformity

**Witness:** Approved M18-P01 through P20, exact graph prerequisites, source inventory and no unauthorized changes

- **A01:** Every behavior-affecting M18-P01 through P20 choice is explicitly resolved and signed before affected code exists
- **A02:** M18-01..04 mapping, API/SQL/roles and test inventory match approved contracts on the frozen source
- **A03:** G16/G17 constraints, default-off gates and historical evidence remain unchanged

### G18-S02: Five-role permission enforcement

**Witness:** Real authenticated HTTP, five-role positive/negative matrix and typed permission unit tests

- **A01:** Support and Administrator within selected active organization can use exactly approved technical incident operations
- **A02:** Manager remains limited, Analyst denied, and Auditor is read-only only with explicit revocable M18 grant
- **A03:** Every permission is checked server-side; UI hiding and generic M16 audit grants cannot grant incident rights

### G18-S03: Tenant object isolation

**Witness:** Two synthetic organizations; guessed incident, note, action, evidence IDs and raw SQL boundary

- **A01:** List and detail return only selected-organization rows and allowed fields
- **A02:** Foreign incident/child IDs and protected cursors reveal no existence or data compared with unknown IDs
- **A03:** Composite tenant constraints prevent cross-organization notes, actions, evidence and assignment references

### G18-S04: Session, organization and revocation

**Witness:** Live account/session/membership/organization suspension plus transaction-race probes

- **A01:** Revoked, expired or disabled sessions reject protected operations immediately
- **A02:** Organization switch and membership/role revocation invalidate cached incident and cursor visibility
- **A03:** Revalidation inside write transaction prevents queued requests committing after revocation

### G18-S05: Creation, severity and input bounds

**Witness:** Creation and validation tests, malicious and over-limit payloads

- **A01:** A permitted create persists valid incident with New state, correct service, SEV-1..4 and UTC timestamps
- **A02:** Invalid severity, source, lengths, unknown fields and timestamps fail with zero side effects
- **A03:** Limited Manager creation omits forbidden technical fields and never conveys privileged detail access

### G18-S06: Seven-state positive transition chain

**Witness:** State-machine unit, HTTP and committed event sequence

- **A01:** New -> Triage -> Investigating -> Mitigated -> Monitoring -> Resolved -> Closed follows approved guards
- **A02:** Each accepted transition records exact previous/new state, actor, server UTC and incremented version
- **A03:** Current projection matches ordered immutable history and correct resolved/closed timestamps

### G18-S07: Illegal transition and bypass resistance

**Witness:** Invalid jump, generic patch, forged actor and state-guard mutants

- **A01:** Unapproved skips, terminal edits, duplicate closure and missing reason produce failure without state change
- **A02:** A generic mutation cannot overwrite state, actor, version, resolved or closed fields
- **A03:** Removing the transition guard is detected by an effective negative test

### G18-S08: Concurrent writers and stale version

**Witness:** Real competing connections, row locks, expectedVersion regression

- **A01:** Two concurrent updates from the same version commit at most one authorized state change
- **A02:** Losing request receives deterministic conflict without phantom event, note or audit
- **A03:** Role or assignee revocation racing the commit cannot publish unauthorized changes

### G18-S09: Idempotency and ambiguous retry

**Witness:** Same operation key, changed payload, lost response and retry after restart

- **A01:** An organization- and actor-scoped operation key makes initial create replay safe before any incident ID exists; exact matching replay produces no duplicate incident, event or audit effect
- **A02:** Same key with different payload is rejected without modifying incident or history
- **A03:** Unknown commit outcome is resolved through durable operation identity rather than claiming false success

### G18-S10: Append-only incident history

**Witness:** Direct SQL UPDATE/DELETE/TRUNCATE, EF mutation and migration tests

- **A01:** Authorized app can append events/notes/evidence but cannot edit existing entries
- **A02:** API/worker runtime roles cannot UPDATE, DELETE, TRUNCATE or disable guards on immutable tables
- **A03:** Historical timeline survives closure, reopen, API restart and migration upgrade unchanged

### G18-S11: M16 atomic audit coupling

**Witness:** Injected audit insertion failure and direct transaction observation

- **A01:** Every successful sensitive incident mutation atomically commits its exact approved allowlisted M16 audit event set, without missing or duplicate logical events
- **A02:** Required audit write failure rolls back projection and append-only incident records
- **A03:** Audit metadata excludes note body, raw diagnostic values and credentials while preserving safe provenance

### G18-S12: Denied audit and database privilege

**Witness:** Access denial with audit failure, runtime-role and migrator grants

- **A01:** An unauthorized request never changes incident state even when denial-audit persistence fails
- **A02:** Only migrator holds schema authority and API/worker cannot update/delete protected audit and history
- **A03:** No pre-existing accepted M16 audit API permission or privacy/retention invariant changes

### G18-S13: Assignment and severity reassessment

**Witness:** Active same-tenant assignee, reassignment, severity change and stale membership

- **A01:** Only authorized actors assign to active permitted same-organization technical responders
- **A02:** Reassignment or SEV change records reason, prior/new value, actor and version
- **A03:** Foreign, removed or unauthorized assignee and unauthorized severity changes produce no effects

### G18-S14: Investigation notes and confidentiality

**Witness:** Bounded notes, XSS canaries, response/logger audit export inspection

- **A01:** Permitted notes preserve authorship, UTC and immutable chronology under correct visibility
- **A02:** Secret, HTML, oversized, malformed and private-data canaries cannot leak via audit/log/metrics/HTTP/browser
- **A03:** Manager/Analyst/foreign users cannot infer protected note contents or private diagnostics

### G18-S15: M17-safe evidence capture

**Witness:** Sanitized source snapshot and negative raw trace or URL attempts

- **A01:** An allowed service/rule/observed UTC snapshot with correlation digest is attached only by explicit authorized command
- **A02:** Raw trace payloads, unsafe URLs, tenant/user identifiers, secrets and unbounded labels are rejected or omitted
- **A03:** Stored safe evidence remains meaningful after M17 telemetry expires and missing values remain unknown

### G18-S16: Corrective actions and root cause

**Witness:** Versioned action state, independent cause record, owner and cancel failure

- **A01:** Investigation records cause classification or explicit approved unknown and associated actions
- **A02:** Action state/owner/evidence changes append versioned immutable history without silent overwrites
- **A03:** Cross-tenant action links, direct deletion and invalid action transition fail cleanly

### G18-S17: Evidence-required resolution and closure

**Witness:** Contradictory recovery evidence, missing integrity proof and exception denial

- **A01:** Monitoring -> Resolved requires recorded health, recovery and data-integrity checks under approved policy
- **A02:** Resolved -> Closed requires an eligible independent confirmer for higher severity or an expressly approved audited exception, plus recorded corrective-action dispositions
- **A03:** Missing, stale or contradictory evidence and unauthorized exception prevent closure and leave current state unchanged

### G18-S18: Reopen and repeated lifecycle

**Witness:** Reopen after Resolved/Closed, concurrent reopen and state recovery

- **A01:** Explicit authorized reopen enters approved investigation state with reason and new version
- **A02:** Prior closure, actor and verification evidence remain visible and immutable
- **A03:** Duplicate reopen or stale-version replay cannot fabricate a second lifecycle event

### G18-S19: Alert to manually confirmed incident

**Witness:** Controlled M17 fault, safe alert preview and authenticated create

- **A01:** M17 alert alone creates no incident before explicit user confirmation
- **A02:** Human-confirmed incident stores only server-issued sanitized service observation; platform-level telemetry cannot claim another tenant's incident impact
- **A03:** Wrong-organization or forged M17 reference cannot create/link incident or grant telemetry access

### G18-S20: Default-off telemetry and degradation

**Witness:** Default Compose, collector down, stale/no telemetry and transport hold

- **A01:** Incident CRUD remains available when M17 export is disabled/unreachable if required database and audit are healthy
- **A02:** Missing collector, expired series or absent signal displays unknown, never verified recovery
- **A03:** M9 transport hold, M10/M11/M15 opt-ins and M17 private default-off profile remain unchanged

### G18-S21: End-to-end fault, repair and integrity

**Witness:** Real disposable readiness fault, logs/metrics and safe SQL checks

- **A01:** Controlled fault produces bounded observable signal and an explicitly opened incident with reproducible symptoms
- **A02:** Mitigation and service restoration show actual health and relevant data-integrity proof
- **A03:** Closed/reopened evidence connects to the same incident/correlation digest without customer-data leak

### G18-S22: HTTP API safety and pagination

**Witness:** Request schema, CSRF/Origin, cursor tamper, rate limit and errors

- **A01:** All versioned routes follow defined request/response enums, bounded payloads and stable scoped pagination
- **A02:** Unauthorized, forged origin, invalid cursor and oversize requests fail with sanitized consistent status/code
- **A03:** 503 or unknown commit outcome never becomes success, and retry-after-recovery preserves committed state

### G18-S23: Three-browser English/Turkish functional UI

**Witness:** Chromium/Firefox/WebKit full auth workflows, locale, conflict recovery and org switch

- **A01:** Support/Admin can create, transition, investigate, close and reopen in English and Turkish across three engines
- **A02:** Manager has only approved safe summaries, Auditor only a current distinct M18 read grant, Analyst denied; organization switching clears all previous-tenant data
- **A03:** Loading/empty/error/unknown/conflict and saved draft flows work without English-only text or hidden privilege changes

### G18-S24: Q3 snapshot and accepted regressions

**Witness:** Disposable historic safe-copy or reasoned N/A, impacted regression outputs and original hashes

- **A01:** Q3 representative historical snapshot boots, upgrades and integrity spot checks on disposable copy, OR exact NOT APPLICABLE evidence is recorded
- **A02:** Original retained volume is unchanged, and fresh/seeded migrations and runtime DB grants are correct
- **A03:** Impacted G16/G17 plus accepted business/notification/session regressions pass on exact candidate; no required skip

### G18-S25: Independent S25 review and G18 decision

**Witness:** Raw source and manifest, exception ledger, independent review document and formal decision

- **A01:** A distinct reviewer authenticates frozen source, hashes, 25 scenarios, 72 technical assertions and negative/security evidence
- **A02:** All policy, provenance, performance, Q3 and procedural exceptions are disclosed and dispositioned without editing failures
- **A03:** A genuinely separate reviewer signs a source- and evidence-bound PASS/NOT PASS recommendation; distinct explicit G18 acceptance and merge authorization remain necessary


The [decision-to-evidence crosswalk](M18_DECISION_TO_EVIDENCE_CROSSWALK.md) defines approval ordering, complete case-to-negative-control mapping and fixtures. [Proposed HTTP/UI examples](M18_API_AND_UI_CONTRACT_EXAMPLES.md) illustrate expected results without asserting that an endpoint exists.

## Explicit G18 evidence refinements

The [machine-readable inventory](M18_G18_ASSERTION_INVENTORY_20261118.json) now maps every scenario to its affected M18-Pxx policies, work package, required witness layers and named adversarial negative-control family. These are **planned controls**, not executed witnesses. The [security and transaction contract](M18_SECURITY_AND_TRANSACTION_CONTRACT.md) must be reconciled with approved policies before any implementing code.

G18-S02 must demonstrate the Auditor grant lifecycle separately from KPI/audit grants and Manager summary-only response semantics. G18-S09 must test incident creation replay before an incident ID is known, changed-payload conflict, permission revocation before replay, and ambiguous commit response. G18-S11 must inspect actual migration-owned M16 trigger events and actor context. G18-S15 and G18-S19 must reject forged service alerts and the assumption that service telemetry proves tenant impact. G18-S17 must distinguish observed recovery from subsequent independently confirmed closure without inventing a second actor. Each missing proof leaves its specific assertion NOT_EXECUTED or FAIL.

## Exact evidence contract

Each atomic assertion must map to a source-qualified test ID, raw exit status, structured result, fixture identifier, input/expected/actual fields, relevant negative witness and independently hash-verified artifact path. Grouping multiple tests is allowed only when every assertion is explicitly named and individually reconciled. Screenshots alone cannot substitute for SQL, audit or role grants, and SQL fixtures alone cannot substitute for browser state. Three-browser requirements mean Chromium, Firefox and WebKit; English and Turkish must each be verified for functional workflows, not only dictionary key existence.

| Evidence class | Minimum retained artifacts |
| --- | --- |
| Source/provenance | Full SHA/tree, clean tree status, diff and ownership, EF migration inventory, locks, tool versions, fixture and schema hashes |
| Policy | Signed P01-P20 register, transition and role truth tables, scope deviations and exception approvals |
| Unit/contract | Exact commands, stdout/stderr/exit, TRX/TAP, type/enum/API schema, mutation mappings |
| SQL/HTTP | Fresh and upgrade database, role grants, tenant FK/immutability, atomic M16 audit, request and response traces with redaction |
| Browser | Chromium, Firefox, WebKit traces and screenshots for English and Turkish, sanitized network evidence, role/organization switches |
| Runtime faults | Controlled fault timeline, M17 safe observation, database stop/restart, health/integrity proof, no implicit ticket activation |
| Q3 | Original checksum, disposable clone inventory and integrity or justified explicit NOT_APPLICABLE |
| Regression | Impacted G16/G17 and accepted upstream module reruns on final exact SHA, including M9 hold and M15 default-off |
| Evidence review | Complete expected-file manifest with SHA-256/bytes, cross-checks, original failures, reviewer notes and signed instrument |

No secret tokens, raw personal data, customer shipments, private raw telemetry, SQL parameters, full unrestricted notes or source credentials belong in artifacts. Original failures are immutable records; corrections create new attempts with new SHA and links.

## Required effective negative controls

At least one source-level or test-level effective control must detect each class: removed tenant predicate, expanded role grant, ignored session revocation after lock, bypassed state transition, lost version CAS, duplicate retry event, absent M16 required audit, mutable history, exposed note/trace data, unsafe arbitrary M17 URL, missing recovery proof, treating no telemetry as healthy, and skipping required assertion or falsifying manifest. Document mutant compile status, applicable test, whether actually killed, and classification. Mutation survival is not silently recast as PASS.

## Execution gate sequence

1. Source/policy preflight: frozen revision, approvals and 75-assertion contract.
2. Clean builds plus fast unit/contract tests.
3. Fresh/upgrade PostgreSQL, role/constraint and exact audit atomicity tests.
4. HTTP authorization, concurrency, cursor, CSRF and privacy negatives.
5. M17 controlled fault, safe evidence, default-off and exporter-down behavior.
6. Bilingual full browser journey in Chromium, Firefox and WebKit.
7. Q3 disposable historical-volume check (or supported N/A) and impacted G16/G17/M1-M15 regressions.
8. Effective negative controls, retained raw evidence and exact hash reconciliation.
9. Independent S25 raw source/evidence review with signed recommendation, explicit policy exception register, and separate G18 acceptance authorization.

If any prior stage fails, fix precisely, audit the fix, record the new SHA and rerun every affected stage. A later successful stage cannot overwrite the failed attempt. No GitHub Actions dispatch or merge is authorized by this verification plan.

## Exit record template

| Field | Required value at execution |
| --- | --- |
| Candidate source, tree and workspace | Exact full SHA, clean status, owned E:-backed resources |
| Versioned policy | Exact P01-P20 choices and signed rationale |
| Scenario results | 25 row outcomes with all 75 assertion results and witness links |
| Failed history | Every failed and superseded attempt retained unaltered |
| Mutations and regressions | Effective kill/disposition table and complete affected test matrix |
| Q3 | Safe-copy result or justified NOT_APPLICABLE for S24-A01 |
| Manifest | Source-bound SHA-256, bytes and expected/actual file reconciliation |
| Independent review | Distinct signed A01/A02/A03 recommendation, procedural/technical exceptions |
| Gate decision | Explicit accepted/not accepted, decision date and source, never assumed from CI |

The [machine-readable inventory](M18_G18_ASSERTION_INVENTORY_20261118.json) is the contract for future strict reconciliation. It contains **no executed witnesses**.
