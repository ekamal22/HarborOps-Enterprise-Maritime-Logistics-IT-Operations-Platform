# M17 Local Observability Operations and Fault-Diagnostic Runbook

**M17 Round 2 correctness remediation (2026-11-13):** Candidate source fixes for all ten Round 2 findings are on the M17 feature branch, including fail-closed trace privacy, separated monitoring networks, paired diagnostic sampling, worker telemetry tests, alert recovery fixtures, and failure-safe source-locked evidence. No .NET, Docker, Prometheus, browser, performance or G17-S25 campaign has been executed or accepted for the repaired head. **G17 remains NOT ACCEPTED with 0/25 scenarios and 0/75 atomic assertions reviewed.** [Round 2 remediation register](M17_CORRECTNESS_ROUND2_REMEDIATION_20261113.md).

**M17 correctness remediation (2026-11-13):** Candidate fixes for findings F17-001 through F17-014 are committed on the isolated feature branch, but .NET compilation, local Compose startup, collector/alert behavior, three-browser results, performance and G17-S25 have not been executed or accepted. **G17 remains NOT ACCEPTED with 0/25 scenarios and 0/75 atomic assertions reviewed.** [Remediation register](M17_CORRECTNESS_REMEDIATION_20261113.md).

**Source candidate:** [M17 implementation candidate status](M17_IMPLEMENTATION_CANDIDATE_STATUS_20261112.md). G17 remains unverified and not accepted.

**State:** Unverified implementation candidate procedure, 2026-11-12. The M17 collector configuration, instrument catalogue, initial alert rules, dashboard files, optional Compose override and local runner exist on a separate feature branch. They have not been started or validated. Do not run them against retained resources.
**See:** [Architecture](M17_ARCHITECTURE_DESIGN.md), [implementation plan](M17_IMPLEMENTATION_PLAN.md), [policy proposals](M17_POLICY_PROPOSAL_20261112.md), [G17 verification](M17_VERIFICATION_PLAN.md).

## 1. Operator intent and constraints

M17 distinguishes **service liveness** from **dependency readiness**, **application failure** from **monitoring failure**, and **disabled/held** services from failed services. It provides evidence for later M18 incidents but does not create or close incidents or send external alerts. If the collector is down, do not interpret absent metrics as proof of a healthy API.

Never use the only retained project PostgreSQL database, existing Docker volumes, production secrets, historical source history or personally identifying shipment data to prove a telemetry failure. Never run broad Docker prune or delete unverified volumes. Use only a named disposable local project with confirmed ownership.

## Current isolated verification command

This procedure is restricted to the dedicated M17 candidate branch and an explicitly new E:-backed directory. Do not use it against retained PostgreSQL or existing Compose projects. Execute only in a clean Windows E: worktree after ensuring no unrelated service occupies the isolated ports.

Prerequisites: .NET 10 SDK, Node 22 or later, npm, Docker Engine with Compose, the exact source SHA, and a unique `ho-verify-m17-*` project name. Store five separate disposable secrets in the local process environment, not tracked files: `HARBOROPS_DB_PASSWORD`, `HARBOROPS_API_DB_PASSWORD`, `HARBOROPS_WORKER_DB_PASSWORD`, `HARBOROPS_M10_CORRECTION_PROOF_KEY`, and `HARBOROPS_M17_GRAFANA_PASSWORD`. Set `HARBOROPS_M17_LINK_KEY` to a separately generated base64 encoding of exactly 32 random bytes. The runner validates presence, and the application validates the link key's format and length.

After providing these disposable values, run from the isolated worktree:

```powershell
$sha = (git rev-parse HEAD).Trim()
.\scripts\run-g17-local.ps1 `
    -ExpectedSha $sha `
    -EvidenceDirectory 'E:\harborops-g17-evidence\candidate-unique-run' `
    -ProjectName 'ho-verify-m17-unique-run' `
    -StartDisposableStack -FaultProbe -StopOwnedContainers
```

