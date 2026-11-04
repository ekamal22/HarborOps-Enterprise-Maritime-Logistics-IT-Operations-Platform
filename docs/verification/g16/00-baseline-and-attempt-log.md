# G16 local verification: baseline and attempt log

**Status:** In progress. Technical verification incomplete. G16 acceptance pending.
This document is verification reporting only. No implementation file was modified.

## Source under test

| Item | Value |
| --- | --- |
| Branch | `feat/m16-audit-access-hardening` |
| Source revision | `ef08d7c91d368f082fc97bac32acab1fdbd5cfca` (remote HEAD confirmed equal to the recorded revision before the campaign) |
| Source worktree | `E:\HarborOps-Verification\worktrees\m16-source` (detached, clean, never edited) |
| `main` at campaign start | `7b9e19d730e3a09c5e1184262460dfeac9319e3d` |
| M9 branch head observed | `cff7c254f642432086689437f8a7403b683425bd` (`feat/m9-worker-reconciliation`, read only) |
| Reporting branch | `verify/g16-local-evidence`, based on `main` |
| Private evidence root | `E:\HarborOps-Verification\evidence` (attempt roots below) |

The M16 branch is based on approved M16 documentation (`6c14c78`), before the M8 merge into `main`. M16 has not been combined with `main` or M9 in this campaign.

## Preflight (Phase A) observed facts

- Docker Engine 29.6.2 (Linux), Compose v5.3.1, Docker data image on E: (runner storage check passed).
- GitHub Actions: repository API reports `enabled: false`. Not touched.
- Ports 5176, 8086 and 55446 were free. No `ho-verify-g16` Compose project, volume or `ho-g16-pg` container existed. Subnet 172.30.250.0/24 was unused by other Docker networks.
- Unrelated containers (`harborops-*` on 5173/8080/55432, `tz-m11d-lab-*`) were running and are not touched.
- E: free 98.5 GB. C: free 4.7 GB of 200 GB (98% used); all caches and temp for the campaign are on E:.
- Caches were seeded by robocopy from the previous accepted run (g8 attempt 04), excluding TEMP, TMPDIR, HOME.

## Runner inspection notes (no edits made)

- `scripts/verify-m16-local.sh` is fail-fast under `set -e`: the first failed stage ends the run.
- The runner requires a root ending `/m16` and refuses to reuse an existing `evidence` directory, so every attempt needs a new parent directory.
- The runner contains no fault-injection or mutation stage, and the role check in `scripts/check-m16-db-roles.sh` is a grant-contract probe only. Several G16 scenarios (S06, S08, S19, S22, S29 and the guard-removal proofs required by the plan) depend solely on integration tests.

## Attempts

### Attempt 01 (original runner, unmodified)

- Started 2026-11-04T18:38:37Z. Root: `E:\HarborOps-Verification\evidence\m16`.
- Stage `preflight` exit 0. Stage `docs` exit 1 after 0 s. The runner stopped; no later stage executed.
- See finding G16-F001.

### Attempt 02 (derived runner, docs stage tolerated)

- Derived copy kept at `E:\HarborOps-Verification\evidence\attempt-02\verify-m16-derived.sh` next to the untouched original copy. The only differences: `cd` pinned to the source worktree and the `docs` stage failure tolerated so that independent stages run. The source tree and its scripts are unchanged.
- Results of this attempt are recorded in later checkpoints.
