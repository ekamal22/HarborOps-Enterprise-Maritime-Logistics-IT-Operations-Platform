# Publication Safety Status

**Updated:** 2026-11-03  
**Repository visibility:** Private  
**Publication clearance:** NOT CLEARED  
**Public application hosting:** NOT CLEARED

This is the current decision register. Earlier security reviews remain historical records of the exact revisions they examined. No entry here changes accepted M0-G0 through M6-G6 evidence.

| Finding | Current state | Remaining condition |
| --- | --- | --- |
| PS-01 Historical disclosures | Current-tree scan found no high-confidence provider credentials. All 33 available historical job logs from 35 Actions runs were screened for selected secret patterns without matches. All 17 downloadable retained artifact archives were inspected. Three archives contain serialized temporary browser authentication cookie values. | Full Git-object and divergence review is outstanding. Keep raw archives private and exclude them from any public export. |
| PS-02 Automatic hosted workflows | Automatic triggers removed from the default branch and all older branch tips. Read-only checks covered 11 branches and 26 existing workflow files, all manual-only. No new workflow runs were observed. | GitHub Actions settings still require administrative confirmation or explicit disabling. |
| PS-03 Branch governance | `main` currently reports no branch protection. Repository rulesets list is empty. | Apply an enforced `main` branch rule for reviewed changes, blocked force pushes and blocked deletion. Administrative settings cannot be changed by the connected repository actions. |
| PS-04 Divergent branch histories | Historical branches are preserved privately. | Review all refs before publishing this repository. Prefer a new, separately reviewed history-free source snapshot. |
| PS-05 Local environment disclosure | Development and verification documents on private historical branches include machine-specific paths. | Avoid publishing historical development documents. Review a curated public source snapshot and redact any remaining identifying details. |
| PS-06 Supply chain | G6 recorded earlier clean dependency and HIGH/CRITICAL image checks. | Re-run up-to-date local package and image scans on the exact candidate revision and retain evidence. |
| PS-07 License | Restrictive `LICENSE` now exists on `main`. | Confirm licensing intent before any public source distribution. |
| HS-01 HTTP transport | Source hardening and regression tests are prepared on `fix/publication-security-readiness`. | E:-backed local build, contract, integration and proxy verification, followed by independent acceptance. |
| HS-02 Browser headers and TLS | Browser-response policy and hosted TLS configuration template are prepared on the security branch. | E:-backed browser tests, verified certificates, trusted proxy configuration and controlled hosted deployment acceptance. |

## Publication verification attempt 1, November 3, 2026

- GitHub Actions run [41526406931](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41526406931) completed with **failure**. The tested revision was `d19be42e79f20c472d8a655dab125cab4ac11a4d`, on the isolated verification branch. This does not clear publication or hosting.
- The single `security` job completed its source checkout, script syntax checks, disposable PostgreSQL setup, solution build, and unit and contract tests. The integration step failed: 161 failed, 26 passed, 187 total. The failure stopped frontend tests, package advisories, Compose smoke, browser regressions, and image vulnerability scans. Cleanup completed. No run artifacts were published.
- Repeated integration failures occur in the shared test helper at `ReferenceDataHttpTests.Send` while obtaining `/api/v1/auth/csrf`, which returns HTTP 400. The hardened transport gate requires an actual loopback or explicitly trusted network peer. The original in-memory `WebApplicationFactory` requests have no socket peer, so the test harness did not provide the required peer identity. This is a test-host setup regression, not evidence that the hardened production transport check should be weakened.
- The security work branch now contains a test-only `LocalPeerTestServer` startup filter, applied to integration factories, to provide a loopback peer when the in-memory request has no remote IP. The filter registers a concrete instance rather than relying on a service activator. The production transport check remains unchanged. The fix has not yet passed a complete hosted regression; its effectiveness is pending verification.
- A subsequent run must test its exact checkout SHA and complete every required stage, including PostgreSQL integration, frontend, advisory scans, Compose, three browser engines, and image scans. No historical disclosure, repository governance, licensing, hosting, or publication blocker is waived by a successful test run.

## Historical artifact disclosure

The 17 retained downloadable Actions archives contain nested browser trace files. The read-only review found 52 serialized authentication cookie-value occurrences across artifacts `13026999079`, `13027355828` and `13027499753`. These are understood to be disposable synthetic-session credentials; no active production compromise has been demonstrated. Nevertheless, the original trace archives are not suitable for public release. They remain preserved in the private repository for gate evidence. No credential values are reproduced here.

## Acceptance and publication boundary

The development changes and local audit runners are maintained on `fix/publication-security-readiness`, with draft pull request #6. Acceptance pending. Do not merge unverified transport changes or make the repository public. A source-only portfolio should be created from a separately approved, scanned snapshot with **no imported historical Git metadata, branches, Actions archives or internal development evidence**.

No GitHub Actions, public hosting, source publication, branch history rewrite or evidence deletion is authorized by this document.
