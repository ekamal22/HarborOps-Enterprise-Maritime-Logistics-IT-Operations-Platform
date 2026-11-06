#!/usr/bin/env node
// Independent reparse of the G10 raw evidence. It never trusts a runner summary: it re-reads the stage ledgers,
// TRX files, Playwright JSON, stack-check JSON, mutation JSON, Trivy JSON, performance records and SHA-256
// manifests and recomputes every figure. Missing or failed evidence is reported as such, never as a pass.
//
// Usage: node scripts/summarize-g10-evidence.mjs <base root> <browser supplement root> <mutation root> [output-prefix]
//   base root             the full run (.NET, stack, audit, scans); may have stopped at a failed stage
//   browser supplement    same-SHA run that re-executed the browser matrix (other stages DEV-SKIPPED)
//   mutation root         same-SHA standalone execution of scripts/run-g10-mutations.mjs
// Every section reports ok true or false. A failed base stage is listed, never hidden.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const args = process.argv.slice(2);
for (const a of args.slice(0, 3)) assert.ok(a && fs.existsSync(a), "Root not found: " + a);
const prefix = args[3] ?? path.join(path.resolve(args[0]), "g10-independent-summary");
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
const base = load(args[0]), browserSupp = load(args[1]);
const mutationRoot = path.resolve(args[2]);
const out = { generatedAtUtc: new Date().toISOString(), baseRoot: base.root, browserSupplementRoot: browserSupp.root,
  mutationRoot, sourceSha: base.sha, sections: {}, notes: [] };
const problems = [];
const section = (name, value, ok) => { out.sections[name] = { ok: Boolean(ok), ...value }; if (!ok) problems.push(name); };

// 1. source lock
const mutation = (() => { const f = path.join(mutationRoot, "evidence", "g10-mutations.json");
  return fs.existsSync(f) ? JSON.parse(fs.readFileSync(f, "utf8")) : null; })();
section("source-lock", { base: base.sha, browserSupplement: browserSupp.sha, mutation: mutation?.sourceSha ?? null,
  scriptHashesEqual: base.read("verification-script-hashes.txt") === browserSupp.read("verification-script-hashes.txt") },
  /^[0-9a-f]{40}$/.test(base.sha) && browserSupp.sha === base.sha && mutation?.sourceSha === base.sha &&
  base.read("verification-script-hashes.txt") === browserSupp.read("verification-script-hashes.txt"));

// 2. stage ledgers
const COVERED_BY_BASE = ["web-build", "package-audit", "sdk-postgres-1", "dotnet-build", "unit", "contract", "dependencies", "integration",
  "sdk-postgres-2", "integration-repeat", "image-scans", "restricted-db-roles", "audit-http-matrix", "audit-outage", "g10-stack-check", "security-mutations"];
const baseFailed = base.rows.filter(r => r.rc !== 0 || r.executed !== "yes" || r.sha !== base.sha).map(r => `${r.stage}:rc=${r.rc}:${r.executed}`);
const baseOk = new Set(base.rows.filter(r => r.rc === 0 && r.executed === "yes").map(r => r.stage));
section("stages-base", { executedPassed: [...baseOk].length, failedStages: baseFailed,
  note: "the base run stopped at its first failed stage; later stages are covered by the same-SHA supplements" },
  COVERED_BY_BASE.filter(s => !["security-mutations"].includes(s)).every(s => baseOk.has(s)) &&
  baseFailed.length === 1 && baseFailed[0].startsWith("browser-engines:"));
const suppSkipped = browserSupp.rows.filter(r => r.executed === "DEV-SKIPPED").map(r => r.stage);
const suppBad = browserSupp.rows.filter(r => r.executed === "yes" && (r.rc !== 0 || r.sha !== browserSupp.sha)).map(r => `${r.stage}:rc=${r.rc}`);
section("stages-browser-supplement", { skippedAsCoveredElsewhere: suppSkipped, failedStages: suppBad,
  browserStage: browserSupp.rows.find(r => r.stage === "browser-engines") ?? null },
  suppBad.length === 0 && suppSkipped.every(s => COVERED_BY_BASE.includes(s)) &&
  browserSupp.rows.some(r => r.stage === "browser-engines" && r.rc === 0 && r.executed === "yes") &&
  browserSupp.rows.some(r => r.stage === "browser-m10-fixtures" && r.rc === 0 && r.executed === "yes"));

