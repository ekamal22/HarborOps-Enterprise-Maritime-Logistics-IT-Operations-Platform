#!/usr/bin/env node
// Reconcile G8 cases against executable stage results. Missing proof is BLOCKED,
// not a pass. Formal acceptance and independent archive review happen separately.
import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const root = process.argv[2];
assert.ok(root && existsSync(root), "G8 evidence directory is required");
const read = name => existsSync(join(root, name))
  ? readFileSync(join(root, name), "utf8") : "";
const sha = read("source-sha.txt").trim();
assert.match(sha, /^[0-9a-f]{40}$/, "Source SHA required");
const stages = new Map();
for (const row of read("stages.tsv").split("\n").filter(Boolean)) {
  const [name, revision, status, seconds] = row.split("\t");
  stages.set(name, { revision, code: Number(status), seconds: Number(seconds) });
}
const results = [];
const dir = join(root, "trx");
if (existsSync(dir)) {
  for (const name of readdirSync(dir).filter(x => x.endsWith(".trx"))) {
    const content = readFileSync(join(dir, name), "utf8");
    for (const row of content.matchAll(/<UnitTestResult\b([^>]*?)\/?>/g)) {
      const testName = /testName="([^"]+)"/.exec(row[1])?.[1];
      const outcome = /outcome="([^"]+)"/.exec(row[1])?.[1];
      if (testName) results.push({ file: name, testName, outcome });
    }
  }
}
let mutation = null;
try { mutation = JSON.parse(read("g8-mutations.json")); } catch {}
const fail = [];
const present = name => {
  const state = stages.get(name);
  if (!state) return false;
  if (state.revision !== sha || state.code !== 0) {
    fail.push(name + ": exit " + state.code + " / revision " + state.revision);
    return false;
  }
  return true;
};
function tested(method) {
  const rows = results.filter(x => x.testName.includes("." + method));
  if (!rows.length) return false;
  if (rows.some(x => x.outcome !== "Passed")) {
    fail.push(method + ": failed or skipped");
    return false;
  }
  const integration = rows.some(x => x.file.includes("Integration"));
  return !integration || ["g8-Integration.trx", "g8-Integration-repeat.trx"]
    .every(name => rows.some(x => x.file === name));
}
const browsers = () => {
  const dir = join(root, "playwright");
  if (!existsSync(dir)) return false;
  const files = readdirSync(dir).filter(x => x.endsWith(".json"));
  if (!["chromium", "firefox", "webkit"].every(e =>
    files.some(f => f.startsWith(e + "-")))) return false;
  return files.every(file => {
    const stats = JSON.parse(readFileSync(join(dir, file), "utf8")).stats;
    return stats?.expected > 0 && stats.unexpected === 0 &&
      stats.skipped === 0 && stats.flaky === 0;
  });
};
const mutations = () => mutation?.sourceSha === sha &&
  mutation.control?.status === "PASS" && mutation.mutants?.length === 6 &&
  mutation.mutants.every(x => x.status === "KILLED");
