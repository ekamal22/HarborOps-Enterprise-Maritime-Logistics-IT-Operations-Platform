#!/usr/bin/env node
// Reconciles raw G9 evidence into the 25-scenario register. A scenario is PASS only when every
// named test, stack check, stage and mutation record it depends on exists and passed. Nothing is
// inferred: missing evidence is NOT EXECUTED or FAIL, never PASS. G9 acceptance is never decided here.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const evidence = process.argv[2];
assert.ok(evidence && fs.existsSync(evidence), "An evidence directory is required");
const read = file => fs.existsSync(path.join(evidence, file)) ? fs.readFileSync(path.join(evidence, file), "utf8") : null;
const json = file => { const text = read(file); return text ? JSON.parse(text) : null; };
const sha = (read("source-sha.txt") || "").trim();

const stages = new Map((read("stages.tsv") || "").split("\n").filter(Boolean).map(line => {
    const [label, , rc] = line.split("\t");
    return [label, Number(rc)];
}));
const stage = label => stages.get(label) === 0;

const outcomes = new Map();
for (const file of ["m9-integration.trx", "m9-integration-repeat.trx", "m9-unit.trx", "m9-contract.trx"]) {
    const text = read(path.join("trx", file));
    const rows = new Map();
    if (text) for (const m of text.matchAll(/<UnitTestResult\b([^>]*?)\/?>/g)) {
        const name = /testName="([^"]+)"/.exec(m[1])?.[1];
        const outcome = /outcome="([^"]+)"/.exec(m[1])?.[1];
        if (name) rows.set(name, outcome);
    }
    outcomes.set(file, rows);
}
const passedIn = (file, needle) => {
    const matches = [...(outcomes.get(file) ?? new Map())].filter(([name]) => name.includes(needle));
    return matches.length > 0 && matches.every(([, outcome]) => outcome === "Passed");
};
const dotnet = needle => passedIn("m9-integration.trx", needle) && passedIn("m9-integration-repeat.trx", needle);
const unit = needle => passedIn("m9-unit.trx", needle);

const stack = new Map((json("g9-stack-results.json")?.results ?? []).map(x => [x.id, x.status]));
const stackOk = id => stack.get(id) === "PASS";
const mutations = json("g9-mutations.json");
const scenarios = [];
const add = (id, title, checks, notes = "") => {
    const results = checks.map(([label, ok]) => ({ label, ok: Boolean(ok) }));
    const status = results.length === 0 ? "NOT EXECUTED" : results.every(x => x.ok) ? "PASS" : "FAIL";
    scenarios.push({ id, title, status, checks: results, notes });
};
const D = (name) => [`integration x2: ${name}`, dotnet(name)];
const S = (id) => [`stack check: ${id}`, stackOk(id)];

scenarios.push({ id: "G9-S01", title: "Accepted M9-01 local contract", status: "BLOCKED", checks: [],
    notes: "The six M9-01 local choices remain acceptance pending; code behavior is not approval." });
add("G9-S02", "Additive migration and populated M8 upgrade", [
    D("PopulatedM8ReceiptSurvivesM9UpgradeAndClaimedWorkPreventsDowngrade"),
    D("ModelSnapshot_MatchesCurrentEntityModel"), D("Migrations_AreDiscoveredInOrder"),
    D("RealM0M1M2UpgradePreservesLegacyAccountSessionAndBaselineOnRepeat"),
    S("fresh-volume-migrations-applied")]);
add("G9-S03", "Scoped SQL integrity and transition guards", [
    D("DirectSqlCannotForgeDuplicateOrTerminalWorkEvidence"), D("InvalidDirectSqlCannotSkipStateOrRewriteEvidence"),
    D("DirectSqlRedriveCannotBypassAttributedImmutableHistory"), S("worker-role-least-privilege")]);
add("G9-S04", "Unique immutable effect and attempt constraints", [
    D("DirectSqlCannotForgeDuplicateOrTerminalWorkEvidence"), D("ValidTimeScopedContainerAssignmentProducesExactlyOneImmutableEffect")]);
add("G9-S05", "Atomic due claim and fair polling", [
    D("PollingSelectsOnlyDueOrganizationsWithAnExclusiveCursorAndPartialIndexes"),
    D("DistinctConcurrentDbContextsCannotShareOneLease")]);
add("G9-S06", "Two live worker processes race", [
    D("TwoIndependentWorkersContendOnOneReceiptAndOnlyOneClaimsIt"),
    D("IntakeAndWorkerClaimSerializeOnTheOrganizationLockWithoutDeadlock")]);
add("G9-S07", "Tenant and source isolation", [D("ReferenceResolutionIsTenantTimeSafeAndNeverGuessed"),
    D("UnknownReferenceIsParkedAndCanBeRedrivenOnlyByManager")]);
add("G9-S08", "Lease renewal and clock boundaries", [D("HeartbeatExtendsOnlyLiveLeaseAndExpiredLeaseIsFencedThenRecovered"),
    ["unit: bounded defaults", unit("SafeDefaultsAreBoundedAndConfigured")]]);
add("G9-S09", "Expired lease recovery and stale fencing", [D("HeartbeatExtendsOnlyLiveLeaseAndExpiredLeaseIsFencedThenRecovered")]);
add("G9-S10", "Crash immediately after claim commit", [D("KilledWorkerAfterClaimAndBeforeEffectCommitIsRecoveredWithOneEffect")],
    "Real worker process killed after its claim committed; the effect insert was blocked in flight.");
