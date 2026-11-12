# Engineering and Documentation Conventions

**Turkish localization source integrated, 2026-11-12:** [PR #48](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/48) merged at `c81e54bfef25a0ff957f27b488ab7bd420551244` following 41 passing frontend and 99 passing browser executions in [run #43199664246](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43199664246). All server-facing rules and default-off operational controls remain unchanged. [Verification and acceptance](TURKISH_G15_S25_POSTMERGE_ACCEPTANCE_20261112.md).

**G15 current disposition (2026-11-12):** G15/S25 **ACCEPTED** and merged into `main` via PR #44 (`49934d793542ef43cfe587c54b16cfd5cce8e2f4`). Tested source `ebb28d2332ab6e7eb4264265db3ae35e8fbcbc35` passed run 43178122017; reviewer-independence attestation remains a disclosed procedural exception. G13 is still conditionally integrated. P10 fail-closed capture, P12 no deletion and default-off delivery remain mandatory; production rollout and G24 release are not approved. [Final decision](G15_S25_FINAL_ACCEPTANCE_20261112.md), [post-merge reconciliation](G15_S25_POSTMERGE_ACCEPTANCE_RECONCILIATION_20261112.md).

G0-G12, G15 and G16 are accepted and integrated. G13 remains conditionally accepted. G15 S25 was explicitly accepted with a disclosed reviewer-independence procedural exception; production deployment remains unauthorized.


## Localization implementation conventions

User-facing English labels used by the covered application workspaces should be included in `web/src/i18n/messages.mjs` for Turkish presentation through `useI18n`. Locale identifiers are `en` and `tr-TR`; language preference is stored in `harborops.language` and synchronized across tabs. Preserve every server-facing enum, state, key and permission; translate visible text only. Verify with `npm --prefix web test`, `npm --prefix web run build` and authenticated browser tests before integration. The accepted combined source-locked reference is [run #43199664246](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43199664246).

## Current G10 contribution boundary

M10-L01 through M10-L14 were approved on 2026-11-07 with revised L07/L14 and conditional L06, verified for S01/S25 conformance and merged via [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28). The thirteenth migration, restricted signed corrections and atomic audit are accepted. Preserve source-locked historical evidence and the M9 transport hold.

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

## M17 implementation and evidence discipline

For observability changes follow [M17 implementation](M17_IMPLEMENTATION_PLAN.md), [M17 policy proposals](M17_POLICY_PROPOSAL_20261112.md), [G17 verification](M17_VERIFICATION_PLAN.md) and [the 75-assertion inventory](M17_G17_ASSERTION_INVENTORY_20261112.json). Keep new exporter and dashboard disabled by default and internal-only; instrumentation must not modify accepted API/SQL/session/audit behavior. Never add high-cardinality tenant/user identifiers or private payloads to metric labels, trace spans, screenshots or logs. Use a source-locked disposable E:-backed campaign, preserve failed evidence, review negative privacy/outage tests and obtain independent G17-S25 assessment before requesting acceptance. Docs-only M17 planning does not authorize Actions dispatch or implementation.

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

The M5 shipment implementation and [G5 acceptance](M5_G5_GATE_CLOSURE.md) are integrated into `main`. Preserve its tenant-scoped state history, optimistic concurrency and authorization behavior in later work. M6 allocations are also integrated and [G6 accepted](M6_G6_GATE_CLOSURE.md). Use local E:-backed verification, `[skip ci]` commits and pull requests to protected `main`; do not run GitHub Actions without explicit authorization.

## Accepted M4 module and contribution boundary

Read the [M4 architecture](M4_ARCHITECTURE_DESIGN.md), [implementation plan](M4_IMPLEMENTATION_PLAN.md) and [G4 matrix](M4_VERIFICATION_PLAN.md) before any implementation. [D-003 Option B](DECISIONS.md) is accepted, and [G4 evidence](M4_EXECUTED_VERIFICATION.md) was independently reviewed and accepted on 2026-11-01. Maintain the frozen identifier contract unless a new explicit decision authorizes a change. Voyage/container feature branches may progress in parallel, but use one coordinated EF migration order and preserve accepted M3 composite tenant identity. No GitHub Actions. Every commit uses `[skip ci]`, and the accepted G4 behavior must be preserved during later integration.

## Accepted M3 implementation and local verification boundary

The M3 [architecture](M3_ARCHITECTURE_DESIGN.md), [implementation plan](M3_IMPLEMENTATION_PLAN.md) and [G3 verification specification](M3_VERIFICATION_PLAN.md) govern the accepted implementation in `main`. The complete G3 matrix passed locally on `b72025d35d62d0a8a45b466986a0b3bd89eebadb` and was explicitly accepted on 2026-10-31 ([evidence](M3_EXECUTED_VERIFICATION.md)). Keep all M0-M3 regression coverage, preserve tenant ownership and Manager/Analyst authorization, and use disposable E:-backed verification. GitHub Actions remain prohibited. M4 was accepted and integrated into `main` on 2026-11-01.

## Historical M1 identity implementation review boundary

The accepted [D-001 decision](ADR_D001_BROWSER_SESSIONS.md) and [M1 design](M1_AUTHENTICATION_DESIGN.md) control authentication implementation. The completed M1 pull request included schema/migration safety, server-session authority, cookie/CSRF/limit configuration, restricted local provisioning, secure key persistence, API contract tests, functional browser coverage and the historical GitHub Actions evidence in [G1 Verification](M1_VERIFICATION_PLAN.md). This does not authorize any additional workflow runs. No public account seeds, secret examples or actual key material may enter version control. Retained local Docker volumes are not routine test targets.

Do not conflate architecture approval with milestone acceptance. **G0-G12 and G16 are explicitly accepted and integrated into protected `main` by 2026-11-08.** Remaining gates require their own separate reviews. [G11/G12 closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md). No deployment is implied.

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

## Current accepted M9/M16 integration (2026-11-06)

Accepted `main` includes M0-M12/G0-G12 and M16/G16. [G9 closure](M9_G9_GATE_CLOSURE.md) preserves 809/809 .NET tests, 14/14 mutation families and 174/174 browser executions. M16's accepted [combined gate](M16_G16_GATE_CLOSURE.md) records later independently reviewed security, migration, browser and F001 extension evidence. The mandatory G9 sustained transport-processing hold remains, and no production deployment is authorized.

M16's standalone `e571157`, combined `79318c6` and post-acceptance F001 `8ba7727` records retain their separate source-locked evidence. The original S07/S08/S28 supplement was later reviewed, with its provenance limitation disclosed. G16 was accepted and [PR #25](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/25) merged on 2026-11-06. Keep persisted resources on E:, preserve historical evidence, do not run GitHub Actions, and include `[skip ci]` in commits. [Documentation guide](DOCUMENT_STATUS_GUIDE.md) distinguishes living versus historical documents.
