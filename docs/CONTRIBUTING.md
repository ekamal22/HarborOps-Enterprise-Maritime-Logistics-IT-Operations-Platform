# Engineering and Documentation Conventions

## Required workspace storage

All project-specific persistent files must be on E:. Use `E:\\APP IDEAS\\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform` for the repository; keep NuGet/npm caches, Docker/WSL state, database volumes, build outputs, temporary project datasets, screenshots and logs under approved E-drive directories. See [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Unavoidable short-lived Windows temporary activity on C: is permitted; new persistent project storage on C: is not. Inspect effective paths before running new tools, and do not prune or relocate unrelated data.

## Working approach

- Keep changes limited to the named milestone and its explicit dependencies.
- Prefer small, reviewable revisions with a reproducible validation trail.
- Do not modify unrelated modules merely to make a change appear complete.
- Update affected requirements, interfaces, architecture, tests, and status records together.
- Distinguish planned work from implemented code and measured results.
- Never claim a test passed without recording the actual executed command and outcome.

## Branches and commits

- Use descriptive branch names tied to a milestone or concern, such as feat/m1-shipment-core or fix/m2-event-deduplication.
- Write concise, imperative commit subjects describing the change, not the activity or an unverified outcome.
- Do not append co-author trailers, attribution footers, automated signatures, or tool-origin notes to commit messages.
- Review commit metadata before publication. Keep the commit body relevant to technical content.
- Do not rewrite shared history without an explicit reason and approval.

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

## Change reporting

Preferred structure: scope, changed paths, behavior, tests actually executed, results, known limitations, and acceptance status. If no tests were run, state "not run" and why.
