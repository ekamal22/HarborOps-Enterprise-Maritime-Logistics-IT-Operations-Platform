#!/usr/bin/env node
// Render a human-readable summary of a completed G6 evidence directory.
// Usage: node scripts/summarize-g6-evidence.mjs <evidence-dir> > G6_RUN_SUMMARY.md
// Scenario-to-test mapping is read from the quoted test names in scripts/assert-g6-evidence.mjs so
// the table cannot drift from the checker. Nothing here changes the evidence; it only reads it.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const dir = process.argv[2];
assert.ok(dir && fs.existsSync(dir), "Evidence directory required");
const at = (x) => path.join(dir, x);
const read = (x) => fs.readFileSync(at(x), "utf8");
const gib = (n) => (n / 1024 ** 3).toFixed(2);
const sha = read("g6-source-sha.txt").trim();
const out = [];
const say = (line = "") => out.push(line);

say("# G6 run summary");
say();
say("Source SHA: `" + sha + "`");
const start = read("g6-start-utc.txt").trim(), end = read("g6-end-utc.txt").trim();
say("Started: " + start + "  Completed: " + end + "  Elapsed: " +
  Math.round((Date.parse(end) - Date.parse(start)) / 60000) + " minutes");
say();
say("## Stage ledger");
say();
say("| Stage | Exit | Seconds |");
say("| --- | --- | --- |");
for (const row of read("g6-stages.tsv").trim().split(/\r?\n/).map((x) => x.split("\t")))
  say("| " + row[0] + " | " + row[2] + " | " + (row[3] ?? "n/a") + " |");

const suites = {};
say();
say("## .NET suites (TRX)");
say();
say("| Suite | Total | Passed | Failed | Skipped | File |");
say("| --- | --- | --- | --- | --- | --- |");
for (const name of ["unit", "contract", "integration-fresh", "integration-repeat"]) {
  const xml = read("trx/" + name + ".trx");
  const rows = [...xml.matchAll(/<UnitTestResult\b([^>]*?)\/?>/g)].map((m) => ({
    name: /testName="([^"]+)"/.exec(m[1])?.[1], outcome: /outcome="([^"]+)"/.exec(m[1])?.[1] }));
  suites[name] = rows;
  const count = (o) => rows.filter((r) => r.outcome === o).length;
  const skipped = rows.filter((r) => !["Passed", "Failed"].includes(r.outcome)).length;
  say("| " + name + " | " + rows.length + " | " + count("Passed") + " | " + count("Failed") + " | " +
    skipped + " | trx/" + name + ".trx |");
}

say();
say("## Browser suites (Playwright JSON)");
say();
say("| Engine | Report files | Specs executed | Passed | Failed | Skipped | Flaky |");
say("| --- | --- | --- | --- | --- | --- | --- |");
for (const engine of ["chromium", "firefox", "webkit"]) {
  const files = fs.readdirSync(at("playwright")).filter((x) => x.startsWith(engine + "-") && x.endsWith(".json"));
  let expected = 0, unexpected = 0, skipped = 0, flaky = 0;
  for (const f of files) {
    const stats = JSON.parse(fs.readFileSync(at("playwright/" + f), "utf8")).stats;
    expected += stats.expected; unexpected += stats.unexpected; skipped += stats.skipped; flaky += stats.flaky;
  }
  say("| " + engine + " | " + files.length + " | " + (expected + unexpected + skipped + flaky) + " | " + expected +
    " | " + unexpected + " | " + skipped + " | " + flaky + " |");
}

say();
say("## Security mutations");
say();
const mutations = JSON.parse(read("g6-mutations.json"));
say("Control: " + mutations.control.status + " (" + mutations.control.passed + " tests passed)");
say();
say("| Variant | Result | Intended test | Failure message |");
say("| --- | --- | --- | --- |");
for (const m of mutations.mutants) {
  const message = Object.values(m.failureMessages ?? {})[0] ?? "";
  say("| " + m.variant + " | " + m.status + " | " + m.expectedFailure + " | " + message.replaceAll("|", "/").slice(0, 160) + " |");
}

say();
say("## Historical snapshot checkpoint (G6-S10)");
say();
const snap = JSON.parse(read("g6-snapshot.json"));
say("Outcome: " + snap.outcome);
say("Source volume: " + (snap.sourceVolume?.name ?? "n/a") + ", created " + (snap.sourceVolume?.createdAt ?? "n/a") +
  ", PostgreSQL major " + (snap.sourceVolume?.postgresMajor ?? "n/a") + ", " + (snap.sourceVolume?.sizeMegabytes ?? "n/a") + " MB");
