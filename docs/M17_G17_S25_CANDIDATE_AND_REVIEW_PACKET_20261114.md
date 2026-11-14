# G17-S25 Remediation Candidate and Separate Review Packet

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
