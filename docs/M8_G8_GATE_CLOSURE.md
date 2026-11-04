# M8/G8 Gate Closure

**Decision date:** 2026-11-04  
**Decision:** D-006 Option A accepted; M8-01 to M8-04 and G8 accepted following independent raw evidence review.  
**Acceptance references:** [D-006 Option A](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/14#issuecomment-6633731945) and [G8/M8 acceptance](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/14#issuecomment-6633777914).  
**Reviewed source:** `537af9c2af99751aa2fbedf1a2115b7715e7b9e6`  
**Run ID:** `20261104T073952Z-0d093512c50ec17d`  
**Independent review:** [G8 sealed-evidence review](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md)  
**Executed record:** [M8 executed G8 verification](M8_EXECUTED_VERIFICATION.md)

## Original technical evidence

The successful local E:-backed G8 attempt 04 returned wrapper exit 2, correctly indicating a completed technical run with separate decisions pending. All 24 technical stage exits were 0. Original results are 203 unit, 39 contract, 242 fresh and 242 repeated integration tests, 58 per engine in Chromium, Firefox and WebKit, 11 of 11 intended security mutants killed with a 12/12 control, and three HIGH/CRITICAL-clear image scans.

The original 25-case raw register contains 23 technical PASS results (G8-S02 through G8-S24) and two BLOCKED results (G8-S01, G8-S25). Those original results are immutable and are not modified or reclassified after execution.

## Separate decision and review supplements

- **G8-S01:** Closed by the independently recorded D-006 Option A decision on 2026-11-04, with the contract consequences detailed below.
- **G8-S25:** Closed through independent examination of the sealed uploaded evidence: SHA-256 validation of the complete archive and all 174 manifest files, separate parsing of all TRX and Playwright reports, targeted mutation results, COMMIT fault-window test executions, restart digests and image scanner results. See [independent review](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md).
- **G8 acceptance:** Explicitly authorized conditional on satisfactory evidence, with that condition satisfied by the review and recorded separately from D-006. See the [G8 acceptance reference](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/14#issuecomment-6633777914).

A script-created review supplement on the original E: workstation may be added later for portability, but it must not rewrite original evidence, pretend to be a new technical run or be mistaken for an additional acceptance decision.

## Accepted D-006 Option A contract

PostgreSQL transactionally records a durable receipt, bounded immutable accepted event and separate Pending work row, then acknowledges only after COMMIT. Per-source/profile external event keys remain permanently unique and canonical replay returns the original receipt. Collisions produce bounded metadata and HTTP 409. The original source credential is reauthorized under consistent lock order inside the write transaction. The demonstrator has a configurable default admission cap of 10,000 Pending work rows per organization. It does not delete immutable accepted history.

**Implementation qualifications accepted:** Scoped uniqueness uses a PostgreSQL unique index, equivalent for uniqueness enforcement to the proposal's named unique constraint. M8 only permits Pending work rows and rejects state updates/deletes. M9 must add a separately reviewed migration for lease, retry, Processed and DeadLetter transitions without changing immutable receipt or event history; capacity stays occupied until such processing exists. Throughput, production-sized data, backup retention, hosted TLS, worker processing and M10 projections are not claimed by this gate.

## Retained limits and integration boundaries

The source-locked evidence includes a prior failed Firefox teardown attempt with unproven root cause, followed by a clean final three-engine run without silent retries. Trivy findings apply to its recorded database snapshot only. M8 acceptance does not imply public hosting, full security certification, incident modules, M9 worker execution or G24 release.

Earlier unsuccessful G8 attempts 01 to 03, the successful sealed attempt 04, original G8-S01/S25 BLOCKED flags, all accepted G0-G7 evidence and the original D-006 proposal history remain preserved. GitHub Actions were not used. No change to a retained production or historical database is authorized.

**Integration permission:** The accepted M8 feature branch may be merged to protected `main` with documentation and review provenance preserved. Subsequent dependent M9 work may use the accepted G8 interface contract; M9 remains a separately gated milestone.
