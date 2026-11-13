# G17 Source-Locked Verification and Acceptance Plan

**Current M17/G17 status (2026-11-13):** All 72 technical G17 assertions have authored source witnesses in the 75-assertion coverage register; the three G17-S25 independent reviewer assertions remain pending. Strict no-skip source preflight passed, and source-locked disposable verification is underway after retained failed runs and corrections. Partial unit, contract and frontend passes do not constitute complete G17 evidence. No G17 technical or independent acceptance decision has been issued. **G17 NOT ACCEPTED; acceptance pending.** [Readiness](M17_G17_S25_TEST_READINESS_20261113.md) | [Verification recovery and exact runs](M17_G17_VERIFICATION_RECOVERY_20261113.md).

**Source candidate:** [M17 implementation candidate status](M17_IMPLEMENTATION_CANDIDATE_STATUS_20261112.md). G17 remains unverified and not accepted.

**Prepared:** 2026-11-12. **Gate:** G17 for M17 observability and service health.
**State:** Implementation candidate under source-locked, disposable verification; authored technical coverage 72/72 with 3 independent reviewer assertions outstanding. **0/25 scenarios and 0/75 assertions accepted for G17.** Partial test passes and failed campaigns are retained, not counted as acceptance.
**Entry:** G3 accepted, **M17-P01 through P09 approved as the design baseline on 2026-11-12**, and M17-01 through M17-04 implemented in a source-locked candidate. P02/P04/P09 provisional numerical limits and P05 local alert thresholds must be measured and their final reference values recorded before a G17 recommendation.
**References:** [architecture](M17_ARCHITECTURE_DESIGN.md), [implementation](M17_IMPLEMENTATION_PLAN.md), [policy proposals](M17_POLICY_PROPOSAL_20261112.md), [runbook](M17_OPERATIONS_RUNBOOK.md).

## 1. Evidence rules

All executable scenarios require actual behavioral observations, not the existence of source or a test method. For every applicable atomic assertion record: expected outcome, actual outcome, exact SHA, test method/fixture, environment, raw output artifact, timestamps, exit code, negative control and reviewer disposition. Mark `PASS`, `FAIL`, `BLOCKED`, `NOT EXECUTED` or narrowly justified `NOT APPLICABLE` per assertion. Do not count skipped tests, expected failures, green wrapper jobs without underlying results, or stale SHA evidence as passes.

Use a restricted, owned, disposable PostgreSQL/Compose environment. Preserve accepted health, tenant/session and M9/M10/M11/M15/M16 invariants. Do not start held transport, notification delivery, projector or rule evaluator except where a specific approved disposable test opts in. Three-browser runs must be fresh, authenticated where needed, with exact case totals and no flakes/retries/skips for required tests.

The source-locked result is complete only when raw source SHA, sanitizer report, TRX/Playwright results, trace and metric sample snapshots, alert transitions, resource bounds, local network exposure checks and evidence hash manifest agree. A reviewer must inspect underlying evidence independently of the implementation author's summary. G17-S25 remains a separate manual decision.

**Approved-policy conformance check:** G17-S01-A02 must verify implementation against all nine approved design decisions, with exact measurement evidence for the initial P02 500-series, P04 10%/200 spans/sec/2,048-record and P09 10% p95/15% peak-memory values, or a documented reviewed adjustment before G17. For P05 use the approved initial local alert rules and record trigger/clear calibration. An approved design decision is not a PASS result for any G17-S01 through S25 assertion.

## 2. Scenario and witness matrix

