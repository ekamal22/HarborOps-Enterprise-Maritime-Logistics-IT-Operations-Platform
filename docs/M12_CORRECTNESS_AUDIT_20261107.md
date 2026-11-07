# M12 End-to-End Correctness Audit and Source Remediation

**Date:** 2026-11-07  
**Scope:** M12-01 through M12-04 candidate on `feat/m12-shipment-search-export`, PR #32, against protected `main` `6ead1d076946dd96b53401265a31efc35c4a666c`.  
**Post-remediation code checkpoint before documentation changes:** `43ad67a64fe191a0ac04e47194201d6f5021e70f`.  
**Disposition:** Source-level remediation committed. Executable verification, security proof, integration with M11 and G12 acceptance pending. Not merged and not deployed.

## Audit scope and method

Inspected the M12 domain policy, typed permissions, scoped EF query and tenant locks, cursor encode/decode, HTTP transport/error and throttling, CSV encoding, restricted M16 audit function and role grants, additive EF migration, API dependency injection, local Compose and environment template, React query/download flow, new unit/contract/PostgreSQL/browser tests, G5/M6/M10/M16 dependencies and the concurrent M11 feature branch. Inspected the associated living documentation and canonical execution prerequisites.

A pure source-consistency pass checked 15 cross-file contracts. All 15 were source-consistent at the examined revision, but this is **not** a .NET build or passing runtime test. Repository changes were made through the connected source interface. The executing environment has Node but no .NET SDK, PostgreSQL, Docker or local checkout/network access to the repository, so live PostgreSQL, HTTP and three-browser verification could not be performed. No external CI or GitHub Actions were enabled or run.

## Findings and remediations

| ID | Severity | Observed issue | Remediation status |
| --- | --- | --- | --- |
| M12-A01 | High | Original export audit function accepted any API SQL caller with guessed active actor/session identifiers and lacked a proof of HTTP-runtime authority | **Source fixed:** separate 32-byte signing key; HMAC-SHA256 binds organization, actor, session, row count, byte count, filter digest, correlation and 45-second expiry; migration-owned verifier; restricted grants; unique event/correlation prevents replay. Must be proven against real restricted PostgreSQL login. |
| M12-A02 | High | SQL input checks used nullable comparisons that could bypass invalid-metadata conditions | **Source fixed:** explicit NULL/empty UUID, row/byte, SHA digest and correlation checks, strict expiry; negative SQL test source added. |
| M12-A03 | Medium | Audit event reported `export_completed` before the HTTP file reached the client | **Source fixed:** `shipment.export_prepared` records authorized, fully materialized data before transfer. Tests and event-specific M16 metadata allowlist updated. |
| M12-A04 | Medium | Cookie-authenticated GET export had no independent browser-origin check | **Source fixed:** deny cross-origin Fetch Metadata and Origin where supplied; negative HTTP test added. SameSite cookie defenses remain mandatory. |
| M12-A05 | Medium | Advanced search introduced a duplicate accessible label `Shipment workflow`, jeopardizing existing M5 browser selectors | **Source fixed:** unique `Filter by workflow status` label; M5 selector and M12 browser assertions retained. |
| M12-A06 | Medium | Protected pagination cursors were tenant/filter bound but replayable by another authorized user or session within the same tenant | **Source fixed:** cursor also binds actor and login session, and each page rechecks live shipment-read scope. Cross-user/session negative tests added. |
| M12-A07 | Low | The search parser checked control characters only after trim, allowing prefix/suffix controls to vanish before validation | **Source fixed:** reject control characters in raw search input before normalization; unit cases added. |
| M12-A08 | High | Secure audit-key provisioning was not represented in the runtime configuration | **Source fixed:** independent `HARBOROPS_M12_EXPORT_PROOF_KEY` is passed only to migrator/API as `M12ExportProof__Key`. Missing key returns 503 with no CSV; malformed provisioned key fails migration provisioning. No secret is committed. |
| M12-A09 | Integration risk | M11 and M12 both edit API registration, role provisioning, migration discovery and shipment workspace | **Pending integrated review:** migration IDs `20261107180000_ShipmentExceptions` and `20261107230000_M12ShipmentExportAudit` are distinct and sort correctly. Combined migration set, snapshot and tests must be reconciled on one integration SHA. |
| M12-A10 | Verification blocker | Runtime tests, restricted-role evidence, full G12 source lock and independent gate decision are absent | **NOT EXECUTED:** no actual G12 scenario is marked PASS or accepted. |

