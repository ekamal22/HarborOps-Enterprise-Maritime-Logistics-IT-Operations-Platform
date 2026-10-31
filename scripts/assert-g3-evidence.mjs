#!/usr/bin/env node
// Correlate executed scenarios with named tests. This does not grant G3 acceptance.
import fs from "node:fs";
import path from "node:path";

const root = process.argv[2];
if (!root || !fs.existsSync(root)) throw new Error("Evidence root required");
function paths(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? paths(full) : [full];
  });
}
const trxFolder = path.join(root, "trx");
if (!fs.existsSync(trxFolder)) throw new Error("No TRX directory");
const files = paths(trxFolder).filter(name => name.endsWith(".trx"));
if (!files.length) throw new Error("No executed .NET TRX files");
const tests = new Map();
for (const name of files) {
  const xml = fs.readFileSync(name, "utf8");
  for (const [tag] of xml.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const test = /\btestName="([^"]+)"/.exec(tag)?.[1];
    const status = /\boutcome="([^"]+)"/.exec(tag)?.[1];
    if (!test || !status) continue;
    const prior = tests.get(test) ?? new Set();
    prior.add(status);
    tests.set(test, prior);
  }
}
const success = fragment => {
  const matches = [...tests].filter(([test]) => test.includes(fragment));
  return matches.length > 0 && matches.every(([, statuses]) =>
    statuses.has("Passed") && !statuses.has("Failed") && !statuses.has("NotExecuted"));
};
const reportFile = process.env.HARBOROPS_G3_JSON_REPORT ??
  path.join(root, "playwright-g3.json");
if (!fs.existsSync(reportFile)) throw new Error("Missing Playwright JSON results");
const report = JSON.parse(fs.readFileSync(reportFile, "utf8"));
const browserTests = [];
function walk(suites) {
  for (const suite of suites ?? []) {
    for (const spec of suite.specs ?? [])
      for (const item of spec.tests ?? [])
        browserTests.push({ title: spec.title, engine: item.projectName,
          results: (item.results ?? []).map(result => result.status) });
    walk(suite.suites);
  }
}
walk(report.suites);
const browserSuccess = title => ["chromium", "firefox", "webkit"].every(engine =>
  browserTests.some(item => item.engine === engine && item.title.includes(title) &&
    item.results.includes("passed") && !item.results.some(s =>
      s === "failed" || s === "skipped" || s === "timedOut")));
const stageSuccess = stage => {
  const name = path.join(root, "g3-" + stage + ".log");
  return fs.existsSync(name) && fs.statSync(name).size > 0;
};
const scenarios = [
  ["S01", ["ReferencePolicyTests.NormalizesLocalCodes", "ReferencePolicyTests.RejectsInvalidLocodes"]],
  ["S02", ["ReferencePolicyTests.AcceptsChecksumValidImo", "ReferencePolicyTests.RejectsInvalidMmsi"]],
  ["S03", ["ReferencePolicyTests.ReferencePermissions", "ReferenceAdversarialHttpTests.SupportAndAuditor"]],
  ["S04", ["MigrationSnapshotTests.ModelSnapshot", "ReferenceHistoricalUpgradeTests.M2Seed"]],
  ["S05", ["ReferenceHistoricalUpgradeTests.M2Seed"]],
  ["S06", ["ReferenceHistoricalUpgradeTests.DirectSql"]],
  ["S07", [], [], ["fixtures"]],
  ["S08", ["ReferenceBoundaryMatrixTests.EveryReferenceRouteRejectsAnonymous", "ReferenceSessionBoundaryTests.EveryReferenceVerb"]],
  ["S09", ["ReferenceBoundaryMatrixTests.MissingSelection"]],
  ["S10", ["ReferenceBoundaryMatrixTests.EveryReferenceRouteRejectsAnonymous"]],
  ["S11", ["ReferenceAdversarialHttpTests.ForeignPortAndVessel"]],
  ["S12", ["ReferenceAdversarialHttpTests.ForeignPortAndVessel"]],
  ["S13", ["ReferenceAdversarialHttpTests.SupportAndAuditor"]],
  ["S14", ["ReferenceAdversarialHttpTests.SecurityDowngrade", "ReferenceBoundaryMatrixTests.SuspendedMembership", "ReferenceSessionBoundaryTests.EveryReferenceVerb"]],
  ["S15", ["ReferenceAdditionalContractTests.MalformedBodies", "ReferenceSessionBoundaryTests.ForeignOrigin"]],
  ["S16", ["ReferenceDataHttpTests.ScopedPort"]],
  ["S17", ["ReferenceAdditionalContractTests.VesselFull"]],
  ["S18", ["ReferenceAdditionalContractTests.VesselFull", "ReferenceDataHttpTests.MigrationConstraints", "ReferenceConcurrencyTests.ParallelDuplicateCreates", "ReferenceConcurrencyTests.IdenticalExternalIdentifiers"]],
  ["S19", ["ReferenceBoundaryMatrixTests.QueryBounds"]],
  ["S20", ["ReferenceDataHttpTests.CompetingUpdates", "ReferenceConcurrencyTests.ParallelUpdateAndArchive"]],
  ["S21", ["ReferenceAdditionalContractTests.VesselFull"]],
  ["S22", [], ["Manager port and vessel", "Analyst can read"]],
  ["S23", [], ["delayed old-tenant", "reference list clears", "logging out during"]],
  ["S24", ["OrganizationBoundaryMatrixTests.MissingAndForeignSelections"], ["Organization switch discards"], ["dotnet", "session", "recovery"]],
  ["S25", [], [], ["deps", "web", "docs", "stack", "scan"]]
];
const rows = scenarios.map(([id, unit, browser = [], stages = []]) => {
  const missing = [
    ...unit.filter(v => !success(v)).map(v => "TRX:" + v),
    ...browser.filter(v => !browserSuccess(v)).map(v => "Browser:" + v),
    ...stages.filter(v => !stageSuccess(v)).map(v => "Stage:" + v)
  ];
  return { id: "G3-" + id, missing };
});
const shaFile = path.join(root, "m3-source-sha.txt");
const sha = fs.existsSync(shaFile) ? fs.readFileSync(shaFile, "utf8").trim() : "unrecorded";
const md = [
  "# G3 Executed Evidence Index", "",
  "Source revision: " + sha, "",
  "This checks successful named results and stage-log presence, not full scenario acceptance.",
  "", "| Scenario | Named evidence | Missing |", "| --- | --- | --- |",
  ...rows.map(x => "| " + x.id + " | " + (x.missing.length ? "INCOMPLETE" : "PRESENT") +
    " | " + (x.missing.join(", ") || "None") + " |"),
  "", "G3 remains open until all mandatory assertions and residual risks receive review."
].join("\n");
fs.writeFileSync(path.join(root, "G3_EXECUTION_INDEX.md"), md + "\n");
console.log(md);
if (rows.some(x => x.missing.length)) process.exitCode = 1;