| Scenario | Required behavior with atomic assertions | Planned technical witness | Layer / owner |
| --- | --- | --- | --- |
| **G17-S01 Source/contract conformity** | S01-A01 all four work packages implemented; S01-A02 approved P01-P09 and exact instrument mapping match source; S01-A03 no unapproved default service, API or migration changes | Source tree inventory, contract checker, diff against accepted main and signed policy register | Source / review |
| **G17-S02 Metric schema and type** | S02-A01 each metric has exact name/type/unit; S02-A02 no duplicate request counters; S02-A03 histogram produces nonzero latency samples | Meter listener unit tests, raw collector samples, metric dictionary comparison | Unit / metrics |
| **G17-S03 Existing health preservation** | S03-A01 live stays responsive with DB down; S03-A02 ready goes unavailable on DB stop or absent seed within budget; S03-A03 existing JSON/status paths unchanged | Real HTTP plus disposable DB stop/restart and health-contract regressions | Integration / health |
| **G17-S04 Server-controlled correlation** | S04-A01 response correlation generated on server; S04-A02 error and structured log scope use same value; S04-A03 client-supplied correlation cannot spoof it | Header injection, JSON/log match and audit regression | Contract / tracing |
| **G17-S05 Label allowlist** | S05-A01 metric attributes contain only approved keys; S05-A02 no user/tenant/request/SQL fields; S05-A03 unknown route has stable fallback | Raw OTLP scrape and canary scanner | Security / metrics |
| **G17-S06 Cardinality and memory bound** | S06-A01 10,000 distinct routes/identifiers stay within approved active series cap; S06-A02 overflow coalesces; S06-A03 memory does not grow without bound | High-cardinality generator, listener census and memory samples | Load / security |
| **G17-S07 Valid context propagation** | S07-A01 W3C valid trace context accepted without altering auth; S07-A02 API -> safe DB work connected by trace; S07-A03 original audit correlation preserved | Real HTTP plus spans/logs and authorized DB read | Integration / tracing |
| **G17-S08 Invalid context rejection** | S08-A01 malformed traceparent/tracestate handled safely; S08-A02 oversized baggage ignored; S08-A03 no raw header is exported, no 500 | Parser fuzz/canary tests and trace inspection | Security / tracing |
| **G17-S09 DB timing and outages** | S09-A01 successful bounded DB operation emits duration/outcome; S09-A02 outage emits classified failure without secret SQL; S09-A03 business error contract remains unchanged | Disposable PG interruption, sanitized spans/JSON, HTTP response | Integration / DB |
| **G17-S10 Worker status and hold** | S10-A01 worker that is intentionally disabled/held remains labelled disabled/held; S10-A02 enabled disposable worker emits correct attempts/outcomes; S10-A03 no worker auto-start | Default Compose check and explicit opt-in fixture | Integration / worker |
| **G17-S11 Asynchronous causal linking** | S11-A01 authorized worker work links safely to source or documented correlation; S11-A02 fabricated parent/tenant trace context rejected; S11-A03 replay/retry has unique bounded spans | Inbox/work/retry test with raw trace and existing source IDs checked privately | Integration / worker |
| **G17-S12 Bounded exporter failures** | S12-A01 collector down does not change API response, transaction or readiness; S12-A02 export queue bounded/drops counted; S12-A03 exporter recoverable without manual data repair | Kill collector, burst injection, memory/drops and application comparison | Fault / export |
| **G17-S13 Security redaction** | S13-A01 secrets, cookies, personal canaries and SQL values absent from logs, spans and metric labels; S13-A02 sanitized errors still diagnose component and type; S13-A03 retained M16 audit is not exposed | Seeded canary scanner across raw artifacts and role isolation checks | Security / privacy |
| **G17-S14 Tenant and authorization isolation** | S14-A01 cross-organization request produces same denial as baseline; S14-A02 telemetry does not disclose tenant detail; S14-A03 instrumentation cannot elevate a role | Two-tenant role/session matrix and sanitized evidence | Security / API |
| **G17-S15 Restart and cancellation** | S15-A01 process restart and canceled request do not leak stale spans or requests; S15-A02 shutdown drain is bounded; S15-A03 no duplicated business effect or worker lease change | Process/cancellation/recovery probes with original state invariants | Fault / lifecycle |
| **G17-S16 Private stack exposure** | S16-A01 default Compose contains no collector/dashboard; S16-A02 optional dashboard bound loopback; S16-A03 collector not exposed externally, untrusted reads denied | Compose config, listener/network probe from separate namespace, local permissions | Security / ops |
| **G17-S17 Alert triggers and clears** | S17-A01 controlled 5xx/latency/readiness faults trigger configured alerts; S17-A02 threshold/window evidence exact; S17-A03 clear/hysteresis works after recovery | Deterministic signal replay, live controlled fault and alert state transition ledger | Alerts / ops |
| **G17-S18 Low-traffic and disabled suppression** | S18-A01 zero/low traffic is unknown/insufficient, not healthy; S18-A02 held workers cause no false failure alert; S18-A03 cool-down deduplicates repeat alerts | Startup/quiet profile test and worker flag matrix | Alerts / ops |
| **G17-S19 Local dashboard rendering** | S19-A01 critical panels show actual live measurement; S19-A02 unknown/unavailable have explicit distinct states; S19-A03 alert links only sanitized diagnostic evidence | Chromium, Firefox, WebKit browser suites and raw collector crosscheck | Browser / UI |
| **G17-S20 End-to-end fault journey** | S20-A01 browser/API fault yields server correlation; S20-A02 trace to DB/worker or documented async bridge resolves; S20-A03 dashboard alert names correct component without private payload | Full diagnostic session with screenshots, spans, logs, SQL/alert witness | End-to-end |
| **G17-S21 Performance overhead** | S21-A01 paired workload measurement meets approved p95 and memory budgets; S21-A02 exact environment/variance reported; S21-A03 pressure does not block business | Three paired normal/burst benchmark runs with per-sample raw output | Load / resource |
| **G17-S22 Storage and retention isolation** | S22-A01 local telemetry capped and expires only its own disposable data; S22-A02 full store degrades observation safely; S22-A03 M8/M11/M15/M16 retained data unchanged | Storage-full fixture, read-only baseline checks, verified volume ownership | Fault / retention |
| **G17-S23 Defaults and regression** | S23-A01 M9/M10/M11/M13/M15 opt-in/hold semantics unchanged; S23-A02 accepted authorization/health/audit APIs preserved; S23-A03 changed source build, .NET regression and targeted browser suites pass | Default Compose and test matrix with before/after source inventory | Regression |
| **G17-S24 Evidence integrity** | S24-A01 every claimed assertion maps to real source-locked result; S24-A02 source hashes and manifests complete/no unlisted files; S24-A03 negative control rejects tampering, skip and wrong SHA | Evidence verifier tests, manifest digest and JSON/TRX reconciliation | Evidence / reviewer |
| **G17-S25 Independent review and gate recommendation** | S25-A01 separate reviewer checks raw source, privacy, metrics and full cases; S25-A02 policy and procedural exceptions disclosed; S25-A03 reviewer issues explicit signed PASS/NOT PASS before G17 acceptance | Independent review file and formal G17 decision record | Manual / approval |

