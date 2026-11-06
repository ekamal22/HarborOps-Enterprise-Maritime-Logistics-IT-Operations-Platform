# M16 / G16 Gate Closure

**Acceptance date:** 2026-11-06
**Formal disposition:** **G16 accepted for the combined M9/M16 implementation, with the documented operating restrictions and residual limitations.**
**Authority:** Explicit approval in the project conversation on 2026-11-06 of G16 acceptance and all four recommended Option A integration decisions, following independent inspection of the uploaded combined evidence archive.
**Verified runtime source:** `79318c6b6126362a0b26a1452aad6d2b48fdf4b4`.
**Accepted baseline:** `main` at `b4f7b93f1ee8135b9a16b605ded315b3fbc036cc`, including accepted M9.
**Integration PR:** [PR #25](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/25), pending separate merge authorization.
**Evidence archive:** `cg16-combined-evidence-79318c6.tar.gz`, SHA-256 `ffa263f7f5abb3c27a0907d3704b902fd8b0812b237920079b6c92c4461dd74b`.

## Source-locked evidence reviewed

The independently inspected archive contained 544 files in the SHA-256 manifest. All 544 entries matched, with no missing or unlisted files; the full archive checksum matched the submitted report. The raw test records, mutation outputs, upgrade snapshots and security findings were examined independently of the original technical summary.

| Verification dimension | Reviewed result |
| --- | --- |
| .NET warning-as-error build | PASS |
| .NET unit / contract | 228/228 and 39/39 |
| PostgreSQL integration | 286/286, repeated 286/286 on a fresh database |
| Web unit and production build | 18/18, build PASS |
| Chromium, Firefox, WebKit | 61/61 in each engine in final-03 |
| Audit HTTP and outage/recovery | 74/74 and 14/14 in both final-02 and final-03 |
| Combined stack and restricted-role checks | 20/20 in both final-02 and final-03 |
| Targeted integration-boundary mutations | Clean control and 18/18 intended mutant kills |
| Existing accepted-M9 populated upgrade | 12/12 post-upgrade checks; all 20 preexisting table hashes preserved |
| Four final image scans | No reported HIGH or CRITICAL findings |

**Evidence composition is accepted as a qualified result.** The final source did not have one uninterrupted all-green execution. `final-02` had four unexpected Firefox browser results across three specifications, while the same-source `final-03` supplement passed every browser specification and the targeted mutations. Earlier `final-01` image-scan failures were corrected before the frozen tested source. All failed attempts remain preserved, and the Firefox environment-fault explanation was not proven. The accepted technical finding is the aggregate of these source-matched records, not a claim that `final-02` passed unconditionally.

The populated-M9 upgrade demonstrated the additive M16 migration applied after an already-installed M9 worker migration, with the 17 attempt rows, three processing effects and retained table contents preserved. See [executed combined verification](M9_M16_COMBINED_VERIFICATION.md), [combined integration contract](M9_M16_COMBINED_INTEGRATION_STATUS.md), [standalone main evidence review](M16_G16_INDEPENDENT_EVIDENCE_REVIEW.md) and [supplement review](M16_G16_SUPPLEMENT_EVIDENCE_REVIEW.md).

## Four approved integration decisions

| Decision | Approved disposition, Option A |
| --- | --- |
| G16-I01: Worker-effect audit | Immutable M9 attempt-history and processing-effect records remain the authoritative worker audit record. No duplicate M16 rows for M9 transitions or redrive are required for this gate. |
| G16-I02: Database role provisioning | The M16 migration-time provisioner is the authoritative path for the combined stack. The manual M9 role script remains historical/standalone and is not a second combined-stack startup authority. |
| G16-I03: Privileged migrator | Retain the documented privileged migrator for the present local-first scope. Runtime API and worker identities stay restricted; privileged administrators remain outside the claimed database-only tamper resistance. |
| G16-I04: F001 API confinement mechanism | Accept the tested provisioner-installed database guard triggers, without introducing a twelfth migration now. A separate security design is required if the API database identity is shared more broadly. |

These choices supplement, and do not reverse, approved M16-D01 through M16-D05.

## Continuing restrictions and disclosed limitations

- **Mandatory accepted-M9 condition:** Do not enable sustained transport-event processing before a separately approved and verified approach prevents `NeedsReconciliation` backlog exhaustion at the 10,000 unresolved-work-item limit. Controlled isolated tests do not authorize sustained service.
- Retention remains 365 days online followed by controlled archival. No automatic deletion, hard purge, external cryptographic anchoring or global audit export is authorized by G16.
- The audit cursor's documented statement-time/late-commit ordering caveat remains. Do not represent it as a commit-ordered cursor.
- The original standalone G16 supplemental probe has a source-SHA provenance limitation; the combined source-locked campaign supplies later evidence without rewriting the original probe.
- A pre-correction M16 database was not found in inspectable retained environments. A separate retained old-copy volume was intentionally not inspected. If any incompatible database is later identified, require a separately approved safe-copy remediation.
- The intermittent Firefox teardown failure remains an explicitly disclosed, unproven infrastructure attribution.
- The upstream image `apt` correction must be revisited when base images include the fixed version.
- This acceptance does not certify future M10 changes, later unreleased milestones, public hosting, the full G24 release or production deployment.

## Merge boundary

**G16 is accepted, but M16 is not yet merged into protected `main`.** This approval does not itself authorize a merge. Before merging PR #25:

1. Review the exact final source diff, especially F001 guard triggers, F002 role grants, migration history and changed Docker images, and resolve any substantive source findings.
2. Confirm the PR's later changes from `79318c6b6126362a0b26a1452aad6d2b48fdf4b4` remain documentation and evidence-summarization only; do not claim runtime verification of any subsequently changed application code.
3. Reconcile the gate closure and current status documents without overwriting historical failed attempts or accepted M9/G9 reports.
4. Confirm PR #25 is current against `main`, has no unresolved review concerns, and satisfies applicable protected-branch rules. GitHub Actions must remain disabled.
5. Obtain explicit separate PR #25 merge authorization. Use a merge commit with `[skip ci]`, verify the exact post-merge `main` tree and preserve the accepted-M9 operating restriction.
6. Reconcile `docs/STATUS.md`, `docs/DECISIONS.md` and release/operations references with the actual merge SHA after merging.

**Final disposition:** G16 accepted on 2026-11-06 for verified source `79318c6b6126362a0b26a1452aad6d2b48fdf4b4`. PR #25 merge and production deployment remain unauthorized until separately approved.

## Subsequent pre-merge security review finding

The independent final source-diff review identified a missing negative case in F001: before the correction below, the restricted API role could potentially insert a fabricated `Redriven` attempt at generation zero against a newly accepted `Pending` item, naming an active Manager, without a legitimate parked-to-Pending redrive transition. This is not covered by the tested runtime SHA `79318c6` and is distinct from the previously accepted F001 fix.

The integration branch now includes a follow-up restriction requiring a nonzero redrive generation, a reset attempt counter and the expected manual-redrive reason code, plus a focused regression that uses matching generation-zero values. **This follow-up source change requires targeted PostgreSQL execution and a source-locked gate extension before merging.** The previously recorded G16 acceptance remains attached to `79318c6`; this notice neither alters the original evidence nor claims the new change has already passed. PR #25 must stay unmerged while validation is outstanding.
