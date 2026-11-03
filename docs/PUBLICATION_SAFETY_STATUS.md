# Publication Safety Status

**Updated:** 2026-11-03  
**Repository visibility:** Private  
**Publication clearance:** NOT CLEARED  
**Public application hosting:** NOT CLEARED

This is the current decision register. Earlier security reviews remain historical records of the exact revisions they examined. No entry here changes accepted M0-G0 through M6-G6 evidence.

| Finding | Current state | Remaining condition |
| --- | --- | --- |
| PS-01 Historical disclosures | Current-tree scan found no high-confidence provider credentials. All 33 available historical job logs from 35 Actions runs were screened for selected secret patterns without matches. All 17 downloadable retained artifact archives were inspected. Three archives contain serialized temporary browser authentication cookie values. | Full Git-object and divergence review is outstanding. Keep raw archives private and exclude them from any public export. |
| PS-02 Automatic hosted workflows | Automatic triggers removed from the default branch and all older branch tips. Earlier read-only checks covered 11 branches and 26 existing workflow files. Three later isolated verification runs were recorded; the temporary verification workflow is restored to manual-only. No recurring automatic triggers are approved. | GitHub Actions settings still require administrative confirmation or explicit disabling. |
| PS-03 Branch governance | `main` currently reports no branch protection. Repository rulesets list is empty. | Apply an enforced `main` branch rule for reviewed changes, blocked force pushes and blocked deletion. Administrative settings cannot be changed by the connected repository actions. |
| PS-04 Divergent branch histories | Historical branches are preserved privately. | Review all refs before publishing this repository. Prefer a new, separately reviewed history-free source snapshot. |
| PS-05 Local environment disclosure | Development and verification documents on private historical branches include machine-specific paths. | Avoid publishing historical development documents. Review a curated public source snapshot and redact any remaining identifying details. |
| PS-06 Supply chain | G6 recorded earlier clean dependency and HIGH/CRITICAL image checks. | The isolated run at `7145ec24a8ac289dfcf6f02644f54f35f4744c8a` passed current NuGet/npm advisory checks and three source-revision-labeled HIGH/CRITICAL image scans. Complete independent E:-backed review, retain scan evidence and assess any subsequent source changes. |
| PS-07 License | Restrictive `LICENSE` now exists on `main`. | Confirm licensing intent before any public source distribution. |
| HS-01 HTTP transport | Source hardening and regression tests are prepared on `fix/publication-security-readiness`. | Hosted verification at `7145ec24a8ac289dfcf6f02644f54f35f4744c8a` passed unit, contract, integration and local Compose/HTTP checks. Independent E:-backed transport verification and review of any later changes remain pending. |
| HS-02 Browser headers and TLS | Browser-response policy and hosted TLS configuration template are prepared on the security branch. | Hosted Chromium, Firefox and WebKit tests passed at the tested SHA. E:-backed review, real TLS certificates, trusted proxy configuration and controlled hosted deployment acceptance remain pending. |

## Publication verification attempt 1, November 3, 2026

- GitHub Actions run [41526406931](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41526406931) completed with **failure**. The tested revision was `d19be42e79f20c472d8a655dab125cab4ac11a4d`, on the isolated verification branch. This does not clear publication or hosting.
- The single `security` job completed its source checkout, script syntax checks, disposable PostgreSQL setup, solution build, and unit and contract tests. The integration step failed: 161 failed, 26 passed, 187 total. The failure stopped frontend tests, package advisories, Compose smoke, browser regressions, and image vulnerability scans. Cleanup completed. No run artifacts were published.
- Repeated integration failures occur in the shared test helper at `ReferenceDataHttpTests.Send` while obtaining `/api/v1/auth/csrf`, which returns HTTP 400. The hardened transport gate requires an actual loopback or explicitly trusted network peer. The original in-memory `WebApplicationFactory` requests have no socket peer, so the test harness did not provide the required peer identity. This is a test-host setup regression, not evidence that the hardened production transport check should be weakened.
- The security work branch now contains a test-only `LocalPeerTestServer` startup filter, applied to integration factories, to provide a loopback peer when the in-memory request has no remote IP. The filter registers a concrete instance rather than relying on a service activator. The production transport check remains unchanged. The fix has not yet passed a complete hosted regression; its effectiveness is pending verification.
- A subsequent run must test its exact checkout SHA and complete every required stage, including PostgreSQL integration, frontend, advisory scans, Compose, three browser engines, and image scans. No historical disclosure, repository governance, licensing, hosting, or publication blocker is waived by a successful test run.

