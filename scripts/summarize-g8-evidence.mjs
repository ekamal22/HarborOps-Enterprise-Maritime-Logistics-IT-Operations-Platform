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
const unitMethods = new Set([
  "CanonicalReplayIgnoresEquivalentUtcSerialization",
  "CanonicalReplayIgnoresJsonPropertyOrderAndInsignificantWhitespace",
  "ChangedEventIdentityAndMeaningProduceDistinctDigests",
  "InvalidPendingAdmissionLimitRejectsStartup",
  "ValidPendingAdmissionLimitIsDeterministic"
]);
function tested(method, minCases = 1) {
  const requiredFiles = unitMethods.has(method)
    ? ["g8-Unit.trx"] : ["g8-Integration.trx", "g8-Integration-repeat.trx"];
  return requiredFiles.every(file => {
    const rows = results.filter(x => x.file === file &&
      x.testName.includes("." + method));
    if (rows.length < minCases) return false;
    if (rows.some(x => x.outcome !== "Passed")) {
      fail.push(method + " in " + file + ": failed or skipped");
      return false;
    }
    return true;
  });
}
const browsers = () => {
  const dir = join(root, "playwright");
  const specsDir = "tests/browser/specs";
  if (!existsSync(dir) || !existsSync(specsDir)) return false;
  const specs = readdirSync(specsDir)
    .filter(name => name.endsWith(".spec.mjs"))
    .map(name => name.slice(0, -".spec.mjs".length));
  if (!specs.length) return false;
  return ["chromium", "firefox", "webkit"].every(engine =>
    specs.every(spec => {
      const name = engine + "-" + spec + ".json";
      const path = join(dir, name);
      if (!existsSync(path)) return false;
      try {
        const stats = JSON.parse(readFileSync(path, "utf8")).stats;
        return stats?.expected > 0 && stats.unexpected === 0 &&
          stats.skipped === 0 && stats.flaky === 0;
      } catch { return false; }
    }));
};
const requiredMutants = [
  "final-digest", "collision-as-replay", "no-work", "early-ack",
  "append-only", "quota", "no-event", "unique-key", "tenant-fk",
  "final-revocation", "source-status"
];
const restartIntegrity = () => {
  try {
    const proof = JSON.parse(read("g8-restart-integrity.json"));
    const keys = ["raw", "canonical", "receiptDigest"];
    const digestsValid = keys.every(key =>
      /^[a-f0-9]{64}$/.test(proof.beforeSha256?.[key]) &&
      proof.afterSha256?.[key] === proof.beforeSha256[key]);
    return digestsValid && proof.sourceSha === sha &&
      proof.runId === read("run-id.txt").trim() &&
      /^[a-f0-9-]{36}$/i.test(proof.receiptId) && proof.status === "PASS" &&
      proof.rawBytesUnchanged === true && proof.canonicalDigestUnchanged === true &&
      proof.canonicalEventUnchanged === true && proof.pendingWorkUnchanged === true;
  } catch { return false; }
};
const mutations = () => mutation?.sourceSha === sha &&
  mutation.control?.status === "PASS" &&
  mutation.mutants?.length === requiredMutants.length &&
  requiredMutants.every(variant => mutation.mutants.some(m =>
    m.variant === variant && m.status === "KILLED" &&
    typeof m.expectedFailure === "string" &&
    m.failedTests?.some(name => name.includes(m.expectedFailure))));

