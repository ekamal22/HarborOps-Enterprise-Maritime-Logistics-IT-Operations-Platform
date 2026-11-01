#!/usr/bin/env node
// G5 evidence indexing is a review input, never an acceptance decision.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const root = process.argv[2];
assert.ok(root && fs.existsSync(root), "Run-specific G5 evidence directory required");
const file = name => path.join(root, name);
const read = name => fs.readFileSync(file(name), "utf8");
const source = read("g5-source-sha.txt").trim();
const current = execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim();
assert.match(source, /^[0-9a-f]{40}$/);
assert.equal(source, current, "Current source does not match G5 execution");
assert.equal(read("m5-source-sha.txt").trim(), source);
const started = Date.parse(read("g5-start-utc.txt").trim());
assert.ok(Number.isFinite(started) && Date.now() - started >= 0 &&
  Date.now() - started < 24 * 3600 * 1000, "Invalid G5 execution start");
const fresh = name => fs.existsSync(file(name)) && fs.statSync(file(name)).isFile() &&
  fs.statSync(file(name)).size > 0 && fs.statSync(file(name)).mtimeMs >= started - 2000;
assert.ok(!fresh("g5-aborted.txt"), "G5 execution contains a current abort marker");
const stages = [
  "decision-unit","schema-index","shipment-schema","preflight","postgres-image","dotnet","deps","web","docs",
  "stack","accounts","organizations","session","setup-fixtures","fixtures","m4-fixtures",
  "m5-fixtures","browsers","reference-outage","maritime-outage","shipment-outage","recovery","scan"
];
const ledger = read("g5-stages.tsv").trim().split(/\r?\n/).map(line => line.split("\t"));
for (const stage of stages) {
  const rows = ledger.filter(x => x[0] === stage);
  assert.equal(rows.length, 1, "Missing/duplicate G5 stage " + stage);
  assert.equal(rows[0][1], source, "Wrong source: " + stage);
  assert.equal(rows[0][2], "0", "Failed G5 stage " + stage);
  assert.ok(fresh("g5-" + stage + ".log"), "Missing stage log " + stage);
}
function recurse(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(ent =>
    ent.isDirectory() ? recurse(path.join(dir, ent.name)) : [path.join(dir, ent.name)]);
}
const trxRoot = file("trx");
assert.ok(fs.existsSync(trxRoot), "No TRX directory");
const results = new Map();
for (const candidate of recurse(trxRoot).filter(p => p.endsWith(".trx"))) {
  assert.ok(fs.statSync(candidate).mtimeMs >= started - 2000, "Stale TRX: " + candidate);
  const xml = fs.readFileSync(candidate, "utf8");
  assert.ok(xml.includes("<TestRun"), "Invalid TRX file");
  for (const [tag] of xml.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const name = /\btestName="([^"]+)"/.exec(tag)?.[1];
    const outcome = /\boutcome="([^"]+)"/.exec(tag)?.[1];
    assert.ok(name && outcome, "Malformed TRX test case");
    const all = results.get(name) || [];
    all.push(outcome);
    results.set(name, all);
    assert.equal(outcome, "Passed", "Unexpected .NET test result: " + name);
  }
}
assert.ok(results.size > 0, "No .NET test outcomes");
const unit = token => [...results].some(([name, outcomes]) =>
  name.includes(token) && outcomes.length > 0 && outcomes.every(x => x === "Passed"));
const reports = [];
const browserRoot = file("playwright");
assert.ok(fs.existsSync(browserRoot), "Browser reports missing");
for (const name of fs.readdirSync(browserRoot).filter(x => x.endsWith(".json"))) {
  const candidate = path.join(browserRoot, name);
  assert.ok(fs.statSync(candidate).mtimeMs >= started - 2000, "Stale browser report " + name);
  const report = JSON.parse(fs.readFileSync(candidate, "utf8"));
  assert.equal(report.stats?.unexpected ?? 0, 0, "Unexpected browser tests");
  assert.equal(report.stats?.skipped ?? 0, 0, "Skipped browser tests");
  assert.equal(report.stats?.interrupted ?? 0, 0, "Interrupted browser tests");
  function walk(suites) {
    for (const suite of suites ?? []) {
      for (const spec of suite.specs ?? [])
        for (const test of spec.tests ?? [])
          reports.push({ engine: test.projectName, title: spec.title,
            results: (test.results ?? []).map(x => x.status) });
      walk(suite.suites);
    }
  }
  walk(report.suites);
}
const browser = (title, engine) => reports.some(r => r.title === title && r.engine === engine &&
  r.results.length > 0 && r.results.every(x => x === "passed"));
