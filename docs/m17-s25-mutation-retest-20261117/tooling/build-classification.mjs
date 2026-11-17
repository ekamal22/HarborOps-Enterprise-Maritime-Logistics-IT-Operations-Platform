// Builds classification.json and the Markdown table from the per-mutant retest records plus the written dispositions.
import fs from "node:fs"; import path from "node:path";
const [base, outJson, outMd] = process.argv.slice(2);
const load = (dir, id) => { const f = path.join(base, dir, id + ".json"); return fs.existsSync(f) ? JSON.parse(fs.readFileSync(f, "utf8")) : null; };
const assertionDetail = (dir, rec, which) => {
  const run = rec?.[which]; if (!run || !run.failing_tests?.length) return [];
  const text = fs.readFileSync(path.join(base, dir, run.output), "utf8"); const out = [];
  for (const name of run.failing_tests) {
    const start = text.indexOf("not ok ", text.indexOf("not ok ") >= 0 ? text.indexOf(" - " + name) - 12 : 0);
    const i = text.indexOf(" - " + name); const rest = text.slice(i, i + 3000); const block = rest.split(/\n(?:ok|not ok) \d+ - /)[0];
    const err = /error: \|-\n([\s\S]*?)\n\s+code:/.exec(block)?.[1] ?? /error: (.*)/.exec(block)?.[1] ?? "";
    const kind = /name: '([A-Za-z]+)'/.exec(block)?.[1] ?? "";
    const where = /at |\(file:\/\/\/[^)]*?([^\/]+\.mjs:\d+):\d+\)/.exec(block)?.[1] ?? /([A-Za-z0-9._-]+\.mjs:\d+):\d+/.exec(block.slice(block.indexOf("stack:")))?.[1] ?? "";
    out.push({test: name, error_type: kind, assertion_message: err.split("\n").map(s => s.trim()).filter(Boolean).slice(0, 5).join(" | ").slice(0, 400), asserted_at: where});
  }
  return out;
};
const D = JSON.parse(fs.readFileSync(path.join(base, "dispositions.json"), "utf8"));
const rows = D.map(d => {
  const frozen = load(d.frozen_dir, d.id), cand = d.candidate_dir ? load(d.candidate_dir, d.id) : null;
  const pick = r => r && ({source_sha256: r.source_sha256, mutated_sha256: r.mutated_sha256, occurrences: r.mutation.occurrences_in_source, command: r.command.join(" "),
    control_before: {exit: r.control_before.exit, signal: r.control_before.signal, timed_out: r.control_before.timed_out, tests: r.control_before.tests, pass: r.control_before.pass},
    mutated: r.mutated_run && {exit: r.mutated_run.exit, signal: r.mutated_run.signal, timed_out: r.mutated_run.timed_out, tests: r.mutated_run.tests, pass: r.mutated_run.pass, fail: r.mutated_run.fail},
    control_after: r.control_after && {exit: r.control_after.exit, signal: r.control_after.signal, timed_out: r.control_after.timed_out, tests: r.control_after.tests, pass: r.control_after.pass},
    restored_hash_matches: r.restored_sha256_matches, raw_output: {control_before: r.control_before.output, mutated: r.mutated_run?.output, control_after: r.control_after?.output}});
  return {...d, mutation: frozen ? {target: frozen.target, from: frozen.mutation.from, to: frozen.mutation.to} : null,
    frozen_source: pick(frozen), frozen_failing: assertionDetail(d.frozen_dir, frozen, "mutated_run"),
    candidate_source: pick(cand), candidate_failing: cand ? assertionDetail(d.candidate_dir, cand, "mutated_run") : [],
    also_runs: (d.also ?? []).map(a => { const r = load(a.dir, d.id); return r ? {label: a.label, dir: a.dir, source: pick(r), failing: assertionDetail(a.dir, r, "mutated_run")} : null; }).filter(Boolean)};
});
fs.writeFileSync(outJson, JSON.stringify({frozen_source_commit: "634053c4d0c3a2cb8c51b2c29fda87b126e08993", candidate_commit: D.candidate_commit ?? null, mutants: rows}, null, 2));
const cell = s => String(s ?? "").replace(/\|/g, "\\|").replace(/\n/g, " ");
const md = ["| ID | Family | Mutant | Frozen source 634053c4 | Candidate source | Final disposition |", "| --- | --- | --- | --- | --- | --- |"];
for (const r of rows) {
  const f = r.frozen_source?.mutated, c = r.candidate_source?.mutated;
  const fr = !r.frozen_source ? "not executed" : !f ? "pattern not found" : r.frozen_source.control_before.exit !== 0 ? `control failed here (exit ${r.frozen_source.control_before.exit})` : f.exit === 0 ? "SURVIVED (exit 0)" : `KILLED (exit ${f.exit}, ${f.fail} failing)`;
  const cr = !r.candidate_source ? "n/a" : !c ? "pattern not found" : c.exit === 0 ? "SURVIVED (exit 0)" : `KILLED (exit ${c.exit}, ${c.fail} failing)`;
  md.push(`| ${r.id} | ${r.family} | ${cell(r.name)} | ${fr} | ${cr} | **${r.disposition}** |`);
}
fs.writeFileSync(outMd, md.join("\n") + "\n"); console.log(md.join("\n"));
