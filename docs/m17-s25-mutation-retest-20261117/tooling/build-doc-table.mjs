import fs from "node:fs";
const [cls, docIn, docOut, linkBase] = process.argv.slice(2);
const c = JSON.parse(fs.readFileSync(cls, "utf8")); const md = fs.readFileSync("evidence/classification-table.md", "utf8");
const esc = s => String(s ?? "").replace(/`/g, "'").replace(/\n/g, " ");
const run = (label, r, fail) => {
  if (!r) return `${label}: not executed`;
  const m = r.mutated; const cb = r.control_before, ca = r.control_after;
  if (!m) return `${label}: pattern not found or ambiguous (occurrences ${r.occurrences}); control before exit ${cb.exit}`;
  if (cb.exit !== 0) return `${label}: the unmutated control already fails on this host (exit ${cb.exit}; ${cb.pass}/${cb.tests} passed, the stand-in process is not visible because Get-CimInstance is unavailable), so the mutant is NOT counted as tested`;
  const status = `${m.exit === 0 ? "exit 0 (survived)" : `exit ${m.exit}`}, signal ${m.signal ?? "none"}, timeout ${m.timed_out ? "yes" : "no"}`;
  const ctl = `control before exit ${cb.exit} (${cb.pass}/${cb.tests}), control after exit ${ca?.exit} (${ca?.pass}/${ca?.tests}), restored hash ${r.restored_hash_matches ? "matches" : "DIFFERS"}`;
  const named = fail.length ? " Failing assertion: " + fail.map(f => `"${esc(f.test)}"${f.assertion_message ? " (" + (f.error_type ? f.error_type + ": " : "") + esc(f.assertion_message).slice(0, 200) + (f.asserted_at ? " at " + f.asserted_at : "") + ")" : ""}`).join("; ") : "";
  return `${label}: ${status}; ${m.pass}/${m.tests} tests passed; ${ctl}.${named}`;
};
let out = md + "\n";
for (const m of c.mutants) {
  const f = m.frozen_source;
  out += `\n#### ${m.id} ${m.name} (${m.family})\n\n`;
  out += `- Target: \`${m.mutation?.target}\`; original SHA-256 \`${(f?.source_sha256 ?? "").slice(0, 16)}\`, mutated \`${(f?.mutated_sha256 ?? "").slice(0, 16)}\`; locator occurrences in the source: ${f?.occurrences}.\n`;
  out += `- Mutation: replace \`${esc(m.mutation?.from).slice(0, 170)}\` with \`${esc(m.mutation?.to).slice(0, 120) || "(nothing)"}\`.\n`;
  out += `- Command: \`${esc(f?.command).replace(/^node /, "node ")}\`\n`;
  out += `- ${run("Frozen source", f, m.frozen_failing)}\n`;
  for (const a of m.also_runs ?? []) out += `- ${run(a.label, a.source, a.failing)}\n`;
  if (m.candidate_source) out += `- ${run("Candidate source", m.candidate_source, m.candidate_failing)}\n`;
  out += `- Raw output: \`${m.frozen_dir}/${m.id}.*.out.txt\`${m.candidate_dir ? `, \`${m.candidate_dir}/${m.id}.*.out.txt\`` : ""}.\n`;
  out += `- **Disposition ${m.disposition}.** ${esc(m.explanation)}\n- Demonstration: ${esc(m.demonstration)}\n`;
}
fs.writeFileSync(docOut, fs.readFileSync(docIn, "utf8").replace("TABLE_PLACEHOLDER", out));
