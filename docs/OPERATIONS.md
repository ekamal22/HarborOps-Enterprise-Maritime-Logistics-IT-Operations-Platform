# Operations and Reliability

## Environment model

- Local: Docker Compose-managed API, worker, PostgreSQL, web, and optional observability stack.
- Demonstration: disposable cloud environment activated only when required.
- Production: not in scope for initial milestone acceptance.

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

## M2 organization operations runbook (verified locally; Acceptance pending)

The [M2 architecture](M2_ARCHITECTURE_DESIGN.md) defines an opt-in local `--organization-cli bootstrap` command that creates one organization and its first Administrator membership transactionally for an already provisioned active user. The command is implemented in `src/HarborOps.Api/Organizations/OrganizationCli.cs` and was executed in the local G2 verification: two synthetic organizations were created, and a missing opt-in, an unknown account and a duplicate slug were each rejected with exit code 1 and no rows written. See [M2 Implementation Plan](M2_IMPLEMENTATION_PLAN.md) and the [source inventory](M2_IMPLEMENTATION_STATUS.md). Membership changes use authorized, CSRF-protected scoped APIs with role checks performed from PostgreSQL. There is no public organization registration. A sole active Administrator must assign an additional active Administrator before using the M1 account self-disable action; the API returns 409 `last_administrator` otherwise. The restricted invocation is `docker compose run --rm -it -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api --organization-cli bootstrap north-terminal "North Terminal" operator@example.test`, after applying migrations and provisioning the account. Rejected restricted commands print a sanitized message and exit with code 1.

G2 tests use disposable databases and controlled environments. The [local G2 verification](M2_EXECUTED_VERIFICATION.md) ran `scripts/verify-m2-local.sh` against the isolated `ho-verify` Compose project with all caches and evidence on E:. Do not access or delete the primary workstation PostgreSQL volume or perform historical-volume compatibility ahead of scheduled checkpoints. A required unavailable tool or browser engine is BLOCKED, not a successful test.

## M0 local runbook

- Set a unique password in a local untracked `.env` based on `.env.example`
- Run `docker compose config --quiet` to validate interpolation, then `docker compose up -d --build`
- Inspect `docker compose ps` and `docker compose logs migrator api postgres web`
- Probe `/health/live`, `/health/ready`, and `/api/v1/system/baseline`
- Run `bash scripts/smoke-compose.sh --project ho-verify --confirm-disposable` on a separate disposable test project with valid local credentials; this script refuses the primary database project
- Stop with `docker compose down`. Only use `docker compose down -v` for deliberate data deletion

The database is bound only to `127.0.0.1:55432` on the host; the API and web ports are bound to loopback. Compose does not configure HTTPS and must not be exposed as-is on a public network.

Failure behavior (reverified 2026-10-31 in [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md)): when PostgreSQL becomes unavailable after startup, liveness stays HTTP 200, readiness becomes HTTP 503 and the baseline endpoint returns a controlled HTTP 503 with a correlation identifier. The UI shows a service error and recovers on refresh after the database returns. To repeat the exercise on a disposable local stack run `bash scripts/check-m0-degraded.sh --project ho-verify --confirm-local-outage` against the same isolated test project. Bootstrap schema migration recovery, backup, and restore procedures remain pending until M19.
