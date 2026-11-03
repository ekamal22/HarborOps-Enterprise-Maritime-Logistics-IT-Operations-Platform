# Publication and Hosted Security Remediation Register

**Date:** 2026-11-02  
**Implementation source:** `fix/publication-security-readiness`, merged to `main` through PR #6  
**Baseline:** `main` at `4d26514caee5cc835f6eef230e0433141291374e`  
**Merged implementation:** `474b83837d45312e155f2c2d220af28d927fc1b3` (verified source with documentation updates)  
**Publication decision:** NOT CLEARED  
**Internet-hosting decision:** NOT CLEARED  
**Verification state:** Complete local E:-backed regression run passed at `b9adaa8ae0fc5f2df9deb5e4f090695d5048d672` (see below), after the isolated hosted run at `7145ec24a8ac289dfcf6f02644f54f35f4744c8a` passed every test stage and failed only in cleanup. Historical disclosure review, repository governance, licensing and publication approval remain outstanding.

This register tracks remediation after the [original security review](PUBLIC_REPOSITORY_SECURITY_AUDIT_2026-11-02.md). Do not alter the prior review's observations or reinterpret accepted M0 through M6 test evidence.

## Latest isolated verification evidence

[Run 41534507638](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41534507638) tested exact checkout `7145ec24a8ac289dfcf6f02644f54f35f4744c8a`, whose parent was security source `d9bc2aba875039bcd84b1212e023cb8cd9f4e319`. All substantive stages completed successfully: 149 unit, 21 contract, 187 PostgreSQL integration, 18 frontend and 18 browser tests, plus Compose, Nginx, local HTTP probes, NuGet/npm advisories and three exact-revision HIGH/CRITICAL image scans. The job conclusion is **failure**, because Trivy's root-owned cache files could not be removed by the unprivileged final cleanup step. The disposable Compose stack, volumes and PostgreSQL container were removed. No downloadable artifacts were uploaded.

The isolated manual-only workflow was corrected at `8cf9fffef3cc41823d367e7a54d3e40f5678e242` with scoped cleanup and a source guard that works after additional verification commits. This correction has not been executed; no further Actions run was launched. The workflow remains manual-only. [Publication Safety Status](PUBLICATION_SAFETY_STATUS.md) contains the attempt-by-attempt results and outstanding blockers.

The hosted run does not replace E:-backed local verification, complete historical Git-object and retained-artifact disclosure review, governance enforcement, approved source snapshot review, licensing decision, or real-host TLS validation. Accepted G6 evidence remains intact. Neither publication nor hosting is cleared.

## Local E:-backed verification, November 3, 2026

A complete local regression run (executed as commit `efe9043` before a rebase onto three documentation-only upstream commits, which produced the identical source tree at `b9adaa8ae0fc5f2df9deb5e4f090695d5048d672`) executed on the disposable `ho-verify-g6` Compose project and a throwaway PostgreSQL container, with every cache, temporary file, browser binary and report on E:. The runner is `scripts/verify-publication-local.sh`; it reuses the accepted M6 stage implementations and adds the publication stages in `scripts/verify-publication-stages-local.sh`. It does not repeat the accepted G6 mutation or historical-snapshot gates: no migration or schema file changed on this branch, so those accepted results are not invalidated and remain the reference for that evidence.

| Area | Result at source `b9adaa8ae0fc5f2df9deb5e4f090695d5048d672` |
| --- | --- |
| Release build with warnings as errors | Passed |
| Unit tests | 149 of 149 passed |
| API contract tests, including 18 new end-to-end transport cases | 39 of 39 passed |
| PostgreSQL integration tests, fresh database and repeat on the migrated database | 187 of 187 passed, both passes |
| Frontend tests and production build | 18 of 18 passed, build succeeded |
| NuGet and npm advisories (web and browser projects) | No vulnerable packages |
| Compose build, migration, startup, readiness, degraded-mode and session recovery | Passed |
| Transport probes (`nginx -t`, local header and Host checks, untrusted Docker peer with a localhost Host header, spoofed forwarded address through and around the proxy, unexpected Host) | 9 of 9 passed |
| Chromium, Firefox and WebKit, all 15 specs per engine | 57 of 57 per engine passed; no failed, flaky or skipped tests |
| Assignment, shipment, reference and maritime outage checks and scoped recovery | Passed |
| Trivy HIGH and CRITICAL scans of the postgres, api and web images, labelled with the source revision | No findings |

Commits after `b9adaa8` change documentation and `scripts/audit-publication-supply-chain.py` only; `src`, `tests`, `web`, `docker` and `compose.yaml` are byte-identical to the verified source. The source-locked supply-chain audit was then run against a freshly built stack at `6726983a18a4b7e9b539c86f636ac20d8682e630`: NuGet, both npm audits and all three image scans passed.

