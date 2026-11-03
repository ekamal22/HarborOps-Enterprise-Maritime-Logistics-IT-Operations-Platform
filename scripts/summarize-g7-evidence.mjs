#!/usr/bin/env node
// Derive per-scenario G7 outcomes from raw evidence already on disk. No scenario passes because a
// name appears in this table: every requirement must be present in the raw TRX, stage log or report.
// Usage: node scripts/summarize-g7-evidence.mjs <evidence directory>
import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const dir = process.argv[2];
assert.ok(dir && existsSync(dir), "Evidence directory required");
const read = name => { const file = join(dir, name); return existsSync(file) ? readFileSync(file, "utf8") : ""; };
const sha = read("source-sha.txt").trim();

const stages = new Map();
for (const line of read("stages.tsv").split("\n").filter(Boolean)) {
  const [name, revision, code, seconds] = line.split("\t");
  stages.set(name, { revision, code: Number(code), seconds: Number(seconds) });
}

// Parse every TRX file; each result is tagged with the file it came from.
const trxDir = join(dir, "trx");
const results = [];
if (existsSync(trxDir)) {
  for (const file of readdirSync(trxDir).filter(x => x.endsWith(".trx"))) {
    const xml = readFileSync(join(trxDir, file), "utf8");
    for (const match of xml.matchAll(/<UnitTestResult\b([^>]*?)\/?>/g)) {
      const name = /testName="([^"]+)"/.exec(match[1])?.[1];
      const outcome = /outcome="([^"]+)"/.exec(match[1])?.[1];
      if (name) results.push({ file, name, outcome });
    }
  }
}
const trxTotals = {};
for (const file of new Set(results.map(x => x.file))) {
  const rows = results.filter(x => x.file === file);
  trxTotals[file] = {
    total: rows.length,
    passed: rows.filter(x => x.outcome === "Passed").length,
    failed: rows.filter(x => x.outcome === "Failed").length,
    other: rows.filter(x => x.outcome !== "Passed" && x.outcome !== "Failed").length
  };
}

const problems = [];
const stage = name => {
  const found = stages.get(name);
  if (!found) { problems.push("stage " + name + " not executed"); return false; }
  if (found.revision !== sha) { problems.push("stage " + name + " ran on another revision"); return false; }
  if (found.code !== 0) { problems.push("stage " + name + " exit " + found.code); return false; }
  return true;
};
// A test requirement passes only if it ran in every TRX file of its kind and always passed.
const test = method => {
  const rows = results.filter(x => x.name.includes("." + method));
  if (!rows.length) { problems.push("test " + method + " absent from raw results"); return false; }
  const files = new Set(rows.map(x => x.file));
  if ([...files].some(f => f.includes("Integration")) &&
      !(files.has("g7-Integration.trx") && files.has("g7-Integration-repeat.trx"))) {
    problems.push("test " + method + " missing from a repeat run"); return false;
  }
  if (rows.some(x => x.outcome !== "Passed")) { problems.push("test " + method + " not passed"); return false; }
  return true;
};
const allTrxClean = () => Object.keys(trxTotals).length >= 4 &&
  Object.values(trxTotals).every(x => x.total > 0 && x.failed === 0 && x.other === 0);

const reports = existsSync(join(dir, "playwright"))
  ? readdirSync(join(dir, "playwright")).filter(x => x.endsWith(".json")) : [];
const browser = spec => ["chromium", "firefox", "webkit"].every(engine => {
  const name = engine + "-" + spec + ".json";
  if (!reports.includes(name)) { problems.push("browser report " + name + " absent"); return false; }
  const stats = JSON.parse(readFileSync(join(dir, "playwright", name), "utf8")).stats;
  const ok = stats.expected > 0 && stats.unexpected === 0 && stats.skipped === 0 && stats.flaky === 0;
  if (!ok) problems.push("browser report " + name + " not clean");
  return ok;
});
const allBrowsers = () => stage("browsers") && reports.length >= 48 &&
  reports.every(name => {
    const stats = JSON.parse(readFileSync(join(dir, "playwright", name), "utf8")).stats;
    return stats.expected > 0 && stats.unexpected === 0 && stats.skipped === 0 && stats.flaky === 0;
  });

let mutations = null;
try { mutations = JSON.parse(read("g7-mutations.json")); } catch { /* absent */ }
const mutationsOk = () => mutations?.sourceSha === sha && mutations.control?.status === "PASS" &&
  mutations.mutants?.length === 5 && mutations.mutants.every(x => x.status === "KILLED");

const trivyClean = () => {
  const names = ["postgres", "api", "web"];
  return names.every(name => {
    const file = join(dir, "trivy", name + ".json");
    if (!existsSync(file)) { problems.push("trivy " + name + " absent"); return false; }
    const report = JSON.parse(readFileSync(file, "utf8"));
    return (report.Results ?? []).every(r => (r.Vulnerabilities ?? []).length === 0);
  }) && existsSync(join(dir, "trivy", "version.txt")) && /Vulnerability DB/i.test(read("trivy/version.txt"));
};
const operatorCli = existsSync(join(dir, "source-cli-operator.tsv"));

