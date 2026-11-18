// Builds the G18 assertion-by-assertion ledger from the raw evidence of one source-locked attempt.
// Usage: node scripts/reconcile-g18-assertions.mjs --evidence <dir> --sha <40 hex> --out <ledger.json> [--map tests/g18/assertion-map.json]
// An assertion is PASS only when every mapped witness exists and passed. A missing witness is NOT_EXECUTED, never PASS.
// G18-S24-A01 may be NOT_APPLICABLE only with the Q3 discovery record. The three G18-S25 assertions stay pending
// independent review whatever the technical result is.
import { readFileSync, readdirSync, existsSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";

const arg = (name, fallback) => { const i = process.argv.indexOf(name); return i > 0 ? process.argv[i + 1] : fallback; };
const evidence = arg("--evidence");
const sha = arg("--sha");
const out = arg("--out");
const mapPath = arg("--map", resolve(import.meta.dirname, "..", "tests/g18/assertion-map.json"));
if (!evidence || !/^[0-9a-f]{40}$/.test(sha ?? "") || !out) throw new Error("--evidence, --sha (40 hex) and --out are required");
const map = JSON.parse(readFileSync(mapPath, "utf8")).assertions;
const json = file => existsSync(join(evidence, file)) ? JSON.parse(readFileSync(join(evidence, file), "utf8")) : null;
const files = (dir, pattern) => existsSync(join(evidence, dir)) ? readdirSync(join(evidence, dir)).filter(f => pattern.test(f)).map(f => join(evidence, dir, f)) : [];
const decode = text => text.replace(/&quot;/g, "\"").replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&apos;/g, "'");