### Original integration failure

The failure of hosted run 41526406931 (161 of 187 integration tests, HTTP 400 from `GET /api/v1/auth/csrf`) was reproduced on the source before the test-host fix, where the first test in `IdentityLifecycleTests` failed with a 400 from that endpoint. ASP.NET Core `TestServer` requests carry no remote socket address, so the hardened transport check correctly rejected them. `LocalPeerTestServer` supplies `127.0.0.1` only when the remote address is absent, runs before forwarded-header handling and transport validation, and does not alter production code. All 187 tests pass with it.

### Defects found and corrected during verification

| Defect | Correction |
| --- | --- |
| `scripts/check-planning-docs.mjs` required the workstation folder name that the disclosure reduction intentionally removed | The check now requires the generic E:-backed wording and rejects the folder name |
| Firefox `maritime-gate` container pagination spec failed once in a full run | The workspace is keyed by organization and role and can remount after an organization switch, resetting the tab. The spec now waits for the organization request and retries the tab click until the Containers view is visible. No assertion was weakened and no retries were enabled globally. 30 of 30 isolated runs passed across the three engines afterwards |
| Publication Python scripts launched tools by bare name, which fails on Windows for `.cmd` wrappers and `npm` | Executables are resolved through `PATH` first |
| `check-publication-governance.py` built an invalid repository URL | Fixed |
| `audit-publication-supply-chain.py` treated a vulnerability-free NuGet audit as having no inventory, and passed a bare image ID that the Docker daemon rejects | A listed project with no `frameworks` key is a clean result, and images are scanned as `sha256:` references |
| Three integration tests committed credential-like password constants flagged by the secret scanner | Those three values are generated per run |

### Transport regression coverage

`tests/HarborOps.ContractTests/TransportHardeningHttpTests.cs` drives the real pipeline with an explicit socket peer: loopback over local HTTP, a missing remote address (rejected as `invalid_transport`), a remote attacker with a forged `localhost` or `127.0.0.1` Host, an untrusted Docker peer, the configured gateway and proxy peers, forwarded address and scheme from a trusted proxy and from an untrusted peer (ignored), unexpected Host values (rejected by host filtering), plain HTTP rejected and HTTPS accepted when the local HTTP exception is disabled, hosted mode refusing to start without a key protection certificate, and Secure, HttpOnly, SameSite=Strict antiforgery cookies in hosted mode.

### Not executed

Hosted HTTPS checks (`scripts/verify-publication-hosted.mjs`) require an actual approved domain and certificates and were **not executed**. Accessibility verification remains deferred to the final designated release stage. No GitHub Actions run was started by this verification, and the corrected `verify-publication-security.yml` has not been dispatched.

### Publication scripts

`audit-publication-history.py` inventoried 26 refs, 4,797 reachable objects and 38 workflow runs, and does **not** clear: three remote branch tips were not present in the local object store at audit time, five unreachable objects need separate review, and the scanner reported three credential-like test constants (since removed from the current tree, still present in old commits), nine non-credential matches in historical logs and failure snapshots (image digests and synthetic page-snapshot text), and serialized synthetic session cookie values inside nested browser trace archives of three historical runs. Raw outputs are retained in a private, access-restricted location and are not part of any shareable package. `prepare-public-source-snapshot.py` produced a 189-file history-free candidate that passed its secret scan; an independent extraction contained no personal, workstation, assistant-attribution or non-synthetic email content, built with zero warnings and passed all 149 unit tests. It remains a review candidate, not an approved release. `check-publication-governance.py` confirms the repository is private and Actions is disabled, and that `main` has no branch protection and no rulesets; that setting requires administrative action.

## Findings and closure requirements

