# G7 Tracking Contracts and Source Authentication: Verification Plan

**State:** Execution harness committed and exercised in development runs. The gate evidence run and acceptance are pending; no G7 scenario is accepted.
**Prepared:** 2026-11-03
**Milestone:** M7-01 through M7-04 and gate G7
**Base:** Accepted G0-G6 `main` at `97b67739cf1dad543bf25fa56326fb4702990f41`
**Contracts:** [D-002 proposal](M7_D002_TRACKING_REFERENCE_PROPOSAL.md), [M7 architecture](M7_ARCHITECTURE_DESIGN.md), [implementation plan](M7_IMPLEMENTATION_PLAN.md), [Roadmap](ROADMAP.md), [requirements](REQUIREMENTS.md).

## 1. Non-negotiable gate rules

1. D-002 must be explicitly accepted with an immutable upstream 2.2.0 reference and an exact supported adapter subset before executable G7 acceptance.
2. Every required G7-S01 to G7-S25 case must be executed against a single source SHA; authored test methods and source inspection alone are not passing evidence.
3. M7 must not return 200, 201 or 202 for tracking-event intake while it has no durable receipt. Positive auth and payload tests expect valid processing **followed by** HTTP 503 `intake_unavailable` and no persistence. A real success acknowledgment belongs to M8.
4. Unit, contract, real PostgreSQL fresh/repeat integration, current-to-M7 upgrade, source CLI, concurrency, proxy/transport, controlled outage and existing accepted regressions must be covered. Three-browser regressions are required if shared routing/auth changes affect browser behavior, without introducing a new M7 UI or early accessibility audit.
5. All mandatory cases must report PASS, FAIL, BLOCKED or NOT EXECUTED with actual commands and logs. Do not infer a G7 pass from G6 acceptance. Preserve G0-G6 accepted evidence without rerunning unrelated mutation/snapshot gates.
6. Evidence, Docker data, browser assets, caches and temporary outputs are E:-backed; use disposable database and Compose project names. Never touch the only retained database/volume or unrelated projects.
7. GitHub Actions is disabled. No dispatch, rerun, hosted workflow or automatic trigger.
8. D-006, the M8 inbox receipt/dead-letter decision, remains open. G7 does not satisfy FR-006 immutability or FR-008 deduplication by itself.
9. G7 technical completion is distinct from explicit acceptance and permission to merge. Dedicated accessibility testing stays M24-04.

## 2. G7-S01 to G7-S25 mandatory matrix

| Case | Required executed evidence |
| --- | --- |
| **G7-S01** | D-002 explicit acceptance: exact normative release/reference SHA, reviewed license, first supported event families/codes/classifiers, adapter mapping and limitations; reject substitution with unpinned sources |
| **G7-S02** | Pure source lifecycle policy: valid Active/Disabled transitions, token expiry and not-before boundaries, 90-day maximum expiry, 24-hour rotation overlap, at-most-two active keys and invalid transitions |
| **G7-S03** | Source credential entropy and storage: secure random 256-bit issuance, digest-only persistence, nonreversible storage, constant-time digest validation, unique IDs, no default credential and one-time controlled display |
| **G7-S04** | Fresh PostgreSQL M0-M7 schema: composite tenant FKs, positive version/time checks, unique active-reserved source code, indexes, no unauthorized cascade deletion, EF snapshot matches migration |
| **G7-S05** | Populated accepted M6-to-M7 upgrade on a disposable copy and optional rollback/reapply: M1 sessions, M2 memberships, M3 references, M4 catalogs, M5 shipments and M6 assignments preserved byte-for-byte where applicable |
| **G7-S06** | Restricted source CLI: explicit opt-in, valid create/inspect/rotate/revoke/disable, unknown source/org, duplicate source, disabled org, invalid command and no secret in args or logs |
| **G7-S07** | Bearer authentication matrix: absent, wrong scheme, malformed, duplicate header, oversized header, unknown credential, incorrect digest, revoked, expired, not-yet-valid and disabled source all safely denied |
| **G7-S08** | Rotation and revocation concurrency: two simultaneous rotations, revoke versus in-flight validation, stale credential after revocation, overlap cutoff, DB uniqueness and no third active credential |
| **G7-S09** | Two-organization entitlement/IDOR: source token only resolves own org; forged org/body/header, foreign organization/source IDs, inactive organization, fake source codes and cross-tenant enumerations denied without existence leak |
| **G7-S10** | Browser identity separation: human cookie/CSRF never substitutes for bearer source auth; bearer cannot call human-scoped endpoints; no source token in browser storage or frontend assets |
| **G7-S11** | Transport trust: HTTPS, real loopback local opt-in, non-loopback HTTP rejection, forged localhost Host, forged X-Forwarded-For/Proto, untrusted proxy, unknown Host and absent peer fail closed |
| **G7-S12** | Rate limits: unknown remote partition, authenticated source partition, threshold, queued requests zero, reverse-proxy address handling, 429/Retry-After and no tenant exhaustion via chosen client-supplied IDs |
| **G7-S13** | Media/profile contract: exact JSON MIME and charset policy, missing/unsupported profile, compressed and multipart requests, method mismatch, unsupported version and safe error statuses |
| **G7-S14** | JSON parser boundaries: 0/1/16384/16385-byte inputs, chunked length bypass, malformed UTF-8, depth 12/13, duplicate and unknown keys, arrays at root, comments and trailing commas |
| **G7-S15** | Event identity/reference validation: invalid/overlong sourceEventId, control/Unicode normalization, zero/malformed identifiers, missing conditional reference, cross-family reference fields and D-003 synthetic vs ISO equipment codes |
| **G7-S16** | UTC policy: Z/+00:00, non-UTC offsets, absent/ambiguous time, leap-day, DST-looking input, future cutoff and lower historical bound, injected TimeProvider rather than workstation clock |
| **G7-S17** | Pinned positive subset fixtures: each approved supported event family/code/classifier normalizes deterministically, fields match recorded DCSA 2.2.0 mapping and profile fixtures have pinned hashes |
| **G7-S18** | Unsupported variants: unknown family/code/classifier, IoT/reefer, document/shipment codes excluded from v1, upstream future fields and invalid combination rejected 422 without side effects |
| **G7-S19** | HTTP response envelope: code/message/correlationId and no-store across 400/401/403/413/415/422/429/503; no stack trace, SQL text, source existence details or unredacted raw input |
| **G7-S20** | No-durable-ack proof: valid authenticated request yields 503 intake_unavailable plus Retry-After, no inbox, event-history, projection, worker, outbox, status mutation or 2xx; retries remain explicitly unacknowledged |
| **G7-S21** | Credential store and cancellation failures: DB unavailable before source check, outage mid-auth, timeout, client cancellation and restart yield sanitized 503/499 as applicable and never succeed open |
| **G7-S22** | Accepted M0-M6 contract/unit/integration regressions: sessions, five-role/tenant rules, reference CRUD, maritime, shipment/version/status history, assignment temporal constraints and migration preservation |
| **G7-S23** | Compose and browser integration: source CLI in disposable Compose, API and proxy behavior, main browser authentication unchanged in Chromium/Firefox/WebKit where shared middleware is touched, controlled teardown |
| **G7-S24** | Security effectiveness: adversarial tests detect disabled tenant filtering, bypassed bearer verification, insecure transport, disabled size bound and skipped revocation; current NuGet/npm and exact-image HIGH/CRITICAL scans clean when relevant |
| **G7-S25** | Reproducible evidence package: exact source/image IDs, E:-backed storage proof, all stage exit statuses, complete TRX/browser output, migration SQL/schema checks, sanitized logs, cleanup proof, SHA-256 manifest and independent review |