// ---- xUnit results from every TRX
const tests = [];
for (const file of files("trx", /\.trx$/i)) {
  const text = readFileSync(file, "utf8");
  for (const m of text.matchAll(/<UnitTestResult\b([^>]*)>/g)) {
    const name = decode((m[1].match(/testName="([^"]*)"/) ?? [])[1] ?? "");
    const outcome = (m[1].match(/outcome="([^"]*)"/) ?? [])[1] ?? "";
    tests.push({ name, outcome, file });
  }
}
// ---- web node tests (spec reporter lines)
const webLines = existsSync(join(evidence, "web-test.log")) ? readFileSync(join(evidence, "web-test.log"), "utf8").split("\n") : [];
const webPass = webLines.filter(l => /^\s*✔ /.test(l)).map(l => l.replace(/^\s*✔ /, "").replace(/ \([\d.]+ms\)\s*$/, ""));
const webFail = webLines.filter(l => /^\s*✖ /.test(l)).map(l => l.replace(/^\s*✖ /, ""));
// ---- Playwright JSON per engine
const browser = {};
for (const engine of ["chromium", "firefox", "webkit"]) {
  const report = json(`browser-${engine}.json`);
  browser[engine] = [];
  const walk = suite => {
    for (const spec of suite.specs ?? []) for (const t of spec.tests ?? [])
      browser[engine].push({ title: spec.title, project: t.projectName, status: (t.results?.at(-1)?.status) ?? t.status });
    for (const child of suite.suites ?? []) walk(child);
  };
  for (const suite of report?.suites ?? []) walk(suite);
}
const checks = Object.fromEntries((json("source-checks.json")?.results ?? []).map(r => [r.name, r.status]));
const stageFile = join(evidence, "stages.jsonl");
const stages = existsSync(stageFile)
  ? Object.fromEntries(readFileSync(stageFile, "utf8").split("
").filter(Boolean).map(l => JSON.parse(l)).map(s => [s.name, s.status])) : {};
const mutations = existsSync(join(evidence, "mutation-results.jsonl"))
  ? Object.fromEntries(readFileSync(join(evidence, "mutation-results.jsonl"), "utf8").split("\n").filter(Boolean).map(l => JSON.parse(l)).map(r => [r.id, r])) : {};
const journey = Object.fromEntries((json("journey.json")?.steps ?? []).map(s => [s.name, s.ok]));
const q3 = json("q3-disposition.json");
const retained = { before: json("retained-inventory-before.json"), after: json("retained-inventory-after.json") };

function evaluate(witness) {
  const [kind, ...rest] = witness.split(":");
  const name = rest.join(":");
  if (kind === "test") {
    const set = tests.filter(t => {
      if (!name.includes(".")) return t.name.includes("." + name + ".");
      const i = t.name.indexOf("." + name);
      return i >= 0 && (t.name.length === i + 1 + name.length || t.name[i + 1 + name.length] === "(");
    });
    if (set.length === 0) return { witness, status: "NOT_EXECUTED", detail: "no result" };
    const bad = set.filter(t => t.outcome !== "Passed");
    return { witness, status: bad.length ? "FAIL" : "PASS", detail: `${set.length} case(s)${bad.length ? ", failing: " + bad.map(b => b.name).join("; ") : ""}` };
  }
  if (kind === "browser") {
    const detail = []; let status = "PASS";
    for (const engine of ["chromium", "firefox", "webkit"]) {
      const hits = browser[engine].filter(t => t.title.includes(name) || t.title.includes(name.replace(/ \((English|Turkish)\)$/, "")));
      if (hits.length !== 2) { status = status === "FAIL" ? "FAIL" : "NOT_EXECUTED"; detail.push(`${engine}: ${hits.length} of 2`); continue; }
      if (hits.some(h => h.status !== "passed")) { status = "FAIL"; detail.push(`${engine}: ${hits.map(h => h.status).join(",")}`); }
      else detail.push(`${engine}: 2 passed`);
    }
    return { witness, status, detail: detail.join("; ") };
  }
  if (kind === "node") {
    const pass = webPass.filter(l => l.includes(name)); const fail = webFail.filter(l => l.includes(name));
    return { witness, status: fail.length ? "FAIL" : pass.length ? "PASS" : "NOT_EXECUTED", detail: `${pass.length} passed` };
  }
  if (kind === "check") return { witness, status: checks[name] ?? "NOT_EXECUTED", detail: "" };
  if (kind === "stage") return { witness, status: stages[name] ?? "NOT_EXECUTED", detail: "" };
  if (kind === "mutation") {
    const r = mutations[name];
    if (!r) return { witness, status: "NOT_EXECUTED", detail: "" };
    return { witness, status: r.status === "killed" ? "PASS" : "FAIL", detail: `mutant ${r.status}; failing tests: ${(r.failed_tests ?? []).length}` };
  }
  if (kind === "journey") return { witness, status: journey[name] === undefined ? "NOT_EXECUTED" : journey[name] ? "PASS" : "FAIL", detail: "" };
  if (kind === "q3") {
    if (name === "snapshot-discovery")
      return { witness, status: q3?.disposition === "NOT_APPLICABLE" || q3?.disposition === "PASS" ? q3.disposition : "NOT_EXECUTED", detail: q3?.reason ?? "" };
    if (name === "retained-resources-unchanged") {
      if (!retained.before || !retained.after) return { witness, status: "NOT_EXECUTED", detail: "" };
      return { witness, status: retained.before.digest === retained.after.digest ? "PASS" : "FAIL", detail: `${retained.before.digest.slice(0, 12)} -> ${retained.after.digest.slice(0, 12)}` };
    }
  }
  if (kind === "independent") return { witness, status: "PENDING_INDEPENDENT_REVIEW", detail: "requires a genuinely separate reviewer" };
  return { witness, status: "NOT_EXECUTED", detail: "unknown witness kind" };
}

const ledger = [];
for (const [id, entry] of Object.entries(map)) {
  const details = entry.witnesses.map(evaluate);
  let status;
  if (id.startsWith("G18-S25")) status = "PENDING_INDEPENDENT_REVIEW";
  else if (id === "G18-S24-A01") status = details[0].status === "NOT_APPLICABLE" ? "NOT_APPLICABLE" : details[0].status;
  else if (details.some(d => d.status === "FAIL")) status = "FAIL";
  else if (details.every(d => d.status === "PASS")) status = "PASS";
  else status = "NOT_EXECUTED";
  ledger.push({ id, status, source_sha: sha, witnesses: details });
}
const count = s => ledger.filter(l => l.status === s).length;
const summary = { total: ledger.length, PASS: count("PASS"), FAIL: count("FAIL"), NOT_EXECUTED: count("NOT_EXECUTED"), NOT_APPLICABLE: count("NOT_APPLICABLE"), PENDING_INDEPENDENT_REVIEW: count("PENDING_INDEPENDENT_REVIEW") };
writeFileSync(out, JSON.stringify({ gate: "G18", source_sha: sha, generated_at_utc: new Date().toISOString(), summary, ledger }, null, 2));
console.log(JSON.stringify(summary));
process.exit(summary.FAIL || summary.NOT_EXECUTED ? 1 : 0);