To include the separately held tracking worker only inside the fresh disposable verification project, explicitly add `-VerifyDisposableWorker` alongside `-StartDisposableStack`. This is not a default action and does not authorize M9 activation in any retained environment. The worker probe preserves its own raw results and restores the owned worker after the fault check.

The evidence directory must not exist beforehand. Its sibling `candidate-unique-run-disposable-stack` must also not exist. The runtime profile uses loopback PostgreSQL port 155432, API 18080, web 15173 and Grafana 3301; first confirm they are free. The runner creates the database only in its separate E:-backed folder, checks project ownership labels, runs .NET and frontend checks, validates Prometheus rules, exercises the isolated browser suites and stores an unreviewed evidence manifest. It never issues `docker compose down -v`, prunes a volume, changes protected main or declares gate acceptance.

After a failure, preserve the evidence directory, the separate owned database directory and the source SHA for investigation. The `-StopOwnedContainers` option requests a stop of only its uniquely named services, not deletion. A failed build, browser case, rule test or source check is a failed campaign and must not be converted into a G17 pass. The `G17-S25` review occurs separately after raw evidence completeness is established.

## 2. Before optional-profile startup

1. Verify `git rev-parse HEAD` against the implementation candidate and record the exact SHA and dirty-worktree status. Protected main and accepted evidence branches remain untouched.
2. Confirm the approved E:-backed repository, Docker Desktop/WSL data, NuGet/npm caches and evidence paths per [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Do not write persistent project data to C:.
3. Check no conflicting Compose project already exists. The disposable profile must use the existing verified `ho-verify-*` naming guard with explicit confirmation and a unique project ID.
4. Provision only dummy accounts, dummy shipments and a freshly seeded test database. Do not copy or attach retained production/legacy volumes.
5. Verify the optional M17 override is enabled explicitly. Confirm the default Compose profile has no collector, dashboard, new background worker, exposed OTLP port or modified M9/M10/M11/M13/M15 feature flags.
6. Confirm the **approved P01-P09 design baseline** and exact, pinned container image/package versions. P02/P04/P09 numerical targets remain provisional until measured and recorded; P05 local alert thresholds require calibration before G17. Any changed limit must have a documented reviewed rationale. Design approval does not authorize profile startup or make G17 assertions pass.
7. Keep dashboard and any read interface bound to 127.0.0.1. Verify the collector only accepts internal-network OTLP; no internet-facing receiver or cloud exporter.
8. Record the intended fault, risk, abort conditions, project ownership labels and evidence file list before any fault injection.

The implementation must provide a tested command wrapper for approved startup, readiness wait, evidence capture and stop. A local verification wrapper is authored as `scripts/run-g17-local.ps1`, but it has not been executed or accepted for use against retained resources. Avoid inventing one in an operator procedure.

## 3. Basic health reading

| Observation | What it means | What it does not prove |
| --- | --- | --- |
| `/health/live` healthy | The API process responds to its liveness probe | Database, worker, telemetry or shipment readiness |
| `/health/ready` healthy | Existing DB connectivity and baseline seed check succeeded | All background work or notification delivery is enabled |
| Database readiness unhealthy | DB unavailable or required baseline not seeded | Telemetry collector is responsible |
| Worker state disabled/held | Feature is intentionally not running under existing flags/hold | A failed worker or page-worthy alert |
| Collector absent/degraded | Diagnostic data may be delayed or missing | Business API should fail or business transaction should rollback |
| Metrics absent | Insufficient/unknown telemetry | A healthy or zero-error service |

Always check the source timestamp and sample count in dashboards. For APIs with no requests in a window, display an insufficient-data condition rather than zero latency or 100% availability. For a worker under the M9 hold, status must explicitly say held, never backlog healthy.

## 4. Diagnostic fault walkthrough

**Example: a controlled shipment request fails while PostgreSQL is stopped.**

1. Record the clean test environment, trace policy, expected healthy baseline and last known request correlation.
2. Submit one synthetic authenticated request and capture its server-generated `X-Correlation-ID`. Do not capture cookies or hidden credentials.
3. Stop only the specifically owned disposable PostgreSQL service using the approved wrapper. Preserve container logs, disposable database identifiers and source labels.
4. Submit the expected failing request; record sanitized HTTP status/code, correlation ID, exception *class* (not raw message), trace/span IDs and timestamps.
5. Confirm `/health/live` remains responsive and `/health/ready` becomes unhealthy within its existing bounded timeout.
6. Confirm the telemetry signal for the API failure, classified DB dependency failure, and corresponding alert are present with safe attributes and correct triggering window.
7. Confirm an expected refusal/503 did not commit a partial shipment mutation, duplicate accepted event, bypass tenant auth, or disclose SQL/credentials.
8. Restore only the disposable database service. Verify readiness recovery, alert clear/hysteresis and no stale in-flight traces.
9. Cross-reference HTTP, logs, spans, metrics and alerts with the same source identity; explicitly report any missing asynchronous trace bridge instead of claiming full continuity.
10. Capture sanitized artifacts, test case result and digest manifest; mark the scenario PASS only after independent review of all expected observations.

**Exporter-only fault:** Stop only the optional collector while API and PostgreSQL continue. A valid business operation must still succeed normally, monitoring quality becomes unavailable/degraded, bounded telemetry drops are observable, memory stays within budget, and recovery does not require migration or database repair. Collector health is never substituted for business readiness.

**Worker-specific fault:** Enable a worker only under an approved disposable opt-in fixture, then crash/restart it under fixture ownership. Verify retries and existing database semantics, but do not release the M9 transport hold, activate M15 delivery by default or change accepted lock/lease semantics.

## 5. Dashboard and alert interpretation

- API request and latency panels use bounded route templates. Spike investigation starts at HTTP status class and operation family, never raw request paths.
- DB panels expose bounded outcome and readiness. No SQL text, parameters or database credentials.
- Worker panels show disabled, held, active, degraded and unknown distinctly. Backlog age has meaning only for enabled worker state and a known observation source.
- Exporter panels show freshness, failed export and dropped records. A broken collector must not silently produce healthy green operational indicators.
- Alert state ledger contains rule ID, bounded severity, evaluated window, threshold, observed aggregate, suppression reason, transition timestamp and sanitized correlation evidence reference.
- No alert itself is an M18 incident, no notification is sent to customers/users and no external channel is configured by M17.

## 6. Evidence handling and privacy

Allowed: synthetic API route names, operation family, status class, duration, approved role-neutral worker kind, sanitized correlation ID, trace IDs, known local service name, test-source commit, metric/alert state and numerical limits.

Forbidden: cookies, bearer tokens, CSRF tokens, passwords, database credentials/connection strings, raw JWTs, full SQL, SQL bindings, raw exception/provider messages, e-mail, user/session/tenant IDs as metric attributes, real shipment identifiers, arbitrary paths/queries, private audit metadata and opaque export proof keys. All test canaries must be synthetic. Before publishing artifacts, scan the entire binary/text archive and review screenshots and dashboards.

Use 256-bit SHA digest manifests over every retained evidence file. Independently verify every entry and reject unlisted/unhashed files, missing source SHA, incompatible source versions and skipped or retried required cases. Preserve historical failed outputs.

## 7. Local cleanup and data protection

After the test completes, **stop only the disposable project recorded by its unique project name and validated ownership labels** through the verified wrapper. Do not remove or prune any other containers, volumes, snapshots or local caches. Do not automatically delete even the disposable evidence until it has been reviewed and retained under an approved retention decision. Preserve the original E:-backed workspace and retained PostgreSQL volumes.

If a service fails to stop, is missing an ownership label, or its volume source is ambiguous, leave it untouched and record the blocker. No destructive cleanup is permitted by a generic monitoring fault runbook.

## 8. Gate and release handoff

G17 closure requires [the 25-scenario matrix](M17_VERIFICATION_PLAN.md), approved policies, independent raw evidence review and explicit acceptance. Once G17 is accepted, M18 can consume diagnostic references under its own incident CRUD policy and G16 audit rules. Neither this runbook nor G17 authorizes public dashboards, a hosted collector, paging integrations, production deployment, external telemetry export or G24 release.