A scenario marked passing only in source comments, naming, or a previous milestone report is not executed G7 evidence. A source credential that is valid but has no durable inbox must never obtain an event-acceptance status.

## 3. Fixture and adversarial coverage

Use deterministic fixtures with two distinct organizations, one suspended organization, multiple sources per organization, overlapping credentials, five existing human roles, same-looking tenant-local references, approved supported and unsupported tracking profiles, and adversarial input variants.

Run actual independently scheduled database transactions for rotation/revocation races, not only task methods executed serially. Assert database constraints directly using SQL, with foreign tenant/source IDs. Test every negative HTTP response through the real API pipeline, including validation of the transport peer and forwarded headers.

The positive contract fixture must demonstrate that an approved event can be parsed and normalized while the HTTP receipt remains 503. There is no tracking-events database record in M7. Do not create a fake inbox just to satisfy tests.

## 4. Execution stages and commands to implement

The implementation branch includes `scripts/verify-m7-local.sh`, a source-locked E:-backed technical runner covering build, .NET regression, five isolated security mutations, web, migrations, browser/proxy, credential checks, controlled outage, image scans and evidence teardown. G7 remains blocked until every G7-S01 to G7-S25 scenario is independently checked. The full required stage taxonomy remains:
`preflight dotnet contracts db migration auth parser http transport compose browsers recovery scans evidence teardown`. Some boundaries run inside combined stages, so `scripts/summarize-g7-evidence.mjs` derives each G7-S01 to G7-S25 outcome from the raw TRX files, stage records, browser reports, mutation report and scan reports, and writes `g7-scenarios.json` and `g7-scenarios.md`. A scenario is PASS only when every listed test, stage and report exists and passed on the same source revision. Manual operator evidence remains mandatory for G7-S06.

Each stage must write its command, environment and source identity before running. Define unambiguous success/failure exit codes. Do not copy G6 scripts without reviewing their current stage dependencies, resource names and evidence output. Do not start a primary Compose stack or change retained production-like volumes.

The runner writes evidence to `E:\HarborOps-Verification\m7\evidence` and a hashed archive and manifest to `E:\HarborOps-Verification\m7\archives`. Sanitize raw secrets and test credential values before producing a shareable evidence package; keep restricted originals private if necessary.

Required evidence files: stage manifest, source revision, environment versions, official DCSA reference identity/hash, supported mapping manifest, executed test counts, per-case G7 results, SQL/upgrade evidence, CLI and proxy logs sanitized, no-ack receipts, advisory and image scan JSON, after-state Docker resource inventory, E:/C: accounting, SHA-256 manifest, and acceptance report.

## 5. Gate closure and risk classification

- **Critical:** Unapproved D-002, false 2xx ingestion acknowledgment, cross-tenant credential access, real secret exposure, production transport bypass or lost/unbounded event input.
- **High:** Broken revocation, incomplete DB constraint verification, bypassable parser bounds, untested concurrency or migration data loss.
- **Medium:** Unsupported variant mapping ambiguity, missing independent provenance for reference fixtures, missing optional browser regression when shared middleware changes.
- **Administrative:** Unavailable externally hosted TLS domain does not block local source-contract G7 if local transport trust is demonstrably verified; hosted deployment is a separate release gate.

G7 is not eligible for closure while any mandatory scenario is unresolved. Following a fully executed verification report and independent review, G7 stays **acceptance pending** until explicitly approved. Only then may the implementation be merged through a PR to protected `main`. M8 separately requires accepted D-006 and G7 before durable receipt semantics can be introduced.
