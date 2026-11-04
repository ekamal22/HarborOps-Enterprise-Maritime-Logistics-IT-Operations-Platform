#!/usr/bin/env node
// Record technical outcome independently from the formal decision gate.
import assert from "node:assert/strict";
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const [evidence, runnerRc] = process.argv.slice(2);
assert.ok(evidence);
const report = JSON.parse(readFileSync(join(evidence, "g8-scenarios.json"), "utf8"));
const staged = readFileSync(join(evidence, "stages.tsv"), "utf8")
  .split("\n").filter(Boolean).map(line => {
    const [name, sha, code] = line.split("\t");
    return { name, sha, code: Number(code) };
  });
const needed = ["preflight","script-syntax","network-contract","documentation","tracking-contract",
  "postgres-image","build","dotnet-regressions","security-mutations",
  "nuget-advisories","web-build","npm-advisories","compose","accounts",
  "organizations","source-fixture","source-cli-negative","ingress-before",
  "browser-fixtures","browsers","source-outage-recovery","image-scans",
  "evidence-safety","evidence-summary"];
const healthyStages = needed.every(name => staged.some(x =>
  x.name === name && x.sha === report.sourceSha && x.code === 0));
const technical = report.scenarios.filter(x =>
  !["G8-S01", "G8-S25"].includes(x.id));
const technicalPass = Number(runnerRc) === 0 && healthyStages &&
  technical.length === 23 && technical.every(x => x.status === "PASS");
const result = {
  sourceSha: report.sourceSha,
  runId: readFileSync(join(evidence, "run-id.txt"), "utf8").trim(),
  status: technicalPass ? "PASS" : "FAILED",
  technicalScenariosPassed: technical.filter(x => x.status === "PASS").length,
  requiredTechnicalScenarios: 23,
  technicalStagesComplete: healthyStages,
  formalGateStatus: "ACCEPTANCE_PENDING",
  decisionRequired: ["D-006", "independent evidence review", "G8 acceptance"]
};
writeFileSync(join(evidence, "g8-technical-status.json"), JSON.stringify(result, null, 2)+"\n", {flag:"wx"});
writeFileSync(join(evidence, "g8-formal-gate-status.json"), JSON.stringify({
  sourceSha: report.sourceSha,runId:result.runId,
  status:"ACCEPTANCE_PENDING",technicalStatus:result.status,
  accepted:false
}, null, 2)+"\n",{flag:"wx"});
console.log("G8 technical status:",result.status,"formal gate:",result.formalGateStatus);