**Atomic scope:** 25 scenarios, 75 distinct A01-A03 assertions. Current gate progress: 0/75 accepted for M17; source-locked verification is in progress. If a source implementation changes scope, update this inventory before testing; do not infer missing assertions from a passing scenario title.

## 3. Test source and minimum campaign

| Campaign | Tests / required evidence | Required tooling, constraints |
| --- | --- | --- |
| Source and contracts | Exact metric schema, source allowlists, route contracts, configuration default-off, policy decisions | No runtime alterations to accepted code; source inventory |
| Unit | Options validation; meters/listeners; sampling, cardinality, redaction, alert state machine, trace header parser | .NET test projects, fixed synthetic time |
| Contract | Liveness/readiness, JSON errors and correlation, case-sensitive header behavior, no public telemetry endpoint | HTTP contract tests with negative cases |
| Restricted PostgreSQL integration | Seeded/current DB, outage, latency, tenant access, trace span, recovery, immutability | Fresh disposable PostgreSQL and runtime role |
| Worker integration | Enabled/held/disabled distinctions, opt-in services, claim/retry and async causal linkage | No M9 hold release, no live backlog |
| Browser | Authenticated local dashboard/alert/diagnostic flows and regressions on Chromium, Firefox and WebKit | No skips, flaky/retry cases; explicit counts |
| Fault and security | Collector disappearance, invalid header, long path, sensitive canary, queue full, process crash, storage full, cross-tenant | Failure must not alter accepted data/authorization |
| Performance | Three paired runs, latency p95, memory, load surge, measurement uncertainty, resource limits | Same SHA, fixtures, host and warmup; compare export on/off |
| Evidence integrity | SHA, raw TRX, Playwright JSON, manifest and independent file digest review | Fail on missing, duplicate, wrong SHA or unlisted files |

The implementation branch decides exact commands and test names in its evidence plan. Suggested commands, run only when authorized and on a disposable environment: `dotnet build HarborOps.sln -c Release -warnaserror`, `dotnet test` for specifically enumerated projects, `npm --prefix web test`, `npm --prefix web run build`, and `npx playwright test` for exact authenticated G17 specs in each engine. Do not launch a blanket run of unrelated accepted gates just to inflate coverage. Avoid unapproved GitHub Actions dispatch. Any future hosted workflow must be locked to its exact verification branch/source and must not be activated by docs-only pushes.

## 4. Artifact contract and independent review

Suggested disposable evidence layout, final paths confirmed during implementation:

- `source-sha.txt`, `git-diff.txt`, `dependencies.txt`, `policy-decisions.md`.
- `unit.trx`, `contract.trx`, `integration.trx`, sanitized test logs, before/after HTTP contract JSON.
- `metrics-samples.json`, `spans-sanitized.json`, `logs-redacted.json`, `alert-transitions.json`, `service-state.json`, `network-ports.txt`.
- Per-engine Playwright JSON, screenshots only where scrubbed, result counts and test names.
- `benchmark.csv`, machine/OS/runtime configuration, profile toggles, telemetry-disabled baseline and measured thresholds.
- `privacy-scan.json`, `source-config-digest.json`, `volume-ownership.json`, `sha256.txt`.

No raw database dump, session cookie, key, token, arbitrary URL, real email, personal data, proprietary shipment content, unredacted exception or credential is permitted in evidence. Evidence lives only in the approved E:-backed location or specifically approved retained CI artifacts with bounded retention. A reviewer independently calculates file hashes, parses every test result including method and parameterized case counts, inspects captured signals, reviews missing/partial scenarios and verifies test source equivalence to the candidate.

**Acceptance verdict rules:** G17 cannot be recommended on metrics dashboards alone, one passing workflow, unfinished negative scenarios, unvalidated provisional numeric limits or undocumented changes to approved policies, unverifiable source artifacts, stale tests or unreviewed evidence. Technical PASS requires all required applicable executable assertions plus correct controlled fault behavior. Formal G17 requires independent review and explicit acceptance decision. Any procedural exception must be specifically disclosed and approved; it is never silently treated as independent attestation.

## 5. Revision and closure

When implemented, keep a named scenario ledger with each atomic assertion and exact evidence URI/digest. Document failed attempts and targeted remediation. Do not retroactively mark failing runs green. Close M17 on its own reviewed branch/PR after evidence and decision, then update the current status, roadmap and M18 prerequisite records. Keep production deployment off unless independently authorized.