const allBrowser = title => ["chromium","firefox","webkit"].every(e => browser(title,e));
const scanned = ["postgres","api","web"].every(name => {
  const fn = file("trivy/" + name + ".json");
  if (!fs.existsSync(fn) || fs.statSync(fn).mtimeMs < started - 2000) return false;
  const scan = JSON.parse(fs.readFileSync(fn, "utf8"));
  return Array.isArray(scan.Results) && scan.Results.every(entry =>
    !(entry.Vulnerabilities || []).some(v => ["HIGH","CRITICAL"].includes(v.Severity)));
});
const scenarios = [
  ["S01", unit("ShipmentPolicyTests.WorkflowMatrix")],
  ["S02", unit("ShipmentPolicyTests.WorkflowMatrix") && unit("ShipmentPolicyTests.RejectUnknownStatus")],
  ["S03", unit("ShipmentPolicyTests.ReferencesAndUtcAreStrict")],
  ["S04", unit("MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel") &&
    unit("MigrationSnapshotTests.Migrations_AreDiscoveredInOrder")],
  ["S05", unit("ShipmentMigrationTests.SeededM4HistorySurvivesM5UpgradeRollbackAndReapply")],
  ["S06", unit("ShipmentHttpTests.DirectSqlCannotDeleteShipmentOrForgeStatusChange")],
  ["S07", stages.includes("m5-fixtures")],
  ["S08", unit("ShipmentBoundaryTests.AllShipmentRoutesRejectInvalidatedAuthentication")],
  ["S09", unit("ShipmentBoundaryTests.SelectedOrganizationAndRoleChangesFailClosed")],
  ["S10", unit("ShipmentHttpTests.DuplicateArchivedReferenceAndCrossTenantIdentityStayScoped")],
  ["S11", unit("ShipmentBoundaryTests.ForeignAndArchivedPortsRejectShipmentCreation")],
  ["S12", unit("ShipmentBoundaryTests.FullFiveRoleShipmentMatrix")],
  ["S13", unit("ShipmentBoundaryTests.QueuedShipmentWriteRechecksSessionRevocation")],
  ["S14", unit("ShipmentBoundaryTests.InvalidPayloadOriginAndCsrfAreRejected")],
  ["S15", unit("ShipmentHttpTests.DuplicateArchivedReferenceAndCrossTenantIdentityStayScoped")],
  ["S16", unit("ShipmentHttpTests.AnalystCreatesTransitionsArchivesRestoresAndReadsImmutableHistory")],
  ["S17", unit("ShipmentHttpTests.AnalystCreatesTransitionsArchivesRestoresAndReadsImmutableHistory")],
  ["S18", unit("ShipmentHttpTests.AnalystCreatesTransitionsArchivesRestoresAndReadsImmutableHistory")],
  ["S19", unit("ShipmentBoundaryTests.PaginationAndFiltersStayTenantScoped")],
  ["S20", unit("ShipmentHttpTests.CompetingExpectedVersionUpdatesHaveOneWinnerAndOneHistoryRecord")],
  ["S21", unit("ShipmentBoundaryTests.FailedTransitionLeavesNoPartialHistory")],
  ["S22", allBrowser("M5 Analyst creates and advances a scoped shipment")],
  ["S23", allBrowser("M5 Auditor shipment view does not grant M4 management") &&
    allBrowser("M5 tenant change invalidates prior shipment list")],
  ["S24", unit("MaritimePolicyTests.RoleGrantsStayWithinBusinessPermissions") &&
    unit("ReferenceDataHttpTests.ScopedPortAndVesselLifecycleAndRoleMatrix")],
  ["S25", scanned]
];
for (const [id, passed] of scenarios)
  console.log("G5-" + id + ": " + (passed ? "EVIDENCE PRESENT, REVIEW REQUIRED" : "INCOMPLETE"));
assert.ok(scenarios.every(([, passed]) => passed), "Incomplete mandatory G5 evidence");
console.log("G5 evidence index complete for review, no acceptance implied.");