| Finding | Current remediation | Required evidence before closure |
| --- | --- | --- |
| PUB-01 historical disclosure | Added `scripts/audit-publication-history.py` for complete ref inventory, Git-object inspection, all available historical workflow logs and artifacts, and redacted secret scanning | Run locally with E:-backed outputs; inspect findings and unreachable Git objects; manually review personal, contractual, business, test and historical data in all refs; verify any inaccessible artifacts separately; revoke leaked secrets if found; record actual result and clearance decision |
| PUB-02 automatic hosted workflows | Removed all `pull_request` triggers from `verify-m0.yml`, `verify-m1.yml` and `verify-m2.yml`; retained manual dispatch only | Merge approved workflow changes; verify repository-level Actions settings prohibit unapproved execution; confirm that no hosted runs were started; `[skip ci]` alone is not sufficient |
| HOST-01 loopback transport trust | Replace Host-only detection with connection peer check; restrict local hosts and explicit trusted Compose peers; trust forwarded client scheme/IP only from configured reverse proxies; restrict API's default AllowedHosts | Execute contract tests and complete regression on local E: resources, including direct API and Nginx routes, forged Host, Origin and forwarded headers, remote simulated clients, cookies and proxy traversal; hosted environment must disable local HTTP |
| HOST-02 browser and TLS configuration | Add restrictive local CSP, frame, MIME, referrer and permissions headers; create TLS-only hosted reverse proxy template with HSTS after verified TLS | Run `node scripts/verify-publication-http.mjs` against the local stack; validate Nginx syntax and browser tests; for hosted run `node scripts/verify-publication-hosted.mjs https://verified.example` against the actual approved target domain, including certificates, trusted proxy IP, redirect and Secure cookies |
| PUB-03 current supply chain | Add `scripts/audit-publication-supply-chain.py` for source-locked NuGet, npm and three-image vulnerability reviews | Execute locally with current advisory feeds and image IDs from exactly the reviewed SHA; triage findings; assess third-party action pinning before any Actions re-enablement |
| PUB-04 machine-specific disclosures | Reduce workstation directory and disk information in current shared operational documents | Run full documentation and Git-history disclosure review; check milestone evidence references and historic committed versions; remove or withhold unneeded material from a public source export |
| PUB-05 license clarity | Add restrictive `LICENSE` stating that no reuse rights are granted | Confirm that the selected source-viewing terms match the intended publication strategy; do not misrepresent this as an open-source license |

## Local source and supply-chain verification

Execute only in a clean E:-backed checkout with controllable caches, temporary files, Docker/WSL state, reports and archives also on E:. The Python audit scripts never dispatch GitHub Actions and do not delete or rewrite Git objects or historical evidence.

1. Run existing .NET, PostgreSQL, web and browser regressions using an isolated E:-backed verification environment. The new contract cases are in `tests/HarborOps.ContractTests/AuthenticationSecurityTests.cs`.
2. Start a disposable local Compose stack only after checking its dedicated network subnet `172.30.254.0/24` is available. Host-facing ports must remain bound to `127.0.0.1`.
3. Run `node scripts/verify-publication-http.mjs` against the local web endpoint, confirm the CSP does not break functional browser tests, and inspect HTTP-only/Secure cookie behavior.
4. Run `python scripts/audit-publication-history.py E:/security-evidence/publication/history-review` after authenticating the read-only GitHub CLI and installing required local scanners on E:.
5. Build revision-labeled disposable images, then run `python scripts/audit-publication-supply-chain.py E:/security-evidence/publication/dependencies`. Inspect NuGet/npm findings and Trivy JSON reports.
6. Record exact source SHA, scanner versions, result totals, all exceptional cases and verified output hashes. Evidence from a previous SHA cannot clear the current source.

Both Python scripts intentionally fail closure when required evidence is missing or a scanner reports a finding. A no-finding automated result still requires manual disclosure review before public visibility.

## Hosted TLS template

`web/nginx.hosted.conf.template` is a reference profile only. It is **not** activated by `compose.yaml` and does not grant hosting approval.

Before activating a hosted environment:

- Use a verified public hostname and valid mounted TLS certificate chain and private key. Render **only** `HARBOROPS_PUBLIC_HOST` in the template, preserving Nginx runtime variables such as `$request_uri` and `$remote_addr`.
- Set API `AllowedHosts` to the exact public hostname, `Identity:AllowInsecureLoopbackHttp=false`, and provide a mounted certificate and password for Data Protection key encryption at rest.
- Set `ReverseProxy:KnownProxies` to the actual private reverse-proxy IP. Do not trust arbitrary forwarded headers or entire untrusted networks.
- Do not expose PostgreSQL or the internal API directly. Configure TLS termination, network segmentation and all other hosted controls separately.
- Run controlled live-host negative tests before permitting users or external traffic. The local HTTP profile must not be reused for public hosting.

## Publication gate

The existing repository must remain **private** until full-history review, workflow settings, dependency review, disclosure review and licensing decisions have supporting evidence and an explicit publication approval. Do not rewrite Git history, delete divergent branches or purge retained artifacts to make a scan appear clean. If a clean source-only portfolio snapshot is chosen instead, create and review it as a separate repository without importing the private development history.

No status in this register changes M0 through M6 acceptance or asserts G7, production security certification, or release approval.

## Independent repository-wide publication findings

The current private `main` branch received a narrow hotfix at `498426bd3fe765eb3fdb90a933ed533545600dda`, removing pull-request triggers from the two remaining automatically triggered default-branch verification workflows. This change has no bearing on historical workflow logs, settings, old branch definitions or accepted M0-M6 evidence.

