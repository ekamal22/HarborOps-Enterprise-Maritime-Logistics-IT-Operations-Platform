#!/usr/bin/env node
// Independent reparse of the combined M9/M16 raw evidence. It never trusts a runner summary: it re-reads the
// stage ledgers, TRX files, Playwright JSON, probe JSON, mutation JSON, Trivy JSON and SHA-256 manifests and
// recomputes every figure. Missing or failed evidence is reported as such, never as a pass.
//
// Usage: node scripts/summarize-combined-evidence.mjs <base cg16 root> [<supplement cg16 root>] [output-prefix]
// With one root the whole campaign must be in it. With two roots the base run supplies the .NET build, test
// and dependency stages and the supplement (same source SHA) supplies everything else; the supplement must
// mark exactly those .NET stages as DEV-SKIPPED and the base must show each of them executed and passed.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const args = process.argv.slice(2);
assert.ok(args[0] && fs.existsSync(args[0]), "A base cg16 verification root is required");
const supplementRoot = args[1] && fs.existsSync(args[1]) ? path.resolve(args[1]) : null;
const prefix = (supplementRoot ? args[2] : args[1]) ?? path.join(path.resolve(args[0]), "independent-summary");
const DOTNET_STAGES = ["sdk-postgres-1", "dotnet-build", "unit", "contract", "dependencies", "integration", "sdk-postgres-2", "integration-repeat"];

const load = rootPath => {
  const root = path.resolve(rootPath), evidence = path.join(root, "evidence");
  const read = rel => { const f = path.join(evidence, rel); return fs.existsSync(f) ? fs.readFileSync(f, "utf8") : null; };
  const json = rel => { const t = read(rel); return t ? JSON.parse(t) : null; };
  const list = rel => fs.existsSync(path.join(evidence, rel)) ? fs.readdirSync(path.join(evidence, rel)) : [];
  const sha = (read("source-sha.txt") || "").trim();
  const rows = (read("stages.tsv") || "").split("\n").filter(Boolean).slice(1).map(l => {
    const [stage, rowSha, rc, seconds, startUtc, executed] = l.split("\t");
    return { stage, sha: rowSha, rc: Number(rc), seconds: Number(seconds), startUtc, executed };
  });
  return { root, evidence, read, json, list, sha, rows };
};
const base = load(args[0]);
const supp = supplementRoot ? load(supplementRoot) : null;
const out = { generatedAtUtc: new Date().toISOString(), baseRoot: base.root, supplementRoot: supp?.root ?? null, sourceSha: base.sha, sections: {}, notes: [] };
const problems = [];
const section = (name, value, ok) => { out.sections[name] = { ok: Boolean(ok), ...value }; if (!ok) problems.push(name); };
const runs = supp ? [["base", base], ["supplement", supp]] : [["base", base]];
const runOf = supp ?? base;   // the run that must supply browsers and mutations

// 1. source lock
section("source-lock", { base: base.sha, supplement: supp?.sha ?? null,
  scriptHashesEqual: supp ? base.read("verification-script-hashes.txt") === supp.read("verification-script-hashes.txt") : true },
  /^[0-9a-f]{40}$/.test(base.sha) && (!supp || (supp.sha === base.sha &&
    base.read("verification-script-hashes.txt") === supp.read("verification-script-hashes.txt"))));

// 2. stage ledgers
const ledger = (r, allowSkippedDotnet) => {
  const bad = [];
  for (const row of r.rows) {
    const executedOk = row.executed === "yes" || (allowSkippedDotnet && row.executed === "DEV-SKIPPED" && DOTNET_STAGES.includes(row.stage));
    if (row.rc !== 0 || row.sha !== r.sha || !executedOk) bad.push(`${row.stage}:rc=${row.rc}:${row.executed}`);
  }
  return bad;
};
if (supp) {
  const baseFailed = ledger(base, false);
  const baseNames = new Set(base.rows.filter(r => r.rc === 0 && r.executed === "yes").map(r => r.stage));
  section("stages-base", { failedStages: baseFailed, rowCount: base.rows.length,
    note: "base run stopped at its first failed stage; the supplement covers the remainder" },
  DOTNET_STAGES.every(s => baseNames.has(s)) && baseFailed.every(f => f.startsWith("browser-engines:")));
  const skipped = supp.rows.filter(r => r.executed === "DEV-SKIPPED").map(r => r.stage);
  const suppBad = ledger(supp, true);
  section("stages-supplement", { failedStages: suppBad, skippedAsCoveredByBase: skipped, rowCount: supp.rows.length },
    suppBad.length === 0 && JSON.stringify([...skipped].sort()) === JSON.stringify([...DOTNET_STAGES].sort()) &&
    supp.rows.some(r => r.stage === "security-mutations" && r.rc === 0 && r.executed === "yes"));
} else {
  section("stages", { failedStages: ledger(base, false), rowCount: base.rows.length }, base.rows.length > 0 && ledger(base, false).length === 0);
}

