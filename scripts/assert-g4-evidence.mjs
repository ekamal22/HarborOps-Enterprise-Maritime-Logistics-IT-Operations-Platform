#!/usr/bin/env node
// G4 evidence provenance and named test checks. This does not accept the milestone.
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const root = process.argv[2];
if (!root || !fs.existsSync(root)) throw new Error("Run-specific evidence directory required");
const read = file => fs.readFileSync(path.join(root, file), "utf8");
const source = read("g4-source-sha.txt").trim();
const current = execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim();
if (!/^[0-9a-f]{40}$/.test(source) || source !== current ||
    read("m4-source-sha.txt").trim() !== source)
  throw new Error("G4 evidence source does not match the current implementation");
const started = Date.parse(read("g4-start-utc.txt").trim());
if (!Number.isFinite(started) || started > Date.now() + 60000 ||
    Date.now() - started > 24 * 60 * 60 * 1000)
  throw new Error("Invalid or stale G4 run start timestamp");
const existsFresh = file => {
  const name = path.join(root, file);
  return fs.existsSync(name) && fs.statSync(name).isFile() &&
    fs.statSync(name).size > 0 && fs.statSync(name).mtimeMs >= started - 2000;
};
if (existsFresh("g4-aborted.txt"))
  throw new Error("G4 run has an abort marker");
const expectedStages = [
  "preflight", "postgres-image", "dotnet", "deps", "web", "docs", "stack",
  "accounts", "organizations", "session", "setup-fixtures", "fixtures", "m4-fixtures",
  "browsers", "reference-outage", "maritime-outage", "recovery", "scan"
];
const stages = read("g4-stages.tsv").trim().split(/\r?\n/).map(line => line.split("\t"));
for (const name of expectedStages) {
  const matching = stages.filter(row => row[0] === name);
  if (matching.length !== 1 || matching[0][1] !== source || matching[0][2] !== "0" ||
      !existsFresh("g4-" + name + ".log"))
    throw new Error("Missing, failed, stale or wrong-source stage: " + name);
}
const trxRoot = path.join(root, "trx");
if (!fs.existsSync(trxRoot)) throw new Error("Missing .NET TRX directory");
function recurse(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(item => {
    const name = path.join(dir, item.name);
    return item.isDirectory() ? recurse(name) : [name];
  });
}
const trxFiles = recurse(trxRoot).filter(file =>
  file.endsWith(".trx") && fs.statSync(file).mtimeMs >= started - 2000);
if (!trxFiles.length) throw new Error("No fresh .NET test results");
const cases = new Map();
for (const file of trxFiles) {
  const xml = fs.readFileSync(file, "utf8");
  if (!xml.includes("<TestRun")) throw new Error("Malformed TRX: " + path.basename(file));
  for (const [tag] of xml.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const name = /\btestName="([^"]+)"/.exec(tag)?.[1];
    const outcome = /\boutcome="([^"]+)"/.exec(tag)?.[1];
    if (!name || !outcome) throw new Error("Incomplete TRX result");
    const all = cases.get(name) ?? [];
    all.push(outcome);
    cases.set(name, all);
  }
}
const unit = name => {
  const matches = [...cases.entries()].filter(([test]) =>
    test.endsWith("." + name) || test.includes("." + name + "("));
  return matches.length > 0 && matches.every(([, outcomes]) =>
    outcomes.length > 0 && outcomes.every(v => v === "Passed"));
};
const browserRoot = path.join(root, "playwright");
if (!fs.existsSync(browserRoot)) throw new Error("Missing per-spec browser reports");
const files = fs.readdirSync(browserRoot).filter(file => file.endsWith(".json"));
if (!files.length) throw new Error("No browser reports");
const browserCases = [];
for (const file of files) {
  const location = path.join(browserRoot, file);
  if (fs.statSync(location).mtimeMs < started - 2000)
    throw new Error("Stale browser report: " + file);
  const report = JSON.parse(fs.readFileSync(location, "utf8"));
  if ((report.stats?.unexpected ?? 0) !== 0 ||
      (report.stats?.interrupted ?? 0) !== 0 ||
      (report.stats?.skipped ?? 0) !== 0)
    throw new Error("Browser report failed or skipped tests: " + file);
  function walk(suites) {
    for (const suite of suites ?? []) {
      for (const spec of suite.specs ?? []) {
        for (const test of spec.tests ?? []) {
          browserCases.push({ title: spec.title, engine: test.projectName,
            expectedStatus: test.expectedStatus,
            results: (test.results ?? []).map(result => result.status) });
        }
      }
      walk(suite.suites);
    }
  }
  walk(report.suites);
}
const browser = name => ["chromium", "firefox", "webkit"].every(engine =>
  browserCases.some(row => row.engine === engine && row.title === name &&
    row.expectedStatus !== "skipped" && row.results.length > 0 &&
    row.results.every(result => result === "passed")));
