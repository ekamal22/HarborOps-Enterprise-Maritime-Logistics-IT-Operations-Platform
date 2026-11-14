# M17 Local Policy and Design Decision Register

**G17-S25 review checkpoint (2026-11-14):** Local campaign c7 at `085fc666` completed 72 technical witnesses, but independent S25 returned NOT PASS because several passing tests did not fully demonstrate their required behaviors. The source candidate now contains additional retained telemetry witnesses, a dedicated malformed-context export probe, rendered-state browser tests, browser correlation verification, and a stricter non-loopback negative control. **These corrections are not yet source-locked runtime verified. G17 NOT ACCEPTED; acceptance pending.** [Remediation register](M17_G17_S25_REMEDIATION_20261114.md).

**Current gate context:** P01-P09 remain approved for design, not independently verified G17 outcomes. Technical test authoring is complete for 72 assertions, but source-locked runtime verification and S25 independence remain outstanding. [Current G17 recovery](M17_G17_VERIFICATION_RECOVERY_20261113.md).

**Source candidate:** [M17 implementation candidate status](M17_IMPLEMENTATION_CANDIDATE_STATUS_20261112.md). G17 remains unverified and not accepted.

**Prepared and approved:** 2026-11-12. **Status:** **M17-P01 through M17-P09 approved as the implementation design baseline.** Numeric limits identified below remain provisional engineering targets until implementation measurements confirm or formally revise them. This document retains its original filename for stable links; earlier proposal text is preserved in Git history.
**Entry:** G3 accepted. **Design:** [M17 architecture](M17_ARCHITECTURE_DESIGN.md). **Verification:** [G17 matrix](M17_VERIFICATION_PLAN.md).

**Decision scope:** P01-P09 are approved for M17 design and implementation planning on 2026-11-12. The control direction, default-off behavior, privacy restrictions, local-only dashboard, health compatibility and fail-open telemetry behavior are approved. P02's 500-series reference ceiling, P04's 10% sampling/200-spans-per-second/2,048-record buffering reference limits and P09's 10% latency/15% memory overhead targets are **provisional and must be measured against the actual selected instrumentation**. P05's alert thresholds are approved as initial local test rules subject to evidence-based calibration, not as certified service-level objectives. A change to a provisional value requires recorded measurement, reasoning and review before G17, never silent relaxation.

These are M17-local decisions, not replacements for project-wide D-001 through D-008, and they do not modify accepted M9, M10, M13, M15 or M16 policies. **Approval of the design does not verify implementation, accept G17, authorize production routing, activate any feature, approve deletion of retained data, or dispatch tests.**

## M17-P01: Telemetry collection and export

**Approved design:** Keep existing sanitized JSON console logs and current health endpoints always available. New OpenTelemetry OTLP export, collector and local dashboard are **disabled by default**; enable together only in a specified trusted disposable/local profile. Missing collector must not affect API startup, readiness or business transactions. Reject public, unencrypted or externally addressed exporters in the default profile. No third-party hosted telemetry is authorized.

**Acceptance evidence:** Default Compose adds no external listener/collector, no egress; optional profile cannot be reached from non-loopback network; exporter outage leaves accepted API behavior intact.

## M17-P02: Metric dimensions and cardinality

**Approved design:** Allow only enumerated service kind, normalized route template or operation family, status class, outcome category, worker kind and dependency kind. Do not tag user, organization, session, shipment, source, reference, raw URL, SQL statement, exception string or correlation ID. Enforce a demonstrable upper bound of at most **500 active distinct custom metric series per service** in the disposable reference profile; label overflow must coalesce to a finite `unknown`/other bucket instead of allocating a new series.

**Reason:** Tenant-scoped identifiers are high cardinality and may disclose sensitive activity. The 500-series figure is an approved initial reference-test budget, **provisional until actual metric-series measurements**. It is not observed capacity or a release SLO; any necessary change must be justified and recorded before G17.