// 3. .NET TRX (from the run that executed them: always the base)
const trx = {};
for (const file of base.list("trx").filter(f => f.endsWith(".trx"))) {
  const text = base.read("trx/" + file);
  const c = /<Counters\b([^>]*)\/>/.exec(text)?.[1] ?? "";
  const num = k => Number(new RegExp(k + '="(\\d+)"').exec(c)?.[1] ?? NaN);
  const rowsOutcome = [...text.matchAll(/<UnitTestResult\b[^>]*outcome="([^"]+)"/g)].map(m => m[1]);
  trx[file] = { total: num("total"), executed: num("executed"), passed: num("passed"), failed: num("failed"),
    resultRows: rowsOutcome.length, notPassedRows: rowsOutcome.filter(o => o !== "Passed").length };
}
const need = ["cg16-unit.trx", "cg16-contract.trx", "cg16-integration.trx", "cg16-integration-repeat.trx"];
section("dotnet-trx", { files: trx }, need.every(n => trx[n] && trx[n].failed === 0 && trx[n].passed > 0 &&
  trx[n].passed === trx[n].total && trx[n].resultRows === trx[n].total && trx[n].notPassedRows === 0));
section("integration-repeat-matches-first-pass", { first: trx["cg16-integration.trx"]?.total, repeat: trx["cg16-integration-repeat.trx"]?.total },
  trx["cg16-integration.trx"]?.total === trx["cg16-integration-repeat.trx"]?.total && trx["cg16-integration.trx"]?.total > 0);
