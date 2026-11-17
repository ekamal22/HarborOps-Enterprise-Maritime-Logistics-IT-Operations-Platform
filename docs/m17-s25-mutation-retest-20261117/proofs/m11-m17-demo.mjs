import fs from "node:fs"; import os from "node:os"; import path from "node:path"; import {spawnSync} from "node:child_process"; import {pathToFileURL} from "node:url"; import crypto from "node:crypto";
const src = process.argv[2], outDir = process.argv[3]; fs.mkdirSync(outDir, {recursive: true});
const out = {};
// ---- M11: existing evidence directory, original vs mutant cleanup script
const cleanup = fs.readFileSync(path.join(src, "scripts/cleanup-g17-owned.ps1"), "utf8");
const gate = "if (Test-Path -LiteralPath $evidencePath) { throw 'The cleanup evidence directory already exists. Never overwrite evidence' }";
if (cleanup.split(gate).length !== 2) throw new Error("M11 locator");
const rows = [];
for (const [label, text] of [["original", cleanup], ["M11 mutant", cleanup.replace(gate, "")]]) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "m11-")); fs.copyFileSync(path.join(src, "scripts/g17-docker-ownership.ps1"), path.join(dir, "g17-docker-ownership.ps1")); fs.writeFileSync(path.join(dir, "cleanup-g17-owned.ps1"), text);
  const evidence = path.join(dir, "evidence"); fs.mkdirSync(path.join(evidence, "m17-cleanup-state"), {recursive: true}); fs.writeFileSync(path.join(evidence, "precious.json"), '{"keep":true}'); fs.writeFileSync(path.join(evidence, "m17-owned-cleanup.json"), '{"previous":"record"}');
  const before = fs.readdirSync(evidence, {recursive: true}).sort().map(f => f + ":" + (fs.statSync(path.join(evidence, f)).isFile() ? crypto.createHash("sha256").update(fs.readFileSync(path.join(evidence, f))).digest("hex") : "dir"));
  const r = spawnSync("pwsh", ["-NoProfile", "-File", path.join(dir, "cleanup-g17-owned.ps1"), "-ProjectName", "ho-verify-m17-selftest-m11demo", "-EvidenceDirectory", evidence], {encoding: "utf8"});
  const after = fs.readdirSync(evidence, {recursive: true}).sort().map(f => f + ":" + (fs.statSync(path.join(evidence, f)).isFile() ? crypto.createHash("sha256").update(fs.readFileSync(path.join(evidence, f))).digest("hex") : "dir"));
  rows.push({variant: label, exit: r.status, first_error_line: (r.stderr + r.stdout).split(/\r?\n/).find(l => l.trim()), evidence_unchanged: JSON.stringify(before) === JSON.stringify(after)});
}
out.m11 = rows;
// ---- M17: alertCaseBound is identical with and without the guard over a generated corpus
const wb = fs.readFileSync(path.join(src, "scripts/g17-witness-binding.mjs"), "utf8"); const g = "if (start < 0) return null;";
if (wb.split(g).length !== 2) throw new Error("M17 locator");
const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "m17-")); fs.writeFileSync(path.join(tmp, "o.mjs"), wb); fs.writeFileSync(path.join(tmp, "m.mjs"), wb.replace(g, ""));
const O = await import(pathToFileURL(path.join(tmp, "o.mjs")).href), M = await import(pathToFileURL(path.join(tmp, "m.mjs")).href);
const markers = ["Readiness must not remain critical", "Latency recovers below", "S18-A03 repeat suppression", "A case that does not exist"];
const alertFile = fs.readFileSync(path.join(src, "monitoring/m17/alert-test.yaml"), "utf8");
const pieces = ["tests:", "  # Readiness must not remain critical after recovery.", "  # Latency recovers below the limit", "  # S18-A03 repeat suppression", "", "  - interval: 15s", "    input_series:", "      - series: 'x'", "        values: '0x1'", "    alert_rule_test:", "      - eval_time: 2m", "        alertname: HarborOpsDatabaseUnavailable", "        exp_alerts:", "          - exp_labels: {severity: critical}", "      - eval_time: 8m", "        alertname: HarborOpsDatabaseUnavailable", "        exp_alerts: []", "    promql_expr_test:", "      - expr: other", "  stray: value", "  # trailing comment", "note: Readiness must not remain critical"];
let seed = 12345; const rnd = n => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) % n;
let cases = 0, boundDiffer = 0, blockDiffer = 0, blockDiffExamples = [];
const check = text => { for (const m of markers) { cases++; if (O.alertCaseBound(text, m) !== M.alertCaseBound(text, m)) boundDiffer++; const a = O.alertCaseBlock(text, m), b = M.alertCaseBlock(text, m); if (a !== b) { blockDiffer++; if (blockDiffExamples.length < 3) blockDiffExamples.push({original: a, mutant: b}); } } };
check(alertFile);
for (let i = 0; i < 20000; i++) { const n = 1 + rnd(14); const text = Array.from({length: n}, () => pieces[rnd(pieces.length)]).join(rnd(4) === 0 ? "\n\n" : "\n") + (rnd(2) ? "\n" : ""); check(text); }
out.m17 = {cases_compared: cases, alertCaseBound_differences: boundDiffer, alertCaseBlock_differences: blockDiffer, alertCaseBlock_difference_examples: blockDiffExamples};
fs.writeFileSync(path.join(outDir, "m11-m17-demo.json"), JSON.stringify(out, null, 2)); console.log(JSON.stringify(out, null, 1));
