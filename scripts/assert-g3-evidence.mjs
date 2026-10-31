#!/usr/bin/env node
// G3 evidence provenance and named test checks. This does not accept the milestone.
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const root = process.argv[2];
if (!root || !fs.existsSync(root)) throw new Error("Run-specific evidence directory required");
const read = file => fs.readFileSync(path.join(root, file), "utf8");
const source = read("g3-source-sha.txt").trim();
const current = execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim();
if (!/^[0-9a-f]{40}$/.test(source) || source !== current ||
    read("m3-source-sha.txt").trim() !== source)
  throw new Error("G3 evidence source does not match the current implementation");
const started = Date.parse(read("g3-start-utc.txt").trim());
if (!Number.isFinite(started) || started > Date.now() + 60000 ||
    Date.now() - started > 24 * 60 * 60 * 1000)
  throw new Error("Invalid or stale G3 run start timestamp");
const existsFresh = file => {
  const name = path.join(root, file);
  return fs.existsSync(name) && fs.statSync(name).isFile() &&
    fs.statSync(name).size > 0 && fs.statSync(name).mtimeMs >= started - 2000;
};
if (existsFresh("g3-aborted.txt"))
  throw new Error("G3 run has an abort marker");
const expectedStages = [
  "preflight", "postgres-image", "dotnet", "deps", "web", "docs", "stack",
  "accounts", "organizations", "session", "setup-fixtures", "fixtures",
  "browsers", "reference-outage", "recovery", "scan"
];
const stages = read("g3-stages.tsv").trim().split(/\r?\n/).map(line => line.split("\t"));
for (const name of expectedStages) {
  const matching = stages.filter(row => row[0] === name);
  if (matching.length !== 1 || matching[0][1] !== source || matching[0][2] !== "0" ||
      !existsFresh("g3-" + name + ".log"))
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
const browserFile = process.env.HARBOROPS_G3_JSON_REPORT || path.join(root, "playwright-g3.json");
if (!fs.existsSync(browserFile) || fs.statSync(browserFile).mtimeMs < started - 2000)
  throw new Error("Missing fresh Playwright JSON");
const report = JSON.parse(fs.readFileSync(browserFile, "utf8"));
if ((report.stats?.unexpected ?? 0) !== 0 || (report.stats?.interrupted ?? 0) !== 0 ||
    (report.stats?.skipped ?? 0) !== 0)
  throw new Error("Playwright run contains unexpected, interrupted or skipped tests");
const browserCases = [];
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
  ["S01", ["ReferencePolicyTests.NormalizesLocalCodes", "ReferencePolicyTests.RejectsInvalidLocalCodes", "ReferencePolicyTests.ValidatesOptionalLocodes", "ReferencePolicyTests.RejectsInvalidLocodes"]],
  ["S02", ["ReferencePolicyTests.AcceptsChecksumValidImo", "ReferencePolicyTests.RejectsInvalidImo", "ReferencePolicyTests.RejectsInvalidMmsi", "ReferencePolicyTests.OptionalImoAndMmsiAreNotMandatory"]],
  ["S03", ["ReferencePolicyTests.ReferencePermissionsAreExplicitAndDoNotEscalateOrganizationRoles", "ReferenceAdversarialHttpTests.SupportAndAuditorCannotUseAnyReferenceVerb"]],
  ["S04", ["MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel", "MigrationSnapshotTests.Migrations_AreDiscoveredInOrder", "ReferenceHistoricalUpgradeTests.M2SeedAndAuthenticatedSessionSurviveM3UpgradeAndRepeat"]],
  ["S05", ["ReferenceHistoricalUpgradeTests.M2SeedAndAuthenticatedSessionSurviveM3UpgradeAndRepeat"]],
  ["S06", ["ReferenceHistoricalUpgradeTests.DirectSqlConstraintsAndIdentityTriggersRejectInvalidMutations"]],
  ["S07", [], [], ["fixtures"]],
  ["S08", ["ReferenceBoundaryMatrixTests.EveryReferenceRouteRejectsAnonymousAndWrongRoles", "ReferenceSessionBoundaryTests.EveryReferenceVerbRejectsInvalidatedSession"]],
  ["S09", ["ReferenceBoundaryMatrixTests.MissingSelectionAndTrueForeignRecordsCannotBeEnumerated"]],
  ["S10", ["ReferenceBoundaryMatrixTests.EveryReferenceRouteRejectsAnonymousAndWrongRoles"]],
  ["S11", ["ReferenceAdversarialHttpTests.ForeignPortAndVesselRejectEveryRecordVerbWithoutMutation"]],
  ["S12", ["ReferenceAdversarialHttpTests.ForeignPortAndVesselRejectEveryRecordVerbWithoutMutation"]],
  ["S13", ["ReferenceAdversarialHttpTests.SupportAndAuditorCannotUseAnyReferenceVerb", "ReferenceDataHttpTests.ScopedPortAndVesselLifecycleAndRoleMatrix"]],
  ["S14", ["ReferenceAdversarialHttpTests.SecurityDowngradeAndSuspendedOrganizationDenyReferenceReads", "ReferenceBoundaryMatrixTests.SuspendedMembershipIsDeniedOnNextRequest", "ReferenceSessionBoundaryTests.EveryReferenceVerbRejectsInvalidatedSession"]],
  ["S15", ["ReferenceAdditionalContractTests.MalformedBodiesUnknownPropertiesAndOversizedJsonFailWithSanitized400", "ReferenceSessionBoundaryTests.ForeignOriginRejectsMutationEvenWithRealCsrfToken"]],
  ["S16", ["ReferenceDataHttpTests.ScopedPortAndVesselLifecycleAndRoleMatrix"]],
  ["S17", ["ReferenceAdditionalContractTests.VesselFullLifecycleAndArchivedIdentifierReservation"]],
  ["S18", ["ReferenceConcurrencyTests.ParallelDuplicateCreatesReturnConflictWithoutDuplicateRows", "ReferenceConcurrencyTests.IdenticalExternalIdentifiersAreAllowedAcrossOrganizations", "ReferenceDataHttpTests.MigrationConstraintsEnforceTenantOwnershipAndExternalIdentifiers"]],
  ["S19", ["ReferenceBoundaryMatrixTests.QueryBoundsAndLiteralWildcardsProtectScopedPagination"], ["scoped UI pagination loads beyond 25 results without duplicates"]],
  ["S20", ["ReferenceConcurrencyTests.ParallelUpdateAndArchiveProduceExactlyOneVersionTransition", "ReferenceDataHttpTests.CompetingUpdatesAndImmediateRoleRevocation", "ReferenceRevocationInterleavingTests.QueuedRoleDowngradeWinsBeforeBlockedReferenceWrite"]],
  ["S21", ["ReferenceAdditionalContractTests.VesselFullLifecycleAndArchivedIdentifierReservation"]],
  ["S22", [], ["Manager port and vessel create, edit, search, archive and restore", "Analyst can read M3 references but cannot create or edit", "scoped UI pagination loads beyond 25 results without duplicates", "selected organization and reference rows survive browser reload", "Manager vessel edit archive and restore"]],
  ["S23", [], ["delayed old-tenant port response never appears after organization switch", "reference list clears on dependency failure, then recovers without cached rows", "logging out during a held reference request clears protected state", "revocation during a reference refresh clears protected rows", "revocation removes management controls", "pagination refresh clears stale busy state"]],
  ["S24", ["OrganizationBoundaryMatrixTests.MissingAndForeignSelectionsDenyEveryVerbWithoutEnumeration"], ["Organization switch discards previous tenant reference rows"], ["reference-outage", "recovery"]],
  ["S25", [], [], ["postgres-image", "dotnet", "deps", "web", "docs", "stack", "scan"]]
];
const rows = scenarios.map(([id, required, browserNames = [], stageNames = []]) => {
  const missing = [
    ...required.filter(name => !unit(name)).map(name => "TRX:" + name),
    ...browserNames.filter(name => !browser(name)).map(name => "Browser:" + name),
    ...stageNames.filter(name => !expectedStages.includes(name)).map(name => "Stage:" + name)
  ];
  return { id: "G3-" + id, missing };
});
if (!logOk("integration-fresh.log") || !logOk("integration-repeat.log")) {
  for (const row of rows) row.missing.push("Required fresh and repeat integration logs");
}
if (!scanOk) rows.find(row => row.id === "G3-S25").missing.push("Trivy high/critical scan evidence");
const md = [
  "# G3 Execution Evidence Index", "",
  "Source revision: " + source, "",
  "This index validates fresh run provenance, mandatory stages, named test executions and scanner output.",
  "It does not grant G3 approval. Every requirement still needs independent evidence review.",
  "", "| Scenario | Evidence check | Missing |", "| --- | --- | --- |",
  ...rows.map(row => "| " + row.id + " | " +
    (row.missing.length ? "INCOMPLETE" : "EVIDENCE PRESENT, REVIEW REQUIRED") +
    " | " + (row.missing.join(", ") || "None") + " |"),
  "", "G3 remains open until the completed requirements matrix is independently reviewed."
].join("\n");
fs.writeFileSync(path.join(root, "G3_EXECUTION_INDEX.md"), md + "\n");
console.log(md);
if (rows.some(row => row.missing.length)) process.exitCode = 1;
