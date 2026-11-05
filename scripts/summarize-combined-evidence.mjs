#!/usr/bin/env node
// Independent reparse of the combined M9/M16 raw evidence. It never trusts a runner summary: it re-reads
// stage ledgers, TRX files, Playwright JSON, probe JSON, mutation JSON, Trivy JSON and the SHA-256 manifest
// and recomputes every figure. Missing evidence is reported as missing, never as a pass.
// Usage: node scripts/summarize-combined-evidence.mjs <cg16 root> [output-prefix]
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const rootArg = process.argv[2];
assert.ok(rootArg && fs.existsSync(rootArg), "A cg16 verification root is required");
const root = path.resolve(rootArg);
const evidence = path.join(root, "evidence");
const read = rel => { const f = path.join(evidence, rel); return fs.existsSync(f) ? fs.readFileSync(f, "utf8") : null; };
const json = rel => { const t = read(rel); return t ? JSON.parse(t) : null; };
const list = rel => fs.existsSync(path.join(evidence, rel)) ? fs.readdirSync(path.join(evidence, rel)) : [];
const out = { generatedAtUtc: new Date().toISOString(), root, sourceSha: (read("source-sha.txt") || "").trim(), sections: {} };
const problems = [];
const section = (name, value, ok) => { out.sections[name] = { ok, ...value }; if (!ok) problems.push(name); };

// 1. stage ledger
const stageRows = (read("stages.tsv") || "").split("\n").filter(Boolean).slice(1).map(l => {
  const [stage, sha, rc, seconds, startUtc, executed] = l.split("\t");
  return { stage, sha, rc: Number(rc), seconds: Number(seconds), startUtc, executed };
});
section("stages", {
  count: stageRows.length, failed: stageRows.filter(s => s.rc !== 0).map(s => s.stage),
  shaMismatch: stageRows.filter(s => s.sha !== out.sourceSha).map(s => s.stage),
  rows: stageRows
}, stageRows.length > 0 && stageRows.every(s => s.rc === 0 && s.sha === out.sourceSha && s.executed === "yes"));

// 2. TRX
const trx = {};
for (const file of list("trx").filter(f => f.endsWith(".trx"))) {
  const text = read("trx/" + file);
  const c = /<Counters\b([^>]*)\/>/.exec(text)?.[1] ?? "";
  const num = k => Number(new RegExp(k + '="(\\d+)"').exec(c)?.[1] ?? NaN);
  const results = [...text.matchAll(/<UnitTestResult\b[^>]*outcome="([^"]+)"/g)].map(m => m[1]);
  const byOutcome = results.reduce((a, o) => ({ ...a, [o]: (a[o] ?? 0) + 1 }), {});
  trx[file] = { total: num("total"), executed: num("executed"), passed: num("passed"), failed: num("failed"),
    resultRows: results.length, byOutcome };
}
const trxOk = Object.values(trx).length >= 4 && Object.values(trx).every(t =>
  t.failed === 0 && t.passed > 0 && t.passed === t.total && t.resultRows === t.total);
section("dotnet-trx", { files: trx }, trxOk);
const integrationFresh = trx["cg16-integration.trx"], integrationRepeat = trx["cg16-integration-repeat.trx"];
section("integration-repeat-matches-first-pass", {
  first: integrationFresh?.total, repeat: integrationRepeat?.total
}, Boolean(integrationFresh && integrationRepeat && integrationFresh.total === integrationRepeat.total &&
  integrationFresh.failed === 0 && integrationRepeat.failed === 0));

// 3. probes
const tally = rel => {
  const rows = json(rel);
  if (!Array.isArray(rows)) return { missing: true };
  return { total: rows.length, failed: rows.filter(r => !r.pass).map(r => `${r.id} ${r.name}`) };
};
const http = tally("audit-http-results.json"), outage = tally("audit-outage-results.json");
section("audit-http-matrix", http, !http.missing && http.total > 0 && http.failed.length === 0);
section("audit-outage", outage, !outage.missing && outage.total > 0 && outage.failed.length === 0);
const stack = json("combined-stack/combined-stack-results.json");
section("combined-stack", {
  total: stack?.results?.length, failed: (stack?.results ?? []).filter(r => r.status !== "PASS").map(r => r.id),
  ids: (stack?.results ?? []).map(r => r.id)
}, Boolean(stack?.results?.length) && stack.results.every(r => r.status === "PASS"));