const deps = base.read("dependencies.log") ?? "";
const depProjects = [...deps.matchAll(/project `([^`]+)` has no vulnerable packages/g)].map(m => m[1]);
section("dotnet-dependencies", { cleanProjects: depProjects }, depProjects.length >= 8 && !/\bHigh\b|\bCritical\b|\bModerate\b/.test(deps));

// 4. probes, in every run that executed them
for (const [label, r] of runs) {
  const tally = rel => { const rowsJson = r.json(rel); return Array.isArray(rowsJson) ? { total: rowsJson.length, failed: rowsJson.filter(x => !x.pass).map(x => `${x.id} ${x.name}`) } : { missing: true }; };
  const http = tally("audit-http-results.json"), outage = tally("audit-outage-results.json");
  section(`audit-http-matrix (${label})`, http, !http.missing && http.total > 0 && http.failed.length === 0);
  section(`audit-outage (${label})`, outage, !outage.missing && outage.total > 0 && outage.failed.length === 0);
  const stack = r.json("combined-stack/cg16-stack-results.json");
  section(`combined-stack (${label})`, { total: stack?.results?.length, failed: (stack?.results ?? []).filter(x => x.status !== "PASS").map(x => x.id) },
    Boolean(stack?.results?.length) && stack.results.every(x => x.status === "PASS"));
  const scans = {};
  for (const f of r.list("trivy").filter(n => n.endsWith(".json"))) {
    const data = r.json("trivy/" + f);
    scans[f] = (data?.Results ?? []).flatMap(x => x.Vulnerabilities ?? []).filter(v => ["HIGH", "CRITICAL"].includes(v.Severity)).length;
  }
  section(`image-scans (${label})`, { highOrCritical: scans }, ["postgres.json", "api.json", "web.json", "worker.json"].every(f => scans[f] === 0));
}

// 5. browsers
const engineTally = r => {
  const engines = {};
  for (const file of r.list("playwright").filter(f => f.endsWith(".json"))) {
    const m = /^(chromium|firefox|webkit)-(.+)\.json$/.exec(file); if (!m) continue;
    const stats = r.json("playwright/" + file)?.stats ?? {};
    const e = engines[m[1]] ??= { specs: [], expected: 0, unexpected: 0, flaky: 0, skipped: 0 };
    e.specs.push(m[2]); e.expected += stats.expected ?? 0; e.unexpected += stats.unexpected ?? 0; e.flaky += stats.flaky ?? 0; e.skipped += stats.skipped ?? 0;
  }
  return engines;
};
const engines = engineTally(runOf);
const specCounts = Object.values(engines).map(e => e.specs.length);
section("browsers", { run: supp ? "supplement" : "base", engines: Object.fromEntries(Object.entries(engines).map(([k, v]) => [k, { specs: v.specs.length, expected: v.expected, unexpected: v.unexpected, flaky: v.flaky, skipped: v.skipped }])) },
  ["chromium", "firefox", "webkit"].every(n => engines[n] && engines[n].unexpected === 0 && engines[n].flaky === 0 &&
    engines[n].skipped === 0 && engines[n].expected > 0 && engines[n].specs.length === specCounts[0] && engines[n].specs.length >= 17) &&
  ["audit", "tracking-ingress"].every(s => ["chromium", "firefox", "webkit"].every(n => engines[n]?.specs.includes(s))));
if (supp) {
  const b = engineTally(base);
  out.notes.push("Base run browser stage (preserved, failed): " + JSON.stringify(Object.fromEntries(Object.entries(b).map(([k, v]) => [k, { unexpected: v.unexpected, expected: v.expected }]))));
}

// 6. mutations
const mutation = runOf.json("combined-mutations.json");
section("mutations", {
  control: mutation?.mutants?.find(m => m.variant === "control")?.status, variants: mutation?.mutants?.length,
  killed: (mutation?.mutants ?? []).filter(m => m.status === "killed").length,
  notKilled: (mutation?.mutants ?? []).filter(m => !["control-clean", "killed"].includes(m.status)).map(m => `${m.variant}:${m.status}`),
  sourceShaMatches: mutation?.sourceSha === base.sha
}, Boolean(mutation) && mutation.mutants[0]?.variant === "control" && mutation.mutants[0].status === "control-clean" &&
  mutation.survivorsOrInvalid === 0 && mutation.mutants.length >= 18 && mutation.sourceSha === base.sha);

// 7. teardown, evidence safety, manifests
for (const [label, r] of runs) {
  section(`teardown (${label})`, { teardown: (r.read("teardown.txt") || "").trim() }, (r.read("teardown.txt") || "").trim() === "containers= volumes= sdk=");
  const safety = r.read("evidence-safety.log") ?? "";
  section(`evidence-safety (${label})`, { log: safety.trim().slice(0, 160) }, /redaction probe passed/.test(safety));
  const manifestFile = path.join(r.root, "evidence-manifest.sha256");
  let manifest = { present: fs.existsSync(manifestFile) };
  if (manifest.present) {
    const entries = fs.readFileSync(manifestFile, "utf8").split("\n").filter(Boolean).map(l => {
      const m = /^([0-9a-f]{64}) [ *](.+)$/.exec(l); return m ? { hash: m[1], file: m[2].replace(/^\.\//, "") } : null; });
    const mismatched = [], missing = [], listed = new Set(entries.map(e => e.file)), unlisted = [];
    for (const e of entries) {
      const f = path.join(r.evidence, e.file);
      if (!fs.existsSync(f)) { missing.push(e.file); continue; }
      if (createHash("sha256").update(fs.readFileSync(f)).digest("hex") !== e.hash) mismatched.push(e.file);
    }
    const walk = d => { for (const n of fs.readdirSync(d, { withFileTypes: true })) { const p = path.join(d, n.name);
      if (n.isDirectory()) walk(p); else { const rel = path.relative(r.evidence, p).split(path.sep).join("/"); if (!listed.has(rel)) unlisted.push(rel); } } };
    walk(r.evidence);
    manifest = { present: true, entries: entries.length, mismatched, missing, unlisted };
  }
  section(`manifest (${label})`, manifest, manifest.present && manifest.mismatched.length === 0 && manifest.missing.length === 0 && manifest.unlisted.length === 0);
}

out.overall = problems.length === 0 ? "ALL SECTIONS CONSISTENT" : "SECTIONS NOT CONSISTENT: " + problems.join(", ");
fs.writeFileSync(prefix + ".json", JSON.stringify(out, null, 2));
const md = ["# Independent evidence reparse", "", `Source SHA: ${out.sourceSha}`, `Generated: ${out.generatedAtUtc}`,
  `Base run: ${out.baseRoot}`, `Supplement run: ${out.supplementRoot ?? "none"}`, "", `Overall: ${out.overall}`, ""];
for (const [name, s] of Object.entries(out.sections)) md.push(`- ${s.ok ? "OK  " : "FAIL"} ${name}`);
for (const n of out.notes) md.push("", "Note: " + n);
fs.writeFileSync(prefix + ".md", md.join("\n") + "\n");
console.log(md.join("\n"));
process.exit(problems.length ? 1 : 0);