**Acceptance evidence:** Generate at least 10,000 distinct attacker-selected paths, headers and identifiers. The series count must remain within the approved budget and none of the forbidden labels may appear in scraped/exported raw evidence.

## M17-P03: Trace context and diagnostic data

**Approved design:** Keep existing server-generated `X-Correlation-ID` values and M16 audit correlation. New traces use W3C IDs after strict parsing. Reject/ignore invalid traceparent/tracestate and unlimited baggage. Only safe normalized span names, status, duration and whitelisted attributes can be exported. Exclude cookies, credentials, raw request/response bodies, full query paths, raw SQL statements/parameters and full exception message/stack by default. Asynchronous worker linkage must be either verified with safe persisted correlation or documented as incomplete, not fabricated.

**Acceptance evidence:** Valid/invalid header matrix, cross-tenant and privacy canaries, real API-to-database and API-to-enabled-worker paths.

## M17-P04: Sampling, queueing and degradation

**Approved design:** Reference profile head sampling target **10% of ordinary traces**, subject to an explicit **200 spans/second per API process** ceiling and **2,048 records maximum in-memory export queue** with bounded timeout. These numeric limits remain **provisional**. Validate against the actual OpenTelemetry SDK/exporter buffering and rate-limiter capabilities and pressure results; implement equivalent bounded behavior rather than assuming all figures exist as direct configuration knobs. Document and review any adjusted values before G17. Worker/exporter failure degrades telemetry with a measurable loss counter, not business availability. Do not block request threads to wait for exporter recovery.

**Acceptance evidence:** Compare with telemetry disabled, saturate with 10 times reference traffic, force collector outage, assert no unbounded memory growth or incorrect successful business results; document trace drop behavior.

## M17-P05: Alerts and noise controls

**Approved design:** Implement the following initial testable local-only rules. These thresholds are **approved initial local verification rules**, subject to measurement-based calibration and explicit documentation before G17. They are not a production SLO or authorization for external notifications.

| Alert | Candidate trigger | Candidate clear / suppression | Evidence requirement |
| --- | --- | --- | --- |
| API elevated failure rate | 5xx fraction > 1% over 5 minutes with at least 100 eligible requests | Clear after 5 minutes below 0.5%; low sample counts mark insufficient data | Deterministic request fixtures and no alert for 4xx-only traffic |
| API high p95 | p95 request duration > 1 second over 10 minutes with 100 eligible requests | Clear after 10 minutes below 0.8 seconds | Controlled latency fault, verified histogram |
| Database unavailable | 2 consecutive failed readiness samples, 15 seconds apart | 2 consecutive healthy samples | PostgreSQL stop/restart in disposable profile |
| Active worker backlog stale | oldest eligible work > 120 seconds for 2 evaluations while worker enabled | Below 60 seconds for 2 evaluations | Disposable worker fixture, no alert if worker held or intentionally disabled |
| Telemetry signal absent | expected exporter heartbeat/freshness missing > 90 seconds while profile is enabled | 2 fresh samples | Kill collector, mark monitoring degraded not business healthy |
| Exporter drops | any sustained dropped-signal increase over 2 successive evaluations | Stop increasing for 2 evaluations | Saturate queue with synthetic data |

**Operational policy:** Severity labels are informational/degraded/critical by impact, not a substitute for M18 incident states. Aggregate repeated alerts with stable fingerprints, recorded transitions and cooldown; never page an external destination. Simulate startup, zero traffic, held features, exporter isolation and recovery with no noisy or misleading alerts.

## M17-P06: Local telemetry retention and disposal

**Approved design:** For the dedicated disposable/opt-in local telemetry store only, bound searchable metric samples and traces to **24 hours or 250 MiB per standalone verification profile**, whichever limit is reached first. Explicit data disposal is limited to resources created and positively identified as owned by that disposable M17 verification profile and only after source/evidence capture. Do not configure or run cleanup on existing project data, any retained database volume, M8 event histories, M16 audits, M11 exception history or M15 notification receipts. No automatic cross-volume purge or retention worker.

