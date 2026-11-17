# G17-S25 mutation retest evidence (2026-11-17)

Evidence for `docs/M17_G17_S25_MUTATION_RETEST_20261117.md`. G17 NOT ACCEPTED.

| Path | Content |
| --- | --- |
| `classification.json`, `classification-table.md`, `dispositions.json` | Per-mutant records: source and mutated hashes, exact mutation, command, exit, signal, timeout, test counts, failing assertions, disposition |
| `frozen-run-1/` | Retest of all 17 on the exact frozen tree `634053c4` with the original per-mutant test selection |
| `frozen-run-2-verifier/` | Diagnostic verifier mutants with the strict verifier test file only |
| `frozen-run-3-broad/` | Docker ownership and verifier mutants with every test that can run on a non-Windows host |
| `candidate-run/` | The mutants that exposed test gaps, retested on candidate `60576b16` (tests only) |
| `reconstruction-from-raw-jsonl.json` | Independent reconstruction of the 17 from the raw mutation JSONL files |
| `original-harness-reproduction/` | The original mutation harness run on the frozen source (reports INFRASTRUCTURE_FAILURE because it only recognises the spec reporter) |
| `proofs/` | Adversarial demonstrations for the redundant and unreachable mutants, with their scripts and outputs |
| `regression/` | Full `scripts/*.test.mjs` run on the frozen and the candidate tree: 22 failures in both, identical set, all Windows dependent |
| `input-archives/` | SHA-256 files of the supplied archives, recomputed hashes, and inventories derived by `scripts/g17-evidence-archive.mjs` for the archives whose own `.inventory.json` was not supplied |
| `source-fingerprints.json` | Commits, trees and target file hashes at the frozen and the candidate source, tool versions |
| `tooling/` | The harness and builders that produced these records |

Each `Mnn.json` holds the record; `Mnn.control-before.out.txt`, `Mnn.mutated.out.txt` and `Mnn.control-after.out.txt` are the raw TAP output. To re-run: `git archive 634053c4... | tar -x -C <dir>`, put pwsh and docker on PATH, set `SystemRoot` to a directory containing `System32/tar.exe` (bsdtar), and run `node tooling/retest.mjs <dir> <out> M08 ...` (`G17_PHASE=new` for the candidate, `frozen-broad` for the broad frozen selection).

Windows-only tests (producer, three failing-docker tests) could not run on the retest host and are not counted.