// Each scenario lists what must have executed and passed. "reason" carries decision-dependent blocks.
const scenarios = [
  ["G7-S01", "D-002 explicit acceptance and immutable upstream pin", () => false,
    { blocked: "D-002 acceptance pending; the upstream 2.2.0 definition is hosted on a mutable service" }],
  ["G7-S02", "Source lifecycle policy", () => [test("SourceCodesAndCredentialWindowsAreConstrained"),
    test("SimultaneousRotationsNeverIssueThirdLiveCredential"),
    test("DatabaseRejectsThirdOverlappingCredentialAndForeignTenantSource")].every(Boolean)],
  ["G7-S03", "Credential entropy and storage", () => [test("DatabasePreventsCredentialIdentityMutationAndHistoryDeletion"),
    test("PopulatedM6DataSurvivesM7UpgradeRollbackAndReapply"),
    test("AuthenticatedSourceValidatesButNeverAcknowledgesEvent"), mutations?.mutants?.some(x => x.variant === "digest" && x.status === "KILLED") === true].every(Boolean)],
  ["G7-S04", "Fresh PostgreSQL schema and EF snapshot", () => [stage("tracking-contract"),
    test("ModelSnapshot_MatchesCurrentEntityModel"), test("Migrations_AreDiscoveredInOrder"),
    test("DatabaseRejectsThirdOverlappingCredentialAndForeignTenantSource"),
    test("DatabasePreventsCredentialIdentityMutationAndHistoryDeletion")].every(Boolean)],
  ["G7-S05", "Populated M6 to M7 upgrade, rollback and reapply", () => [
    test("PopulatedM6DataSurvivesM7UpgradeRollbackAndReapply"),
    test("ExistingM5RowsSurviveM6UpgradeRollbackAndReapply")].every(Boolean)],
  ["G7-S06", "Restricted source CLI", () => [stage("source-cli-negative"),
    test("CachedOrganizationCannotIssueCredentialAfterExternalSuspension"), operatorCli].every(Boolean),
    { notExecuted: "Protected-terminal create, rotate, revoke and disable lifecycle needs an operator terminal" }],
  ["G7-S07", "Bearer authentication matrix", () => [stage("ingress-before"),
    test("DuplicateAuthorizationHeaderAndMalformedSourceIdAreRejected"),
    test("ExpiredAndFutureCredentialsNeverAuthenticate"),
    test("ValidCredentialDeniedAfterSourceDisableOrOrganizationSuspension"),
    test("AuthenticationFailuresAreThrottledBeforeExcessiveDatabaseWork"),
    test("RotationAndDisabledSourceFailClosedAcrossTenants")].every(Boolean)],
  ["G7-S08", "Rotation and revocation concurrency", () => [
    test("SimultaneousRotationsNeverIssueThirdLiveCredential"),
    test("DisableAndRotationRaceCannotLeaveUsableCredentials"),
    test("RevocationInvalidatesAllSubsequentAuthenticationsAcrossRequests"),
    test("InFlightValidationWaitsForCommittedRevocation")].every(Boolean)],
  ["G7-S09", "Two-organization entitlement and IDOR", () => [
    test("DuplicateSourceCodeAndForeignCredentialRevocationAreRejected"),
    test("RotationAndDisabledSourceFailClosedAcrossTenants"),
    test("ValidCredentialDeniedAfterSourceDisableOrOrganizationSuspension"),
    test("RejectsSpoofedAndUnrecognizedKeys"),
    test("DatabaseRejectsThirdOverlappingCredentialAndForeignTenantSource")].every(Boolean)],
  ["G7-S10", "Browser identity separation", () => [
    test("BrowserSessionIsNotMachineAuthenticationAndCrossOriginIsDenied"),
    test("SourceBearerCannotAuthenticateHumanScopedEndpoints"), browser("tracking-ingress"),
    stage("web-build")].every(Boolean)],
  ["G7-S11", "Transport trust", () => [test("LoopbackPeerIsAcceptedOverLocalHttp"),
    test("RemoteAttackerWithForgedLocalHostIsRejected"), test("ForwardedHeadersFromAnUntrustedPeerAreIgnored"),
    test("MissingRemoteAddressIsRejectedByProductionTransportValidation"),
    test("UnexpectedHostIsRejectedBeforeTheApplication"), test("OnlyConfiguredDockerPeersMayUseLocalHttp"),
    test("PlainHttpIsRejectedAndHttpsAcceptedWhenLocalHttpExceptionIsDisabled"),
    stage("network-contract")].every(Boolean)],
  ["G7-S12", "Rate limits", () => [test("HighCardinalityPartitionsAreBoundedAndStaleEntriesReclaimed"),
    test("WindowThresholdAndExpiryUseProvidedClock"), test("ConcurrentCallsNeverExceedConfiguredWindow"),
    test("AuthenticationFailuresAreThrottledBeforeExcessiveDatabaseWork")].every(Boolean)],
  ["G7-S13", "Media and profile contract", () => [test("MediaProfilesSizeAndMethodRejectSafely"),
    stage("ingress-before")].every(Boolean)],
  ["G7-S14", "JSON parser boundaries", () => [test("RejectsMalformedAndDuplicateJson"),
    test("AppliesStrictBodyBoundIncludingInvalidUtf8"),
    test("ValidJsonAtExactBodyLimitIsAcceptedAndOneExtraByteIsRejected"),
    test("MediaProfilesSizeAndMethodRejectSafely")].every(Boolean)],
  ["G7-S15", "Event identity and reference validation", () => [
    test("EventIdentityRejectsWhitespaceAndDecodedControls"), test("TransportReferenceRejectsMalformedValues"),
    test("EquipmentReferencesDoNotSilentlyTrimControlCharacters"),
    test("AcceptsExplicitZeroOffsetAndRejectsWrongReferenceFamily")].every(Boolean)],
  ["G7-S16", "UTC policy", () => [test("RejectsNonUtcAndOutOfWindowEvents"),
    test("TimestampPolicyUsesAbsoluteEndAndStrictUtc")].every(Boolean)],
  ["G7-S17", "Pinned positive subset fixtures", () => [test("ParsesOnlyExplicitSupportedSubset"),
    test("CanonicalEventBindsOnlyValidatedServerIdentity"), stage("tracking-contract")].every(Boolean),
    { blocked: "Fixture tests ran; the upstream 2.2.0 mapping and fixture hashes depend on D-002 acceptance", always: true }],
  ["G7-S18", "Unsupported variants", () => [test("RejectsUnsupportedFamilyAndCodes"),
    test("RejectsLegacyWireNamesAndReplayedFields"), test("RejectsSpoofedAndUnrecognizedKeys")].every(Boolean)],
  ["G7-S19", "HTTP response envelope", () => [test("MediaProfilesSizeAndMethodRejectSafely"),
    test("AuthenticationFailuresAreThrottledBeforeExcessiveDatabaseWork"),
    test("ValidCredentialDeniedAfterSourceDisableOrOrganizationSuspension"), stage("ingress-before"),
    stage("source-outage-recovery")].every(Boolean)],
  ["G7-S20", "No durable acknowledgment", () => [test("AuthenticatedSourceValidatesButNeverAcknowledgesEvent"),
    stage("ingress-before"), stage("source-outage-recovery")].every(Boolean)],
  ["G7-S21", "Credential store outage and cancellation", () => [stage("source-outage-recovery"),
    test("CancelledAuthenticationNeverSucceedsOrAcknowledges"),
    test("Readiness_Returns503WhenDatabaseUnreachable")].every(Boolean)],
  ["G7-S22", "Accepted M0 to M6 regressions", () => [stage("build"), stage("dotnet-regressions"), allTrxClean(),
    allBrowsers()].every(Boolean)],
  ["G7-S23", "Compose and browser integration", () => [stage("compose"), stage("accounts"),
    stage("organizations"), stage("source-cli-negative"), stage("ingress-before"), allBrowsers(),
    stage("source-outage-recovery"), read("teardown.txt").trim() === "containers= volumes= database="].every(Boolean)],
  ["G7-S24", "Security effectiveness and scans", () => [stage("security-mutations"), mutationsOk(),
    stage("nuget-advisories"), stage("npm-advisories"), stage("image-scans"), trivyClean()].every(Boolean)],
  ["G7-S25", "Reproducible evidence and independent review", () => false,
    { blocked: "Independent raw-evidence review pending; manifest and archive are written after this summary" }]
];