**Acceptance evidence:** Confirm lifecycle and resource ownership, bounded storage, and failure-safe behavior when the telemetry volume is full. Use E:-backed storage for local evidence.

## M17-P07: Dashboard access and privacy

**Approved design:** Local dashboard is a **developer/operator diagnostic surface only**. Bind its HTTP interface to 127.0.0.1 under an explicit optional Compose profile; no public ingress, no embedding in user-facing shipment workspaces, and no new tenant-wide application permission. If a local dashboard product requires login, the disposable secret must remain outside tracked files, sanitized logs and evidence. Do not infer that network loopback alone authorizes future hosted use.

**Acceptance evidence:** Network exposure tests, unauthorized API/scrape attempts, cross-origin/browser checks, no sensitive user identifiers or business payloads in panels.

## M17-P08: Health endpoint compatibility

**Approved design:** Preserve existing `/health/live` and `/health/ready` paths, status codes, JSON contracts and seed/database semantics. Do not make telemetry collector, dashboard, held M9 worker or disabled optional services readiness prerequisites. Health queries must remain lightweight, finite and safe under database outage.

**Acceptance evidence:** Existing health contract and liveness/readiness integration regressions plus bounded timeout and fault tests.

**P09 measurement rule confirmed 2026-11-14:** The 10 percent p95 and 15 percent memory budgets were not changed. The evidence gate evaluates p95 on pooled observations across three matched rounds per route and memory on the mean of three paired percentage increases. Every per-pair figure and raw sample must be retained; noise or repeated runs do not silently excuse an out-of-budget result. This change and its historical failures are subject to independent S25 scrutiny. See [decision record](DECISIONS.md) and [local c7 measurements](M17_G17_LOCAL_VERIFICATION_RESULT_20261114.md).

## M17-P09: Measured overhead and rollout boundary

**Approved design:** Default-off source must not add a measurable dependency on collector availability. For a matched local reference workload under normal load, set a provisional budget of **p95 request-latency increase <= 10%** and **peak process working-set increase <= 15%** relative to telemetry-disabled baseline. Also report absolute values, confidence/variance, environment, sampling rate and source SHA; a small baseline or noisy runner must not be used to manufacture a pass. These remain **provisional engineering acceptance targets**, not established measurements; evaluate the feasibility and statistical validity of these figures against the matched reference workload before G17 and document any adjustment.

**Acceptance evidence:** Three paired repetitions of a documented representative workload, matched warmup/same hardware/same database source; 10-times-burst and exporter outage probes; positive logs and resource samples. Failure requires remediation or explicit policy reconsideration, not silent threshold relaxation.

## Decision checklist

| ID | Status on 2026-11-12 | Needed before |
| --- | --- | --- |
| P01 | Approved design; implementation evidence pending | First exporter/config implementation |
| P02 | Approved design; numeric cap calibration pending | Custom metric instruments |
| P03 | Approved design; implementation evidence pending | Span enrichment or propagation |
| P04 | Approved design; numeric limits calibration pending | Sampling/export pipeline |
| P05 | Approved initial local rules; trigger/clear calibration pending | Monitoring dashboard and alert rules |
| P06 | Approved design; storage evidence pending | Telemetry volume lifecycle and disposal |
| P07 | Approved design; listener and auth evidence pending | Optional dashboard listener |
| P08 | Approved design; regression evidence pending | Any change near current health paths |
| P09 | Approved design; latency and memory measurement pending | Performance evidence and G17 judgment |

Record approvals, corrections and dates here or in a separate accepted decision file before changing their status. The presence of numeric candidates is not consent to activate, deploy, retain, delete, page or alter production data. M17 technical implementation and G17 acceptance are separate later gates.