| Finding | Implementation state | Remaining hard stop |
| --- | --- | --- |
| PS-01 | E:-scoped historical Git and hosted-run audit runner prepared | Complete coverage, review every finding and inaccessible historical artifact; keep private until cleared |
| PS-02 | Automatic triggers removed from current `main` and development branch workflow definitions | Disable Actions at repository settings; review workflows on every older target branch before exposing it |
| PS-03 | `scripts/check-publication-governance.py` added for evidence-based settings verification | Enforce branch rules and review restrictions in GitHub Settings; integration cannot change administrative settings |
| PS-04 | `scripts/prepare-public-source-snapshot.py` creates an explicit allowlisted source-only ZIP without prior Git history or development documents | Run secret scanner and manual disclosure review, then approve a separate clean public repository; do not simply change visibility of this repository |
| PS-05 | Current shared operational documentation partially generalized; public source export excludes historical verification and operational documents | Inspect approved snapshot content, licenses and personal/third-party references before publication |
| PS-06 | Dependency and image audit runner created | Execute full current local scans against exact source and container revisions on E:; triage actual findings |
| PS-07 | Restrictive `LICENSE` added on this branch | Confirm that inspection-only licensing is intended before any source distribution |
| HS-01 | Remote-connection and trusted-proxy transport controls implemented on the security branch | Local backend, direct HTTP, proxy, forged-header and integration regressions remain pending |
| HS-02 | Security headers and hosted TLS-only Nginx template added | Hosted configuration requires real TLS certificates, protected keys, source-locked tests and explicit hosting approval |

### Repository governance enforcement

The existing repository must remain private until required checks are complete. To enforce default-branch governance, configure GitHub **Settings > Rules > Rulesets** (or **Branches > Branch protection rule**) for `main`: require a pull request, require at least one approving review, dismiss stale approvals after substantive changes, block force pushes and branch deletion, and restrict bypass privileges. Do not configure status checks that implicitly start GitHub Actions. Under **Settings > Actions > General**, disable hosted workflow execution while prohibited. Read-only confirmation is captured by:

`python scripts/check-publication-governance.py E:/security-evidence/publication/governance.json`

The connected GitHub integration cannot change those administrative settings. File-based policies alone cannot enforce them.

### Clean public source without private history

The publication-safe alternative is a **new repository with a fresh initial commit**, created only after review of the archive generated by:

`python scripts/prepare-public-source-snapshot.py E:/security-evidence/publication/snapshot`

The exporter excludes `.git`, all branch and commit metadata, `.github`, local development scripts, and historical `docs/` evidence. It uses explicit source-path allowlists and a redacted secret scan, fails on recognized private paths and non-synthetic email domains, and never publishes or changes repository visibility. The archive remains a review candidate until its contents, dependency licenses and relevant security controls have been independently approved. Preserve this private repository and the original gate evidence without rewriting or deleting history.

### Remaining release requirements

- Execute the existing local contract, unit, PostgreSQL integration and cross-browser regression suites, and new Host/proxy response checks on E: against the exact candidate SHA.
- Review all available historical workflow jobs and retained artifacts as well as Git branches, tags, pull-request refs and deleted Git objects, even when current-tree secret scans are clean.
- Execute current-source NuGet/npm, image and license reviews locally, with pinned evidence hashes, and record unresolved findings.
- Confirm active repository settings from read-only administrative endpoints, not merely tracked workflow YAML.
- Do not expose API authentication or change repository visibility until the corresponding independent approval has been recorded.

### Historical Actions disclosure inspection, November 2, 2026

A read-only review inventoried 35 hosted workflow runs. Two cancelled runs had no available job logs; the other 33 job logs were screened for recognizable private keys, provider tokens, private email domains and long credential assignments, with no matches for those patterns.

All 17 retained downloadable workflow artifact archives were retrieved for inspection. They contained 34 nested browser trace ZIPs, source and reporter assets, synthetic test output and binary screenshots. Pattern-based scanning and targeted inspection found **52 serialized authentication-cookie-value matches** in three artifact archives: `13026999079`, `13027355828`, and `13027499753`. These appear to be disposable test-session values. No live-production use is established, but the contents are security-sensitive and must not be exposed in a public repository or portfolio package. Exact cookie values were not copied into this register. The binary screenshots were not individually reviewed, and a full historical Git-object scan remains outstanding.

**Publication gate:** Preserve the original artifacts in this private repository for accepted gate traceability. Do not publish, link, export or redistribute the original trace archives as part of a public release. An explicitly approved separate history-free source export excludes them. Any proposal to remove retained GitHub artifacts or rewrite history requires a separate evidence-preservation decision and confirmation that no active credential depends on the historical tokens.