const rows = scenarios.map(([id, title, check, extra]) => {
  problems.length = 0;
  let executed = false;
  try { executed = check(); } catch (error) { problems.push(String(error.message ?? error)); }
  let status;
  if (extra?.blocked && (extra.always || !executed)) status = "BLOCKED";
  else if (executed) status = "PASS";
  else if (extra?.notExecuted && problems.every(x => !/not passed|exit/.test(x)) && !operatorCli) status = "NOT EXECUTED";
  else status = problems.some(x => /not passed|exit|not clean/.test(x)) ? "FAIL" : "NOT EXECUTED";
  const note = extra?.blocked ?? (status === "NOT EXECUTED" ? extra?.notExecuted : undefined);
  return { id, title, sourceSha: sha, status, gaps: [...new Set(problems)], note: note ?? "" };
});

const summary = { sourceSha: sha, generatedUtc: new Date().toISOString(), trxTotals, browserReports: reports.length, scenarios: rows };
writeFileSync(join(dir, "g7-scenarios.json"), JSON.stringify(summary, null, 2));
const md = ["# G7 scenario results", "", "Source revision: " + sha, "",
  "| Scenario | Title | Status | Gaps or note |", "| --- | --- | --- | --- |",
  ...rows.map(r => "| " + r.id + " | " + r.title + " | " + r.status + " | " +
    [r.note, ...r.gaps].filter(Boolean).join("; ").replaceAll("|", "/") + " |")].join("\n") + "\n";
writeFileSync(join(dir, "g7-scenarios.md"), md);
console.log(md);
const counts = rows.reduce((a, r) => (a[r.status] = (a[r.status] ?? 0) + 1, a), {});
console.log(JSON.stringify(counts));
