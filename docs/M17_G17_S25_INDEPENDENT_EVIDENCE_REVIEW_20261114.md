# G17-S25 Evidence Review and Acceptance Recommendation

**G17-S25 findings tracking (2026-11-14):** All eight open technical, evidence, performance and procedural items after the second-pass review are recorded in the [S25 open-findings register](M17_G17_S25_OPEN_FINDINGS_REGISTER_20261114.md). This is an issue log, not a correction or verified rerun. The G17 technical campaign is green, but **G17 NOT ACCEPTED**; review and acceptance pending.

**Recorded:** November 14, 2026
**Source:** `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`
**GitHub Actions:** [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851)
**Raw evidence artifact:** `g17-raw-evidence-43541279851-1`, artifact ID `14093266896`
**Technical verification:** PASS, 47/47 stages and 72/72 technical witnesses
**Review recommendation:** **NOT PASS**
**Gate:** **G17 NOT ACCEPTED.** Acceptance pending. No main merge or production activation.

## Review boundary

The hosted campaign is authentically green. The exact source SHA matches the manifest, run ledger, expected-artifact contract and technical assertion ledger. The ledger contains 75 unique A01-A03 assertion identities for 25 scenarios, of which 72 are TECHNICAL_PASS and three are PENDING_INDEPENDENT_REVIEW. All 47 campaign stages passed with exit code zero. Independently parsed TRX totals were 319 unit, 60 contract, 54 scoped integration and 34 regression executions, all passed with zero skips. Node JUnit contains 67 passes. Dashboard browser JSON contains 24 passing executions across Chromium, Firefox and WebKit with no skips, retries or flaky cases; a separate cardinality browser case passed.

The manifest has 45 indexed entries. The downloadable archive contains 43 indexed files with correct byte counts and SHA-256 values, plus the manifest itself. The two absent non-required hidden Playwright `.last-run.json` records are both 45 bytes and have the same published SHA-256 `91d1c43004802cd49950d78eb11c8fa7d05da8ffffe219a8b13b2f561bc00903`. This digest matches Playwright's standard three-line `{status: passed, failedTests: []}` metadata representation. Their original bytes were not downloadable; the actual case-level Playwright reports are available.

The retained export privacy evidence includes 40 API traces and 80 spans, 149 metric series and a 2,092,578-byte API/collector/Tempo log snapshot. A separate malformed-context stage retained 24 Tempo trace documents after 960 malformed-context requests, and the worker journey retained 19 trace documents, 694 metric series and five safe matched trace pairs. Checks of retained span and metric keys found no prohibited tenant, cookie, credential, SQL, user or session attribute keys or the known malformed-canary prefix. Readiness actually rendered Unknown, Unavailable and Ready in all three browser engines. The live 5xx event recorded 120 actual 503 responses, measured rate and volume, and its firing alert.

## Unclosed findings

### F01 Major: G17-S18-A01 API low-sample visualization

The zero/insufficient-traffic browser test reads panel JSON noValue settings, checks historic empty Prometheus queries and verifies panel headings are visible. It does not assert rendered contents of the API request-rate, API 5xx or p95 panels. The new renderedPanelText test checks PostgreSQL readiness only. Require actual three-engine rendered low-volume and no-data API panel outcomes, with measured sample controls and retained source-locked panel states.

### F02 Major: G17-S20 complete diagnostic journey

The new browser correlation check uses Playwright's `browser.newContext().request` API client rather than a page-originated failing browser request. The worker trace-pair and database alert witnesses concern separate synthetic incidents. Their combined PASS claims do not prove one continuous browser-to-API-to-observation causal journey on the same server correlation, and the archived run contains no diagnostic screenshot. Require one page-originated fault and a single correlation aligned across the observed response, sanctioned trace or async bridge, logs, alert and sanitized dashboard evidence.

### F03 Major: G17-S05-A02 and G17-S13-A01 participating worker log privacy

The retained privacy checker captures `docker compose logs --tail 15000 api otelcol tempo` only. The same full campaign exercises a real opted-in worker. Although worker spans and metric series are separately retained and inspected, its logs are not in the raw log snapshot. Require a bounded synthetic-canary negative scan and retained log witness for the worker and other participating services, or explicitly narrow and reapprove the expected log-privacy scope before acceptance.

### F04 Moderate: downloaded evidence packaging

Two hidden Playwright metadata files are indexed in the source-run manifest but were omitted from the Actions artifact. Their known 45-byte success content matches their stored SHA-256, and raw browser/cardinality results are present, so this is not evidence of failed tests. Document it as a S24-A02 procedural exception or include the original files on a future upload; do not claim all 45 original bytes were downloaded.

### F05 Qualified: P09 pooled overhead policy