const logOk = name => existsFresh(name) &&
  /\bPassed:|\bPassed!/.test(read(name)) && !/\bFailed:\s*[1-9]|\bSkipped:\s*[1-9]/.test(read(name));
const scanOk = ["postgres", "api", "web"].every(name => {
  const file = path.join(root, "trivy", name + ".json");
  if (!fs.existsSync(file) || fs.statSync(file).mtimeMs < started - 2000) return false;
  try {
    const scan = JSON.parse(fs.readFileSync(file, "utf8"));
    return Array.isArray(scan.Results) &&
      scan.Results.every(result => !(result.Vulnerabilities ?? []).some(v =>
        v.Severity === "HIGH" || v.Severity === "CRITICAL"));
  } catch { return false; }
});
const scenarios = [
  ["S01",["MaritimePolicyTests.KnownIsoChecksumVectorsAreAccepted","MaritimePolicyTests.IncorrectIsoIdentifiersAreRejected"]],
  ["S02",["MaritimePolicyTests.SyntheticIdentifiersCannotMasqueradeAsIso","MaritimePolicyTests.EquipmentCatalogIsClosed"],["M4 Manager container lifecycle retains synthetic identity"]],
  ["S03",["MaritimePolicyTests.VoyagesRequireConsistentNullableWindowsAndProgress","MaritimePolicyTests.TransitionGraphIsClosed","MaritimePolicyTests.UtcInputsRequireExplicitZeroOffset"]],
  ["S04",["MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel","MigrationSnapshotTests.Migrations_AreDiscoveredInOrder","MaritimeMigrationTests.SeededM3HistorySurvivesM4UpgradeRollbackAndReapply"]],
  ["S05",["MaritimeMigrationTests.SeededM3HistorySurvivesM4UpgradeRollbackAndReapply"]],
  ["S06",["MaritimeMigrationTests.DirectSqlRejectsCrossTenantVoyagesBadIdentifiersAndMutatedIdentity"]],
  ["S07",[],[],["fixtures","m4-fixtures"]],
  ["S08",["ReferenceSessionBoundaryTests.EveryReferenceVerbRejectsInvalidatedSession"]],
  ["S09",["MaritimeBoundaryTests.EveryMaritimeVerbRequiresSelectedOrganization"]],
  ["S10",["MaritimeHttpTests.ForeignParentAndForeignRecordIdsDoNotLeak"]],
  ["S11",["MaritimeConcurrencyTests.ExistingForeignVoyageUuidCannotBeReadOrMutated"]],
  ["S12",["MaritimeHttpTests.ForeignParentAndForeignRecordIdsDoNotLeak"]],
  ["S13",["MaritimePolicyTests.RoleGrantsStayWithinBusinessPermissions","MaritimeHttpTests.ManagerCreatesBothKindsAndAnalystReadsWithoutWritePermission","MaritimeBoundaryTests.NonBusinessRolesReceiveNoImplicitM4Access"]],
  ["S14",["MaritimeBoundaryTests.NonBusinessRolesReceiveNoImplicitM4Access"],["M4 real Manager role revocation clears browser management controls"],["maritime-outage"]],
  ["S15",["MaritimeBoundaryTests.InvalidFiltersAndMalformedPayloadsFailSafely"]],
  ["S16",["MaritimeHttpTests.VoyageProgressAndArchiveRestoreRequireExpectedVersion"],["M4 Manager voyage progress and archive restoration"]],
  ["S17",["MaritimeHttpTests.ManagerCreatesBothKindsAndAnalystReadsWithoutWritePermission"],["M4 Manager container lifecycle retains synthetic identity"]],
  ["S18",["MaritimeHttpTests.InvalidChecksumAndDuplicateIdentifierRejectWithoutPartialWrites","MaritimeConcurrencyTests.DuplicateContainerCreateRacePersistsOneIdentifier"]],
  ["S19",["MaritimeBoundaryTests.InvalidFiltersAndMalformedPayloadsFailSafely"],["M4 tenant switch drops former tenant maritime content"]],
  ["S20",["MaritimeConcurrencyTests.ParentArchivalAndVoyageCreationSerialize","MaritimeBoundaryTests.CompetingContainerUpdatesAreVersionGuarded"]],
  ["S21",["MaritimeHttpTests.VoyageProgressAndArchiveRestoreRequireExpectedVersion"]],
  ["S22",[],["M4 Manager container lifecycle retains synthetic identity","M4 Manager voyage progress and archive restoration","M4 Analyst sees maritime records without mutation controls"]],
  ["S23",[],["M4 delayed old-tenant container response never repaints new organization","M4 reference rows clear on failed request and recover without cache","M4 real Manager role revocation clears browser management controls"]],
  ["S24",["MaritimeMigrationTests.SeededM3HistorySurvivesM4UpgradeRollbackAndReapply","ReferenceHistoricalUpgradeTests.M2SeedAndAuthenticatedSessionSurviveM3UpgradeAndRepeat"],[],["reference-outage","recovery"]],
  ["S25",["MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel"],[],["postgres-image","dotnet","deps","web","docs","stack","scan"]]
];
// A complete implementation cannot pass G4 while D-003 is still a proposal.
const decisionDoc = fs.readFileSync("docs/DECISIONS.md", "utf8");
const d003Approved = /D-003[\s\S]*?\*\*Status:\*\*\s*Accepted/m.test(decisionDoc) &&
  !/D-003[\s\S]{0,400}decision remains pending/i.test(decisionDoc);