// 4. browsers
const engines = {};
for (const file of list("playwright").filter(f => f.endsWith(".json"))) {
  const m = /^(chromium|firefox|webkit)-(.+)\.json$/.exec(file);
  if (!m) continue;
  const data = json("playwright/" + file);
  const stats = data?.stats ?? {};
  const e = engines[m[1]] ??= { specs: [], expected: 0, unexpected: 0, flaky: 0, skipped: 0 };
  e.specs.push(m[2]);
  e.expected += stats.expected ?? 0; e.unexpected += stats.unexpected ?? 0;
  e.flaky += stats.flaky ?? 0; e.skipped += stats.skipped ?? 0;
}
const specFiles = Object.values(engines).map(e => e.specs.length);
section("browsers", { engines }, ["chromium", "firefox", "webkit"].every(n => engines[n] &&
  engines[n].unexpected === 0 && engines[n].flaky === 0 && engines[n].skipped === 0 && engines[n].expected > 0 &&
  engines[n].specs.length === specFiles[0]) &&
  ["audit", "tracking-ingress"].every(s => ["chromium", "firefox", "webkit"].every(n => engines[n]?.specs.includes(s))));

// 5. mutations
const mutation = json("combined-mutations.json");
section("mutations", {
  control: mutation?.mutants?.find(m => m.variant === "control")?.status,
  killed: (mutation?.mutants ?? []).filter(m => m.status === "killed").map(m => m.variant),
  notKilled: (mutation?.mutants ?? []).filter(m => !["control-clean", "killed"].includes(m.status)).map(m => `${m.variant}:${m.status}`)
}, Boolean(mutation) && mutation.mutants[0]?.status === "control-clean" && mutation.survivorsOrInvalid === 0 && mutation.mutants.length > 1);

// 6. scans
const scans = {};
for (const file of list("trivy").filter(f => f.endsWith(".json"))) {
  const data = json("trivy/" + file);
  const vulns = (data?.Results ?? []).flatMap(r => r.Vulnerabilities ?? []);
  scans[file] = { highOrCritical: vulns.filter(v => ["HIGH", "CRITICAL"].includes(v.Severity)).length };
}
section("image-scans", { scans }, ["postgres.json", "api.json", "web.json", "worker.json"].every(f => scans[f]?.highOrCritical === 0));
const deps = read("dependencies.log") ?? "";
section("dotnet-dependencies", { noVulnerable: /has no vulnerable packages/i.test(deps) || !/>\s*\S+\s+\d+\.\S+\s+\S+\s+(High|Critical)/i.test(deps) },
  deps.length > 0 && !/\b(High|Critical)\b/.test(deps.replace(/Severity/g, "")));

// 7. teardown and safety
const teardown = (read("teardown.txt") || "").trim();
section("teardown", { teardown }, teardown === "containers= volumes= sdk=");
const safety = read("evidence-safety.log") ?? "";
section("evidence-safety", { log: safety.trim().slice(0, 200) }, /redaction probe passed/.test(safety));

// 8. manifest verification (every file, both directions)
const manifestFile = path.join(root, "evidence-manifest.sha256");
let manifest = { present: fs.existsSync(manifestFile) };
if (manifest.present) {
  const entries = fs.readFileSync(manifestFile, "utf8").split("\n").filter(Boolean).map(l => {
    const m = /^([0-9a-f]{64}) [ *](.+)$/.exec(l); return m ? { hash: m[1], file: m[2].replace(/^\.\//, "") } : null;
  });
  const bad = [], missing = [];
  for (const e of entries) {
    const f = path.join(evidence, e.file);
    if (!fs.existsSync(f)) { missing.push(e.file); continue; }
    if (createHash("sha256").update(fs.readFileSync(f)).digest("hex") !== e.hash) bad.push(e.file);
  }
  const listed = new Set(entries.map(e => e.file));
  const unlisted = [];
  const walk = d => { for (const n of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, n.name);
    if (n.isDirectory()) walk(p); else { const rel = path.relative(evidence, p).split(path.sep).join("/"); if (!listed.has(rel)) unlisted.push(rel); } } };
  walk(evidence);
  manifest = { present: true, entries: entries.length, mismatched: bad, missing, unlisted };
}
section("manifest", manifest, Boolean(manifest.present) && manifest.mismatched.length === 0 && manifest.missing.length === 0 && manifest.unlisted.length === 0);

out.overall = problems.length === 0 ? "ALL SECTIONS CONSISTENT" : "SECTIONS NOT CONSISTENT: " + problems.join(", ");
const prefix = process.argv[3] ?? path.join(root, "independent-summary");
fs.writeFileSync(prefix + ".json", JSON.stringify(out, null, 2));
const lines = ["# Independent evidence reparse", "", `Source SHA: ${out.sourceSha}`, `Generated: ${out.generatedAtUtc}`, "", `Overall: ${out.overall}`, ""];
for (const [name, s] of Object.entries(out.sections)) lines.push(`- ${s.ok ? "OK  " : "FAIL"} ${name}`);
fs.writeFileSync(prefix + ".md", lines.join("\n") + "\n");
console.log(lines.join("\n"));
process.exit(problems.length ? 1 : 0);
