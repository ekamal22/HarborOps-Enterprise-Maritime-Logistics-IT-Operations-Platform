// Independent reconstruction of the unresolved mutants from the raw JSONL files (does not read survivor-status-derived.*).
import fs from "node:fs"; import crypto from "node:crypto";
const dir = process.argv[2], outFile = process.argv[3];
const files = fs.readdirSync(dir).filter(f => /^.+-\d+\.jsonl$/.test(f)).sort();
const rows = [];
for (const f of files) {
  const [, family, round] = /^(.*)-(\d+)\.jsonl$/.exec(f);
  fs.readFileSync(`${dir}/${f}`, "utf8").split(/\r?\n/).filter(Boolean).forEach((line, i) => {
    const o = JSON.parse(line); if (o.summary || o.name === undefined) return; rows.push({file: f, family, round: +round, line: i + 1, ...o, file_sha256: null});
  });
}
const sha = f => crypto.createHash("sha256").update(fs.readFileSync(`${dir}/${f}`)).digest("hex");
const fileHashes = Object.fromEntries(files.map(f => [f, sha(f)]));
const isControl = n => n === "unmutated control" || n === "unmutated control after the run";
const mutants = rows.filter(r => !isControl(r.name));
const nonKill = mutants.filter(r => r.status !== "KILLED");
const keys = [...new Set(mutants.map(r => r.family + "\u0000" + r.name))];
const unresolved = keys.filter(k => !mutants.some(r => r.family + "\u0000" + r.name === k && r.status === "KILLED"))
  .map(k => { const [family, name] = k.split("\u0000"); return {family, name, history: mutants.filter(r => r.family === family && r.name === name).map(r => ({file: r.file, line: r.line, status: r.status, failed_cases: r.failed_cases, seconds: r.seconds}))}; });
const controlsBad = rows.filter(r => isControl(r.name) && r.status !== "PASS");
const result = {jsonl_files: fileHashes, mutation_rows: mutants.length, distinct_mutants: keys.length, non_kill_rows: nonKill.length,
  distinct_never_killed: unresolved.length, controls_not_pass: controlsBad.map(r => ({file: r.file, name: r.name, status: r.status})), unresolved};
fs.writeFileSync(outFile, JSON.stringify(result, null, 2));
console.log(JSON.stringify({mutation_rows: result.mutation_rows, distinct_mutants: result.distinct_mutants, non_kill_rows: result.non_kill_rows, distinct_never_killed: result.distinct_never_killed, controls_not_pass: result.controls_not_pass.length}));
const byFam = {}; unresolved.forEach(u => byFam[u.family] = (byFam[u.family] || 0) + 1); console.log(byFam);
