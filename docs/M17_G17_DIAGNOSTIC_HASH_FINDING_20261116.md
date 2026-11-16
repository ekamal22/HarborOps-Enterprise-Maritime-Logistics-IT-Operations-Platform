# G17 Diagnostic State Hash-List Finding

**Recorded:** 2026-11-16
**Status (updated 2026-11-16 after the final candidate): CORRECTED prospectively and VERIFIED at source `7cc25dabc42b78debf6c9b200fd5209e45b0f56d`.** The text below records the original finding and is kept as written. Original artifacts remain immutable. See the correction section at the end.

The independent review found that `diag-hashes.txt` agrees with seven of the eight diagnostic files in each of r1c, r2 and r3. The single mismatch in every run is `diag-state.json`. The producer, `docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1`, computes the list and **then** writes the final `stopped_utc` timestamp into `diag-state.json`. The internal hash therefore describes the state file before its final write.

The outer independent-review bundle inventory and archived file bytes agree. The mismatch is an evidence-producer ordering defect, **not established alteration of raw latency samples, campaign stages, or the three PASS verdicts**.

**Prospective fix:** Write the completed diagnostic state before hashing it; exclude the hash list itself from self-referential calculations; add an exact-byte regression test. Do not edit the original three internal hash lists or reissue old manifests as though they were unchanged. Freeze and verify the corrected producer with the next applicable source-locked candidate.

This finding does not change P09 numerical budgets or the qualified P09 decision, and does not itself sign G17-S25.

[Policy decision](M17_G17_P09_QUALIFIED_PASS_DECISION_20261116.md) | [Original controlled results](M17_G17_S25_P09_CONTROLLED_VERIFICATION_RESULTS_20261115.md)

## Correction and verification record (2026-11-16)

**Demonstrated defect.** Recomputed from the committed files, the listed size and checksum of `diag-state.json` do not describe the file: r1c lists 332 bytes (`5658c402...`) and the file is 373 bytes, r2 lists 330 and the file is 371, r3 lists 330 and the file is 371. The 41 extra bytes are the `stopped_utc` field added after hashing.

**Correction.** `docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1` now writes the finalized state first, then writes `diag-hashes.txt` last, excluding that file from its own list. No other behavior changed.

**Verification, separate from the historical record.**
- `scripts/g17-diagnostic-hash-list.mjs` independently checks a diagnostic directory: line format, size and SHA-256 of every listed file, no duplicate, no self-listing, no unlisted file, `diag-state.json` listed and final (`stopped_utc` present, valid and not before `started_utc`).
- `scripts/g17-diagnostic-hash-list.test.mjs` (12 tests) runs the real producer against a disposable fixture, including a second Stop, verifies exact size and checksum of the final state, rejects 16 tamper and malformed cases and 11 further reason-specific cases, and uses `scripts/fixtures/g17-diagnostics-legacy-order.ps1`, a byte copy of the previous producer, as a negative control that must show exactly the one state-file size and checksum mismatch.
- A real Start then Stop run against a disposable project name (8 files, sidecar removed) verified clean.
- 22 single-fact mutants of the verifier were run against its tests; final counts are in the [correctness and test audit](M17_G17_FINAL_CORRECTNESS_AND_TEST_AUDIT_20261116.md).
- A pinned characterization test reads the committed r1c, r2 and r3 records and requires exactly their one known mismatch each, so the originals cannot be edited or "repaired" unnoticed. They were not reconstructed.

**Not claimed.** The corrected producer has not yet produced evidence for a P09 campaign; none was run. The outer independent-review bundle inventory was never affected. This does not change P09 budgets, the qualified P09 decision or the pre-release optimization obligation, and does not sign G17-S25. See the [final result](M17_G17_FINAL_SOURCE_LOCKED_RESULT_20261116.md).