The approved three-pair pooled rule passes: baseline +2.47% p95, authorized +6.78% p95 and mean peak-memory +2.35%, with unchanged 10% and 15% budgets. One third-round baseline p95 pair rises +22.69%, with a small 4.99 ms off baseline. This is expressly reported by the gate and retained in raw samples. It is not by itself a failure of the approved pooled rule, but requires variance and aggregation justification in the final decision.

### F06 Procedure: separate reviewer and sign-off

G17-S25-A01 requires a separate reviewer, and G17-S25-A03 requires a signed explicit PASS or NOT PASS before acceptance. Review participation overlaps with the corrective implementation. This review records an unsigned NOT PASS recommendation and does not establish an independent reviewer signature or waive reviewer separation. Another reviewer must complete sign-off, or a separately authorized procedural exception must be recorded. No exception is presumed.

## Scenario-by-scenario assessment

| Scenario | Technical campaign | Review observation |
| --- | --- | --- |
| G17-S01 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S02 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S03 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S04 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S05 | 3 technical PASS | Broad privacy coverage does not retain optional worker logs |
| G17-S06 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S07 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S08 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S09 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S10 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S11 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S12 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S13 | 3 technical PASS | Retained privacy log scan includes api, otelcol and tempo, not worker |
| G17-S14 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S15 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S16 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S17 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S18 | 3 technical PASS | Readiness state is rendered; low-sample API rate, 5xx and p95 are not |
| G17-S19 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S20 | 3 technical PASS | No page-originated single-correlation browser/API/trace/alert diagnostic journey |
| G17-S21 | 3 technical PASS | Approved pooled metric passes, one per-pair baseline p95 increases 22.69% |
| G17-S22 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S23 | 3 technical PASS | No specific adverse finding in inspected raw witness and source |
| G17-S24 | 3 technical PASS | Two optional Playwright metadata files indexed but omitted from download |
| G17-S25 | 3 independent pending | Review separation and signed independent acceptance are outstanding |

## All 75 atomic assertions

