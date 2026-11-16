# G17 Diagnostic State Hash-List Finding

**Recorded:** 2026-11-16
**Status:** Open prospective evidence-producer correction. Original artifacts remain immutable.

The independent review found that `diag-hashes.txt` agrees with seven of the eight diagnostic files in each of r1c, r2 and r3. The single mismatch in every run is `diag-state.json`. The producer, `docs/m17-s25-p09-controlled-protocol-20261115/diagnostics.ps1`, computes the list and **then** writes the final `stopped_utc` timestamp into `diag-state.json`. The internal hash therefore describes the state file before its final write.

The outer independent-review bundle inventory and archived file bytes agree. The mismatch is an evidence-producer ordering defect, **not established alteration of raw latency samples, campaign stages, or the three PASS verdicts**.

**Prospective fix:** Write the completed diagnostic state before hashing it; exclude the hash list itself from self-referential calculations; add an exact-byte regression test. Do not edit the original three internal hash lists or reissue old manifests as though they were unchanged. Freeze and verify the corrected producer with the next applicable source-locked candidate.

This finding does not change P09 numerical budgets or the qualified P09 decision, and does not itself sign G17-S25.

[Policy decision](M17_G17_P09_QUALIFIED_PASS_DECISION_20261116.md) | [Original controlled results](M17_G17_S25_P09_CONTROLLED_VERIFICATION_RESULTS_20261115.md)