## Resulting proof and export boundary

The export route requires authenticated, actively selected and live Analyst/Manager membership; Administrator, Support and Auditor do not gain export access from generic organization or shipment read. A repeatable-read transaction locks organization, session, user and membership, limits output to 5,000 rows and 10 MiB, quotes and formula-defangs fixed CSV fields, obtains a signed short-lived proof, and calls the restricted database audit function before committing. If the authorization proof, event insertion or commit fails, no file result is returned.

The migration creates `m12_export_proof_keys`, `m12_record_shipment_export` and `uq_m12_export_prepared_correlation`. It does not rewrite accepted migrations. Restricted API/worker logins must not select the signing key; only the migration identity provisions or rotates it. The web and worker do not receive the secret. The audit records *preparation*, not confirmed client download. The proof protects against database-only SQL forgery within the accepted trust model, not compromise of the entire HTTP runtime or the privileged migrator.

The current Compose integration leaves the M12 environment variable optional to avoid changing unrelated accepted G10 verification environments. An absent key fails CSV export closed with a sanitized 503. Real M12 operation requires an independently generated 32-byte base64 key in an untracked E:-backed secret configuration, supplied identically to migrator and HTTP API. It must not be reused as the M10 correction key.

## Existing source and test coverage

- Unit: typed filter normalization, workflow/record status, strict UTC windows, raw control-character rejection, CSV formula and Unicode cases, permission matrix, HMAC proof format and invalid keys.
- Contract: no-session 401, persistent protected cursor integrity, cross-tenant/filter/actor/session/restart-key rejection, export concurrency gate.
- PostgreSQL HTTP: tenant/foreign route, port/date/voyage filtering, stable cursor pagination, wrong-principal denial, malformed queries, restricted API-role export, worker denial, proof-key isolation, null/forged proof rejection and repeated signed-call denial.
- Browser: advanced search preserves unsaved draft, authorized CSV download and Auditor UI denial, using disposable three-engine fixtures.
- Existing: M5 shipment and draft regressions, M6 assignments, M16 event metadata allowlist and migration ordering.

**All of the above are authored test sources, not executed test evidence.** The restricted-role fixture changes a PostgreSQL cluster-wide role password. Run it only in an exclusive disposable G12 instance, never concurrently with another mutable integration campaign. Test results and unexpected failures must be retained with the final SHA.

## Mandatory next verification and acceptance

1. Freeze the final branch SHA after documentation reconciliation. On an isolated E:-backed worktree, run .NET release compilation and unit/contract suites, web build, and planning/status docs checks without running GitHub Actions.
2. Use dedicated PostgreSQL 16 with a fresh migrated database and `harborops_api`/`harborops_worker` credentials. Test key missing/invalid/mismatch, signed proof mutation, role/table grants, duplicate correlation and audit outage, including failed attempts.
3. Execute M5/M6/M10/M16 change-impact regression, live authorization and revocation races, 5000/5001-row and 10-MiB limits, cancellation and PostgreSQL query plans. Validate no CSV on audit failure and no secret leakage.
4. Exercise Chromium, Firefox and WebKit against isolated M12 fixtures and a real restricted runtime role. The browser export test must not be treated as PASS if the runtime key or fixture is unavailable.
5. Complete G12 Q2 historical snapshot compatibility on a safe retained-data copy when available, or record evidence supporting NOT APPLICABLE. No original retained volume may be changed.
6. Reconcile M11 PR #31 and M12 PR #32 on a separately approved integration source: unique migrations, full model/snapshot discovery, grants, API, web and tests, without changing either accepted milestone or the M9 transport hold.
7. Run effective security mutations and final image scans, preserve redacted raw evidence, have a separate independent G12-S25 reviewer verify it, then obtain explicit G12 gate acceptance and separately authorized merge.

**Remaining limitations:** No local compilation, runtime SQL, three-browser campaign, mutation suite, security scan or performance measurement was possible in this audit environment. Formal search p95 benchmarking is the M21-02 work package; G12 query-plan and resource checks are still required. Full production runtime compromise, PostgreSQL superuser and privileged migrator are outside the bounded database-only integrity claim.

No merge, deployment, Actions execution, persistent C: storage change, data deletion, accepted-M9 transport-hold removal or G12 gate approval occurred.
