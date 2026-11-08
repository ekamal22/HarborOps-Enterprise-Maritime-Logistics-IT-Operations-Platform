# Operations and Reliability

**Closure campaign update (2026-11-08):** Independent raw-evidence reviews of the first local package were performed ([G11](M11_G11_INDEPENDENT_EVIDENCE_REVIEW_20261108.md), [G12](M12_G12_INDEPENDENT_EVIDENCE_REVIEW_20261108.md)) and returned S25 NOT PASS. The [closure campaign](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md) then ran the complete 388-case integration suite with no skips, 291 unit and 46 contract tests, measured query plans, an isolated Nginx, Kestrel and PostgreSQL stack (G11 process and database faults 12/12, G12 real network path 32/32), 48/48 three-engine browser tests, 17/17 effective mutants killed and clean dependency, source and image scans, and fixed two defects (export serialization retry, explicit UTC designator). The [G11](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12](M12_G12_EVIDENCE_MATRIX_20261108.md) matrices carry the dispositions (G11 PASS 23, BLOCKED 2; G12 PASS 22, NOT APPLICABLE 1, BLOCKED 2). A new independent re-review of the closure package, the formal S01 policy decisions and the signed M9 hold reconciliation remain pending, so acceptance is pending for both gates. Protected `main`, the M9 hold and all deployment restrictions are unchanged.

**Final M11/M12 technical verification (2026-11-08):** [Run reconciliation](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) confirms **341/341 integration PASS** on `f56d017` with no skips, plus 40/40 impacted, G11 19/19, G12 4/4, three-engine browser 36/36, effective mutants 5/5, SQL read isolation and security scans. [G11/G12 acceptance sufficiency review](M11_M12_ACCEPTANCE_REVIEW_20261108.md), [G11 matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12 matrix](M12_G12_EVIDENCE_MATRIX_20261108.md) record exact remaining evidence gaps. Independent S25 reviews and acceptance pending. Older 340-case and 'in progress' notes below describe historical checkpoints, not current execution.

**Executed M11/M12 verification (2026-11-08):** [Source-locked verification reconciliation](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) records 340/340 passing full integration tests on an earlier SHA, 40/40 latest impacted tests, G11 19/19, G12 4/4, 36/36 three-engine browsers, 5/5 effective mutants, security checks and a verified populated 13-to-15 migration. Source build and 291/291 unit plus 46/46 contract tests passed on a later corrective SHA. The final combined regression run `42443065884` is still in progress; G11/G12 S25 review and acceptance remain pending. Earlier checkpoints below retain their historical meaning.

**Current verification policy (2026-11-08):** [M11/M12 verification operations](M11_M12_VERIFICATION_OPERATIONS_20261108.md) now requires manual, coordinated, source-locked workflows instead of automatic push-triggered campaigns. [Run-by-run progress](M11_M12_VERIFICATION_PROGRESS_20261108.md) is the current verification checkpoint; older reports and source-only notes below are historical. G11/G12 independent S25 review and acceptance remain pending. Protected `main`, the M9 transport hold, and deployment restrictions are unchanged.

**Verification reconciliation, 2026-11-08:** The [M11/M12 source-locked evidence register](M11_M12_VERIFICATION_PROGRESS_20261108.md) records 338/338 whole-suite integration tests on the prior checked source, 291/291 unit, 46/46 contract, 19/19 G11, 4/4 G12, 36/36 three-engine browser, five killed effective mutants and successful runtime-image scans, each with its own exact commit. The expanded 340-case full run, new populated 13-to-15 upgrade coverage and final SHA reconciliation were in progress at this checkpoint. Older source-only or 18/18 and 36/36 statements below are historical. M11 local rules are recorded; [M12 policy choices](M12_LOCAL_CONTRACT_DECISION_20261108.md) are selected for verification; [Q2 applicability](M12_Q2_HISTORICAL_SNAPSHOT_APPLICABILITY_20261108.md) is documented separately. G11/G12 raw evidence, independent S25 reviews and gate acceptance remain pending. Protected `main`, the M9 operating hold, and no deployment remain unchanged.

**Combined-source checkpoint (2026-11-08):** M11 and M12 are assembled on `feat/m11-m12-combined-integration`; shared API, role provisioning, React and migration-count surfaces have been reconciled, and M11 lifecycle code and tests updated under the [recorded M11 policies](M11_LOCAL_CONTRACT_DECISION_20261108.md). Earlier descriptions of missing combined integration or pending M11 policy choices are superseded. G11/G12 executable security and browser evidence does not yet exist, M12 local policies are still pending, and no gate acceptance, protected-main merge, deployment or M9 hold release is authorized.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). Reuse a shared disposable E:-backed stack where safe, but serialize outage and global-login tests. G12 login-changing verification requires a dedicated disposable PostgreSQL server. No deployment or M9 transport-hold release is authorized.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records independent unmerged source candidates in draft PRs #31 and #32. M11 policy approval is blocked, both technical gate campaigns are unexecuted, and acceptance pending. Protected `main` and the M9 transport hold are unchanged.

