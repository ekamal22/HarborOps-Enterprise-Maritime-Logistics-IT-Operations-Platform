# Engineering and Documentation Conventions

**Current M9/G9 status (2026-11-05):** G9 was accepted with the mandatory [M9-01 transport-backlog operating condition](M9_LOCAL_CONTRACT_DECISION.md). M9 is integrated into `main` through PR #16 (`43fe326`) and PR #18 (`4684352`). The [G9 gate closure](M9_G9_GATE_CLOSURE.md) records the evidence review and residual limitations. No production or public deployment is authorized; sustained transport-event processing is held until M10 or another explicitly approved backlog mechanism is verified. Earlier planning or acceptance-pending language in historical subsections reflects the earlier project phase.

## Working approach

- Keep changes limited to the named milestone and its explicit dependencies.
- Prefer small, reviewable revisions with a reproducible validation trail.
- Do not modify unrelated modules merely to make a change appear complete.
- Update affected requirements, interfaces, architecture, tests, and status records together.
- Distinguish planned work from implemented code and measured results.
- Never claim a test passed without recording the actual executed command and outcome.

## Branches and commits

- Use descriptive branch names tied to a milestone or concern, such as `feat/m3-port-vessel-reference-data` or `fix/m3-port-lookup`.
- Write concise, imperative commit subjects describing the change, not the activity or an unverified outcome. Append `[skip ci]` to all commits while GitHub Actions is prohibited.
- Do not append co-author trailers, attribution footers, automated signatures, or tool-origin notes to commit messages.
- Review commit metadata before publication. Keep the commit body relevant to technical content.
- Do not rewrite shared history without an explicit reason and approval.
- Do not trigger, dispatch, rerun or indirectly cause GitHub Actions workflows. Run required verification locally on E: using isolated resources. Check the event triggers before pushing. If a push cannot be made without launching a workflow, do not push and report the blocker.

## Language and references

- Describe the product and changes directly, using neutral project language.
- Avoid third-person statements about the individual maintaining the project.
- Use concise status wording such as "acceptance pending" rather than assigning an acceptance obligation to a person.
- Do not include unrelated professional applications, employer details, personal biography, or recruiting context in project materials.
- Avoid claims of external affiliation, certification, deployment, or compliance that cannot be supported.
- Do not include private customer information or identifying details in issue examples, screenshots, or fixtures.

## Definition of done

A change is done only when:
1. The feature behavior and error cases are implemented.
2. Relevant automated tests were run and results recorded.
3. Data migrations, compatibility, and authorization impacts were considered.
4. Security and privacy implications were reviewed.
5. User-visible states are accessible and consistent where relevant.
6. Documentation and STATUS.md accurately describe the result.
7. Acceptance evidence is available, with any remaining limitations identified.

## Code review checklist

- Correct domain boundary and minimal coupling.
- Validated input and consistent failure behavior.
- Explicit organization and role authorization.
- Transactional integrity and concurrency protection.
- Correct event deduplication/retry behavior when relevant.
- No secrets, sensitive data, or unsuitable telemetry.
- Deterministic tests and reproducible local commands.
- Clear rollback, migration, and operational implications.
- Documentation matches actual code.

## Mandatory local disk policy

