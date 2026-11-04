# Operations and Reliability

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

## M9 worker operations (candidate; G9 technical campaign executed, acceptance pending)

The corrected [M9 source](M9_IMPLEMENTATION_STATUS.md) on [draft PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18) adds a PostgreSQL-leased worker, scoped operator queue actions, a restricted database role, and a separate migration. These instructions describe an **unverified test-only candidate**, not an accepted upgrade or operating procedure for the retained M8 environment.

- Keep the accepted `docker compose up` stack, its PostgreSQL volume and the authentication keyring untouched. The default `compose.yaml` does not start the M9 worker.
- `compose.g9.verify.yaml` is a standalone `ho-verify-g9` project using loopback ports `55439` (database), `8089` (API) and `5179` (web), network `172.30.249.0/24`, and a matching web API URL. Review its resolved configuration with `docker compose -f compose.g9.verify.yaml config` before any approved isolated campaign.
- `compose.m9.worker.yaml` provides an opt-in worker service for coordinated deployment, not an independent accepted service. Provision `harborops_worker` using `scripts/m9-worker-role.sql` only on an approved disposable database following M9 migration and configure a separate untracked `HARBOROPS_WORKER_DB_PASSWORD`. The worker must not run under the application database superuser.
- `dotnet HarborOps.Worker.dll --readiness` is a candidate database connectivity and permission probe. A running or healthy container is not proof of effect-once correctness, reconciliation, claim recovery or G9 acceptance.
- Queue status routes require Manager, Analyst or Auditor; only Manager may submit a reasoned, CSRF-protected redrive. The worker retains immutable accepted receipts, parks unknown or ambiguous references and writes no M10 projection.
- `scripts/verify-g9-local.sh` is the source-locked technical campaign. It needs a clean committed tree, a new E: root ending in `/m9`, and `HARBOROPS_G9_PROJECT` set to a new `ho-verify-g9-<suffix>` Compose project for each attempt. It builds a G9-specific database image tag, never the tag other streams share, pulls base images with retries, starts the stack with `--profile worker`, and tears down only its named project. Technical exit 0 is not G9 acceptance.
- All future source-locked G9 test resources, caches, Docker/WSL data, logs and evidence must be isolated on E: under [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Never repurpose retained data, trigger GitHub Actions, or erase failed evidence.

The [G9 technical record](M9_EXECUTED_VERIFICATION.md), [verification plan](M9_VERIFICATION_PLAN.md), [correctness audit](M9_CORRECTNESS_AUDIT_20261104.md) and an independent review remain necessary. **M9-01 contract acceptance and G9 acceptance pending. No public or production worker deployment is authorized.**
