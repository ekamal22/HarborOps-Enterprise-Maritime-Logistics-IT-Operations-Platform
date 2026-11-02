# Publication and Hosted Security Remediation Register

**Date:** 2026-11-02  
**Work branch:** `fix/publication-security-readiness`  
**Baseline:** `main` at `4d26514caee5cc835f6eef230e0433141291374e`  
**Publication decision:** NOT CLEARED  
**Internet-hosting decision:** NOT CLEARED  
**Verification state:** Source changes prepared; current-source local verification and historical disclosure review not executed.

This register tracks remediation after the [original security review](PUBLIC_REPOSITORY_SECURITY_AUDIT_2026-11-02.md). Do not alter the prior review's observations or reinterpret accepted M0 through M6 test evidence.

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