**Current M11 source audit (2026-11-08):** [Sixth correctness audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) documents informational severity on inactive outcomes, versioned rule-metadata reconciliation and typed due-work continuation. The source changes and new tests are unexecuted. G11-S01 BLOCKED, G11-S02 through S25 NOT EXECUTED, acceptance pending. The M9 transport-processing hold remains in force.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). Reuse a shared disposable E:-backed stack where safe, but serialize outage and global-login tests. G12 login-changing verification requires a dedicated disposable PostgreSQL server. No deployment or M9 transport-hold release is authorized.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records separate unmerged source candidates in draft PRs #31 and #32. M11 policy approval is blocked, both technical gate campaigns are unexecuted, and acceptance pending. Protected `main` and the M9 transport hold are unchanged.

**M12 candidate status (2026-11-08):** [Draft PR #32](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/32) contains scoped shipment search, bounded signed CSV export and source-level correctness repairs. [Latest audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md), [implementation status](M12_IMPLEMENTATION_STATUS.md) and [G12 plan](M12_VERIFICATION_PLAN.md) distinguish authored tests from executed evidence. The fifth pass adds exclusive PostgreSQL role-test gating and CSV cancellation checks. Earlier source fixes remain in force. G12 execution and independent acceptance remain pending. Protected `main` and the M9 transport hold are unchanged.

## M12 fifth-pass test execution boundary

The [latest M12 audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md) identifies server-global PostgreSQL role mutation in the G12 restricted-runtime test. Use an exclusive disposable **server**, not only a disposable database. The test is explicitly SKIPPED unless `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1` is set and performs a second fail-closed execution preflight. xUnit disables parallel execution for its collection within the assembly; concurrent test processes and other assemblies must be stopped separately. Review `HARBOROPS_TEST_DB` local host/database validation before selecting the G12 server. Do not apply the marker to a retained or shared instance.

The export now checks cancellation through synchronous CSV generation and encoding. Real CPU-stall, database-stall and Nginx response-time tests remain unexecuted; the store's 25-second budget does not include authentication or delivery. Keep E: as the backing store for disposable verification volumes and evidence.

## M12 export proof provisioning candidate, 2026-11-07

M12 is present only on the isolated feature branch, not accepted or deployed. See the [source audit](M12_CORRECTNESS_AUDIT_20261107.md) and [G12 verification plan](M12_VERIFICATION_PLAN.md). The accepted main stack and M9 transport hold remain unchanged.

The SQL-only M12 migration creates `m12_export_proof_keys`, the restricted `m12_record_shipment_export` function and a unique event/correlation index. To enable CSV export in a **disposable, approved M12 verification stack**, generate an independent 32-byte random base64 key, for example with `node -e "process.stdout.write(require('node:crypto').randomBytes(32).toString('base64'))"`. Place it in the untracked, E:-backed local secret configuration as `HARBOROPS_M12_EXPORT_PROOF_KEY`, distinct from the M10 key and database passwords. `compose.yaml` passes it to the one-shot privileged migrator and HTTP API as `M12ExportProof__Key`, not the worker, web or projector. No key value belongs in the repository, terminal logs or evidence.

The M12 Compose variable is optional so existing accepted-stack tests are not forced to configure a future module, but export **fails closed with sanitized 503** if the key is missing or the database verifier does not match. A configured but malformed key aborts the privileged provisioning step. To rotate, coordinate the same new value in migrator and API, reprovision with the privileged identity on an isolated stack, then restart only the authorized API instance. Previous outstanding proofs are invalidated; audit records stay immutable. Do not perform this on retained environments without a separate maintenance approval.

The proposed G12 PostgreSQL role test reprovisions server-wide `harborops_api` and `harborops_worker` passwords. It requires a dedicated disposable PostgreSQL instance and must never run concurrently with other mutable verification tests or touch the primary database. All worktrees, images, caches, credentials, volumes and raw evidence belong on E: under [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Run `docker compose config --quiet` and source-locked checks only when a suitable local runtime is available and approved; this audit did not run them.


**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted on protected `main`. The signed-correction security remediation merged in [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) at `9b6c801fde4004a4c0021ec7a6296efe28cf1544` after [G10 S01/S25 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Historical reports retain their original source-locked results. The M9 transport-processing hold continues. No deployment or release is authorized.


**Accepted M9/G9 baseline (2026-11-05):** G9 was accepted with the mandatory [M9-01 transport-backlog operating condition](M9_LOCAL_CONTRACT_DECISION.md). M9 is integrated into `main` through PR #16 (`43fe326`) and PR #18 (`4684352`). The [G9 gate closure](M9_G9_GATE_CLOSURE.md) records the evidence review and residual limitations. No production or public deployment is authorized; sustained transport-event processing is held until M10 or another explicitly approved backlog mechanism is verified. Earlier planning or acceptance-pending language in historical subsections reflects the earlier project phase.

## M11 sixth-pass operating observations

The optional due scanner still uses a bounded organization/shipment UUID keyset with explicit casts. Repeated no-op evaluations refresh their due checkpoint; changed visible metadata requires a full versioned rule update, not an unnoticed stale display. Under G11 verification, track both event counts and scan progression while simulating an inactive/active severity recalibration. No production evaluator enablement is authorized.

## M11 unverified pagination operating boundary

The provisional exception-list cursor has a 20-minute expiry and includes a PostgreSQL creation cutoff. It is not a stable snapshot of mutable lifecycle fields, so a read requiring fresh state must restart pagination. Immutable history is initially previewed at 500 entries and continued through bounded 100-entry pages with separate 20-minute protected cursors. The selected organization and role are revalidated on each page. These operations have not been volume/restart or database-outage tested. The M11 evaluator remains opt-in and separate from M9 transport intake.

## G10 review and operating boundary

The [original G10 independent evidence review](M10_G10_INDEPENDENT_EVIDENCE_REVIEW.md) records the historical twelve-migration campaign. The [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md) supersedes its pending decision after supplemental S01/S25 review and PR #28 integration. The optional M10 projector stays disabled by default and requires a restricted login and privilege preflight. No host-power-cut recovery guarantee is inferred from container SIGKILL testing.

**Do not enable sustained transport-event processing.** ARRI and DEPA remain parked and the 10,000 unresolved-work cap remains enforced. G10 is accepted and merged, but it did not implement cap-safe parked-event handling or ETA computation. Do not deploy or lift the M9 hold without separate authorization.

## Environment model

- Local: Docker Compose-managed API, worker, PostgreSQL, web, and optional observability stack.
- Demonstration: disposable cloud environment activated only when required.
- Production: not in scope for initial milestone acceptance.

**Source publication:** The repository is public for inspection under the restrictive LICENSE, but that does not create a hosted application or approve production operations. `main` is protected, GitHub Actions is disabled, and all controllable verification and operational evidence must remain on E:. Any internet-facing deployment requires separate TLS, secrets, reverse-proxy and operational clearance; see [Publication Safety Status](PUBLICATION_SAFETY_STATUS.md) and [Security](SECURITY.md).

M0 local Compose infrastructure was started on a clean volume and verified on Windows 11 at code revision `4fb34eb`. The stack was stopped after testing, and no hosted service is running. The configured stack contains PostgreSQL, one-shot database migration/seed, HTTP API, and static frontend. New M0 hardening verification is recorded separately in [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md). The worker shell is intentionally not part of the running stack until tracking processing is implemented.

## Observability requirements

| Signal | Minimum requirement |
| --- | --- |
| Application logs | Structured JSON, UTC time, level, service, correlation ID, redacted context |
| Request metrics | Request count, latency percentiles, error rates, auth denials |
| Worker metrics | Inbox age, accepted events, duplicate rate, dead-letter count, processing duration |
| Database metrics | Connection utilization, query latency, failed migrations, capacity |
| Business metrics | Exceptions opened/closed, projected delays, ingest freshness |
| Traces | Correlated API -> worker -> persistence operations where feasible |
| Health | Separate liveness, readiness, and dependency status |
| Alerts | Threshold, owner/recipient scope, deduplication key, acknowledgment trail |

Metrics must avoid high-cardinality container identifiers as labels and must not expose secrets or customer content.

## Service level objectives

Initial targets are non-contractual and require baseline measurements. Define an observation window, deployment profile, synthetic load, and test conditions before publishing performance or reliability claims.

- Essential API availability: measured from controlled probe data.
- Ingestion latency: age between receipt and accepted projection.
- Recovery: time to restore a worker after forced termination.
- Diagnostics: every test incident links a correlation ID and decisive error or metric signal.

## Incident process

1. Detect signal and identify affected service.
2. Open incident with severity, scope, detection time, and reproducible symptoms.
3. Triage: confirm impact, recent changes, dependencies, and immediate containment.
4. Investigate with logs, metrics, traces, and event inbox history.
5. Mitigate and recover while retaining failed receipts.
6. Verify service health and data integrity.
7. Document root cause, contributing factors, corrective actions, and closure evidence.

### Incident states

    New -> Triage -> Investigating -> Mitigated -> Monitoring -> Resolved -> Closed
                         |                 |
                         +-----------------+--> Reopened

State transitions require permissions, timestamps, and reason notes. An incident cannot close without verification evidence or an explicit accepted exception.

### Severity guidance

| Severity | Example | Expected response |
| --- | --- | --- |
| SEV-1 | Sustained total outage or confirmed cross-organization data exposure | Immediate containment and escalation |
| SEV-2 | Major tracking backlog or widespread reporting corruption | Prioritized recovery and investigation |
| SEV-3 | Isolated feature degradation with workaround | Planned correction with incident record |
| SEV-4 | Low-impact usability or diagnostic issue | Normal backlog |

These are workflow classifications, not guaranteed response time commitments.

## Failure injections

- Terminate tracking worker mid-processing, then restart and verify idempotent recovery.
- Drop database connectivity, verify bounded backoff and restored processing.
- Introduce invalid event bursts, verify quarantine without crashing intake.
- Inject read-only dashboard failure, verify clear user-facing error and no data corruption.
- Simulate elevated latency and establish request/trace correlation.

Only run failure injections against isolated local or authorized demonstration infrastructure.

## Backup and restore

### Backup expectations
- PostgreSQL backup with a reproducible command or scripted job, documented version and timestamps.
- Retention and storage permissions defined for each environment.
- Restore into an isolated target, never over an unverified live database.
- Verify migration state, row counts, foreign keys, event projection consistency, and representative API queries.
- Record backup size, restore duration, test date, and observed failures.

### Recovery readiness

A backup is not considered viable until a clean restore test passes. Create a runbook before M19 and include a reproducible restore verification record in the acceptance evidence. Restarting PostgreSQL in M0 is not an isolated backup/restore demonstration.

## Deployment guardrails

- Separate configuration by environment and never commit secrets.
- Database migrations must have a rehearsal and safe rollout plan.
- Expose only necessary ports and use HTTPS for hosted access.
- Restrict monitoring endpoints and database ingress.
- Set a budget or usage cap, disable optional paid services, and document shutdown steps.
- Record exact deployment version and whether sample data is synthetic.

## Required runbooks by M19

- Start and stop local stack.
- Seed and reset demonstration dataset.
- Investigate failed event processing.
- Diagnose 5xx and database connection errors.
- Restore database from backup.
- Rotate and revoke integration credentials.
- Deactivate hosted demonstration resources.

## Accepted G5 operational verification

The dedicated E:-backed G5 stack completed source-locked regression, browser, recovery and evidence checks and G5 was explicitly accepted 2026-11-02. See [G5 executed verification](M5_EXECUTED_VERIFICATION.md) and [closure](M5_G5_GATE_CLOSURE.md). This record is historical; G5 resources must not be reused for newer gates.

## Storage and evidence handling

Follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Keep the repository, caches, PostgreSQL/Docker/WSL data, logs, screenshots and build outputs on E:. The verified local setup used `E:\\APP IDEAS\\harborops-m0-cache` for dependency caches and evidence. Do not introduce persistent C-drive project storage; system temporary writes are permitted only when unavoidable. No global disk relocation, pruning or data deletion is authorized by this runbook.

## Quarter-mile retained-volume verification

Existing local Docker volumes do not need to be checked on each M0 code revision. G0 tests only fresh disposable PostgreSQL initialization and safe outage/recovery. Lightweight snapshot compatibility checks are scheduled at M6, M12, M18 and M24-03; record NOT APPLICABLE when no historical snapshot exists. M19 remains the full backup/restore and historical upgrade rehearsal. Prior to rolling out a breaking schema or database-storage change to retained data, rehearsal-test that migration against a disposable copy. No routine check may modify the original volume.

## M1 authentication operations (accepted local runtime, not publicly deployed)

The approved [M1 design](M1_AUTHENTICATION_DESIGN.md) requires local interactive account provisioning without passwords in CLI arguments, shell history or CI artifacts. The current local CLI implements restricted `provision` and `reset` commands. Authenticated self-disablement is exposed through the protected API. All-session revocation and redacted `auth_events` are implemented. Administrative re-enablement is not implemented; a separately reviewed maintenance procedure is required before it is offered. No web-accessible administrative account management is exposed in M1.

The API application filesystem remains read-only except for its dedicated persistent ASP.NET Core Data Protection key-ring mount. The local key-ring volume follows the E-drive storage policy. Non-local deployments require reviewed key protection, permissions and encryption at rest. On restart, valid sessions must survive because keys persist; revoked sessions must not reappear. Corrupt/missing key rings and unavailable PostgreSQL must fail closed. Preserve isolated `ho-verify` Compose project safety and the G0 synthetic baseline checks.

Nginx and Vite proxy browser `/api/*` requests to the internal API under the same frontend origin. The API's direct loopback port remains diagnostic only. Local development may use loopback HTTP with a separate development cookie name/configuration; non-loopback or hosted authentication requires HTTPS, secure cookies and a trusted reverse-proxy configuration. Do not deploy M1 login publicly over HTTP.

The [M1 verification record](M1_IMPLEMENTATION_VERIFICATION.md) covers synthetic provisioning, password reset, self-disablement, session revocation, API restart, login throttling, CSRF denial, database outage/recovery and expiry. Administrative re-enablement, deliberate key rotation, operational capacity checks and deployment-grade key protection remain future operational tasks. Never record credentials in runbook commands or logs. No real personal credentials are required to pass G1.

## M1 local account and session operations (main)

Both PostgreSQL data and the key ring persist across ordinary container restarts. `docker compose down -v` deletes both named volumes and invalidates existing protected sessions. G1 was accepted on 2026-10-31; [M1 verification](M1_IMPLEMENTATION_VERIFICATION.md) records restart, outage, revocation and replay checks.

The accepted M1 runtime adds a second persistent Docker volume, `harborops-keyring`, separate from PostgreSQL data. The running API container requires write access to `/keys` while the remainder of the filesystem stays read-only. The local-only HTTP exception is permitted only with loopback-bound Compose ports. Never expose the profile on a public network. Non-local deployment must supply a certificate to encrypt Data Protection keys and use HTTPS.

Use `docker compose run --rm -it -e HARBOROPS_ALLOW_ACCOUNT_CLI=true api --account-cli provision EMAIL DISPLAY_NAME` for local provisioning. Use `--account-cli reset EMAIL` to change a password and revoke sessions. Passwords are prompted through terminal input or a controlled stdin pipe; do not supply them as arguments, logs, or tracked environment variables. Restrict who can access the host and Docker volumes.

## M2 organization operations runbook (implemented on `main`; G2 accepted)

The [M2 architecture](M2_ARCHITECTURE_DESIGN.md) defines an opt-in local `--organization-cli bootstrap` command that creates one organization and its first Administrator membership transactionally for an already provisioned active user. The command is implemented in `src/HarborOps.Api/Organizations/OrganizationCli.cs` and was executed in the local G2 verification: two synthetic organizations were created, and a missing opt-in, an unknown account and a duplicate slug were each rejected with exit code 1 and no rows written. See [M2 Implementation Plan](M2_IMPLEMENTATION_PLAN.md) and the [source inventory](M2_IMPLEMENTATION_STATUS.md). Membership changes use authorized, CSRF-protected scoped APIs with role checks performed from PostgreSQL. There is no public organization registration. A sole active Administrator must assign an additional active Administrator before using the M1 account self-disable action; the API returns 409 `last_administrator` otherwise. The restricted invocation is `docker compose run --rm -it -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api --organization-cli bootstrap north-terminal "North Terminal" operator@example.test`, after applying migrations and provisioning the account. Rejected restricted commands print a sanitized message and exit with code 1.

G2 tests use disposable databases and controlled environments. The [local G2 verification](M2_EXECUTED_VERIFICATION.md) ran `scripts/verify-m2-local.sh` against the isolated `ho-verify` Compose project with all caches and evidence on E:. Do not access or delete the primary workstation PostgreSQL volume or perform historical-volume compatibility ahead of scheduled checkpoints. A required unavailable tool or browser engine is BLOCKED, not a successful test.

## Accepted M4 migration and operations boundary

The accepted [M4 implementation](M4_IMPLEMENTATION_STATUS.md) adds the PostgreSQL `20261101010000_VoyageContainerReferences` migration after M3 with tenant-composite foreign keys and validated voyage/container records. Fresh, repeated and historical M3 upgrade/rollback scenarios, real outage/recovery, three-browser tests and E:-backed disposable cleanup passed [G4 executed verification](M4_EXECUTED_VERIFICATION.md). D-003 Option B and G4 were accepted on 2026-11-01. If M5 is developed independently, reconcile migration IDs and rerun the combined source regressions before shared integration.

## Accepted M5, M6 and M7 verification isolation

[G5](M5_G5_GATE_CLOSURE.md), [G6](M6_G6_GATE_CLOSURE.md) and [G7](M7_G7_GATE_CLOSURE.md) were accepted after separate E:-backed disposable verification and independent evidence review. Original runner records and limitations are preserved. Do not rerun historical gates against retained workstation volumes or claim later commits were automatically reverified.

## M7 restricted integration-source operations

The M7 API implements a local operator-only source CLI for create, rotate, revoke, disable and redacted inspect. Source issuance requires explicit opt-in and a protected interactive terminal. Never put bearer values in arguments, logs, source control or shareable evidence. Rotate and revoke against the correct organization; ensure no credential remains active after source disablement. The accepted G7-S06 operator supplement verified the lifecycle, revocation and cleanup. Until M8, even valid authenticated tracking events receive HTTP 503 `intake_unavailable`; do not configure production publishers expecting durable receipt or a 2xx acknowledgment. See [G7 closure](M7_G7_GATE_CLOSURE.md).

## M8 local durable-intake operations (G8 accepted)

The feature branch now provides [correctness remediation](M8_CORRECTNESS_REMEDIATION.md), [second audit remediation](M8_SECOND_REMEDIATION_STATUS.md), a separate [source-locked review reconciliation procedure](M8_G8_RECONCILIATION.md), the [efficient local G8 runbook](M8_G8_VERIFICATION_RUNBOOK.md) and an E:-backed technical runner. The runner cannot itself record D-006 approval or independent G8 acceptance; review inputs remain separately sourced and original raw run evidence must not be overwritten.

The [M8 architecture](M8_ARCHITECTURE_DESIGN.md) proposes an atomic PostgreSQL receipt/event/Pending-work transaction on the accepted M7 tracking route. D-006 Option A and G8 were accepted 2026-11-04 after source-locked technical execution and [independent evidence review](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md). The accepted M8 receipt intake remains distinct from future processing. At G8 acceptance there was no M9 worker or operator command in `main`. A separate, unverified M9 candidate is now recorded below; M8 does not introduce a retention job. For future G8 runs, isolate E:-backed `ho-verify-g8` database, network, image and evidence resources, never reuse the primary or G7 retained stack, and preserve raw sanitized transaction/replay/failure/teardown evidence. Every attempt uses a distinct parent directory with a new `/m8` root. Redirect controllable temporary storage to E:; inventory and safely remove only attributable short-lived task-created C: files after the run, recording before/after measurements. Never prune unrelated data. Do not delete committed receipts or release source-event keys to free capacity. The proposed Pending admission cap and M9 handoff require a separately accepted design; no durable success acknowledgment may be exposed before transactional verification. See [M8 verification plan](M8_VERIFICATION_PLAN.md).

## Parallel development operations boundary

Parallel workstreams must use distinct branches/worktrees, E:-backed output paths and disposable test Compose projects. Only a controlled migration integration may alter the canonical database schema; all branches must validate against the same accepted baseline and reconcile migration ordering before integration. Do not stop/prune primary volumes or reuse production credentials. New modules must preserve M0-M4 session/tenant security and emergency operational controls. A previously accepted recovery or scan gate does not exempt later changes from change-specific regression or final G24 release checks. See [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md).

## M3 reference-data operations (G3 accepted)

M3 delivers accepted organization-owned port/vessel references through an additive PostgreSQL migration and same-origin scoped APIs. The [M3 architecture](M3_ARCHITECTURE_DESIGN.md) and [implementation plan](M3_IMPLEMENTATION_PLAN.md) define optimistic mutation versions and soft archive. Local G3 verification exercised startup, schema migration, real PostgreSQL outage/recovery, sanitized 503, session and role revocation, durable scope and disposable Compose cleanup; all 25 scenarios passed at `b72025d35d62d0a8a45b466986a0b3bd89eebadb` ([evidence](M3_EXECUTED_VERIFICATION.md)). G3 was explicitly accepted 2026-10-31 and M3 is in `main`. Production deployment, full backup/restore and later operational assurance remain out of scope.

## M0 local runbook

- Set a unique password in a local untracked `.env` based on `.env.example`
- Run `docker compose config --quiet` to validate interpolation, then `docker compose up -d --build`
- Inspect `docker compose ps` and `docker compose logs migrator api postgres web`
- Probe `/health/live`, `/health/ready`, and `/api/v1/system/baseline`
- Run `bash scripts/smoke-compose.sh --project ho-verify --confirm-disposable` on a separate disposable test project with valid local credentials; this script refuses the primary database project
- Stop with `docker compose down`. Only use `docker compose down -v` for deliberate data deletion

The database is bound only to `127.0.0.1:55432` on the host; the API and web ports are bound to loopback. Compose does not configure HTTPS and must not be exposed as-is on a public network.

Failure behavior (reverified 2026-10-31 in [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md)): when PostgreSQL becomes unavailable after startup, liveness stays HTTP 200, readiness becomes HTTP 503 and the baseline endpoint returns a controlled HTTP 503 with a correlation identifier. The UI shows a service error and recovers on refresh after the database returns. To repeat the exercise on a disposable local stack run `bash scripts/check-m0-degraded.sh --project ho-verify --confirm-local-outage` against the same isolated test project. Bootstrap schema migration recovery, backup, and restore procedures remain pending until M19.

## M9 worker operations (accepted locally; deployment restricted)

The accepted [M9 implementation](M9_G9_GATE_CLOSURE.md), merged through [PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18), adds a PostgreSQL-leased worker, scoped operator queue actions, a restricted database role and a separate migration. These are locally verified operational capabilities, not permission to upgrade the retained environment without a controlled plan or to deploy publicly.

- Keep the accepted `docker compose up` stack, its PostgreSQL volume and the authentication keyring untouched. The default `compose.yaml` does not start the M9 worker.
- `compose.g9.verify.yaml` is a standalone `ho-verify-g9` project using loopback ports `55439` (database), `8089` (API) and `5179` (web), network `172.30.249.0/24`, and a matching web API URL. Review its resolved configuration with `docker compose -f compose.g9.verify.yaml config` before any approved isolated campaign.
- `compose.m9.worker.yaml` provides the accepted opt-in M9 worker service for coordinated local use, subject to the transport-backlog hold and controlled migration procedures. Provision `harborops_worker` using `scripts/m9-worker-role.sql` only on an approved disposable database following M9 migration and configure a separate untracked `HARBOROPS_WORKER_DB_PASSWORD`. The worker must not run under the application database superuser.
- `dotnet HarborOps.Worker.dll --readiness` probes database connectivity and permissions. A healthy container alone does not prove ongoing effect-once correctness, safe reconciliation, claim recovery or that the M9 transport-backlog condition is satisfied.
- Queue status routes require Manager, Analyst or Auditor; only Manager may submit a reasoned, CSRF-protected redrive. The worker retains immutable accepted receipts, parks unknown or ambiguous references and writes no M10 projection.
- `scripts/verify-g9-local.sh` is the source-locked technical campaign. It needs a clean committed tree, a new E: root ending in `/m9`, and `HARBOROPS_G9_PROJECT` set to a new `ho-verify-g9-<suffix>` Compose project for each attempt. It builds a G9-specific database image tag, never the tag other streams share, pulls base images with retries, starts the stack with `--profile worker`, and tears down only its named project. Technical exit 0 is not G9 acceptance.
- `scripts/verify-g9-browsers-local.sh` runs the 16 existing browser specs in three engines against the isolated G9 stack built from a clean, source-locked checkout (`HARBOROPS_SOURCE_WORKTREE` and `HARBOROPS_EXPECTED_SOURCE_SHA`), with its own `ho-verify-g9-<suffix>` project and an E: root ending in `/m9b`. Failure artifacts are sanitized before archiving; optional `HARBOROPS_BROWSER_ENGINES` and `HARBOROPS_BROWSER_SPECS` narrow a documented partial rerun.
- All future source-locked G9 test resources, caches, Docker/WSL data, logs and evidence must be isolated on E: under [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Never repurpose retained data, trigger GitHub Actions, or erase failed evidence.

The [G9 technical record](M9_EXECUTED_VERIFICATION.md), [verification plan](M9_VERIFICATION_PLAN.md), [correctness audit](M9_CORRECTNESS_AUDIT_20261104.md), [independent review](M9_G9_INDEPENDENT_EVIDENCE_REVIEW.md) and [gate closure](M9_G9_GATE_CLOSURE.md) preserve the accepted verification basis. **M9/G9 accepted 2026-11-05 with mandatory transport-backlog operating restriction. No public or production worker deployment is authorized.**

### M9 transport-backlog deployment hold

All six [M9-01 contract decision](M9_LOCAL_CONTRACT_DECISION.md) choices are approved with an operating condition: **do not enable sustained transport-event processing** until M10 or another explicitly approved, verified mechanism manages those events in `NeedsReconciliation` and prevents exhaustion of the 10,000 unresolved-work cap. Controlled isolated testing remains permitted. The deployment approval checklist must verify that the required mechanism exists before enabling sustained transport traffic; `--readiness` alone does not establish this condition. Processed equipment events do not imply support for transport events. This approval does not authorize a live deployment.

The [source-locked G9 campaign](M9_EXECUTED_VERIFICATION.md) passed 809/809 tests and 14/14 mutation families; the [browser supplement](M9_BROWSER_SUPPLEMENT.md) passed 174/174 browser executions. The later independent G9-S25 review and formal acceptance are recorded in [G9 closure](M9_G9_GATE_CLOSURE.md). At G9 acceptance M16 remained separate; it was subsequently accepted and integrated through [G16 closure](M16_G16_GATE_CLOSURE.md) and PR #25.

## M16 accepted audit integration and deployment hold (2026-11-06)

G16 was accepted after independently reviewed combined evidence and an F001 guard extension, then merged into protected `main` through [PR #25](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/25) at `47ba55e2`. The combined stack retains 11 migrations, uses migration-time API/worker role provisioning, and preserves Auditor-only tenant-scoped reads and immutable M9 worker-effect history as the worker audit of record. The privileged migration identity remains under the accepted local-first threat model. The 365-day online audit retention policy still requires separately verified archival; the cursor late-commit caveat remains.

**Operating restrictions:** The merge does not approve live or public deployment. Do not enable sustained transport-event ingestion or processing until separately approved and verified backlog handling prevents exhausting the 10,000 unresolved-work-item limit. Retained databases require authorized safe-copy rehearsal before migrations; use isolated E:-backed verification environments and keep GitHub Actions disabled.

## M10 on the merged G16/M16 base

The [M10 source reconciliation](M10_M16_SOURCE_RECONCILIATION.md) records the original M10 migration after accepted M16 and M9 migrations. Original M10 PR #23 and remediation [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) are now integrated. A thirteenth migration adds the signed correction boundary and reconciles the NULL-safe projector function for historical databases. G10 isolated Compose uses a privileged migrator, separate API and worker logins, and an optional restricted projector.

The combined G10/M16 campaign was executed locally on frozen source `3e81b88` and is recorded in [M10 executed verification](M10_EXECUTED_VERIFICATION.md). Operating notes from it: the projector is off by default (`M10__EnableProjector` false) and, when enabled, needs the separate `harborops_projector` login provisioned with `scripts/configure-m10-projector-db.sql` (read access to the tables it needs and write access to projections and applications only); its startup self-check refuses to start if any required privilege is missing or a forbidden one is present. The worker holds no M10 table privilege. Run the source-locked campaign with `scripts/verify-g10-local.sh` (new E: root ending `/g10`, unique `ho-verify-cg16-<suffix>` project, `HARBOROPS_DOCKER_VHDX` set); it runs the .NET 10 SDK in a container and layers the accepted combined Compose files. Maintain the M9 transport admission hold, no GitHub Actions, no production or public migration and no replacement of accepted G16 evidence.

## M10 correction authority provisioning, 2026-11-07

**Current M10/G10 operations checkpoint (2026-11-07):** G10 accepted and the remediation merged via PR #28 at `9b6c801fde4004a4c0021ec7a6296efe28cf1544`. Default Compose requires the private `HARBOROPS_M10_CORRECTION_PROOF_KEY` for migrator/API, not worker/web. Historical-upgrade and real correction flows passed at `e3002bd`; see [G10 closure](M10_G10_GATE_CLOSURE_20261107.md). No deployment, release or transport-hold removal is authorized.

**Final merge-readiness update (2026-11-07):** Two review findings are closed in source and verified on `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343` ([record](M10_G10_FINAL_MERGE_READINESS_20261107.md)): the accepted twelfth migration is restored unchanged and the NULL-safe projector scope function now arrives through the thirteenth migration, and the default `compose.yaml` requires `HARBOROPS_M10_CORRECTION_PROOF_KEY` for the migrator and API only. Targeted results: unit 239/239, contract 39/39, projector scope tests 3/3, change-impact integration set 126/126, default-stack correction flow 11/11, six projector mutants killed. The full integration suite, real-stack check and browser run were not repeated on this source. At this intermediate source-review checkpoint, G10-S25 and S01 were not yet executed. Both were subsequently reconciled and G10 accepted on 2026-11-07; the G10 gate closure is authoritative.


The remediated M10 correction route requires `HARBOROPS_M10_CORRECTION_PROOF_KEY`, a private base64 encoding of 32 cryptographically random bytes. The default `compose.yaml`, the G10 Compose file and the combined override pass it to the one-shot privileged migrator and the HTTP API as `M10CorrectionProof__Key` and to no other service. The default file uses fail-fast interpolation, so Compose refuses to start the stack with a clear message when the variable is unset or empty, and the migrator rejects a value that is not base64 of exactly 32 bytes. Generate one locally with `node -e "process.stdout.write(require('node:crypto').randomBytes(32).toString('base64'))"`, store it in the untracked `.env` next to the database passwords (`.env.example` shows the variable with an invalid placeholder), and never print it. The restricted worker, projector and web services do not receive it. Do not track, print, bundle, or log the key. Keep all verification secrets and artifacts on E: in the local Windows environment.

The new migration creates restricted verifier and immutable proof-nonce tables. Migration-time role provisioning removes direct correction INSERT from `harborops_api` and grants execution of the constrained signed correction function. Without a valid configured and provisioned key, corrections fail closed; a missing dependency returns HTTP 503. To rotate the key, apply the same new secret through the privileged migrator and HTTP runtime, then restart the API. Short-lived outstanding proofs are invalidated. Review nonce growth and retention before sustained production use.

The additive thirteenth migration passed fresh and populated historical upgrade verification. For any future non-test environment, apply normal backup, rollback feasibility and change-management procedures; a retained correction proof nonce intentionally blocks unsafe downgrade. G10 acceptance does not itself authorize deployment. Preserve earlier A1-A7 source-locked evidence and the M9 transport hold.

## M11/M12 local closure addendum (2026-11-08)

- The M11 evaluator is an opt-in hosted service (`M11:EnableEvaluator`, default off). When enabled, its due scan pages 25 shipments per 200 ms when nothing is due, so a full sweep takes roughly N/125 seconds for N shipments (from code; not measured).
- Free-text search on a tenant that holds most of the table scans the whole shipments heap (measured 32 to 49 ms at 170,000 rows). Plan a trigram index before text search must serve millions of rows.
- Export concurrency and rate limits are per API instance. Behind several instances the effective limits multiply.
- Evidence and retained plans: [local closure results](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md).
