# G17-S25 Remediation Candidate and Separate Review Packet

**Remediation verification checkpoint, November 14, 2026:** [#43589093044](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43589093044) FAILED the new positive S15 lease-restart integration test after 319 unit, 60 contract and 54 other integration tests passed without skips. The new test launched opted-in telemetry without the required zero span-event and link limits; the existing privacy guard correctly rejected the host. Source corrections now set and restore both limits within the isolated test and use an uppercase-only synthetic shipment reference for the S22 database fixture. These changes need a fresh full campaign. Historical failure artifact `g17-raw-evidence-43589093044-1` is retained. **G17 NOT ACCEPTED; separate S25 sign-off remains pending.**

**Date:** November 14, 2026  
**Technical baseline:** GitHub Actions [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851), executable source `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`, technical PASS, independent S25 NOT PASS.  
**Candidate:** New source-locked G17 feature branch descendant; new full campaign still pending at the time of this record.  
**Gate:** G17 NOT ACCEPTED. Acceptance pending.

## Corrective source and required evidence

| Finding | Implemented source | Fail-closed requirement |
| --- | --- | --- |
| F01 | Three-engine browser test records actual no-data states for API request rate, 5xx and p95, with per-engine raw JSON | No report consisting only of panel titles or saved noValue fields passes |
| F02 | Real page-originated HTTP 503 injection during the actual owned PostgreSQL outage; retains server correlation, source trace ID, sanitized correlated log, and rendered database alert | No unrelated trace, mismatched correlation, unrendered alert, or browser API-client-only claim passes |
| F03 | Participating worker logs retained and scanned after real worker receipt processing | Private synthetic canary or missing positive worker log fails |
| F04 | Artifact upload explicitly includes hidden metadata, but the manifest scans the complete owned evidence directory first | Original Playwright last-run metadata must be delivered and hash-verified |
| F05 | A second script reconstructs all P09 per-pair, pooled p95 and memory numbers from retained raw samples and discloses individual exceedances | Numeric budgets remain 10 percent p95 and 15 percent mean peak memory; no silent waiver or rewritten samples |
| F07 | M17 integration test creates and claims a real leased receipt, restarts the API host, and checks exact lease token, epoch, state, attempt count and absence of duplicate effects | The source-locked integration TRX must show this specific method passed |
| F08 | Owned storage-fault test seeds nonempty M8/M11/M15/M16 synthetic history and records actual ENOSPC plus whole-public-schema data-only before/after SHA-256 | Missing protected fixture row, no real write rejection, or different history hash fails |

## Independent review requirement (F06)

The implementation work cannot certify itself as independent. The following review fields intentionally remain incomplete until a distinct authorized reviewer inspects the new raw source-locked evidence.

| Independent assertion | Requirement | Disposition |
| --- | --- | --- |
| G17-S25-A01 | Review source, exact raw artifacts, all 75 clauses, security and browser behaviors separately from the remediation | PENDING |
| G17-S25-A02 | Record explicit dispositions for all eight findings and the P09/packaging procedural qualifications | PENDING |
| G17-S25-A03 | Make a signed explicit PASS or NOT PASS recommendation, before any acceptance | PENDING |

**Reviewer:** Acceptance pending  
**Source SHA reviewed:** Acceptance pending  
**Evidence run ID and artifact hash:** Acceptance pending  
**Signed recommendation and date:** Acceptance pending

The separate review may record NOT PASS if any required witness is absent, skipped, uninspectable, or only inferred from another test. A passing Actions campaign is necessary but not sufficient.

## Boundaries

Keep original local c7, the green #43541279851 archive, and every failed hosted attempt intact. Do not modify protected `main` or accepted G13/G15/G16/Turkish behavior. The M9 transport hold and M15 retained-data and authorization protections stay intact. Source changes are limited to the M17 G17 technical witness and source-locked evidence path. No production activation is authorized.


## First hosted remediation attempt and corrections

- [#43589093044](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43589093044) was pinned to `773c65e2239d2fc238bc29b995f5ed6c8952733c`. Exact-source preflight PASS, .NET build PASS, 319/319 unit PASS, 60/60 contract PASS, 54/55 integration PASS. No required skips. The one failing method was `M17RestartLeaseWitnessTests.ApiRestartPreservesActiveWorkerLeaseAndNoDuplicateEffects`, rejected before fixture creation by `M17TracePrivacyGuard.RequireSafeExporterLimits` because event/link export limits were not zero. This is a correctly enforced guard, not a reason to weaken production safety policy.
- The test now requires `OTEL_SPAN_EVENT_COUNT_LIMIT=0` and `OTEL_SPAN_LINK_COUNT_LIMIT=0` for both API host lifetimes, restores their prior values in `finally`, and joins the existing worker-isolation test collection.
- A source audit also identified that a random lowercase hexadecimal suffix violated the shipment reference-code CHECK expression in the later S22 fixture. The synthetic shipment reference is now uppercase, while the separate tenant slug stays lowercase.
- The original run and artifact remain unchanged. A new exact-source campaign must complete before any finding is marked closed.