| Assertion | Campaign status | Expected behavior | Review assessment |
| --- | --- | --- | --- |
| G17-S01-A01 | TECHNICAL_PASS | all four work packages implemented | No specific adverse finding |
| G17-S01-A02 | TECHNICAL_PASS | approved P01-P09 and exact instrument mapping match source | No specific adverse finding |
| G17-S01-A03 | TECHNICAL_PASS | no unapproved default service, API or migration changes | No specific adverse finding |
| G17-S02-A01 | TECHNICAL_PASS | each metric has exact name/type/unit | No specific adverse finding |
| G17-S02-A02 | TECHNICAL_PASS | no duplicate request counters | No specific adverse finding |
| G17-S02-A03 | TECHNICAL_PASS | histogram produces nonzero latency samples | No specific adverse finding |
| G17-S03-A01 | TECHNICAL_PASS | live stays responsive with DB down | No specific adverse finding |
| G17-S03-A02 | TECHNICAL_PASS | ready goes unavailable on DB stop or absent seed within budget | No specific adverse finding |
| G17-S03-A03 | TECHNICAL_PASS | existing JSON/status paths unchanged | No specific adverse finding |
| G17-S04-A01 | TECHNICAL_PASS | response correlation generated on server | No specific adverse finding |
| G17-S04-A02 | TECHNICAL_PASS | error and structured log scope use same value | No specific adverse finding |
| G17-S04-A03 | TECHNICAL_PASS | client-supplied correlation cannot spoof it | No specific adverse finding |
| G17-S05-A01 | TECHNICAL_PASS | metric attributes contain only approved keys | No specific adverse finding |
| G17-S05-A02 | TECHNICAL_PASS | no user/tenant/request/SQL fields | COVERAGE_GAP: participating worker log privacy |
| G17-S05-A03 | TECHNICAL_PASS | unknown route has stable fallback | No specific adverse finding |
| G17-S06-A01 | TECHNICAL_PASS | 10,000 distinct routes/identifiers stay within approved active series cap | No specific adverse finding |
| G17-S06-A02 | TECHNICAL_PASS | overflow coalesces | No specific adverse finding |
| G17-S06-A03 | TECHNICAL_PASS | memory does not grow without bound | No specific adverse finding |
| G17-S07-A01 | TECHNICAL_PASS | W3C valid trace context accepted without altering auth | No specific adverse finding |
| G17-S07-A02 | TECHNICAL_PASS | API -> safe DB work connected by trace | No specific adverse finding |
| G17-S07-A03 | TECHNICAL_PASS | original audit correlation preserved | No specific adverse finding |
| G17-S08-A01 | TECHNICAL_PASS | malformed traceparent/tracestate handled safely | No specific adverse finding |
| G17-S08-A02 | TECHNICAL_PASS | oversized baggage ignored | No specific adverse finding |
| G17-S08-A03 | TECHNICAL_PASS | no raw header is exported, no 500 | No specific adverse finding |
| G17-S09-A01 | TECHNICAL_PASS | successful bounded DB operation emits duration/outcome | No specific adverse finding |
| G17-S09-A02 | TECHNICAL_PASS | outage emits classified failure without secret SQL | No specific adverse finding |
| G17-S09-A03 | TECHNICAL_PASS | business error contract remains unchanged | No specific adverse finding |
| G17-S10-A01 | TECHNICAL_PASS | worker that is intentionally disabled/held remains labelled disabled/held | No specific adverse finding |
| G17-S10-A02 | TECHNICAL_PASS | enabled disposable worker emits correct attempts/outcomes | No specific adverse finding |
| G17-S10-A03 | TECHNICAL_PASS | no worker auto-start | No specific adverse finding |
| G17-S11-A01 | TECHNICAL_PASS | authorized worker work links safely to source or documented correlation | No specific adverse finding |
| G17-S11-A02 | TECHNICAL_PASS | fabricated parent/tenant trace context rejected | No specific adverse finding |
| G17-S11-A03 | TECHNICAL_PASS | replay/retry has unique bounded spans | No specific adverse finding |
| G17-S12-A01 | TECHNICAL_PASS | collector down does not change API response, transaction or readiness | No specific adverse finding |
| G17-S12-A02 | TECHNICAL_PASS | export queue bounded/drops counted | No specific adverse finding |
| G17-S12-A03 | TECHNICAL_PASS | exporter recoverable without manual data repair | No specific adverse finding |
| G17-S13-A01 | TECHNICAL_PASS | secrets, cookies, personal canaries and SQL values absent from logs, spans and metric labels | COVERAGE_GAP: worker logs absent from retained scan |
| G17-S13-A02 | TECHNICAL_PASS | sanitized errors still diagnose component and type | No specific adverse finding |
| G17-S13-A03 | TECHNICAL_PASS | retained M16 audit is not exposed | No specific adverse finding |
| G17-S14-A01 | TECHNICAL_PASS | cross-organization request produces same denial as baseline | No specific adverse finding |
| G17-S14-A02 | TECHNICAL_PASS | telemetry does not disclose tenant detail | No specific adverse finding |
| G17-S14-A03 | TECHNICAL_PASS | instrumentation cannot elevate a role | No specific adverse finding |
| G17-S15-A01 | TECHNICAL_PASS | process restart and canceled request do not leak stale spans or requests | No specific adverse finding |
| G17-S15-A02 | TECHNICAL_PASS | shutdown drain is bounded | No specific adverse finding |
| G17-S15-A03 | TECHNICAL_PASS | no duplicated business effect or worker lease change | No specific adverse finding |
| G17-S16-A01 | TECHNICAL_PASS | default Compose contains no collector/dashboard | No specific adverse finding |
| G17-S16-A02 | TECHNICAL_PASS | optional dashboard bound loopback | No specific adverse finding |
| G17-S16-A03 | TECHNICAL_PASS | collector not exposed externally, untrusted reads denied | No specific adverse finding |
| G17-S17-A01 | TECHNICAL_PASS | controlled 5xx/latency/readiness faults trigger configured alerts | No specific adverse finding |
| G17-S17-A02 | TECHNICAL_PASS | threshold/window evidence exact | No specific adverse finding |
| G17-S17-A03 | TECHNICAL_PASS | clear/hysteresis works after recovery | No specific adverse finding |
| G17-S18-A01 | TECHNICAL_PASS | zero/low traffic is unknown/insufficient, not healthy | COVERAGE_GAP: API low-sample panels not directly rendered |
| G17-S18-A02 | TECHNICAL_PASS | held workers cause no false failure alert | No specific adverse finding |
| G17-S18-A03 | TECHNICAL_PASS | cool-down deduplicates repeat alerts | No specific adverse finding |
| G17-S19-A01 | TECHNICAL_PASS | critical panels show actual live measurement | No specific adverse finding |
| G17-S19-A02 | TECHNICAL_PASS | unknown/unavailable have explicit distinct states | No specific adverse finding |
| G17-S19-A03 | TECHNICAL_PASS | alert links only sanitized diagnostic evidence | No specific adverse finding |
| G17-S20-A01 | TECHNICAL_PASS | browser/API fault yields server correlation | COVERAGE_GAP: request client not page-originated |
| G17-S20-A02 | TECHNICAL_PASS | trace to DB/worker or documented async bridge resolves | SCENARIO_GAP: unrelated worker correlation |
| G17-S20-A03 | TECHNICAL_PASS | dashboard alert names correct component without private payload | SCENARIO_GAP: alert from separate fault |
| G17-S21-A01 | TECHNICAL_PASS | paired workload measurement meets approved p95 and memory budgets | QUALIFIED: pooled P09 pass, one +22.69% individual p95 |
| G17-S21-A02 | TECHNICAL_PASS | exact environment/variance reported | No specific adverse finding |
| G17-S21-A03 | TECHNICAL_PASS | pressure does not block business | No specific adverse finding |
| G17-S22-A01 | TECHNICAL_PASS | local telemetry capped and expires only its own disposable data | No specific adverse finding |
| G17-S22-A02 | TECHNICAL_PASS | full store degrades observation safely | No specific adverse finding |
| G17-S22-A03 | TECHNICAL_PASS | M8/M11/M15/M16 retained data unchanged | No specific adverse finding |
| G17-S23-A01 | TECHNICAL_PASS | M9/M10/M11/M13/M15 opt-in/hold semantics unchanged | No specific adverse finding |
| G17-S23-A02 | TECHNICAL_PASS | accepted authorization/health/audit APIs preserved | No specific adverse finding |
| G17-S23-A03 | TECHNICAL_PASS | changed source build, .NET regression and targeted browser suites pass | No specific adverse finding |
| G17-S24-A01 | TECHNICAL_PASS | every claimed assertion maps to real source-locked result | No specific adverse finding |
| G17-S24-A02 | TECHNICAL_PASS | source hashes and manifests complete/no unlisted files | QUALIFIED: two manifest-indexed Playwright metadata files omitted |
| G17-S24-A03 | TECHNICAL_PASS | negative control rejects tampering, skip and wrong SHA | No specific adverse finding |
| G17-S25-A01 | PENDING_INDEPENDENT_REVIEW | separate reviewer checks raw source, privacy, metrics and full cases | NOT_PASS: incomplete behavior and reviewer separation |
| G17-S25-A02 | PENDING_INDEPENDENT_REVIEW | policy and procedural exceptions disclosed | EXCEPTIONS_DISCLOSED: formal closure pending |
| G17-S25-A03 | PENDING_INDEPENDENT_REVIEW | reviewer issues explicit signed PASS/NOT PASS before G17 acceptance | NOT_PASS recommendation; separate signed decision missing |

