// Adversarial demonstration for M03 (size), M04 (extra extracted), M05 (unextracted): the original verifier and each
// mutant verify the same archives. Part A uses the real standard reader (honest extraction). Part B simulates an
// extraction that misbehaves, to show exactly which mutants would then differ.
import fs from "node:fs"; import os from "node:os"; import path from "node:path"; import {pathToFileURL} from "node:url";
const src = process.argv[2], outDir = process.argv[3];
const original = fs.readFileSync(path.join(src, "scripts/g17-evidence-archive.mjs"), "utf8");
const edit = {
  M03: ["else if (found.bytes !== file.bytes) problems.push", "else if (false) problems.push"],
  M04: ["for (const name of actualMap.keys()) if (!expected.has(name)) problems.push", "for (const name of actualMap.keys()) if (false) problems.push"],
  M05: ["if (!found) problems.push(`not extracted", "if (false) problems.push(`not extracted"]
};
const work = fs.mkdtempSync(path.join(os.tmpdir(), "archive-demo-"));
const withHook = text => text.replace("function extract(archivePath, destination) {", "function extractReal(archivePath, destination) {") +
  `\nfunction extract(a, d) { extractReal(a, d); const mode = process.env.G17_LIE; if (!mode) return;\n` +
  `  const files = fs.readdirSync(d, {recursive: true}).filter(f => fs.statSync(path.join(d, f)).isFile()).sort();\n` +
  `  if (mode === "extra") fs.writeFileSync(path.join(d, "planted.txt"), "x");\n` +
  `  if (mode === "skip") fs.rmSync(path.join(d, files[0]));\n` +
  `  if (mode === "truncate") fs.writeFileSync(path.join(d, files[0]), "");\n}\n`;
const mods = {};
for (const [id, text] of [["original", original], ...Object.entries(edit).map(([id, [f, t]]) => { if (original.split(f).length !== 2) throw new Error(id + " locator"); return [id, original.replace(f, t)]; })]) {
  const file = path.join(work, `archive-${id}.mjs`); fs.writeFileSync(file, withHook(text)); mods[id] = await import(pathToFileURL(file).href + "?v=" + id);
}
const evidence = path.join(work, "evidence"); fs.mkdirSync(path.join(evidence, "sub"), {recursive: true});
fs.writeFileSync(path.join(evidence, "a.txt"), "alpha\n"); fs.writeFileSync(path.join(evidence, "sub", "b.txt"), "bravo bravo\n"); fs.writeFileSync(path.join(evidence, "c.bin"), Buffer.alloc(300, 7));
const archive = path.join(work, "good.zip"); mods.original.createArchive(evidence, archive);
const inv = JSON.parse(fs.readFileSync(archive + ".inventory.json", "utf8"));
const variants = (name, mutate) => { const dir = path.join(work, name); fs.mkdirSync(dir); const a = path.join(dir, "x.zip"); fs.copyFileSync(archive, a); fs.copyFileSync(archive + ".sha256", a + ".sha256");
  const doc = structuredClone(inv); mutate(doc); fs.writeFileSync(a + ".inventory.json", JSON.stringify(doc)); return a; };
const cases = {
  "intact archive and inventory": d => {},
  "inventory claims a different size for a file": d => { d.files[0].bytes += 1; d.total_bytes += 1; },
  "inventory claims a different hash": d => { d.files[0].sha256 = "0".repeat(64); },
  "inventory omits a file the archive holds": d => { d.total_bytes -= d.files[0].bytes; d.files.shift(); d.file_count -= 1; },
  "inventory lists a file the archive lacks": d => { d.files.push({path: "ghost.txt", bytes: 1, sha256: "1".repeat(64)}); d.file_count += 1; d.total_bytes += 1; }
};
const rows = [];
for (const [label, mutate] of Object.entries(cases)) for (const lie of [null]) {
  const a = variants(label.replace(/\W+/g, "-"), mutate); const row = {part: "A honest reader", case: label};
  for (const id of Object.keys(mods)) { const r = mods[id].verifyArchive(a); row[id] = {ok: r.ok, problems: r.problems}; }
  rows.push(row);
}
for (const lie of ["extra", "skip", "truncate"]) {
  const a = variants("lie-" + lie, () => {}); const row = {part: "B misbehaving extraction (simulated)", case: `extraction ${lie}`}; process.env.G17_LIE = lie;
  for (const id of Object.keys(mods)) { const r = mods[id].verifyArchive(a); row[id] = {ok: r.ok, problems: r.problems}; }
  delete process.env.G17_LIE; rows.push(row);
}
const summary = rows.map(r => ({part: r.part, case: r.case, verdicts: Object.fromEntries(Object.keys(mods).map(id => [id, r[id].ok])),
  verdict_differs_from_original: Object.keys(mods).filter(id => id !== "original" && r[id].ok !== r.original.ok)}));
fs.mkdirSync(outDir, {recursive: true}); fs.writeFileSync(path.join(outDir, "archive-demo.json"), JSON.stringify({rows, summary}, null, 2));
console.log(JSON.stringify(summary, null, 1));
