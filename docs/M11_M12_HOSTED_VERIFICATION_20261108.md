# M11/M12 Hosted Verification Record

**Authorization:** Hosted GitHub Actions verification is authorized for the isolated M11/M12 combined integration candidate, including necessary diagnostic reruns. This authorization does not accept G11 or G12, merge to protected main, or authorize deployment.

**Reference baseline:** `6ead1d076946dd96b53401265a31efc35c4a666c`
**Verification branch:** `feat/m11-m12-combined-integration`
**Workflow:** `.github/workflows/verify-m11-m12-combined.yml`

## Verification boundaries

The workflow establishes the exact source SHA before tests and separates source/build/unit/contract evidence from G11 and G12 runtime evidence. G11 and G12 use independent disposable PostgreSQL servers. The exclusive G12 suite uses `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1` only in its own server-isolated job. Source and test logs, TRX files and SHA-256 manifests are uploaded as short-retention run artifacts where produced.

A failing or skipped job is not PASS. All scenario dispositions require matching raw evidence and a separate independent G11-S25 and G12-S25 review. Any subsequent remediation produces a different source SHA and needs impacted tests rerun against the new SHA.

## Known open issues at launch

- M11 inherited broad API SELECT permissions require least-privilege SQL hardening and direct restricted-role verification.
- M11 conflict-specific container/event provenance is not complete.
- M12-P01 through P16 remain undecided.
- The G11 and G12 verification matrices are not closed by a source build or limited targeted integration tests.

The M9 transport hold remains mandatory. Original M11/M12 branches, protected `main` and production data are not modified by this verification plan.