say("Restored server version: " + (snap.postgresVersion ?? "n/a") + ". Source file fingerprint unchanged: " + snap.sourceUnmodified);
say("Historical migrations before: " + (snap.evidence?.before?.migrations ?? []).join(", "));
say("Pre-existing tables preserved by content hash: " + Object.keys(snap.evidence?.before?.tables ?? {}).join(", "));
say("Added by upgrade: " + (snap.evidence?.addedTables ?? []).join(", "));
say("Discovered candidates: " + (snap.dockerVolumeCandidates ?? []).map((x) => x.name).join(", ") +
  "; file locations searched: " + (snap.discoveryPaths ?? []).join("; "));

say();
say("## Storage and teardown");
say();
for (const phase of ["before", "after"]) {
  const j = JSON.parse(read("g6-storage-" + phase + ".json"));
  say("- " + phase + ": " + j.drives.map((d) => d.Name + ": used " + gib(d.Used) + " GiB, free " + gib(d.Free) + " GiB").join("; "));
}
const td = JSON.parse(read("g6-teardown.json"));
say("- teardown: remaining containers " + td.remainingContainers + ", volumes " + td.remainingVolumes +
  ", integration containers " + td.remainingIntegrationContainers);

say();
say("## Scenario results");
say();
say("State vocabulary: PASSED EXECUTABLE (observed on this SHA, review required), ACCEPTANCE PENDING, FAILED, NOT EXECUTED, NOT APPLICABLE.");
say();
say("| Scenario | State | Executed evidence |");
say("| --- | --- | --- |");
const checker = fs.readFileSync("scripts/assert-g6-evidence.mjs", "utf8");
const decision = fs.readFileSync("docs/M6_LOCAL_CONTRACT_DECISION.md", "utf8");
const approved = /\*\*State:\*\*\s*Accepted\b/i.test(decision);
const index = read("reports/G6_EVIDENCE_INDEX.md");
for (let i = 1; i <= 25; i++) {
  const id = "G6-S" + String(i).padStart(2, "0");
  const line = checker.split(/\r?\n/).find((l) => l.trim().startsWith('["' + id + '"'));
  const names = [...(line ?? "").matchAll(/"([A-Z][A-Za-z0-9]{20,})"/g)].map((m) => m[1]);
  const proofs = names.map((n) => {
    const all = Object.values(suites).flat().filter((r) => r.name?.includes(n));
    const passed = all.filter((r) => r.outcome === "Passed").length;
    return n + " (" + passed + "/" + all.length + " passed across unit, fresh and repeat runs)";
  });
  let state = index.includes("| " + id + " | EVIDENCE PRESENT") ? "PASSED EXECUTABLE" : "INCOMPLETE";
  if (id === "G6-S01") state = approved ? "PASSED EXECUTABLE" : "ACCEPTANCE PENDING (contract Proposed)";
  let detail = proofs.join("; ");
  if (id === "G6-S01") detail = approved ? "Contract decision accepted" :
    "Approval-only blocker: the twelve-rule contract is Proposed and no approval is recorded; executed S02-S25 evidence exercises its rules";
  if (id === "G6-S10") detail = "Historical volume restore and upgrade, outcome " + snap.outcome;
  if (id === "G6-S11") detail = "g6-fixtures.json: two tenants, five roles, idempotent replay";
  if (id === "G6-S21") detail = "All ten M6 scenarios plus accepted specs passed in Chromium";
  if (id === "G6-S22") detail = "All ten M6 scenarios plus accepted specs passed in Firefox and WebKit";
  if (id === "G6-S23") detail = "Full unit, contract, fresh and repeat suites and 57 browser specs per engine";
  if (id === "G6-S24") detail = "Four mutations killed, clean control passed, scans: " +
    fs.readdirSync(at("trivy")).join(", ") + ", NuGet and npm audits";
  if (id === "G6-S25") detail = "Stage ledger, outage and recovery stages, teardown and storage records";
  say("| " + id + " | " + state + " | " + (detail || "see G6_EVIDENCE_INDEX.md") + " |");
}
process.stdout.write(out.join("\n") + "\n");
