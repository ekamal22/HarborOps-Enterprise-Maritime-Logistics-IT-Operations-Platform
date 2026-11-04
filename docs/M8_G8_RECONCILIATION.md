# G8 Independent Evidence Reconciliation

**State:** Procedure available. D-006 and G8 acceptance pending. No G8 run has been executed for this implementation.

The local [G8 runner](../scripts/verify-m8-local.sh) intentionally records `G8-S01` and `G8-S25` as blocked until D-006 is explicitly decided and the raw source-locked evidence is reviewed independently. Do not edit `evidence/g8-scenarios.json`, stage logs, source hashes or original archived evidence to change gate disposition.

Every E:-backed G8 verification root is single-use. A run has a unique ID of the form `YYYYMMDDTHHMMSSZ-<16 hex characters>` stored in `evidence/run-id.txt`. The final archived files live in `archives/<run-id>/g8-<sha>-<run-id>-evidence.tar.gz` and `archives/<run-id>/g8-<sha>-<run-id>-manifest.sha256`. The runner refuses to reuse an evidence root and checks extracted archive files against the exact manifest and private canaries before allowing the archive to be shared. Only when all technical cases S02-S24 are PASS, all technical stages have exit 0 and the final archive-safety check passed should an independent review record be prepared.

The review record is a separate JSON file, placed outside the raw evidence directory, containing:

```json
{
  "sourceSha": "<40-digit audited git SHA>",
  "runId": "<run ID from evidence/run-id.txt>",
  "technicalStatus": "PASS",
  "scenarioReportSha256": "<SHA-256 of evidence/g8-scenarios.json>",
  "manifestSha256": "<SHA-256 of archived manifest>",
  "evidenceArchiveSha256": "<SHA-256 of archived evidence tar.gz>",
  "finalizationSha256": "<SHA-256 of archives/<run-id>/g8-finalization-status.json>",
  "archiveSafetySha256": "<SHA-256 of archives/<run-id>/archive-safety.log>",
  "d006Decision": "accepted",
  "d006DecisionRef": "<actual GitHub decision-comment or issue URL>",
  "independentReview": "complete",
  "independentReviewRef": "<actual independent review URL>",
  "reviewedBy": "<independent reviewer>",
  "reviewedScenarioIds": ["G8-S01", "G8-S02", "...", "G8-S25"]
}
```

The wrapper uses exit code 1 for a technical or archive-finalization failure and exit code 2 for a technically clean run with a sanitized archive and formal acceptance still pending. The separate `archives/<run-id>/g8-finalization-status.json` must record `finalizationStatus: PASS`, `archiveStatus: PASS`, `technicalStatus: PASS`, and `expectedWrapperExitCode: 2` before independent reconciliation. `evidence/g8-technical-status.json` and `evidence/g8-formal-gate-status.json` record these as separate states. Neither exit code constitutes acceptance.

The example above intentionally uses placeholders, not assertions of completed approval. The actual `reviewedScenarioIds` array must contain the exact 25 distinct IDs in order. To reconcile, run from the repository checkout with Node installed:

```bash
node scripts/reconcile-g8-review.mjs "E:\\HarborOps-Verification\\g8-attempt-01\\m8" "E:\\HarborOps-Verification\\review\\g8-review.json"
```

The utility checks source identity, the selected immutable run ID, independently recorded technical status, the full 25-case order, technical passes, stage source/exits, final archive safety, archived manifest, evidence, finalization and archive-safety hashes, explicit D-006 decision and independent review references. It writes a *new* `archives/<run-id>/g8-<sha>-<run-id>-review-reconciliation.json` with exclusive creation. It never overwrites the original run or implies acceptance. Reference authenticity and independent reviewer qualifications still need human verification. A reconciled review is **not** G8 approval: explicit acceptance remains separate before merging PR #14 or starting dependent M9 work.

The G8 run root must be unique per attempt and end with `/m8`. For example, `E:\\HarborOps-Verification\\g8-attempt-01\\m8` is one isolated root. Subsequent attempts use a new parent folder, even when repeating the same source SHA. Final logs and teardown are scanned before an archive is promoted; the private tarball is independently extracted and its contents checked against the run-specific manifest and credential canaries. Do not share any quarantined archive.

GitHub Actions remain disabled. All local evidence and review inputs stay E:-backed. No permanent project-specific writes on C:.