## Isolated publication verification attempts 2 and 3, November 3, 2026

- Attempt 2, [run 41534435227](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41534435227), failed before testing because a shallow checkout did not contain the parent commit required by the source identity guard. The subsequent attempt used `fetch-depth: 2`.
- Attempt 3, [run 41534507638](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41534507638), completed with **failure** on exact checkout SHA `7145ec24a8ac289dfcf6f02644f54f35f4744c8a`. Its parent was security source `d9bc2aba875039bcd84b1212e023cb8cd9f4e319`. All 17 substantive steps before cleanup succeeded. The logs report 149/149 unit, 21/21 contract, 187/187 PostgreSQL integration, 18/18 frontend and 18/18 browser executions. The browser checks ran across Chromium, Firefox and WebKit. Solution build, current NuGet/npm advisories, disposable Compose smoke, Nginx syntax, HTTP probes, synthetic account provisioning, and three exact-revision HIGH/CRITICAL image scans also completed successfully.
- The job failed solely in its final cleanup step: Trivy created root-owned files under `.verification/trivy`, and the unprivileged runner could not remove `metadata.json`, `trivy.db` and `fanal`. Compose services, isolated volumes and the disposable PostgreSQL container were removed before that failure. No run artifacts were uploaded. Successful substantive stages are recorded as observed results, **not** a successful overall run or independent publication clearance.
- The manual-only workflow on isolated verification branch `verify/publication-security-20261103-r3-once` was corrected at `8cf9fffef3cc41823d367e7a54d3e40f5678e242` to use scoped privileged cleanup and a history-aware source guard. This correction is **not yet executed**. No new Actions run was started, and automatic triggers were not enabled.
- Accepted G6 mutation and historical snapshot evidence remains unchanged. The hosted attempt did not execute full Git-object disclosure review, manual historical-artifact clearance, repository governance enforcement, real-host TLS checks, or final source snapshot review. The publication and hosting decisions remain **NOT CLEARED**. Acceptance pending.

## Local E:-backed verification, November 3, 2026

The complete local regression run passed at source `b9adaa8ae0fc5f2df9deb5e4f090695d5048d672` (149 unit, 39 contract, 187 integration twice, 18 frontend, 57 browser tests per engine on Chromium, Firefox and WebKit, transport probes, outage and recovery checks, current advisory audits and three image scans). The original HTTP 400 integration failure was reproduced and traced to in-memory test requests having no remote address; the production transport check was not weakened. The source-locked supply-chain audit passed at `6726983a18a4b7e9b539c86f636ac20d8682e630`. Details, corrected defects and items not executed are in the [remediation register](PUBLICATION_REMEDIATION_STATUS.md#local-e-backed-verification-november-3-2026).

Still open: complete historical disclosure review (unreachable Git objects, serialized synthetic cookie values in retained trace archives, manual review of binary screenshots), enforced `main` branch rules (the repository currently reports none), licensing intent, approval of the history-free snapshot as a new repository, and hosted TLS validation against a real domain. HS-01 and HS-02 local verification is complete for the source above; neither publication nor hosting is cleared.

## Historical artifact disclosure

The 17 retained downloadable Actions archives contain nested browser trace files. The read-only review found 52 serialized authentication cookie-value occurrences across artifacts `13026999079`, `13027355828` and `13027499753`. These are understood to be disposable synthetic-session credentials; no active production compromise has been demonstrated. Nevertheless, the original trace archives are not suitable for public release. They remain preserved in the private repository for gate evidence. No credential values are reproduced here.

## Acceptance and publication boundary

The development changes and local audit runners are maintained on `fix/publication-security-readiness`, with draft pull request #6. Acceptance pending. Do not merge unverified transport changes or make the repository public. A source-only portfolio should be created from a separately approved, scanned snapshot with **no imported historical Git metadata, branches, Actions archives or internal development evidence**.

No GitHub Actions, public hosting, source publication, branch history rewrite or evidence deletion is authorized by this document.