add("G9-S11", "Crash during processing before COMMIT", [D("KilledWorkerAfterClaimAndBeforeEffectCommitIsRecoveredWithOneEffect")]);
add("G9-S12", "COMMIT acknowledgment ambiguity", [D("AmbiguousFinalizeCommitIsReconstructedFromDurableState")]);
add("G9-S13", "Duplicate delivery does not repeat resolution", [D("ReplayedDeliveryYieldsOneReceiptAndOneEffect"),
    S("e2e-worker-processes-resolved-and-unresolved-events")]);
add("G9-S14", "Equipment resolution is time and tenant safe", [D("ReferenceResolutionIsTenantTimeSafeAndNeverGuessed"),
    D("ValidTimeScopedContainerAssignmentProducesExactlyOneImmutableEffect")]);
add("G9-S15", "Transport and unknown references are not guessed", [D("ReferenceResolutionIsTenantTimeSafeAndNeverGuessed")]);
add("G9-S16", "Deterministic finite retry schedule", [D("TransientFailuresFollowBoundedScheduleAndStopAtFiveAttempts"),
    ["unit: deterministic bounded retry", unit("RetryIsDeterministicFiniteAndBounded")]]);
add("G9-S17", "Dead-letter and reconstruction evidence", [D("PermanentProcessorFailureIsDeadLetteredWithImmutableEvidence"),
    D("UnknownReferenceIsParkedAndCanBeRedrivenOnlyByManager")]);
add("G9-S18", "Authorized redrive is bounded and auditable", [D("UnknownReferenceIsParkedAndCanBeRedrivenOnlyByManager"),
    D("DirectSqlRedriveCannotBypassAttributedImmutableHistory"), S("operator-api-roles-and-redrive-ceiling")]);
add("G9-S19", "Tenant and operator security", [S("operator-api-roles-and-redrive-ceiling"), S("worker-role-least-privilege"),
    S("service-logs-contain-no-secrets"), D("UnknownReferenceIsParkedAndCanBeRedrivenOnlyByManager")]);
add("G9-S20", "Exact 10,000 unresolved-backlog cap", [D("UnresolvedCeilingCountsLeasedRetryAndReconciliationAtTheExactLimit"),
    D("LeasedAndReconciliationStatesStillConsumeAdmissionQuota"), D("Exact9999_10000_10001AndConcurrentCrossSourceAdmission")]);
add("G9-S21", "Organization suspension and resumed processing", [D("SuspendedOrganizationIsNotProcessedAndResumesExactlyOnce")]);
add("G9-S22", "Affected G0-G8 regression", [
    ["stage unit-tests", stage("unit-tests")], ["stage contract-tests", stage("contract-tests")],
    ["stage integration-tests", stage("integration-tests")], ["stage integration-repeat", stage("integration-repeat")],
    ["stage trx-summary", stage("trx-summary")], ["stage tracking-contract", stage("tracking-contract")],
    ["stage web-build", stage("web-build")], S("database-outage-and-recovery"), S("stopped-worker-leaves-pending-then-processes-once")],
    "Existing browser journeys are not rerun by this runner; see the final report for that boundary.");
add("G9-S23", "Adversarial mutation effectiveness", [
    ["clean control passed", mutations?.control?.status === "PASS"],
    ["twelve distinct families killed by intended tests", (mutations?.mutants ?? []).filter(x => x.status === "KILLED" && x.killedBy?.length > 0).length === 12]]);
const scans = ["postgres", "api", "web", "worker"].every(s => fs.existsSync(path.join(evidence, "trivy", s + ".json")));
add("G9-S24", "Dependency, image, operational and evidence safety", [
    ["nuget advisories", stage("nuget-advisories")], ["npm advisories", stage("npm-advisories")],
    ["image scans (exact source-labelled images)", stage("image-scans") && scans], S("images-carry-source-revision-label"),
    ["stack teardown", stage("stack-teardown")], ["evidence safety scan", stage("evidence-safety")],
    ["no leftover named resources", (read("teardown.txt") || "").trim() === "containers= volumes= networks= database="]]);
scenarios.push({ id: "G9-S25", title: "Independent source-locked review and explicit acceptance", status: "NOT EXECUTED",
    checks: [], notes: "Requires independent evidence review and a separate explicit acceptance decision." });

const executable = scenarios.filter(x => !["G9-S01", "G9-S25"].includes(x.id));
const technical = executable.every(x => x.status === "PASS") ? "PASS" : "FAILED";
const register = { sourceSha: sha, generatedAtUtc: new Date().toISOString(), scenarios };
fs.writeFileSync(path.join(evidence, "g9-scenarios.json"), JSON.stringify(register, null, 2));
fs.writeFileSync(path.join(evidence, "g9-technical-status.json"), JSON.stringify({
    sourceSha: sha, status: technical,
    note: "Technical execution result only. G9-S01 is BLOCKED and G9-S25 NOT EXECUTED; G9 acceptance is pending.",
    counts: Object.fromEntries(["PASS", "FAIL", "BLOCKED", "NOT EXECUTED"].map(k => [k, scenarios.filter(x => x.status === k).length]))
}, null, 2));
for (const s of scenarios) console.log(`${s.id} ${s.status}${s.status === "FAIL" ? " <- " + s.checks.filter(c => !c.ok).map(c => c.label).join("; ") : ""}`);
console.log("Technical status: " + technical);
process.exit(technical === "PASS" ? 0 : 1);
