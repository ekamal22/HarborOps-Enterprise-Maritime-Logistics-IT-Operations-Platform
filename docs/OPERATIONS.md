# Operations and Reliability

## Environment model

- Local: Docker Compose-managed API, worker, PostgreSQL, web, and optional observability stack.
- Demonstration: disposable cloud environment activated only when required.
- Production: not in scope for initial milestone acceptance.

The M0 Compose profile has been started and locally verified on Windows 11 against code revision `4fb34eb`. The stack was stopped after verification; no persistent hosting or production deployment exists. The M0 register records health, fault injection, browser behavior and recovery. Future event processing, operational telemetry, backup/restore and incident workflows remain planned.

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

A backup is not considered viable until a clean restore test passes. Create a runbook before M19 and include a reproducible restore verification record in the acceptance evidence. M0's disposable PostgreSQL service restart was not a backup/restore rehearsal.

## E-drive operational storage

For local operations, project workspace, database and Docker/WSL data, logs, screenshot evidence, and dependency/build caches must follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md). M0's local run used the E-drive Docker and cache baseline. Stop before enabling tools that require persistent storage on C:; short-lived system temporary writes are the only permitted exception.

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
