#!/usr/bin/env node
// Separate, source-locked G8 review reconciliation. Never changes the raw run.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { join, resolve } from "node:path";

const [rootArg, reviewArg] = process.argv.slice(2);
assert.ok(rootArg && /^(?:[Ee]:[\\/]|\/e\/)/.test(rootArg),
  "An E:-backed G8 verification root is required");
assert.ok(reviewArg, "A separate reviewed decision record is required");
const root = resolve(rootArg);
const reviewPath = resolve(reviewArg);
assert.ok(!reviewPath.startsWith(join(root, "evidence") + "/") &&
  !reviewPath.startsWith(join(root, "evidence") + "\\"),
  "Decision and independent review must be separate from immutable raw evidence");

const evidencePath = join(root, "evidence");
const file = name => readFileSync(join(evidencePath, name));
const sha = file("source-sha.txt").toString("utf8").trim();
assert.match(sha, /^[a-f0-9]{40}$/);
const runId = file("run-id.txt").toString("utf8").trim();
assert.match(runId, /^\d{8}T\d{6}Z-[0-9a-f]{16}$/, "Unique G8 run ID required");
const reportBytes = file("g8-scenarios.json");
const report = JSON.parse(reportBytes.toString("utf8"));
assert.equal(report.sourceSha, sha);
assert.equal(report.scenarios?.length, 25);
const expectedIds = Array.from({ length: 25 },
  (_, i) => "G8-S" + String(i + 1).padStart(2, "0"));
assert.deepEqual(report.scenarios.map(x => x.id), expectedIds);

const technical = report.scenarios.filter(x =>
  x.id !== "G8-S01" && x.id !== "G8-S25");
assert.ok(technical.every(x => x.status === "PASS"),
  "Incomplete technical cases cannot be reconciled as a reviewed G8 gate");
assert.ok(report.scenarios.find(x => x.id === "G8-S01").status === "BLOCKED");
assert.ok(report.scenarios.find(x => x.id === "G8-S25").status === "BLOCKED");

const technicalState = JSON.parse(file("g8-technical-status.json").toString("utf8"));
assert.equal(technicalState.runId, runId);
assert.equal(technicalState.sourceSha, sha);
assert.equal(technicalState.status, "PASS", "All executable technical stages must pass");
assert.equal(technicalState.formalGateStatus, "ACCEPTANCE_PENDING");
const formal = JSON.parse(file("g8-formal-gate-status.json").toString("utf8"));
assert.equal(formal.runId, runId);
assert.equal(formal.sourceSha, sha);
assert.equal(formal.status, "ACCEPTANCE_PENDING");
assert.equal(formal.accepted, false);
const stageRows = file("stages.tsv").toString("utf8").split("\n").filter(Boolean);
assert.ok(stageRows.length >= 18, "Complete technical stage record required");
assert.ok(stageRows.every(row => {
  const fields = row.split("\t");
  return fields.length === 4 && fields[1] === sha && fields[2] === "0";
}), "Stage status or source SHA mismatch");

const archiveDir = join(root, "archives", runId);
const prefix = "g8-" + sha + "-" + runId;
const archive = join(archiveDir, prefix + "-evidence.tar.gz");
const manifest = join(archiveDir, prefix + "-manifest.sha256");
const safetyLog = join(archiveDir, "archive-safety.log");
assert.ok(existsSync(safetyLog) &&
  readFileSync(safetyLog,"utf8").includes("G8 archive and manifest integrity PASS"),
  "Post-package archive safety proof required");
assert.ok(existsSync(archive) && existsSync(manifest),
  "Archived raw evidence and hashes are required");
const digest = bytes => createHash("sha256").update(bytes).digest("hex");
const actual = {
  sourceSha: sha,
  scenarioReportSha256: digest(reportBytes),
  manifestSha256: digest(readFileSync(manifest)),
  evidenceArchiveSha256: digest(readFileSync(archive))
};

const review = JSON.parse(readFileSync(reviewPath, "utf8"));
assert.equal(review.runId, runId, "Independent review must select this exact run");
assert.equal(review.technicalStatus, "PASS", "Independent review requires passing technical evidence");
for (const key of Object.keys(actual))
  assert.equal(review[key], actual[key], "Source-locked review mismatch: " + key);
assert.equal(review.d006Decision, "accepted", "Explicit D-006 approval required");
assert.equal(review.independentReview, "complete", "Independent review must be recorded");
assert.deepEqual(review.reviewedScenarioIds, expectedIds);
const expectedRef = value => typeof value === "string" &&
  /^https:\/\/github\.com\/[^\s]+\/(?:pull|issues)\/\d+(?:#\S+)?$/.test(value);
assert.ok(expectedRef(review.d006DecisionRef),
  "A concrete D-006 approval reference is required");
assert.ok(expectedRef(review.independentReviewRef),
  "A separate independent review reference is required");
assert.notEqual(review.d006DecisionRef, review.independentReviewRef,
  "Decision and independent review require separate references");
assert.ok(typeof review.reviewedBy === "string" && review.reviewedBy.trim().length >= 3,
  "An identifiable independent reviewer is required");
const record = {
  sourceSha: sha,
  runId,
  recordedAtUtc: new Date().toISOString(),
  technicalCaseCount: technical.length,
  reviewedScenarioIds: expectedIds,
  decisionRef: review.d006DecisionRef,
  independentReviewRef: review.independentReviewRef,
  reviewedBy: review.reviewedBy,
  ...actual,
  disposition: "Review references reconciled. Explicit G8 acceptance pending."
};
const outputDir = archiveDir;
mkdirSync(outputDir, { recursive:true });
const output = join(outputDir, prefix + "-review-reconciliation.json");
assert.ok(!existsSync(output), "Preserve previous reconciliation; do not overwrite");
writeFileSync(output, JSON.stringify(record, null, 2) + "\n", { flag:"wx" });
console.log("G8 source-locked review references reconciled: " + output);
console.log("G8 acceptance pending; this command does not approve or merge the gate");