const images = () => {
  const rows = read("image-ids.tsv").split("\n").filter(Boolean)
    .map(line => line.split("\t"));
  if (!["postgres", "api", "web"].every(service =>
      rows.some(([name, image, revision]) =>
        name === service && image?.startsWith("sha256:") && revision === sha)))
    return false;
  return ["postgres", "api", "web"].every(name => {
    const path = join(root, "trivy", name + ".json");
    if (!existsSync(path)) return false;
    try {
      const report = JSON.parse(readFileSync(path, "utf8"));
      return (report.Results ?? []).every(x =>
        (x.Vulnerabilities ?? []).length === 0);
    } catch { return false; }
  });
};
const evidence = [
  ["S01", "D-006 explicit approval", [], [], "D-006 acceptance pending"],
  ["S02", "Canonical identity", ["CanonicalReplayIgnoresEquivalentUtcSerialization", "CanonicalReplayIgnoresJsonPropertyOrderAndInsignificantWhitespace", "ChangedEventIdentityAndMeaningProduceDistinctDigests"], ["tracking-contract"]],
  ["S03", "Schema and snapshot", ["ModelSnapshot_MatchesCurrentEntityModel", "Migrations_AreDiscoveredInOrder", "M8DatabaseIndexesMatchRelationalModel", "DirectSqlCannotRewriteAcceptedEventReceiptOrWork"], ["build", "dotnet-regressions"]],
  ["S04", "Populated M7 to M8 upgrade and safe rollback", ["ExistingM7SourceAndCredentialSurviveM8UpgradeAndEmptyRollback", "PopulatedM8DowngradeIsRejectedAndPreservesImmutableHistory"], ["dotnet-regressions"]],
  ["S05", "Atomic accepted triple", ["NewReceiptAndCanonicalReplayCommitOneImmutableTriple"], ["dotnet-regressions"]],
  ["S06", "Post-commit acknowledgment", ["NewReceiptAndCanonicalReplayCommitOneImmutableTriple", ["CommitUncertaintyNeverProducesSuccessAndReplayResolvesOutcome", 2]], ["dotnet-regressions", "security-mutations"]],
  ["S07", "Ten sequential and concurrent replays", ["NewReceiptAndCanonicalReplayCommitOneImmutableTriple", "TenSequentialCanonicalReplaysKeepOriginalIdentity", "ConcurrentDuplicateIntakeCreatesExactlyOneReceipt"], ["dotnet-regressions"]],
  ["S08", "JSON property order, whitespace and UTC equality", ["CanonicalReplayIgnoresJsonPropertyOrderAndInsignificantWhitespace", "CanonicalReplayIgnoresEquivalentUtcSerialization"], ["dotnet-regressions"]],
  ["S09", "Key collision", ["ChangedPayloadConflictsWithoutMutatingOriginalOrLeakingBody"], ["dotnet-regressions"]],
  ["S10", "Twenty simultaneous same-key deliveries", ["ConcurrentConflictingDeliveryKeepsFirstCommittedPayload", "ConcurrentDuplicateIntakeCreatesExactlyOneReceipt"], ["dotnet-regressions"]],
  ["S11", "Organization and source isolation", ["TenantAndSourceKeysAreIndependentAndRevocationStopsReplay", "SameOrganizationDifferentSourcesReuseExternalKeyWithoutCollision"], ["dotnet-regressions"]],
  ["S12", "Live revocation, disablement and suspension lock waits", [["OverlappingRevocationDisableAndSuspensionRespectCommitOrder", 3], "RevocationBeforeFinalAuthorizationIsRejected"], ["dotnet-regressions"]],
  ["S13", "Final digest, status, validity windows and rotation", ["FinalTransactionAuthorizationRejectsWrongDigestAndForeignSource", "RotationOverlapExpiryAndNotBeforeAreCheckedAtFinalReceipt", "CredentialExpiryEvaluatedAfterCredentialLockWait"], ["dotnet-regressions", "security-mutations"]],
  ["S14", "Accepted M7 negative request contract", ["MediaProfilesSizeAndMethodRejectSafely", "BrowserSessionIsNotMachineAuthenticationAndCrossOriginIsDenied", "AuthenticationFailuresAreThrottledBeforeExcessiveDatabaseWork", "ValidCredentialDeniedAfterSourceDisableOrOrganizationSuspension", "DuplicateAuthorizationHeaderAndMalformedSourceIdAreRejected"], ["dotnet-regressions"]],
  ["S15", "Unknown reference remains Pending", ["SyntacticallyValidUnknownReferenceIsReceivedWithoutShipmentMutation"], ["dotnet-regressions"]],
  ["S16", "Real 9999/10000/10001 cap and cross-source race", ["Exact9999_10000_10001AndConcurrentCrossSourceAdmission", "PendingCapacityReturnsRetryable503ButReplaysOriginalReceipt"], ["dotnet-regressions", "security-mutations"]],
  ["S17", "Dependency outage, real COMMIT disconnect and lost acknowledgment", [["CommitUncertaintyNeverProducesSuccessAndReplayResolvesOutcome", 2], ["RealPostgresCommitTransportLossNeverAcknowledgesAndReplayResolves", 2], "InitialAuthenticationDatabaseOutageGivesRetryable503"], ["dotnet-regressions", "source-outage-recovery"]],
  ["S18", "Mid-transaction cancellation and timeout", ["MidTransactionCancellationRollsBackCompleteReceiptTriple", "CancelledReceiptTransactionDoesNotAcknowledge", ["CommitUncertaintyNeverProducesSuccessAndReplayResolvesOutcome", 2]], ["dotnet-regressions"]],
  ["S19", "Durable restart and replay", ["AcceptedReceiptSurvivesConnectionAndApiRestart"], ["dotnet-regressions", "source-outage-recovery"]],
  ["S20", "Direct SQL integrity and tenant constraints", ["DirectSqlCannotRewriteAcceptedEventReceiptOrWork", "DirectSqlRejectsDuplicateKeysAndForgedTenantCredentialScopes", "M8DatabaseIndexesMatchRelationalModel"], ["dotnet-regressions", "security-mutations"]],
  ["S21", "Sensitive data isolation", ["ReceiptErrorsDoNotExposeCredentialsOrPayload"], ["dotnet-regressions", "web-build", "evidence-safety"]],
  ["S22", "Accepted G0 through G7 regression suite", ["ModelSnapshot_MatchesCurrentEntityModel", "AuthenticatedSourceCommitsAndAcknowledgesEvent"], ["dotnet-regressions", "tracking-contract", "browsers"]],
  ["S23", "Eleven effective security mutations", [], ["security-mutations"]],
  ["S24", "Advisories and exact image scans", [], ["nuget-advisories", "npm-advisories", "image-scans"]],
  ["S25", "Independent source-locked raw evidence review", [], ["evidence-safety"], "Independent package review and gate acceptance pending"]
];
assert.equal(evidence.length, 25);
const scenarios = evidence.map(([suffix, description, tests, stagesNeeded, note]) => {
  const gaps = [];
  if (note) gaps.push(note);
  for (const entry of tests) {
    const [name, count] = typeof entry === "string" ? [entry, 1] : entry;
    if (!tested(name, count)) gaps.push("executed PASS missing: " + name +
      " (at least " + count + " in each required TRX)");
  }
  for (const name of stagesNeeded) if (!present(name)) gaps.push("stage missing/failed: " + name);
  if (suffix === "S19" && !restartIntegrity())
    gaps.push("exact raw bytes, canonical digest and Pending work restart proof missing");
  if (suffix === "S22" && !browsers()) gaps.push("three clean browser engines missing");
  if (suffix === "S23" && !mutations()) gaps.push("eleven effective G8 mutations missing");
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