const rows = scenarios.map(([id, required, browserNames = [], stageNames = []]) => {
  const missing = [
    ...required.filter(name => !unit(name)).map(name => "TRX:" + name),
    ...browserNames.filter(name => !browser(name)).map(name => "Browser:" + name),
    ...stageNames.filter(name => !expectedStages.includes(name)).map(name => "Stage:" + name)
  ];
  return { id: "G4-" + id, missing };
});
if (!d003Approved) rows.find(row => row.id === "G4-S01").missing.push("D-003 acceptance pending");
if (!logOk("integration-fresh.log") || !logOk("integration-repeat.log")) {
  for (const row of rows) row.missing.push("Required fresh and repeat integration logs");
}
if (!scanOk) rows.find(row => row.id === "G4-S25").missing.push("Trivy high/critical scan evidence");
const md = [
  "# G4 Execution Evidence Index", "",
  "Source revision: " + source, "",
  "This index validates fresh run provenance, mandatory stages, named test executions and scanner output.",
  "It does not grant G4 approval. Every requirement still needs independent evidence review.",
  "", "| Scenario | Evidence check | Missing |", "| --- | --- | --- |",
  ...rows.map(row => "| " + row.id + " | " +
    (row.missing.length ? "INCOMPLETE" : "EVIDENCE PRESENT, REVIEW REQUIRED") +
    " | " + (row.missing.join(", ") || "None") + " |"),
  "", "G4 remains open until the completed requirements matrix is independently reviewed."
].join("\n");
fs.writeFileSync(path.join(root, "G4_EXECUTION_INDEX.md"), md + "\n");
console.log(md);
if (rows.some(row => row.missing.length)) process.exitCode = 1;
