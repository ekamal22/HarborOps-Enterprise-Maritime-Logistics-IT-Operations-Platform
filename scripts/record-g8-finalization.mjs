#!/usr/bin/env node
// The final artifact outcome is separate from the original immutable run.
import assert from "node:assert/strict";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const [evidence, archiveDir, stageCode, archiveCode] = process.argv.slice(2);
assert.ok(evidence && archiveDir);
const technical = JSON.parse(readFileSync(join(evidence, "g8-technical-status.json"), "utf8"));
const sourceSha = readFileSync(join(evidence, "source-sha.txt"), "utf8").trim();
const runId = readFileSync(join(evidence, "run-id.txt"), "utf8").trim();
assert.equal(technical.sourceSha, sourceSha);
assert.equal(technical.runId, runId);
const raw = join(archiveDir, "g8-" + sourceSha + "-" + runId + "-evidence.tar.gz");
const archived = Number(archiveCode) === 0 && existsSync(raw);
const valid = Number(stageCode) === 0 && technical.status === "PASS" && archived;
const result = {
  sourceSha, runId,
  technicalStatus: technical.status,
  archiveStatus: archived ? "PASS" : "FAILED",
  finalizationStatus: valid ? "PASS" : "FAILED",
  formalGateStatus: "ACCEPTANCE_PENDING",
  expectedWrapperExitCode: valid ? 2 : 1,
  generatedAtUtc: new Date().toISOString()
};
writeFileSync(join(archiveDir, "g8-finalization-status.json"),
  JSON.stringify(result, null, 2) + "\n", { flag:"wx" });
console.log("G8 finalization:", result.finalizationStatus,
  "formal acceptance pending");