Use the configured E:-backed workspace root for the working tree and E-backed locations for caches, build/test output, Docker/WSL state and logs. See [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Never configure persistent project storage on C:. Unavoidable system temporary use may occur. Inspect effective paths before introducing additional tools. Do not prune or relocate existing storage without specific approval.

## Scoped milestone execution

- The release is split into M0-M24, each with its own G0-G24 evidence gate. The graph data contains the authoritative task dependencies.
- Before starting a work package, identify its graph ID and verify all **explicit** predecessors. Milestone numbers and P0-P5 stage labels are not serial prerequisites; ready packages may run concurrently. Implement only their bounded output and necessary dependencies.
- Attach tests covering negative cases, permissions and migration implications to that specific work package, rather than deferring all verification until the final release.
- Completion requires executed evidence and explicit gate acceptance. A reviewable commit, source inspection, or successful partial test suite is insufficient to close a gate. Parallel source branches must be integrated and rerun against the merged source SHA before gate acceptance.
- The former v1 M1-M6 node IDs are superseded. Historical task references must be mapped by deliverable and verification, not by copying IDs.

## Concurrent branches and migration coordination

Use one branch/worktree per independent milestone, from an accepted baseline. Establish shared API and permission contracts before integration, and coordinate additive database migrations in one canonical order. Keep caches, Docker images/data and evidence on E:. Never rebase unrelated unaccepted work into the same gate or infer that an earlier independent PASS certifies the combined result. See [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md). GitHub Actions remain prohibited, and commits require `[skip ci]`.

## Planning document authority

- Update [PRD](PRD.md) for product scope and priorities, [Requirements](REQUIREMENTS.md) for atomic FR/NFR changes, [Roadmap](ROADMAP.md) for milestone delivery and gate changes, and [execution-graph.json](execution-graph.json) for work-package dependency changes.
- Never hand-edit [Execution Graph](EXECUTION_GRAPH.md). Regenerate with `node scripts/render-execution-graph.mjs`, then check with `node scripts/render-execution-graph.mjs --check` and `node scripts/check-planning-docs.mjs`.
- Treat [Status](STATUS.md) and milestone evidence registers as the only source for achieved results and acceptance state.
- When changing a node ID or requirement mapping, update affected code tasks, test evidence, gates, and references in the same change.

## Accepted M5 shipment module

The M5 shipment implementation and [G5 acceptance](M5_G5_GATE_CLOSURE.md) are integrated into `main`. Preserve its tenant-scoped state history, optimistic concurrency and authorization behavior in later work. M6 allocations are also integrated and [G6 accepted](M6_G6_GATE_CLOSURE.md). Use local E:-backed verification, `[skip ci]` commits and pull requests to protected `main`; keep GitHub Actions disabled.

## Accepted M4 module and contribution boundary

Read the [M4 architecture](M4_ARCHITECTURE_DESIGN.md), [implementation plan](M4_IMPLEMENTATION_PLAN.md) and [G4 matrix](M4_VERIFICATION_PLAN.md) before any implementation. [D-003 Option B](DECISIONS.md) is accepted, and [G4 evidence](M4_EXECUTED_VERIFICATION.md) was independently reviewed and accepted on 2026-11-01. Maintain the frozen identifier contract unless a new explicit decision authorizes a change. Voyage/container feature branches may progress in parallel, but use one coordinated EF migration order and preserve accepted M3 composite tenant identity. No GitHub Actions. Every commit uses `[skip ci]`, and the accepted G4 behavior must be preserved during later integration.

## Accepted M3 implementation and local verification boundary

The M3 [architecture](M3_ARCHITECTURE_DESIGN.md), [implementation plan](M3_IMPLEMENTATION_PLAN.md) and [G3 verification specification](M3_VERIFICATION_PLAN.md) govern the accepted implementation in `main`. The complete G3 matrix passed locally on `b72025d35d62d0a8a45b466986a0b3bd89eebadb` and was explicitly accepted on 2026-10-31 ([evidence](M3_EXECUTED_VERIFICATION.md)). Keep all M0-M3 regression coverage, preserve tenant ownership and Manager/Analyst authorization, and use disposable E:-backed verification. GitHub Actions remain prohibited. M4 was accepted and integrated into `main` on 2026-11-01.

## Historical M1 identity implementation review boundary

The accepted [D-001 decision](ADR_D001_BROWSER_SESSIONS.md) and [M1 design](M1_AUTHENTICATION_DESIGN.md) control authentication implementation. The completed M1 pull request included schema/migration safety, server-session authority, cookie/CSRF/limit configuration, restricted local provisioning, secure key persistence, API contract tests, functional browser coverage and the historical GitHub Actions evidence in [G1 Verification](M1_VERIFICATION_PLAN.md). This does not authorize any additional workflow runs. No public account seeds, secret examples or actual key material may enter version control. Retained local Docker volumes are not routine test targets.

Do not conflate architecture approval with milestone acceptance. **G0-G9 are verified and explicitly accepted through 2026-11-05, with M8 and M9 integrated into protected `main` through PRs #14 and #18.** M1 merged via PR #4 ([evidence](M1_IMPLEMENTATION_VERIFICATION.md)), M2 via PR #5 ([evidence](M2_EXECUTED_VERIFICATION.md)), M3 by fast-forward ([evidence](M3_EXECUTED_VERIFICATION.md)); M4-M6 have accepted closure records, and M7 was accepted on 2026-11-03 ([closure](M7_G7_GATE_CLOSURE.md)). M8/G8 and D-006 Option A were accepted after [independent review](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md). G10-G24 each require independent verification and explicit acceptance. Final accessibility verification is reserved for M24-04.

## Documentation hygiene

- README is the entry point; implementation detail belongs in docs.
- Avoid duplicated authoritative definitions. Link to the primary specification.
- Update API_CONTRACTS.md for endpoint/interface changes.
- Update DOMAIN_MODEL.md for persistent invariants and migration changes.
- Update SECURITY.md when a trust boundary changes.
- Update OPERATIONS.md for runtime and incident behavior.
- Update ACCEPTANCE_CRITERIA.md for changed release gates.
- Keep STATUS.md evidence-based and current.
- Review all relative links and status claims before merging.
- Run `node scripts/render-execution-graph.mjs --check` and `node scripts/check-planning-docs.mjs` before source-locked milestone verification. The planning checker validates links throughout README and docs, status invariants and prohibited attribution trailers.

## Change reporting

Preferred structure: scope, changed paths, behavior, tests actually executed, results, known limitations, and acceptance status. If no tests were run, state "not run" and why.

## Current M9/M16 integration checkpoint

Accepted `main` includes M0-M9/G0-G9. M9's 809/809 .NET, 14/14 mutation and 174/174 browser evidence, independent review, [M9-01 accepted contract](M9_LOCAL_CONTRACT_DECISION.md) and [G9 closure](M9_G9_GATE_CLOSURE.md) are documented. Sustained transport-event processing remains held pending approved backlog handling; production deployment is not authorized.

M16's standalone technical verification on `e571157` has a completed [162-file main-campaign evidence review](M16_G16_INDEPENDENT_EVIDENCE_REVIEW.md), and the [S07/S08/S28 supplemental log/JSON](M16_G16_SUPPLEMENT_EVIDENCE_REVIEW.md) are **REVIEWED** (6/6 checks), but combined M9/M16 migration/security regressions, G16 acceptance and merge authorization remain pending. Before integration, preserve M9 operator permissions and routes, M16 audit permissions and routes, both ordered migrations, worker-role provisioning boundaries and applicable audit coverage. Keep persistent resources and evidence on E:, preserve accepted `main`, do not run GitHub Actions and include `[skip ci]` in every commit.
