# G8 Independent Evidence Reconciliation

**State:** Procedure available. D-006 and G8 acceptance pending. No G8 run has been executed for this implementation.

The local [G8 runner](../scripts/verify-m8-local.sh) intentionally records `G8-S01` and `G8-S25` as blocked until D-006 is explicitly decided and the raw source-locked evidence is reviewed independently. Do not edit `evidence/g8-scenarios.json`, stage logs, source hashes or original archived evidence to change gate disposition.

After a complete E:-backed G8 run, its cleanup produces the source-specific `archives/g8-<sha>-evidence.tar.gz` and `archives/g8-<sha>-manifest.sha256`. Only when all technical cases S02-S24 are PASS and all technical stages have exit 0 should an independent review record be prepared.

The review record is a separate JSON file, placed outside the raw evidence directory, containing:

```json
{
  "sourceSha": "<40-digit audited git SHA>",
  "scenarioReportSha256": "<SHA-256 of evidence/g8-scenarios.json>",
  "manifestSha256": "<SHA-256 of archived manifest>",
  "evidenceArchiveSha256": "<SHA-256 of archived evidence tar.gz>",
  "d006Decision": "accepted",
  "d006DecisionRef": "<actual GitHub decision-comment or issue URL>",
  "independentReview": "complete",
  "independentReviewRef": "<actual independent review URL>",
  "reviewedBy": "<independent reviewer>",
  "reviewedScenarioIds": ["G8-S01", "G8-S02", "...", "G8-S25"]
}
```

The example above intentionally uses placeholders, not assertions of completed approval. The actual `reviewedScenarioIds` array must contain the exact 25 distinct IDs in order. To reconcile, run from the repository checkout with Node installed:

```bash
node scripts/reconcile-g8-review.mjs "E:\\HarborOps-Verification\\m8" "E:\\HarborOps-Verification\\review\\g8-review.json"
```

The utility checks source identity, the full 25-case order, technical passes, stage source/exits, archived manifest and evidence hashes, explicit D-006 decision and independent review references. It writes a *new* `archives/g8-<sha>-review-reconciliation.json` with exclusive creation. It never overwrites the original run or implies acceptance. Reference authenticity and independent reviewer qualifications still need human verification. A reconciled review is **not** G8 approval: explicit acceptance remains separate before merging PR #14 or starting dependent M9 work.

GitHub Actions remain disabled. All local evidence and review inputs stay E:-backed. No permanent project-specific writes on C:.