## S25 decision and closure requirements

- **G17-S25-A01: NOT PASS.** Source-locked evidence is materially improved, but F01-F03 leave behavior that is required yet not independently demonstrated. Reviewer separation is also unfulfilled.
- **G17-S25-A02: EXCEPTIONS DISCLOSED, closure pending.** F04 metadata packaging, F05 approved pooled P09 with its individual exceedance, and F06 reviewer separation are recorded without a silent waiver.
- **G17-S25-A03: NOT PASS recommendation recorded.** This is not a signed acceptance approval from a distinct reviewer.

**Result:** G17 remains NOT ACCEPTED. Preserve the original green run and all earlier failed and c7 evidence. Fix F01-F03 with real positive and negative witnesses, run a new exact-source complete campaign when tests or implementation change, and have a distinct reviewer consider F04-F06 and sign an explicit later decision. Protected `main`, M9 hold, M15 retention and all accepted G13/G15/G16/Turkish gates remain unchanged.


## Second-pass independent evidence check, November 14, 2026

All 25 scenario IDs and 75 atomic assertions were rechecked against the exact S25 requirements and the immutable raw hosted artifact. The 33 independent raw checks were rerun and returned 31 passes plus the two previously disclosed P09 and Playwright-metadata qualifications. Two additional gaps were identified and logged in the [open-findings register](M17_G17_S25_OPEN_FINDINGS_REGISTER_20261114.md).

**S25-F07, G17-S15-A01/A03:** The restart test's before and after application footprint is `0,0,0` (work items, effects, receipts). The source neither seeds nor verifies a real leased worker item, and does not link the 80 canceled read requests to trace identities. It cannot demonstrate the required active worker lease invariant or cancellation-specific trace lifecycle solely by those checks.

**S25-F08, G17-S22-A02/A03:** The storage test proves an owned full telemetry tmpfs, healthy business HTTP responses, and equal complete public-schema data-only dump hashes. It does not prove an explicit degraded telemetry signal nor any preexisting protected M8/M11/M15/M16 rows that could be destroyed. Positive synthetic retained history and a degradation witness are required before these assertions can receive independent closure.

**Updated review disposition:** Eight open findings (F01-F08), including six previously reported and two found in the second pass. G17 source and technical run unchanged; G17-S25-A01 NOT PASS, A02 exceptions/open coverage recorded, A03 separate signed decision pending. **G17 NOT ACCEPTED.** This addendum does not overrule or rewrite the earlier green run or the original review date.