const images = () => ["postgres", "api", "web"].every(name => {
  const path = join(root, "trivy", name + ".json");
  if (!existsSync(path)) return false;
  const report = JSON.parse(readFileSync(path, "utf8"));
  return (report.Results ?? []).every(x => (x.Vulnerabilities ?? []).length === 0);
});
const evidence = [
  ["S01", "D-006 explicit approval", [], [], "D-006 acceptance pending"],
  ["S02", "Canonical identity", ["CanonicalReplayIgnoresEquivalentUtcSerialization", "ChangedEventIdentityAndMeaningProduceDistinctDigests"], ["tracking-contract"]],
  ["S03", "Schema and snapshot", ["ModelSnapshot_MatchesCurrentEntityModel", "Migrations_AreDiscoveredInOrder", "DirectSqlCannotRewriteAcceptedEventReceiptOrWork"], ["build", "dotnet-regressions"]],
  ["S04", "Populated M7 to M8 upgrade", ["ExistingM7SourceAndCredentialSurviveM8UpgradeAndEmptyRollback"], ["dotnet-regressions"]],
  ["S05", "Atomic accepted triple", ["NewReceiptAndCanonicalReplayCommitOneImmutableTriple"], ["dotnet-regressions"]],
  ["S06", "Post-commit acknowledgment", ["NewReceiptAndCanonicalReplayCommitOneImmutableTriple", "DirectSqlCannotRewriteAcceptedEventReceiptOrWork"], ["dotnet-regressions", "security-mutations"]],
  ["S07", "Idempotent replay", ["NewReceiptAndCanonicalReplayCommitOneImmutableTriple", "ConcurrentDuplicateIntakeCreatesExactlyOneReceipt"], ["dotnet-regressions"]],
  ["S08", "Canonical byte equality", ["CanonicalReplayIgnoresEquivalentUtcSerialization"], ["dotnet-regressions"]],
  ["S09", "Key collision", ["ChangedPayloadConflictsWithoutMutatingOriginalOrLeakingBody"], ["dotnet-regressions"]],
  ["S10", "Concurrent conflicting replay", ["ConcurrentConflictingDeliveryKeepsFirstCommittedPayload"], ["dotnet-regressions"]],
  ["S11", "Organization and source isolation", ["TenantAndSourceKeysAreIndependentAndRevocationStopsReplay"], ["dotnet-regressions"]],
  ["S12", "Revocation interleaving", ["RevocationBeforeFinalAuthorizationIsRejected"], ["dotnet-regressions"]],
  ["S13", "Final credential validation", ["FinalTransactionAuthorizationRejectsWrongDigestAndForeignSource"], ["dotnet-regressions", "security-mutations"]],
  ["S14", "M7 negative regression", ["MediaProfilesSizeAndMethodRejectSafely", "BrowserSessionIsNotMachineAuthenticationAndCrossOriginIsDenied"], ["dotnet-regressions"]],
  ["S15", "Unknown reference retained pending", ["SyntacticallyValidUnknownReferenceIsReceivedWithoutShipmentMutation"], ["dotnet-regressions"]],
  ["S16", "Pending capacity", ["PendingCapacityReturnsRetryable503ButReplaysOriginalReceipt"], ["dotnet-regressions", "security-mutations"]],
  ["S17", "Database failure/ambiguous COMMIT", ["CommitUncertaintyNeverProducesSuccessAndReplayResolvesOutcome"], ["dotnet-regressions"]],
  ["S18", "Cancellation and timeout", ["CancelledReceiptTransactionDoesNotAcknowledge"], ["dotnet-regressions"]],
  ["S19", "Restart persistence", ["AcceptedReceiptSurvivesConnectionAndApiRestart"], ["source-outage-recovery"]],
  ["S20", "Direct SQL invariants", ["DirectSqlCannotRewriteAcceptedEventReceiptOrWork"], ["dotnet-regressions", "security-mutations"]],
  ["S21", "No secret or raw payload emission", ["ReceiptErrorsDoNotExposeCredentialsOrPayload"], ["dotnet-regressions"]],
  ["S22", "Accepted G0 to G7 regressions", ["NewReceiptAndCanonicalReplayCommitOneImmutableTriple", "ModelSnapshot_MatchesCurrentEntityModel"], ["dotnet-regressions", "browsers"]],
  ["S23", "Effective security mutants", [], ["security-mutations"]],
  ["S24", "Advisories and exact image scans", [], ["nuget-advisories", "npm-advisories", "image-scans"]],
  ["S25", "Independent raw evidence review", [], [], "Independent package review and gate acceptance pending"]
];
assert.equal(evidence.length, 25);
const scenarios = evidence.map(([suffix, description, tests, stagesNeeded, note]) => {
  const gaps = [];
  if (note) gaps.push(note);
  for (const name of tests) if (!tested(name)) gaps.push("executed PASS missing: " + name);
  for (const name of stagesNeeded) if (!present(name)) gaps.push("stage missing/failed: " + name);
  if (suffix === "S22" && !browsers()) gaps.push("three clean browser engines missing");
  if (suffix === "S23" && !mutations()) gaps.push("six effective G8 mutations missing");
  if (suffix === "S24" && !images()) gaps.push("complete HIGH/CRITICAL image scan evidence missing");
  const status = gaps.length ? "BLOCKED" : "PASS";
  return { id:"G8-" + suffix, description, status, gaps, note:note ?? "" };
});
const report = { sourceSha:sha, generatedAtUtc:new Date().toISOString(),
  stages:Object.fromEntries(stages), testExecutions:results.length,
  failures:fail, totals:{ PASS:scenarios.filter(x=>x.status==="PASS").length,
    BLOCKED:scenarios.filter(x=>x.status==="BLOCKED").length },
  scenarios };
writeFileSync(join(root, "g8-scenarios.json"), JSON.stringify(report, null, 2));
console.log(JSON.stringify({ sourceSha:sha, tests:results.length, totals:report.totals }));
