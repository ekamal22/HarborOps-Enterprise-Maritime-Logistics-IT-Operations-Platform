# G16 findings register

Findings are numbered in order of discovery. Severity reflects impact on the M16 candidate or on verification. Root causes are marked confirmed or hypothesis.

## G16-F001: Generated execution graph is stale; the G16 runner fails at its second stage

- **Severity:** Medium (documentation defect that blocks the whole scripted campaign; Low as a product defect)
- **Component:** `docs/EXECUTION_GRAPH.md`, `scripts/verify-m16-local.sh` stage `docs`
- **Observed:** `node scripts/render-execution-graph.mjs --check` exits 1 with "Execution graph view is stale". The runner stops after `preflight` (exit 0) and `docs` (exit 1); attempt 01 ran no further stage.
- **Expected:** The committed generated view matches `docs/execution-graph.json`, so the runner proceeds.
- **Reproduction:** from a clean checkout of `ef08d7c`, `node scripts/render-execution-graph.mjs --check`.
- **Evidence:** `E:\HarborOps-Verification\evidence\m16\evidence\docs.log`, `stages.tsv`.
- **Root cause (confirmed):** Commit `bb5015d` updated the M16 entries in `docs/execution-graph.json` (architecture decisions line and G16 acceptance evidence line) without regenerating `docs/EXECUTION_GRAPH.md`. A regeneration in a scratch copy changes exactly two lines (789 and 818) and nothing else.
- **Impact:** Verification completeness. The runner cannot reach any technical stage from the committed source. `check-planning-docs.mjs` passes separately on a clean `git archive` of the revision (25 milestones, 126 graph nodes, 107 Markdown files).
- **Recommended correction:** Regenerate and commit `docs/EXECUTION_GRAPH.md` on the implementation branch, then rerun on the new revision. Consider making the docs stage run the planning check even if the graph check fails.
- **Retest:** `render-execution-graph.mjs --check` exits 0 on the corrected revision; the full runner proceeds past `docs`.
- **Handling in this campaign:** Source stays frozen. Attempt 02 uses a derived runner that tolerates this one stage failure so the remaining stages can run; its evidence is labelled derived.