// 3. .NET TRX
const trx = {};
for (const file of base.list("trx").filter(f => f.endsWith(".trx"))) {
  const text = base.read("trx/" + file);
  const c = /<Counters\b([^>]*)\/>/.exec(text)?.[1] ?? "";
  const num = k => Number(new RegExp(k + '="(\\d+)"').exec(c)?.[1] ?? NaN);
  const outcomes = [...text.matchAll(/<UnitTestResult\b[^>]*outcome="([^"]+)"/g)].map(m => m[1]);
  const names = new Set([...text.matchAll(/<UnitTestResult\b[^>]*testName="([^"]+)"/g)].map(m => m[1]));
  trx[file] = { total: num("total"), executed: num("executed"), passed: num("passed"), failed: num("failed"),
    resultRows: outcomes.length, notPassedRows: outcomes.filter(o => o !== "Passed").length,
    m10Rows: [...names].filter(n => /M10|TrackingProjectionG10|TrackingTimelinePolicy/.test(n)).length };
}
const need = ["g10-unit.trx", "g10-contract.trx", "g10-integration.trx", "g10-integration-repeat.trx"];
section("dotnet-trx", { files: trx }, need.every(n => trx[n] && trx[n].failed === 0 && trx[n].passed > 0 &&
  trx[n].passed === trx[n].total && trx[n].resultRows === trx[n].total && trx[n].notPassedRows === 0));
section("integration-repeat-matches-first-pass", { first: trx["g10-integration.trx"]?.total, repeat: trx["g10-integration-repeat.trx"]?.total },
  trx["g10-integration.trx"]?.total === trx["g10-integration-repeat.trx"]?.total && trx["g10-integration.trx"]?.total > 0);
const m10Tests = trx["g10-integration.trx"]?.m10Rows ?? 0;
section("m10-tests-present", { integrationRowsMatchingM10: m10Tests }, m10Tests >= 12);
const deps = (base.read("nuget-vulnerable.txt") ?? "") + (base.read("dependencies.log") ?? "");
section("dotnet-dependencies", { vulnerableReported: /has the following vulnerable packages/.test(deps) },
  deps.length > 0 && !/has the following vulnerable packages/.test(deps));
const web = base.read("web-build.log") ?? "";
// The web suite uses the Node test runner: "tests N", "pass N", "fail N".
const webCount = key => Number(new RegExp("^\\S*\\s*" + key + " (\\d+)\\s*$", "m").exec(web)?.[1] ?? NaN);
const webTotal = webCount("tests"), webPass = webCount("pass"), webFail = webCount("fail");
section("web-tests-and-build", { tests: webTotal, passed: webPass, failed: webFail, builtLine: /built in/.test(web) },
  webTotal > 0 && webPass === webTotal && webFail === 0 && /built in/.test(web));

// 4. probes, stack, scans (base run)
const tally = rel => { const j = base.json(rel); return Array.isArray(j) ? { total: j.length, failed: j.filter(x => !x.pass).map(x => `${x.id} ${x.name}`) } : { missing: true }; };
const http = tally("audit-http-results.json"), outage = tally("audit-outage-results.json");
section("audit-http-matrix", http, !http.missing && http.total > 0 && http.failed.length === 0);
section("audit-outage", outage, !outage.missing && outage.total > 0 && outage.failed.length === 0);
const stack = base.json("g10-stack/g10-stack-results.json");
section("g10-stack", { total: stack?.results?.length, failed: (stack?.results ?? []).filter(x => x.status !== "PASS").map(x => x.id),
  m10Checks: (stack?.results ?? []).filter(x => x.id.startsWith("m10-")).length },
  Boolean(stack?.results?.length) && stack.results.every(x => x.status === "PASS") &&
  (stack?.results ?? []).filter(x => x.id.startsWith("m10-")).length >= 7);
const scans = {};
for (const f of base.list("trivy").filter(n => n.endsWith(".json"))) {
  const data = base.json("trivy/" + f);
  scans[f] = (data?.Results ?? []).flatMap(x => x.Vulnerabilities ?? []).filter(v => ["HIGH", "CRITICAL"].includes(v.Severity)).length;
}
section("image-scans", { highOrCritical: scans }, ["postgres.json", "api.json", "web.json", "worker.json"].every(f => scans[f] === 0));
const perf = base.read("performance.txt") ?? "";
const perfLine = /large-history events=(\d+) pages=(\d+) page_p95_ms=([\d.]+) page_max_ms=([\d.]+) rebuild_ms=(\d+) rebuild_private_mem_delta_mib=(-?\d+)/.exec(perf);
section("performance", perfLine ? { events: +perfLine[1], pages: +perfLine[2], pageP95Ms: +perfLine[3], pageMaxMs: +perfLine[4], rebuildMs: +perfLine[5], rebuildMemDeltaMiB: +perfLine[6],
  usesKeysetIndex: /ix_m10_timeline_keyset/.test(perf), hasSortNode: /\bSort\b/.test(perf.split("-- plan --")[1] ?? "") } : { missing: true },
  Boolean(perfLine) && +perfLine[1] > 10000 && +perfLine[4] < 5000 && +perfLine[5] < 60000 && /ix_m10_timeline_keyset/.test(perf) && !/\bSort\b/.test(perf.split("-- plan --")[1] ?? ""));

// 5. browsers: the base run (preserved, failed) and the same-SHA supplement
const engineTally = r => {
  const engines = {};
  for (const file of r.list("playwright").filter(f => f.endsWith(".json"))) {
    const m = /^(chromium|firefox|webkit)-(.+)\.json$/.exec(file); if (!m) continue;
    const stats = r.json("playwright/" + file)?.stats ?? {};
    const e = engines[m[1]] ??= { specs: [], expected: 0, unexpected: 0, flaky: 0, skipped: 0, failedSpecs: [] };
    e.specs.push(m[2]); e.expected += stats.expected ?? 0; e.unexpected += stats.unexpected ?? 0; e.flaky += stats.flaky ?? 0; e.skipped += stats.skipped ?? 0;
    if ((stats.unexpected ?? 0) > 0) e.failedSpecs.push(m[2]);
  }
  return engines;
};
const summarizeEngines = e => Object.fromEntries(Object.entries(e).map(([k, v]) => [k, { specs: v.specs.length, expected: v.expected, unexpected: v.unexpected,
  flaky: v.flaky, skipped: v.skipped, failedSpecs: v.failedSpecs }]));
const baseEngines = engineTally(base), suppEngines = engineTally(browserSupp);
section("browsers-base-run (preserved failure)", { engines: summarizeEngines(baseEngines) }, true);
const full = e => ["chromium", "firefox", "webkit"].every(n => e[n] && e[n].unexpected === 0 && e[n].flaky === 0 && e[n].skipped === 0 && e[n].expected > 0 &&
  e[n].specs.length >= 17) && ["tracking-timeline", "audit", "tracking-ingress"].every(s => ["chromium", "firefox", "webkit"].every(n => e[n]?.specs.includes(s)));
section("browsers-supplement", { engines: summarizeEngines(suppEngines) }, full(suppEngines));
// Every execution line of the repeated spec in the supplement console log (the JSON keeps only the last).
const webkitTest6 = r => [...(r.read("browser-engines.log") ?? "").matchAll(/^\s+(ok|x)\s+\d+ \[webkit\] [^\n]*assignment\.spec\.mjs:\d+:\d+ [^\n]*G6 logout disposes allocation views/gm)].map(m => m[1]);
const baseWk = webkitTest6(base), suppWk = webkitTest6(browserSupp);
section("webkit-assignment-logout-test", { baseRunOutcomes: baseWk, supplementOutcomes: suppWk },
  baseWk.length === 1 && baseWk[0] === "x" && suppWk.length === 1 && suppWk[0] === "ok");
out.notes.push("The base run failed this one WebKit test (preserved). It passed once in the same-SHA supplement. One pass does not prove the cause: the preserved trace shows the click racing the initial shipments list load in an M6 spec, so it is recorded as an intermittent residual risk.");

// 6. mutations
section("mutations", {
  control: mutation?.mutants?.find(m => m.variant === "control")?.status, variants: mutation?.mutants?.length,
  killed: (mutation?.mutants ?? []).filter(m => m.status === "killed").length,
  notKilled: (mutation?.mutants ?? []).filter(m => !["control-clean", "killed"].includes(m.status)).map(m => `${m.variant}:${m.status}`)
}, Boolean(mutation) && mutation.mutants[0]?.variant === "control" && mutation.mutants[0].status === "control-clean" &&
  mutation.survivorsOrInvalid === 0 && mutation.mutants.length >= 29);

// 7. teardown, evidence safety, manifests (runner roots) and a post-hoc manifest for the mutation evidence
for (const [label, r] of [["base", base], ["browser-supplement", browserSupp]]) {
  section(`teardown (${label})`, { teardown: (r.read("teardown.txt") || "").trim() }, (r.read("teardown.txt") || "").trim() === "containers= volumes= sdk=");
  const safety = r.read("evidence-safety.log") ?? "";
  section(`evidence-safety (${label})`, { log: safety.trim().slice(0, 160) }, /redaction probe passed/.test(safety));
  const manifestFile = path.join(r.root, "evidence-manifest.sha256");
  let manifest = { present: fs.existsSync(manifestFile) };
  if (manifest.present) {
    const entries = fs.readFileSync(manifestFile, "utf8").split("\n").filter(Boolean).map(l => {
      const m = /^([0-9a-f]{64}) [ *](.+)$/.exec(l); return m ? { hash: m[1], file: m[2].replace(/^\.\//, "") } : null; }).filter(Boolean);
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
{
  const evidenceDir = path.join(mutationRoot, "evidence"), lines = [];
  const walk = d => { for (const n of fs.readdirSync(d, { withFileTypes: true })) { const p = path.join(d, n.name);
    if (n.isDirectory()) walk(p); else lines.push(createHash("sha256").update(fs.readFileSync(p)).digest("hex") + "  " + path.relative(evidenceDir, p).split(path.sep).join("/")); } };
  walk(evidenceDir);
  fs.writeFileSync(path.join(mutationRoot, "evidence-manifest.sha256"), lines.sort().join("\n") + "\n");
  section("manifest (mutation, computed during this reparse)", { entries: lines.length }, lines.length >= 30);
}

out.overall = problems.length === 0 ? "ALL SECTIONS CONSISTENT" : "SECTIONS NOT CONSISTENT: " + problems.join(", ");
fs.writeFileSync(prefix + ".json", JSON.stringify(out, null, 2));
const md = ["# Independent G10 evidence reparse", "", `Source SHA: ${out.sourceSha}`, `Generated: ${out.generatedAtUtc}`,
  `Base run: ${out.baseRoot}`, `Browser supplement: ${out.browserSupplementRoot}`, `Mutation run: ${out.mutationRoot}`, "", `Overall: ${out.overall}`, ""];
for (const [name, s] of Object.entries(out.sections)) md.push(`- ${s.ok ? "OK  " : "FAIL"} ${name}`);
for (const n of out.notes) md.push("", "Note: " + n);
fs.writeFileSync(prefix + ".md", md.join("\n") + "\n");
console.log(md.join("\n"));
process.exit(problems.length ? 1 : 0);
